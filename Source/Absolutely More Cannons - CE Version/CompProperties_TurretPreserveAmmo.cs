using Verse;

namespace AbsolutelyMoreCannons
{
    public class CompProperties_TurretPreserveAmmo : CompProperties
    {
        public bool defaultPreserveAmmo = true;

        public CompProperties_TurretPreserveAmmo()
        {
            this.compClass = typeof(CompTurretPreserveAmmo);
        }
    }
}
