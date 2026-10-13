using Game.Core;
using Game.Data.Definitions;

namespace Game.Economy
{
    /// <summary>SPEC-50 §4.3 inventory value contract.</summary>
    public readonly struct ItemDelta { public readonly ContentId Item; public readonly int Count;   // + add, − remove; 0 ignored
        public ItemDelta(ContentId item, int count) { Item = item; Count = count; } }
}
