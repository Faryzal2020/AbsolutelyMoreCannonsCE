using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// XML ModExtension for turret ThingDefs.
    /// Enables continuous target tracking and per-shot leading during burst fire.
    /// </summary>
    public class TurretTrackingExtension : DefModExtension
    {
        /// <summary>
        /// Whether the turret continuously updates rotation and bullet lead mid-burst.
        /// </summary>
        public bool enableMidBurstTracking = true;
    }
}
