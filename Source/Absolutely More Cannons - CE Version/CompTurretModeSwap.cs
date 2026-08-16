using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component that allows swapping a turret between direct and indirect fire modes
    /// by deconstructing and rebuilding with the alternate turret def.
    /// </summary>
    public class CompTurretModeSwap : ThingComp
    {
        private CompProperties_TurretModeSwap Props => (CompProperties_TurretModeSwap)props;
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // Only show gizmo if alternate def is configured
            if (string.IsNullOrEmpty(Props.alternateDef))
                yield break;
                
            // Hide only if explicitly owned by a non-player faction
            if (parent.Faction != null && !parent.Faction.IsPlayer)
                yield break;
                
            var command = new Command_Action
            {
                defaultLabel = Props.gizmoLabel ?? "Switch Fire Mode",
                defaultDesc = Props.gizmoDesc ?? "Rebuild this turret in alternate fire mode",
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIcon ?? "UI/Commands/Attack", reportFailure: false),
                action = () => TrySwapMode(),
                hotKey = KeyBindingDefOf.Misc1
            };

            try
            {
                // Guard: Check if turret is broken down
                var breakdownComp = parent.TryGetComp<CompBreakdownable>();
                if (breakdownComp != null && breakdownComp.BrokenDown)
                {
                    command.Disable("Turret is broken down.");
                }
                else if (IsFiringOrBursting(parent))
                {
                    command.Disable("Turret is currently firing.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Error updating gizmo state: {ex.Message}");
            }

            yield return command;
        }

        private bool IsFiringOrBursting(Thing turret)
        {
            try
            {
                var gun = GetTurretGun(turret);
                if (gun != null)
                {
                    var eq = gun.TryGetComp<CompEquippable>();
                    if (eq?.PrimaryVerb != null && eq.PrimaryVerb.state == VerbState.Bursting)
                    {
                        return true;
                    }
                }
                
                var warmupField = turret.GetType().GetField("burstWarmupTicksLeft", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (warmupField != null)
                {
                    object val = warmupField.GetValue(turret);
                    if (val is int ticks && ticks > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to check firing state: {ex.Message}");
            }
            return false;
        }
        
        private void TrySwapMode()
        {
            Log.Message($"[TurretModeSwap] ===== Starting mode swap for {parent.def.defName} =====");
            
            // Get the alternate turret def
            var alternateDef = DefDatabase<ThingDef>.GetNamedSilentFail(Props.alternateDef);
            if (alternateDef == null)
            {
                Log.Error($"[TurretModeSwap] Could not find alternate def: {Props.alternateDef}");
                return;
            }
            
            // Store current state
            var map = parent.Map;
            var position = parent.Position;
            var rotation = parent.Rotation;
            var faction = parent.Faction;
            bool wasSelected = Find.Selector.IsSelected(parent);
            float healthPercent = (float)parent.HitPoints / (float)parent.MaxHitPoints;
            float savedBarrelRotation = GetTurretTopRotation(parent);
            
            // Get forced target
            LocalTargetInfo forcedTarget = GetForcedTarget(parent);
            Log.Message($"[TurretModeSwap] Saved target: {(forcedTarget.IsValid ? forcedTarget.ToString() : "None")}, Saved barrel rotation: {savedBarrelRotation:F1}°");
            
            // Get manning pawn
            Pawn manningPawn = GetManningPawn(parent);
            Log.Message($"[TurretModeSwap] Saved manning pawn: {manningPawn?.LabelShort ?? "None"}");
            
            // Get power state
            var oldPowerComp = parent.TryGetComp<CompPowerTrader>();
            bool wasPowered = oldPowerComp?.PowerOn ?? false;
            Log.Message($"[TurretModeSwap] Saved power state: {(wasPowered ? "ON" : "OFF")}");
            
            // Get burst cooldown
            int burstCooldown = GetBurstCooldown(parent);
            Log.Message($"[TurretModeSwap] Saved burst cooldown: {burstCooldown} ticks");
            
            // Get ammo info
            ThingDef spawnedAmmoType = null;
            int totalAmmoToRestore = 0;
            var gun = GetTurretGun(parent);
            if (gun != null)
            {
                int ammo = GetCurrentAmmo(gun, out ThingDef ammoType);
                if (ammo > 0 && ammoType != null)
                {
                    Log.Message($"[TurretModeSwap] Will restore {ammo}x {ammoType.defName}");
                    spawnedAmmoType = ammoType;
                    totalAmmoToRestore = ammo;
                }
            }
            
            // Get heat value from smoke comp
            float currentHeat = 0f;
            var oldSmokeComp = parent.TryGetComp<CompTurretSmoker>();
            if (oldSmokeComp != null)
            {
                currentHeat = oldSmokeComp.CurrentBurstHeat;
                Log.Message($"[TurretModeSwap] Saved heat value: {currentHeat:F1}");
                
                // Manually unregister from smoke manager BEFORE destroying
                var smokeManager = map.GetComponent<MapComponent_TurretSmokeManager>();
                if (smokeManager != null)
                {
                    smokeManager.UnregisterSmoker(oldSmokeComp);
                    Log.Message($"[TurretModeSwap] Unregistered old turret from smoke manager");
                }
            }
            
            // Get FCS state before destroying old turret
            Thing loadedFCSItem = null;
            ThingDef savedTargetFCSDef = null;
            var oldFCSComp = parent.TryGetComp<CompTurretFCS>();
            if (oldFCSComp != null)
            {
                savedTargetFCSDef = oldFCSComp.targetFCSDef;
                var fcsContainer = oldFCSComp.GetDirectlyHeldThings();
                if (fcsContainer != null && fcsContainer.Count > 0 && oldFCSComp.LoadedFCSItem != null)
                {
                    loadedFCSItem = fcsContainer.Take(oldFCSComp.LoadedFCSItem);
                    Log.Message($"[TurretModeSwap] Extracted loaded FCS: {loadedFCSItem?.def?.defName ?? "None"}");
                }
            }
            
            // Destroy current turret (no resources)
            Log.Message($"[TurretModeSwap] Destroying old turret...");
            parent.Destroy(DestroyMode.Vanish);
            
            // Spawn new turret
            Log.Message($"[TurretModeSwap] Spawning new turret: {alternateDef.defName}");
            var newTurret = ThingMaker.MakeThing(alternateDef, parent.Stuff);
            newTurret.SetFactionDirect(faction);
            newTurret.HitPoints = Mathf.Clamp(Mathf.RoundToInt(healthPercent * newTurret.MaxHitPoints), 1, newTurret.MaxHitPoints);
            
            GenSpawn.Spawn(newTurret, position, map, rotation);
            Log.Message($"[TurretModeSwap] New turret spawned");

            // Restore FCS state
            if (loadedFCSItem != null || savedTargetFCSDef != null)
            {
                var newFCSComp = newTurret.TryGetComp<CompTurretFCS>();
                if (newFCSComp != null)
                {
                    newFCSComp.targetFCSDef = savedTargetFCSDef;
                    if (loadedFCSItem != null)
                    {
                        var newFcsContainer = newFCSComp.GetDirectlyHeldThings();
                        if (newFcsContainer != null)
                        {
                            newFcsContainer.TryAdd(loadedFCSItem);
                            Log.Message($"[TurretModeSwap] Restored loaded FCS ({loadedFCSItem.def.defName}) to new turret");
                        }
                    }
                }
                else if (loadedFCSItem != null)
                {
                    GenPlace.TryPlaceThing(loadedFCSItem, position, map, ThingPlaceMode.Near);
                    Log.Warning($"[TurretModeSwap] New turret lacks CompTurretFCS. Dropped {loadedFCSItem.def.defName} on ground.");
                }
            }

            // Restore UI selection
            if (wasSelected)
            {
                Find.Selector.Select(newTurret);
            }
            
            // Force power connection & state immediately if it was powered
            var newPowerComp = newTurret.TryGetComp<CompPowerTrader>();
            if (newPowerComp != null)
            {
                try
                {
                    var setupMethod = typeof(CompPower).GetMethod("SetUpPowerVars", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    setupMethod?.Invoke(newPowerComp, null);
                    
                    if (wasPowered)
                    {
                        newPowerComp.PowerOn = true;
                        Log.Message($"[TurretModeSwap] Connected to power grid & activated power immediately");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[TurretModeSwap] Failed to connect power immediately: {ex.Message}");
                }
            }
            
            // Restore burst cooldown
            if (burstCooldown > 0)
            {
                SetBurstCooldown(newTurret, burstCooldown);
                Log.Message($"[TurretModeSwap] Restored burst cooldown: {burstCooldown} ticks");
            }
            
            // Restore heat value to new smoke comp
            if (currentHeat > 0f)
            {
                var newSmokeComp = newTurret.TryGetComp<CompTurretSmoker>();
                if (newSmokeComp != null)
                {
                    newSmokeComp.CurrentBurstHeat = currentHeat;
                    Log.Message($"[TurretModeSwap] Restored heat value: {currentHeat:F1}");
                }
            }
            
            // Restore barrel rotation and forced target
            if (forcedTarget.IsValid)
            {
                SetForcedTarget(newTurret, forcedTarget);
                Log.Message($"[TurretModeSwap] Restored target: {forcedTarget}");
            }

            var turretWithComps = newTurret as ThingWithComps;
            if (turretWithComps != null)
            {
                var delayedRotationComp = new CompDelayedRotation();
                delayedRotationComp.parent = turretWithComps;
                delayedRotationComp.Initialize(null);
                turretWithComps.AllComps.Add(delayedRotationComp);

                if (forcedTarget.IsValid)
                {
                    delayedRotationComp.ScheduleRotation(forcedTarget, 2);
                }
                else if (savedBarrelRotation >= 0f)
                {
                    delayedRotationComp.ScheduleRotationAngle(savedBarrelRotation, 2);
                }
            }
            
            // Reassign manning pawn
            if (manningPawn != null && !manningPawn.Dead && manningPawn.Spawned)
            {
                ReassignManningPawn(newTurret, manningPawn);
                Log.Message($"[TurretModeSwap] Reassigned manning pawn: {manningPawn.LabelShort}");
            }
            
            // Directly restore ammo into CompAmmoUser without spawning items on the ground
            if (spawnedAmmoType != null && totalAmmoToRestore > 0)
            {
                RestoreTurretAmmo(newTurret, spawnedAmmoType, totalAmmoToRestore);
            }
            
            // Flash effect and message
            FleckMaker.ThrowSmoke(position.ToVector3(), map, 1f);
            Messages.Message(
                $"{newTurret.Label} fire mode switched",
                newTurret,
                MessageTypeDefOf.NeutralEvent,
                historical: false
            );
            
            Log.Message($"[TurretModeSwap] ===== Mode swap complete =====");
        }
        
        private ThingWithComps GetTurretGun(Thing turret)
        {
            try
            {
                var gunProp = turret.GetType().GetProperty("Gun");
                if (gunProp != null)
                {
                    return gunProp.GetValue(turret) as ThingWithComps;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to get turret gun: {ex.Message}");
            }
            
            return null;
        }
        
        private float GetTurretTopRotation(Thing turret)
        {
            try
            {
                var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var fieldNames = new[] { "top", "Top", "turretTop", "TurretTop", "gunTop", "GunTop" };
                foreach (var fieldName in fieldNames)
                {
                    var topField = turret.GetType().GetField(fieldName, bindingFlags);
                    if (topField != null)
                    {
                        object turretTop = topField.GetValue(turret);
                        if (turretTop != null)
                        {
                            var curRotationProp = turretTop.GetType().GetProperty("CurRotation");
                            if (curRotationProp != null)
                            {
                                return (float)curRotationProp.GetValue(turretTop);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to get turret top rotation: {ex.Message}");
            }
            return -1f;
        }

        private LocalTargetInfo GetForcedTarget(Thing turret)
        {
            try
            {
                var forcedTargetField = turret.GetType().GetField("forcedTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                if (forcedTargetField != null)
                {
                    return (LocalTargetInfo)forcedTargetField.GetValue(turret);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to get forced target: {ex.Message}");
            }
            
            return LocalTargetInfo.Invalid;
        }
        
        private void SetForcedTarget(Thing turret, LocalTargetInfo target)
        {
            try
            {
                var forcedTargetField = turret.GetType().GetField("forcedTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                if (forcedTargetField != null)
                {
                    forcedTargetField.SetValue(turret, target);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to set forced target: {ex.Message}");
            }
        }
        
        private Pawn GetManningPawn(Thing turret)
        {
            try
            {
                var mannable = turret.TryGetComp<CompMannable>();
                if (mannable != null)
                {
                    var manningPawnProp = mannable.GetType().GetProperty("ManningPawn");
                    if (manningPawnProp != null)
                    {
                        return manningPawnProp.GetValue(mannable) as Pawn;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to get manning pawn: {ex.Message}");
            }
            
            return null;
        }
        
        private void ReassignManningPawn(Thing turret, Pawn pawn)
        {
            try
            {
                var mannable = turret.TryGetComp<CompMannable>();
                if (mannable != null)
                {
                    var job = JobMaker.MakeJob(JobDefOf.ManTurret, turret);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to reassign manning pawn: {ex.Message}");
            }
        }
        
        private int GetBurstCooldown(Thing turret)
        {
            try
            {
                var cooldownField = turret.GetType().GetField("burstCooldownTicksLeft");
                if (cooldownField != null)
                {
                    return (int)cooldownField.GetValue(turret);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to get burst cooldown: {ex.Message}");
            }
            
            return 0;
        }
        
        private void SetBurstCooldown(Thing turret, int ticks)
        {
            try
            {
                var cooldownField = turret.GetType().GetField("burstCooldownTicksLeft");
                if (cooldownField != null)
                {
                    cooldownField.SetValue(turret, ticks);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to set burst cooldown: {ex.Message}");
            }
        }
        
        private int GetCurrentAmmo(ThingWithComps gun, out ThingDef ammoType)
        {
            ammoType = null;
            
            try
            {
                var compAmmoUserType = Type.GetType("CombatExtended.CompAmmoUser, CombatExtended");
                if (compAmmoUserType == null) return 0;
                
                var ammoComp = gun.AllComps.Find(c => c.GetType() == compAmmoUserType || compAmmoUserType.IsAssignableFrom(c.GetType()));
                if (ammoComp == null) return 0;
                
                var curMagCountProp = compAmmoUserType.GetProperty("CurMagCount");
                int count = 0;
                if (curMagCountProp != null)
                {
                    count = (int)curMagCountProp.GetValue(ammoComp);
                }
                
                var currentAmmoField = compAmmoUserType.GetField("currentAmmoInt", BindingFlags.Instance | BindingFlags.NonPublic);
                if (currentAmmoField != null)
                {
                    ammoType = currentAmmoField.GetValue(ammoComp) as ThingDef;
                }
                
                return count;
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to get ammo info: {ex.Message}");
            }
            
            return 0;
        }
        
        private void RestoreTurretAmmo(Thing turret, ThingDef ammoType, int totalAmmoToRestore)
        {
            try
            {
                var gun = GetTurretGun(turret);
                if (gun == null)
                {
                    Log.Warning("[TurretModeSwap] Could not get gun for ammo restoration");
                    return;
                }
                
                var compAmmoUserType = Type.GetType("CombatExtended.CompAmmoUser, CombatExtended");
                if (compAmmoUserType == null) return;
                
                var ammoComp = gun.AllComps.Find(c => c.GetType() == compAmmoUserType || compAmmoUserType.IsAssignableFrom(c.GetType()));
                if (ammoComp == null) return;
                
                var currentAmmoProp = compAmmoUserType.GetProperty("CurrentAmmo");
                var selectedAmmoProp = compAmmoUserType.GetProperty("SelectedAmmo");
                var curMagCountProp = compAmmoUserType.GetProperty("CurMagCount");
                var magSizeProp = compAmmoUserType.GetProperty("MagSize");
                
                currentAmmoProp?.SetValue(ammoComp, ammoType);
                selectedAmmoProp?.SetValue(ammoComp, ammoType);
                
                int magSize = magSizeProp != null ? (int)magSizeProp.GetValue(ammoComp) : totalAmmoToRestore;
                curMagCountProp?.SetValue(ammoComp, Mathf.Min(totalAmmoToRestore, magSize));
                
                Log.Message($"[TurretModeSwap] Directly restored {Mathf.Min(totalAmmoToRestore, magSize)}x {ammoType.defName} ammo to {turret.def.defName}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to directly restore ammo: {ex.Message}");
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
        }
    }
}

