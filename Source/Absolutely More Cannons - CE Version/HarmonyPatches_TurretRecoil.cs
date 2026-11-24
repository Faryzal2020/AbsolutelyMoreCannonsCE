using System;
using System.Collections.Generic;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Replaces Combat Extended's handheld infantry shoulder-rifle recoil model (GetRecoilVec) for mounted turrets
    /// with an authentic mounted turret actuator & single-pulse jolt stabilization model:
    /// 1. Control Mode separation:
    ///    - MachineActuated (terminal/powered servo/FCS): Operator pawn skill has 0% effect on physical mount recoil.
    ///    - DirectMechanical (hand-cranked/manual pintle): Operator pawn ShootingAccuracy scales recoil management moderately.
    /// 2. Single-pulse initial jolt (up -> down -> tiny up -> stabilized center) scaled by recoilAmount and stabilized
    ///    at a rate governed by actuatorStrength.
    /// 3. Centered, symmetrically damped steady-state harmonic vibration (eliminating CE's 3:1 upward muzzle climb bias
    ///    and uncorrelated per-tick angular teleportation on high-RPM rotary cannons).
    /// 4. Caps ProjectileCE.AccuracyFactor burst-shot growth for turrets so 100+ shot bursts do not turn trees into 100% collision magnets.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_TurretRecoil
    {
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, int> numShotsFiredRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, int>("numShotsFired");

        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> lastRecoilDegRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("lastRecoilDeg");

        private struct BurstRecoilState
        {
            public int LastShotIndex;
            public int LastShotTick;
            public float PrevYawOffsetDeg;
            public float PrevPitchOffsetDeg;
        }

        private static readonly Dictionary<int, BurstRecoilState> turretRecoilStates = new Dictionary<int, BurstRecoilState>();

        private static readonly TurretRecoilExtension DefaultRecoilConfig = new TurretRecoilExtension();

        static HarmonyPatches_TurretRecoil()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.TurretRecoil");

            var recoilMethod = AccessTools.Method(typeof(Verb_LaunchProjectileCE), "GetRecoilVec");
            if (recoilMethod != null)
            {
                // Run after BallisticsTelemetry's Prefix (Priority.First), replacing vanilla GetRecoilVec for mounted turrets
                harmony.Patch(
                    original: recoilMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretRecoil), nameof(Prefix_GetRecoilVec)) { priority = Priority.High });
            }

            var launchMethod = AccessTools.Method(
                typeof(ProjectileCE),
                nameof(ProjectileCE.Launch),
                new[] { typeof(Thing), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Thing), typeof(float) });
            if (launchMethod != null)
            {
                harmony.Patch(
                    original: launchMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretRecoil), nameof(Prefix_ProjectileLaunch_CapAccuracyFactor)));
            }
        }

        /// <summary>
        /// Determines whether a verb belongs to a mounted turret that should use the mounted recoil model.
        /// Applies automatically to all Building_Turret casters and any weapon with RecoilPattern.Mounted or TurretRecoilExtension.
        /// </summary>
        public static bool IsMountedTurretVerb(Verb_LaunchProjectileCE verb, out Building_Turret turret)
        {
            turret = null;
            if (verb == null || verb.caster == null) return false;

            turret = verb.caster as Building_Turret ?? (verb.caster.ParentHolder as Building_Turret);
            if (turret != null) return true;

            if (verb.VerbPropsCE != null && verb.VerbPropsCE.recoilPattern == RecoilPattern.Mounted)
                return true;

            if (verb.EquipmentSource?.def?.HasModExtension<TurretRecoilExtension>() ?? false)
                return true;

            return false;
        }

        /// <summary>
        /// Resolves the effective TurretRecoilExtension for a turret/weapon, or returns the default configuration.
        /// </summary>
        public static TurretRecoilExtension GetRecoilExtension(Building_Turret turret, Thing equipment)
        {
            if (equipment?.def != null)
            {
                var eqExt = equipment.def.GetModExtension<TurretRecoilExtension>();
                if (eqExt != null) return eqExt;
            }

            if (turret?.def != null)
            {
                var tExt = turret.def.GetModExtension<TurretRecoilExtension>();
                if (tExt != null) return tExt;
            }

            if (turret is Building_TurretGun btg && btg.gun?.def != null)
            {
                var gExt = btg.gun.def.GetModExtension<TurretRecoilExtension>();
                if (gExt != null) return gExt;
            }

            return DefaultRecoilConfig;
        }

        /// <summary>
        /// Resolves whether a turret is MachineActuated (terminal/servo/powered) or DirectMechanical (manual mount).
        /// Priority:
        /// 1. Explicit controlMode in TurretRecoilExtension
        /// 2. Explicit controlMode in CompTurretBarrel / TurretBarrelExtension
        /// 3. AutoDetect:
        ///    - Has CompTurretFCS (unmanned FCS turret) -> MachineActuated
        ///    - Has CompPowerTrader (powered motor/terminal turret) -> MachineActuated
        ///    - Has NonSnapTurretExtension (heavy mechanized traverse drive) -> MachineActuated
        ///    - Otherwise (unpowered manned pintle/hand-crank mount) -> DirectMechanical
        /// </summary>
        public static TurretControlMode ResolveControlMode(Building_Turret turret, Thing equipment, TurretRecoilExtension recoilExt)
        {
            if (recoilExt != null && recoilExt.controlMode != TurretControlMode.AutoDetect)
                return recoilExt.controlMode;

            if (turret != null)
            {
                var barrelComp = turret.TryGetComp<CompTurretBarrel>();
                if (barrelComp?.Extension != null && barrelComp.Extension.controlMode != TurretControlMode.AutoDetect)
                    return barrelComp.Extension.controlMode;

                var barrelExt = turret.def?.GetModExtension<TurretBarrelExtension>();
                if (barrelExt != null && barrelExt.controlMode != TurretControlMode.AutoDetect)
                    return barrelExt.controlMode;

                if (turret.TryGetComp<CompTurretFCS>() != null)
                    return TurretControlMode.MachineActuated;

                if (turret.TryGetComp<CompPowerTrader>() != null)
                    return TurretControlMode.MachineActuated;

                if (turret.def != null && turret.def.HasModExtension<NonSnapTurretExtension>())
                    return TurretControlMode.MachineActuated;
            }

            return TurretControlMode.DirectMechanical;
        }

        /// <summary>
        /// Resolves the effective actuator strength (> 0) for the turret.
        /// Priority:
        /// 1. Explicit actuatorStrength on CompTurretBarrel / TurretBarrelExtension (if > 0)
        /// 2. Explicit actuatorStrength on TurretRecoilExtension (if != 1.0 or explicitly configured)
        /// 3. Auto-scaled default based on turret footprint size and control mode.
        /// </summary>
        public static float ResolveActuatorStrength(Building_Turret turret, Thing equipment, TurretRecoilExtension recoilExt, TurretControlMode mode)
        {
            if (turret != null)
            {
                var barrelComp = turret.TryGetComp<CompTurretBarrel>();
                if (barrelComp?.Extension != null && barrelComp.Extension.actuatorStrength > 0f)
                    return Mathf.Max(0.1f, barrelComp.Extension.actuatorStrength);

                var barrelExt = turret.def?.GetModExtension<TurretBarrelExtension>();
                if (barrelExt != null && barrelExt.actuatorStrength > 0f)
                    return Mathf.Max(0.1f, barrelExt.actuatorStrength);
            }

            if (recoilExt != null && recoilExt != DefaultRecoilConfig && recoilExt.actuatorStrength > 0f)
            {
                return Mathf.Max(0.1f, recoilExt.actuatorStrength);
            }

            // Automatic baseline from turret footprint size and control mode
            float baseStrength = mode == TurretControlMode.MachineActuated ? 1.25f : 0.85f;
            if (turret?.def != null)
            {
                int area = turret.def.size.x * turret.def.size.z;
                if (area >= 16) baseStrength *= 1.35f;      // 4x4+ heavy mounts (e.g. GAU-8, M247, Naval guns)
                else if (area >= 9) baseStrength *= 1.15f;  // 3x3 medium mounts (e.g. Vulcan, BMP-2, Flak 38)
            }

            return Mathf.Max(0.1f, baseStrength);
        }

        /// <summary>
        /// Resolves the rotational inertia / weight of the turret head (> 0).
        /// Priority:
        /// 1. Explicit turretHeadWeight on CompTurretBarrel / TurretBarrelExtension (if > 0)
        /// 2. Explicit turretHeadWeight on TurretRecoilExtension (if > 0)
        /// 3. Auto-scaled default based on turret footprint size.
        /// </summary>
        public static float ResolveTurretHeadWeight(Building_Turret turret, Thing equipment, TurretRecoilExtension recoilExt)
        {
            if (turret != null)
            {
                var barrelComp = turret.TryGetComp<CompTurretBarrel>();
                if (barrelComp?.Extension != null && barrelComp.Extension.turretHeadWeight > 0f)
                    return Mathf.Max(0.1f, barrelComp.Extension.turretHeadWeight);

                var barrelExt = turret.def?.GetModExtension<TurretBarrelExtension>();
                if (barrelExt != null && barrelExt.turretHeadWeight > 0f)
                    return Mathf.Max(0.1f, barrelExt.turretHeadWeight);
            }

            if (recoilExt != null && recoilExt.turretHeadWeight > 0f)
            {
                return Mathf.Max(0.1f, recoilExt.turretHeadWeight);
            }

            if (turret?.def != null)
            {
                int area = turret.def.size.x * turret.def.size.z;
                if (area >= 25) return 10.0f; // 5x5+ massive naval/heavy artillery turrets
                if (area >= 16) return 6.0f;  // 4x4 heavy turrets (GAU-8, M247, etc.)
                if (area >= 9) return 3.0f;   // 3x3 medium turrets (M61 Vulcan, BMP-2, Flak 38)
            }

            return 1.5f; // 2x2 light turrets
        }

        /// <summary>
        /// Evaluates the single-pulse damped jolt waveform for normalized progress u in [0, 1]:
        /// - u in [0.00, 0.35]: Sharp initial upward jolt peaking at +1.0
        /// - u in [0.35, 0.70]: Actuator counter-torque dip reaching -0.35
        /// - u in [0.70, 0.90]: Tiny secondary rebound reaching +0.10
        /// - u in [0.90, 1.00]: Settles smoothly to 0.0 (stabilized center)
        /// </summary>
        public static float EvaluateSinglePulseJolt(float u)
        {
            if (u <= 0f || u >= 1f) return 0f;

            if (u < 0.35f)
            {
                // Half-sine rise from 0 to +1.0 and back to 0 at u=0.35
                float phase = (u / 0.35f) * Mathf.PI;
                return Mathf.Sin(phase);
            }
            if (u < 0.70f)
            {
                // Half-sine counter-dip from 0 to -0.35 and back to 0 at u=0.70
                float phase = ((u - 0.35f) / 0.35f) * Mathf.PI;
                return -0.35f * Mathf.Sin(phase);
            }
            if (u < 0.90f)
            {
                // Tiny rebound from 0 to +0.10 and back to 0 at u=0.90
                float phase = ((u - 0.70f) / 0.20f) * Mathf.PI;
                return 0.10f * Mathf.Sin(phase);
            }

            return 0f;
        }

        public static bool Prefix_GetRecoilVec(Verb_LaunchProjectileCE __instance, ref float rotation, ref float angle)
        {
            if (!IsMountedTurretVerb(__instance, out Building_Turret turret))
            {
                return true; // Non-turret / standard pawn weapon: run vanilla CE GetRecoilVec
            }

            int numShotsFired = numShotsFiredRef(__instance);
            int casterId = turret?.thingIDNumber ?? __instance.caster.thingIDNumber;
            int nowTick = Find.TickManager?.TicksGame ?? 0;

            if (numShotsFired <= 0)
            {
                // First shot of burst (index 0): zero recoil, reset state
                turretRecoilStates[casterId] = new BurstRecoilState
                {
                    LastShotIndex = 0,
                    LastShotTick = nowTick,
                    PrevYawOffsetDeg = 0f,
                    PrevPitchOffsetDeg = 0f
                };
                return false;
            }

            float recoil = __instance.RecoilAmount;
            if (recoil <= 0f)
            {
                return false;
            }

            Thing equipment = __instance.EquipmentSource;
            TurretRecoilExtension ext = GetRecoilExtension(turret, equipment);
            TurretControlMode controlMode = ResolveControlMode(turret, equipment, ext);
            float actuatorStrength = ResolveActuatorStrength(turret, equipment, ext, controlMode);

            // 1. Operator Skill Factor:
            //    - MachineActuated (controlled via terminal/powered servo): Pawn has ZERO effect on recoil (1.0x).
            //    - DirectMechanical (direct manual control): Low-skill operator has worse recoil management,
            //      scaling smoothly from 1.0x (at ShootingAccuracy >= 4.5) up to (1.0 + directControlMaxSkillPenalty) (at ShootingAccuracy <= 0.5).
            float operatorFactor = 1.0f;
            if (controlMode == TurretControlMode.DirectMechanical)
            {
                float shootAcc = Mathf.Clamp(__instance.ShootingAccuracy, 0.5f, 4.5f);
                float normalizedDeficit = (4.5f - shootAcc) / 4.0f; // 0.0 at 4.5 acc, 1.0 at 0.5 acc
                operatorFactor = 1.0f + normalizedDeficit * Mathf.Max(0f, ext.directControlMaxSkillPenalty);
            }

            // 2. Single-Pulse Initial Jolt (Up -> Down -> Tiny Up -> Center/Stabilized):
            //    How fast it stabilizes is governed by actuatorStrength: stronger actuators settle in fewer shots
            //    and reduce the peak jolt excursion.
            int settleShots = Mathf.Max(2, Mathf.RoundToInt(ext.baseSettleShots / Mathf.Max(0.25f, actuatorStrength)));
            float joltPitchDeg = 0f;
            float transientMultiplier = 1.0f;

            if (numShotsFired <= settleShots)
            {
                // Map shot 1..settleShots across the single-pulse waveform
                // Shot 1 starts at the first positive peak (u = 0.175), and shot settleShots reaches 1.0 (0 offset)
                float u = settleShots <= 1
                    ? 1f
                    : Mathf.Lerp(0.175f, 1.0f, (numShotsFired - 1f) / (settleShots - 1f));

                float pulse = EvaluateSinglePulseJolt(u);
                float joltAmplitude = recoil * ext.initialJoltMultiplier / Mathf.Sqrt(actuatorStrength);
                joltPitchDeg = pulse * joltAmplitude * operatorFactor;

                // Slightly wider lateral vibration during the initial impulse transient before gyro/actuator lock-in
                transientMultiplier = Mathf.Lerp(1.35f, 1.0f, (float)numShotsFired / settleShots);
            }

            // 3. Steady-State Centered Harmonic Vibration (symmetrical around line of fire, no 3:1 upward climb):
            float steadyAmp = (recoil * ext.steadyStateVibration / actuatorStrength) * operatorFactor * transientMultiplier;
            float targetYawNoise = Rand.Range(-steadyAmp, steadyAmp);
            float vertAmp = steadyAmp * ext.verticalVibrationRatio;
            float targetPitchNoise = Rand.Range(-vertAmp, vertAmp);

            // 4. Temporal Angular Smoothing across consecutive burst shots:
            //    Prevents 3900 RPM rotary cannons from teleporting across the recoil cone every 16ms tick.
            if (!turretRecoilStates.TryGetValue(casterId, out BurstRecoilState prevState)
                || numShotsFired < prevState.LastShotIndex
                || (nowTick - prevState.LastShotTick) > 120)
            {
                prevState = default;
            }

            float smoothing = Mathf.Clamp(ext.angularSmoothing, 0f, 0.92f);
            float smoothedYawDeg = Mathf.Lerp(targetYawNoise, prevState.PrevYawOffsetDeg, smoothing);
            float smoothedPitchNoiseDeg = Mathf.Lerp(targetPitchNoise, prevState.PrevPitchOffsetDeg, smoothing);

            float finalRecoilYawDeg = smoothedYawDeg;
            float finalRecoilPitchDeg = joltPitchDeg + smoothedPitchNoiseDeg;

            turretRecoilStates[casterId] = new BurstRecoilState
            {
                LastShotIndex = numShotsFired,
                LastShotTick = nowTick,
                PrevYawOffsetDeg = smoothedYawDeg,
                PrevPitchOffsetDeg = smoothedPitchNoiseDeg
            };

            rotation += finalRecoilYawDeg;
            angle += finalRecoilPitchDeg * Mathf.Deg2Rad;
            lastRecoilDegRef(__instance) += Mathf.Abs(finalRecoilPitchDeg);

            return false; // Skip vanilla CE GetRecoilVec
        }

        /// <summary>
        /// Caps ProjectileCE.AccuracyFactor on long turret bursts so that 100+ shot rotary bursts
        /// do not multiply tree/bush collision probability by 75x.
        /// </summary>
        public static void Prefix_ProjectileLaunch_CapAccuracyFactor(ProjectileCE __instance, Thing launcher)
        {
            if (__instance == null || launcher == null) return;

            bool isTurretShot = launcher is Building_Turret;
            if (!isTurretShot && __instance.equipment?.ParentHolder is Building_Turret)
            {
                isTurretShot = true;
            }

            if (isTurretShot && __instance.AccuracyFactor > 3.5f)
            {
                __instance.AccuracyFactor = Mathf.Min(__instance.AccuracyFactor, 3.5f);
            }
        }
    }
}
