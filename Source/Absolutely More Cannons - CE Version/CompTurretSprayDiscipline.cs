using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    public struct SprayConeDiagReport
    {
        public bool HasValidReference;
        public float ReferenceAngle;
        public string ReferenceSource;
        public float ConeThresholdDegrees;

        public int StandingHostilesInRange;
        public int ValidInConeCount;

        public Pawn ClosestInCone;
        public float ClosestInConeDist;
        public float ClosestInConeAngleDelta;

        public Pawn NearestOutsideCone;
        public float NearestOutsideConeDist;
        public float NearestOutsideConeAngleDelta;

        public int BlockedByLoSInConeCount;
        public Pawn NearestBlockedLoSInCone;
        public float NearestBlockedLoSDist;
        public float NearestBlockedLoSAngleDelta;
    }

    public class CompTurretSprayDiscipline : ThingComp
    {
        public bool sprayDisciplineEnabled = true;
        public int shotsFiredAtCurrentTarget = 0;
        private int lastTargetThingID = -1;
        private bool initialized = false;

        public CompProperties_TurretSprayDiscipline Props => (CompProperties_TurretSprayDiscipline)props;

        public TurretSprayDisciplineExtension Extension => parent?.def?.GetModExtension<TurretSprayDisciplineExtension>();

        public int ShotsPerTarget => Extension?.shotsPerTarget ?? Props?.shotsPerTarget ?? 4;
        public float CycleConeDegrees => Extension?.cycleConeDegrees ?? Props?.cycleConeDegrees ?? 10f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && !initialized)
            {
                if (Extension != null)
                {
                    sprayDisciplineEnabled = Extension.defaultEnableSprayDiscipline;
                }
                else if (Props != null)
                {
                    sprayDisciplineEnabled = Props.defaultEnableSprayDiscipline;
                }
                initialized = true;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref sprayDisciplineEnabled, "sprayDisciplineEnabled", true);
            Scribe_Values.Look(ref shotsFiredAtCurrentTarget, "shotsFiredAtCurrentTarget", 0);
            Scribe_Values.Look(ref lastTargetThingID, "lastTargetThingID", -1);
            Scribe_Values.Look(ref initialized, "initializedSprayDiscipline", true);
        }

        public void Notify_ShotFired(LocalTargetInfo currentTarget)
        {
            int curID = currentTarget.HasThing ? currentTarget.Thing.thingIDNumber : currentTarget.Cell.GetHashCode();
            if (curID != lastTargetThingID)
            {
                lastTargetThingID = curID;
                shotsFiredAtCurrentTarget = 1;
            }
            else
            {
                shotsFiredAtCurrentTarget++;
            }
        }

        public void ResetShotCount()
        {
            shotsFiredAtCurrentTarget = 0;
            lastTargetThingID = -1;
        }

        public bool TryGetNextConeTarget(Verb verb, LocalTargetInfo currentTarget, out LocalTargetInfo newTarget)
        {
            newTarget = LocalTargetInfo.Invalid;
            if (parent == null || !parent.Spawned || parent.Map == null || verb == null)
            {
                return false;
            }

            var mapPawns = parent.Map.mapPawns.AllPawnsSpawned;
            if (mapPawns == null || mapPawns.Count == 0)
            {
                return false;
            }

            Vector3 turretPos = parent.Position.ToVector3Shifted();
            float minRange = verb.verbProps.minRange;
            float maxRange = verb.verbProps.range;

            // Determine reference aiming angle
            float currentAimAngle;
            if (currentTarget.IsValid && currentTarget.Cell.IsValid)
            {
                currentAimAngle = (currentTarget.Cell - parent.Position).AngleFlat;
            }
            else
            {
                var barrelComp = parent.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    currentAimAngle = barrelComp.GetCurrentBarrelRotation();
                }
                else
                {
                    currentAimAngle = parent.Rotation.AsAngle;
                }
            }

            float coneMaxDelta = CycleConeDegrees;
            float bestDistance = float.MaxValue;
            Pawn bestCandidate = null;

            Thing currentTargetThing = currentTarget.Thing;
            bool currentTargetInvalidOrDowned = currentTargetThing == null ||
                                                !currentTargetThing.Spawned ||
                                                currentTargetThing.Destroyed ||
                                                (currentTargetThing is Pawn p && (p.Dead || p.Downed));

            foreach (var pawn in mapPawns)
            {
                if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) continue;
                if (parent.Faction != null && !pawn.HostileTo(parent.Faction)) continue;

                // Skip current target unless it's dead/downed and we need any valid target
                if (!currentTargetInvalidOrDowned && pawn == currentTargetThing) continue;

                float dist = (pawn.Position - parent.Position).LengthHorizontal;
                if (dist < minRange || dist > maxRange) continue;

                // Angle check relative to current aiming line
                float candidateAngle = (pawn.Position - parent.Position).AngleFlat;
                float angleDelta = Mathf.Abs(Mathf.DeltaAngle(currentAimAngle, candidateAngle));
                if (angleDelta > coneMaxDelta) continue;

                // Line of Sight & CanHitTarget check
                if (!verb.CanHitTarget(pawn)) continue;

                // Pick candidate closest to turret
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestCandidate = pawn;
                }
            }

            if (bestCandidate != null)
            {
                newTarget = new LocalTargetInfo(bestCandidate);
                return true;
            }

            return false;
        }

        public bool HasAnyConeTarget(Verb verb, LocalTargetInfo currentTarget)
        {
            var report = EvaluateCone(verb, currentTarget);
            return report.ValidInConeCount > 0;
        }

        public SprayConeDiagReport EvaluateCone(Verb verb, LocalTargetInfo currentTarget)
        {
            var report = new SprayConeDiagReport
            {
                HasValidReference = false,
                ReferenceAngle = 0f,
                ReferenceSource = "None",
                ConeThresholdDegrees = CycleConeDegrees,
                StandingHostilesInRange = 0,
                ValidInConeCount = 0,
                ClosestInCone = null,
                ClosestInConeDist = float.MaxValue,
                ClosestInConeAngleDelta = 0f,
                NearestOutsideCone = null,
                NearestOutsideConeDist = float.MaxValue,
                NearestOutsideConeAngleDelta = 0f,
                BlockedByLoSInConeCount = 0,
                NearestBlockedLoSInCone = null,
                NearestBlockedLoSDist = float.MaxValue,
                NearestBlockedLoSAngleDelta = 0f
            };

            if (parent == null || !parent.Spawned || parent.Map == null || verb == null)
            {
                return report;
            }

            float minRange = verb.verbProps != null ? verb.verbProps.minRange : 0f;
            float maxRange = verb.verbProps != null ? verb.verbProps.range : 9999f;

            float currentAimAngle;
            if (currentTarget.IsValid && currentTarget.Cell.IsValid)
            {
                currentAimAngle = (currentTarget.Cell - parent.Position).AngleFlat;
                string targName = currentTarget.HasThing && currentTarget.Thing != null
                    ? currentTarget.Thing.LabelShortCap
                    : $"Cell {currentTarget.Cell}";
                report.ReferenceSource = $"{targName} ({currentAimAngle:F1} deg)";
                report.HasValidReference = true;
            }
            else
            {
                var barrelComp = parent.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    currentAimAngle = barrelComp.GetCurrentBarrelRotation();
                    report.ReferenceSource = $"Barrel ({currentAimAngle:F1} deg)";
                }
                else
                {
                    currentAimAngle = parent.Rotation.AsAngle;
                    report.ReferenceSource = $"TurretRot ({currentAimAngle:F1} deg)";
                }
                report.HasValidReference = true;
            }

            report.ReferenceAngle = currentAimAngle;

            var mapPawns = parent.Map.mapPawns?.AllPawnsSpawned;
            if (mapPawns == null || mapPawns.Count == 0)
            {
                return report;
            }

            float coneMaxDelta = CycleConeDegrees;

            foreach (var pawn in mapPawns)
            {
                if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) continue;
                if (parent.Faction != null && !pawn.HostileTo(parent.Faction)) continue;

                float dist = (pawn.Position - parent.Position).LengthHorizontal;
                if (dist < minRange || dist > maxRange) continue;

                report.StandingHostilesInRange++;

                float candidateAngle = (pawn.Position - parent.Position).AngleFlat;
                float angleDelta = Mathf.Abs(Mathf.DeltaAngle(currentAimAngle, candidateAngle));

                if (angleDelta <= coneMaxDelta)
                {
                    if (verb.CanHitTarget(pawn))
                    {
                        report.ValidInConeCount++;
                        if (dist < report.ClosestInConeDist)
                        {
                            report.ClosestInCone = pawn;
                            report.ClosestInConeDist = dist;
                            report.ClosestInConeAngleDelta = angleDelta;
                        }
                    }
                    else
                    {
                        report.BlockedByLoSInConeCount++;
                        if (dist < report.NearestBlockedLoSDist)
                        {
                            report.NearestBlockedLoSInCone = pawn;
                            report.NearestBlockedLoSDist = dist;
                            report.NearestBlockedLoSAngleDelta = angleDelta;
                        }
                    }
                }
                else
                {
                    if (dist < report.NearestOutsideConeDist)
                    {
                        report.NearestOutsideCone = pawn;
                        report.NearestOutsideConeDist = dist;
                        report.NearestOutsideConeAngleDelta = angleDelta;
                    }
                }
            }

            return report;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (parent.Faction != Faction.OfPlayer && !Prefs.DevMode)
            {
                yield break;
            }

            if (Extension != null && !Extension.allowToggle)
            {
                yield break;
            }

            Texture2D icon = sprayDisciplineEnabled
                ? (ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmoON", false) ?? ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmo", false))
                : (ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmoOFF", false) ?? ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmo", false));

            yield return new Command_Toggle
            {
                defaultLabel = sprayDisciplineEnabled ? "Spray Discipline: ON" : "Spray Discipline: OFF",
                defaultDesc = $"When enabled, the rotary gunner will dynamically switch targets mid-burst after firing {ShotsPerTarget} rounds if another standing hostile is within a {CycleConeDegrees}° visual cone.",
                icon = icon,
                isActive = () => sprayDisciplineEnabled,
                toggleAction = () =>
                {
                    sprayDisciplineEnabled = !sprayDisciplineEnabled;
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        AMCLogger.LogTurretTarget($"[Spray Discipline Toggle] {parent.LabelCap} @ {parent.Position} set to: {sprayDisciplineEnabled}");
                    }
                }
            };
        }
    }
}
