using System.Collections.Generic;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Situational state tags evaluated by <see cref="CompFabricatorThoughts"/> to gate and weight topics.
    /// </summary>
    public enum FabricatorSituationTag
    {
        Idle,
        Crafting,
        LowMaterials,
        FullStorage,
        TrackingPawn,
        Upgrading,
        HasModule,
        HostilesPresent,
        LowPower,
        BrokenDown,
        SwitchedOff,
        TrackedPawnMissingArm,
        TrackedPawnIsPrisoner,
        MultipleFabricators,
        CorpseNearby,
        ColonistCorpseNearby,
        HighWealth,
        LowWealth,
        TrackedPawnArmed,
        TrackedPawnInjured,
        TrackedPawnMentalBreak,
        TrackedPawnDrafted,
        BadWeather,
        ToxicFallout,
        ExtremeCold,
        ExtremeHeat,
        NightTime,
        HighProduction,
        IdleTooLong,
        NearTurret,
        TurretFiringNearby
    }

    /// <summary>
    /// A single weighted line entry inside a <see cref="FabricatorTopicDef"/>.
    /// Supports dynamic tokens: {PAWN}, {AMMO}, {MODULE}, {UPGRADE}, {CORPSE}, {WEALTH},
    /// {NEW_COLONIST}, {WEAPON}, {CRAFT_SKILL}, {MOOD}, {WEATHER}, {TEMP}, {HOUR},
    /// {TOTAL_CRAFTED}, {QUEUE_COUNT}, {STORAGE_PCT}, {TURRET}.
    /// </summary>
    public class FabricatorTopicLine
    {
        public string text;
        public float weight = 1.0f;
        public bool requiresTrackedPawn = false;
        public bool requiresCrafting = false;
        public bool requiresModule = false;
        public bool requiresCorpse = false;
        public bool requiresTurret = false;
        public bool requiresTrackedPawnWeapon = false;
        public int minUpgradeLevel = 0;
    }

    /// <summary>
    /// XML-defined topic glossary entry for the Ammo Fabricator's AI thought/speech bubbles.
    /// </summary>
    public class FabricatorTopicDef : Def
    {
        public float baseWeight = 1.0f;
        public int cooldownTicks = 1800;
        public float recencyPenalty = 0.25f;
        public bool allowWhenUnpowered = false;
        public string eventTag;

        public List<FabricatorSituationTag> requiredTags = new List<FabricatorSituationTag>();
        public List<FabricatorSituationTag> forbiddenTags = new List<FabricatorSituationTag>();
        public List<FabricatorSituationTag> boostedTags = new List<FabricatorSituationTag>();
        public float boostMultiplier = 2.0f;

        public List<FabricatorTopicLine> lines = new List<FabricatorTopicLine>();
    }
}
