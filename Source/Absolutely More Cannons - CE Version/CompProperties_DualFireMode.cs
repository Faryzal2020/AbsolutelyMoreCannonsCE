using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// CompProperties for dual fire mode component.
    /// Attach this to turret buildings to enable fire mode switching.
    /// </summary>
    public class CompProperties_DualFireMode : CompProperties
    {
        public CompProperties_DualFireMode()
        {
            compClass = typeof(CompDualFireMode);
        }

        /// <summary>
        /// Gets the DualFireModeExtension from a ThingDef.
        /// </summary>
        public DualFireModeExtension GetEffectiveExtension(ThingDef def)
        {
            return def?.GetModExtension<DualFireModeExtension>();
        }
    }
}
