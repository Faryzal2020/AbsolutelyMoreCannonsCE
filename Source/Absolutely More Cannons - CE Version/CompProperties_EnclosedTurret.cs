using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

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

        // ---------------- Open-top seat & gun shield ----------------

        /// <summary>
        /// Seat position in cells, in the turret-top-local frame (+Z along the barrel, +X to the barrel's right),
        /// rotating with the turret top. Unset (NaN sentinel) keeps the classic enclosed behaviour.
        /// RimWorld XML field parsing does not support Nullable, hence the sentinel.
        /// </summary>
        public Vector3 seatOffset = new Vector3(float.NaN, 0f, float.NaN);

        /// <summary>Total shield width in degrees, centred on the barrel. 0 = no shield, 360 = all round.</summary>
        public float shieldArc = 0f;

        /// <summary>Top edge of the shield in CE height units above ground (same units as barrelHeight; ~metres).</summary>
        public float shieldHeight = 0f;

        /// <summary>Sharp armour of the shield (CE mm RHA), compared with a round's sharp penetration.</summary>
        public float shieldArmor = 0f;


        public bool HasSeat => !float.IsNaN(seatOffset.x);

        public bool HasShield => HasSeat && shieldArc > 0f;

        public CompProperties_EnclosedTurret()
        {
            compClass = typeof(CompEnclosedTurret);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string e in base.ConfigErrors(parentDef))
            {
                yield return e;
            }
            if (HasSeat && hidePawnGraphics)
            {
                yield return "[AMC OpenTop] seatOffset is set but hidePawnGraphics is true; the gunner stays hidden.";
            }
            if (shieldArc > 0f && !HasSeat)
            {
                yield return "[AMC OpenTop] shield fields need seatOffset.";
            }
        }

        /// <summary>
        /// Calculates the protection factor (0.0 to 1.0) for a given DamageDef.
        /// </summary>
        public float GetProtectionFor(DamageDef damageDef)
        {
            switch (GetCategoryFor(damageDef, out float custom))
            {
                case ProtectionCategory.Custom: return custom;
                case ProtectionCategory.Flame: return flameProtection;
                case ProtectionCategory.Explosive: return explosiveProtection;
                case ProtectionCategory.Bullet: return bulletProtection;
                case ProtectionCategory.Melee: return meleeProtection;
                default: return generalProtection;
            }
        }

        /// <summary>
        /// Classifies a DamageDef into the protection category GetProtectionFor uses.
        /// <paramref name="customProtection"/> is set when a customProtections entry matches.
        /// </summary>
        public ProtectionCategory GetCategoryFor(DamageDef damageDef, out float customProtection)
        {
            customProtection = 0f;
            if (damageDef == null) return ProtectionCategory.General;

            // Check custom DamageDef list first
            if (customProtections != null && customProtections.Count > 0)
            {
                for (int i = 0; i < customProtections.Count; i++)
                {
                    if (customProtections[i] != null && customProtections[i].damageDef == damageDef)
                    {
                        customProtection = customProtections[i].protection;
                        return ProtectionCategory.Custom;
                    }
                }
            }

            string defName = damageDef.defName ?? "";

            // Flame / Burn
            if (damageDef == DamageDefOf.Flame || damageDef == DamageDefOf.Burn || defName.Contains("Flame") || defName.Contains("Burn"))
            {
                return ProtectionCategory.Flame;
            }

            // Explosive / Shrapnel / Fragment / Bomb
            if (damageDef == DamageDefOf.Bomb || damageDef.isExplosive || defName.Contains("Bomb") || defName.Contains("Fragment") || defName.Contains("Explosion") || defName.Contains("Shrapnel"))
            {
                return ProtectionCategory.Explosive;
            }

            // Bullet / Sharp
            if (damageDef == DamageDefOf.Bullet || (damageDef.armorCategory != null && damageDef.armorCategory.defName == "Sharp") || defName.Contains("Bullet") || defName.Contains("Arrow"))
            {
                return ProtectionCategory.Bullet;
            }

            // Melee / Blunt / Cut / Stab / Bite / Scratch
            if (damageDef == DamageDefOf.Blunt || damageDef == DamageDefOf.Cut || damageDef == DamageDefOf.Stab || damageDef == DamageDefOf.Bite || damageDef == DamageDefOf.Scratch || (damageDef.armorCategory != null && damageDef.armorCategory.defName == "Blunt"))
            {
                return ProtectionCategory.Melee;
            }

            return ProtectionCategory.General;
        }
    }

    /// <summary>Protection categories used by CompProperties_EnclosedTurret.GetProtectionFor.</summary>
    public enum ProtectionCategory
    {
        General,
        Custom,
        Flame,
        Explosive,
        Bullet,
        Melee,
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
