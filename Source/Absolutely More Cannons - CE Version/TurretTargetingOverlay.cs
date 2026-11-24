using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Per-turret targeting and burst history tracked across bursts for the Targeting HUD.
    /// </summary>
    public class TurretTargetingRecord
    {
        public LocalTargetInfo LastTarget = LocalTargetInfo.Invalid;
        public string LastTargetLabel = "None";
        public string LastTargetSource = "None";
        public int LastTargetTick = -1;

        // Active burst tracking
        public bool IsBursting = false;
        public int CurrentBurstShotsFired = 0;
        public int CurrentBurstPlannedShots = 0;
        public int CurrentBurstStartTick = -1;
        public string PendingBurstEndReason = null;

        public LocalTargetInfo PreShotVerbTarget = LocalTargetInfo.Invalid;
        public LocalTargetInfo PreShotTurretTarget = LocalTargetInfo.Invalid;
        public bool SpraySwitchedThisShot = false;
        public bool PreserveAmmoRetargetedThisShot = false;

        public readonly HashSet<Thing> CurrentBurstEngagedTargets = new HashSet<Thing>();
        public readonly HashSet<Thing> CurrentBurstAlreadyDeadAtEngage = new HashSet<Thing>();
        public readonly HashSet<Thing> CurrentBurstInstigatorKills = new HashSet<Thing>();
        public Thing CurrentBurstLastTargetThing = null;
        public int CurrentBurstTargetSwitches = 0;
        public int CurrentBurstSpraySwitches = 0;
        public int CurrentBurstCERetargetSwitches = 0;
        public bool CurrentBurstHasCERetargetDesync = false;
        public readonly List<string> CurrentBurstEvents = new List<string>();
        public bool HasPendingConeSnapshot = false;
        public SprayConeDiagReport PendingConeSnapshot;

        public bool CurrentBurstHasAimYaw = false;
        public float CurrentBurstStartAimYaw = 0f;
        public float CurrentBurstPrevAimYaw = 0f;
        public float CurrentBurstAccumAimYawSwing = 0f;

        public bool CurrentBurstHasShotYaw = false;
        public float CurrentBurstPrevShotYaw = 0f;
        public float CurrentBurstAccumShotYawSwing = 0f;

        // Previous completed burst frozen snapshot (purely from that single burst)
        public bool HasPreviousBurst = false;
        public int LastBurstShotsFired = 0;
        public int LastBurstPlannedShots = 0;
        public int LastBurstDurationTicks = 0;
        public int LastBurstEndTick = -1;
        public string LastBurstEndReason = "None";
        public string LastBurstConeSummary = "N/A";
        public bool LastBurstStoppedWithEnemiesInCone = false;
        public string LastBurstTimeline = "None";
        public int LastShotTick = -1;

        public int LastBurstEngagedCount = 0;
        public int LastBurstEngagedKills = 0;
        public int LastBurstCollateralKills = 0;
        public int LastBurstEngagedDowned = 0;
        public int LastBurstTargetSwitches = 0;
        public int LastBurstSpraySwitches = 0;
        public int LastBurstCERetargetSwitches = 0;
        public float LastBurstAccumAimYawSwing = 0f;
        public float LastBurstNetAimYawDelta = 0f;
        public float LastBurstAccumShotYawSwing = 0f;
    }

    /// <summary>
    /// Harmony patches to capture per-turret burst start, shot fired, burst completion,
    /// and target kills for the live Turret Targeting HUD.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_TurretTargeting
    {
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, int> numShotsFiredRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, int>("numShotsFired");

        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, float> shotRotationRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, float>("shotRotation");

        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, bool> didRetargetRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, bool>("didRetarget");

        static HarmonyPatches_TurretTargeting()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.TurretTargeting");

            // Hook Verb_LaunchProjectileCE.WarmupComplete with a Prefix so NotifyBurstStart runs:
            // 1) Only when the burst truly starts firing (after CE's 2-stage _isAiming warmup finishes),
            // 2) Before base.WarmupComplete() fires Shot #1 inside TryCastNextBurstShot().
            harmony.Patch(
                AccessTools.Method(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.WarmupComplete)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretTargeting), nameof(Prefix_VerbWarmupComplete)) { priority = Priority.First });

            harmony.Patch(
                AccessTools.Method(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretTargeting), nameof(Prefix_TryCastShot)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(HarmonyPatches_TurretTargeting), nameof(Postfix_TryCastShot)) { priority = Priority.Last });

            harmony.Patch(
                AccessTools.Method(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.BurstComplete)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches_TurretTargeting), nameof(Prefix_BurstComplete)));

            harmony.Patch(
                AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches_TurretTargeting), nameof(Postfix_PawnKill)));
        }

        public static void Prefix_VerbWarmupComplete(Verb_LaunchProjectileCE __instance)
        {
            if (__instance == null || __instance.caster == null) return;
            if (!(__instance.caster is Building_Turret turret) || !turret.Spawned) return;

            var overlay = turret.Map?.GetComponent<MapComponent_TurretTargetingOverlay>();
            overlay?.NotifyBurstStart(turret, __instance);
        }

        public static void Prefix_TryCastShot(Verb_LaunchProjectileCE __instance)
        {
            if (__instance == null || __instance.caster == null) return;
            if (!(__instance.caster is Building_Turret turret) || !turret.Spawned) return;

            var overlay = turret.Map?.GetComponent<MapComponent_TurretTargetingOverlay>();
            overlay?.NotifyPreShotTarget(turret, __instance.CurrentTarget);
        }

        public static void Postfix_TryCastShot(Verb_LaunchProjectileCE __instance, bool __result)
        {
            if (__instance == null || __instance.caster == null) return;
            if (!(__instance.caster is Building_Turret turret) || !turret.Spawned) return;

            var overlay = turret.Map?.GetComponent<MapComponent_TurretTargetingOverlay>();
            if (overlay == null) return;

            int shotsFired = numShotsFiredRef(__instance);
            bool ceDidRetarget = didRetargetRef(__instance);

            if (!__result)
            {
                overlay.NotifyShotCastFailed(turret, __instance, shotsFired, ceDidRetarget);
                return;
            }

            float shotYaw = shotRotationRef(__instance);
            overlay.NotifyShotFired(turret, __instance, shotsFired, shotYaw, ceDidRetarget);
        }

        public static void Prefix_BurstComplete(Building_TurretGunCE __instance)
        {
            if (__instance == null || !__instance.Spawned) return;
            var overlay = __instance.Map?.GetComponent<MapComponent_TurretTargetingOverlay>();
            if (overlay == null) return;

            var verb = __instance.AttackVerb as Verb_LaunchProjectileCE;
            if (verb == null) return;

            int shotsFired = numShotsFiredRef(verb);
            overlay.NotifyBurstComplete(__instance, verb, shotsFired);
        }

        public static void Postfix_PawnKill(Pawn __instance, DamageInfo? dinfo)
        {
            if (__instance == null || !__instance.Dead || !dinfo.HasValue) return;
            Thing instigator = dinfo.Value.Instigator;
            if (instigator == null) return;

            Map map = __instance.MapHeld ?? instigator.MapHeld;
            var overlay = map?.GetComponent<MapComponent_TurretTargetingOverlay>();
            overlay?.NotifyPawnKilledByInstigator(__instance, instigator);
        }
    }

    /// <summary>
    /// Draws a live on-screen HUD below the Ballistics HUD monitoring the selected turret's
    /// targeting data, firing stage, timers, burst counts, ammo/reload, spray discipline,
    /// and a bottom summary of the previous completed burst.
    /// </summary>
    public class MapComponent_TurretTargetingOverlay : MapComponent
    {
        private readonly Dictionary<int, TurretTargetingRecord> records = new Dictionary<int, TurretTargetingRecord>();
        private bool minimized = false;

        // Cached reflection for turret internal state (supports both Building_TurretGunCE and Building_TurretGun)
        private static readonly FieldInfo ceWarmupField = AccessTools.Field(typeof(Building_TurretGunCE), "burstWarmupTicksLeft");
        private static readonly FieldInfo ceCooldownField = AccessTools.Field(typeof(Building_TurretGunCE), "burstCooldownTicksLeft");
        private static readonly FieldInfo ceResetTargetField = AccessTools.Field(typeof(Building_TurretGunCE), "resetTargetTicks");
        private static readonly FieldInfo ceHoldFireField = AccessTools.Field(typeof(Building_TurretGunCE), "holdFire");
        private static readonly FieldInfo ceTargetField = HarmonyPatches.GetCurrentTargetField(typeof(Building_TurretGunCE));

        private static readonly FieldInfo vanillaWarmupField = AccessTools.Field(typeof(Building_TurretGun), "burstWarmupTicksLeft");
        private static readonly FieldInfo vanillaCooldownField = AccessTools.Field(typeof(Building_TurretGun), "burstCooldownTicksLeft");
        private static readonly FieldInfo vanillaResetTargetField = AccessTools.Field(typeof(Building_TurretGun), "resetTargetTicks");
        private static readonly FieldInfo vanillaHoldFireField = AccessTools.Field(typeof(Building_TurretGun), "holdFire");
        private static readonly FieldInfo vanillaTargetField = HarmonyPatches.GetCurrentTargetField(typeof(Building_TurretGun));

        private static readonly FieldInfo verbBurstShotsLeftField = AccessTools.Field(typeof(Verb), "burstShotsLeft");
        private static readonly MethodInfo ammoIsReloadingGetter = AccessTools.PropertyGetter(typeof(CompAmmoUser), "IsReloading");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, int> numShotsFiredRef =
            AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, int>("numShotsFired");

        public MapComponent_TurretTargetingOverlay(Map map) : base(map)
        {
        }

        private TurretTargetingRecord GetOrCreateRecord(int thingID)
        {
            if (!records.TryGetValue(thingID, out var rec))
            {
                rec = new TurretTargetingRecord();
                records[thingID] = rec;
            }
            return rec;
        }

        public void NotifyPendingBurstEndReason(Building_Turret turret, string reason)
        {
            if (turret == null) return;
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            rec.PendingBurstEndReason = reason;
        }

        private static void AppendBurstEvent(TurretTargetingRecord rec, string evt)
        {
            if (rec == null || string.IsNullOrEmpty(evt)) return;
            if (rec.CurrentBurstEvents.Count >= 6)
            {
                rec.CurrentBurstEvents.RemoveAt(0);
            }
            rec.CurrentBurstEvents.Add(evt);
        }

        public void NotifySprayDisciplineSwitch(
            Building_Turret turret,
            LocalTargetInfo oldTarget,
            LocalTargetInfo newTarget,
            string switchWhy,
            int shotsOnOldTarget)
        {
            if (turret == null) return;
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            rec.SpraySwitchedThisShot = true;
            rec.CurrentBurstSpraySwitches++;
            rec.CurrentBurstHasCERetargetDesync = false;
            rec.LastTargetSource = $"Spray Cycle ({switchWhy})";

            if (newTarget.IsValid)
            {
                rec.LastTarget = newTarget;
                rec.LastTargetLabel = FormatTargetShort(newTarget);
                rec.LastTargetTick = Find.TickManager.TicksGame;
                RecordTargetAndAimYaw(turret, rec, newTarget);
            }

            string oldName = FormatTargetNameAndState(oldTarget);
            string newName = FormatTargetNameAndState(newTarget);
            AppendBurstEvent(rec, $"Shot#{rec.CurrentBurstShotsFired + 1} [Spray:{switchWhy}] {oldName} ({shotsOnOldTarget}sh) -> {newName}");
        }

        public void NotifySprayDisciplineNoNextTarget(
            Building_Turret turret,
            LocalTargetInfo curTarget,
            string failWhy)
        {
            if (turret == null) return;
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            string curName = FormatTargetNameAndState(curTarget);
            AppendBurstEvent(rec, $"Shot#{rec.CurrentBurstShotsFired + 1} [Spray:{failWhy}] No other target in cone from {curName}");
        }

        public void NotifySprayDisciplineTriggerRelease(
            Building_Turret turret,
            Verb_LaunchProjectileCE verb,
            LocalTargetInfo refTarget,
            SprayConeDiagReport coneReport)
        {
            if (turret == null) return;
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            int shotNum = verb != null ? numShotsFiredRef(verb) : rec.CurrentBurstShotsFired;
            rec.HasPendingConeSnapshot = true;
            rec.PendingConeSnapshot = coneReport;

            string refStr = FormatTargetNameAndState(refTarget);
            rec.PendingBurstEndReason = $"Spray Discipline: Cone Clear after Shot #{shotNum} (Ref: {refStr})";
            AppendBurstEvent(rec, $"Shot#{shotNum} [Spray Release] Cone clear from {refStr}");
        }

        public void NotifyPreserveAmmoRetarget(
            Building_Turret turret,
            LocalTargetInfo oldDownedTarget,
            LocalTargetInfo newTarget)
        {
            if (turret == null) return;
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            rec.PreserveAmmoRetargetedThisShot = true;
            rec.CurrentBurstCERetargetSwitches++;
            rec.CurrentBurstHasCERetargetDesync = turret.CurrentTarget != newTarget;
            rec.LastTargetSource = "PreserveAmmo -> CE Retarget";

            string oldName = FormatTargetNameAndState(oldDownedTarget);
            string newName = FormatTargetNameAndState(newTarget);
            AppendBurstEvent(rec, $"Shot#{rec.CurrentBurstShotsFired + 1} [PreserveAmmo+CE Retarget] {oldName} -> {newName}");
        }

        public void NotifyPreserveAmmoAbort(
            Building_Turret turret,
            Verb_LaunchProjectileCE verb,
            LocalTargetInfo downedTarget)
        {
            if (turret == null) return;
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            int nextShot = (verb != null ? numShotsFiredRef(verb) : rec.CurrentBurstShotsFired) + 1;

            var sprayComp = turret.TryGetComp<CompTurretSprayDiscipline>();
            if (sprayComp != null && verb != null)
            {
                rec.HasPendingConeSnapshot = true;
                rec.PendingConeSnapshot = sprayComp.EvaluateCone(verb, downedTarget);
            }

            string targName = FormatTargetNameAndState(downedTarget);
            rec.PendingBurstEndReason = $"Preserve Ammo Prefix (Shot #{nextShot}): {targName} Downed & CE 3c-Segment Retarget failed (Spray Postfix skipped!)";
            AppendBurstEvent(rec, $"Shot#{nextShot} [ABORT:PreserveAmmo] {targName} downed; CE 3c Retarget failed before Spray Postfix");
        }

        public void NotifyShotCastFailed(
            Building_Turret turret,
            Verb_LaunchProjectileCE verb,
            int shotsFiredSoFar,
            bool ceDidRetarget)
        {
            if (turret == null || verb == null) return;
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            int attemptedShot = shotsFiredSoFar + 1;

            LocalTargetInfo failTarget = verb.CurrentTarget.IsValid ? verb.CurrentTarget : rec.PreShotVerbTarget;
            var sprayComp = turret.TryGetComp<CompTurretSprayDiscipline>();
            if (sprayComp != null && !rec.HasPendingConeSnapshot)
            {
                rec.HasPendingConeSnapshot = true;
                rec.PendingConeSnapshot = sprayComp.EvaluateCone(verb, failTarget);
            }

            // If PreserveAmmo already recorded the exact abort reason, keep it
            if (!string.IsNullOrEmpty(rec.PendingBurstEndReason))
            {
                return;
            }

            string targDesc = FormatTargetNameAndState(failTarget);
            string failCause;
            if (failTarget.Thing is Pawn p && (p.Dead || !p.Spawned))
            {
                failCause = $"Target {targDesc} died/despawned in flight -> CE 3c Retarget={ceDidRetarget}, ShootLine/KeepBurst=false (Spray Postfix skipped!)";
            }
            else if (failTarget.Thing is Pawn dp && dp.Downed)
            {
                failCause = $"Target {targDesc} downed -> CE 3c Retarget={ceDidRetarget}, TryCastShot=false (Spray Postfix skipped!)";
            }
            else if (failTarget.HasThing && failTarget.Thing != null && (failTarget.Thing.Destroyed || !failTarget.Thing.Spawned))
            {
                failCause = $"Target {targDesc} destroyed/despawned -> ShootLine/KeepBurst=false (Spray Postfix skipped!)";
            }
            else
            {
                failCause = $"TryCastShot returned false on {targDesc} (No ShootLine / KeepBurst=false / Ammo check; Spray Postfix skipped!)";
            }

            rec.PendingBurstEndReason = $"Pre-Shot Abort (Shot #{attemptedShot}): {failCause}";
            AppendBurstEvent(rec, $"Shot#{attemptedShot} [ABORT:TryCastShot=false] {failCause}");
        }

        private static void RecordTargetAndAimYaw(Building_Turret turret, TurretTargetingRecord rec, LocalTargetInfo target)
        {
            if (!target.IsValid) return;

            if (target.HasThing && target.Thing != null)
            {
                Thing t = target.Thing;
                if (rec.CurrentBurstEngagedTargets.Add(t))
                {
                    // Remember if the target was somehow already dead before we first engaged it in this burst
                    if ((t is Pawn p && p.Dead) || t.Destroyed)
                    {
                        rec.CurrentBurstAlreadyDeadAtEngage.Add(t);
                    }
                }
                if (rec.CurrentBurstLastTargetThing != null && rec.CurrentBurstLastTargetThing != t)
                {
                    rec.CurrentBurstTargetSwitches++;
                }
                rec.CurrentBurstLastTargetThing = t;
            }

            float aimYaw = GetAimYaw(turret, target);
            if (!rec.CurrentBurstHasAimYaw)
            {
                rec.CurrentBurstHasAimYaw = true;
                rec.CurrentBurstStartAimYaw = aimYaw;
                rec.CurrentBurstPrevAimYaw = aimYaw;
            }
            else
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(rec.CurrentBurstPrevAimYaw, aimYaw));
                rec.CurrentBurstAccumAimYawSwing += delta;
                rec.CurrentBurstPrevAimYaw = aimYaw;
            }
        }

        private static void ComputeCurrentBurstKillStats(
            TurretTargetingRecord rec,
            out int engagedCount,
            out int engagedKills,
            out int collateralKills,
            out int engagedDowned)
        {
            engagedCount = rec.CurrentBurstEngagedTargets.Count;
            engagedKills = 0;
            collateralKills = 0;
            engagedDowned = 0;

            HashSet<Thing> countedEngagedKills = new HashSet<Thing>();

            foreach (Thing t in rec.CurrentBurstEngagedTargets)
            {
                if (t == null) continue;
                bool wasAlreadyDead = rec.CurrentBurstAlreadyDeadAtEngage.Contains(t);

                if (t is Pawn p)
                {
                    if (p.Dead)
                    {
                        if (!wasAlreadyDead && countedEngagedKills.Add(p))
                        {
                            engagedKills++;
                        }
                    }
                    else if (p.Downed)
                    {
                        engagedDowned++;
                    }
                }
                else if (t.Destroyed && !wasAlreadyDead)
                {
                    if (countedEngagedKills.Add(t))
                    {
                        engagedKills++;
                    }
                }
            }

            foreach (Thing k in rec.CurrentBurstInstigatorKills)
            {
                if (k == null) continue;
                if (rec.CurrentBurstEngagedTargets.Contains(k))
                {
                    if (!rec.CurrentBurstAlreadyDeadAtEngage.Contains(k) && countedEngagedKills.Add(k))
                    {
                        engagedKills++;
                    }
                }
                else
                {
                    collateralKills++;
                }
            }
        }

        private static float GetAimYaw(Building_Turret turret, LocalTargetInfo target)
        {
            if (target.IsValid)
            {
                Vector3 diff = (target.HasThing ? target.Thing.TrueCenter() : target.Cell.ToVector3Shifted()) - turret.TrueCenter();
                return diff.AngleFlat();
            }
            var barrelComp = turret.TryGetComp<CompTurretBarrel>();
            if (barrelComp != null)
            {
                return barrelComp.GetCurrentBarrelRotation();
            }
            return turret.Rotation.AsAngle;
        }

        public void NotifyBurstStart(Building_Turret turret, Verb_LaunchProjectileCE verb)
        {
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            rec.IsBursting = true;
            rec.CurrentBurstShotsFired = 0;
            rec.CurrentBurstPlannedShots = verb.ShotsPerBurst;
            rec.CurrentBurstStartTick = Find.TickManager.TicksGame;
            rec.PendingBurstEndReason = null;

            rec.PreShotVerbTarget = verb.CurrentTarget;
            rec.PreShotTurretTarget = turret.CurrentTarget;
            rec.SpraySwitchedThisShot = false;
            rec.PreserveAmmoRetargetedThisShot = false;

            rec.CurrentBurstEngagedTargets.Clear();
            rec.CurrentBurstAlreadyDeadAtEngage.Clear();
            rec.CurrentBurstInstigatorKills.Clear();
            rec.CurrentBurstLastTargetThing = null;
            rec.CurrentBurstTargetSwitches = 0;
            rec.CurrentBurstSpraySwitches = 0;
            rec.CurrentBurstCERetargetSwitches = 0;
            rec.CurrentBurstHasCERetargetDesync = false;
            rec.CurrentBurstEvents.Clear();
            rec.HasPendingConeSnapshot = false;

            rec.CurrentBurstHasAimYaw = false;
            rec.CurrentBurstStartAimYaw = 0f;
            rec.CurrentBurstPrevAimYaw = 0f;
            rec.CurrentBurstAccumAimYawSwing = 0f;

            rec.CurrentBurstHasShotYaw = false;
            rec.CurrentBurstPrevShotYaw = 0f;
            rec.CurrentBurstAccumShotYawSwing = 0f;

            if (verb.CurrentTarget.IsValid)
            {
                rec.LastTarget = verb.CurrentTarget;
                rec.LastTargetLabel = FormatTargetShort(verb.CurrentTarget);
                rec.LastTargetSource = "Burst Start";
                rec.LastTargetTick = Find.TickManager.TicksGame;
                RecordTargetAndAimYaw(turret, rec, verb.CurrentTarget);
                AppendBurstEvent(rec, $"Start -> {FormatTargetNameAndState(verb.CurrentTarget)}");
            }
        }

        public void NotifyPreShotTarget(Building_Turret turret, LocalTargetInfo preTarget)
        {
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            if (!rec.IsBursting) return;
            rec.PreShotVerbTarget = preTarget;
            rec.PreShotTurretTarget = turret.CurrentTarget;
            rec.SpraySwitchedThisShot = false;
            rec.PreserveAmmoRetargetedThisShot = false;
            RecordTargetAndAimYaw(turret, rec, preTarget);
        }

        public void NotifyShotFired(Building_Turret turret, Verb_LaunchProjectileCE verb, int shotsFired, float shotYaw, bool ceDidRetarget)
        {
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            if (!rec.IsBursting || shotsFired == 1 && rec.CurrentBurstShotsFired > 1)
            {
                // Safety: start a clean burst record if WarmupComplete was bypassed
                NotifyBurstStart(turret, verb);
            }

            rec.CurrentBurstShotsFired = shotsFired;
            if (rec.CurrentBurstPlannedShots <= 0)
            {
                rec.CurrentBurstPlannedShots = verb.ShotsPerBurst;
            }
            rec.LastShotTick = Find.TickManager.TicksGame;

            // Detect if CE's native Verb_LaunchProjectileCE.Retarget() switched the verb target at the start of TryCastShot
            if (ceDidRetarget && !rec.PreserveAmmoRetargetedThisShot)
            {
                rec.CurrentBurstCERetargetSwitches++;
                bool stillDesynced = turret.CurrentTarget != verb.CurrentTarget;
                rec.CurrentBurstHasCERetargetDesync = stillDesynced;
                rec.LastTargetSource = stillDesynced ? "CE Retarget (3c segment, Verb only)" : "CE Retarget (3c segment, Synced)";
                string fromStr = FormatTargetNameAndState(rec.PreShotVerbTarget);
                string toStr = FormatTargetNameAndState(verb.CurrentTarget);
                string syncNote = stillDesynced ? $" (Turret target still {FormatTargetNameAndState(turret.CurrentTarget)})" : "";
                AppendBurstEvent(rec, $"Shot#{shotsFired} [CE Retarget] {fromStr} -> {toStr}{syncNote}");
            }
            else if (rec.SpraySwitchedThisShot)
            {
                // Spray Discipline updates both Verb and Turret targets, resolving any prior desync
                rec.CurrentBurstHasCERetargetDesync = false;
            }

            // Track shot launch yaw swing (includes sway/recoil/spread + target switches)
            float normShotYaw = shotYaw % 360f;
            if (normShotYaw < 0f) normShotYaw += 360f;
            if (!rec.CurrentBurstHasShotYaw)
            {
                rec.CurrentBurstHasShotYaw = true;
                rec.CurrentBurstPrevShotYaw = normShotYaw;
            }
            else
            {
                rec.CurrentBurstAccumShotYawSwing += Mathf.Abs(Mathf.DeltaAngle(rec.CurrentBurstPrevShotYaw, normShotYaw));
                rec.CurrentBurstPrevShotYaw = normShotYaw;
            }

            // Track post-shot target (captures Spray Discipline or CE Retarget mid-burst switch)
            if (verb.CurrentTarget.IsValid)
            {
                rec.LastTarget = verb.CurrentTarget;
                rec.LastTargetLabel = FormatTargetShort(verb.CurrentTarget);
                rec.LastTargetTick = Find.TickManager.TicksGame;
                RecordTargetAndAimYaw(turret, rec, verb.CurrentTarget);
            }
        }

        public void NotifyBurstComplete(Building_Turret turret, Verb_LaunchProjectileCE verb, int shotsFired)
        {
            var rec = GetOrCreateRecord(turret.thingIDNumber);
            int nowTick = Find.TickManager.TicksGame;

            if (verb.CurrentTarget.IsValid)
            {
                rec.LastTarget = verb.CurrentTarget;
                rec.LastTargetLabel = FormatTargetShort(verb.CurrentTarget);
                rec.LastTargetTick = nowTick;
                RecordTargetAndAimYaw(turret, rec, verb.CurrentTarget);
            }

            int plannedShots = rec.CurrentBurstPlannedShots > 0 ? rec.CurrentBurstPlannedShots : verb.ShotsPerBurst;

            // Evaluate cone snapshot if not already captured by trigger release or shot abort
            var sprayComp = turret.TryGetComp<CompTurretSprayDiscipline>();
            if (sprayComp != null && !rec.HasPendingConeSnapshot)
            {
                LocalTargetInfo coneRefTarget = verb.CurrentTarget.IsValid ? verb.CurrentTarget : rec.LastTarget;
                rec.HasPendingConeSnapshot = true;
                rec.PendingConeSnapshot = sprayComp.EvaluateCone(verb, coneRefTarget);
            }

            // Freeze exact kill & down counts purely from this burst at burst completion
            ComputeCurrentBurstKillStats(
                rec,
                out int engagedCount,
                out int engagedKills,
                out int collateralKills,
                out int engagedDowned);

            rec.HasPreviousBurst = true;
            rec.LastBurstShotsFired = shotsFired;
            rec.LastBurstPlannedShots = plannedShots;
            rec.LastBurstEndTick = nowTick;
            rec.LastBurstDurationTicks = rec.CurrentBurstStartTick >= 0 ? Mathf.Max(0, nowTick - rec.CurrentBurstStartTick) : 0;

            rec.LastBurstEngagedCount = engagedCount;
            rec.LastBurstEngagedKills = engagedKills;
            rec.LastBurstCollateralKills = collateralKills;
            rec.LastBurstEngagedDowned = engagedDowned;

            rec.LastBurstTargetSwitches = rec.CurrentBurstTargetSwitches;
            rec.LastBurstSpraySwitches = rec.CurrentBurstSpraySwitches;
            rec.LastBurstCERetargetSwitches = rec.CurrentBurstCERetargetSwitches;
            rec.LastBurstAccumAimYawSwing = rec.CurrentBurstAccumAimYawSwing;
            rec.LastBurstNetAimYawDelta = rec.CurrentBurstHasAimYaw
                ? Mathf.DeltaAngle(rec.CurrentBurstStartAimYaw, rec.CurrentBurstPrevAimYaw)
                : 0f;
            rec.LastBurstAccumShotYawSwing = rec.CurrentBurstAccumShotYawSwing;

            rec.LastBurstEndReason = DetermineBurstEndReason(turret, verb, rec, shotsFired, plannedShots);

            if (rec.HasPendingConeSnapshot)
            {
                rec.LastBurstConeSummary = FormatConeDiagSummary(rec.PendingConeSnapshot);
                rec.LastBurstStoppedWithEnemiesInCone = rec.PendingConeSnapshot.ValidInConeCount > 0;
            }
            else
            {
                rec.LastBurstConeSummary = "Spray Discipline not present on turret";
                rec.LastBurstStoppedWithEnemiesInCone = false;
            }

            rec.LastBurstTimeline = rec.CurrentBurstEvents.Count > 0
                ? string.Join(" | ", rec.CurrentBurstEvents.Skip(Mathf.Max(0, rec.CurrentBurstEvents.Count - 3)))
                : "No switch/abort events recorded";

            rec.IsBursting = false;
            rec.PendingBurstEndReason = null;
            rec.HasPendingConeSnapshot = false;
        }

        public void NotifyPawnKilledByInstigator(Pawn killedPawn, Thing instigator)
        {
            int? turretID = ResolveTurretID(instigator);
            if (!turretID.HasValue) return;

            if (records.TryGetValue(turretID.Value, out var rec))
            {
                // Only record kills that occur during an active firing burst.
                // Never mutate LastBurst snapshot after a burst has completed.
                if (rec.IsBursting)
                {
                    rec.CurrentBurstInstigatorKills.Add(killedPawn);
                }
            }
        }

        private int? ResolveTurretID(Thing instigator)
        {
            if (instigator is Building_Turret turret)
            {
                return turret.thingIDNumber;
            }
            // Check if instigator is a gun or manning pawn belonging to one of our tracked turrets
            foreach (var kvp in records)
            {
                // Fast check on currently spawned buildings if needed
            }
            var turrets = map?.listerBuildings?.allBuildingsColonist;
            if (turrets != null)
            {
                for (int i = 0; i < turrets.Count; i++)
                {
                    if (turrets[i] is Building_TurretGunCE ceTurret)
                    {
                        if (ceTurret.Gun == instigator || ceTurret.MannableComp?.ManningPawn == instigator)
                            return ceTurret.thingIDNumber;
                    }
                    else if (turrets[i] is Building_TurretGun vanillaTurret)
                    {
                        if (vanillaTurret.gun == instigator)
                            return vanillaTurret.thingIDNumber;
                    }
                }
            }
            return null;
        }

        private static string DetermineBurstEndReason(
            Building_Turret turret,
            Verb_LaunchProjectileCE verb,
            TurretTargetingRecord rec,
            int shotsFired,
            int plannedShots)
        {
            // 1. Explicit reason set by Spray Discipline or Preserve Ammo patches
            if (!string.IsNullOrEmpty(rec.PendingBurstEndReason))
            {
                return rec.PendingBurstEndReason;
            }

            // 2. Check turret power/active & holdFire
            bool isTurretActive = (turret as Building_TurretGunCE)?.Active
                               ?? (turret as Building_TurretGun)?.Active
                               ?? true;
            if (!isTurretActive)
            {
                return "Turret Disabled / Power Lost";
            }

            bool holdFire = false;
            if (turret is Building_TurretGunCE ceTurret)
                holdFire = (bool)(ceHoldFireField?.GetValue(ceTurret) ?? false);
            else if (turret is Building_TurretGun vanillaTurret)
                holdFire = (bool)(vanillaHoldFireField?.GetValue(vanillaTurret) ?? false);

            if (holdFire)
            {
                return "Manual Hold Fire";
            }

            // 3. Check magazine depletion
            Thing gunThing = (turret as Building_TurretGunCE)?.Gun ?? (turret as Building_TurretGun)?.gun;
            CompAmmoUser ammoComp = (gunThing as ThingWithComps)?.TryGetComp<CompAmmoUser>();
            if (ammoComp != null && ammoComp.CurMagCount <= 0)
            {
                if (shotsFired > plannedShots)
                {
                    return $"Magazine Depleted (Spray Hold: {shotsFired} shots)";
                }
                if (shotsFired < plannedShots)
                {
                    return $"Magazine Depleted ({shotsFired}/{plannedShots} shots)";
                }
                return $"Reached Burst Limit & Mag Empty ({shotsFired}/{plannedShots})";
            }

            // 4. Reached or exceeded planned burst limit
            if (plannedShots > 0 && shotsFired >= plannedShots)
            {
                if (shotsFired > plannedShots)
                {
                    return $"Spray Hold Completed ({shotsFired} shots, Base {plannedShots})";
                }
                return $"Reached Burst Limit ({shotsFired}/{plannedShots})";
            }

            // 5. Stopped early — inspect target state
            LocalTargetInfo target = verb.CurrentTarget.IsValid ? verb.CurrentTarget : rec.LastTarget;
            if (target.IsValid)
            {
                if (target.Thing is Pawn pawn)
                {
                    if (pawn.Dead) return "Target Killed";
                    if (pawn.Downed) return "Target Downed";
                    if (!pawn.Spawned) return "Target Despawned / Left Map";
                }
                else if (target.HasThing && target.Thing != null)
                {
                    if (target.Thing.Destroyed || !target.Thing.Spawned)
                        return "Target Destroyed";
                }

                float dist = (target.Cell - turret.Position).LengthHorizontal;
                if (verb.verbProps != null)
                {
                    if (dist > verb.verbProps.range) return $"Target Out of Range ({dist:F1}c > {verb.verbProps.range:F0}c)";
                    if (dist < verb.verbProps.minRange) return $"Target Inside Min Range ({dist:F1}c < {verb.verbProps.minRange:F0}c)";
                }
                if (!verb.CanHitTarget(target))
                {
                    return "Line of Sight Lost";
                }
            }
            else
            {
                return "Target Lost / Cleared";
            }

            if (shotsFired == 0)
            {
                return "Aborted Before First Shot";
            }

            return $"Interrupted Early ({shotsFired}/{plannedShots} shots)";
        }

        public override void MapComponentOnGUI()
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || !settings.showTargetingOverlay) return;
            if (Find.CurrentMap != map) return;

            // Position directly below Ballistics HUD
            Rect ballisticsRect = map.GetComponent<MapComponent_BallisticsOverlay>()?.CurrentHUDRect ?? Rect.zero;
            float x = ballisticsRect != Rect.zero ? ballisticsRect.x : 16f;
            float yStart = ballisticsRect != Rect.zero ? ballisticsRect.yMax + 8f : 110f;

            Building_Turret selectedTurret = GetSelectedTurret();

            if (minimized)
            {
                string pillLabel = selectedTurret != null
                    ? $"Targeting HUD: {selectedTurret.LabelCap}"
                    : "Targeting HUD";
                Rect pillRect = new Rect(x, yStart, 210f, 26f);
                Widgets.DrawBoxSolidWithOutline(pillRect, new Color(0.08f, 0.09f, 0.11f, 0.88f), new Color(0.4f, 0.45f, 0.55f, 0.8f));
                if (Widgets.ButtonText(pillRect, pillLabel))
                {
                    minimized = false;
                }
                return;
            }

            if (selectedTurret == null)
            {
                Rect emptyRect = new Rect(x, yStart, 310f, 52f);
                Widgets.DrawBoxSolidWithOutline(emptyRect, new Color(0.08f, 0.09f, 0.11f, 0.85f), new Color(0.35f, 0.4f, 0.5f, 0.8f));
                Text.Font = GameFont.Tiny;
                Widgets.Label(emptyRect.ContractedBy(6f), "Targeting Viewer: Select a turret to monitor...\n(Disable in Mod Settings -> Show Live Turret Targeting Viewer)");
                Text.Font = GameFont.Small;
                return;
            }

            const float width = 560f;
            const float height = 490f;
            Rect windowRect = new Rect(x, yStart, width, height);

            // Keep inside screen vertically if Ballistics HUD is expanded
            if (windowRect.yMax > UI.screenHeight - 8f)
            {
                windowRect.y = Mathf.Max(8f, UI.screenHeight - height - 8f);
            }

            Widgets.DrawBoxSolidWithOutline(windowRect, new Color(0.07f, 0.08f, 0.10f, 0.90f), new Color(0.42f, 0.48f, 0.58f, 0.9f));

            Rect inner = windowRect.ContractedBy(8f);
            float y = inner.y;

            // Header bar
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.85f, 0.93f, 1f);
            Widgets.Label(new Rect(inner.x, y, 310f, 22f), $"Targeting: {selectedTurret.LabelCap}");
            GUI.color = Color.white;

            float bx = inner.xMax - 90f;
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(new Rect(bx, y, 48f, 20f), "Reset"))
            {
                records.Remove(selectedTurret.thingIDNumber);
            }
            if (Widgets.ButtonText(new Rect(bx + 52f, y, 36f, 20f), "Hide"))
            {
                minimized = true;
            }

            y += 24f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            DrawTurretDetails(ref y, inner, selectedTurret);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private Building_Turret GetSelectedTurret()
        {
            if (Find.Selector == null) return null;
            if (Find.Selector.SingleSelectedThing is Building_Turret single && single.Map == map)
            {
                return single;
            }
            var selected = Find.Selector.SelectedObjects;
            if (selected != null)
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    if (selected[i] is Building_Turret t && t.Map == map)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        private void DrawTurretDetails(ref float y, Rect inner, Building_Turret turret)
        {
            int nowTick = Find.TickManager.TicksGame;
            records.TryGetValue(turret.thingIDNumber, out TurretTargetingRecord rec);

            // Read live turret state
            int warmupTicks = 0;
            int cooldownTicks = 0;
            int resetTargetTicks = 0;
            bool holdFire = false;
            LocalTargetInfo curTarget = turret.CurrentTarget;
            LocalTargetInfo forcedTarget = turret.ForcedTarget;
            Verb verb = turret.AttackVerb;
            Verb_LaunchProjectileCE verbCE = verb as Verb_LaunchProjectileCE;

            if (turret is Building_TurretGunCE ceTurret)
            {
                warmupTicks = (int)(ceWarmupField?.GetValue(ceTurret) ?? 0);
                cooldownTicks = (int)(ceCooldownField?.GetValue(ceTurret) ?? 0);
                resetTargetTicks = (int)(ceResetTargetField?.GetValue(ceTurret) ?? 0);
                holdFire = (bool)(ceHoldFireField?.GetValue(ceTurret) ?? false);
                if (!curTarget.IsValid && ceTargetField != null)
                {
                    curTarget = (LocalTargetInfo)(ceTargetField.GetValue(ceTurret) ?? LocalTargetInfo.Invalid);
                }
            }
            else if (turret is Building_TurretGun vanillaTurret)
            {
                warmupTicks = (int)(vanillaWarmupField?.GetValue(vanillaTurret) ?? 0);
                cooldownTicks = (int)(vanillaCooldownField?.GetValue(vanillaTurret) ?? 0);
                resetTargetTicks = (int)(vanillaResetTargetField?.GetValue(vanillaTurret) ?? 0);
                holdFire = (bool)(vanillaHoldFireField?.GetValue(vanillaTurret) ?? false);
                if (!curTarget.IsValid && vanillaTargetField != null)
                {
                    curTarget = (LocalTargetInfo)(vanillaTargetField.GetValue(vanillaTurret) ?? LocalTargetInfo.Invalid);
                }
            }

            // Read CompAmmoUser & CompFireModes
            CompAmmoUser ammoComp = null;
            CompFireModes fireModes = verbCE?.CompFireModes;
            Thing gunThing = (turret as Building_TurretGunCE)?.Gun ?? (turret as Building_TurretGun)?.gun;
            if (gunThing is ThingWithComps gunComps)
            {
                ammoComp = gunComps.TryGetComp<CompAmmoUser>();
                if (fireModes == null)
                {
                    fireModes = gunComps.TryGetComp<CompFireModes>();
                }
            }

            bool isReloading = ammoComp != null && ammoIsReloadingGetter != null && (bool)ammoIsReloadingGetter.Invoke(ammoComp, null);
            bool outOfAmmo = ammoComp != null && !ammoComp.HasAmmo && ammoComp.CurMagCount <= 0;
            int burstShotsLeft = verb != null && verbBurstShotsLeftField != null
                ? (int)verbBurstShotsLeftField.GetValue(verb)
                : 0;
            bool isBursting = (verb != null && verb.state == VerbState.Bursting) || burstShotsLeft > 0;
            bool isTurretActive = (turret as Building_TurretGunCE)?.Active
                               ?? (turret as Building_TurretGun)?.Active
                               ?? true;

            // 1. Firing Stage
            string stage;
            Color stageColor;
            if (!isTurretActive)
            {
                stage = "DISABLED / UNPOWERED";
                stageColor = new Color(0.65f, 0.65f, 0.7f);
            }
            else if (holdFire)
            {
                stage = "HOLD FIRE";
                stageColor = new Color(1f, 0.75f, 0.35f);
            }
            else if (isReloading)
            {
                stage = "RELOADING";
                stageColor = new Color(0.45f, 0.8f, 1f);
            }
            else if (outOfAmmo)
            {
                stage = "OUT OF AMMO";
                stageColor = new Color(1f, 0.42f, 0.42f);
            }
            else if (isBursting)
            {
                int fired = verbCE != null ? numShotsFiredRef(verbCE) : (rec?.CurrentBurstShotsFired ?? 0);
                int planned = verbCE != null ? verbCE.ShotsPerBurst : (verb?.verbProps?.burstShotCount ?? rec?.CurrentBurstPlannedShots ?? 0);
                stage = $"FIRING (Shot {fired}/{planned}, Left: {burstShotsLeft})";
                stageColor = new Color(1f, 0.45f, 0.35f);
            }
            else if (warmupTicks > 0)
            {
                stage = $"AIMING / WARMUP ({warmupTicks}t / {warmupTicks / 60f:F2}s)";
                stageColor = new Color(1f, 0.9f, 0.4f);
            }
            else if (cooldownTicks > 0)
            {
                stage = $"COOLDOWN ({cooldownTicks}t / {cooldownTicks / 60f:F2}s)";
                stageColor = new Color(0.75f, 0.85f, 1f);
            }
            else if (curTarget.IsValid)
            {
                stage = "TARGET LOCKED / READY";
                stageColor = new Color(0.5f, 1f, 0.6f);
            }
            else
            {
                stage = "IDLE / SCANNING";
                stageColor = new Color(0.75f, 0.8f, 0.85f);
            }

            DrawLine(ref y, inner, $"Stage: {stage}", stageColor);

            // 2. Timers & Flags
            string resetStr = resetTargetTicks > 0 ? $"{resetTargetTicks}t ({resetTargetTicks / 60f:F1}s)" : "0t";
            DrawLine(ref y, inner,
                $"Timers: Warmup={warmupTicks}t ({warmupTicks / 60f:F2}s) | Cooldown={cooldownTicks}t ({cooldownTicks / 60f:F2}s) | Reset={resetStr}",
                Color.white);

            // 3. Fire Modes & Ammo
            string aimModeStr = fireModes != null ? fireModes.CurrentAimMode.ToString() : "N/A";
            string fireModeStr = fireModes != null ? fireModes.CurrentFireMode.ToString() : "N/A";
            if (ammoComp != null)
            {
                string ammoName = ammoComp.CurrentAmmo?.LabelCap.ToString() ?? "None";
                DrawLine(ref y, inner,
                    $"Modes: Aim={aimModeStr} | Fire={fireModeStr} | Mag={ammoComp.CurMagCount}/{ammoComp.MagSize} ({ammoName})",
                    new Color(0.85f, 0.9f, 0.95f));
            }
            else
            {
                DrawLine(ref y, inner, $"Modes: Aim={aimModeStr} | Fire={fireModeStr} | No CompAmmoUser", new Color(0.85f, 0.9f, 0.95f));
            }

            y += 3f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            // 4. Targets
            DrawLine(ref y, inner, "TARGETING", new Color(1f, 0.88f, 0.55f));
            LocalTargetInfo verbTarget = verb != null ? verb.CurrentTarget : LocalTargetInfo.Invalid;

            // A true CE Retarget desync only occurs during an active burst (or right after CE Retarget ran in that burst),
            // NOT during initial AIMING / WARMUP when Turret Target has already acquired a new target and Verb.TryStartCastOn hasn't run yet.
            bool midBurstDesync = curTarget.IsValid && verbTarget.IsValid && curTarget != verbTarget
                               && (isBursting || (rec != null && rec.CurrentBurstHasCERetargetDesync && cooldownTicks > 0));
            bool pendingWarmupSync = curTarget.IsValid && verbTarget.IsValid && curTarget != verbTarget && !isBursting && warmupTicks > 0;

            if (curTarget.IsValid)
            {
                float dist = (curTarget.Cell - turret.Position).LengthHorizontal;
                float angle = (curTarget.Cell - turret.Position).AngleFlat;
                string forcedTag = forcedTarget.IsValid && forcedTarget == curTarget ? " [FORCED]" : "";
                string statusTag = midBurstDesync
                    ? " [STALE: Verb retargeted mid-burst]"
                    : pendingWarmupSync
                        ? " [NEW WARMUP TARGET]"
                        : "";
                DrawLine(ref y, inner,
                    $"  Turret Target : {FormatTargetDetailed(curTarget)} | {dist:F1}c @ {angle:F1} deg{forcedTag}{statusTag}",
                    midBurstDesync ? new Color(1f, 0.72f, 0.38f) : new Color(0.6f, 1f, 0.7f));
            }
            else
            {
                string forcedStr = forcedTarget.IsValid ? $" (Forced: {FormatTargetShort(forcedTarget)})" : "";
                DrawLine(ref y, inner, $"  Turret Target : None{forcedStr}", new Color(0.7f, 0.75f, 0.8f));
            }

            if (verbTarget.IsValid)
            {
                float vDist = (verbTarget.Cell - turret.Position).LengthHorizontal;
                float vAngle = (verbTarget.Cell - turret.Position).AngleFlat;
                string desyncNote = midBurstDesync
                    ? " [DESYNC: CE Retarget updated Verb only!]"
                    : pendingWarmupSync
                        ? " (Prev burst leftover until warmup finishes)"
                        : " (Synced with Turret)";
                Color verbTargetColor = midBurstDesync
                    ? new Color(1f, 0.55f, 0.35f)
                    : pendingWarmupSync
                        ? new Color(0.68f, 0.72f, 0.78f)
                        : new Color(0.75f, 0.92f, 0.8f);
                DrawLine(ref y, inner,
                    $"  Verb Target   : {FormatTargetDetailed(verbTarget)} | {vDist:F1}c @ {vAngle:F1} deg{desyncNote}",
                    verbTargetColor);
            }
            else
            {
                DrawLine(ref y, inner, "  Verb Target   : None", new Color(0.65f, 0.7f, 0.75f));
            }

            if (rec != null && rec.LastTargetTick >= 0)
            {
                string ageStr = $"{(nowTick - rec.LastTargetTick) / 60f:F1}s ago";
                string lastLiveState = rec.LastTarget.IsValid ? FormatTargetDetailed(rec.LastTarget) : rec.LastTargetLabel;
                string srcStr = !string.IsNullOrEmpty(rec.LastTargetSource) ? $" via [{rec.LastTargetSource}]" : "";
                DrawLine(ref y, inner, $"  Last Shot At  : {lastLiveState}{srcStr} ({ageStr})", new Color(0.8f, 0.82f, 0.86f));
            }
            else
            {
                DrawLine(ref y, inner, "  Last Shot At  : None recorded", new Color(0.6f, 0.65f, 0.7f));
            }

            // 5. Active Burst Progress
            if (rec != null && rec.IsBursting)
            {
                ComputeCurrentBurstKillStats(
                    rec,
                    out int curEngagedCount,
                    out int curEngagedKills,
                    out int curCollateralKills,
                    out int curEngagedDowned);

                string curCollateralStr = curCollateralKills > 0 ? $" (+{curCollateralKills} stray)" : "";
                DrawLine(ref y, inner,
                    $"  Active Burst  : {rec.CurrentBurstShotsFired}/{rec.CurrentBurstPlannedShots} shots | Killed: {curEngagedKills}{curCollateralStr} | Downed: {curEngagedDowned} | Engaged: {curEngagedCount} (Sw: {rec.CurrentBurstTargetSwitches})",
                    new Color(1f, 0.75f, 0.45f));
            }
            else
            {
                DrawLine(ref y, inner, "  Active Burst  : Idle", new Color(0.6f, 0.65f, 0.7f));
            }

            y += 3f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            // 6. Spray Discipline & Turret Comps
            DrawLine(ref y, inner, "SPRAY DISCIPLINE & TURRET COMPS", new Color(1f, 0.88f, 0.55f));
            var sprayComp = turret.TryGetComp<CompTurretSprayDiscipline>();
            if (sprayComp == null)
            {
                DrawLine(ref y, inner, "  Spray Discipline: Not Enabled on Turret", new Color(0.6f, 0.65f, 0.7f));
            }
            else
            {
                bool bypassedByAimMode = fireModes != null && fireModes.CurrentAimMode == AimMode.SuppressFire;
                string statusText = sprayComp.sprayDisciplineEnabled
                    ? (bypassedByAimMode ? "ON (BYPASSED: SuppressFire)" : "ON")
                    : "OFF";
                Color statusColor = !sprayComp.sprayDisciplineEnabled
                    ? new Color(1f, 0.48f, 0.45f)
                    : bypassedByAimMode
                        ? new Color(1f, 0.85f, 0.35f)
                        : new Color(0.45f, 1f, 0.55f);

                DrawLine(ref y, inner,
                    $"  Spray Discipline: {statusText} | Shots/Targ: {sprayComp.shotsFiredAtCurrentTarget}/{sprayComp.ShotsPerTarget} | Cone: {sprayComp.CycleConeDegrees:F0} deg",
                    statusColor);

                // While bursting, Spray Discipline evaluates cone from verbTarget; while warming up/idle, evaluate from curTarget
                LocalTargetInfo liveConeRef = isBursting && verbTarget.IsValid
                    ? verbTarget
                    : (curTarget.IsValid ? curTarget : verbTarget);
                string coneCheckStr = verb != null && liveConeRef.IsValid
                    ? FormatConeDiagSummary(sprayComp.EvaluateCone(verb, liveConeRef))
                    : "N/A (no active target)";
                DrawLine(ref y, inner, $"  Rolling Cone    : {coneCheckStr}", new Color(0.8f, 0.85f, 0.9f));
            }

            var preserveComp = turret.TryGetComp<CompTurretPreserveAmmo>();
            string preserveStr = preserveComp != null ? (preserveComp.preserveAmmo ? "ON" : "OFF") : "N/A";

            var fcsComp = turret.TryGetComp<CompTurretFCS>();
            string fcsStr = fcsComp != null
                ? (fcsComp.HasFCS ? $"{fcsComp.LoadedFCSItem.LabelCap} (Aim x{fcsComp.ActiveStats?.aimTimeMultiplier ?? 1f:F2})" : "Empty")
                : "N/A";

            var barrelComp = turret.TryGetComp<CompTurretBarrel>();
            string barrelStr = barrelComp != null ? $"{barrelComp.GetCurrentBarrelRotation():F1} deg" : "N/A";
            if (TurretTrackingUtility.TryGetTraverseState(turret.thingIDNumber, out var travState) && travState.MaxSpeed > 0f)
            {
                barrelStr += $" (Vel: {Mathf.Abs(travState.CurrentAngularVelocity):F2}/{travState.MaxSpeed:F2} deg/t, Accel: {travState.AccelPerTickSq:F3}, Act: {travState.ActuatorStrength:F1}, Wt: {travState.HeadWeight:F1})";
            }

            DrawLine(ref y, inner, $"  Preserve Ammo   : {preserveStr} | Barrel Angle: {barrelStr}", new Color(0.75f, 0.8f, 0.85f));
            DrawLine(ref y, inner, $"  FCS             : {fcsStr}", new Color(0.75f, 0.8f, 0.85f));

            y += 3f;
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            y += 4f;

            // 7. Bottom Section:
            // - While actively firing (rec.IsBursting), show CURRENT BURST STATUS (purely from the ongoing burst).
            // - While in warmup / cooldown / idle, show LAST BURST STATUS (frozen snapshot purely from the last completed burst).
            if (rec != null && rec.IsBursting)
            {
                int curDurTicks = rec.CurrentBurstStartTick >= 0 ? Mathf.Max(0, nowTick - rec.CurrentBurstStartTick) : 0;
                float curDurSec = curDurTicks / 60f;
                DrawLine(ref y, inner,
                    $"CURRENT BURST STATUS (LIVE FIRING | Elapsed: {curDurTicks}t / {curDurSec:F2}s)",
                    new Color(0.45f, 0.95f, 1f));

                ComputeCurrentBurstKillStats(
                    rec,
                    out int curEngagedCount,
                    out int curEngagedKills,
                    out int curCollateralKills,
                    out int curEngagedDowned);

                DrawLine(ref y, inner,
                    $"  Status       : Firing in progress (Shot {rec.CurrentBurstShotsFired}/{rec.CurrentBurstPlannedShots})",
                    new Color(0.55f, 1f, 0.65f));

                LocalTargetInfo liveConeRef = verbTarget.IsValid ? verbTarget : curTarget;
                string liveConeStr = sprayComp != null && verb != null && liveConeRef.IsValid
                    ? FormatConeDiagSummary(sprayComp.EvaluateCone(verb, liveConeRef))
                    : "N/A";
                DrawLine(ref y, inner, $"  Live Cone    : {liveConeStr}", new Color(0.8f, 0.9f, 0.95f));

                string collateralTag = curCollateralKills > 0 ? $" (+{curCollateralKills} stray)" : "";
                DrawLine(ref y, inner,
                    $"  Shots & Kills: {rec.CurrentBurstShotsFired}/{rec.CurrentBurstPlannedShots} shots | Killed: {curEngagedKills}{collateralTag} | Downed: {curEngagedDowned} | Engaged: {curEngagedCount} (Switches: {rec.CurrentBurstTargetSwitches} [Spray:{rec.CurrentBurstSpraySwitches}, CE:{rec.CurrentBurstCERetargetSwitches}])",
                    Color.white);

                string curTrace = rec.CurrentBurstEvents.Count > 0
                    ? string.Join(" | ", rec.CurrentBurstEvents.Skip(Mathf.Max(0, rec.CurrentBurstEvents.Count - 3)))
                    : "Firing...";
                DrawLine(ref y, inner, $"  Burst Trace  : {curTrace}", new Color(0.92f, 0.86f, 0.72f));

                float curNetAimDelta = rec.CurrentBurstHasAimYaw
                    ? Mathf.DeltaAngle(rec.CurrentBurstStartAimYaw, rec.CurrentBurstPrevAimYaw)
                    : 0f;
                DrawLine(ref y, inner,
                    $"  Yaw Swings   : Aim Accum: {rec.CurrentBurstAccumAimYawSwing:F1} deg (Net {curNetAimDelta:+0.0;-0.0;0.0} deg) | Shot Accum: {rec.CurrentBurstAccumShotYawSwing:F1} deg",
                    new Color(0.8f, 0.88f, 0.96f));
            }
            else if (rec == null || !rec.HasPreviousBurst)
            {
                DrawLine(ref y, inner, "LAST BURST STATUS", new Color(1f, 0.88f, 0.55f));
                DrawLine(ref y, inner, "  Waiting for a burst to complete...", new Color(0.6f, 0.65f, 0.7f));
            }
            else
            {
                float ageSec = (nowTick - rec.LastBurstEndTick) / 60f;
                float durSec = rec.LastBurstDurationTicks / 60f;
                DrawLine(ref y, inner,
                    $"LAST BURST STATUS ({ageSec:F1}s ago | Duration: {rec.LastBurstDurationTicks}t / {durSec:F2}s)",
                    new Color(1f, 0.88f, 0.55f));

                bool preShotAbort = rec.LastBurstEndReason.StartsWith("Pre-Shot Abort") || rec.LastBurstEndReason.StartsWith("Preserve Ammo Prefix");
                Color reasonColor = preShotAbort || rec.LastBurstStoppedWithEnemiesInCone
                    ? new Color(1f, 0.52f, 0.38f)
                    : rec.LastBurstEndReason.StartsWith("Reached Burst Limit") || rec.LastBurstEndReason.StartsWith("Spray") || rec.LastBurstEndReason == "Target Killed"
                        ? new Color(0.5f, 1f, 0.6f)
                        : rec.LastBurstEndReason.StartsWith("Preserve Ammo") || rec.LastBurstEndReason.StartsWith("Magazine") || rec.LastBurstEndReason == "Target Downed"
                            ? new Color(1f, 0.88f, 0.4f)
                            : new Color(1f, 0.55f, 0.45f);

                DrawLine(ref y, inner, $"  End Reason   : {rec.LastBurstEndReason}", reasonColor);

                Color coneEndColor = rec.LastBurstStoppedWithEnemiesInCone
                    ? new Color(1f, 0.6f, 0.35f)
                    : new Color(0.75f, 0.9f, 0.8f);
                DrawLine(ref y, inner, $"  Cone @ End   : {rec.LastBurstConeSummary}", coneEndColor);

                string collateralTag = rec.LastBurstCollateralKills > 0 ? $" (+{rec.LastBurstCollateralKills} stray)" : "";
                DrawLine(ref y, inner,
                    $"  Shots & Kills: {rec.LastBurstShotsFired}/{rec.LastBurstPlannedShots} shots | Killed: {rec.LastBurstEngagedKills}{collateralTag} | Downed: {rec.LastBurstEngagedDowned} | Engaged: {rec.LastBurstEngagedCount} (Switches: {rec.LastBurstTargetSwitches} [Spray:{rec.LastBurstSpraySwitches}, CE:{rec.LastBurstCERetargetSwitches}])",
                    Color.white);

                DrawLine(ref y, inner,
                    $"  Burst Trace  : {rec.LastBurstTimeline}",
                    new Color(0.92f, 0.86f, 0.72f));

                DrawLine(ref y, inner,
                    $"  Yaw Swings   : Aim Accum: {rec.LastBurstAccumAimYawSwing:F1} deg (Net {rec.LastBurstNetAimYawDelta:+0.0;-0.0;0.0} deg) | Shot Accum: {rec.LastBurstAccumShotYawSwing:F1} deg",
                    new Color(0.8f, 0.88f, 0.96f));
            }
        }

        private static string FormatConeDiagSummary(SprayConeDiagReport r)
        {
            if (!r.HasValidReference)
            {
                return "No reference angle";
            }

            if (r.ValidInConeCount > 0 && r.ClosestInCone != null)
            {
                return $"{r.ValidInConeCount} in {r.ConeThresholdDegrees:F0} deg cone! (Closest: {r.ClosestInCone.LabelShortCap} @ {r.ClosestInConeDist:F1}c, dYaw {r.ClosestInConeAngleDelta:F1} deg) | Ref: {r.ReferenceSource}";
            }

            if (r.BlockedByLoSInConeCount > 0 && r.NearestBlockedLoSInCone != null)
            {
                return $"0 hit-able in cone ({r.BlockedByLoSInConeCount} blocked by LoS: {r.NearestBlockedLoSInCone.LabelShortCap} @ {r.NearestBlockedLoSDist:F1}c, dYaw {r.NearestBlockedLoSAngleDelta:F1} deg) | Ref: {r.ReferenceSource}";
            }

            if (r.NearestOutsideCone != null)
            {
                return $"0 in {r.ConeThresholdDegrees:F0} deg cone (Nearest outside: {r.NearestOutsideCone.LabelShortCap} @ {r.NearestOutsideConeDist:F1}c, dYaw {r.NearestOutsideConeAngleDelta:F1} deg > {r.ConeThresholdDegrees:F0} deg) | Ref: {r.ReferenceSource}";
            }

            return $"0 in {r.ConeThresholdDegrees:F0} deg cone (0 standing hostiles in range) | Ref: {r.ReferenceSource}";
        }

        private static string FormatTargetNameAndState(LocalTargetInfo target)
        {
            if (!target.IsValid) return "None";
            if (target.Thing is Pawn pawn)
            {
                string state = pawn.Dead ? "Dead"
                             : !pawn.Spawned ? "Despawned"
                             : pawn.Downed ? "Downed"
                             : "Alive";
                return $"{pawn.LabelShortCap}[{state}]";
            }
            if (target.HasThing && target.Thing != null)
            {
                string state = (target.Thing.Destroyed || !target.Thing.Spawned) ? "Gone" : "Active";
                return $"{target.Thing.LabelShortCap}[{state}]";
            }
            return $"Cell{target.Cell}";
        }

        private static string FormatTargetShort(LocalTargetInfo target)
        {
            if (!target.IsValid) return "Invalid";
            if (target.HasThing && target.Thing != null)
            {
                return $"{target.Thing.LabelCap} @ {target.Cell}";
            }
            return $"Cell {target.Cell}";
        }

        private static string FormatTargetDetailed(LocalTargetInfo target)
        {
            if (!target.IsValid) return "Invalid";
            if (target.Thing is Pawn pawn)
            {
                string state = pawn.Dead ? "Dead"
                             : !pawn.Spawned ? "Despawned"
                             : pawn.Downed ? "Downed"
                             : "Alive";
                return $"{pawn.LabelCap} [{state}] @ {pawn.Position}";
            }
            if (target.Thing != null)
            {
                string state = (target.Thing.Destroyed || !target.Thing.Spawned) ? "Gone" : "Active";
                return $"{target.Thing.LabelCap} [{state}] @ {target.Cell}";
            }
            return $"Cell {target.Cell}";
        }

        private static void DrawLine(ref float y, Rect inner, string text, Color color)
        {
            GUI.color = color;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f), text);
            y += 17f;
        }
    }
}
