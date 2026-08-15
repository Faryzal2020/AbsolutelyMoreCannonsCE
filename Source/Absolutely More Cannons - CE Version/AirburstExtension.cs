using Verse;

namespace AbsolutelyMoreCannons
{
    public enum AirburstType
    {
        Altitude,
        Flak
    }

    /// <summary>
    /// Configuration extension for Airburst projectiles in Combat Extended.
    /// Supports Altitude-based and Flak (Proximity)-based mid-air detonation.
    /// </summary>
    public class AirburstExtension : DefModExtension
    {
        /// <summary>
        /// Minimum flight ticks before the airburst fuse arms.
        /// Prevents premature detonation near launcher muzzle.
        /// </summary>
        public int armingTicks = 10;

        /// <summary>
        /// Type of airburst trigger (Altitude or Flak).
        /// </summary>
        public AirburstType type = AirburstType.Altitude;

        /// <summary>
        /// Altitude threshold in vertical meters (ExactPosition.y).
        /// Triggers when descending projectile reaches or falls below this height.
        /// </summary>
        public float burstAltitude = 3.0f;

        /// <summary>
        /// Detection radius (in cells/meters) for Flak (Proximity) mode.
        /// </summary>
        public float proximityRadius = 4.0f;
    }
}
