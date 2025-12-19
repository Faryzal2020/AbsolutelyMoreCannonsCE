using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Comp properties for per-turret accuracy override
    /// Allows individual turrets to have custom sway/recoil/spread reduction values
    /// </summary>
    public class CompProperties_AccuracyOverride : CompProperties
    {
        /// <summary>
        /// Sway reduction multiplier (0.0 = no reduction, 1.0 = 100% reduction)
        /// </summary>
        public float swayReduction = 0f;
        
        /// <summary>
        /// Recoil reduction multiplier (0.0 = no reduction, 1.0 = 100% reduction)
        /// </summary>
        public float recoilReduction = 0f;
        
        /// <summary>
        /// Spread reduction multiplier (0.0 = no reduction, 1.0 = 100% reduction)
        /// </summary>
        public float spreadReduction = 0f;
        
        public CompProperties_AccuracyOverride()
        {
            compClass = typeof(Comp_AccuracyOverride);
        }
    }
    
    /// <summary>
    /// Comp for storing and retrieving per-turret accuracy overrides
    /// </summary>
    public class Comp_AccuracyOverride : ThingComp
    {
        public CompProperties_AccuracyOverride Props => (CompProperties_AccuracyOverride)props;
        
        /// <summary>
        /// Get the effective sway reduction for this turret
        /// Combines global slider with per-turret override
        /// </summary>
        public float GetSwayReduction()
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null) return Props.swayReduction * 100f;
            
            // Formula: Global + (PerTurret × (1 - Global/100))
            float globalPercent = settings.swayReductionPercent;
            float perTurretPercent = Props.swayReduction * 100f;
            
            return globalPercent + (perTurretPercent * (1f - globalPercent / 100f));
        }
        
        /// <summary>
        /// Get the effective recoil reduction for this turret
        /// </summary>
        public float GetRecoilReduction()
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null) return Props.recoilReduction * 100f;
            
            float globalPercent = settings.recoilReductionPercent;
            float perTurretPercent = Props.recoilReduction * 100f;
            
            return globalPercent + (perTurretPercent * (1f - globalPercent / 100f));
        }
        
        /// <summary>
        /// Get the effective spread reduction for this turret
        /// </summary>
        public float GetSpreadReduction()
        {
            var settings = TurretBarrelAnimationMod.settings;
            if (settings == null) return Props.spreadReduction * 100f;
            
            float globalPercent = settings.spreadReductionPercent;
            float perTurretPercent = Props.spreadReduction * 100f;
            
            return globalPercent + (perTurretPercent * (1f - globalPercent / 100f));
        }
        
        public override string CompInspectStringExtra()
        {
            // Check if user wants to see this info
            var settings = TurretBarrelAnimationMod.settings;
            if (settings != null && !settings.showAccuracyOverrideInspect)
            {
                return null;
            }
            
            if (Props.swayReduction <= 0f && Props.recoilReduction <= 0f && Props.spreadReduction <= 0f)
            {
                return null;
            }
            
            string result = "AMC Accuracy Overrides:\n";
            
            if (Props.swayReduction > 0f)
            {
                result += $"  Sway: {GetSwayReduction():F0}% reduction\n";
            }
            if (Props.recoilReduction > 0f)
            {
                result += $"  Recoil: {GetRecoilReduction():F0}% reduction\n";
            }
            if (Props.spreadReduction > 0f)
            {
                result += $"  Spread: {GetSpreadReduction():F0}% reduction";
            }
            
            return result.TrimEnd('\n');
        }
    }
}
