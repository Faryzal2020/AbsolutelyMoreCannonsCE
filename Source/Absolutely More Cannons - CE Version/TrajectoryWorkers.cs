using System;
using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Trajectory worker supporting delayed guidance, apex/descending activation, and target retargeting (VLS / cruise missile / guided artillery mechanics).
    /// Holds off target-seeking acceleration and homing steering until guidanceDelay has elapsed
    /// and/or when the projectile transitions from ascending to descending altitude (velocity.y <= 0).
    /// Retargets hostiles within retargetRadius if the initial target dies or is destroyed.
    /// Optionally overrides initial launch angle with vlsLaunchAngle for steep upward launch.
    /// </summary>
    public class VLSTrajectoryWorker : BallisticsTrajectoryWorker
    {
        public override float ShotAngle(CombatExtended.ProjectilePropertiesCE projectilePropsCE, Vector3 source, Vector3 targetPos, float? speed = null)
        {
            if (projectilePropsCE is ProjectilePropertiesCE amcProps && amcProps.vlsLaunchAngle > 0f)
            {
                return Mathf.Clamp(amcProps.vlsLaunchAngle, 1f, 89.9f) * Mathf.Deg2Rad;
            }
            return base.ShotAngle(projectilePropsCE, source, targetPos, speed);
        }

        public override Vector3 MoveForward(ProjectileCE projectile)
        {
            var amcProps = projectile.Props as ProjectilePropertiesCE;
            bool isGuidedActive = TrajectoryWorkerUtility.IsGuidanceActive(projectile, amcProps);

            Vector3 currentPos = projectile.ExactPosition;
            Vector3 nextPos;

            if (!isGuidedActive && amcProps != null && amcProps.flyOverhead)
            {
                currentPos = TrajectoryWorkerUtility.GetLerpedPositionAtTick(projectile, projectile.FlightTicks);
                nextPos = TrajectoryWorkerUtility.GetLerpedPositionAtTick(projectile, projectile.FlightTicks + 1);

                projectile.velocity = nextPos - currentPos;
            }
            else
            {
                projectile.shotSpeed = GetSpeed(projectile.velocity);
                nextPos = BallisticMove(projectile);
            }

            TrajectoryWorkerUtility.UpdateProjectileRotation(projectile);
            return nextPos;
        }

        protected override void ReactiveAcceleration(ProjectileCE projectile)
        {
            var amcProps = projectile.Props as ProjectilePropertiesCE;

            LocalTargetInfo currentTarget = projectile.intendedTarget;
            if (!TrajectoryWorkerUtility.CheckOrRetarget(projectile, amcProps, ref currentTarget))
            {
                return;
            }

            Vector3 targetPos = currentTarget.Thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            targetPos = targetPos.WithY(projectile.intendedTargetHeight);

            Vector3 delta = targetPos - projectile.ExactPosition;
            if (delta.sqrMagnitude < 0.01f)
            {
                return;
            }

            // 1. Rocket motor acceleration (speedGain & fuelTicks)
            if (projectile.fuelTicks > 0)
            {
                projectile.fuelTicks--;
                float acceleration = projectile.Props.speedGain / GenTicks.TicksPerRealSecond / GenTicks.TicksPerRealSecond;
                projectile.velocity += delta.normalized * acceleration;
            }

            // 2. Angular homing steering (homingAcceleration)
            float steeringRate = amcProps?.homingAcceleration ?? 0f;
            if (steeringRate <= 0f && projectile.homingAcceleration > 0f)
            {
                steeringRate = projectile.homingAcceleration;
            }

            if (steeringRate > 0f)
            {
                // Don't steer if projectile has passed target (moving away)
                if (Vector3.Dot(projectile.velocity, delta) > 0f)
                {
                    Vector3 targetVelocity = delta.normalized * projectile.velocity.magnitude;
                    projectile.velocity = Vector3.RotateTowards(projectile.velocity, targetVelocity, steeringRate, 0f);
                }
            }
        }

        public override Vector3 ExactPosToDrawPos(Vector3 exactPosition, int FlightTicks, int ticksToTruePosition, float altitude)
        {
            float sh = Mathf.Max(0f, exactPosition.y * 0.84f);
            if (FlightTicks < ticksToTruePosition)
            {
                sh *= (float)FlightTicks / ticksToTruePosition;
            }
            return new Vector3(exactPosition.x, altitude, exactPosition.z + sh);
        }

        public override bool GuidedProjectile => true;
    }

    /// <summary>
    /// Trajectory worker for smart rockets with delayed guidance activation and retargeting support.
    /// </summary>
    public class DelayedSmartRocketTrajectoryWorker : BallisticsTrajectoryWorker
    {
        public override Vector3 MoveForward(ProjectileCE projectile)
        {
            var amcProps = projectile.Props as ProjectilePropertiesCE;
            bool isGuidedActive = TrajectoryWorkerUtility.IsGuidanceActive(projectile, amcProps);

            Vector3 currentPos = projectile.ExactPosition;
            Vector3 nextPos;

            if (!isGuidedActive && amcProps != null && amcProps.flyOverhead)
            {
                currentPos = TrajectoryWorkerUtility.GetLerpedPositionAtTick(projectile, projectile.FlightTicks);
                nextPos = TrajectoryWorkerUtility.GetLerpedPositionAtTick(projectile, projectile.FlightTicks + 1);

                projectile.velocity = nextPos - currentPos;
            }
            else
            {
                projectile.shotSpeed = GetSpeed(projectile.velocity);
                nextPos = BallisticMove(projectile);
            }

            TrajectoryWorkerUtility.UpdateProjectileRotation(projectile);
            return nextPos;
        }

        protected override void ReactiveAcceleration(ProjectileCE projectile)
        {
            var amcProps = projectile.Props as ProjectilePropertiesCE;

            LocalTargetInfo currentTarget = projectile.intendedTarget;
            if (!TrajectoryWorkerUtility.CheckOrRetarget(projectile, amcProps, ref currentTarget))
            {
                return;
            }

            if (projectile.fuelTicks < 1)
            {
                return;
            }

            projectile.fuelTicks--;
            Vector3 targetPos = currentTarget.Thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            targetPos = targetPos.WithY(projectile.intendedTargetHeight);

            Vector3 delta = targetPos - projectile.ExactPosition;
            projectile.velocity += delta.normalized * projectile.Props.speedGain / GenTicks.TicksPerRealSecond / GenTicks.TicksPerRealSecond;
        }

        public override Vector3 ExactPosToDrawPos(Vector3 exactPosition, int FlightTicks, int ticksToTruePosition, float altitude)
        {
            float sh = Mathf.Max(0f, exactPosition.y * 0.84f);
            if (FlightTicks < ticksToTruePosition)
            {
                sh *= (float)FlightTicks / ticksToTruePosition;
            }
            return new Vector3(exactPosition.x, altitude, exactPosition.z + sh);
        }

        public override bool GuidedProjectile => true;
    }

    /// <summary>
    /// Trajectory worker for homing bullets/missiles with delayed steering activation and retargeting support.
    /// </summary>
    public class DelayedHomingTrajectoryWorker : BallisticsTrajectoryWorker
    {
        public override Vector3 MoveForward(ProjectileCE projectile)
        {
            var amcProps = projectile.Props as ProjectilePropertiesCE;
            bool isGuidedActive = TrajectoryWorkerUtility.IsGuidanceActive(projectile, amcProps);

            Vector3 currentPos = projectile.ExactPosition;
            Vector3 nextPos;

            if (!isGuidedActive && amcProps != null && amcProps.flyOverhead)
            {
                currentPos = TrajectoryWorkerUtility.GetLerpedPositionAtTick(projectile, projectile.FlightTicks);
                nextPos = TrajectoryWorkerUtility.GetLerpedPositionAtTick(projectile, projectile.FlightTicks + 1);

                projectile.velocity = nextPos - currentPos;
            }
            else
            {
                projectile.shotSpeed = GetSpeed(projectile.velocity);
                nextPos = BallisticMove(projectile);
            }

            TrajectoryWorkerUtility.UpdateProjectileRotation(projectile);
            return nextPos;
        }

        protected override void ReactiveAcceleration(ProjectileCE projectile)
        {
            var amcProps = projectile.Props as ProjectilePropertiesCE;

            float steeringRate = amcProps?.homingAcceleration ?? 0f;
            if (steeringRate <= 0f && projectile.homingAcceleration > 0f)
            {
                steeringRate = projectile.homingAcceleration;
            }

            if (steeringRate <= 0f)
            {
                return;
            }

            LocalTargetInfo currentTarget = projectile.intendedTarget;
            if (!TrajectoryWorkerUtility.CheckOrRetarget(projectile, amcProps, ref currentTarget))
            {
                return;
            }

            Vector3 targetPos = currentTarget.Thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            targetPos.y = projectile.ExactPosition.y;

            Vector3 delta = targetPos - projectile.ExactPosition;
            if (delta.sqrMagnitude < 0.01f)
            {
                return;
            }

            if (Vector3.Dot(projectile.velocity, delta) <= 0f)
            {
                return;
            }

            Vector3 targetVelocity = delta.normalized * projectile.velocity.magnitude;
            projectile.velocity = Vector3.RotateTowards(projectile.velocity, targetVelocity, steeringRate, 0f);
        }

        public override Vector3 ExactPosToDrawPos(Vector3 exactPosition, int FlightTicks, int ticksToTruePosition, float altitude)
        {
            float sh = Mathf.Max(0f, exactPosition.y * 0.84f);
            if (FlightTicks < ticksToTruePosition)
            {
                sh *= (float)FlightTicks / ticksToTruePosition;
            }
            return new Vector3(exactPosition.x, altitude, exactPosition.z + sh);
        }

        public override bool GuidedProjectile => true;
    }

    /// <summary>
    /// Helper utilities for AMC trajectory workers (retargeting, search logic, guidance status).
    /// </summary>
    public static class TrajectoryWorkerUtility
    {
        private static readonly AccessTools.FieldRef<ProjectileCE, Quaternion?> DrawRotationRef =
            AccessTools.FieldRefAccess<ProjectileCE, Quaternion?>("_drawRotation");

        public static float CalculateScreenAngle(Vector3 velocity)
        {
            float screenVx = velocity.x;
            float screenVy = velocity.z + (velocity.y * 0.84f);

            if (Mathf.Abs(screenVx) < 0.0001f && Mathf.Abs(screenVy) < 0.0001f)
            {
                return 0f;
            }

            return (new Vector3(screenVx, 0f, screenVy)).AngleFlat();
        }

        public static void UpdateProjectileRotation(ProjectileCE projectile)
        {
            if (projectile == null || projectile.velocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float screenAngle = CalculateScreenAngle(projectile.velocity);
            try
            {
                DrawRotationRef(projectile) = Quaternion.AngleAxis(screenAngle, Vector3.up);
            }
            catch
            {
                // Silently ignore if _drawRotation field access fails
            }

            // Note: shotRotation is left untouched so that ExactRotation (ground shadow) remains 100% accurate.
        }

        public static void InitializeLaunchRotation(ProjectileCE projectile)
        {
            if (projectile == null) return;

            Vector3 vel = projectile.velocity;
            if (vel.sqrMagnitude < 0.0001f && projectile.TrajectoryWorker != null)
            {
                vel = projectile.TrajectoryWorker.GetInitialVelocity(projectile.shotSpeed, projectile.shotRotation, projectile.shotAngle);
            }

            float screenAngle = CalculateScreenAngle(vel);
            try
            {
                DrawRotationRef(projectile) = Quaternion.AngleAxis(screenAngle, Vector3.up);
            }
            catch { }
        }

        public static bool IsGuidanceActive(ProjectileCE projectile, ProjectilePropertiesCE amcProps)
        {
            if (amcProps == null) return true;

            // 1. Guidance delay condition
            if (projectile.FlightTicks < amcProps.GuidanceDelayTicks)
            {
                return false;
            }

            // 2. Descending condition (active only when vertical velocity <= 0)
            if (amcProps.guidanceOnDescending && projectile.velocity.y > 0f)
            {
                return false;
            }

            return true;
        }

        public static Vector3 GetLerpedPositionAtTick(ProjectileCE projectile, int tick)
        {
            float startingTicks = projectile.startingTicksToImpact;
            if (startingTicks <= 0f) startingTicks = 1f;

            Vector2 lerp2D = Vector2.LerpUnclamped(projectile.origin, projectile.Destination, (float)tick / startingTicks);

            float seconds = (float)tick / GenTicks.TicksPerRealSecond;
            float height = projectile.shotHeight + (projectile.shotSpeed * Mathf.Sin(projectile.shotAngle) * seconds) - (0.5f * projectile.GravityPerWidth * seconds * seconds);
            if (height < 0f) height = 0f;

            return new Vector3(lerp2D.x, height, lerp2D.y);
        }

        public static bool CheckOrRetarget(ProjectileCE projectile, ProjectilePropertiesCE amcProps, ref LocalTargetInfo currentTarget)
        {
            bool isInvalid = currentTarget.ThingDestroyed || (currentTarget.Thing is Pawn p && (p.Dead || p.Downed));
            if (!isInvalid)
            {
                return true;
            }

            float radius = amcProps?.retargetRadius ?? 0f;
            if (radius <= 0f)
            {
                return false;
            }

            Map map = projectile.Map;
            if (map == null)
            {
                return false;
            }

            Vector3 searchCenter = currentTarget.Thing?.DrawPos ?? (currentTarget.Cell.IsValid ? currentTarget.Cell.ToVector3Shifted() : projectile.ExactPosition);
            IntVec3 centerCell = searchCenter.ToIntVec3();

            Thing launcher = projectile.launcher;
            Faction launcherFaction = launcher?.Faction ?? Faction.OfPlayer;

            Thing closestTarget = null;
            float closestDistSq = float.MaxValue;
            float radiusSq = radius * radius;

            int radiusCells = Mathf.CeilToInt(radius);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(centerCell, radiusCells, true))
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> thingList = cell.GetThingList(map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing t = thingList[i];
                    if (t == null || t.Destroyed) continue;

                    bool isHostile = false;
                    if (launcher != null)
                    {
                        isHostile = GenHostility.HostileTo(t, launcher);
                    }
                    else if (launcherFaction != null)
                    {
                        isHostile = GenHostility.HostileTo(t, launcherFaction);
                    }

                    if (!isHostile) continue;

                    if (t is Pawn enemyPawn)
                    {
                        if (enemyPawn.Dead || enemyPawn.Downed) continue;
                    }
                    else if (t is Building_TurretGun || t is Building_TurretGunCE)
                    {
                        // Valid turret target
                    }
                    else if (t is Building b)
                    {
                        if (b.Faction == null) continue;
                    }
                    else
                    {
                        continue;
                    }

                    float distSq = (t.DrawPos - searchCenter).sqrMagnitude;
                    if (distSq <= radiusSq && distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closestTarget = t;
                    }
                }
            }

            if (closestTarget != null)
            {
                projectile.intendedTarget = new LocalTargetInfo(closestTarget);
                currentTarget = projectile.intendedTarget;
                return true;
            }

            return false;
        }
    }
}
