using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Optional XML ModExtension for per-turret overrides of "Prioritize Closest" target selection parameters.
    /// Global defaults are configured on CompProperties_TurretPrioritizeClosest in TurretBuilding_Base.xml.
    /// </summary>
    public class TurretPrioritizeClosestExtension : DefModExtension
    {
        public bool? defaultPrioritizeClosest = null;
        public float? commitmentSeconds = null;
        public float? visionConeDegrees = null;
        public float? coneExpandDegreesPerSecond = null;
        public float? traverseTimeWeight = null;
        public float? minSwitchAdvantageTiles = null;
        public bool? allowToggle = null;
    }
}
