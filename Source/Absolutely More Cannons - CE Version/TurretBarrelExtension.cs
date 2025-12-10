using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// DefModExtension for turret barrel animation configuration.
    /// This extension is added to turret building defs to enable barrel animation.
    /// </summary>
    public class TurretBarrelExtension : DefModExtension
    {
        /// <summary>
        /// The graphic data for the barrel component.
        /// </summary>
        public GraphicData barrelGraphic;

        /// <summary>
        /// Offset from turret center to barrel pivot point.
        /// </summary>
        public UnityEngine.Vector3 barrelOffset = UnityEngine.Vector3.zero;

        /// <summary>
        /// Size multiplier for barrel drawing.
        /// </summary>
        public float barrelDrawSize = 1f;

        /// <summary>
        /// Recoil animation settings.
        /// </summary>
        public RecoilAnimation recoilAnimation = new RecoilAnimation();

    /// <summary>
    /// Barrel spinning animation settings (texture-based, not rotation-based).
    /// </summary>
    public SpinningAnimation spinningAnimation = new SpinningAnimation();

        /// <summary>
        /// Firing animation settings.
        /// </summary>
        public FiringAnimation firingAnimation = new FiringAnimation();

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
        /// Number of barrels to draw. Default is 1.
        /// If greater than 1, barrels will be drawn side-by-side with spacing.
        /// </summary>
        public int barrelAmount = 1;

        /// <summary>
        /// Distance between barrels when multiple barrels are drawn.
        /// Measured in tiles.
        /// </summary>
        public float barrelSpacing = 1f;

        /// <summary>
        /// If true, barrels fire sequentially (one barrel animates per trigger, cycling through).
        /// If false, all barrels animate together on each trigger.
        /// </summary>
        public bool sequentialFiring = false;
    }

    /// <summary>
    /// Recoil animation configuration.
    /// </summary>
    public class RecoilAnimation
    {
        /// <summary>
        /// Maximum recoil distance.
        /// </summary>
        public float maxDistance = 0.2f;

        /// <summary>
        /// Duration in ticks to reach maximum recoil distance (backward motion).
        /// </summary>
        public int recoilDuration = 5;

        /// <summary>
        /// Duration in ticks to return from maximum distance to start position.
        /// </summary>
        public int returnDuration = 10;

        /// <summary>
        /// Whether to use a curve for recoil phase acceleration/deceleration.
        /// If true: rapid acceleration to max velocity, then deceleration to zero at maxDistance.
        /// If false: linear motion.
        /// </summary>
        public bool useRecoilCurve = true;

        /// <summary>
        /// Whether to use a curve for return phase acceleration/deceleration.
        /// If true: rapid acceleration from maxDistance, then deceleration to zero at start.
        /// If false: linear motion.
        /// </summary>
        public bool useReturnCurve = false;

        /// <summary>
        /// Legacy: Whether to use a curve (for backwards compatibility).
        /// Maps to useRecoilCurve and useReturnCurve.
        /// </summary>
        public bool curve
        {
            get => useRecoilCurve && useReturnCurve;
            set
            {
                useRecoilCurve = value;
                useReturnCurve = value;
            }
        }

        /// <summary>
        /// Whether recoil affects rotation.
        /// </summary>
        public bool affectsRotation = false;

        /// <summary>
        /// Maximum recoil angle in degrees (deprecated, kept for backwards compatibility).
        /// </summary>
        public float maxAngle = 2f;

        /// <summary>
        /// Total duration in ticks (recoilDuration + returnDuration).
        /// Calculated automatically.
        /// </summary>
        public int TotalDuration => recoilDuration + returnDuration;

        /// <summary>
        /// Legacy: Recoil duration in ticks (for backwards compatibility).
        /// Maps to TotalDuration.
        /// </summary>
        public int durationTicks
        {
            get => TotalDuration;
            set
            {
                // If set via old XML, split it proportionally
                recoilDuration = Mathf.RoundToInt(value * 0.2f); // 20% for recoil
                returnDuration = value - recoilDuration; // Rest for return
            }
        }

        /// <summary>
        /// Legacy: Recoil curve (for backwards compatibility).
        /// Generated automatically based on recoilDuration, returnDuration, and curve setting.
        /// </summary>
        public SimpleCurve recoilCurve
        {
            get
            {
                var totalTicks = TotalDuration;
                if (totalTicks <= 0) totalTicks = 1;
                
                float recoilProgress = (float)recoilDuration / totalTicks;
                
                var curve = new SimpleCurve();
                
                // Recoil phase: 0 to maxDistance
                curve.Add(new CurvePoint(0f, 0f));
                
                if (this.useRecoilCurve)
                {
                    // Non-linear velocity profile: rapid acceleration (almost instant) to max velocity,
                    // then deceleration to zero at maxDistance
                    // Using cubic ease-out for smooth deceleration
                    
                    // Very fast acceleration phase (first 10% of recoil duration)
                    float fastAccelProgress = recoilProgress * 0.1f;
                    float fastAccelDistance = 0.7f; // Reach 70% of max distance quickly
                    curve.Add(new CurvePoint(fastAccelProgress, fastAccelDistance));
                    
                    // Deceleration phase: ease-out cubic curve
                    // Cubic ease-out: 1 - (1-t)^3
                    for (float t = 0.1f; t <= 1f; t += 0.1f)
                    {
                        float progress = recoilProgress * t;
                        float easeOut = 1f - Mathf.Pow(1f - t, 3f); // Cubic ease-out
                        float distance = fastAccelDistance + (1f - fastAccelDistance) * easeOut;
                        curve.Add(new CurvePoint(progress, distance));
                    }
                }
                else
                {
                    // Linear motion for recoil phase
                    curve.Add(new CurvePoint(recoilProgress, 1f)); // Linear to max
                }
                
                // Return phase: maxDistance to 0
                float returnStart = recoilProgress;
                
                if (this.useReturnCurve)
                {
                    // Non-linear return: rapid acceleration, then deceleration
                    float returnFastAccelProgress = returnStart + (1f - recoilProgress) * 0.1f;
                    float returnFastAccelDistance = 0.3f; // From 100% to 30% quickly
                    curve.Add(new CurvePoint(returnFastAccelProgress, returnFastAccelDistance));
                    
                    // Return deceleration: ease-out cubic
                    for (float t = 0.1f; t <= 1f; t += 0.1f)
                    {
                        float progress = returnStart + (1f - recoilProgress) * t;
                        float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                        float distance = returnFastAccelDistance * (1f - easeOut); // From fastAccelDistance to 0
                        curve.Add(new CurvePoint(progress, distance));
                    }
                }
                else
                {
                    // Linear motion for return phase
                    curve.Add(new CurvePoint(1f, 0f)); // Linear return
                }
                
                curve.Add(new CurvePoint(1f, 0f)); // Fully returned
                
                return curve;
            }
            set { /* Ignore - curve is generated automatically */ }
        }
    }

    /// <summary>
    /// Spinning animation configuration for barrel spinning effects using texture frames.
    /// Requires Graphic_Collection with multiple frames showing different spin states.
    /// </summary>
    public class SpinningAnimation
    {
        /// <summary>
        /// Whether spinning animation is enabled.
        /// </summary>
        public bool enabled = false;

        /// <summary>
        /// Base spinning speed in frames per tick.
        /// </summary>
        public float baseSpeed = 0.1f;

        /// <summary>
        /// Speed multiplier when firing.
        /// </summary>
        public float firingMultiplier = 2f;

        /// <summary>
        /// Acceleration rate.
        /// </summary>
        public float acceleration = 0.05f;

        /// <summary>
        /// Deceleration rate (0-1, lower = faster deceleration).
        /// </summary>
        public float deceleration = 0.98f;

        /// <summary>
        /// Maximum spinning speed in frames per tick.
        /// </summary>
        public float maxSpeed = 1f;

        /// <summary>
        /// Minimum spinning speed to maintain animation (set to 0 to allow full stop).
        /// </summary>
        public float minSpeed = 0f;

        /// <summary>
        /// Whether to spin continuously even when not firing.
        /// </summary>
        public bool spinWhenIdle = false;
    }

    /// <summary>
    /// Firing animation configuration.
    /// </summary>
    public class FiringAnimation
    {
        /// <summary>
        /// Whether firing animation is enabled.
        /// </summary>
        public bool enabled = true;

        /// <summary>
        /// Animation duration in ticks.
        /// </summary>
        public int durationTicks = 5;

        /// <summary>
        /// Scale animation curve.
        /// </summary>
        public SimpleCurve scaleCurve;

        /// <summary>
        /// Position offset animation curve.
        /// </summary>
        public SimpleCurve positionOffsetCurve;

        /// <summary>
        /// Maximum scale multiplier.
        /// </summary>
        public float maxScale = 1.1f;

        /// <summary>
        /// Maximum position offset.
        /// </summary>
        public float maxPositionOffset = 0.05f;

        /// <summary>
        /// Whether to draw a flash/light effect when firing.
        /// </summary>
        public bool drawFlash = false;

        /// <summary>
        /// Flash color (R, G, B, A).
        /// </summary>
        public Color flashColor = new Color(1f, 0.8f, 0.4f, 1f); // Orange/yellow flash

        /// <summary>
        /// Flash size multiplier.
        /// </summary>
        public float flashSize = 1f;

        /// <summary>
        /// Flash brightness multiplier. Higher values = brighter flash.
        /// Default: 2.0 (doubles the brightness).
        /// </summary>
        public float flashBrightness = 2f;

        /// <summary>
        /// Flash intensity curve over time.
        /// </summary>
        public SimpleCurve flashIntensityCurve;

        /// <summary>
        /// Offset from barrel tip for flash position (in tiles).
        /// </summary>
        public float flashOffset = 0f;

        /// <summary>
        /// Optional EffecterDef name for muzzle flash light effect.
        /// If set, this effect will be spawned when firing, working parallel to the existing flash implementation.
        /// Leave empty/null to disable.
        /// </summary>
        public string muzzleFlashEffect = null;

        public FiringAnimation()
        {
            scaleCurve = new SimpleCurve
            {
                new CurvePoint(0f, 1f),
                new CurvePoint(0.5f, 1.1f),
                new CurvePoint(1f, 1f)
            };

            positionOffsetCurve = new SimpleCurve
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(0.3f, 0.05f),
                new CurvePoint(1f, 0f)
            };

            // Default flash intensity curve: bright at start, fade out quickly
            flashIntensityCurve = new SimpleCurve
            {
                new CurvePoint(0f, 1f),    // Full brightness at start
                new CurvePoint(0.2f, 0.8f), // Still bright
                new CurvePoint(0.5f, 0.3f), // Fading
                new CurvePoint(1f, 0f)      // Faded out
            };
        }
    }
}
