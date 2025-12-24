using System;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Component properties for turret smoke effects system.
    /// Defines configuration for three smoke types: muzzle, heat, and shockwave.
    /// </summary>
    public class CompProperties_TurretSmoker : CompProperties
    {
        public CompProperties_TurretSmoker()
        {
            this.compClass = typeof(CompTurretSmoker);
        }

        // === MUZZLE SMOKE ===
        // Directional smoke puff that appears when gun fires
        
        /// <summary>Enable/disable muzzle smoke</summary>
        public bool muzzleEnabled = false;
        
        /// <summary>FleckDef to spawn for muzzle smoke</summary>
        public FleckDef muzzleFleckDef = null;
        
        /// <summary>Number of particles per shot</summary>
        public int muzzleParticleCount = 3;
        
        /// <summary>Time in ticks to spawn all particles (density control)</summary>
        public int muzzleSpawnDuration = 5;
        
        /// <summary>Vector3 offset from barrel tip</summary>
        public Vector3 muzzleOffset = Vector3.zero;
        
        /// <summary>Maximum forward velocity when first spawned</summary>
        public float muzzleVelocity = 2.0f;
        
        /// <summary>Ticks for directional motion before transitioning to default wind motion</summary>
        public int muzzleVelDuration = 20;
        
        /// <summary>Scale multiplier for particles (supports "min~max" for random range)</summary>
        public string muzzleParticleSize = "1.0";
        
        /// <summary>Direction spread cone in degrees (±random(0, cone) from turret direction)</summary>
        public float directionCone = 0f;
        
        /// <summary>Ticks to wait after TryCastShot before spawning (sync with flash)</summary>
        public int muzzleSpawnDelay = 2;
        
        // Parsed values
        private float muzzleParticleSizeMin = 1.0f;
        private float muzzleParticleSizeMax = 1.0f;
        
        /// <summary>
        /// Get randomized particle size based on configured range
        /// </summary>
        public float GetRandomParticleSize()
        {
            if (muzzleParticleSizeMin == muzzleParticleSizeMax)
                return muzzleParticleSizeMin;
            return Rand.Range(muzzleParticleSizeMin, muzzleParticleSizeMax);
        }

        // === HEAT SMOKE ===
        // Continuous smoke emission from barrel when gun is hot
        
        /// <summary>Enable/disable heat smoke</summary>
        public bool heatEnabled = false;
        
        /// <summary>FleckDef for heat smoke</summary>
        public FleckDef heatFleckDef = null;
        
        /// <summary>Burst count threshold before heat smoke activates</summary>
        public int heatThreshold = 4;
        
        /// <summary>Heat (burst count) reduction per second</summary>
        public float heatDecayRate = 0.5f;
        
        /// <summary>Vector3 offset from turret center for heat smoke spawn</summary>
        public Vector3 heatOffset = Vector3.zero;
        
        /// <summary>Particles per second when heat smoke is active</summary>
        public float heatEmissionRate = 2.0f;
        
        /// <summary>Scale multiplier for heat smoke particles</summary>
        public float heatParticleSize = 1.0f;
        
        /// <summary>Number of emission points along barrel</summary>
        public int heatEmissionPoints = 1;
        
        /// <summary>Distance between emission points in cells</summary>
        public float heatEmissionSpacing = 0.5f;
        
        /// <summary>Flat increase to heatDecayRate per unit of heat above threshold</summary>
        public float decayIncrease = 0f;
        
        /// <summary>Flat increase to heatEmissionRate per unit of heat above threshold</summary>
        public float emissionIncrease = 0f;

        // === SHOCKWAVE SMOKE ===
        // Radial ground burst around muzzle from firing shockwave
        
        /// <summary>Enable/disable shockwave smoke</summary>
        public bool shockwaveEnabled = false;
        
        /// <summary>FleckDef for shockwave smoke</summary>
        public FleckDef shockwaveFleckDef = null;
        
        /// <summary>Ring radius in cells</summary>
        public float shockwaveRadius = 1.5f;
        
        /// <summary>Particle count within radius (distributed evenly)</summary>
        public int shockwaveDensity = 8;
        
        /// <summary>Vertical offset from ground</summary>
        public Vector3 shockwaveOffset = Vector3.zero;
        
        /// <summary>Scale multiplier for shockwave particles</summary>
        public float shockwaveParticleSize = 1.0f;

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            
            // Parse muzzle particle size - supports "min~max" format
            if (!string.IsNullOrEmpty(muzzleParticleSize))
            {
                if (muzzleParticleSize.Contains("~"))
                {
                    string[] parts = muzzleParticleSize.Split('~');
                    if (parts.Length == 2 && 
                        float.TryParse(parts[0].Trim(), out float min) && 
                        float.TryParse(parts[1].Trim(), out float max))
                    {
                        muzzleParticleSizeMin = min;
                        muzzleParticleSizeMax = max;
                    }
                    else
                    {
                        Log.Warning($"[AMC] Invalid muzzleParticleSize format '{muzzleParticleSize}' for {parentDef?.defName}. Using default 1.0");
                        muzzleParticleSizeMin = muzzleParticleSizeMax = 1.0f;
                    }
                }
                else
                {
                    if (float.TryParse(muzzleParticleSize.Trim(), out float size))
                    {
                        muzzleParticleSizeMin = muzzleParticleSizeMax = size;
                    }
                    else
                    {
                        Log.Warning($"[AMC] Invalid muzzleParticleSize '{muzzleParticleSize}' for {parentDef?.defName}. Using default 1.0");
                        muzzleParticleSizeMin = muzzleParticleSizeMax = 1.0f;
                    }
                }
            }
            
            // Set default fleck defs if not specified
            if (muzzleEnabled && muzzleFleckDef == null)
            {
                muzzleFleckDef = RimWorld.FleckDefOf.Smoke;
            }
            if (heatEnabled && heatFleckDef == null)
            {
                heatFleckDef = RimWorld.FleckDefOf.Smoke;
            }
            if (shockwaveEnabled && shockwaveFleckDef == null)
            {
                shockwaveFleckDef = RimWorld.FleckDefOf.Smoke;
            }
        }
    }
}
