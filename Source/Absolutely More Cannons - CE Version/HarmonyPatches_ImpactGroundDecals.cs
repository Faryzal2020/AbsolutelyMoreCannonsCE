using System;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patch on ProjectileCE.Impact to spawn temporary ground crater and soot overlay decals
    /// for shells configured with ImpactGroundDecalExtension.
    /// </summary>
    [HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.Impact))]
    public static class Patch_ProjectileCE_Impact_GroundDecals
    {
        public static void Prefix(ProjectileCE __instance, Thing hitThing)
        {
            try
            {
                if (__instance == null || __instance.Map == null || __instance.def == null)
                {
                    return;
                }

                // If projectile has explosionDelay > 0 and hasn't finished its countdown yet, wait for actual detonation
                if (__instance is ProjectileCE_Explosive explosive &&
                    __instance.def.projectile != null &&
                    __instance.def.projectile.explosionDelay > 0 &&
                    !__instance.landed &&
                    explosive.ticksToDetonation == 0)
                {
                    return;
                }

                ImpactGroundDecalExtension ext = __instance.def.GetModExtension<ImpactGroundDecalExtension>();
                if (ext == null || (!ext.spawnCrater && !ext.spawnSoot))
                {
                    return;
                }

                // 1. Verify this is a ground impact (not mid-air airburst / flak / high roof hit)
                if (!IsGroundImpact(__instance, hitThing))
                {
                    return;
                }

                Map map = __instance.Map;
                Vector3 exactPos = __instance.ExactPosition;
                if (hitThing is Pawn hitPawn)
                {
                    exactPos.x = hitPawn.DrawPos.x;
                    exactPos.z = hitPawn.DrawPos.z;
                }

                IntVec3 cell = exactPos.ToIntVec3();
                if (!cell.IsValid || !cell.InBounds(map))
                {
                    return;
                }

                TerrainDef terrain = map.terrainGrid?.TerrainAt(cell);
                if (terrain == null)
                {
                    return;
                }

                // Do not spawn ground crater or soot on water or space vacuum tiles
                if (IsWaterOrSpaceTerrain(terrain))
                {
                    return;
                }

                // 2. Check terrain eligibility:
                // - Crater: only on Dirt/Soil and Sand
                // - Soot: on all solid ground terrains
                bool canSpawnCraterHere = ext.spawnCrater && IsDirtOrSandTerrain(terrain);
                bool canSpawnSootHere = ext.spawnSoot;

                if (!canSpawnCraterHere && !canSpawnSootHere)
                {
                    return;
                }

                // 3. Calculate dynamic scale from explosionRadius and damageAmountBase
                float explosionRadius = __instance.def.projectile != null ? __instance.def.projectile.explosionRadius : 1f;
                float damageAmount = __instance.DamageAmount;

                // Also check CompProperties_ExplosiveCE if explosionRadius on projectile is 0
                if (explosionRadius <= 0f)
                {
                    var compExplosive = __instance.def.GetCompProperties<CompProperties_ExplosiveCE>();
                    if (compExplosive != null && compExplosive.explosiveRadius > 0f)
                    {
                        explosionRadius = compExplosive.explosiveRadius;
                        if (compExplosive.damageAmountBase > 0f)
                        {
                            damageAmount = compExplosive.damageAmountBase;
                        }
                    }
                }

                float baseScale = ext.ComputeBaseScale(explosionRadius, damageAmount);
                float craterScale = baseScale * ext.craterScaleMultiplier;
                float sootScale = baseScale * ext.sootScaleMultiplier;

                var manager = map.GetComponent<MapComponent_ImpactDecalManager>();
                manager?.AddImpactDecal(
                    impactPos: exactPos,
                    spawnCrater: canSpawnCraterHere,
                    spawnSoot: canSpawnSootHere,
                    craterScale: craterScale,
                    sootScale: sootScale,
                    ext: ext);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[AMC] Error spawning ground impact decal: {ex}", 9482716);
            }
        }

        /// <summary>
        /// Determines whether the projectile impact occurred on or at ground level (excluding mid-air airbursts and roof impacts).
        /// </summary>
        private static bool IsGroundImpact(ProjectileCE projectile, Thing hitThing)
        {
            Vector3 exactPos = projectile.ExactPosition;

            // If it hit a pawn or building on the ground, only allow if impact altitude is near ground level
            if (hitThing != null)
            {
                return exactPos.y <= 2.0f;
            }

            // If hitThing is null (ground, roof, or mid-air fuse), require low altitude
            // Note: CE roof impacts happen at roof height (~2.0f+), and airbursts happen at burstAltitude
            if (exactPos.y > 1.8f)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the terrain is water or space vacuum where ground craters/soot should not appear.
        /// </summary>
        private static bool IsWaterOrSpaceTerrain(TerrainDef terrain)
        {
            if (terrain.IsWater || terrain.takeSplashes)
            {
                return true;
            }

            if (string.Equals(terrain.defName, "Space", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the terrain is dirt/soil or sand (excludes stone, concrete, metal, wood, carpet, tile, ice, etc.).
        /// </summary>
        private static bool IsDirtOrSandTerrain(TerrainDef terrain)
        {
            // Exclude man-made floors or foundations
            if (terrain.layerable || !terrain.natural)
            {
                return false;
            }

            // 1. Check RimWorld's built-in TerrainCategoryType (Soil and Sand cover vanilla Soil, SoilRich, MossyTerrain, MarshyTerrain, Mud, Riverbank, Sand, SoftSand)
            if (terrain.categoryType == TerrainDef.TerrainCategoryType.Soil ||
                terrain.categoryType == TerrainDef.TerrainCategoryType.Sand)
            {
                return true;
            }

            // 2. Check terrain tags ("Soil", "Sand", "Mud", "Dirt") for terrains like Gravel ("stony soil" has tag "Soil" in vanilla)
            if (terrain.tags != null)
            {
                for (int i = 0; i < terrain.tags.Count; i++)
                {
                    string tag = terrain.tags[i];
                    if (string.Equals(tag, "Soil", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "Sand", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "Dirt", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "Mud", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // 3. Check generatedFilth (e.g. Filth_Dirt or Filth_Sand), while excluding Ice (which has generatedFilth=Filth_Dirt in vanilla)
            if (terrain.generatedFilth != null &&
                (terrain.tags == null || !terrain.tags.Contains("Ice")) &&
                terrain.defName.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) < 0)
            {
                string filthName = terrain.generatedFilth.defName;
                if (string.Equals(filthName, "Filth_Dirt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(filthName, "Filth_Sand", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
