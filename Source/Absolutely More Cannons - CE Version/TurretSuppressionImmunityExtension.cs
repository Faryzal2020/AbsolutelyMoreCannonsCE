using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// XML ModExtension for manned turret ThingDefs.
    /// Prevents operators from being suppressed and fleeing/hunkering while manning this turret.
    /// </summary>
    public class TurretSuppressionImmunityExtension : DefModExtension
    {
        /// <summary>
        /// Whether the pawn manning this turret is immune to suppression reactions (default: true).
        /// </summary>
        public bool preventOperatorSuppression = true;
    }
}
