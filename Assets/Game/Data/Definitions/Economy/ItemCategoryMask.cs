namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1: bit i corresponds to ItemCategory i; append only.</summary>
    [System.Flags] public enum ItemCategoryMask : ushort
    { None = 0, Block = 1 << 0, Resource = 1 << 1, Consumable = 1 << 2, Module = 1 << 3, Officer = 1 << 4, Paint = 1 << 5, Quest = 1 << 6, Misc = 1 << 7 }
}
