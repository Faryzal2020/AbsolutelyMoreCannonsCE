using Verse;

namespace AbsolutelyMoreCannons
{
    public class CompProperties_TurretSprayDiscipline : CompProperties
    {
        public bool defaultEnableSprayDiscipline = true;
        public int shotsPerTarget = 4;
        public float cycleConeDegrees = 10f;
        public bool allowToggle = true;

        public CompProperties_TurretSprayDiscipline()
        {
            compClass = typeof(CompTurretSprayDiscipline);
        }
    }
}
