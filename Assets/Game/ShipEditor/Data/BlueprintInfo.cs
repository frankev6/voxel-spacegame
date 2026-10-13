namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct BlueprintInfo
    {
        public string Id;
        public string DisplayName;
        public bool ReadOnly;
        public int BlockCount;
        public long ModifiedUtcTicks;
    }
}
