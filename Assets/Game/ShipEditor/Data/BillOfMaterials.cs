namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public sealed class BillOfMaterials
    {
        // Item.Value ordinal, then block-only lines by Block.
        public readonly System.Collections.Generic.List<BomLine> Lines = new System.Collections.Generic.List<BomLine>(64);
        public int TotalRequired, TotalRefunded, TotalLostDamaged, TotalShortfall;
        public bool HasUnbuildable; // Required >0 without an item in a non-creative session
    }
}
