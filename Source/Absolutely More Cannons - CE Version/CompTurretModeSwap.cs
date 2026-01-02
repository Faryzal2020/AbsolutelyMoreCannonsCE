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
            // Only show gizmo if we have an alternate def configured
            if (string.IsNullOrEmpty(Props.alternateDef))
                yield break;
                
            yield return new Command_Action
            {
                defaultLabel = Props.gizmoLabel ?? "Switch Fire Mode",
                defaultDesc = Props.gizmoDesc ?? "Rebuild this turret in alternate fire mode",
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIcon ?? "UI/Commands/Attack", reportFailure: false),
                action = () => TrySwapMode(),
                hotKey = KeyBindingDefOf.Misc1
            };
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
            float currentHealth = parent.HitPoints;
            
            // Get forced target
            LocalTargetInfo forcedTarget = GetForcedTarget(parent);
            Log.Message($"[TurretModeSwap] Saved target: {(forcedTarget.IsValid ? forcedTarget.ToString() : "None")}");
            
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
            
            // Get ammo info (will spawn one stack at a time during reload)
            ThingDef spawnedAmmoType = null;
            int totalAmmoToRestore = 0;
            var gun = GetTurretGun(parent);
            if (gun != null)
            {
                int ammo = GetCurrentAmmo(gun, out ThingDef ammoType);
                if (ammo > 0 && ammoType != null)
                {
                    Log.Message($"[TurretModeSwap] Will restore {ammo}x {ammoType.defName} (stackLimit: {ammoType.stackLimit})");
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
                // (DestroyMode.Vanish might not call PostDeSpawn)
                var smokeManager = map.GetComponent<MapComponent_TurretSmokeManager>();
                if (smokeManager != null)
                {
                    smokeManager.UnregisterSmoker(oldSmokeComp);
                    Log.Message($"[TurretModeSwap] Unregistered old turret from smoke manager");
                }
            }
            
            // Destroy current turret (no resources)
            Log.Message($"[TurretModeSwap] Destroying old turret...");
            parent.Destroy(DestroyMode.Vanish);
            
            // Spawn new turret
            Log.Message($"[TurretModeSwap] Spawning new turret: {alternateDef.defName}");
            var newTurret = ThingMaker.MakeThing(alternateDef, parent.Stuff);
            newTurret.SetFactionDirect(faction);
            newTurret.HitPoints = (int)currentHealth;
            
            GenSpawn.Spawn(newTurret, position, map, rotation);
            Log.Message($"[TurretModeSwap] New turret spawned");
            
            // Force power to activate immediately (bypasses 2-3 second delay)
            if (wasPowered)
            {
                var newPowerComp = newTurret.TryGetComp<CompPowerTrader>();
                if (newPowerComp != null)
                {
                    try
                    {
                        // Call SetUpPowerVars to force immediate power recognition
                        var setupMethod = typeof(CompPowerTrader).GetMethod("SetUpPowerVars", 
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        if (setupMethod != null)
                        {
                            setupMethod.Invoke(newPowerComp, null);
                            Log.Message($"[TurretModeSwap] Forced power comp update - turret should be powered immediately");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[TurretModeSwap] Failed to force power update: {ex.Message}");
                    }
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
                    Log.Message($"[TurretModeSwap] Restored heat value: {currentHeat:F1} - smoke will continue with new turret's settings");
                }
                else
                {
                    Log.Warning($"[TurretModeSwap] New turret does not have CompTurretSmoker - heat value lost");
                }
            }
            
            // Restore forced target (can do immediately)
            if (forcedTarget.IsValid)
            {
                SetForcedTarget(newTurret, forcedTarget);
                Log.Message($"[TurretModeSwap] Restored target: {forcedTarget}");
                
                // Add delayed rotation component to the new turret
                Log.Message($"[TurretModeSwap] Creating CompDelayedRotation for {newTurret.def.defName}");
                var delayedRotationComp = new CompDelayedRotation();
                var turretWithComps = newTurret as ThingWithComps;
                
                if (turretWithComps == null)
                {
                    Log.Error($"[TurretModeSwap] Failed to cast newTurret to ThingWithComps! Type: {newTurret.GetType().Name}");
                }
                else
                {
                    delayedRotationComp.parent = turretWithComps;
                    delayedRotationComp.Initialize(null);  // CompProperties not needed for this component
                    Log.Message($"[TurretModeSwap] CompDelayedRotation created and initialized");
                    
                    // Add to the turret's components list
                    // Search up the inheritance chain for the 'comps' field
                    FieldInfo compsField = null;
                    Type searchType = newTurret.GetType();
                    while (searchType != null && compsField == null)
                    {
                        compsField = searchType.GetField("comps", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (compsField == null)
                        {
                            searchType = searchType.BaseType;
                        }
                    }
                    
                    if (compsField != null)
                    {
                        Log.Message($"[TurretModeSwap] Found 'comps' field in {searchType.Name}");
                        var comps = compsField.GetValue(newTurret) as List<ThingComp>;
                        if (comps != null)
                        {
                            int compCountBefore = comps.Count;
                            comps.Add(delayedRotationComp);
                            Log.Message($"[TurretModeSwap] Added to comps list (count: {compCountBefore} → {comps.Count})");
                            
                            delayedRotationComp.ScheduleRotation(forcedTarget, 2);
                            Log.Message($"[TurretModeSwap] Rotation scheduled to aim at {forcedTarget} in 2 ticks");
                        }
                        else
                        {
                            Log.Warning($"[TurretModeSwap] Failed to get comps list - field value is null");
                        }
                    }
                    else
                    {
                        Log.Error($"[TurretModeSwap] Failed to find 'comps' field in inheritance chain of {newTurret.GetType().Name}");
                    }
                }
            }
            else
            {
                Log.Message($"[TurretModeSwap] No valid target to restore rotation for");
            }
            
            // Reassign manning pawn
            if (manningPawn != null && !manningPawn.Dead && manningPawn.Spawned)
            {
                ReassignManningPawn(newTurret, manningPawn);
                Log.Message($"[TurretModeSwap] Reassigned manning pawn: {manningPawn.LabelShort}");
            }
            
            // Trigger immediate reload - will spawn ammo one stack at a time
            if (spawnedAmmoType != null && totalAmmoToRestore > 0)
            {
                TriggerReload(newTurret, spawnedAmmoType, totalAmmoToRestore, position, map);
            }
            
            // Flash effect and message
            FleckMaker.ThrowSmoke(position.ToVector3(), map, 1f);
            Messages.Message(
                $"{newTurret.Label} fire mode switched",
                newTurret,
                MessageTypeDefOf.NeutralEvent,
                historical: false
            );
            
            Log.Message($"[TurretModeSwap] ===== Mode swap complete - ammo will auto-reload from spawned items =====");
        }
        
        private ThingWithComps GetTurretGun(Thing turret)
        {
            try
            {
                // Try to get Gun property via reflection (CE turrets have this)
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
                    // Force the pawn to man the new turret
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
                // burstCooldownTicksLeft is a public field in Building_TurretGunCE
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
                
                var ammoComp = gun.AllComps.Find(c => c.GetType() == compAmmoUserType);
                if (ammoComp == null) return 0;
                
                // Get current ammo count
                var curMagCountProp = compAmmoUserType.GetProperty("CurMagCount");
                int count = 0;
                if (curMagCountProp != null)
                {
                    count = (int)curMagCountProp.GetValue(ammoComp);
                }
                
                // Get current ammo type
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
        
        private void TriggerReload(Thing turret, ThingDef ammoType, int totalAmmoToLoad, IntVec3 position, Map map)
        {
            try
            {
                // Get the turret's gun
                var gun = GetTurretGun(turret);
                if (gun == null)
                {
                    Log.Warning("[TurretModeSwap] Could not get gun for reload trigger");
                    return;
                }
                
                // Get CompAmmoUser
                var compAmmoUserType = Type.GetType("CombatExtended.CompAmmoUser, CombatExtended");
                if (compAmmoUserType == null) return;
                
                var ammoComp = gun.AllComps.Find(c => c.GetType() == compAmmoUserType);
                if (ammoComp == null) return;
                
                // Get magazine size
                var propsProp = compAmmoUserType.GetProperty("Props");
                int magazineSize = 999999;
                if (propsProp != null)
                {
                    var props = propsProp.GetValue(ammoComp);
                    var magazineSizeProp = props.GetType().GetProperty("magazineSize");
                    if (magazineSizeProp != null)
                    {
                        magazineSize = (int)magazineSizeProp.GetValue(props);
                    }
                }
                
                // Get stack limit
                int stackLimit = ammoType.stackLimit;
                
                // Spawn and reload one stack at a time
                int loadedSoFar = 0;
                int remaining = totalAmmoToLoad;
                var loadAmmoMethod = compAmmoUserType.GetMethod("LoadAmmo");
                
                while (remaining > 0 && loadedSoFar < magazineSize && loadAmmoMethod != null)
                {
                    // Spawn next stack
                    int stackSize = Math.Min(remaining, stackLimit);
                    Thing ammoThing = ThingMaker.MakeThing(ammoType);
                    ammoThing.stackCount = stackSize;
                    GenSpawn.Spawn(ammoThing, position, map);
                    Log.Message($"[TurretModeSwap] Spawned stack of {stackSize}x {ammoType.defName}");
                    
                    // Immediately load from this stack
                    loadAmmoMethod.Invoke(ammoComp, new object[] { ammoThing, false });
                    
                    int consumed = stackSize - (ammoThing.Destroyed ? 0 : ammoThing.stackCount);
                    loadedSoFar += consumed;
                    remaining -= consumed;
                    
                    Log.Message($"[TurretModeSwap] Loaded {consumed} rounds! Total: {loadedSoFar}/{totalAmmoToLoad}");
                    
                    // If ammo wasn't fully consumed, we're done (magazine full)
                    if (!ammoThing.Destroyed)
                    {
                        Log.Message($"[TurretModeSwap] Magazine full, {ammoThing.stackCount} rounds remaining");
                        break;
                    }
                }
                
                Log.Message($"[TurretModeSwap] Reload complete! Final count: {loadedSoFar}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurretModeSwap] Failed to trigger reload: {ex.Message}");
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            // No state to save - all info comes from props
        }
    }
}
