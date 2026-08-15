using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// ModExtension to boost indirect fire charge index by a configurable offset (default +1).
    /// Prevents low-velocity shallow arcs for airburst artillery without affecting other mods or vanilla turrets.
    /// </summary>
    public class TurretChargeBoostExtension : DefModExtension
    {
        /// <summary>
        /// How many charge levels/indexes to add to the baseline charge calculated by CE.
        /// Defaults to +1 (e.g. Charge 1 -> Charge 2). Clamped to max charge index available.
        /// </summary>
        public int chargeOffset = 1;

        /// <summary>
        /// Toggle whether this charge boost extension is active.
        /// </summary>
        public bool enabled = true;
    }
}
