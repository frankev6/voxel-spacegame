using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [System.Serializable] public sealed class AuthoredLane
    {
        public AuthoredStarSystemDefinition a, b;
        public LaneKind kind = LaneKind.Hyperlane;
        public Game.Data.Logic.CompiledCondition condition = new Game.Data.Logic.CompiledCondition();  // empty source = always open
        [UnityEngine.TextArea(1, 3)] public string lockedMessage;   // shown on the map while closed
        public BodyTemplateDefinition gateTemplateA, gateTemplateB; // null = galaxy default for the kind
        public bool hiddenUntilKnown;
    }
}
