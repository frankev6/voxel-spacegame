using Game.Core;
using Game.Data.Definitions;

namespace Game.Economy
{
    /// <summary>SPEC-50 §4.3 inventory value contract.</summary>
    public struct ItemStack
    {
        public ItemDefinition Item;
        public int Count;                 // 1..Item.MaxStack when Item != null
        public ContentId OriginalId;      // default unless Item == catalog.MissingItem
        public bool IsEmpty => Item == null;
    }
}
