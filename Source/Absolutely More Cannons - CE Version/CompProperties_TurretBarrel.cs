using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component properties for turret barrel animation.
    /// This component handles the animation and drawing of turret barrels.
    /// </summary>
    public class CompProperties_TurretBarrel : CompProperties
    {
        /// <summary>
        /// The graphic data for the barrel component.
        /// If null, uses the extension from the building def.
        /// </summary>
        public GraphicData barrelGraphic;

        /// <summary>
        /// Offset from turret center to barrel pivot point.
        /// If zero, uses the extension from the building def.
        /// </summary>
        public UnityEngine.Vector3 barrelOffset = UnityEngine.Vector3.zero;

        /// <summary>
        /// Size multiplier for barrel drawing.
        /// </summary>
        public float barrelDrawSize = 1f;

        /// <summary>
        /// Recoil animation settings.
        /// If null, uses the extension from the building def.
        /// </summary>
        public RecoilAnimation recoilAnimation;

    /// <summary>
    /// Barrel spinning animation settings (texture-based).
    /// If null, uses the extension from the building def.
    /// </summary>
    public SpinningAnimation spinningAnimation;

        /// <summary>
        /// Firing animation settings.
        /// If null, uses the extension from the building def.
        /// </summary>
        public FiringAnimation firingAnimation;

        /// <summary>
        /// Layer offset for drawing order (higher values draw on top).
        /// Used when drawOnTop is false to draw above base but below turret top.
        /// </summary>
        public float drawLayerOffset = 0.05f;

        /// <summary>
        /// Whether to draw the barrel on top of the turret top graphics.
        /// If true, barrel will be drawn after turret top via Harmony patch.
        /// If false, barrel will be drawn below turret top but above base using Y offset.
        /// </summary>
        public bool drawOnTop = false;

        /// <summary>
        /// Whether the barrel should be drawn when the turret is destroyed.
        /// </summary>
        public bool drawWhenDestroyed = true;

        /// <summary>
        /// Whether to use the turret's rotation for barrel rotation.
        /// </summary>
        public bool inheritTurretRotation = true;

        /// <summary>
        /// Optional charge boost offset for indirect fire artillery (+1 to charge index up to max charge).
        /// </summary>
        public int chargeBoostOffset = 0;

        public CompProperties_TurretBarrel()
        {
            compClass = typeof(CompTurretBarrel);
        }

        /// <summary>
        /// List of selectable RPM values for the turret gizmo.
        /// </summary>
        public List<float> maxRPMs = new List<float>();

        /// <summary>
        /// List of selectable burst counts for the turret gizmo.
        /// </summary>
        public List<int> selectableBurstCounts = new List<int>();

        /// <summary>
        /// Gets the effective turret barrel extension from either this component or the building def.
        /// </summary>
        public TurretBarrelExtension GetEffectiveExtension(ThingDef buildingDef)
        {
            var extension = buildingDef.GetModExtension<TurretBarrelExtension>();
            if (extension == null)
            {
                // Create a default extension if none exists
                extension = new TurretBarrelExtension();
            }

            // Override with component properties if they are set
            if (barrelGraphic != null)
                extension.barrelGraphic = barrelGraphic;
            if (barrelOffset != UnityEngine.Vector3.zero)
                extension.barrelOffset = barrelOffset;
            if (barrelDrawSize != 1f)
                extension.barrelDrawSize = barrelDrawSize;
            if (recoilAnimation != null)
                extension.recoilAnimation = recoilAnimation;
            if (spinningAnimation != null)
                extension.spinningAnimation = spinningAnimation;
            if (firingAnimation != null)
                extension.firingAnimation = firingAnimation;
            if (drawLayerOffset != 0.05f)
                extension.drawLayerOffset = drawLayerOffset;
            if (drawOnTop != false)
                extension.drawOnTop = drawOnTop;
            if (drawWhenDestroyed != true)
                extension.drawWhenDestroyed = drawWhenDestroyed;
            if (inheritTurretRotation != true)
                extension.inheritTurretRotation = inheritTurretRotation;

            if (maxRPMs != null && maxRPMs.Count > 0)
                extension.maxRPMs = maxRPMs;
            if (selectableBurstCounts != null && selectableBurstCounts.Count > 0)
                extension.selectableBurstCounts = selectableBurstCounts;

            return extension;
        }
    }
}
