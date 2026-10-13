using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Editor.Content;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>The required PWR-008 reference test measures converted blueprint instances against SPEC-22 §4.9.</summary>
    public sealed class PowerBalanceTests : GameTestBase
    {
        [Test]
        public void ReferenceShips_MatchTable()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath);
            Assert.IsNotNull(database, "The converted content database must exist.");
            using var service = new ContentService(database);
            var catalog = service.Catalog;
            var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            using var library = new BlueprintLibrary(catalog, content);
            (string Ship, double Capacity, double Draw, double Sustain, double Recharge)[] rows =
            {
                ("ship_frigate", 200, 6, double.PositiveInfinity, 5),
                ("ship_fighter", 200, 36, double.PositiveInfinity, 5),
                ("ship_vestari", 320, 40, double.PositiveInfinity, 8),
                ("ship_butterfly", 200, 46, 33, 5),
                ("ship_crescent", 200, 45, 40, 5),
            };
            foreach (var row in rows)
            {
                var blueprint = catalog.Get<ShipBlueprintDefinition>(new ContentId(row.Ship));
                Assert.IsTrue(library.TryGet(blueprint.Id.Hash, out var structure), row.Ship);
                var budget = PowerBudgetValidation.Measure(structure, catalog);
                Assert.AreEqual(40, budget.Generation, row.Ship);
                Assert.AreEqual(row.Capacity, budget.Capacity, row.Ship);
                Assert.AreEqual(row.Draw, budget.WeaponDraw, row.Ship);
                Assert.AreEqual(0, budget.ShieldDraw, row.Ship); Assert.AreEqual(0, budget.ThrusterDraw, row.Ship);
                if (double.IsPositiveInfinity(row.Sustain)) Assert.IsTrue(double.IsPositiveInfinity(budget.SustainSeconds), row.Ship);
                else Assert.AreEqual(row.Sustain, budget.SustainSeconds, 1, row.Ship);
                Assert.AreEqual(row.Recharge, budget.RechargeSeconds, 1, row.Ship);
                Assert.GreaterOrEqual(budget.SustainSeconds, 30, row.Ship);
                Assert.LessOrEqual(budget.RechargeSeconds, 10, row.Ship);
            }

            var shieldBlock = catalog.Get<BlockDefinition>(new ContentId("block_shield_small"));
            Assert.IsTrue(shieldBlock.TryGetModule<ShieldEmitterModule>(out var shield));
            Assert.AreEqual(3000, shield.Params.Capacity); Assert.AreEqual(120, shield.Params.RegenPerSecond);
            Assert.AreEqual(4, shield.Params.RechargeDelaySeconds); Assert.AreEqual(8, shield.Params.PowerDraw);
            Assert.IsTrue(library.TryGet(new ContentId("ship_fighter").Hash, out var fighter));
            using var shieldedFighter = fighter.Clone();
            shieldedFighter.TryGetCellBounds(out var min, out _);
            Assert.AreEqual(PlaceResult.Ok, shieldedFighter.TryPlaceBlock(new BlockPlacement
                { Block = catalog.GetBlockIndex(shieldBlock), Origin = min - new int3(2, 0, 0) }, content));
            var shieldedBudget = PowerBudgetValidation.Measure(shieldedFighter, catalog);
            Assert.AreEqual(fighter.Data.Instances.Length + 1, shieldedFighter.Data.Instances.Length);
            Assert.AreEqual(40, shieldedBudget.Generation); Assert.AreEqual(200, shieldedBudget.Capacity);
            Assert.AreEqual(36, shieldedBudget.WeaponDraw); Assert.AreEqual(8, shieldedBudget.ShieldDraw);
            Assert.AreEqual(0, shieldedBudget.ThrusterDraw);
            Assert.AreEqual(50, shieldedBudget.SustainSeconds, 1);
            Assert.AreEqual(6.3, shieldedBudget.RechargeSeconds, 1);
            Assert.GreaterOrEqual(shieldedBudget.SustainSeconds, 30);
            Assert.LessOrEqual(shieldedBudget.RechargeSeconds, 10);
            Assert.IsTrue(fighter.IsReadOnly, "The shipped blueprint remains immutable.");
        }
    }
}
