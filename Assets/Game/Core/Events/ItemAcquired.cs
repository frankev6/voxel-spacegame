namespace Game.Core.Events
{
    /// <summary>SPEC-50 §4.2: managed publisher only for narrative, loot, purchase, salvage and dev.</summary>
    public readonly struct ItemAcquired
    {
        public readonly ContentHash Item;
        public readonly int ItemIndex;
        public readonly int Count;
        public readonly AcquireSource Source;
        public ItemAcquired(ContentHash item, int itemIndex, int count, AcquireSource source)
        { Item = item; ItemIndex = itemIndex; Count = count; Source = source; }
    }
}
