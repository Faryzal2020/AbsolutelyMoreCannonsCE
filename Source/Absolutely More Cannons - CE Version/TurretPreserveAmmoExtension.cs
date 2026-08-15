using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// XML ModExtension for turret ThingDefs.
    /// Specifies default Preserve Ammo configuration and permissions.
    /// </summary>
    public class TurretPreserveAmmoExtension : DefModExtension
    {
        /// <summary>
        /// Default state of Preserve Ammo when turret is constructed (default: true).
        /// </summary>
        public bool defaultPreserveAmmo = true;

        /// <summary>
        /// Whether the player is allowed to toggle Preserve Ammo via gizmo (default: true).
        /// </summary>
        public bool allowToggle = true;
    }
}
