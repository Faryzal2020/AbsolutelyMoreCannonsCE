using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// DefModExtension for explosive projectiles to configure temporary ground crater and soot overlay decals on impact.
    /// Supports dynamic scaling based on explosionRadius and logarithmic damageAmountBase, separate XML scale multipliers,
    /// independent random rotation, terrain filtering (craters on dirt/sand only, soot on all solid ground),
    /// and rain-accelerated soot fading.
    /// </summary>
    public class ImpactGroundDecalExtension : DefModExtension
    {
        /// <summary>
        /// Whether to spawn the temporary medium crater texture on dirt/sand ground impacts.
        /// Set to false for HE shells smaller than 90mm.
        /// </summary>
        public bool spawnCrater = true;

        /// <summary>
        /// Whether to spawn the 50% opacity soot texture overlay on ground impacts.
        /// </summary>
        public bool spawnSoot = true;

        /// <summary>
        /// XML scale multiplier applied to the computed base scale for the crater graphic.
        /// </summary>
        public float craterScaleMultiplier = 1.0f;

        /// <summary>
        /// XML scale multiplier applied to the computed base scale for the soot overlay graphic.
        /// </summary>
        public float sootScaleMultiplier = 1.0f;

        /// <summary>
        /// Opacity of the crater texture before fade-out begins (default 1.0 = 100%).
        /// </summary>
        public float craterOpacity = 1.0f;

        /// <summary>
        /// Opacity of the soot overlay texture before fade-out begins (default 0.5 = 50%).
        /// </summary>
        public float sootOpacity = 0.5f;

        /// <summary>
        /// Ticks the crater remains at full opacity before starting to fade out (default 80,000 ticks).
        /// </summary>
        public int craterSolidTicks = 80000;

        /// <summary>
        /// Ticks it takes for the crater to fade out after craterSolidTicks expires (default 1,800 ticks = 30 seconds).
        /// </summary>
        public int craterFadeTicks = 1800;

        /// <summary>
        /// Ticks the soot remains at full opacity before starting to fade out (default 40,000 ticks = twice as fast as crater).
        /// </summary>
        public int sootSolidTicks = 40000;

        /// <summary>
        /// Ticks it takes for the soot to fade out after sootSolidTicks expires (default 900 ticks = 15 seconds, twice as fast as crater).
        /// </summary>
        public int sootFadeTicks = 900;

        /// <summary>
        /// Extra aging speed multiplier for soot per unit of RainRate when unroofed (default 2.0 -> 3x total speed at RainRate 1.0).
        /// </summary>
        public float rainSootFadeMultiplier = 2.0f;

        /// <summary>
        /// Multiplier on explosionRadius for the base decal size calculation.
        /// </summary>
        public float radiusWeight = 1.1f;

        /// <summary>
        /// Weight of the logarithmic damage bonus in the base decal size calculation.
        /// </summary>
        public float damageLogWeight = 0.6f;

        /// <summary>
        /// Maximum damage value considered for the logarithmic damage bonus (caps huge shells/nukes from over-scaling via damage).
        /// </summary>
        public float maxDamageForScaling = 1000f;

        /// <summary>
        /// Computes the base visual draw scale (in cells) from explosion radius and base damage.
        /// Primarily driven by explosionRadius, with a capped logarithmic contribution from damage.
        /// </summary>
        public float ComputeBaseScale(float explosionRadius, float damageAmount)
        {
            float radPart = Mathf.Max(0.25f, explosionRadius) * radiusWeight;
            float clampedDamage = Mathf.Clamp(damageAmount, 0f, maxDamageForScaling);
            float dmgPart = damageLogWeight * Mathf.Log(1f + (clampedDamage / 50f));
            return Mathf.Max(0.5f, radPart + dmgPart);
        }
    }
}
