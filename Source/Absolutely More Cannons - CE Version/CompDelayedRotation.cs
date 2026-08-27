using Verse;
using UnityEngine;
using System;
using System.Reflection;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Temporary component that applies delayed rotation to a turret.
    /// Used after mode swapping to allow the turret top to initialize before setting rotation.
    /// </summary>
    public class CompDelayedRotation : ThingComp
    {
        private LocalTargetInfo targetToAim = LocalTargetInfo.Invalid;
        private float explicitAngle = -1f;
        private int ticksRemaining = 0;
        
        public void ScheduleRotation(LocalTargetInfo target, int delayTicks = 2)
        {
            targetToAim = target;
            explicitAngle = -1f;
            ticksRemaining = delayTicks;
            Log.Message($"[CompDelayedRotation] Scheduled target rotation for {parent.def.defName} in {delayTicks} ticks");
        }

        public void ScheduleRotationAngle(float angleDeg, int delayTicks = 2)
        {
            targetToAim = LocalTargetInfo.Invalid;
            explicitAngle = angleDeg;
            ticksRemaining = delayTicks;
            Log.Message($"[CompDelayedRotation] Scheduled angle rotation ({angleDeg:F1}°) for {parent.def.defName} in {delayTicks} ticks");
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            if (ticksRemaining > 0)
            {
                ticksRemaining--;
                
                if (ticksRemaining == 0)
                {
                    if (targetToAim.IsValid)
                    {
                        SetTurretRotationToTarget(targetToAim);
                    }
                    else if (explicitAngle >= 0f)
                    {
                        SetTurretRotationAngle(explicitAngle);
                    }
                    
                    Cleanup();
                }
            }
        }

        private void Cleanup()
        {
            targetToAim = LocalTargetInfo.Invalid;
            explicitAngle = -1f;
            try
            {
                var comps = parent?.AllComps;
                if (comps != null && comps.Contains(this))
                {
                    comps.Remove(this);
                    Log.Message($"[CompDelayedRotation] Cleaned up temporary component from {parent.def.defName}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CompDelayedRotation] Failed to self-remove component: {ex.Message}");
            }
        }
        
        private object GetTurretTop()
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fieldNames = new[] { "top", "Top", "turretTop", "TurretTop", "gunTop", "GunTop" };
            
            foreach (var fieldName in fieldNames)
            {
                var topField = parent.GetType().GetField(fieldName, bindingFlags);
                if (topField != null)
                {
                    object turretTop = topField.GetValue(parent);
                    if (turretTop != null)
                        return turretTop;
                }
            }
            return null;
        }

        private void SetTurretRotationAngle(float rimWorldAngle)
        {
            try
            {
                if (parent != null)
                {
                    var nonSnapField = parent.GetType().GetField("NonSnapTurretRot", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (nonSnapField != null)
                    {
                        nonSnapField.SetValue(parent, rimWorldAngle);
                        Log.Message($"[CompDelayedRotation] Set NonSnapTurretRot on {parent.def.defName} to {rimWorldAngle:F1}°");
                    }
                }

                object turretTop = GetTurretTop();
                if (turretTop == null) return;
                
                var curRotationProp = turretTop.GetType().GetProperty("CurRotation");
                if (curRotationProp != null && curRotationProp.CanWrite)
                {
                    curRotationProp.SetValue(turretTop, rimWorldAngle);
                    Log.Message($"[CompDelayedRotation] Set turret rotation angle directly to {rimWorldAngle:F1}°");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CompDelayedRotation] Failed to set turret rotation angle: {ex.Message}");
            }
        }
        
        private void SetTurretRotationToTarget(LocalTargetInfo target)
        {
            try
            {
                object turretTop = GetTurretTop();
                if (turretTop == null)
                {
                    Log.Warning($"[CompDelayedRotation] Could not find turret top field for {parent.GetType().Name}");
                    return;
                }
                
                Vector3 turretPos = parent.DrawPos;
                Vector3 targetPos = target.IsValid ? target.CenterVector3 : turretPos;
                Vector3 direction = targetPos - turretPos;
                
                float angleRad = Mathf.Atan2(direction.z, direction.x);
                float angleDeg = angleRad * Mathf.Rad2Deg;
                float rimWorldAngle = 90f - angleDeg;
                
                while (rimWorldAngle < 0f) rimWorldAngle += 360f;
                while (rimWorldAngle >= 360f) rimWorldAngle -= 360f;
                
                SetTurretRotationAngle(rimWorldAngle);
            }
            catch (Exception ex)
            {
                Log.Error($"[CompDelayedRotation] Failed to set turret rotation to target: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_TargetInfo.Look(ref targetToAim, "targetToAim");
            Scribe_Values.Look(ref explicitAngle, "explicitAngle", -1f);
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
        }
    }
}

