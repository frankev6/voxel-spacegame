namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1: authored loot row in table order.</summary>
    [System.Serializable] public struct LootEntry
    {
        public ItemDefinition item;
        public int min, max;
        public float chancePercent;
    }
}
