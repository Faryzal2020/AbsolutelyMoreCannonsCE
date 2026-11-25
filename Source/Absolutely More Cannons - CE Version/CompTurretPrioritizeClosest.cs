using System;
using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AbsolutelyMoreCannons
{
    public class CompTurretPrioritizeClosest : ThingComp
    {
        public bool prioritizeClosestEnabled = true;
        public int committedTargetThingID = -1;
        public int alignmentCompletedTick = -1;
        public bool isWaitingForAlignment = false;
        public float currentConeHalfDegrees = 180f;

        private bool initialized = false;
        private int lastConeUpdateTick = -1;

        public CompProperties_TurretPrioritizeClosest Props => (CompProperties_TurretPrioritizeClosest)props;

        public TurretPrioritizeClosestExtension Extension => parent?.def?.GetModExtension<TurretPrioritizeClosestExtension>();

        public bool DefaultPrioritizeClosest => Extension?.defaultPrioritizeClosest ?? Props?.defaultPrioritizeClosest ?? true;
        public float CommitmentSeconds => Extension?.commitmentSeconds ?? Props?.commitmentSeconds ?? 4.0f;
        public float FocusConeHalfDegrees => Mathf.Clamp(Extension?.visionConeDegrees ?? Props?.visionConeDegrees ?? 45.0f, 5f, 180f);
        public float ConeExpandDegreesPerSecond => Mathf.Max(0.1f, Extension?.coneExpandDegreesPerSecond ?? Props?.coneExpandDegreesPerSecond ?? 10.0f);
        public float TraverseTimeWeight => Mathf.Max(0f, Extension?.traverseTimeWeight ?? Props?.traverseTimeWeight ?? 8.0f);
        public float MinSwitchAdvantageTiles => Mathf.Max(0.5f, Extension?.minSwitchAdvantageTiles ?? Props?.minSwitchAdvantageTiles ?? 3.0f);
        public bool AllowToggle => Extension?.allowToggle ?? Props?.allowToggle ?? true;

        public int CommitmentTicks => Mathf.Max(1, Mathf.RoundToInt(CommitmentSeconds * 60f));

        public bool IsLockedBySprayDiscipline
        {
            get
            {
                var sprayComp = parent?.TryGetComp<CompTurretSprayDiscipline>();
                return sprayComp != null && sprayComp.sprayDisciplineEnabled;
            }
        }

        public bool IsEffectivelyActive => prioritizeClosestEnabled && !IsLockedBySprayDiscipline;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && !initialized)
            {
                prioritizeClosestEnabled = DefaultPrioritizeClosest;
                currentConeHalfDegrees = 180f;
                initialized = true;
            }

            // Spray Discipline always takes priority over Prioritize Closest
            if (IsLockedBySprayDiscipline)
            {
                prioritizeClosestEnabled = false;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref prioritizeClosestEnabled, "prioritizeClosestEnabled", true);
            Scribe_Values.Look(ref committedTargetThingID, "committedTargetThingID", -1);
            Scribe_Values.Look(ref alignmentCompletedTick, "alignmentCompletedTick", -1);
            Scribe_Values.Look(ref isWaitingForAlignment, "isWaitingForAlignment", false);
            Scribe_Values.Look(ref currentConeHalfDegrees, "currentConeHalfDegrees", 180f);
            Scribe_Values.Look(ref initialized, "initializedPrioritizeClosest", true);
        }

        public void Notify_SprayDisciplineEnabled()
        {
            if (prioritizeClosestEnabled)
            {
                prioritizeClosestEnabled = false;
                ResetCommitment();
            }
        }

        public void ResetCommitment()
        {
            committedTargetThingID = -1;
            alignmentCompletedTick = -1;
            isWaitingForAlignment = false;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (parent == null || !parent.Spawned || parent.Map == null)
                return;

            // Enforce Spray Discipline priority
            if (IsLockedBySprayDiscipline)
            {
                if (prioritizeClosestEnabled)
                {
                    prioritizeClosestEnabled = false;
                    ResetCommitment();
                }
                return;
            }

            if (!prioritizeClosestEnabled)
                return;

            if (!(parent is Building_Turret turret))
                return;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int elapsedTicks = lastConeUpdateTick >= 0 ? Mathf.Max(1, nowTick - lastConeUpdateTick) : 1;
            lastConeUpdateTick = nowTick;

            Verb verb = turret.AttackVerb;
            LocalTargetInfo curTarget = turret.CurrentTarget;
            bool hasValidTarget = IsTargetValidAndAlive(turret, verb, curTarget, checkLoS: false);

            if (hasValidTarget)
            {
                // Snap immediately to the 45-degree (90-degree total arc) focus cone
                currentConeHalfDegrees = FocusConeHalfDegrees;
                UpdateTargetAlignmentState(turret, curTarget, nowTick);
            }
            else
            {
                // No valid current target: clear commitment and widen vision cone by +10 deg/sec up to 180 deg (360 total)
                if (committedTargetThingID != -1)
                {
                    ResetCommitment();
                }

                float expandStep = (ConeExpandDegreesPerSecond / 60f) * elapsedTicks;
                currentConeHalfDegrees = Mathf.Min(180f, currentConeHalfDegrees + expandStep);
            }
        }

        /// <summary>
        /// Tracks whether the turret has finished rotating to the committed target and starts the commitment timer once aligned.
        /// </summary>
        public void UpdateTargetAlignmentState(Building_Turret turret, LocalTargetInfo target, int nowTick)
        {
            if (!target.IsValid)
            {
                ResetCommitment();
                return;
            }

            int targID = GetTargetIdentifier(target);
            if (targID != committedTargetThingID)
            {
                LockOntoTarget(turret, target, nowTick);
                return;
            }

            if (isWaitingForAlignment)
            {
                if (IsTurretAlignedWithTarget(turret, target))
                {
                    isWaitingForAlignment = false;
                    alignmentCompletedTick = nowTick;
                }
            }
        }

        /// <summary>
        /// Called when a shot is actually cast at the target, guaranteeing alignment is marked complete.
        /// </summary>
        public void Notify_ShotFiredAt(Building_Turret turret, LocalTargetInfo target)
        {
            if (!IsEffectivelyActive || !target.IsValid) return;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int targID = GetTargetIdentifier(target);

            if (targID != committedTargetThingID)
            {
                committedTargetThingID = targID;
                isWaitingForAlignment = false;
                alignmentCompletedTick = nowTick;
            }
            else if (isWaitingForAlignment)
            {
                isWaitingForAlignment = false;
                alignmentCompletedTick = nowTick;
            }

            currentConeHalfDegrees = FocusConeHalfDegrees;
        }

        public void LockOntoTarget(Building_Turret turret, LocalTargetInfo newTarget, int nowTick)
        {
            if (!newTarget.IsValid)
            {
                ResetCommitment();
                return;
            }

            committedTargetThingID = GetTargetIdentifier(newTarget);
            currentConeHalfDegrees = FocusConeHalfDegrees;

            if (IsTurretAlignedWithTarget(turret, newTarget))
            {
                isWaitingForAlignment = false;
                alignmentCompletedTick = nowTick;
            }
            else
            {
                isWaitingForAlignment = true;
                alignmentCompletedTick = -1;
            }
        }

        public bool IsTurretAlignedWithTarget(Building_Turret turret, LocalTargetInfo target)
        {
            if (turret == null || !target.IsValid) return true;

            if (turret is Building_TurretGunCE ceTurret && ceTurret.NonSnap)
            {
                float barrelRot = GetReferenceBarrelAngle(turret);
                float targetAngle = GetAngleToTarget(turret, target);
                float delta = Mathf.Abs(Mathf.DeltaAngle(barrelRot, targetAngle));

                float speed = ceTurret.NonSnapExtension != null ? Mathf.Max(0.25f, ceTurret.NonSnapExtension.speed) : 2.0f;
                float tolerance = Mathf.Max(2.5f, speed * 2.0f);
                return delta <= tolerance;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the turret is currently locked onto a valid target and either still rotating to it
        /// or within the post-alignment commitment window (e.g. 4.0 seconds).
        /// </summary>
        public bool IsCommittedToTarget(Building_Turret turret, Verb verb, LocalTargetInfo currentTarget, out float remainingCommitmentSeconds)
        {
            remainingCommitmentSeconds = 0f;
            if (!IsEffectivelyActive || turret == null || !currentTarget.IsValid)
                return false;

            if (!IsTargetValidAndAlive(turret, verb, currentTarget, checkLoS: true))
                return false;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            UpdateTargetAlignmentState(turret, currentTarget, nowTick);

            if (isWaitingForAlignment)
            {
                remainingCommitmentSeconds = CommitmentSeconds;
                return true;
            }

            if (alignmentCompletedTick >= 0)
            {
                int ticksElapsed = nowTick - alignmentCompletedTick;
                int commitTicks = CommitmentTicks;
                if (ticksElapsed < commitTicks)
                {
                    remainingCommitmentSeconds = (commitTicks - ticksElapsed) / 60f;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a target is still alive, spawned, hostile, not downed (when Preserve Ammo is enabled),
        /// within min/max range, within CompFireArc, and optionally has Line of Sight / CanHitTarget.
        /// </summary>
        public bool IsTargetValidAndAlive(Building_Turret turret, Verb verb, LocalTargetInfo target, bool checkLoS)
        {
            if (turret == null || !turret.Spawned || turret.Map == null || !target.IsValid)
                return false;

            if (target.ThingDestroyed)
                return false;

            if (target.HasThing)
            {
                Thing t = target.Thing;
                if (t == null || t.Destroyed || !t.Spawned || t.Map != turret.Map)
                    return false;

                if (t is Pawn pawn)
                {
                    if (pawn.Dead)
                        return false;

                    if (pawn.Downed && ShouldIgnoreDownedPawns(turret))
                        return false;

                    if (turret.Faction != null && !pawn.HostileTo(turret.Faction))
                        return false;
                }
                else if (t is IAttackTarget attackTarg)
                {
                    if (attackTarg.ThreatDisabled(turret as IAttackTargetSearcher))
                        return false;
                    if (turret.Faction != null && !t.HostileTo(turret.Faction))
                        return false;
                }
            }

            if (turret is Building_TurretGunCE ceTurret)
            {
                var fireArc = ceTurret.CompFireArc;
                if (fireArc != null && !fireArc.WithinFireArc(target))
                    return false;

                if (ceTurret.IsMortarOrProjectileFliesOverhead && turret.Map.roofGrid != null && turret.Map.roofGrid.Roofed(target.Cell))
                    return false;
            }

            if (verb != null && verb.verbProps != null)
            {
                float dist = (target.Cell - turret.Position).LengthHorizontal;
                if (dist < verb.verbProps.minRange || dist > verb.verbProps.range)
                    return false;

                if (checkLoS && !verb.CanHitTarget(target))
                    return false;
            }

            return true;
        }

        private static bool ShouldIgnoreDownedPawns(Building_Turret turret)
        {
            var preserveComp = turret.TryGetComp<CompTurretPreserveAmmo>();
            if (preserveComp != null)
                return preserveComp.preserveAmmo;

            var ext = turret.def?.GetModExtension<TurretPreserveAmmoExtension>();
            if (ext != null)
                return ext.defaultPreserveAmmo;

            return true;
        }

        public static float GetReferenceBarrelAngle(Building_Turret turret)
        {
            if (turret == null) return 0f;

            float authRot = TurretTrackingUtility.GetAuthoritativeBarrelRotation(turret);
            if (!float.IsNaN(authRot))
            {
                return Mathf.Repeat(authRot, 360f);
            }

            var barrelComp = turret.TryGetComp<CompTurretBarrel>();
            if (barrelComp != null)
            {
                return Mathf.Repeat(barrelComp.GetCurrentBarrelRotation(), 360f);
            }

            if (turret.CurrentTarget.IsValid)
            {
                return GetAngleToTarget(turret, turret.CurrentTarget);
            }

            return turret.Rotation.AsAngle;
        }

        public static float GetAngleToTarget(Building_Turret turret, LocalTargetInfo target)
        {
            if (turret == null || !target.IsValid) return 0f;
            Vector3 targetPos = target.HasThing && target.Thing != null
                ? target.Thing.TrueCenter()
                : target.Cell.ToVector3Shifted();
            Vector3 diff = targetPos - turret.TrueCenter();
            return diff.AngleFlat();
        }

        /// <summary>
        /// Estimates the physical time in seconds required for the turret to rotate through deltaAngleDeg.
        /// Accounts for NonSnapTurretExtension speed, inertial acceleration/deceleration (actuatorStrength / turretHeadWeight),
        /// and NonSnapTurretExtension.preferedAngleRange weighting.
        /// </summary>
        public float EstimateTraverseTimeSeconds(Building_Turret turret, float deltaAngleDeg)
        {
            float absAngle = Mathf.Abs(deltaAngleDeg);
            if (absAngle <= 0.1f) return 0f;

            if (turret is Building_TurretGunCE ceTurret && ceTurret.NonSnap && ceTurret.NonSnapExtension != null)
            {
                var nonSnapExt = ceTurret.NonSnapExtension;
                float maxSpeedDegPerTick = Mathf.Max(0.01f, nonSnapExt.speed);
                float accelDegPerTickSq = TurretTrackingUtility.GetAngularAcceleration(ceTurret, out _, out _);

                // Distance required to accelerate to maxSpeed and brake back to 0: d_crit = v_max^2 / a
                float critAngle = (maxSpeedDegPerTick * maxSpeedDegPerTick) / Mathf.Max(0.0001f, accelDegPerTickSq);
                float traverseTicks;
                if (absAngle <= critAngle)
                {
                    // Triangle velocity profile (accelerates halfway, brakes halfway): t = 2 * sqrt(d / a)
                    traverseTicks = 2f * Mathf.Sqrt(absAngle / Mathf.Max(0.0001f, accelDegPerTickSq));
                }
                else
                {
                    // Trapezoidal velocity profile (cruises at maxSpeed): t = d / v_max + v_max / a
                    traverseTicks = (absAngle / maxSpeedDegPerTick) + (maxSpeedDegPerTick / Mathf.Max(0.0001f, accelDegPerTickSq));
                }

                float traverseSeconds = traverseTicks / 60f;

                // Apply NonSnapTurretExtension preferred angle weighting if target is outside preferred cone
                if (nonSnapExt.preferedAngleRange > 0f && absAngle > nonSnapExt.preferedAngleRange)
                {
                    float mult = Mathf.Max(1f, nonSnapExt.angleWeightMultiplier);
                    traverseSeconds *= mult;
                }

                return traverseSeconds;
            }

            // Snap turret fallback: small angular penalty (0.25s equivalent for a 180 deg turn)
            return (absAngle / 180f) * 0.25f;
        }

        /// <summary>
        /// Computes the effective engagement cost (in tile-equivalents) for a candidate target.
        /// Lower score is higher priority:
        /// Cost = HorizontalDistanceTiles + (TraverseTimeWeight * EstimatedTraverseTimeSeconds)
        /// </summary>
        public float ComputeTargetEffectiveCost(Building_Turret turret, LocalTargetInfo target, float currentBarrelAngle, out float distTiles, out float angleDeltaDeg, out float estTraverseSec)
        {
            distTiles = (target.Cell - turret.Position).LengthHorizontal;
            float targetAngle = GetAngleToTarget(turret, target);
            angleDeltaDeg = Mathf.Abs(Mathf.DeltaAngle(currentBarrelAngle, targetAngle));
            estTraverseSec = EstimateTraverseTimeSeconds(turret, angleDeltaDeg);

            return distTiles + (TraverseTimeWeight * estTraverseSec);
        }

        /// <summary>
        /// Evaluates all valid hostile targets on the map within the turret's current dynamic vision cone
        /// (or the 45-degree focus cone when already engaged) and determines whether to acquire or switch targets.
        /// </summary>
        public bool TryEvaluateSmartTarget(
            Building_Turret turret,
            Verb verb,
            LocalTargetInfo currentTarget,
            out LocalTargetInfo chosenTarget,
            out string decisionSummary)
        {
            chosenTarget = LocalTargetInfo.Invalid;
            decisionSummary = null;

            if (!IsEffectivelyActive || turret == null || !turret.Spawned || turret.Map == null || verb == null)
                return false;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            bool currentStillValid = IsTargetValidAndAlive(turret, verb, currentTarget, checkLoS: true);

            // 1. If current target is valid and we are still inside the post-alignment commitment window, stay committed.
            if (currentStillValid && IsCommittedToTarget(turret, verb, currentTarget, out float remSec))
            {
                chosenTarget = currentTarget;
                decisionSummary = isWaitingForAlignment
                    ? $"Rotating to committed target { currentTarget.Label }"
                    : $"Committed to { currentTarget.Label } ({remSec:F1}s left)";
                return true;
            }

            float barrelAngle = GetReferenceBarrelAngle(turret);
            float activeConeHalf = currentStillValid ? FocusConeHalfDegrees : currentConeHalfDegrees;
            float minRange = verb.verbProps?.minRange ?? 0f;
            float maxRange = verb.verbProps?.range ?? 9999f;
            bool ignoreDowned = ShouldIgnoreDownedPawns(turret);
            CompFireArc fireArc = (turret as Building_TurretGunCE)?.CompFireArc;
            bool isManned = turret.TryGetComp<CompMannable>() != null;
            bool checkRoofForOverhead = turret is Building_TurretGunCE ceTurret && ceTurret.IsMortarOrProjectileFliesOverhead;

            float currentTargetCost = float.MaxValue;
            float currentTargetDist = float.MaxValue;
            if (currentStillValid)
            {
                currentTargetCost = ComputeTargetEffectiveCost(turret, currentTarget, barrelAngle, out currentTargetDist, out _, out _);
            }

            Thing bestCandidate = null;
            float bestCandidateCost = float.MaxValue;
            float bestCandidateDist = float.MaxValue;
            float bestCandidateAngle = 0f;
            float bestCandidateTraverseSec = 0f;

            // Scan hostile attack targets on the map
            IList<IAttackTarget> potentialTargets = turret.Map.attackTargetsCache?.GetPotentialTargetsFor(turret as IAttackTargetSearcher);
            if (potentialTargets != null)
            {
                for (int i = 0; i < potentialTargets.Count; i++)
                {
                    IAttackTarget attackTarget = potentialTargets[i];
                    if (attackTarget == null) continue;

                    Thing candidateThing = attackTarget.Thing;
                    if (candidateThing == null || !candidateThing.Spawned || candidateThing.Destroyed)
                        continue;

                    if (attackTarget.ThreatDisabled(turret as IAttackTargetSearcher))
                        continue;

                    if (candidateThing is Pawn pawn)
                    {
                        if (pawn.Dead) continue;
                        if (pawn.Downed && ignoreDowned) continue;
                        if (!isManned && turret.Faction != null && GenAI.MachinesLike(turret.Faction, pawn))
                            continue;
                        if (isManned && pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
                            continue;
                    }

                    // Skip current target here; we already computed currentTargetCost
                    if (currentStillValid && candidateThing == currentTarget.Thing)
                        continue;

                    if (checkRoofForOverhead && turret.Map.roofGrid != null && turret.Map.roofGrid.Roofed(candidateThing.Position))
                        continue;

                    float dist = (candidateThing.Position - turret.Position).LengthHorizontal;
                    if (dist < minRange || dist > maxRange)
                        continue;

                    LocalTargetInfo candInfo = new LocalTargetInfo(candidateThing);
                    if (fireArc != null && !fireArc.WithinFireArc(candInfo))
                        continue;

                    float candAngle = GetAngleToTarget(turret, candInfo);
                    float angleDelta = Mathf.Abs(Mathf.DeltaAngle(barrelAngle, candAngle));

                    // Vision cone filter:
                    // - When already shooting a valid target, candidate MUST be inside the 45-degree (90-deg arc) focus cone
                    //   and visibly closer in distance than the current target.
                    // - When idle/unengaged, candidate must be inside the dynamically widening currentConeHalfDegrees.
                    if (angleDelta > activeConeHalf)
                        continue;

                    if (currentStillValid && dist >= currentTargetDist)
                        continue;

                    float estTravSec = EstimateTraverseTimeSeconds(turret, angleDelta);
                    float cost = dist + (TraverseTimeWeight * estTravSec);

                    if (cost >= bestCandidateCost)
                        continue;

                    // Verify Line of Sight / CanHitTarget only for candidates that beat the current best score
                    if (!verb.CanHitTarget(candInfo))
                        continue;

                    bestCandidate = candidateThing;
                    bestCandidateCost = cost;
                    bestCandidateDist = dist;
                    bestCandidateAngle = angleDelta;
                    bestCandidateTraverseSec = estTravSec;
                }
            }

            // 2. Decide between current target and best candidate in the vision cone
            if (currentStillValid)
            {
                if (bestCandidate != null && (bestCandidateCost + MinSwitchAdvantageTiles) < currentTargetCost)
                {
                    chosenTarget = new LocalTargetInfo(bestCandidate);
                    LockOntoTarget(turret, chosenTarget, nowTick);
                    decisionSummary = $"Switched {currentTarget.Label} ({currentTargetDist:F1}c, Cost {currentTargetCost:F1}) -> {bestCandidate.LabelShortCap} ({bestCandidateDist:F1}c, dYaw {bestCandidateAngle:F1} deg, Slew {bestCandidateTraverseSec:F2}s, Cost {bestCandidateCost:F1})";

                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        AMCLogger.LogTurretTarget($"[Prioritize Closest] {turret.LabelCap} @ {turret.Position}: {decisionSummary}");
                    }
                    return true;
                }

                // Current target remains more cost-effective than rotating to any closer target
                chosenTarget = currentTarget;
                decisionSummary = $"Kept {currentTarget.Label} ({currentTargetDist:F1}c, Cost {currentTargetCost:F1})";
                return true;
            }

            // 3. No valid current target: acquire the best candidate in the dynamic vision cone
            if (bestCandidate != null)
            {
                chosenTarget = new LocalTargetInfo(bestCandidate);
                LockOntoTarget(turret, chosenTarget, nowTick);
                decisionSummary = $"Acquired {bestCandidate.LabelShortCap} ({bestCandidateDist:F1}c, dYaw {bestCandidateAngle:F1} deg, Slew {bestCandidateTraverseSec:F2}s, Cost {bestCandidateCost:F1}, Cone ±{activeConeHalf:F0} deg)";

                var settings = TurretBarrelAnimationMod.settings;
                if (settings != null && settings.logTurretTarget)
                {
                    AMCLogger.LogTurretTarget($"[Prioritize Closest] {turret.LabelCap} @ {turret.Position}: {decisionSummary}");
                }
                return true;
            }

            return false;
        }

        private static int GetTargetIdentifier(LocalTargetInfo target)
        {
            if (!target.IsValid) return -1;
            return target.HasThing && target.Thing != null
                ? target.Thing.thingIDNumber
                : target.Cell.GetHashCode();
        }

        public string GetStatusSummary(Building_Turret turret)
        {
            if (IsLockedBySprayDiscipline)
                return "OFF (Locked by Spray Discipline)";

            if (!prioritizeClosestEnabled)
                return "OFF";

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            string coneStr = $"±{currentConeHalfDegrees:F0} deg ({currentConeHalfDegrees * 2f:F0} deg arc)";

            if (turret != null && turret.CurrentTarget.IsValid && IsTargetValidAndAlive(turret, turret.AttackVerb, turret.CurrentTarget, checkLoS: false))
            {
                if (isWaitingForAlignment)
                {
                    return $"ON | Cone: {coneStr} | Rotating to target (4.0s commit pending)";
                }
                if (alignmentCompletedTick >= 0)
                {
                    int elapsed = nowTick - alignmentCompletedTick;
                    int total = CommitmentTicks;
                    if (elapsed < total)
                    {
                        float rem = (total - elapsed) / 60f;
                        return $"ON | Cone: {coneStr} | Committed: {rem:F1}s / {CommitmentSeconds:F1}s left";
                    }
                }
                return $"ON | Cone: {coneStr} | Commitment ready (Scanning closer in cone)";
            }

            return $"ON | Cone: {coneStr} (Widening +{ConeExpandDegreesPerSecond:F0} deg/s)";
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

            if (!AllowToggle)
            {
                yield break;
            }

            bool lockedBySpray = IsLockedBySprayDiscipline;
            if (lockedBySpray && prioritizeClosestEnabled)
            {
                prioritizeClosestEnabled = false;
            }

            Texture2D icon = ContentFinder<Texture2D>.Get("UI/Commands/Switch", false)
                          ?? ContentFinder<Texture2D>.Get("UI/Buttons/AMC_preserveAmmo", false);

            var toggleCmd = new Command_Toggle
            {
                defaultLabel = "Prioritize Closest",
                defaultDesc = $"When enabled, the gunner evaluates closer targets in a {FocusConeHalfDegrees * 2f:F0}° forward focus cone (±{FocusConeHalfDegrees:F0}°) after each burst, balancing target distance against turret rotation time. After rotating to a new target, the gunner commits to it for {CommitmentSeconds:F1}s unless it dies. When no target is present, the vision cone widens by ±{ConeExpandDegreesPerSecond:F0}°/s back to 360°.",
                icon = icon,
                isActive = () => prioritizeClosestEnabled && !IsLockedBySprayDiscipline,
                toggleAction = () =>
                {
                    if (IsLockedBySprayDiscipline)
                    {
                        prioritizeClosestEnabled = false;
                        return;
                    }

                    prioritizeClosestEnabled = !prioritizeClosestEnabled;
                    if (!prioritizeClosestEnabled)
                    {
                        ResetCommitment();
                    }
                    else
                    {
                        currentConeHalfDegrees = 180f;
                    }

                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logTurretTarget)
                    {
                        AMCLogger.LogTurretTarget($"[Prioritize Closest Toggle] {parent.LabelCap} @ {parent.Position} set to: {prioritizeClosestEnabled}");
                    }
                }
            };

            if (lockedBySpray)
            {
                toggleCmd.Disable("Disabled while Spray Discipline is active.");
            }

            yield return toggleCmd;
        }
    }
}
