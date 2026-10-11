using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Editor.Content;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Content
{
    /// <summary>DATA-009 gate: the real content database has zero validation errors and boots ContentService.</summary>
    public sealed class ProjectContentTests
    {
        [Test]
        public void ProjectContent_HasZeroValidationErrors()
        {
            var report = ContentValidatorRunner.Validate(ContentDatabaseBuilder.Rebuild());
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }

        [Test]
        public void V2Additions_HaveExpectedFunctions()
        {
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            var reactor = service.Catalog.Get<BlockDefinition>(new ContentId("block_reactor_small"));
            var cargo = service.Catalog.Get<BlockDefinition>(new ContentId("block_cargo_small"));
            ref var reactorDef = ref service.BlockDefs.Value.Blocks[service.Catalog.GetBlockIndex(reactor)];
            ref var cargoDef = ref service.BlockDefs.Value.Blocks[service.Catalog.GetBlockIndex(cargo)];
            Assert.AreEqual((1ul << (int)BlockModuleId.PowerSource) | (1ul << (int)BlockModuleId.Explosive), reactorDef.ModuleMask);
            Assert.AreEqual(1ul << (int)BlockModuleId.Cargo, cargoDef.ModuleMask);
            Assert.AreEqual(BlockCategory.Power, reactorDef.Category); Assert.AreEqual(BlockCategory.Utility, cargoDef.Category);
            Assert.AreEqual(8, reactorDef.Cells.Length); Assert.AreEqual(2, cargoDef.Cells.Length);
            Assert.AreEqual("Reactor Small", reactor.DisplayName); Assert.AreEqual("Cargo Pod Small", cargo.DisplayName);
            Assert.AreEqual(150, reactorDef.CellHp); Assert.AreEqual(1200, reactorDef.FunctionHp); Assert.AreEqual(50, reactorDef.CellMass);
            Assert.AreEqual(100, cargoDef.CellHp); Assert.AreEqual(200, cargoDef.FunctionHp); Assert.AreEqual(20, cargoDef.CellMass);
            Assert.AreEqual(300, reactor.BasePrice); Assert.AreEqual(ItemRarity.Rare, reactor.Rarity);
            Assert.AreEqual(20, cargo.BasePrice); Assert.AreEqual(ItemRarity.Common, cargo.Rarity);
            Assert.IsTrue(reactor.TryGetModule<PowerSourceModule>(out var power)); Assert.AreEqual(60, power.Params.Generation);
            Assert.IsTrue(reactor.TryGetModule<ExplosiveModule>(out var explosive));
            Assert.AreEqual(4, explosive.Params.BlastRadiusCells); Assert.AreEqual(400, explosive.Params.BlastDamage);
            Assert.IsTrue(cargo.TryGetModule<CargoModule>(out var storage)); Assert.AreEqual(50, storage.Params.Capacity);
            Assert.AreEqual("spritetest2_transparent_12", reactor.Icon.name); Assert.AreEqual("spritetest2_transparent_0", cargo.Icon.name);
            Assert.AreEqual("base_2x2_hull_base_2x2_hull", reactor.PropMesh.name);
            Assert.AreEqual("base_1x2_hull.001_base_1x2_hull.001", cargo.PropMesh.name);
            foreach (var block in new[] { reactor, cargo })
            {
                Assert.AreEqual(BlockKind.Functional, block.Kind); Assert.AreEqual(BlockShape.Cube, block.Shape);
                Assert.AreEqual(OrientationMask.AllBits, block.AllowedOrientations.Bits); Assert.AreEqual(BlockFaceMask.All, block.AttachFaces);
                Assert.AreEqual(0, block.Clearance.Count); Assert.AreEqual(999, block.MaxStack);
            }
        }

        [Test]
        public void RealDatabase_BootsContentService()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath);
            Assert.IsNotNull(db, "Assets/Game/Content/ContentDatabase.asset exists");
            using var service = new ContentService(db);
            Assert.AreEqual("block_missing", service.Catalog.MissingBlock.IdString);
            Assert.AreEqual("item_missing", service.Catalog.MissingItem.IdString);
            Assert.AreEqual(63, service.Catalog.MissingBlock.MaterialLayer);
            Assert.AreEqual(service.Catalog.GetBlockIndex(service.Catalog.MissingBlock), service.BlockDefs.Value.MissingBlockIndex);
            Assert.IsTrue(service.Catalog.TryGet(new ContentId("item_missing"), out _));
        }
    }
}
