using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Data structure for tracking individual muzzle smoke particles during their velocity phase.
    /// After velDuration expires, particles transition to default wind-based motion.
    /// </summary>
    public class MuzzleSmokeParticle
    {
        /// <summary>Current world position of particle</summary>
        public Vector3 position;
        
        /// <summary>Normalized direction vector (turret facing when spawned)</summary>
        public Vector3 direction;
        
        /// <summary>Maximum forward velocity when first spawned</summary>
        public float maxVelocity;
        
        /// <summary>Total ticks for directional motion before transitioning to default</summary>
        public int velDuration;
        
        /// <summary>Ticks this particle has been alive</summary>
        public int ticksAlive;
        
        /// <summary>FleckDef to spawn</summary>
        public FleckDef fleckDef;
        
        /// <summary>Particle size scale</summary>
        public float size;
        
        /// <summary>Rotation rate in degrees per tick</summary>
        public float rotationRate;

        /// <summary>
        /// Calculate current velocity using cubic deceleration curve.
        /// Curve: v = v0 * (1 - t/T)^3 where t = ticksAlive, T = velDuration
        /// This provides smooth cubic ease-out deceleration to zero.
        /// </summary>
        public float GetCurrentVelocity()
        {
            if (ticksAlive >= velDuration)
                return 0f;
            
            float progress = (float)ticksAlive / velDuration;  // 0.0 to 1.0
            float velocityMultiplier = Mathf.Pow(1f - progress, 3f);  // Cubic ease-out
            return maxVelocity * velocityMultiplier;
        }

        /// <summary>
        /// Returns true if particle should transition to default wind motion
        /// </summary>
        public bool ShouldTransition()
        {
            return ticksAlive >= velDuration;
        }
    }
}
