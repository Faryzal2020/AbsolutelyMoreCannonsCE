using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Configuration for a single visual layer of the fabricator AI eye,
    /// positioned and scaled relative to the building's main graphic.
    /// </summary>
    public class FabricatorEyeLayerProperties
    {
        public string texPath;
        public Vector2 scale = Vector2.one;
        public Vector3 offset = Vector3.zero;
        public Color color = Color.white;
        public bool useGlowShader = false;
    }

    /// <summary>
    /// CompProperties for <see cref="CompFabricatorEye"/>, configuring the layered
    /// eyeball, cover, and glow graphics as well as tracking and idle look-around behavior.
    /// </summary>
    public class CompProperties_FabricatorEye : CompProperties
    {
        public FabricatorEyeLayerProperties eyeball = new FabricatorEyeLayerProperties
        {
            texPath = "Things/Building/AUTOFABRICATOR_EYEBALL",
            scale = new Vector2(0.08125f, 0.08125f),
            offset = Vector3.zero,
            useGlowShader = false
        };

        public FabricatorEyeLayerProperties eyeballOff = new FabricatorEyeLayerProperties
        {
            texPath = "Things/Building/AUTOFABRICATOR_EYE_OFF",
            scale = new Vector2(0.08125f, 0.08125f),
            offset = Vector3.zero,
            useGlowShader = false
        };

        public FabricatorEyeLayerProperties cover = new FabricatorEyeLayerProperties
        {
            texPath = "Things/Building/AUTOFABRICATOR_COVER",
            scale = Vector2.one,
            offset = Vector3.zero,
            useGlowShader = false
        };

        public FabricatorEyeLayerProperties glow = new FabricatorEyeLayerProperties
        {
            texPath = "Things/Building/AUTOFABRICATOR_GLOW",
            scale = new Vector2(0.2708f, 0.2688f),
            offset = Vector3.zero,
            useGlowShader = true
        };

        public FabricatorEyeLayerProperties topOff = new FabricatorEyeLayerProperties
        {
            texPath = "Things/Building/AUTOFABRICATOR_TOP_OFF",
            scale = Vector2.one,
            offset = Vector3.zero,
            useGlowShader = false
        };

        public FabricatorEyeLayerProperties topOn = new FabricatorEyeLayerProperties
        {
            texPath = "Things/Building/AUTOFABRICATOR_TOP_ON",
            scale = Vector2.one,
            offset = Vector3.zero,
            useGlowShader = false
        };

        // Eyeball movement bounds (in map tiles/world units) relative to its neutral offset
        public float maxOffsetLeft = 0.15f;
        public float maxOffsetRight = 0.15f;
        public float maxOffsetDown = 0.50f;
        public float maxOffsetUp = 0.50f;

        // Pawn tracking settings
        public float detectRadius = 12f;
        public bool onlyTrackInFront = true;
        public float frontZMargin = 1.0f;

        // Smoothing and idle behavior (60 ticks = 1 second; 300..600 ticks = 5..10 seconds)
        public float trackSpeed = 0.25f;
        public float idleSpeed = 0.20f;
        public int idleHoldTicksMin = 300;
        public int idleHoldTicksMax = 600;

        // Saccades (rapid eye darts) while staring at a tracked pawn (45..180 ticks = 0.75..3.0 seconds)
        public Vector2 trackSaccadeMagnitude = new Vector2(0.04f, 0.09f);
        public int trackSaccadeIntervalMin = 45;
        public int trackSaccadeIntervalMax = 180;

        // Wider saccades while staring at a random idle location (45..150 ticks = 0.75..2.5 seconds)
        public Vector2 idleSaccadeMagnitude = new Vector2(0.08f, 0.18f);
        public int idleSaccadeIntervalMin = 45;
        public int idleSaccadeIntervalMax = 150;

        // Periodic chance to ignore nearby pawns and wander/idle anyway (300..600 ticks = 5..10 seconds)
        public int ignorePawnCheckIntervalMin = 300;
        public int ignorePawnCheckIntervalMax = 600;
        public float ignorePawnChance = 0.35f;
        public int ignorePawnDurationMin = 300;
        public int ignorePawnDurationMax = 600;

        public bool requirePower = true;

        public CompProperties_FabricatorEye()
        {
            compClass = typeof(CompFabricatorEye);
        }
    }
}
