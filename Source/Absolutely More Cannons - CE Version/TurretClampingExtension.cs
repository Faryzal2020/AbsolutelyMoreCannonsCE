using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// DefModExtension for turret ThingDefs or turret weapon ThingDefs to configure individual turret clamping parameters.
    /// Allows setting max vertical (elevation) deviation and max horizontal (rotation) deviation per turret in XML.
    /// </summary>
    public class TurretClampingExtension : DefModExtension
    {
        /// <summary>
        /// Maximum allowed vertical (elevation) deviation in degrees relative to target elevation angle.
        /// Value of -1 (or negative) disables vertical elevation clamping.
        /// </summary>
        public float maxVerticalDeviation = -1f;

        /// <summary>
        /// Maximum allowed horizontal (rotation) deviation in degrees relative to turret orientation.
        /// Value of -1 (or negative) disables horizontal rotation clamping.
        /// </summary>
        public float maxRotationDeviation = -1f;

        /// <summary>
        /// Alias for maxVerticalDeviation for intuitive XML naming.
        /// </summary>
        public float maxElevationDeviation = -1f;

        /// <summary>
        /// Alias for maxRotationDeviation for intuitive XML naming.
        /// </summary>
        public float maxHorizontalDeviation = -1f;

        /// <summary>
        /// Check if vertical elevation clamping is enabled for this turret.
        /// </summary>
        public bool HasVerticalClamping => EffectiveMaxVerticalDeviation >= 0f;

        /// <summary>
        /// Check if horizontal rotation clamping is enabled for this turret.
        /// </summary>
        public bool HasRotationClamping => EffectiveMaxRotationDeviation >= 0f;

        /// <summary>
        /// Get the effective maximum vertical deviation angle (degrees).
        /// </summary>
        public float EffectiveMaxVerticalDeviation
        {
            get
            {
                if (maxVerticalDeviation >= 0f) return maxVerticalDeviation;
                if (maxElevationDeviation >= 0f) return maxElevationDeviation;
                return -1f;
            }
        }

        /// <summary>
        /// Get the effective maximum horizontal rotation deviation angle (degrees).
        /// </summary>
        public float EffectiveMaxRotationDeviation
        {
            get
            {
                if (maxRotationDeviation >= 0f) return maxRotationDeviation;
                if (maxHorizontalDeviation >= 0f) return maxHorizontalDeviation;
                return -1f;
            }
        }
    }
}
