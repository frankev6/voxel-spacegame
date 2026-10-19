using Game.Core;
using Unity.Mathematics;

namespace Game.Presentation.Components
{
    /// <summary>Sixteen-byte ability snapshot (SPEC-61 §4.5).</summary>
    public struct HudAbilityState
    {
        public ContentHash Ability;
        public half Cooldown01;
        public half Channel01;   // ABL-006: share of a running duration ability's time left (the channel ring); 0 = not running
        public byte Charges;
        public byte Ready;
        public byte Active;
        public byte Blocked;     // AbilityBlockReason
    }
}
