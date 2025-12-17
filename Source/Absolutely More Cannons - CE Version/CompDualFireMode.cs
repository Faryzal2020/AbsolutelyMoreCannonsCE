using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using RimWorld;
using UnityEngine;
using CombatExtended;
using HarmonyLib;

namespace AbsolutelyMoreCannons
{
    public class CompDualFireMode : ThingComp
    {
        private CompProperties_DualFireMode Props => (CompProperties_DualFireMode)props;
        private DualFireModeExtension Extension => parent.def.GetModExtension<DualFireModeExtension>();
        
        private FireMode currentMode;
        
        // Cloned verbs - created once, reused
        private Verb directVerb;
        private Verb indirectVerb;
        private Verb originalVerb; // Reference to gun's base verb
        
        public FireMode CurrentMode => currentMode;
        public Verb DirectVerb => directVerb;
        public Verb IndirectVerb => indirectVerb;
        public Verb ActiveVerb => currentMode == FireMode.Direct ? directVerb : indirectVerb;
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            if (!respawningAfterLoad)
            {
                currentMode = Extension?.defaultMode ?? FireMode.Direct;
            }
            
            // Initialize verbs after gun is fully set up
            InitializeVerbs();
        }
        
        public void InitializeVerbs()
        {
            if (Extension == null)
            {
                Log.Error("[DualFireMode] Extension is null - cannot initialize verbs");
                return;
            }
            
            Log.Message($"[DualFireMode] Initializing verbs for {parent.def.defName}, parent type: {parent.GetType().Name}");
            
            // Get the gun via reflection (Gun property exists on both vanilla and CE turrets)
            var gunProp = parent.GetType().GetProperty("Gun");
            if (gunProp == null)
            {
                Log.Error("[DualFireMode] Could not find Gun property on turret");
                return;
            }
            
            var gun = gunProp.GetValue(parent) as ThingWithComps;
            if (gun == null)
            {
                Log.Error("[DualFireMode] Turret has no gun");
                return;
            }
            
            var compEquippable = gun.TryGetComp<CompEquippable>();
            if (compEquippable?.PrimaryVerb == null)
            {
                Log.Error("[DualFireMode] Gun has no primary verb");
                return;
            }
            
            originalVerb = compEquippable.PrimaryVerb;
            
            // DIRECT MODE: Clone the original verb (will be Verb_ShootCE or similar)
            directVerb = CloneVerb(originalVerb);
            
            // INDIRECT MODE: Create a NEW Verb_ShootMortarCE instance
            indirectVerb = CreateMortarVerb(originalVerb);
            
            if (directVerb == null || indirectVerb == null)
            {
                Log.Error("[DualFireMode] Failed to create verbs");
                return;
            }
            
            // Configure each verb
            ConfigureVerb(directVerb, Extension.directFire, "Direct");
            ConfigureVerb(indirectVerb, Extension.indirectFire, "Indirect");
            
            Log.Message($"[DualFireMode] Successfully initialized verbs for {parent.def.defName}");
            Log.Message($"[DualFireMode]   Direct: {directVerb.GetType().Name}, Caster={directVerb.caster?.def.defName ?? "NULL"}, verbProps={directVerb.verbProps != null}, minRange={directVerb.verbProps?.minRange ?? -1}");
            Log.Message($"[DualFireMode]   Indirect: {indirectVerb.GetType().Name}, Caster={indirectVerb.caster?.def.defName ?? "NULL"}, verbProps={indirectVerb.verbProps != null}, minRange={indirectVerb.verbProps?.minRange ?? -1}");
        }
        
        private Verb CloneVerb(Verb source)
        {
            try
            {
                // Use reflection to access protected MemberwiseClone
                var cloneMethod = typeof(object).GetMethod("MemberwiseClone", 
                    BindingFlags.Instance | BindingFlags.NonPublic);
                    
                if (cloneMethod == null)
                {
                    Log.Error("[DualFireMode] Could not find MemberwiseClone method");
                    return null;
                }
                
                var cloned = (Verb)cloneMethod.Invoke(source, null);
                
                // Verify critical fields were cloned
                if (cloned.caster == null)
                {
                    Log.Error("[DualFireMode] Cloned verb has null caster");
                    return null;
                }
                
                Log.Message($"[DualFireMode] Cloned {source.GetType().Name} successfully");
                return cloned;
            }
            catch (Exception ex)
            {
                Log.Error($"[DualFireMode] Exception cloning verb: {ex}");
                return null;
            }
        }
        
