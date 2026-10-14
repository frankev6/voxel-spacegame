using System;
using Game.Core;
using Game.Data.Services;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §4.3: blocks are their own inventory items; air and Missing are not obtainable.</summary>
    public sealed class BlockItemMap
    {
        readonly ContentId[] items;
        readonly int[] prices;
        public BlockItemMap(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            items = new ContentId[catalog.BlockCount + 1]; prices = new int[items.Length];
            for (int i = 1; i < items.Length; i++)
            {
                var block = catalog.GetBlock((ushort)i);
                if (ReferenceEquals(block, catalog.MissingBlock)) continue;
                items[i] = block.Id; prices[i] = block.BasePrice;
            }
        }
        public int Count => items.Length;
        public bool HasItem(int block) => block > 0 && block < items.Length && !string.IsNullOrEmpty(items[block].Value);
        public ContentId ItemOf(int block) => HasItem(block) ? items[block] : default;
        public int PriceOf(int block) => HasItem(block) ? prices[block] : 0;
    }
}
