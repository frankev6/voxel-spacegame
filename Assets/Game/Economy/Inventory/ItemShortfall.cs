using Game.Core;
using Game.Data.Definitions;

namespace Game.Economy
{
    /// <summary>SPEC-50 §4.3 inventory value contract.</summary>
    public readonly struct ItemShortfall { public readonly ContentId Item; public readonly int Missing;
        public ItemShortfall(ContentId item, int missing) { Item = item; Missing = missing; } }
}