        private Verb CreateMortarVerb(Verb sourceVerb)
        {
            try
            {
                // Get Verb_ShootMortarCE type
                var mortarVerbType = AccessTools.TypeByName("CombatExtended.Verb_ShootMortarCE");
                if (mortarVerbType == null)
                {
                    Log.Error("[DualFireMode] Could not find Verb_ShootMortarCE type");
                    return null;
                }
                
                // Create new mortar verb instance
                var mortarVerb = (Verb)Activator.CreateInstance(mortarVerbType);
                
                // Copy critical fields from source verb (caster, equipment, etc.)
                CopyVerbFields(sourceVerb, mortarVerb);
                
                // CRITICAL: Get VerbProperties from a REAL mortar, not from our direct-fire gun
                var mortarVerbProps = GetMortarVerbProperties();
                if (mortarVerbProps != null)
                {
                    mortarVerb.verbProps = CloneVerbProperties(mortarVerbProps);
                    Log.Message("[DualFireMode] Using real mortar VerbProperties");
                }
                else
                {
                    // Fallback: clone from source (will likely fail targeting)
                    mortarVerb.verbProps = CloneVerbProperties(sourceVerb.verbProps);
                    Log.Warning("[DualFireMode] Could not find real mortar, using cloned props (may not work)");
                }
                
                // Verify critical fields
                if (mortarVerb.caster == null)
                {
                    Log.Error("[DualFireMode] Created mortar verb has null caster");
                    return null;
                }
                
                Log.Message($"[DualFireMode] Created {mortarVerbType.Name} successfully, caster: {mortarVerb.caster.def.defName}");
                return mortarVerb;
            }
            catch (Exception ex)
            {
                Log.Error($"[DualFireMode] Exception creating mortar verb: {ex}");
                return null;
            }
        }
        
        private VerbProperties GetMortarVerbProperties()
        {
            // Try to find a real CE mortar turret
            var mortarDefNames = new[] { 
                "Turret_MortarCE",      // CE mortar
                "Turret_Mortar",         // Vanilla mortar
                "Turret_81mmMortar"      // Our own mortar if it exists
            };
            
            foreach (var defName in mortarDefNames)
            {
                var mortarDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (mortarDef?.building?.turretGunDef != null)
                {
                    var gunDef = mortarDef.building.turretGunDef;
                    if (gunDef.Verbs != null && gunDef.Verbs.Count > 0)
                    {
                        var verbProps = gunDef.Verbs[0];
                        Log.Message($"[DualFireMode] Found real mortar VerbProperties from {defName}");
                        return verbProps;
                    }
                }
            }
            
            Log.Warning("[DualFireMode] Could not find any mortar turret for VerbProperties reference");
            return null;
        }
        
        private void CopyVerbFields(Verb source, Verb target)
        {
            // Copy critical fields that verbs need to function
            var verbType = typeof(Verb);
            
            // Get all fields including private ones
            var fields = verbType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            foreach (var field in fields)
            {
                // Don't skip verbProps - we need it!
                // Skip only truly immutable fields
                if (field.IsLiteral || field.IsInitOnly) continue; // Skip constants and readonly
                
                try
                {
                    var value = field.GetValue(source);
                    field.SetValue(target, value);
                }
                catch
                {
                    // Some fields might not be copyable, that's ok
                }
            }
            
            // Don't clone verbProps here - it's set separately in CreateMortarVerb
            
            Log.Message($"[DualFireMode] Copied fields from {source.GetType().Name} to {target.GetType().Name}");
        }
        
        private VerbProperties CloneVerbProperties(VerbProperties source)
        {
            // Use MemberwiseClone to create a shallow copy
            var cloneMethod = typeof(object).GetMethod("MemberwiseClone", 
                BindingFlags.Instance | BindingFlags.NonPublic);
                
            if (cloneMethod == null)
            {
                Log.Error("[DualFireMode] Could not find MemberwiseClone for VerbProperties");
                return source; // Return original as fallback
            }
            
            try
            {
                var cloned = (VerbProperties)cloneMethod.Invoke(source, null);
                return cloned;
            }
            catch (Exception ex)
            {
                Log.Error($"[DualFireMode] Failed to clone VerbProperties: {ex}");
                return source; // Return original as fallback
            }
        }
        
