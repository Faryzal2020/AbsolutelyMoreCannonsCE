using System;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using CombatExtended;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Configuration properties for throwing scattered spark visual flecks on projectile impact.
    /// </summary>
    public class CompProperties_ScatteredSparks : CompProperties
    {
        /// <summary>
        /// FleckDef to spawn for scattered sparks effect.
        /// Defaults to Fleck_SparkThrownFast or MicroSparksFast if null.
        /// </summary>
        public FleckDef fleckDef;

        /// <summary>
        /// Range of number of sparks to throw on impact (e.g. 5~15).
        /// </summary>
        public IntRange count = new IntRange(5, 10);

        /// <summary>
        /// Initial velocity speed of thrown sparks (e.g. 5.0~10.0).
        /// </summary>
        public FloatRange speed = new FloatRange(5f, 10f);

        /// <summary>
        /// Size / scale of the spark flecks (e.g. 0.2~0.4).
        /// </summary>
        public FloatRange scale = new FloatRange(0.2f, 0.4f);

        /// <summary>
        /// Lifespan in seconds of the thrown sparks before fading (e.g. 0.15~0.3).
        /// </summary>
        public FloatRange airTime = new FloatRange(0.15f, 0.3f);

        /// <summary>
        /// Angle cone spread in degrees relative to the impact vector (+/- spreadAngle). Default: 45.
        /// </summary>
        public float spreadAngle = 45f;

        /// <summary>
        /// If true, sparks are thrown backward away from impact point (ricochet direction).
        /// If false, sparks are thrown forward along trajectory. Default: true.
        /// </summary>
        public bool reverseDirection = true;

        /// <summary>
        /// Chance (0.0 to 1.0) of triggering the spark throw on impact. Default: 1.0.
        /// </summary>
        public float chance = 1.0f;

        public CompProperties_ScatteredSparks()
        {
            compClass = typeof(CompScatteredSparks);
        }
    }

    /// <summary>
    /// Component attached to projectiles that throws scattered spark visual flecks upon impact.
    /// </summary>
    public class CompScatteredSparks : ThingComp
    {
        public CompProperties_ScatteredSparks Props => (CompProperties_ScatteredSparks)props;

        public void OnImpact(Thing hitThing)
        {
            if (parent == null || parent.Map == null) return;
            Map map = parent.Map;

            if (Props.chance < 1.0f && Rand.Value > Props.chance)
                return;

            Vector3 loc = parent.DrawPos;
            float impactRotation = parent.Rotation.AsAngle;

            if (parent is ProjectileCE projCE)
            {
                loc = projCE.ExactPosition;
                impactRotation = projCE.shotRotation;
            }

            if (!loc.ShouldSpawnMotesAt(map))
                return;

            FleckDef fleck = Props.fleckDef 
                ?? DefDatabase<FleckDef>.GetNamed("Fleck_SparkThrownFast", false) 
                ?? FleckDefOf.MicroSparksFast;

            if (fleck == null) return;

            float baseAngle = Props.reverseDirection ? ((-impactRotation) % 360f) : (impactRotation % 360f);
            int sparkCount = Props.count.RandomInRange;

            Rand.PushState();
            FleckCreationData creationData = FleckMaker.GetDataStatic(loc, map, fleck);
            creationData.spawnPosition.y += 3.0f;

            for (int i = 0; i < sparkCount; i++)
            {
                creationData.velocityAngle = baseAngle + Rand.Range(-Props.spreadAngle, Props.spreadAngle);
                creationData.scale = Props.scale.RandomInRange;
                creationData.velocitySpeed = Props.speed.RandomInRange;
                creationData.spawnPosition = loc;
                creationData.spawnPosition.y += 3.0f;
                creationData.airTimeLeft = Props.airTime.RandomInRange;

                map.flecks.CreateFleck(creationData);
            }
            Rand.PopState();
        }
    }

    /// <summary>
    /// Harmony patch on ProjectileCE.Impact and Projectile.Impact to trigger CompScatteredSparks.
    /// </summary>
    [HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.Impact))]
    public static class Patch_ProjectileCE_Impact_ScatteredSparks
    {
        public static void Postfix(ProjectileCE __instance, Thing hitThing)
        {
            if (__instance == null) return;
            var comp = __instance.GetComp<CompScatteredSparks>();
            comp?.OnImpact(hitThing);
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Projectile_Impact_ScatteredSparks
    {
        public static void Postfix(Projectile __instance, Thing hitThing)
        {
            if (__instance == null) return;
            var comp = __instance.GetComp<CompScatteredSparks>();
            comp?.OnImpact(hitThing);
        }
    }
}
