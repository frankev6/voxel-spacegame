namespace Game.Presentation.Components
{
    /// <summary>Eight-byte fire-group snapshot; Group is bit index 0..2 (SPEC-61 §4.5).</summary>
    public struct HudWeaponGroupState
    {
        public byte Group;
        public byte WeaponCount;
        public byte Firing;
        public byte Pad;
        public float Ready01;
    }
}
