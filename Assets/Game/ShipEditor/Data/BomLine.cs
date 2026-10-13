namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct BomLine
    {
        public ushort Block;
        public Game.Core.ContentId Item; // default when there is no item
        public int Required;             // max(0, draftIntact - originalIntact)
        public int Refunded;             // max(0, originalIntact - draftIntact)
        public int LostDamaged;          // originalDamaged - draftDamaged, ≥0
        public int Available;            // last inventory count; 0 in creative
        public int Shortfall => Unity.Mathematics.math.max(0, Required - Available);
        public bool HasItem => !string.IsNullOrEmpty(Item.Value);
    }
}
