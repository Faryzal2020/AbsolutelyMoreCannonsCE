using System;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// CompProperties for turret mode swapping.
    /// Defines the alternate turret def to swap to.
    /// </summary>
    public class CompProperties_TurretModeSwap : CompProperties
    {
        /// <summary>
        /// DefName of the alternate turret to swap to
        /// </summary>
        public string alternateDef;
        
        /// <summary>
        /// Label shown on the gizmo button
        /// </summary>
        public string gizmoLabel;
        
        /// <summary>
        /// Description shown on the gizmo tooltip
        /// </summary>
        public string gizmoDesc;
        
        /// <summary>
        /// Icon path for the gizmo (relative to Textures/)
        /// </summary>
        public string gizmoIcon;
        
        public CompProperties_TurretModeSwap()
        {
            compClass = typeof(CompTurretModeSwap);
        }
    }
}
