using System.Collections.Generic;
using Verse;
using RimWorld;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Configuration properties for enclosed manned turrets.
    /// Manages fine-grained damage protection factors, fire, temperature, and visual settings.
    /// Protection values range from 0.0 (0% protection / full damage) to 1.0 (100% protection / total immunity).
    /// </summary>
    public class CompProperties_EnclosedTurret : CompProperties
    {
        /// <summary>
        /// Base protection factor for unspecified damage types (0.0 to 1.0, default: 1.0).
        /// </summary>
        public float generalProtection = 1.0f;

        /// <summary>
        /// Protection factor against bullets, high-velocity projectiles, and sharp attacks (0.0 to 1.0, default: 1.0).
        /// </summary>
        public float bulletProtection = 1.0f;

        /// <summary>
        /// Protection factor against bombs, shrapnel, fragmentation, and explosions (0.0 to 1.0, default: 1.0).
        /// </summary>
        public float explosiveProtection = 1.0f;

        /// <summary>
        /// Protection factor against flames and burns (0.0 to 1.0, default: 1.0). When 1.0, pawn cannot catch fire.
        /// </summary>
        public float flameProtection = 1.0f;

        /// <summary>
        /// Protection factor against blunt, cut, stab, and melee attacks (0.0 to 1.0, default: 1.0).
        /// </summary>
        public float meleeProtection = 1.0f;

        /// <summary>
        /// Protection factor against extreme heat, cold, hypothermia, and heatstroke (0.0 to 1.0, default: 1.0).
        /// When 1.0, pawn's ambient temperature evaluates to comfortable 21°C.
        /// </summary>
        public float temperatureProtection = 1.0f;

        /// <summary>
        /// Custom per-DamageDef protection overrides.
        /// </summary>
        public List<CustomDamageProtection> customProtections;

        /// <summary>
        /// Whether the operator's physical pawn graphic is hidden while manning (default: true).
        /// Set to false for open-topped turrets or gun mounts so the pawn remains visible at the turret center.
        /// </summary>
        public bool hidePawnGraphics = true;

        /// <summary>
        /// Whether the operator's draw position, name label, and selection indicators are centered on the turret (default: true).
        /// </summary>
        public bool centerPawnPosition = true;

        /// <summary>
        /// Legacy alias for centerPawnPosition.
        /// </summary>
        public bool centerPawnName = true;

        /// <summary>
        /// Label for the dismount gizmo button.
        /// </summary>
        public string dismountGizmoLabel = "Dismount Turret";

        /// <summary>
        /// Tooltip description for the dismount gizmo button.
        /// </summary>
        public string dismountGizmoDesc = "Orders the operator to come out from the turret and clears their prioritized work.";

        /// <summary>
        /// Icon path for the dismount gizmo button (relative to Textures/).
        /// </summary>
        public string dismountGizmoIcon = "UI/Commands/Deselect";

        public CompProperties_EnclosedTurret()
        {
            compClass = typeof(CompEnclosedTurret);
        }

        /// <summary>
        /// Calculates the protection factor (0.0 to 1.0) for a given DamageDef.
        /// </summary>
        public float GetProtectionFor(DamageDef damageDef)
        {
            if (damageDef == null) return generalProtection;

            // Check custom DamageDef list first
            if (customProtections != null && customProtections.Count > 0)
            {
                for (int i = 0; i < customProtections.Count; i++)
                {
                    if (customProtections[i] != null && customProtections[i].damageDef == damageDef)
                    {
                        return customProtections[i].protection;
                    }
                }
            }

            string defName = damageDef.defName ?? "";

            // Flame / Burn
            if (damageDef == DamageDefOf.Flame || damageDef == DamageDefOf.Burn || defName.Contains("Flame") || defName.Contains("Burn"))
            {
                return flameProtection;
            }

            // Explosive / Shrapnel / Fragment / Bomb
            if (damageDef == DamageDefOf.Bomb || damageDef.isExplosive || defName.Contains("Bomb") || defName.Contains("Fragment") || defName.Contains("Explosion") || defName.Contains("Shrapnel"))
            {
                return explosiveProtection;
            }

            // Bullet / Sharp
            if (damageDef == DamageDefOf.Bullet || (damageDef.armorCategory != null && damageDef.armorCategory.defName == "Sharp") || defName.Contains("Bullet") || defName.Contains("Arrow"))
            {
                return bulletProtection;
            }

            // Melee / Blunt / Cut / Stab / Bite / Scratch
            if (damageDef == DamageDefOf.Blunt || damageDef == DamageDefOf.Cut || damageDef == DamageDefOf.Stab || damageDef == DamageDefOf.Bite || damageDef == DamageDefOf.Scratch || (damageDef.armorCategory != null && damageDef.armorCategory.defName == "Blunt"))
            {
                return meleeProtection;
            }

            return generalProtection;
        }
    }

    /// <summary>
    /// Pair matching a specific DamageDef to a protection factor (0.0 to 1.0).
    /// </summary>
    public class CustomDamageProtection
    {
        public DamageDef damageDef;
        public float protection = 1.0f;
    }
}
