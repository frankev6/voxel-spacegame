using Game.Core;

namespace Game.Presentation.Components
{
    /// <summary>Sixteen-byte ability snapshot (SPEC-61 §4.5).</summary>
    public struct HudAbilityState
    {
        public ContentHash Ability;
        public float Cooldown01;
        public byte Charges;
        public byte Ready;
        public byte Active;
        public byte Pad;
    }
}
