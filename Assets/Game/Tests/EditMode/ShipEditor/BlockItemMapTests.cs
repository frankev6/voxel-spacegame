using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class BlockItemMapTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void Item_IsBlockItself()
        {
            var definitions = BlockDefsBlobBuilderTests.Defs(out _, out _);
            foreach (var d in definitions) TestContentFactory.Edit(d, so => TestContentFactory.Set(so, "basePrice", 17));
            var catalog = FactCatalogTests.Content(definitions); var map = new BlockItemMap(catalog);
            for (ushort i = 1; i <= catalog.BlockCount; i++)
            {
                var block = catalog.GetBlock(i); if (block == catalog.MissingBlock) continue;
                Assert.IsTrue(map.HasItem(i)); Assert.AreEqual(block.Id, map.ItemOf(i)); Assert.AreEqual(17, map.PriceOf(i));
            }
        }
        [Test] public void AirAndMissing_HaveNoItem()
        {
            var catalog = FactCatalogTests.Content(BlockDefsBlobBuilderTests.Defs(out _, out _)); var map = new BlockItemMap(catalog);
            Assert.IsFalse(map.HasItem(0)); Assert.IsFalse(map.HasItem(catalog.GetBlockIndex(catalog.MissingBlock)));
            Assert.AreEqual(0, map.PriceOf(0)); Assert.AreEqual(default(Game.Core.ContentId), map.ItemOf(catalog.GetBlockIndex(catalog.MissingBlock)));
        }
    }
}
