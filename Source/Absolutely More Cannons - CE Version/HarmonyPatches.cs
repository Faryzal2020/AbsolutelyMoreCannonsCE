using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld; // Add this using directive
using UnityEngine; // Add this using directive

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patches to integrate turret barrel animations with CombatExtended.
    /// </summary>
    [StaticConstructorOnStartup]
    public static partial class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("AbsolutelyMoreCannons.TurretBarrelAnimation");

            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Message("Turret Barrel Animation: Executed harmony.PatchAll() successfully.");
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Error($"Turret Barrel Animation: Error executing harmony.PatchAll(): {ex}");
                }
            }

            // Patch GenDraw.DrawRadiusRing to handle large turret ranges (>70 tiles)
            TryPatchLargeRadiusRing(harmony);

            // Patch PlaceWorker_ShowTurretRadius.AllowsPlacing to handle null map during designator preview
            TryPatchPlaceWorkerShowTurretRadius(harmony);
            
            // Patch Verb.TryCastNextBurstShot for RPM override
            harmony.Patch(
                original: AccessTools.Method(typeof(Verb), "TryCastNextBurstShot"),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_TryCastNextBurstShot))
            );

            // Patch CE projectile launch for parameter logging
            TryPatchCEProjectileLaunch(harmony);

            // Patch Building_TurretGun.CanSetTarget for FCS operability check
            var canSetTargetVanilla = AccessTools.PropertyGetter(typeof(Building_TurretGun), "CanSetTarget");
            if (canSetTargetVanilla != null)
            {
                harmony.Patch(
                    original: canSetTargetVanilla,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretGun_CanSetTarget))
                );
            }

            // Try to patch CE turret methods at runtime
            TryPatchCETurrets(harmony);

            // Patch enclosed manned turrets at runtime
            HarmonyPatches_EnclosedTurret.TryPatchEnclosedTurrets(harmony);

            // Patch turret view transfer at runtime
            HarmonyPatches_TurretViewTransfer.TryPatchTurretViewTransfer(harmony);

            // Patch third-party Muzzle Flash mod if present
            TryPatchMuzzleFlashMod(harmony);
        }

        /// <summary>
        /// Patches GenDraw.DrawRadiusRing to handle large turret ranges (>70 tiles).
        /// Vanilla RimWorld has a precalculated list that doesn't support very large radii.
        /// </summary>
        private static void TryPatchLargeRadiusRing(Harmony harmony)
        {
            try
            {
                // Find the DrawRadiusRing method - there are multiple overloads
                // We need the one with (IntVec3 center, float radius) signature
                var drawRadiusRingMethod = AccessTools.Method(typeof(GenDraw), "DrawRadiusRing", 
                    new Type[] { typeof(IntVec3), typeof(float) });
                
                if (drawRadiusRingMethod != null)
                {
                    harmony.Patch(
                        original: drawRadiusRingMethod,
                        prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Prefix_DrawRadiusRing))
                    );
                    Log.Message("Turret Barrel Animation: Patched GenDraw.DrawRadiusRing for large radius support.");
                }
                else
                {
                    Log.Warning("Turret Barrel Animation: Could not find GenDraw.DrawRadiusRing(IntVec3, float) method.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Turret Barrel Animation: Error patching DrawRadiusRing: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix patch for GenDraw.DrawRadiusRing to handle large radii (>70 tiles).
        /// Draws a simplified ring for large radii to avoid the precalculated list error.
        /// </summary>
        public static bool Prefix_DrawRadiusRing(IntVec3 center, float radius)
        {
            // If radius is too large, use simplified drawing
            if (radius > 70f)
            {
                DrawLargeRadiusRing(center, radius);
                return false; // Skip original method
            }
            return true; // Run original method for normal radii
        }

        /// <summary>
        /// Draws a simplified radius ring for large turret ranges.
        /// Uses density-based markers that scale with radius to avoid the precalculated list limitation.
        /// </summary>
        private static void DrawLargeRadiusRing(IntVec3 center, float radius)
        {
            try
            {
                Vector3 centerPos = center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
                Color color = Color.white;
                
                DrawDensityBasedMarkers(centerPos, radius, color);
            }
            catch (Exception ex)
            {
                // If drawing fails, just skip it - better than crashing
                Log.Warning($"Turret Barrel Animation: Error drawing large radius ring: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws markers around a circle based on density percentage.
        /// Marker count scales with radius to maintain consistent visual density.
        /// </summary>
        private static void DrawDensityBasedMarkers(Vector3 center, float radius, Color color)
        {
            // Density as percentage: 0.5 = 50% filled (max requested), 1.0 = 100% filled (no gaps)
            const float MARKER_DENSITY = 0.5f; // 50% density - maximum requested
            const int MIN_MARKERS = 16; // Minimum markers for small large radii
            const int MAX_MARKERS = 200; // Maximum markers to avoid performance issues
            
            // Calculate circumference in tiles
            float circumference = 2f * Mathf.PI * radius;
            
            // Marker size (in tiles) - approximate size of the marker mesh
            float markerSizeTiles = 0.8f;
            
            // Calculate how many markers would fit if placed continuously (100% density)
            float maxPossibleMarkers = circumference / markerSizeTiles;
            
            // Apply density percentage
            int markerCount = Mathf.RoundToInt(maxPossibleMarkers * MARKER_DENSITY);
            markerCount = Mathf.Clamp(markerCount, MIN_MARKERS, MAX_MARKERS);
            
            // Angle step between each marker
            float angleStep = 360f / markerCount;
            
            // Use the same material as vanilla for consistency
            Material markerMaterial = GenDraw.InteractionCellMaterial;
            
            // Draw markers around the circle
            for (int i = 0; i < markerCount; i++)
            {
                float angle = i * angleStep;
                float radians = angle * Mathf.Deg2Rad;
                
                // RimWorld coordinate system: X = East/West, Z = North/South
                // Rotation: 0° = North, 90° = East
                Vector3 offset = new Vector3(
                    Mathf.Sin(radians) * radius,  // X (East/West)
                    0f,
                    Mathf.Cos(radians) * radius    // Z (North/South)
                );
                
                Vector3 markerPos = center + offset;
                
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    markerPos,
                    Quaternion.identity,
                    markerMaterial,
                    0
                );
            }
        }

        private static void TryPatchPlaceWorkerShowTurretRadius(Harmony harmony)
        {
            try
            {
                var allowsPlacingMethod = AccessTools.Method(typeof(PlaceWorker_ShowTurretRadius), "AllowsPlacing");
                if (allowsPlacingMethod != null)
                {
                    harmony.Patch(
                        original: allowsPlacingMethod,
                        prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Prefix_PlaceWorker_ShowTurretRadius_AllowsPlacing))
                    );
                    if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Message("Turret Barrel Animation: Patched PlaceWorker_ShowTurretRadius.AllowsPlacing for null-map safety.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"Turret Barrel Animation: Error patching PlaceWorker_ShowTurretRadius.AllowsPlacing: {ex.Message}");
                }
            }
        }

        public static bool Prefix_PlaceWorker_ShowTurretRadius_AllowsPlacing(BuildableDef checkingDef, Map map, ref AcceptanceReport __result)
        {
            if (map == null)
            {
                __result = true;
                return false;
            }

            if (checkingDef is ThingDef thingDef && thingDef.building?.turretGunDef != null)
            {
                var verbs = thingDef.building.turretGunDef.Verbs;
                if (verbs != null && verbs.Count > 0)
                {
                    var verb = verbs[0];
                    if (verb != null && (!verb.requireLineOfSight || (verb.verbClass != null && verb.verbClass.Name.Contains("Mortar")) || verb.range > 100f))
                    {
                        __result = true;
                        return false;
                    }
                }
            }
            return true;
        }

        private static void TryPatchCETurrets(Harmony harmony)
        {
            // Find CE turret class at runtime
            var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");

            if (ceTurretType != null)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Message("Turret Barrel Animation: Found CombatExtended turrets, applying patches.");
                }

                // Patch CE verb firing to trigger barrel animations
                // We patch Verb_LaunchProjectileCE.TryCastShot because this is called when CE turrets actually fire
                // This is more reliable than BeginBurst which can be called even when the verb can't fire
                TryPatchCEVerbFiring(harmony);

                // Patch Verb_ShootCE.ShotsPerBurstFor for burst count override
                var shootCEType = AccessTools.TypeByName("CombatExtended.Verb_ShootCE");
                if (shootCEType != null)
                {
                    var shotsPerBurstForMethod = AccessTools.Method(shootCEType, "ShotsPerBurstFor");
                    if (shotsPerBurstForMethod != null)
                    {
                        harmony.Patch(
                            original: shotsPerBurstForMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_ShotsPerBurstFor))
                        );
                        if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                        {
                            Log.Message("Turret Barrel Animation: Patched Verb_ShootCE.ShotsPerBurstFor");
                        }
                    }
                }

                // Patch burst completion to trigger recoil
                var burstCompleteMethod = AccessTools.Method(ceTurretType, "BurstComplete");
                if (burstCompleteMethod != null)
                {
                    harmony.Patch(
                        original: burstCompleteMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_BurstComplete))
                    );
                }

                // Patch the turret building's DrawAt to draw barrel after turret top if needed
                var drawAtMethod = AccessTools.Method(ceTurretType, "DrawAt");
                if (drawAtMethod != null)
                {
                    harmony.Patch(
                        original: drawAtMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretDrawAt))
                    );
                }

                // Patch turret top drawing to ensure barrel draws after turret top
                TryPatchCETurretTop(harmony);

                // Patch CE & Vanilla turret Active, CanSetTarget, and IsOperational for FCS operability check
                PatchTurretFCSOperability(harmony, ceTurretType);
            }
            else
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Message("Turret Barrel Animation: CombatExtended not found. Barrel animations will only work with basic recoil.");
                }
                PatchTurretFCSOperability(harmony, null);
            }
        }

        private static readonly HashSet<MethodBase> patchedMethods = new HashSet<MethodBase>();

        private static MethodInfo GetImplementedMethod(Type type, string methodName)
        {
            Type current = type;
            while (current != null && current != typeof(object))
            {
                var method = AccessTools.DeclaredMethod(current, methodName);
                if (method != null && !method.IsAbstract)
                {
                    return method;
                }
                current = current.BaseType;
            }
            return null;
        }

        private static MethodInfo GetImplementedPropertyGetter(Type type, string propertyName)
        {
            Type current = type;
            while (current != null && current != typeof(object))
            {
                var prop = AccessTools.DeclaredPropertyGetter(current, propertyName);
                if (prop != null && !prop.IsAbstract)
                {
                    return prop;
                }
                current = current.BaseType;
            }
            return null;
        }

        private static void SafePatchPostfix(Harmony harmony, MethodBase original, HarmonyMethod postfix)
        {
            if (original == null || patchedMethods.Contains(original)) return;
            try
            {
                harmony.Patch(original, postfix: postfix);
                patchedMethods.Add(original);
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC] Harmony patch skipped for {original.DeclaringType?.Name}.{original.Name}: {ex.Message}");
                }
            }
        }

        public static FieldInfo GetCurrentTargetField(Type type)
        {
            if (type == null) return null;
            return AccessTools.Field(type, "currentTargetInt") 
                ?? AccessTools.Field(type, "currentTarget") 
                ?? AccessTools.Field(type, "targetInt");
        }

        private static void PatchTurretFCSOperability(Harmony harmony, Type ceTurretType)
        {
            var startupSettings = TurretBarrelAnimationMod.settings;
            bool logStartup = startupSettings != null && startupSettings.logStartup;

            if (logStartup)
            {
                Log.Message($"[AMC Startup] PatchTurretFCSOperability executing. ceTurretType: {ceTurretType?.FullName ?? "null"}");
            }
            List<Type> turretTypes = new List<Type> { typeof(Building_TurretGun) };
            if (ceTurretType != null && ceTurretType != typeof(Building_TurretGun))
            {
                turretTypes.Add(ceTurretType);
            }

            var ceMultiVerbsType = AccessTools.TypeByName("CombatExtended.Building_Turret_MultiVerbs");
            if (ceMultiVerbsType != null && !turretTypes.Contains(ceMultiVerbsType))
            {
                turretTypes.Add(ceMultiVerbsType);
            }

            var ceCiwsType = AccessTools.TypeByName("CombatExtended.Building_CIWS_CE");
            if (ceCiwsType != null && !turretTypes.Contains(ceCiwsType))
            {
                turretTypes.Add(ceCiwsType);
            }

            foreach (var t in turretTypes)
            {
                var activeProp = GetImplementedPropertyGetter(t, "Active");
                SafePatchPostfix(harmony, activeProp, new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretGun_Active)));

                var isOperationalProp = GetImplementedPropertyGetter(t, "IsOperational");
                SafePatchPostfix(harmony, isOperationalProp, new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretGun_IsOperational)));

                var canSetTargetProp = GetImplementedPropertyGetter(t, "CanSetTarget");
                SafePatchPostfix(harmony, canSetTargetProp, new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretGun_CanSetTarget)));

                var tryFindNewTargetMethod = GetImplementedMethod(t, "TryFindNewTarget");
                if (tryFindNewTargetMethod != null)
                {
                    if (logStartup)
                    {
                        Log.Message($"[AMC Startup] Found TryFindNewTarget on {t.Name}: ReturnType={tryFindNewTargetMethod.ReturnType.Name}, Params=[{string.Join(", ", tryFindNewTargetMethod.GetParameters().Select(p => p.ParameterType.Name))}]");
                    }
                    SafePatchPostfix(harmony, tryFindNewTargetMethod, new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretGun_TryFindNewTarget)));
                }
                else if (logStartup)
                {
                    Log.Message($"[AMC Startup] TryFindNewTarget NOT found on {t.Name}");
                }

                var tickMethod = GetImplementedMethod(t, "Tick");
                SafePatchPostfix(harmony, tickMethod, new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretGun_Tick)));
            }

            // Also attempt to patch CanHitTarget for diagnostic telemetry.
            // Only patch CE's own verb type - never fall back to patching vanilla Verb.CanHitTarget,
            // which would affect every weapon (pawns included) in the entire game.
            var shootCEType = AccessTools.TypeByName("CombatExtended.Verb_ShootCE");
            var canHitMethod = shootCEType != null ? GetImplementedMethod(shootCEType, "CanHitTarget") : null;
            if (canHitMethod != null)
            {
                if (logStartup)
                {
                    Log.Message($"[AMC Startup] Found CanHitTarget on {canHitMethod.DeclaringType.Name}. Patching...");
                }
                SafePatchPostfix(harmony, canHitMethod, new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_ShootCE_CanHitTarget)));
            }
            else if (logStartup)
            {
                Log.Message("[AMC Startup] CanHitTarget NOT found!");
            }
        }

        private static readonly Dictionary<int, int> turretTickCounter = new Dictionary<int, int>();

        public static void Postfix_TurretGun_Tick(Thing __instance)
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || !settings.logTurretTarget) return;

            if (__instance == null || !__instance.Spawned) return;

            int thingID = __instance.thingIDNumber;
            if (!turretTickCounter.TryGetValue(thingID, out int count))
            {
                count = 0;
            }
            count++;
            turretTickCounter[thingID] = count;

            if (count % 60 != 0) return; // Every 60 ticks (1 second)

            if (__instance is Building_TurretGun turretGun)
            {
                var fcsComp = __instance.TryGetComp<CompTurretFCS>();
                var barrelComp = __instance.TryGetComp<CompTurretBarrel>();

                FieldInfo targetField = GetCurrentTargetField(typeof(Building_TurretGun));
                LocalTargetInfo curTarget = (LocalTargetInfo)(targetField?.GetValue(turretGun) ?? LocalTargetInfo.Invalid);
                int warmup = (int)(AccessTools.Field(typeof(Building_TurretGun), "burstWarmupTicksLeft")?.GetValue(turretGun) ?? 0);
                int cooldown = (int)(AccessTools.Field(typeof(Building_TurretGun), "burstCooldownTicksLeft")?.GetValue(turretGun) ?? 0);
                int resetTarget = (int)(AccessTools.Field(typeof(Building_TurretGun), "resetTargetTicks")?.GetValue(turretGun) ?? 0);
                bool holdFire = (bool)(AccessTools.Field(typeof(Building_TurretGun), "holdFire")?.GetValue(turretGun) ?? false);

                var opProp = GetImplementedPropertyGetter(__instance.GetType(), "IsOperational");
                bool isOperational = (bool)(opProp != null ? opProp.Invoke(__instance, null) : false);

                var canSetProp = GetImplementedPropertyGetter(__instance.GetType(), "CanSetTarget");
                bool canSetTarget = (bool)(canSetProp != null ? canSetProp.Invoke(__instance, null) : false);

                string ammoInfo = "N/A";
                if (turretGun.gun is ThingWithComps gunWithComps && gunWithComps.AllComps != null)
                {
                    var ammoComp = gunWithComps.AllComps.FirstOrDefault(c => c.GetType().FullName == "CombatExtended.CompAmmoUser");
                    if (ammoComp != null)
                    {
                        int curMag = (int)(AccessTools.PropertyGetter(ammoComp.GetType(), "CurMagCount")?.Invoke(ammoComp, null) ?? 0);
                        bool reloading = (bool)(AccessTools.PropertyGetter(ammoComp.GetType(), "IsReloading")?.Invoke(ammoComp, null) ?? false);
                        bool hasAmmo = (bool)(AccessTools.PropertyGetter(ammoComp.GetType(), "HasAmmo")?.Invoke(ammoComp, null) ?? false);
                        ammoInfo = $"Mag: {curMag}, Reloading: {reloading}, HasAmmo: {hasAmmo}";
                    }
                }

                AMCLogger.LogTurretTarget(
                    $"[Turret Tick State] {__instance.LabelCap} (ID:{thingID}) @ {__instance.Position} | " +
                    $"Active: {turretGun.Active} | Operational: {isOperational} | CanSetTarget: {canSetTarget} | " +
                    $"Target: {(curTarget.IsValid ? curTarget.ToString() : "Invalid")} | HoldFire: {holdFire} | " +
                    $"WarmupTicks: {warmup} | CooldownTicks: {cooldown} | ResetTicks: {resetTarget} | " +
                    $"HasFCS: {(fcsComp != null ? fcsComp.HasFCS.ToString() : "N/A")} | HasBarrelComp: {barrelComp != null} | {ammoInfo}"
                );
            }
        }

        public static void Postfix_TurretGun_TryFindNewTarget(Thing __instance, ref LocalTargetInfo __result)
        {
            if (__instance is Building_TurretGun turretGun)
            {
                var comp = __instance.TryGetComp<CompTurretFCS>();

                // CIWS ground target fallback if current target is invalid
                if (!__result.IsValid && __instance.GetType().Name.Contains("CIWS"))
                {
                    TryCIWSGroundTargetFallback(turretGun, ref __result);
                }

                var diagSettings = TurretBarrelAnimationMod.settings;
                if (diagSettings != null && diagSettings.logTurretTarget)
                {
                    AMCLogger.LogTurretTarget($"[FCS Target Scan] {__instance.LabelCap} @ {__instance.Position} | Target: {(__result.IsValid ? __result.ToString() : "None")} | HasFCS: {(comp != null ? comp.HasFCS.ToString() : "N/A (Manned)")}");

                    if (!__result.IsValid && turretGun.Spawned && turretGun.Map != null)
                    {
                        DiagnoseTargetingFailure(turretGun);
                    }
                }
            }
        }

        public static void TryCIWSGroundTargetFallback(Building_TurretGun __instance, ref LocalTargetInfo result)
        {
            // If CIWS did not find an air target (result is invalid), check for ground targets using Verb 0 (Verb_ShootCE)
            if (__instance != null && !result.IsValid && __instance.Spawned && __instance.Map != null)
            {
                var mannable = __instance.TryGetComp<CompMannable>();
                if (mannable != null && !mannable.MannedNow) return;

                // Try to find a ground target using the primary shooting verb (verb index 0)
                var gun = __instance.gun;
                if (gun != null)
                {
                    var eq = gun.TryGetComp<CompEquippable>();
                    if (eq != null && eq.AllVerbs != null && eq.AllVerbs.Count > 0)
                    {
                        Verb groundVerb = eq.AllVerbs[0];
                        if (groundVerb != null)
                        {
                            // Search for hostiles in range of groundVerb
                            var mapPawns = __instance.Map.mapPawns.AllPawnsSpawned;
                            if (mapPawns != null)
                            {
                                float bestDist = float.MaxValue;
                                Pawn bestTarget = null;
                                float minR = groundVerb.verbProps.minRange;
                                float maxR = groundVerb.verbProps.range;

                                foreach (var p in mapPawns)
                                {
                                    if (p == null || !p.Spawned || p.Dead || p.Downed) continue;
                                    if (__instance.Faction != null && p.HostileTo(__instance.Faction))
                                    {
                                        float dist = (p.Position - __instance.Position).LengthHorizontal;
                                        if (dist >= minR && dist <= maxR && dist < bestDist)
                                        {
                                            if (groundVerb.CanHitTarget(p))
                                            {
                                                bestDist = dist;
                                                bestTarget = p;
                                            }
                                        }
                                    }
                                }

                                if (bestTarget != null)
                                {
                                    result = new LocalTargetInfo(bestTarget);
                                    var currentTargetField = GetCurrentTargetField(typeof(Building_TurretGun));
                                    if (currentTargetField != null)
                                    {
                                        currentTargetField.SetValue(__instance, result);
                                    }
                                    AMCLogger.LogTurretTarget($"[CIWS Fallback] {__instance.LabelCap} automatically acquired ground target: {bestTarget.LabelCap} @ {bestTarget.Position}");
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void Postfix_Verb_ShootCE_CanHitTarget(Verb __instance, LocalTargetInfo targ, ref bool __result)
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null || !settings.logTurretTarget) return;

            Thing caster = __instance?.caster;
            if (caster is Building_TurretGun turret && turret.Spawned)
            {
                var fcsComp = turret.TryGetComp<CompTurretFCS>();
                if (fcsComp != null && targ.IsValid)
                {
                    AMCLogger.LogTurretTarget($"[CE CanHitTarget Check] {turret.LabelCap} vs {(targ.HasThing ? targ.Thing.LabelCap : targ.ToString())} @ {targ.Cell} | CanHit: {__result} | HasFCS: {fcsComp.HasFCS}");
                }
            }
        }

        public static void DiagnoseTargetingFailure(Building_TurretGun turret)
        {
            if (turret == null || turret.Map == null || !turret.Spawned) return;

            var gun = turret.gun;
            if (gun == null)
            {
                AMCLogger.LogTurretTarget($"[TARGET DIAGNOSTIC] {turret.LabelCap} @ {turret.Position} | Turret gun is NULL!");
                return;
            }

            // Check ammo
            bool hasAmmo = true;
            string ammoDetails = "No CompAmmoUser";
            if (gun is ThingWithComps gunWithComps && gunWithComps.AllComps != null)
            {
                foreach (var c in gunWithComps.AllComps)
                {
                    if (c != null && c.GetType().Name == "CompAmmoUser")
                    {
                        var hasAmmoProp = c.GetType().GetProperty("HasAmmo", BindingFlags.Public | BindingFlags.Instance);
                        if (hasAmmoProp != null)
                        {
                            hasAmmo = (bool)hasAmmoProp.GetValue(c);
                        }
                        var curMagProp = c.GetType().GetProperty("CurMagCount", BindingFlags.Public | BindingFlags.Instance);
                        int cur = curMagProp != null ? (int)curMagProp.GetValue(c) : -1;
                        ammoDetails = $"CurMag: {cur}, HasAmmo: {hasAmmo}";
                        break;
                    }
                }
            }

            // Check AttackVerb
            Verb verb = turret.AttackVerb;
            string verbName = verb != null ? verb.GetType().Name : "NULL";
            float minRange = verb != null ? verb.verbProps.minRange : 0f;
            float maxRange = verb != null ? verb.verbProps.range : 0f;

            // Check Mannable
            var mannable = turret.TryGetComp<CompMannable>();
            bool isManned = mannable == null || mannable.MannedNow;

            AMCLogger.LogTurretTarget($"[TARGET DIAGNOSTIC] {turret.LabelCap} ({turret.GetType().Name}) @ {turret.Position} | Manned: {isManned} | Ammo: [{ammoDetails}] | Verb: {verbName} (MinRange: {minRange}, MaxRange: {maxRange})");

            if (!hasAmmo)
            {
                AMCLogger.LogTurretTarget($"[TARGET DIAGNOSTIC] {turret.LabelCap} -> REJECTED: Out of ammo!");
                return;
            }

            if (!isManned)
            {
                AMCLogger.LogTurretTarget($"[TARGET DIAGNOSTIC] {turret.LabelCap} -> REJECTED: Turret is un-manned!");
                return;
            }

            if (turret.Faction == null)
            {
                AMCLogger.LogTurretTarget($"[TARGET DIAGNOSTIC] {turret.LabelCap} -> Faction is NULL!");
                return;
            }

            var mapPawns = turret.Map.mapPawns.AllPawnsSpawned;
            int count = 0;
            if (mapPawns != null)
            {
                foreach (var p in mapPawns)
                {
                    if (p == null || !p.Spawned || p.Dead || p.Downed) continue;
                    if (p.HostileTo(turret.Faction))
                    {
                        count++;
                        if (count <= 5)
                        {
                            float dist = (p.Position - turret.Position).LengthHorizontal;
                            bool inRange = dist >= minRange && dist <= maxRange;
                            bool canHit = verb != null && verb.CanHitTarget(p);
                            AMCLogger.LogTurretTarget($"[TARGET DIAGNOSTIC] Hostile Pawn #{count}: {p.LabelCap} @ {p.Position} | Dist: {dist:F1} | InRange: {inRange} | CanHitTarget: {canHit}");
                        }
                    }
                }
            }

            if (count == 0)
            {
                AMCLogger.LogTurretTarget($"[TARGET DIAGNOSTIC] {turret.LabelCap} -> No hostile spawned pawns found on map for Faction {turret.Faction.def.defName}!");
            }
        }

        public static string GetTurretInactiveReason(Thing turret)
        {
            List<string> reasons = new List<string>();

            var power = turret.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
                reasons.Add("No Power (PowerOn=False)");

            var mannable = turret.TryGetComp<CompMannable>();
            if (mannable != null && !mannable.MannedNow)
                reasons.Add("Unmanned (MannedNow=False)");

            var forbiddable = turret.TryGetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
                reasons.Add("Forbidden");

            var flickable = turret.TryGetComp<CompFlickable>();
            if (flickable != null && !flickable.SwitchIsOn)
                reasons.Add("Switched Off");

            var stunnable = turret.TryGetComp<CompStunnable>();
            if (stunnable != null)
            {
                var stunnedProp = stunnable.GetType().GetProperty("Stunned", BindingFlags.Public | BindingFlags.Instance);
                if (stunnedProp != null && (bool)stunnedProp.GetValue(stunnable))
                    reasons.Add("Stunned/EMP");
            }

            var compFCS = turret.TryGetComp<CompTurretFCS>();
            if (compFCS != null && !compFCS.HasFCS)
                reasons.Add("Missing FCS Module");

            if (turret is ThingWithComps twc && twc.AllComps != null)
            {
                foreach (var c in twc.AllComps)
                {
                    if (c != null && c.GetType().Name == "CompAmmoUser")
                    {
                        var hasAmmoProp = c.GetType().GetProperty("HasAmmo", BindingFlags.Public | BindingFlags.Instance);
                        if (hasAmmoProp != null)
                        {
                            bool hasAmmo = (bool)hasAmmoProp.GetValue(c);
                            if (!hasAmmo) reasons.Add("No Ammo in Gun (HasAmmo=False)");
                        }
                        var useAmmoProp = c.GetType().GetProperty("UseAmmo", BindingFlags.Public | BindingFlags.Instance);
                        if (useAmmoProp != null)
                        {
                            bool useAmmo = (bool)useAmmoProp.GetValue(c);
                            var curMagProp = c.GetType().GetProperty("CurMagCount", BindingFlags.Public | BindingFlags.Instance);
                            int curMag = curMagProp != null ? (int)curMagProp.GetValue(c) : 0;
                            if (useAmmo && curMag <= 0) reasons.Add($"Magazine Empty (CurMagCount={curMag})");
                        }
                        break;
                    }
                }
            }

            if (reasons.Count == 0)
                return "Unknown Base Native Rejection";

            return string.Join(", ", reasons);
        }

        public static void Postfix_TurretGun_Active(Thing __instance, ref bool __result)
        {
            bool initialResult = __result;
            var comp = __instance.TryGetComp<CompTurretFCS>();
            if (comp != null && !comp.HasFCS)
            {
                __result = false;
            }

            if (comp != null || !__result)
            {
                if (!__result)
                {
                    string reason = GetTurretInactiveReason(__instance);
                    AMCLogger.LogTurretTarget($"[FCS Active Check] {__instance.LabelCap} @ {__instance.Position} | Active: FALSE (initial: {initialResult}) | Reason: [{reason}]");
                }
                else
                {
                    AMCLogger.LogTurretTarget($"[FCS Active Check] {__instance.LabelCap} @ {__instance.Position} | Active: TRUE | HasFCS: True");
                }
            }
        }

        public static void Postfix_TurretGun_CanSetTarget(Thing __instance, ref bool __result)
        {
            bool initialResult = __result;
            var comp = __instance.TryGetComp<CompTurretFCS>();
            if (comp != null && !comp.HasFCS)
            {
                __result = false;
            }

            if (comp != null)
            {
                AMCLogger.LogTurretTarget($"[FCS CanSetTarget Check] {__instance.LabelCap} @ {__instance.Position} | CanSetTarget: {__result} (initial: {initialResult}) | HasFCS: {comp.HasFCS}");
            }
        }

        public static void Postfix_TurretGun_IsOperational(Thing __instance, ref bool __result)
        {
            bool initialResult = __result;
            var comp = __instance.TryGetComp<CompTurretFCS>();
            if (comp != null && !comp.HasFCS)
            {
                __result = false;
            }

            if (comp != null)
            {
                AMCLogger.LogTurretTarget($"[FCS IsOperational Check] {__instance.LabelCap} @ {__instance.Position} | IsOperational: {__result} (initial: {initialResult}) | HasFCS: {comp.HasFCS}");
            }
        }

        /// <summary>
        /// Attempts to patch CE turret top drawing to ensure barrel draws after turret top.
        /// </summary>
        private static void TryPatchCETurretTop(Harmony harmony)
        {
            // Try to find CE turret top class
            var ceTurretTopType = AccessTools.TypeByName("CombatExtended.TurretTop");
            
            if (ceTurretTopType != null)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Message("Turret Barrel Animation: Found CE TurretTop class, patching Draw method.");
                }
                
                // Try to patch the Draw method
                var drawMethod = AccessTools.Method(ceTurretTopType, "Draw");
                if (drawMethod != null)
                {
                    harmony.Patch(
                        original: drawMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretTopDraw))
                    );
                    if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Message("Turret Barrel Animation: Successfully patched CE TurretTop.Draw method.");
                    }
                }
                else
                {
                    // Try alternative method names
                    var drawAtMethod = AccessTools.Method(ceTurretTopType, "DrawAt");
                    if (drawAtMethod != null)
                    {
                        harmony.Patch(
                            original: drawAtMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TurretTopDraw))
                        );
                        if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                        {
                            Log.Message("Turret Barrel Animation: Successfully patched CE TurretTop.DrawAt method.");
                        }
                    }
                    else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Warning("Turret Barrel Animation: Could not find Draw or DrawAt method in CE TurretTop class.");
                    }
                }
            }
            else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
            {
                Log.Message("Turret Barrel Animation: CE TurretTop class not found. Using Y offset method only.");
            }
        }

        /// <summary>
        /// Patches CE verb firing using Harmony's TargetMethod approach for runtime method discovery.
        /// Also patches warmup-related methods to provide warmup event notifications.
        /// </summary>
        private static void TryPatchCEVerbFiring(Harmony harmony)
        {
            var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
            if (verbType == null)
            {
                //Log.Warning("[Barrel Flash Debug] Verb_LaunchProjectileCE type not found");
                return;
            }

            // Patch TryCastShot for firing events
            var tryCastShotMethod = AccessTools.Method(verbType, "TryCastShot");
            if (tryCastShotMethod != null)
            {
                harmony.Patch(
                    original: tryCastShotMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_LaunchProjectileCE_TryCastShot))
                );
                //Log.Message("[Barrel Flash Debug] Successfully patched Verb_LaunchProjectileCE.TryCastShot");
            }
            else
            {
                //Log.Warning("[Barrel Flash Debug] Could not find Verb_LaunchProjectileCE.TryCastShot method");
            }

            // Patch IncrementBarrelCount to add our custom lateral offset
            var incrementBarrelMethod = AccessTools.Method(verbType, "IncrementBarrelCount");
            if (incrementBarrelMethod != null)
            {
                harmony.Patch(
                    original: incrementBarrelMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_LaunchProjectileCE_IncrementBarrelCount))
                );
            }
            else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
            {
                Log.Warning("[Projectile Offset] Could not find Verb_LaunchProjectileCE.IncrementBarrelCount method");
            }

            // Patch ShiftTarget for rotation clamping
            // ShiftTarget is called after spread/sway calculations but before projectile launch
            // It modifies the shotRotation field which controls horizontal deviation
            
            var settings = TurretBarrelAnimationMod.settings;
            
            if (incrementBarrelMethod != null && settings != null && settings.logStartup)
            {
                Log.Message("[Projectile Offset] Successfully patched Verb_LaunchProjectileCE.IncrementBarrelCount");
            }
            
            // Find ALL ShiftTarget methods
            var allMethods = verbType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var shiftTargetMethods = allMethods.Where(m => m.Name == "ShiftTarget").ToList();

            if (settings != null && settings.logStartup)
            {
                Log.Message($"[AMC] Found {shiftTargetMethods.Count} ShiftTarget method(s) in {verbType.Name}");
            }
            
            int patchedCount = 0;
            foreach (var method in shiftTargetMethods)
            {
                var parameters = method.GetParameters();
                string paramString = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                if (settings != null && settings.logStartup)
                {
                    Log.Message($"[AMC]   - ShiftTarget({paramString})");
                }
                
                try
                {
                    // Patch for rotation clamping
                    var harmonyMethod = new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_LaunchProjectileCE_ShiftTarget_ClampRotation));
                    harmonyMethod.priority = Priority.Last; // Ensure our patch runs last
                    
                    harmony.Patch(
                        original: method,
                        postfix: harmonyMethod
                    );
                    
                    // Also add detailed vertical angle logging patch
                    var loggingHarmonyMethod = new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_LaunchProjectileCE_ShiftTarget_DetailedLogging));
                    loggingHarmonyMethod.priority = Priority.Last; // Run after CE's calculations
                    
                    harmony.Patch(
                        original: method,
                        postfix: loggingHarmonyMethod
                    );
                    
                    if (settings != null && settings.logStartup)
                    {
                        Log.Message($"[AMC]     ✓ Successfully patched this overload (clamping + detailed logging)");
                    }
                    patchedCount++;
                }
                catch (Exception ex)
                {
                    if (settings != null && settings.logStartup)
                    {
                        Log.Warning($"[AMC]     ✗ Failed to patch: {ex.Message}");
                    }
                }
            }
            if (settings != null && settings.logStartup)
            {
                if (patchedCount > 0)
                {
                    Log.Message($"[AMC] Patched {patchedCount} ShiftTarget overload(s) for rotation clamping");
                }
                else
                {
                    Log.Warning("[AMC] Could not patch any ShiftTarget method for rotation clamping");
                }
            }

            // Patch warmup-related methods
            TryPatchCEVerbWarmup(harmony, verbType);
        }

        /// <summary>
        /// Patches CE verb warmup methods to provide warmup event notifications.
        /// </summary>
        private static void TryPatchCEVerbWarmup(Harmony harmony, System.Type verbType)
        {
            // Patch WarmupComplete for successful warmup finish
            var warmupCompleteMethod = AccessTools.Method(verbType, "WarmupComplete");
            if (warmupCompleteMethod != null)
            {
                harmony.Patch(
                    original: warmupCompleteMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_WarmupComplete))
                );
                //Log.Message("[Warmup Debug] Successfully patched Verb.WarmupComplete");
            }

            // Patch Reset method for warmup interruption
            var resetMethod = AccessTools.Method(verbType, "Reset");
            if (resetMethod != null)
            {
                harmony.Patch(
                    original: resetMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_Reset))
                );
                //Log.Message("[Warmup Debug] Successfully patched Verb.Reset");
            }

            // Try to patch TryStartCastOn for warmup start
            // This method initiates the warmup process
            // Note: TryStartCastOn has multiple overloads, so we need to be more specific
            var tryStartCastOnMethods = AccessTools.GetDeclaredMethods(verbType)
                .Where(m => m.Name == "TryStartCastOn")
                .ToArray();

            if (tryStartCastOnMethods.Length > 0)
            {
                // Try to find a method that returns bool (the main TryStartCastOn method)
                var tryStartCastOnMethod = tryStartCastOnMethods.FirstOrDefault(m => m.ReturnType == typeof(bool));
                if (tryStartCastOnMethod != null)
                {
                    harmony.Patch(
                        original: tryStartCastOnMethod,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_TryStartCastOn))
                    );
                    //Log.Message("[Warmup Debug] Successfully patched Verb.TryStartCastOn");
                }
                else
                {
                    //Log.Warning("[Warmup Debug] Could not find suitable TryStartCastOn method to patch");
                }
            }
        }

        /// <summary>
        /// Called after a CE verb tries to cast a shot.
        /// Only triggers firing animation if the shot was successful and the caster is a turret.
        /// Also logs projectile parameters if logging is enabled.
        /// </summary>
        public static void Postfix_Verb_LaunchProjectileCE_TryCastShot(bool __result, object __instance)
        {
            try
            {
                // Only trigger if the shot was successful
                if (!__result)
                {
                    return;
                }

                // Get the caster (turret) from the verb
                Thing caster = null;

                // Try property first
                var casterProperty = AccessTools.Property(__instance.GetType(), "Caster");
                if (casterProperty != null)
                {
                    caster = casterProperty.GetValue(__instance) as Thing;
                }

                // Try field if property didn't work
                if (caster == null)
                {
                    var casterField = AccessTools.Field(__instance.GetType(), "caster");
                    if (casterField != null)
                    {
                        caster = casterField.GetValue(__instance) as Thing;
                    }
                }

                if (caster == null)
                {
                    return; // Couldn't find caster
                }

                // Check if it's a turret (CE or vanilla)
                bool isTurret = caster is Building_Turret;
                if (!isTurret)
                {
                    // Try to check if it's a CE turret by type name
                    var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
                    if (ceTurretType != null && ceTurretType.IsAssignableFrom(caster.GetType()))
                    {
                        isTurret = true;
                    }
                }

                if (!isTurret)
                {
                    return; // Not a turret, ignore
                }

                // === LOG FIRING TIMESTAMP (FIRST - when gun actually fires) ===
                int fireTime = Find.TickManager.TicksGame;
                AMCLogger.LogTurretFireTimestamp(
                    $"T={fireTime} | Turret: {caster.LabelCap} at {caster.Position} FIRED");

                // Get turret base rotation (actual turret facing) - declare early for both logging and smoke
                float turretBaseRotation = float.NaN;
                if (caster is Building_Turret building_turret)
                {
                    try
                    {
                        // Try toget the turret top
                        var topField = building_turret.GetType().GetField("top", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (topField != null)
                        {
                            var top = topField.GetValue(building_turret);
                            if (top != null)
                            {
                                // Get CurRotation property
                                var curRotationProp = top.GetType().GetProperty("CurRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (curRotationProp != null)
                                {
                                    turretBaseRotation = (float)curRotationProp.GetValue(top);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors - we'll just not have turret rotation
                    }
                }

                // === LOG PROJECTILE LAUNCH PARAMETERS ===
                try
                {
                    Type verbType = __instance.GetType();
                    
                    // Get equipment
                    FieldInfo equipmentSourceField = verbType.GetField("EquipmentSource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    Thing equipment = equipmentSourceField?.GetValue(__instance) as Thing;

                    // Get target
                    PropertyInfo currentTargetProp = verbType.GetProperty("CurrentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo currentTargetField = verbType.GetField("currentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    LocalTargetInfo target = default(LocalTargetInfo);
                    
                    if (currentTargetProp != null)
                    {
                        target = (LocalTargetInfo)currentTargetProp.GetValue(__instance);
                    }
                    else if (currentTargetField != null)
                    {
                        target = (LocalTargetInfo)currentTargetField.GetValue(__instance);
                    }

                    // Get origin (2D position)
                    Vector2 origin = new Vector2(caster.Position.x, caster.Position.z);

                    // Get shot height
                    float shotHeight = 0.85f; // Default turret height
                    FieldInfo shotHeightField = verbType.GetField("shotHeight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (shotHeightField != null)
                    {
                        shotHeight = (float)shotHeightField.GetValue(__instance);
                    }

                    // Get shot speed
                    float shotSpeed = 100f; // Fallback default
                    FieldInfo shotSpeedField = verbType.GetField("shotSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (shotSpeedField != null)
                    {
                        shotSpeed = (float)shotSpeedField.GetValue(__instance);
                    }

                    // Get shot angle
                    float shotAngle = 0f;
                    FieldInfo shotAngleField = verbType.GetField("shotAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (shotAngleField != null)
                    {
                        shotAngle = (float)shotAngleField.GetValue(__instance);
                    }

                    // Get shot rotation
                    float shotRotation = 0f;
                    FieldInfo shotRotationField = verbType.GetField("shotRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (shotRotationField != null)
                    {
                        shotRotation = (float)shotRotationField.GetValue(__instance);
                    }

                    // turretBaseRotation already extracted above (line ~496)

                    // Extract CE's internal deviation breakdown
                    float rotationDegrees = 0f; // Sway + Recoil
                    float lastShotRotation = 0f; // Base rotation to shifted target
                    Vector2 newTargetLoc = Vector2.zero; // Shifted target position
                    
                    try
                    {
                        FieldInfo rotationDegreesField = verbType.GetField("rotationDegrees", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (rotationDegreesField != null)
                        {
                            rotationDegrees = (float)rotationDegreesField.GetValue(__instance);
                        }
                        
                        FieldInfo lastShotRotationField = verbType.GetField("lastShotRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (lastShotRotationField != null)
                        {
                            lastShotRotation = (float)lastShotRotationField.GetValue(__instance);
                        }
                        
                        FieldInfo newTargetLocField = verbType.GetField("newTargetLoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (newTargetLocField != null)
                        {
                            newTargetLoc = (Vector2)newTargetLocField.GetValue(__instance);
                        }
                    }
                    catch
                    {
                        // Silently fail - these are optional debugging values
                    }

                    // Call the logger
                    AMCLogger.LogProjectileLaunch(
                        verbInstance: __instance,
                        launcher: caster,
                        origin: origin,
                        shotHeight: shotHeight,
                        shotSpeed: shotSpeed,
                        shotAngle: shotAngle,
                        shotRotation: shotRotation,
                        turretBaseRotation: turretBaseRotation,
                        rotationDegrees: rotationDegrees,
                        lastShotRotation: lastShotRotation,
                        newTargetLoc: newTargetLoc,
                        target: target,
                        equipment: equipment,
                        projectileInstance: null
                    );
                }
                catch (Exception logEx)
                {
                    // Silently fail logging - don't break the game
                    Log.Warning($"[AMC] Failed to log projectile parameters: {logEx.Message}");
                }

                // Found a turret that successfully fired - trigger the animation
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    barrelComp.TriggerFiring();
                }

                // Trigger smoke effects (use rotation extracted above)
                var smokeComp = caster.TryGetComp<CompTurretSmoker>();
                if (smokeComp != null)
                {
                    smokeComp.OnFired(turretBaseRotation);
                }
            }
            catch (Exception ex)
            {
                // Catch any exceptions to prevent breaking the turret's firing logic
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_LaunchProjectileCE_TryCastShot: {ex}");
            }
        }




        /// <summary>
        /// Called after a verb successfully completes warmup.
        /// Triggers warmup complete events for turrets.
        /// </summary>
        public static void Postfix_Verb_WarmupComplete(object __instance)
        {
            try
            {
                // Get the caster from the verb
                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null)
                {
                    return;
                }

                // Check if it's a turret
                if (!IsTurret(caster))
                {
                    return; // Not a turret, ignore
                }

                // Notify the mod that warmup completed for this turret
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Call a method to handle warmup completion
                    barrelComp.OnWarmupComplete();
                }

                //Log.Message($"[Warmup Debug] Warmup completed for turret {caster.def.defName}");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_WarmupComplete: {ex}");
            }
        }

        /// <summary>
        /// Called after a verb is reset (warmup interrupted).
        /// Triggers warmup interrupt events for turrets.
        /// </summary>
        public static void Postfix_Verb_Reset(object __instance)
        {
            try
            {
                // Get the caster from the verb
                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null)
                {
                    return;
                }

                // Check if it's a turret
                if (!IsTurret(caster))
                {
                    return; // Not a turret, ignore
                }

                // Notify the mod that warmup was interrupted for this turret
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Call a method to handle warmup interruption
                    barrelComp.OnWarmupInterrupted();
                }

                //Log.Message($"[Warmup Debug] Warmup interrupted for turret {caster.def.defName}");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_Reset: {ex}");
            }
        }

        /// <summary>
        /// Called after TryStartCastOn (warmup initiation attempt).
        /// Triggers warmup start events for turrets when warmup actually begins.
        /// </summary>
        public static void Postfix_Verb_TryStartCastOn(bool __result, object __instance)
        {
            try
            {
                // Only trigger if TryStartCastOn returned true (warmup successfully started)
                if (!__result)
                {
                    return;
                }

                // Get the caster from the verb
                Thing caster = GetCasterFromVerb(__instance);
                if (caster == null)
                {
                    return;
                }

                // Check if it's a turret
                if (!IsTurret(caster))
                {
                    return; // Not a turret, ignore
                }

                // Notify the mod that warmup started for this turret
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Call a method to handle warmup start
                    barrelComp.OnWarmupStarted();
                }

                //Log.Message($"[Warmup Debug] Warmup started for turret {caster.def.defName}");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[Barrel Animation] Error in Postfix_Verb_TryStartCastOn: {ex}");
            }
        }

        /// <summary>
        /// Helper method to get the caster from a verb instance.
        /// </summary>
        private static Thing GetCasterFromVerb(object verbInstance)
        {
            // Try property first
            var casterProperty = AccessTools.Property(verbInstance.GetType(), "Caster");
            if (casterProperty != null)
            {
                return casterProperty.GetValue(verbInstance) as Thing;
            }

            // Try field if property didn't work
            var casterField = AccessTools.Field(verbInstance.GetType(), "caster");
            if (casterField != null)
            {
                return casterField.GetValue(verbInstance) as Thing;
            }

            return null;
        }

        /// <summary>
        /// Helper method to check if a thing is a turret.
        /// </summary>
        private static bool IsTurret(Thing thing)
        {
            if (thing is Building_Turret)
            {
                return true;
            }

            // Check for CE turret types
            var ceTurretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
            if (ceTurretType != null && ceTurretType.IsAssignableFrom(thing.GetType()))
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// Called after a turret completes a burst.
        /// Triggers burst complete events for spinning animation.
        /// </summary>
        public static void Postfix_BurstComplete(object __instance)
        {
            if (__instance is Building_Turret turret)
            {
                var barrelComp = turret.GetComp<CompTurretBarrel>();
                if (barrelComp != null)
                {
                    // Trigger burst complete for spinning animation
                    barrelComp.OnBurstComplete();
                }
            }
        }

        /// <summary>
        /// Called after CE turret building drawing.
        /// Draws the barrel after the turret top if drawOnTop is true.
        /// This is an alternative method that works by patching the turret building itself.
        /// </summary>
        public static void Postfix_TurretDrawAt(object __instance)
        {
            try
        {
            if (__instance is Building_Turret turret)
            {
                var barrelComp = turret.GetComp<CompTurretBarrel>();
                    if (barrelComp != null && barrelComp.Extension != null && barrelComp.Extension.drawOnTop)
                {
                        // Draw the barrel after the turret building (which includes turret top) has been drawn
                        // This ensures the barrel appears on top
                        barrelComp.DrawBarrelNow();
                    }
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"Turret Barrel Animation: Error in Postfix_TurretDrawAt: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called after CE turret top drawing.
        /// Draws the barrel component after the turret top only if drawOnTop is true.
        /// </summary>
        public static void Postfix_TurretTopDraw(object __instance)
        {
            try
            {
                Building_Turret turret = null;
                
                // Try to get the parent turret from the turret top
                // CE TurretTop typically has a reference to the parent turret
                var turretTopType = __instance.GetType();
                
                // Try field names
                var fieldNames = new[] { "parentTurret", "ParentTurret", "turret", "Turret", "parent", "Parent" };
                foreach (var fieldName in fieldNames)
                {
                    var field = AccessTools.Field(turretTopType, fieldName);
                    if (field != null)
                    {
                        var value = field.GetValue(__instance);
                        if (value is Building_Turret t)
                        {
                            turret = t;
                            break;
                        }
                    }
                }
                
                // Try property names if field didn't work
                if (turret == null)
                {
                    var propertyNames = new[] { "parentTurret", "ParentTurret", "turret", "Turret", "parent", "Parent" };
                    foreach (var propName in propertyNames)
                    {
                        var prop = AccessTools.Property(turretTopType, propName);
                        if (prop != null)
                        {
                            var value = prop.GetValue(__instance);
                            if (value is Building_Turret t)
                            {
                                turret = t;
                                break;
                            }
                        }
                    }
                }

                if (turret != null)
                {
                    // Debug: Log turret top altitude
                    if (TurretBarrelAnimationMod.settings?.logTemporaryDebug ?? false)
                    {
                        string turretTopAltitudeKey = $"TURRETTOP_ALTITUDE_{turret.def.defName}";
                        if (!AbsolutelyMoreCannons.CompTurretBarrel.loggedTypes.Contains(turretTopAltitudeKey))
                        {
                            AbsolutelyMoreCannons.CompTurretBarrel.loggedTypes.Add(turretTopAltitudeKey);
                            var turretTopDrawPos = turret.DrawPos;
                            Verse.Log.Message($"[Altitude Debug] Turret Top (via parent turret) Y position: {turretTopDrawPos.y:F3}");
                        }
                    }
                    
                    var barrelComp = turret.GetComp<CompTurretBarrel>();
                    if (barrelComp != null && barrelComp.Extension != null)
                    {
                        // Only draw if drawOnTop is enabled
                        if (barrelComp.Extension.drawOnTop)
                        {
                            // Draw the barrel after the turret top
                            barrelComp.DrawBarrelNow();
                        }
                    }
                }
                else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    // Debug: Log that we couldn't find the turret
                    Log.Warning($"Turret Barrel Animation: Could not find parent turret from turret top. TurretTop type: {turretTopType.Name}");
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"Turret Barrel Animation: Error drawing barrel after turret top: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        
        /// <summary>
        /// Postfix for Verb.TryCastNextBurstShot to override tick delay based on RPM.
        /// </summary>
        public static void Postfix_Verb_TryCastNextBurstShot(Verb __instance)
        {
             try
             {
                 Thing caster = __instance.Caster;
                 if (caster == null) return;

                 // Check for our component
                 var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                 if (barrelComp != null)
                 {
                     float ticksFloat = barrelComp.GetCurrentTicksBetweenBurstShots();
                     if (ticksFloat > 0f)
                     {
                         int ticks = Mathf.RoundToInt(ticksFloat);
                         // Use traverse or reflection to set the field since it might be protected/private or we just want to be safe
                         // ticksToNextBurstShot is protected in Verb
                         AccessTools.Field(typeof(Verb), "ticksToNextBurstShot").SetValue(__instance, ticks);
                     }
                 }
             }
             catch (Exception ex)
             {
                // Only log once per session to avoid spam
                if (!CompTurretBarrel.loggedTypes.Contains("TryCastNextBurstShot_Error"))
                {
                    CompTurretBarrel.loggedTypes.Add("TryCastNextBurstShot_Error");
                    Log.Warning($"Turret Barrel Animation: Error in Postfix_Verb_TryCastNextBurstShot: {ex.Message}");
                }
             }
        }

        /// <summary>
        /// Postfix for Verb_ShootCE.ShotsPerBurstFor to override burst count.
        /// </summary>
        public static void Postfix_Verb_ShotsPerBurstFor(ref int __result, object __instance)
        {
             try
             {
                 Thing caster = GetCasterFromVerb(__instance);
                 if (caster == null) return;

                 var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                 if (barrelComp != null)
                 {
                     int burstOverride = barrelComp.GetCurrentBurstCount();
                     if (burstOverride > 0)
                     {
                         __result = burstOverride;
                     }
                 }
             }
             catch (Exception ex)
             {
                  if (!CompTurretBarrel.loggedTypes.Contains("ShotsPerBurstFor_Error"))
                  {
                      CompTurretBarrel.loggedTypes.Add("ShotsPerBurstFor_Error");
                      Log.Warning($"Turret Barrel Animation: Error in Postfix_Verb_ShotsPerBurstFor: {ex.Message}");
                  }
             }
        }

        /// <summary>
        /// Postfix patch for Verb_LaunchProjectileCE.IncrementBarrelCount to add custom barrel offset.
        /// Modifies the return value to include our lateral spacing for multi-barrel turrets.
        /// </summary>
        public static void Postfix_Verb_LaunchProjectileCE_IncrementBarrelCount(object __instance, ref Vector2 __result)
        {
            try
            {
                // Get the caster
                var casterField = AccessTools.Field(__instance.GetType().BaseType, "caster");
                if (casterField == null) return;
                
                var caster = casterField.GetValue(__instance) as Thing;
                if (caster == null) return;

                // Try to get CompTurretBarrel
                var barrelComp = caster.TryGetComp<CompTurretBarrel>();
                if (barrelComp == null || barrelComp.Extension == null) return;

                // Get turret's actual rotation (NOT shotRotation which includes spread/sway)
                float turretRotation = barrelComp.GetCurrentBarrelRotation();
                
                // Calculate rotation in radians for vector math
                float rotationRad = turretRotation * Mathf.Deg2Rad;
                
                // Forward direction (along barrel aim) - matches CompTurretSmoker.GetDirectionFromRotation
                // CE uses Vector2(x, z) so we convert from 3D forward vector
                Vector2 forwardDir = new Vector2(Mathf.Sin(rotationRad), Mathf.Cos(rotationRad));
                
                // Apply forward offset if configured
                Vector2 totalOffset = Vector2.zero;
                if (barrelComp.Extension.firingAnimation != null && 
                    barrelComp.Extension.firingAnimation.projectileSpawnOffset != 0f)
                {
                    float forwardOffset = barrelComp.Extension.firingAnimation.projectileSpawnOffset;
                    totalOffset += forwardDir * forwardOffset;
                }

                // Apply lateral offset for multi-barrel turrets
                int barrelAmount = barrelComp.Extension.barrelAmount;
                float barrelSpacing = barrelComp.Extension.barrelSpacing;
                bool sequentialFiring = barrelComp.Extension.sequentialFiring;

                if (barrelAmount > 1 && barrelSpacing != 0f && sequentialFiring)
                {
                    // Get multiBarrelIndex (already incremented by the original method)
                    var multiBarrelIndexField = AccessTools.Field(__instance.GetType(), "multiBarrelIndex");
                    if (multiBarrelIndexField != null)
                    {
                        int rawIndex = (int)multiBarrelIndexField.GetValue(__instance);
                        int currentBarrelIndex = rawIndex % barrelAmount;

                        // Calculate perpendicular direction (90 degrees clockwise from forward)
                        // Perpendicular in 2D: rotate forward 90° clockwise = (z, -x) → (Cos, -Sin)
                        Vector2 rightDir = new Vector2(Mathf.Cos(rotationRad), -Mathf.Sin(rotationRad));

                        // Calculate lateral offset based on barrel index
                        // Formula: (barrelIndex - (N-1)/2) * spacing
                        float lateralOffset = (currentBarrelIndex - (barrelAmount - 1) / 2f) * barrelSpacing;
                        
                        totalOffset += rightDir * lateralOffset;
                        
                        var settings = TurretBarrelAnimationMod.settings;
                        if (settings != null && settings.logProjectileOffsets)
                        {
                            Log.Message($"[Projectile Offset] {caster.def.defName} - " +
                                $"Barrel {currentBarrelIndex}/{barrelAmount}, " +
                                $"Rotation: {turretRotation:F1}°, " +
                                $"Forward: {barrelComp.Extension.firingAnimation?.projectileSpawnOffset:F2}, " +
                                $"Lateral: {lateralOffset:F2}, " +
                                $"Total: ({totalOffset.x:F2}, {totalOffset.y:F2})");
                        }
                    }
                }
                else if (totalOffset != Vector2.zero)
                {
                    // Log forward offset even for single-barrel turrets (if logging enabled)
                    var settings = TurretBarrelAnimationMod.settings;
                    if (settings != null && settings.logProjectileOffsets)
                    {
                        Log.Message($"[Projectile Offset] {caster.def.defName} - " +
                            $"Rotation: {turretRotation:F1}°, " +
                            $"Forward: {barrelComp.Extension.firingAnimation?.projectileSpawnOffset:F2}, " +
                            $"Total: ({totalOffset.x:F2}, {totalOffset.y:F2})");
                    }
                }
                
                // Apply the total offset
                __result += totalOffset;
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[Barrel Animation] Error in IncrementBarrelCount offset patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patches CE projectile launch to log final parameters before launch.
        /// Uses the existing TryCastShot postfix to extract parameters after they're calculated.
        /// </summary>
        private static void TryPatchCEProjectileLaunch(Harmony harmony)
        {
            try
            {
                var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
                if (verbType == null)
                {
                    if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Message("[AMC] CE Verb_LaunchProjectileCE not found - projectile launch logging disabled.");
                    }
                    return;
                }

                // We'll enhance the existing TryCastShot postfix to include logging
                // The patch is already applied in TryPatchCEVerbFiring, so we just need to make sure
                // our Postfix_Verb_LaunchProjectileCE_TryCastShot does the logging
                var settings = TurretBarrelAnimationMod.settings;
                if (settings != null && settings.logStartup)
                {
                    Log.Message("[AMC] Projectile launch logging will use TryCastShot postfix (already patched).");
                }
                
                // NOTE: ShiftTarget patch for perfect elevation is now in TryPatchCEVerbFiring()
                
                // Also patch ProjectileCE.Launch to log actual spawned projectile parameters
                var projectileCEType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
                if (projectileCEType != null)
                {
                    // Specify exact parameter types to avoid ambiguous match
                    var launchMethod = AccessTools.Method(projectileCEType, "Launch", new Type[] {
                        typeof(Thing),      // launcher
                        typeof(Vector2),    // origin
                        typeof(float),      // shotAngle
                        typeof(float),      // shotRotation
                        typeof(float),      // shotHeight
                        typeof(float),      // shotSpeed
                        typeof(Thing),      // equipment
                        typeof(float)       // dist
                    });
                    
                    if (launchMethod != null)
                    {
                        harmony.Patch(
                            launchMethod,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_ProjectileCE_Launch_V2))
                        );
                        if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                        {
                            Log.Message("[AMC] Successfully patched ProjectileCE.Launch for projectile spawn logging.");
                        }
                    }
                    else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Warning("[AMC] Could not find ProjectileCE.Launch method with specified signature.");
                    }
                }
                else if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning("[AMC] Could not find ProjectileCE type.");
                }
            }
            catch (Exception ex)
            {
                if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                {
                    Log.Warning($"[AMC] Error setting up CE projectile launch logging: {ex.Message}");
                }
            }
        }
        public static void Postfix_ProjectileCE_Launch_V2(Thing launcher, Vector2 origin, float shotAngle, float shotRotation, float shotHeight, float shotSpeed, Thing equipment, float distance)
        {
             try
             {
                 // Pass data to the logger with default values for missing context
                 AMCLogger.LogProjectileLaunch(
                     verbInstance: null,
                     launcher: launcher,
                     origin: origin,
                     shotHeight: shotHeight,
                     shotSpeed: shotSpeed,
                     shotAngle: shotAngle,
                     shotRotation: shotRotation,
                     turretBaseRotation: float.NaN,
                     rotationDegrees: 0f,
                     lastShotRotation: 0f,
                     newTargetLoc: Vector2.zero,
                     target: LocalTargetInfo.Invalid,
                     equipment: equipment,
                     projectileInstance: null
                 );
             }
             catch (Exception ex)
             {
                 Log.Warning($"[AMC] Error in projectile launch logger: {ex.Message}");
             }
        }

        /// <summary>
        /// Checks if the third-party 'Muzzle Flash' mod (by IssacZhuang) is loaded.
        /// If loaded, patches MuzzleFlashUtility.SpawnMuzzleFlash to prevent ghost flashes on aborted burst shots.
        /// We patch SpawnMuzzleFlash (the actual flash renderer) rather than the Harmony postfix method,
        /// because Harmony inlines postfixes into IL — patching the postfix method itself has no effect.
        /// </summary>
        private static void TryPatchMuzzleFlashMod(Harmony harmony)
        {
            try
            {
                Type muzzleFlashUtilType = Verse.GenTypes.GetTypeInAnyAssembly("MuzzleFlash.MuzzleFlashUtility") 
                                          ?? AccessTools.TypeByName("MuzzleFlash.MuzzleFlashUtility");

                if (muzzleFlashUtilType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            var found = asm.GetType("MuzzleFlash.MuzzleFlashUtility");
                            if (found != null)
                            {
                                muzzleFlashUtilType = found;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (muzzleFlashUtilType == null)
                {
                    if (TurretBarrelAnimationMod.settings?.logStartup ?? false)
                    {
                        Log.Message("[AMC] Muzzle Flash mod not detected on startup.");
                    }
                    return;
                }

                // Patch SpawnMuzzleFlash (extension method on Map)
                MethodInfo spawnMethod = AccessTools.Method(muzzleFlashUtilType, "SpawnMuzzleFlash");
                if (spawnMethod != null)
                {
                    var tryCastNextBurstShot = AccessTools.Method(typeof(Verb), "TryCastNextBurstShot");

                    // 1. Prefix on TryCastNextBurstShot — mark turret burst start, reset flags
                    var burstPrefix = new HarmonyMethod(typeof(HarmonyPatches), nameof(Prefix_Verb_TryCastNextBurstShot_MarkTurretBurst));
                    burstPrefix.priority = HarmonyLib.Priority.First;
                    harmony.Patch(
                        original: tryCastNextBurstShot,
                        prefix: burstPrefix
                    );

                    // 2. Postfix on Verb_LaunchProjectileCE.TryCastShot — positive gate (open only when projectile spawned)
                    Type verbLaunchCEType = typeof(CombatExtended.Verb_LaunchProjectileCE);
                    MethodInfo tryCastShot = AccessTools.Method(verbLaunchCEType, "TryCastShot");
                    if (tryCastShot != null)
                    {
                        harmony.Patch(
                            original: tryCastShot,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_TryCastShot_MuzzleFlashGate))
                        );
                    }

                    // 3. Prefix on SpawnMuzzleFlash — consumes the positive gate
                    harmony.Patch(
                        original: spawnMethod,
                        prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Prefix_MuzzleFlashUtility_SpawnMuzzleFlash))
                    );

                    // 4. Postfix on TryCastNextBurstShot — clear flags after MF mod's postfix runs (Priority.Last)
                    var burstPostfix = new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_Verb_TryCastNextBurstShot_ClearTurretBurst));
                    burstPostfix.priority = HarmonyLib.Priority.Last;
                    harmony.Patch(
                        original: tryCastNextBurstShot,
                        postfix: burstPostfix
                    );

                    Log.Message("[AMC] Patched MuzzleFlashUtility.SpawnMuzzleFlash to prevent ghost flashes on aborted turret bursts.");
                }
                else
                {
                    Log.Warning("[AMC] Muzzle Flash mod detected, but SpawnMuzzleFlash method was not found.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC] Failed to patch Muzzle Flash mod: {ex.Message}");
            }
        }
        /// <summary>
        /// Positive gate: set to true ONLY when Verb_LaunchProjectileCE.TryCastShot returns true
        /// (a projectile was actually spawned) for a turret caster. SpawnMuzzleFlash consumes this flag.
        /// This eliminates ghost flashes from:
        /// - Burst wind-down ticks (TryCastNextBurstShot runs but TryCastShot is not called or returns false)
        /// - Aborted shots (target downed/invalid, CE skips projectile spawn)
        /// </summary>
        [ThreadStatic]
        private static bool _allowMuzzleFlash;

        /// <summary>
        /// Postfix on Verb_LaunchProjectileCE.TryCastShot — sets the allow flag when a shot actually fires.
        /// </summary>
        public static void Postfix_TryCastShot_MuzzleFlashGate(bool __result, object __instance)
        {
            _allowMuzzleFlash = false;

            if (!__result) return;

            try
            {
                var verb = __instance as Verb;
                if (verb?.caster == null) return;
                if (!(verb.caster is Building_Turret)) return;

                _allowMuzzleFlash = true;
                AMCLogger.LogMuzzleFlashMod($"[GATE OPEN] TryCastShot succeeded → flash allowed | Turret: {verb.caster.LabelCap}");
            }
            catch { }
        }

        /// <summary>
        /// Harmony Prefix on MuzzleFlashUtility.SpawnMuzzleFlash.
        /// Only allows the flash if a turret projectile was actually spawned on this tick.
        /// For non-turret sources (pawns etc.), always allows.
        /// </summary>
        public static bool Prefix_MuzzleFlashUtility_SpawnMuzzleFlash()
        {
            if (_allowMuzzleFlash)
            {
                AMCLogger.LogMuzzleFlashMod("[SPAWNED] SpawnMuzzleFlash allowed (gate open).");
                _allowMuzzleFlash = false; // Consume
                return true;
            }

            // If gate is not open, check if this is even a turret flash.
            // Non-turret flashes (pawn weapons) won't set the gate, so we always allow them.
            // We can't distinguish here, so we use a secondary flag.
            if (!_isTurretBurstActive)
            {
                // Not inside a turret burst → this is a pawn weapon flash, allow it
                return true;
            }

            AMCLogger.LogMuzzleFlashMod("[PREVENTED] Blocked SpawnMuzzleFlash call (no successful TryCastShot for this burst tick).");
            return false;
        }

        /// <summary>
        /// Tracks whether we are currently inside a turret's TryCastNextBurstShot call.
        /// Used to distinguish turret flashes from pawn weapon flashes in SpawnMuzzleFlash.
        /// </summary>
        [ThreadStatic]
        private static bool _isTurretBurstActive;

        /// <summary>
        /// Prefix on Verb.TryCastNextBurstShot — marks the start of a turret burst tick.
        /// </summary>
        public static void Prefix_Verb_TryCastNextBurstShot_MarkTurretBurst(Verb __instance)
        {
            _isTurretBurstActive = false;
            _allowMuzzleFlash = false;

            if (__instance?.caster == null) return;
            if (__instance.caster is Building_Turret)
            {
                _isTurretBurstActive = true;
            }
        }

        /// <summary>
        /// Postfix on Verb.TryCastNextBurstShot — clears the turret burst flag after the full tick.
        /// Runs AFTER the MF mod's postfix has already called (or not called) SpawnMuzzleFlash.
        /// </summary>
        public static void Postfix_Verb_TryCastNextBurstShot_ClearTurretBurst(Verb __instance)
        {
            _isTurretBurstActive = false;
            _allowMuzzleFlash = false;
        }
    }
}
