using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;

namespace AbsolutelyMoreCannons
{
    public enum AmmoCaliberTier
    {
        Small,      // < 20mm (rifle, pistol, HMG, shotgun, etc.)
        Medium,     // 20mm ~ 130mm (autocannons, tank guns, light/medium artillery)
        Heavy,      // > 130mm (heavy artillery, naval siege guns, rockets/grenades without mm)
        Unknown
    }

    /// <summary>
    /// XML DefModExtension attached to Fabricator Module items (e.g. AMC_Module_MediumCaliberDie).
    /// Defines crafting speed multipliers for matching caliber tiers (or specific categories) and non-matching shells.
    /// </summary>
    public class FabricatorModuleExtension : DefModExtension
    {
        /// <summary>
        /// Target caliber tier that receives the focused speed multiplier.
        /// </summary>
        public AmmoCaliberTier targetCaliberTier = AmmoCaliberTier.Medium;

        /// <summary>
        /// Minimum diameter in millimeters (inclusive) considered part of the target caliber tier.
        /// Default for Medium Caliber: 20mm (e.g. 20x102mm, 20x138mm, 25x137mm, 30mm, 40mm, 57mm, 75mm, 76mm, 88mm, 90mm, 100mm, 105mm, 120mm, 122mm, 127mm, 128mm).
        /// </summary>
        public float minCaliberMm = 20f;

        /// <summary>
        /// Maximum diameter in millimeters (inclusive) considered part of the target caliber tier.
        /// Default for Medium Caliber: 130mm (above 130mm like 150mm, 152mm, 155mm, 203mm, 233mm, 406mm, 600mm is Heavy).
        /// </summary>
        public float maxCaliberMm = 130f;

        /// <summary>
        /// Optional explicit ThingCategoryDefs that always count as matching (if any).
        /// </summary>
        public List<ThingCategoryDef> matchedCategories = new List<ThingCategoryDef>();

        /// <summary>
        /// Crafting speed multiplier for ammunition that matches the module's focus (e.g. 1.5 = +50% faster).
        /// </summary>
        public float matchedSpeedMultiplier = 1.5f;

        /// <summary>
        /// Crafting speed multiplier for all other ammunition (e.g. 0.8 = -20% slower).
        /// </summary>
        public float otherSpeedMultiplier = 0.8f;

        /// <summary>
        /// Optional extra probability (0.0 to 1.0) to trigger a material rebate while this module is installed (0.0 = none).
        /// </summary>
        public float extraRebateChance = 0.0f;

        /// <summary>
        /// Optional extra material rebate return percentage while this module is installed (0.0 = none).
        /// </summary>
        public float extraRebatePercent = 0.0f;

        /// <summary>
        /// Multiplier or bonus applied to total internal capacitor storage capacity while installed (e.g. 1.5 = +50% storage).
        /// </summary>
        public float storageMultiplier = 1.0f;

        // Regex matching "20x138mm", "105mm", "600mm", "8.8cm", "60cm", "12.8cm", ".50", etc.
        private static readonly Regex MmRegex = new Regex(@"(?<![\d.])(\d+(?:\.\d+)?)\s*(?:x\d+(?:\.\d+)?)?\s*mm\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex CmRegex = new Regex(@"(?<![\d.])(\d+(?:\.\d+)?)\s*cm\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EmbeddedMmRegex = new Regex(@"Ammo_?(\d+(?:\.\d+)?)(?:x\d+)?mm", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PounderRegex = new Regex(@"\b6[\s-]*pounder\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<ThingDef, float> cachedCaliberMm = new Dictionary<ThingDef, float>();

        public bool MatchesAmmo(ThingDef ammoDef)
        {
            if (ammoDef == null) return false;

            if (matchedCategories != null && matchedCategories.Count > 0 && ammoDef.thingCategories != null)
            {
                for (int i = 0; i < matchedCategories.Count; i++)
                {
                    ThingCategoryDef cat = matchedCategories[i];
                    if (cat != null && (ammoDef.thingCategories.Contains(cat) || cat.ContainedInThisOrDescendant(ammoDef)))
                    {
                        return true;
                    }
                }
            }

            float mm = DetectCaliberMm(ammoDef);
            if (mm > 0f)
            {
                return mm >= minCaliberMm && mm <= maxCaliberMm;
            }

            // Fallback heuristic if no millimeter/centimeter number is in defName or label:
            // Medium shells in CE/AMC typically weigh between 0.25 kg (20mm) and 35 kg (128mm).
            float mass = ammoDef.BaseMass;
            if (targetCaliberTier == AmmoCaliberTier.Medium)
            {
                return mass >= 0.20f && mass <= 35f;
            }
            else if (targetCaliberTier == AmmoCaliberTier.Heavy)
            {
                return mass > 35f;
            }
            else if (targetCaliberTier == AmmoCaliberTier.Small)
            {
                return mass > 0f && mass < 0.20f;
            }

            return false;
        }

        public float GetSpeedMultiplierFor(ThingDef ammoDef)
        {
            return MatchesAmmo(ammoDef) ? matchedSpeedMultiplier : otherSpeedMultiplier;
        }

        /// <summary>
        /// Parses caliber in millimeters from an AmmoDef's defName, label, or category label.
        /// Works across both AMC and Combat Extended ammunition.
        /// </summary>
        public static float DetectCaliberMm(ThingDef ammoDef)
        {
            if (ammoDef == null) return -1f;
            if (cachedCaliberMm.TryGetValue(ammoDef, out float cached))
                return cached;

            float result = -1f;
            string defName = ammoDef.defName ?? "";
            string label = ammoDef.label ?? "";
            string catLabel = (ammoDef.thingCategories != null && ammoDef.thingCategories.Count > 0)
                ? (ammoDef.thingCategories[0].label ?? "")
                : "";

            // 1. Check embedded defName pattern like Ammo_105mmL7A3_HEAT or Ammo_20x138mm_HE
            Match m = EmbeddedMmRegex.Match(defName);
            if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
            {
                result = val;
            }

            // 2. Check explicit "mm" in label, defName, or category label
            if (result <= 0f)
            {
                m = MmRegex.Match(label);
                if (!m.Success) m = MmRegex.Match(defName);
                if (!m.Success) m = MmRegex.Match(catLabel);
                if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                {
                    result = val;
                }
            }

            // 3. Check explicit "cm" (e.g. "8.8cm", "12.8cm", "60cm") -> multiply by 10
            if (result <= 0f)
            {
                m = CmRegex.Match(label);
                if (!m.Success) m = CmRegex.Match(catLabel);
                if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                {
                    result = val * 10f;
                }
            }

            // 4. Special British pounder naming (e.g. OQF 6-pounder = 57mm)
            if (result <= 0f && (PounderRegex.IsMatch(label) || PounderRegex.IsMatch(catLabel)))
            {
                result = 57f;
            }

            cachedCaliberMm[ammoDef] = result;
            return result;
        }
    }
}
