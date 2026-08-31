using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Configuration properties for transferring turret operator target search line-of-sight origin
    /// to the turret center and bypassing self-occlusion from tall turret buildings (e.g. fillPercent >= 1.0 or 2.0).
    /// </summary>
    public class CompProperties_TurretViewTransfer : CompProperties
    {
        /// <summary>
        /// Whether target searcher line-of-sight origin is transferred from the operator's interaction cell
        /// to the center of the turret building (default: true).
        /// </summary>
        public bool transferViewOrigin = true;

        /// <summary>
        /// Whether the turret's own building footprint and fillPercent are ignored during line-of-sight checks
        /// so the turret structure does not block its own operator's view (default: true).
        /// </summary>
        public bool ignoreSelfOcclusion = true;

        public CompProperties_TurretViewTransfer()
        {
            compClass = typeof(CompTurretViewTransfer);
        }
    }

    /// <summary>
    /// ThingComp enabling view origin transfer and self-occlusion bypass for turrets.
    /// </summary>
    public class CompTurretViewTransfer : ThingComp
    {
        public CompProperties_TurretViewTransfer Props => (CompProperties_TurretViewTransfer)props;
    }
}
