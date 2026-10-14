using System;
using Game.Economy;
using Game.ShipEditor.Data;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.7: intact refunds and replacement charges; damaged losses never yield items.</summary>
    internal static class BomCalculator
    {
        internal static void Refresh(BillOfMaterials result, BlockCountTables original, BlockCountTables draft,
            BlockItemMap items, InventoryModel inventory, bool creative)
        {
            result.Lines.Clear(); result.TotalRequired = result.TotalRefunded = result.TotalLostDamaged = result.TotalShortfall = 0;
            result.HasUnbuildable = false;
            for (int block = 1; block < draft.TypeCount; block++)
            {
                int delta = draft.Intact(block) - original.Intact(block);
                int lost = original.Damaged(block) - draft.Damaged(block);
                if (delta == 0 && lost == 0) continue;
                var item = items.ItemOf(block);
                var line = new BomLine { Block = (ushort)block, Item = item, Required = Math.Max(0, delta),
                    Refunded = Math.Max(0, -delta), LostDamaged = lost,
                    Available = creative || inventory == null || !items.HasItem(block) ? 0 : inventory.CountOf(item) };
                result.Lines.Add(line); result.TotalRequired += line.Required; result.TotalRefunded += line.Refunded;
                result.TotalLostDamaged += line.LostDamaged; result.TotalShortfall += line.Shortfall;
                result.HasUnbuildable |= !creative && line.Required > 0 && !line.HasItem;
            }
            result.Lines.Sort(Compare);
        }
        static int Compare(BomLine a, BomLine b)
        {
            if (a.HasItem != b.HasItem) return a.HasItem ? -1 : 1;
            int item = string.Compare(a.Item.Value, b.Item.Value, StringComparison.Ordinal);
            return item != 0 ? item : a.Block.CompareTo(b.Block);
        }
    }
}
