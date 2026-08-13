using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// XML ModExtension for FCS (Fire Control System) item defs.
    /// Specifies performance multipliers applied to unmanned turrets when loaded.
    /// </summary>
    public class FCSPropertiesDefExtension : DefModExtension
    {
        /// <summary>
        /// Sway multiplier (1.0 = normal, 0.5 = 50% sway, etc.)
        /// </summary>
        public float swayMultiplier = 1.0f;

        /// <summary>
        /// Recoil multiplier (1.0 = normal, 0.5 = 50% recoil, etc.)
        /// </summary>
        public float recoilMultiplier = 1.0f;

        /// <summary>
        /// Spread multiplier (1.0 = normal, 0.5 = 50% spread/dispersion, etc.)
        /// </summary>
        public float spreadMultiplier = 1.0f;

        /// <summary>
        /// Aim time multiplier (1.0 = normal, 0.8 = 20% faster aim/warmup)
        /// </summary>
        public float aimTimeMultiplier = 1.0f;

        /// <summary>
        /// Range multiplier (1.0 = normal, 1.1 = 10% extra range)
        /// </summary>
        public float rangeMultiplier = 1.0f;
    }
}