        private void ConfigureVerb(Verb verb, FireModeConfig config, string modeName)
        {
            if (verb == null || config == null)
                return;
                
            try
            {
                // Get VerbProperties (works for both vanilla and CE)
                var verbProps = verb.verbProps;
                if (verbProps == null)
                {
                    Log.Warning($"[DualFireMode] Verb has null verbProps");
                    return;
                }
                
                // Modify targeting parameters (these exist on base VerbProperties)
                verbProps.minRange = config.minRange;
                verbProps.requireLineOfSight = config.requireLineOfSight;
                
                // CE-specific properties - access via reflection to avoid compile errors
                var verbPropsType = verbProps.GetType();
                
                if (config.circularError > 0f)
                {
                    var circularErrorField = verbPropsType.GetField("circularError");
                    circularErrorField?.SetValue(verbProps, config.circularError);
                }
                    
                if (config.indirectFirePenalty > 0f)
                {
                    var indirectFirePenaltyField = verbPropsType.GetField("indirectFirePenalty");
                    indirectFirePenaltyField?.SetValue(verbProps, config.indirectFirePenalty);
                }
                    
                var stopBurstField = verbPropsType.GetField("stopBurstWithoutLos");
                stopBurstField?.SetValue(verbProps, config.stopBurstWithoutLos);
                
                // CRITICAL: Store verb class metadata for Harmony patches
                if (!string.IsNullOrEmpty(config.verbClass))
                {
                    SetVerbTargetClass(verb, config.verbClass);
                }
                
                Log.Message($"[DualFireMode] Configured {modeName} verb: minRange={config.minRange}, LOS={config.requireLineOfSight}, class={config.verbClass}");
            }
            catch (Exception ex)
            {
                Log.Error($"[DualFireMode] Exception configuring verb: {ex}");
            }
        }
        
        // Store metadata about what verb class this should behave as
        private static readonly Dictionary<Verb, string> verbClassOverrides = new Dictionary<Verb, string>();
        
        private void SetVerbTargetClass(Verb verb, string className)
        {
            if (verbClassOverrides.ContainsKey(verb))
                verbClassOverrides[verb] = className;
            else
                verbClassOverrides.Add(verb, className);
                
            Log.Message($"[DualFireMode] Set verb target class: {className}");
        }
        
        public static string GetVerbTargetClass(Verb verb)
        {
            return verbClassOverrides.TryGetValue(verb, out string className) ? className : null;
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Extension == null)
                yield break;
                
            yield return new Command_Toggle
            {
                defaultLabel = currentMode == FireMode.Direct ? "Direct Fire" : "Indirect Fire",
                defaultDesc = currentMode == FireMode.Direct 
                    ? "Switch to indirect fire mode (artillery arc)\nRequires 25+ cell range, fires over obstacles"
                    : "Switch to direct fire mode (flat trajectory)\nRequires line of sight, higher accuracy",
                icon = ContentFinder<Texture2D>.Get(
                    currentMode == FireMode.Direct 
                        ? Extension.indirectModeIcon 
                        : Extension.directModeIcon, 
                    reportFailure: false),
                isActive = () => currentMode == FireMode.Indirect,
                toggleAction = ToggleFireMode,
                hotKey = KeyBindingDefOf.Misc1
            };
        }
        
        public void ToggleFireMode()
        {
            currentMode = (currentMode == FireMode.Direct) 
                ? FireMode.Indirect 
                : FireMode.Direct;
                
            OnFireModeChanged();
        }
        
        private void OnFireModeChanged()
        {
            // 1. Update turret graphics
            UpdateTurretGraphics();
            
            // 2. Notify barrel component
            var barrelComp = parent.TryGetComp<CompTurretBarrel>();
            if (barrelComp != null)
            {
                // Force a graphics refresh
                parent.DirtyMapMesh(parent.Map);
            }
            
            Log.Message($"[DualFireMode] Switched to {currentMode} mode on {parent.LabelCap}");
        }
        
        private void UpdateTurretGraphics()
        {
            var config = currentMode == FireMode.Direct 
                ? Extension.directFire 
                : Extension.indirectFire;
                
            if (string.IsNullOrEmpty(config?.turretTopGraphic))
                return;
                
            // Update the turret's graphic
            parent.DirtyMapMesh(parent.Map);
        }
        
        public TurretBarrelExtension GetCurrentBarrelExtension()
        {
            if (Extension == null)
                return null;
                
            var config = currentMode == FireMode.Direct 
                ? Extension.directFire 
                : Extension.indirectFire;
                
            return config?.barrelExtension;
        }
        
        public string GetCurrentTurretTopGraphic()
        {
            if (Extension == null)
                return null;
                
            var config = currentMode == FireMode.Direct 
                ? Extension.directFire 
                : Extension.indirectFire;
                
            return config?.turretTopGraphic;
        }
        
        public string GetCurrentAmmoSet()
        {
            if (Extension == null)
                return null;
                
            var config = currentMode == FireMode.Direct 
                ? Extension.directFire 
                : Extension.indirectFire;
                
            return config?.ammoSet;
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentMode, "fireMode", FireMode.Direct);
            
            // Verbs don't serialize - they'll be recreated on load via PostSpawnSetup
        }
    }
}
