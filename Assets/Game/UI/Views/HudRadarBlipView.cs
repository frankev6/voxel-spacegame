namespace Game.UI.Views
{
    /// <summary>UI-facing copy of one HUD radar contact (SPEC-61 §6).</summary>
    public struct HudRadarBlipView
    {
        public UnityEngine.Vector2 OffsetXZ;
        public byte Kind;
        public byte Relation;
        public byte Flags;
    }
}
