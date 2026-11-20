using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AbsolutelyMoreCannons
{
    public enum CapacitorCategory
    {
        Projectile,
        Fuel
    }

    public class CapacitorMaterialEntry
    {
        public ThingDef thingDef;
        public CapacitorCategory category = CapacitorCategory.Projectile;
        /// <summary>
        /// Substitution ratio relative to the category's base unit (e.g., Steel = 1.0, Plasteel = 4.0).
        /// If <= 0, this material cannot be substituted for or used as a substitute.
        /// </summary>
        public float equivalenceValue = 1.0f;
        /// <summary>
        /// Whether this material can be substituted by or substitute for other materials in the same category.
        /// </summary>
        public bool canSubstitute = true;
    }

    public class CapacitorUpgradeLevel
    {
        public string label;
        /// <summary>
        /// Crafting speed multiplier at this level (e.g., 1.25 = +25% faster crafting speed).
        /// </summary>
        public float speedMultiplier = 1.0f;
        /// <summary>
        /// Probability (0.0 to 1.0) on each completed batch to trigger a material rebate (e.g. 0.05 = 5% chance).
        /// </summary>
        public float rebateChance = 0.0f;
        /// <summary>
        /// Fraction of recipe material cost refunded into the internal capacitor when a rebate triggers (e.g. 0.10 = 10% of recipe cost returned).
        /// </summary>
        public float materialRebatePercent = 0.0f;
        /// <summary>
        /// Total internal capacitor storage multiplier at this upgrade level (e.g. 1.20 = +20% storage capacity).
        /// </summary>
        public float storageMultiplier = 1.0f;
        /// <summary>
        /// Total work ticks required by a colonist with sufficient Crafting skill after upgrade materials are delivered.
        /// </summary>
        public float workAmount = 2500f;
        /// <summary>
        /// Minimum Crafting skill level required for a colonist to work on this upgrade.
        /// </summary>
        public int minCraftingSkill = 6;
        /// <summary>
        /// Materials that colonists must haul to the machine to perform this upgrade.
        /// </summary>
        public List<ThingDefCountClass> costList = new List<ThingDefCountClass>();
    }

    public class CompProperties_AmmoCapacitor : CompProperties
    {
        /// <summary>
        /// Work ticks progressed per game tick when powered.
        /// CNCmachine base WorkTableWorkSpeedFactor is 4.0.
        /// </summary>
        public float workSpeedFactor = 4.0f;

        /// <summary>
        /// Offset relative to building position (rotated with building Rotation) where excess materials
        /// and crafted ammo are popped out. Falls back to interactionCell or any walkable adjacent cell.
        /// </summary>
        public IntVec3 outputCellOffset = new IntVec3(0, 0, -1);

        /// <summary>
        /// Default step size for the '+' and '-' buttons in the capacitor UI.
        /// </summary>
        public int defaultStepSize = 75;

        /// <summary>
        /// Base maximum combined material capacity across all internal capacitors before upgrade/module multipliers.
        /// </summary>
        public int baseMaxStorage = 3000;

        /// <summary>
        /// List of materials supported by the internal capacitors and their equivalence values.
        /// </summary>
        public List<CapacitorMaterialEntry> materials = new List<CapacitorMaterialEntry>();

        /// <summary>
        /// XML-defined upgrade levels (e.g. Level 1 to Level 4) providing speed multipliers and material rebates.
        /// </summary>
        public List<CapacitorUpgradeLevel> upgradeLevels = new List<CapacitorUpgradeLevel>();

        public CompProperties_AmmoCapacitor()
        {
            compClass = typeof(CompAmmoCapacitor);
        }
    }
}
