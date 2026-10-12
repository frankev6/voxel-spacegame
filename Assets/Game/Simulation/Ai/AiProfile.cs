using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiProfile.</summary>
    public struct AiProfile
    {
        public Game.Data.Definitions.AiRole Role;
        public Game.Data.Definitions.AiFacing Facing;
        public byte AvoidFriendlyFire;
        public byte Reserved;
        public int ThinkIntervalTicks;
        public float SensorRange, PreferredRangeFraction, MinEngageRange, Throttle, FleeHullFraction;
        public float LeashRadius, PatrolRadius, PlayerBias, Stickiness, OrbitStepRad;
        public int OrbitFlipThinks;
        public Game.Core.ContentHash IdHash;
        public BlobArray<AiAbilityRuleDef> AbilityRules;   // baked from abilityRules (ability → AbilityDefsBlob index)
    }
}
