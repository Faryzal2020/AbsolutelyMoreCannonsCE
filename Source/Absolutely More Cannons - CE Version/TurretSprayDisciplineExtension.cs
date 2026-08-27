using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// XML ModExtension for turret ThingDefs.
    /// Configures dynamic mid-burst target chaining (spray discipline).
    /// </summary>
    public class TurretSprayDisciplineExtension : DefModExtension
    {
        /// <summary>
        /// Whether spray discipline is enabled by default for this turret.
        /// </summary>
        public bool defaultEnableSprayDiscipline = true;

        /// <summary>
        /// Number of rounds fired at a single target before cycling to the next target in the cone.
        /// </summary>
        public int shotsPerTarget = 4;

        /// <summary>
        /// Maximum angle in degrees (left/right deviation from turret center/facing) for target cone cycling.
        /// </summary>
        public float cycleConeDegrees = 10f;

        /// <summary>
        /// Whether the player can toggle spray discipline ON/OFF via UI gizmo.
        /// </summary>
        public bool allowToggle = true;
    }
}
