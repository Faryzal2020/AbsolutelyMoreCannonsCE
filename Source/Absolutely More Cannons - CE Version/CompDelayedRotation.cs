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
        private int ticksRemaining = 0;
        
        public void ScheduleRotation(LocalTargetInfo target, int delayTicks = 2)
        {
            targetToAim = target;
            ticksRemaining = delayTicks;
            Log.Message($"[CompDelayedRotation] Scheduled rotation for {parent.def.defName} in {delayTicks} ticks");
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            if (ticksRemaining > 0)
            {
                Log.Message($"[CompDelayedRotation] Tick countdown for {parent.def.defName}: {ticksRemaining} ticks remaining");
                ticksRemaining--;
                
                if (ticksRemaining == 0 && targetToAim.IsValid)
                {
                    Log.Message($"[CompDelayedRotation] Countdown complete! Applying rotation now...");
                    SetTurretRotationToTarget(targetToAim);
                    Log.Message($"[CompDelayedRotation] Applied rotation to {parent.def.defName}");
                    
                    // Component has done its job, can be removed
                    targetToAim = LocalTargetInfo.Invalid;
                }
            }
        }
        
        private void SetTurretRotationToTarget(LocalTargetInfo target)
        {
            try
            {
                Log.Message($"[CompDelayedRotation] Starting rotation calculation for {parent.def.defName}");
                
                // Get the turret top - using the same approach as CompTurretBarrel
                var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var fieldNames = new[] { "top", "Top", "turretTop", "TurretTop", "gunTop", "GunTop" };
                
                object turretTop = null;
                string foundFieldName = null;
                
                foreach (var fieldName in fieldNames)
                {
                    var topField = parent.GetType().GetField(fieldName, bindingFlags);
                    if (topField != null)
                    {
                        turretTop = topField.GetValue(parent);
                        if (turretTop != null)
                        {
                            foundFieldName = fieldName;
                            break;
                        }
                    }
                }
                
                if (turretTop == null)
                {
                    Log.Warning($"[CompDelayedRotation] Could not find turret top field for {parent.GetType().Name}");
                    return;
                }
                
                Log.Message($"[CompDelayedRotation] Found turret top via field '{foundFieldName}': {turretTop.GetType().Name}");
                
                // Calculate angle from turret to target
                Vector3 turretPos = parent.DrawPos;
                Vector3 targetPos = target.IsValid ? target.CenterVector3 : turretPos;
                
                Log.Message($"[CompDelayedRotation] Turret position: {turretPos}");
                Log.Message($"[CompDelayedRotation] Target position: {targetPos}");
                
                // Calculate angle in degrees
                Vector3 direction = targetPos - turretPos;
                Log.Message($"[CompDelayedRotation] Direction vector: {direction}");
                
                float angleRad = Mathf.Atan2(direction.z, direction.x);
                float angleDeg = angleRad * Mathf.Rad2Deg;
                
                Log.Message($"[CompDelayedRotation] Atan2 angle: {angleDeg:F1}° (raw)");
                
                // RimWorld uses north = 0°, rotating clockwise
                // Atan2 gives us east = 0°, rotating counter-clockwise
                // Convert: RimWorld angle = 90 - Atan2 angle
                float rimWorldAngle = 90f - angleDeg;
                
                Log.Message($"[CompDelayedRotation] After conversion: {rimWorldAngle:F1}°");
                
                // Normalize to 0-360 range
                while (rimWorldAngle < 0f) rimWorldAngle += 360f;
                while (rimWorldAngle >= 360f) rimWorldAngle -= 360f;
                
                Log.Message($"[CompDelayedRotation] After normalization: {rimWorldAngle:F1}°");
                
                // Set the CurRotation property
                var curRotationProp = turretTop.GetType().GetProperty("CurRotation");
                if (curRotationProp != null && curRotationProp.CanWrite)
                {
                    // Get current rotation before setting
                    float oldRotation = (float)curRotationProp.GetValue(turretTop);
                    Log.Message($"[CompDelayedRotation] Current turret rotation: {oldRotation:F1}°");
                    
                    curRotationProp.SetValue(turretTop, rimWorldAngle);
                    Log.Message($"[CompDelayedRotation] ✓ Set turret rotation: {oldRotation:F1}° → {rimWorldAngle:F1}°");
                }
                else
                {
                    Log.Warning($"[CompDelayedRotation] Could not set CurRotation property (exists: {curRotationProp != null}, writable: {curRotationProp?.CanWrite})");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CompDelayedRotation] Failed to set turret rotation: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_TargetInfo.Look(ref targetToAim, "targetToAim");
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
        }
    }
}
