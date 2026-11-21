using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// CompProperties for <see cref="CompFabricatorThoughts"/>, controlling bubble pacing,
    /// world-space anchoring, and situational thresholds.
    /// </summary>
    public class CompProperties_FabricatorThoughts : CompProperties
    {
        public int minIntervalTicks = 720;
        public int maxIntervalTicks = 1680;
        public int bubbleDurationTicks = 300;
        public int eventCooldownTicks = 300;

        public Vector3 bubbleWorldOffset = new Vector3(0f, 0f, 0.95f);
        public float lowMaterialRatio = 0.15f;
        public float fullStorageRatio = 0.85f;
        public int hostileCheckIntervalTicks = 120;

        public float nearbyScanRadius = 10f;
        public float lowWealthThreshold = 15000f;
        public float highWealthThreshold = 100000f;
        public float extremeColdTempC = 0f;
        public float extremeHeatTempC = 35f;
        public int idleTooLongTicks = 15000;
        public int highProductionThreshold = 500;
        public int productionMilestoneStep = 500;

        public CompProperties_FabricatorThoughts()
        {
            compClass = typeof(CompFabricatorThoughts);
        }
    }
}
