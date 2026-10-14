using System;
using Game.App.Economy;
using Game.App.Session;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Galaxy;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;

namespace Game.Tests.EditMode.App
{
    /// <summary>ECO-011: the three transferred owner checks; tiny native structures and direct managed events.</summary>
    public sealed class EconomyWiringTests
    {
        sealed class CargoFixture : IDisposable
        {
            internal readonly EconomyConfigDefinition Config = TestContentFactory.Create<EconomyConfigDefinition>("config_economy_capacity");
            internal readonly ContentService Content;
            internal readonly InventoryModel Inventory;
            readonly ushort cargo;
            internal CargoFixture()
            {
                Content = ContentService.CreateForTests(new ContentDefinition[]
                {
                    AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_cargo_small.asset")
                });
                Inventory = new InventoryModel(Content.Catalog);
                cargo = Content.Catalog.GetBlockIndex(Content.Catalog.Get<BlockDefinition>(new ContentId("block_cargo_small")));
            }
            internal VoxelStructure Ship(int blocks)
            {
                var structure = new VoxelStructure();
                try
                {
                    var content = new VoxelContent { Blocks = Content.BlockDefs, Orientations = Content.Orientations };
                    for (int i = 0; i < blocks; i++)
                        Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(new BlockPlacement { Block = cargo, Origin = new int3(i * 4, 0, 0) }, content));
                    return structure;
                }
                catch { structure.Dispose(); throw; }
            }
            public void Dispose() => Content.Dispose();
        }
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }

        [Test] public void Capacity_TwoCargoBlocks()
        {
            using var f = new CargoFixture(); using var structure = f.Ship(2);
            Assert.AreEqual(4, structure.SolidCellCount, "The real cargo definition occupies two cells per instance.");
            Assert.AreEqual(30, CargoCapacitySync.ComputeCapacity(structure, f.Content.Catalog, f.Config));
            var data = structure.Data; var instance = data.Instances[0]; instance.Flags |= BlockInstanceFlags.Destroyed; data.Instances[0] = instance;
            Assert.AreEqual(25, CargoCapacitySync.ComputeCapacity(structure, f.Content.Catalog, f.Config), "Destroyed cargo functions contribute no capacity.");
            Assert.AreEqual(20, CargoCapacitySync.ComputeCapacity(null, f.Content.Catalog, f.Config));
        }
        [Test] public void Capacity_FollowsFleetChanged()
        {
            using var f = new CargoFixture(); using var fleet = new FleetModel();
            fleet.Add(new FleetShipRecord { PersistentId = 1, Name = "Cargo fixture", Structure = f.Ship(2) }, true);
            int subscribers = EventBus<SessionReset>.SubscriberCount;
            using var sync = new CargoCapacitySync(f.Inventory, fleet, f.Content.Catalog, f.Config);
            Assert.AreEqual(40, sync.NewGameOrder); Assert.AreEqual(30, f.Inventory.Capacity);
            Assert.AreEqual(subscribers + 1, EventBus<SessionReset>.SubscriberCount);
            fleet.ReplaceStructure(1, f.Ship(3));
            Assert.AreEqual(35, f.Inventory.Capacity, "The fleet event must add five slots without a manual recompute.");
            f.Inventory.SetCapacity(1);
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Loaded)); EventHub.DrainAll();
            Assert.AreEqual(35, f.Inventory.Capacity);
            f.Inventory.SetCapacity(1); sync.InitializeNewGame(default(NewGameArgs)); Assert.AreEqual(35, f.Inventory.Capacity);
            sync.Dispose(); sync.Dispose(); Assert.AreEqual(subscribers, EventBus<SessionReset>.SubscriberCount);
            fleet.ReplaceStructure(1, f.Ship(4));
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Cleared)); EventHub.DrainAll();
            Assert.AreEqual(35, f.Inventory.Capacity, "A disposed synchronizer cannot keep writing capacity.");
        }
        [Test] public void Directory_AuthoredFirst()
        {
            using var fixture = new TestGalaxyFixtures.Fixture(2, 0, 42, false);
            // The template merchant sorts first, so authored precedence cannot come from iteration order.
            var templateMerchant = fixture.Create<MerchantDefinition>("merchant_a_template", so =>
            {
                so.FindProperty("displayName").stringValue = "Template market";
                var templates = so.FindProperty("bodyTemplates"); templates.arraySize = 1; templates.GetArrayElementAtIndex(0).objectReferenceValue = fixture.Planet;
            });
            var authoredMerchant = fixture.Create<MerchantDefinition>("merchant_z_authored", so =>
            {
                so.FindProperty("displayName").stringValue = "Authored market";
                var placements = so.FindProperty("authoredBodies"); placements.arraySize = 1;
                placements.GetArrayElementAtIndex(0).FindPropertyRelative("starSystem").objectReferenceValue = fixture.Start;
                placements.GetArrayElementAtIndex(0).FindPropertyRelative("bodyKey").stringValue = "planet";
            });
            var report = new ValidationReport("ECO-011 directory"); var catalog = ContentCatalog.Build(fixture.Definitions, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            using var world = new World("ECO-011 directory"); using var state = new GalaxyState();
            using var galaxy = new GalaxyService(catalog, state, world, new GalaxyFrameTicker());
            var directory = new MerchantDirectory(catalog, galaxy); Assert.IsNull(directory.FindForBody(default));
            galaxy.NewGame(42);
            Assert.IsTrue(galaxy.TryResolveBody(fixture.Start.Id, "planet", out var authoredBody));
            Assert.AreSame(authoredMerchant, directory.FindForBody(authoredBody));
            var other = fixture.Generation.AuthoredStarSystems[1];
            Assert.IsTrue(galaxy.TryResolveBody(other.Id, "planet", out var templateBody));
            Assert.AreSame(templateMerchant, directory.FindForBody(templateBody));
            Assert.IsNull(directory.FindForBody(new BodyId(authoredBody.System, BodyRole.Star, 0, 0)));
            Assert.IsNull(directory.FindForBody(new BodyId(authoredBody.System, BodyRole.PointOfInterest, 999, 0)));
            Assert.IsNull(directory.FindForBody(new BodyId(default, BodyRole.Planet, 0, 0)));
        }
    }
}
