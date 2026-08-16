using CombatExtended;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Harmony patch on ProjectileCE.Impact to handle damageRadius for AbsolutelyMoreCannons.ProjectilePropertiesCE.
    /// Applies silent radial damage (doVisualEffects = false, doSoundEffects = false) around the impact point when explosionRadius <= 0.
    /// If explosionRadius > 0, CE's standard explosion handles it.
    /// </summary>
    [HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.Impact))]
    public static class Patch_ProjectileCE_Impact_DamageRadius
    {
        public static void Prefix(ProjectileCE __instance, Thing hitThing)
        {
            if (__instance == null || __instance.Map == null) return;

            if (__instance.def?.projectile is AbsolutelyMoreCannons.ProjectilePropertiesCE amcProps)
            {
                if (amcProps.explosionRadius <= 0f && amcProps.damageRadius > 0f)
                {
                    ApplySilentDamageRadius(__instance, amcProps);
                }
            }
        }

        public static void ApplySilentDamageRadius(ProjectileCE projectile, AbsolutelyMoreCannons.ProjectilePropertiesCE amcProps)
        {
            Vector3 explodePos = projectile.ExactPosition;
            IntVec3 cell = explodePos.ToIntVec3();
            Map map = projectile.Map;
            if (!cell.IsValid || map == null) return;

            DamageDef damDef = projectile.DamageDef;
            if (damDef == null) return;

            int damAmount = Mathf.FloorToInt(projectile.DamageAmount);
            float armorPen = amcProps.GetExplosionArmorPenetration();

            GenExplosionCE.DoExplosion(
                center: cell,
                map: map,
                radius: amcProps.damageRadius,
                damType: damDef,
                instigator: projectile.launcher,
                damAmount: damAmount,
                armorPenetration: armorPen,
                explosionSound: null,
                weapon: projectile.equipmentDef,
                projectile: projectile.def,
                intendedTarget: null,
                postExplosionSpawnThingDef: amcProps.postExplosionSpawnThingDef,
                postExplosionSpawnChance: amcProps.postExplosionSpawnChance,
                postExplosionSpawnThingCount: amcProps.postExplosionSpawnThingCount,
                postExplosionGasType: amcProps.postExplosionGasType,
                applyDamageToExplosionCellsNeighbors: amcProps.applyDamageToExplosionCellsNeighbors,
                preExplosionSpawnThingDef: amcProps.preExplosionSpawnThingDef,
                preExplosionSpawnChance: amcProps.preExplosionSpawnChance,
                preExplosionSpawnThingCount: amcProps.preExplosionSpawnThingCount,
                chanceToStartFire: amcProps.explosionChanceToStartFire,
                damageFalloff: amcProps.explosionDamageFalloff,
                doVisualEffects: false,
                doSoundEffects: false,
                height: 0f
            );
        }
    }
}
