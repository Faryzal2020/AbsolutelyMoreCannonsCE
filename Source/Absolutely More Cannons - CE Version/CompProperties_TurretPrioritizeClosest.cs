using Verse;

namespace AbsolutelyMoreCannons
{
    public class CompProperties_TurretPrioritizeClosest : CompProperties
    {
        /// <summary>
        /// Whether "Prioritize Closest" is enabled by default on turrets without active Spray Discipline.
        /// </summary>
        public bool defaultPrioritizeClosest = true;

        /// <summary>
        /// Minimum duration in seconds that the turret must remain committed to a newly aligned target
        /// before it can switch to another target (unless the current target dies/downed/loses LoS).
        /// </summary>
        public float commitmentSeconds = 4.0f;

        /// <summary>
        /// Half-angle in degrees of the forward focus vision cone (e.g. 45 degrees = 90 degrees total forward arc).
        /// </summary>
        public float visionConeDegrees = 45.0f;

        /// <summary>
        /// Rate in degrees per second at which the half-angle vision cone widens when no target is present
        /// (e.g. +10 degrees half-angle/sec = +20 degrees total arc/sec, reaching 180 half-angle / 360 total).
        /// </summary>
        public float coneExpandDegreesPerSecond = 10.0f;

        /// <summary>
        /// Equivalent distance penalty in tiles per 1.0 second of estimated turret traverse/rotation time.
        /// Higher values make slow-rotating NonSnap turrets prefer targets closer to their current heading.
        /// </summary>
        public float traverseTimeWeight = 8.0f;

        /// <summary>
        /// Minimum effective cost improvement (in tile-equivalents) required to switch away from a living current target
        /// after the commitment timer expires. Prevents target jittering.
        /// </summary>
        public float minSwitchAdvantageTiles = 3.0f;

        /// <summary>
        /// Whether the player can toggle this mode via gizmo.
        /// </summary>
        public bool allowToggle = true;

        public CompProperties_TurretPrioritizeClosest()
        {
            compClass = typeof(CompTurretPrioritizeClosest);
        }
    }
}
