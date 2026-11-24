using System.Collections.Generic;
using HarmonyLib;
using CombatExtended;
using Verse;
using RimWorld;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Utility class to evaluate mid-burst continuous tracking eligibility and physical turret traverse inertia.
    /// Implements angular acceleration and kinematic braking (deceleration) for NonSnapTurretExtension turrets
    /// based on actuatorStrength (torque), turretHeadWeight (moment of inertia), and NonSnapExtension.speed (max slewing speed).
    /// Also ensures top.CurRotation and shot headings remain strictly synchronized with NonSnapTurretRot at every phase
    /// of the tick (including beforeGunCompEq.verbTracker.VerbsTick() fires burst shots and before Building_CIWS_CE.Tick() completes).
    /// </summary>
    public static class TurretTrackingUtility
    {
        /// <summary>
        /// Base angular acceleration constant (degrees / tick^2) when actuatorStrength == turretHeadWeight (ratio = 1.0).
        /// At ratio = 1.0, accel = 0.15 deg/tick^2 (reaches 2.0 deg/tick in ~13 ticks / 0.22s).
        /// For a heavy GAU-8 turret (e.g. actuatorStrength = 2.0, turretHeadWeight = 8.0 -> ratio = 0.25),
        /// accel = 0.0375 deg/tick^2 (takes ~53 ticks / 0.88s to reach 2.0 deg/tick max speed and ~53 degrees to brake to a stop).
        /// </summary>
        public const float BaseTraverseAccelPerTickSq = 0.15f;

        public struct TurretTraverseState
        {
            public float AuthoritativeRot;
            public float CurrentAngularVelocity; // signed degrees/tick (+ clockwise, - counter-clockwise)
            public float MaxSpeed;               // NonSnapExtension.speed (degrees/tick)
            public float AccelPerTickSq;         // degrees/tick^2
            public float ActuatorStrength;
            public float HeadWeight;
            public int LastTickUpdated;
            public bool Initialized;
        }

        private static readonly Dictionary<int, TurretTraverseState> traverseStates = new Dictionary<int, TurretTraverseState>();

        public static bool TryGetTraverseState(int thingID, out TurretTraverseState state)
        {
            return traverseStates.TryGetValue(thingID, out state);
        }

        /// <summary>
        /// Returns the authoritative physical barrel rotation (0..360, 0=North, 90=East) for a turret.
        /// For NonSnap turrets, always returns NonSnapTurretRot (preventing vanilla TurretTopTick or CIWS from snapping ahead).
        /// </summary>
        public static float GetAuthoritativeBarrelRotation(Building_Turret turret)
        {
            if (turret is Building_TurretGunCE ceTurret)
            {
                if (ceTurret.NonSnap)
                {
                    if (traverseStates.TryGetValue(ceTurret.thingIDNumber, out var st) && st.Initialized)
                    {
                        return st.AuthoritativeRot;
                    }
                    return ceTurret.NonSnapTurretRot;
                }
                if (ceTurret.top != null)
                {
                    return ceTurret.top.CurRotation;
                }
            }
            return float.NaN;
        }

        public static bool CanTrackMidBurst(Thing caster)
        {
            if (caster == null) return false;

            // 1. Check if caster's ThingDef has TurretTrackingExtension
            var trackingExt = caster.def?.GetModExtension<TurretTrackingExtension>();
            if (trackingExt != null && trackingExt.enableMidBurstTracking)
            {
                return true;
            }

            // 2. Check if caster has CompTurretFCS with trackingAbility
            var fcsComp = caster.TryGetComp<CompTurretFCS>();
            if (fcsComp != null && fcsComp.HasFCS && fcsComp.ActiveStats != null && fcsComp.ActiveStats.trackingAbility)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Computes the angular acceleration (degrees/tick^2) for a NonSnap turret from actuatorStrength and turretHeadWeight.
        /// </summary>
        public static float GetAngularAcceleration(Building_TurretGunCE turret, out float actuatorStrength, out float headWeight)
        {
            Thing eq = turret.Gun;
            TurretRecoilExtension recoilExt = HarmonyPatches_TurretRecoil.GetRecoilExtension(turret, eq);
            TurretControlMode mode = HarmonyPatches_TurretRecoil.ResolveControlMode(turret, eq, recoilExt);
            actuatorStrength = HarmonyPatches_TurretRecoil.ResolveActuatorStrength(turret, eq, recoilExt, mode);
            headWeight = HarmonyPatches_TurretRecoil.ResolveTurretHeadWeight(turret, eq, recoilExt);

            float ratio = actuatorStrength / Mathf.Max(0.1f, headWeight);
            return Mathf.Max(0.002f, BaseTraverseAccelPerTickSq * ratio);
        }

        /// <summary>
        /// Steps the turret's physical traverse once per tick BEFORE Building_TurretGunCE.Tick() runs VerbsTick().
        /// This guarantees that when VerbsTick() fires a burst shot on this tick:
        /// 1. NonSnapTurretRot and top.CurRotation ALREADY have the exact inertial angle for this tick
        ///    (not the snapped target angle left over from Building_CIWS_CE.Tick() or TurretTop.TurretTopTick()).
        /// 2. CompTurretBarrel (muzzle flash), CompTurretSmoker (muzzle/shockwave smoke), and Verb_LaunchProjectileCE (bullet vector)
        ///    all read the exact same physical barrel angle as the rendered barrel graphic.
        /// </summary>
        public static void StepInertialTraversePreTick(Building_TurretGunCE turret)
        {
            if (turret == null || !turret.Spawned || !turret.NonSnap || turret.NonSnapExtension == null) return;

            int id = turret.thingIDNumber;
            int nowTick = Find.TickManager?.TicksGame ?? 0;

            if (!traverseStates.TryGetValue(id, out TurretTraverseState state) || !state.Initialized)
            {
                state = new TurretTraverseState
                {
                    AuthoritativeRot = Mathf.Repeat(turret.NonSnapTurretRot, 360f),
                    CurrentAngularVelocity = 0f,
                    LastTickUpdated = nowTick,
                    Initialized = true
                };
            }

            // Prevent double-stepping on the same tick
            if (state.LastTickUpdated == nowTick && state.MaxSpeed > 0f)
            {
                SyncTurretRotationFields(turret, state.AuthoritativeRot);
                return;
            }

            bool isActiveAndManned = turret.Active && (turret.MannableComp == null || turret.MannableComp.MannedNow);
            var verb = turret.AttackVerb;
            bool isBursting = verb != null && verb.state == VerbState.Bursting;
            bool canRotateThisTick = isActiveAndManned && (!isBursting || CanTrackMidBurst(turret));

            float startRot = state.AuthoritativeRot;
            // Sync if external code (like CompDelayedRotation mode swap) explicitly reset NonSnapTurretRot
            if (nowTick - state.LastTickUpdated > 5)
            {
                startRot = Mathf.Repeat(turret.NonSnapTurretRot, 360f);
                state.CurrentAngularVelocity = 0f;
            }

            float maxSpeed = Mathf.Max(0.01f, turret.NonSnapExtension.speed);
            float accel = GetAngularAcceleration(turret, out float actStrength, out float headWeight);
            float newVel = state.CurrentAngularVelocity;
            float newRot = startRot;

            if (canRotateThisTick)
            {
                // Set NonSnapTurretRot to startRot so turret.DeltaAngle computes from our true physical angle
                turret.NonSnapTurretRot = startRot;

                bool hasActiveTarget = turret.CurrentTarget != null && turret.CurrentTarget.IsValid;
                if (!hasActiveTarget)
                {
                    // No active target: brake smoothly to a stop under actuator damping
                    newVel = Mathf.MoveTowards(newVel, 0f, accel);
                    newRot = Mathf.Repeat(startRot + newVel, 360f);
                }
                else
                {
                    float deltaAngle = turret.DeltaAngle; // signed degrees in [-180, +180]
                    float dist = Mathf.Abs(deltaAngle);

                    if (dist <= 0.001f && Mathf.Abs(newVel) <= accel)
                    {
                        newVel = 0f;
                        newRot = Mathf.Repeat(startRot + deltaAngle, 360f);
                    }
                    else
                    {
                        float dir = Mathf.Sign(deltaAngle);

                        // Kinematic braking curve: v_brake = sqrt(2 * a * dist)
                        float brakingSpeed = Mathf.Sqrt(2f * accel * dist);
                        float desiredSpeed = Mathf.Min(maxSpeed, brakingSpeed);
                        float desiredVel = dir * desiredSpeed;

                        newVel = Mathf.MoveTowards(newVel, desiredVel, accel);

                        if (Mathf.Sign(newVel) == dir && Mathf.Abs(newVel) >= dist && brakingSpeed <= accel * 1.5f)
                        {
                            newRot = Mathf.Repeat(startRot + deltaAngle, 360f);
                            newVel = 0f;
                        }
                        else
                        {
                            newRot = Mathf.Repeat(startRot + newVel, 360f);
                        }
                    }
                }
            }
            else
            {
                newVel = 0f;
            }

            bool hasTargetAtPreTick = turret.CurrentTarget != null && turret.CurrentTarget.IsValid;
            state.AuthoritativeRot = newRot;
            state.CurrentAngularVelocity = newVel;
            state.MaxSpeed = maxSpeed;
            state.AccelPerTickSq = accel;
            state.ActuatorStrength = actStrength;
            state.HeadWeight = headWeight;
            // If CurrentTarget was Invalid at Prefix time, Building_TurretGunCE.Tick() may acquire a new target
            // mid-tick via TryStartShootSomething(); leave LastTickUpdated at nowTick - 1 so PostTick steps onto the new target immediately.
            state.LastTickUpdated = hasTargetAtPreTick ? nowTick : (nowTick - 1);
            traverseStates[id] = state;

            SyncTurretRotationFields(turret, newRot);
        }

        /// <summary>
        /// Restores the authoritative physical rotation after vanilla CE's Building_TurretGunCE.Tick()
        /// (which runs top.TurretTopTick() and linear NonSnapTurretRot += speed) and holds warmup at 1 tick
        /// until the heavy turret head has aligned with the target.
        /// Note: When a turret acquires a brand-new target INSIDE Building_TurretGunCE.Tick() (via TryStartShootSomething),
        /// CurrentTarget was Invalid during Prefix and only became valid mid-Tick; we step traverse immediately on that tick too.
        /// </summary>
        public static void EnforceAuthoritativeRotationPostTick(Building_TurretGunCE turret)
        {
            if (turret == null || !turret.Spawned || !turret.NonSnap) return;

            int id = turret.thingIDNumber;
            int nowTick = Find.TickManager?.TicksGame ?? 0;

            if (!traverseStates.TryGetValue(id, out TurretTraverseState state) || !state.Initialized)
            {
                StepInertialTraversePreTick(turret);
                if (!traverseStates.TryGetValue(id, out state)) return;
            }
            else if (state.LastTickUpdated != nowTick)
            {
                StepInertialTraversePreTick(turret);
                traverseStates.TryGetValue(id, out state);
            }

            SyncTurretRotationFields(turret, state.AuthoritativeRot);

            if (turret.CurrentTarget != null && turret.CurrentTarget.IsValid && turret.burstWarmupTicksLeft == 1)
            {
                float remainingAngle = Mathf.Abs(turret.DeltaAngle);
                float alignTolerance = Mathf.Min(state.MaxSpeed, Mathf.Max(0.25f, state.AccelPerTickSq * 3f));
                if (remainingAngle > alignTolerance)
                {
                    turret.burstWarmupTicksLeft = 2;
                }
            }
        }

        public static void SyncTurretRotationFields(Building_TurretGunCE turret, float rot)
        {
            turret.NonSnapTurretRot = rot;
            if (turret.top != null)
            {
                turret.top.CurRotation = rot;
            }
        }
    }

    /// <summary>
    /// Harmony patch for Verb_LaunchProjectileCE.LockRotationAndAngle getter.
    /// Unlocks rotation & angle calculation mid-burst when tracking is enabled.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "LockRotationAndAngle", MethodType.Getter)]
    public static class Patch_Verb_LaunchProjectileCE_LockRotationAndAngle
    {
        [HarmonyPostfix]
        public static void Postfix(Verb_LaunchProjectileCE __instance, ref bool __result)
        {
            if (__result && __instance != null && __instance.caster != null)
            {
                if (TurretTrackingUtility.CanTrackMidBurst(__instance.caster))
                {
                    __result = false;
                }
            }
        }
    }

    /// <summary>
    /// Harmony patches for Building_TurretGunCE.Tick:
    /// - Prefix runs BEFORE GunCompEq.verbTracker.VerbsTick(), stepping inertial rotation and setting top.CurRotation = NonSnapTurretRot
    ///   so any shot fired during VerbsTick() uses the true physical barrel angle (not the snapped target angle from Building_CIWS_CE.Tick() or TurretTopTick()).
    /// - Postfix (Priority.First) IMMEDIATELY overwrites vanilla CE's linear NonSnap step and TurretTopTick() snap before ANY other postfix
    ///   (including telemetry probes, CompTurretBarrel, or Building_CIWS_CE.Tick) can observe a snapped top.CurRotation.
    /// </summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.Tick))]
    public static class Patch_Building_TurretGunCE_Tick_TargetTracking
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Building_TurretGunCE __instance)
        {
            if (__instance != null && __instance.Spawned && __instance.NonSnap)
            {
                TurretTrackingUtility.StepInertialTraversePreTick(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        public static void Postfix(Building_TurretGunCE __instance)
        {
            if (__instance == null || !__instance.Spawned) return;

            if (__instance.NonSnap)
            {
                TurretTrackingUtility.EnforceAuthoritativeRotationPostTick(__instance);
            }
            else if (__instance.Active)
            {
                var verb = __instance.AttackVerb;
                if (verb != null && verb.state == VerbState.Bursting && TurretTrackingUtility.CanTrackMidBurst(__instance))
                {
                    __instance.top?.TurretTopTick();
                }
            }
        }
    }

    /// <summary>
    /// Fixes CombatExtended.Building_CIWS_CE.Tick() unconditionally snapping top.CurRotation = (CurrentTarget.Thing.DrawPos - DrawPos).AngleFlat()
    /// after base.Tick() on NonSnap CIWS turrets (such as the GAU-8 Avenger and M61 Vulcan).
    /// </summary>
    [HarmonyPatch(typeof(Building_CIWS_CE), nameof(Building_CIWS_CE.Tick))]
    public static class Patch_Building_CIWS_CE_Tick_PreventNonSnapOverride
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        public static void Postfix(Building_CIWS_CE __instance)
        {
            if (__instance != null && __instance.NonSnap)
            {
                TurretTrackingUtility.EnforceAuthoritativeRotationPostTick(__instance);
            }
        }
    }

    /// <summary>
    /// Fixes CombatExtended.Verb_LaunchProjectileCE.Retarget() snapping bt.top.CurRotation = (currentTarget.Cell.ToVector3Shifted() - bt.DrawPos).AngleFlat()
    /// mid-burst on NonSnap turrets.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "Retarget")]
    public static class Patch_Verb_LaunchProjectileCE_Retarget_PreventNonSnapOverride
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        public static void Postfix(Verb_LaunchProjectileCE __instance)
        {
            if (__instance?.caster is Building_TurretGunCE ceTurret && ceTurret.NonSnap)
            {
                float barrelRot = TurretTrackingUtility.GetAuthoritativeBarrelRotation(ceTurret);
                if (!float.IsNaN(barrelRot))
                {
                    TurretTrackingUtility.SyncTurretRotationFields(ceTurret, barrelRot);
                }
            }
        }
    }

    /// <summary>
    /// Anchors CE's base horizontal firing heading (Verb_LaunchProjectileCE.ShotRotation) to the turret's physical barrel angle
    /// when a NonSnap turret is slewing between targets (or tracking a moving target).
    /// Preserves target lead / visibility / circular shift offset relative to the target while preventing bullets
    /// from firing out of the side of the barrel before the heavy turret head has physically rotated to the new target.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "ShotRotation", new[] { typeof(Vector3), typeof(Vector3) })]
    public static class Patch_Verb_LaunchProjectileCE_ShotRotation_NonSnapPhysicalAnchor
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        public static void Postfix(Verb_LaunchProjectileCE __instance, Vector3 source, Vector3 targetPos, ref float __result)
        {
            if (__instance?.caster is Building_TurretGunCE ceTurret && ceTurret.NonSnap)
            {
                float barrelRot = TurretTrackingUtility.GetAuthoritativeBarrelRotation(ceTurret);
                if (!float.IsNaN(barrelRot))
                {
                    // In CE's ShotRotation coordinate space: (-90 + Rad2Deg * Atan2(dz, dx)) % 360
                    // RimWorld clockwise angle (0=North, 90=East) maps to CE shotRotation by:
                    //   ceBarrelRot = -barrelRot (normalized to [-180, +180]).
                    // Anchor the base shot heading strictly to the physical barrel rotation so that bullets,
                    // muzzle flash, and smoke are 100% locked to the barrel graphic even while slewing between targets.
                    __result = Mathf.Repeat(-barrelRot + 180f, 360f) - 180f;
                }
            }
        }
    }
}
