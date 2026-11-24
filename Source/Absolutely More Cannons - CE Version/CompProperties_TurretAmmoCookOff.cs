using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Properties for CompTurretAmmoCookOff.
    /// Allows turrets with internal magazines (CompAmmoUser) to risk an ammo cook-off explosion when damaged,
    /// scaling explosion radius and damage by current ammo fill ratio (100% radius & damage at 100% ammo).
    /// </summary>
    public class CompProperties_TurretAmmoCookOff : CompProperties
    {
        // === Explosion Scaling Settings ===

        /// <summary>
        /// Explosion radius (in cells) when the turret's internal magazine is 100% full.
        /// Scales down linearly with current ammo count / magazine size.
        /// </summary>
        public float maxExplosiveRadius = 6f;

        /// <summary>
        /// Minimum explosion radius clamp when cooking off with very few rounds left.
        /// </summary>
        public float minExplosiveRadius = 0.5f;

        /// <summary>
        /// Explosion damage when the turret's internal magazine is 100% full.
        /// Scales down linearly with current ammo count / magazine size.
        /// </summary>
        public int maxDamage = 150;

        /// <summary>
        /// Explosion armor penetration (-1 uses default calculation based on scaled damage).
        /// </summary>
        public float armorPenetration = -1f;

        /// <summary>
        /// DamageDef used for the cook-off explosion (defaults to DamageDefOf.Bomb).
        /// </summary>
        public DamageDef damageDef;

        /// <summary>
        /// Chance for the cook-off explosion to start fires in affected cells.
        /// </summary>
        public float chanceToStartFire = 0.35f;

        /// <summary>
        /// Whether explosion damage falls off from the center toward the outer radius.
        /// </summary>
        public bool damageFalloff = true;

        /// <summary>
        /// Minimum magazine capacity required for this turret to be considered as having an internal magazine.
        /// Prevents single-shot breech guns (magazineSize 1) or twin-barrel single-salvo breeches (magazineSize 2) from cooking off.
        /// </summary>
        public int minMagazineSize = 3;

        /// <summary>
        /// Minimum ammo fill ratio (0.0 to 1.0) required to trigger a cook-off.
        /// </summary>
        public float minAmmoRatio = 0.0f;

        /// <summary>
        /// Whether a magazine cook-off catastrophically destroys the turret.
        /// </summary>
        public bool destroyTurretOnCookOff = true;

        // === Trigger Probability & Damage Filtering Settings ===

        /// <summary>
        /// Turret must be at or below this HP percentage (0.0 to 1.0) after taking a non-lethal hit for cook-off to roll.
        /// </summary>
        public float hitPointsPercentThreshold = 0.85f;

        /// <summary>
        /// Base cook-off chance per hit from Fire / Burn / Heat damage (melting casing and armor).
        /// </summary>
        public float fireCookOffChance = 0.08f;

        /// <summary>
        /// Base cook-off chance rolled every 250 ticks (~4.1s) while the turret itself is actively burning on fire.
        /// </summary>
        public float burningTickCookOffChance = 0.04f;

        /// <summary>
        /// Sharp armor penetration (mm RHA in CE) below which bullets/fragments have only lowPenBulletCookOffChance.
        /// </summary>
        public float minPenForCookOff = 12f;

        /// <summary>
        /// Sharp armor penetration (mm RHA in CE) at which bullets reach full highPenBulletCookOffChance.
        /// </summary>
        public float highPenForCookOff = 45f;

        /// <summary>
        /// Cook-off chance for sharp projectiles whose penetration is below minPenForCookOff.
        /// </summary>
        public float lowPenBulletCookOffChance = 0.0005f;

        /// <summary>
        /// Cook-off chance for high-penetration projectiles at or above highPenForCookOff.
        /// </summary>
        public float highPenBulletCookOffChance = 0.08f;

        /// <summary>
        /// Cook-off chance for explosive/blast hits (Bomb, etc.) at referenceDamageForFullChance.
        /// </summary>
        public float explosiveCookOffChance = 0.04f;

        /// <summary>
        /// Reference damage dealt by a single hit to reach full damage factor scaling.
        /// </summary>
        public float referenceDamageForFullChance = 60f;

        /// <summary>
        /// Cook-off chance for blunt, melee, cut, stab, bite, or scratch damage (almost impossible).
        /// </summary>
        public float bluntCookOffChance = 0.0001f;

        /// <summary>
        /// Fallback cook-off chance for other harmful damage types.
        /// </summary>
        public float defaultCookOffChance = 0.001f;

        /// <summary>
        /// Multiplier applied to the cook-off chance when the hit is lethal (destroys the turret).
        /// </summary>
        public float lethalHitChanceMultiplier = 1.75f;

        /// <summary>
        /// Overall tuning multiplier for this turret's cook-off chance.
        /// </summary>
        public float cookOffChanceMultiplier = 1.0f;

        // === Naval Gun Base Ammo Rack Settings ===

        /// <summary>
        /// True for naval guns whose ammunition rack is located deep in the armored base structure rather than the upper gunhouse,
        /// making them significantly less prone to ammo cook-off.
        /// </summary>
        public bool hasBaseAmmoRack = false;

        /// <summary>
        /// Probability multiplier applied when hasBaseAmmoRack is true (default 0.35 = 65% less prone to cook-off).
        /// </summary>
        public float baseRackChanceMultiplier = 0.35f;

        /// <summary>
        /// Armor penetration threshold multiplier applied when hasBaseAmmoRack is true (default 1.5 = requires 50% more penetration).
        /// </summary>
        public float baseRackPenThresholdMultiplier = 1.5f;

        public CompProperties_TurretAmmoCookOff()
        {
            compClass = typeof(CompTurretAmmoCookOff);
        }

        /// <summary>
        /// Computes the probability (0.0 to 1.0) of an ammo cook-off for a specific incoming hit.
        /// </summary>
        public float GetCookOffChance(DamageInfo dinfo, float totalDamageDealt, float ammoRatio, float hpPercent, bool isLethal)
        {
            DamageDef def = dinfo.Def;
            if (def == null || !def.harmsHealth || totalDamageDealt <= 0f)
                return 0f;

            string defName = def.defName ?? "";

            // Exclude non-lethal/stun/EMP/extinguish/smoke explicitly
            if (def == DamageDefOf.EMP || def == DamageDefOf.Stun || def == DamageDefOf.Extinguish || def == DamageDefOf.Smoke ||
                defName.Contains("EMP") || defName.Contains("Stun") || defName.Contains("Beanbag") || defName.Contains("Rubber"))
            {
                return 0f;
            }

            float baseChance;

            // 1. Fire / Burn / Heat / Thermal (melting casing & cooking propellant)
            if (def == DamageDefOf.Flame || def == DamageDefOf.Burn ||
                (def.armorCategory != null && def.armorCategory.defName == "Heat") ||
                defName.Contains("Flame") || defName.Contains("Burn") || defName.Contains("Heat") ||
                defName.Contains("Thermal") || defName.Contains("Prometheum") || defName.Contains("Napalm"))
            {
                float dmgScale = Mathf.Clamp(totalDamageDealt / 15f, 0.35f, 1.5f);
                baseChance = fireCookOffChance * dmgScale;
            }
            // 2. Blunt / Melee / Cut / Stab / Animal Bite / Scratch (almost impossible)
            else if (def == DamageDefOf.Blunt || def == DamageDefOf.Cut || def == DamageDefOf.Stab ||
                     def == DamageDefOf.Bite || def == DamageDefOf.Scratch || def == DamageDefOf.Crush ||
                     (def.armorCategory != null && def.armorCategory.defName == "Blunt"))
            {
                baseChance = bluntCookOffChance;
            }
            // 3. High-Explosive / Bomb / Blast (excluding low-pen shrapnel/fragments which fall under sharp pen check)
            else if ((def == DamageDefOf.Bomb || def.isExplosive || defName.Contains("Bomb") || defName.Contains("Explosion") || defName.Contains("Thermobaric")) &&
                     !defName.Contains("Fragment") && !defName.Contains("Shrapnel"))
            {
                float dmgFactor = Mathf.Clamp(totalDamageDealt / Mathf.Max(1f, referenceDamageForFullChance), 0.2f, 1.5f);
                baseChance = explosiveCookOffChance * dmgFactor;
            }
            // 4. Bullets / AP Shells / Fragments / Sharp Projectiles (scaled by ArmorPenetrationInt)
            else if (def == DamageDefOf.Bullet ||
                     (def.armorCategory != null && def.armorCategory.defName == "Sharp") ||
                     defName.Contains("Bullet") || defName.Contains("Fragment") || defName.Contains("Shrapnel") || defName.Contains("Arrow"))
            {
                float penMultiplier = hasBaseAmmoRack ? baseRackPenThresholdMultiplier : 1f;
                float effectiveMinPen = minPenForCookOff * penMultiplier;
                float effectiveHighPen = Mathf.Max(effectiveMinPen + 1f, highPenForCookOff * penMultiplier);
                float incomingPen = dinfo.ArmorPenetrationInt;

                if (incomingPen < effectiveMinPen)
                {
                    baseChance = lowPenBulletCookOffChance;
                }
                else
                {
                    float penT = Mathf.InverseLerp(effectiveMinPen, effectiveHighPen, incomingPen);
                    baseChance = Mathf.Lerp(lowPenBulletCookOffChance, highPenBulletCookOffChance, penT);

                    // Bonus scaling for massive anti-tank penetrators well above effectiveHighPen
                    if (incomingPen > effectiveHighPen)
                    {
                        float overPenFactor = Mathf.Clamp(incomingPen / effectiveHighPen, 1f, 1.5f);
                        baseChance *= overPenFactor;
                    }
                }
            }
            else
            {
                baseChance = defaultCookOffChance;
            }

            if (baseChance <= 0f)
                return 0f;

            // Scale slightly by magazine fullness (a full rack presents a larger physical target than 1 round)
            float ammoTargetFactor = Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(ammoRatio));

            // Scale slightly by structural degradation (lower remaining HP = more compromised armor casing)
            float damageStateFactor = Mathf.Lerp(0.6f, 1.35f, 1f - Mathf.Clamp01(hpPercent));

            float finalChance = baseChance * ammoTargetFactor * damageStateFactor * cookOffChanceMultiplier;

            if (hasBaseAmmoRack)
            {
                finalChance *= baseRackChanceMultiplier;
            }

            if (isLethal)
            {
                finalChance *= lethalHitChanceMultiplier;
            }

            return Mathf.Clamp01(finalChance);
        }

        /// <summary>
        /// Computes the probability (0.0 to 1.0) of an ammo cook-off per 250-tick interval while the turret is actively burning.
        /// </summary>
        public float GetBurningTickCookOffChance(float ammoRatio, float hpPercent)
        {
            if (burningTickCookOffChance <= 0f)
                return 0f;

            float ammoTargetFactor = Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(ammoRatio));
            float damageStateFactor = Mathf.Lerp(0.5f, 1.5f, 1f - Mathf.Clamp01(hpPercent));
            float chance = burningTickCookOffChance * ammoTargetFactor * damageStateFactor * cookOffChanceMultiplier;

            if (hasBaseAmmoRack)
            {
                chance *= baseRackChanceMultiplier;
            }

            return Mathf.Clamp01(chance);
        }
    }
}
