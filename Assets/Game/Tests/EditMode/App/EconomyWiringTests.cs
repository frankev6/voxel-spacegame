using System;
using System.Collections.Generic;
using Game.App.Services;
using Game.App.Spawning;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Settings;
using Game.Data.Economy;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Input;
using Game.Galaxy.Components;
using Game.Galaxy.Systems;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Factions;
using Game.Presentation.Audio;
using Game.Presentation.Presenters;
using Game.Tests.Common;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
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
using Game.Voxel.Grid;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;

namespace Game.Tests.EditMode.App
{
    /// <summary>ECO-011/008: real capacity/directory and merchant launcher/faction/presenter owners, with direct bounded calls.</summary>
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
        sealed class MerchantFixture : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Content = new(2, 0, 42, false);
            internal readonly EcsTestWorld Ecs = EcsTestWorld.Create(typeof(OriginRebaseSystem));
            internal EntityManager Em => Ecs.EntityManager;
            internal readonly ContentCatalog Catalog;
            internal readonly FactionDefinition Owner;
            internal readonly MerchantDefinition Merchant;
            internal readonly EconomyService Economy;
            internal readonly GalaxyService Galaxy;
            internal readonly FactionService Factions;
            internal readonly TerritoryService Territory;
            internal readonly StarSystemLoader Loader;
            internal readonly TravelService Travel;
            internal readonly Game.Galaxy.Services.EncounterDirectorService Director;
            internal readonly MerchantLauncher Launcher;
            internal readonly LogicWorldAdapter Adapter;
            internal readonly UIRoot UI;
            internal readonly MerchantPresenter Presenter;
            internal readonly MerchantViewModel Model = new();
            internal readonly MerchantView View;
            readonly GalaxyState state = new();
            readonly PauseService pause = new();
            readonly List<Action> restoreServices = new();
            readonly List<Entity> factionEntities = new();
            readonly GameSession session;
            readonly BlueprintLibrary blueprints;
            readonly VoxelStructureRegistry registry;
            readonly ShipSpawnService spawner;
            readonly AudioService audio;
            readonly InputService input;
            readonly InputServiceBridge inputBridge;
            readonly GameObject documentRoot;
            readonly NativeQueue<ManagedEventEnvelope<OriginShifted>> originEvents;
            internal MerchantFixture()
            {
                try
                {
                    Content.Create<FactionDefinition>("faction_ark", so =>
                    { so.FindProperty("isPlayerFaction").boolValue = true; so.FindProperty("relationLocked").boolValue = true; });
                    Owner = Content.Create<FactionDefinition>("faction_vestari", so =>
                    {
                        so.FindProperty("basePlayerRelation").intValue = -50;
                        var owned = so.FindProperty("ownedStarSystems"); owned.arraySize = 1;
                        owned.GetArrayElementAtIndex(0).objectReferenceValue = Content.Start;
                    });
                    var encounters = Content.Create<EncounterConfigDefinition>("config_encounters");
                    var config = Content.Create<EconomyConfigDefinition>("config_economy");
                    var item = Content.Create<ItemDefinition>("resource_market_test", so =>
                    { so.FindProperty("basePrice").intValue = 100; so.FindProperty("maxStack").intValue = 50; });
                    Merchant = Content.Create<MerchantDefinition>("merchant_test", so =>
                    {
                        so.FindProperty("displayName").stringValue = "Test market";
                        var placements = so.FindProperty("authoredBodies"); placements.arraySize = 1;
                        placements.GetArrayElementAtIndex(0).FindPropertyRelative("starSystem").objectReferenceValue = Content.Start;
                        placements.GetArrayElementAtIndex(0).FindPropertyRelative("bodyKey").stringValue = "planet";
                        var stock = so.FindProperty("stock"); stock.arraySize = 1; var row = stock.GetArrayElementAtIndex(0);
                        row.FindPropertyRelative("item").objectReferenceValue = item;
                        row.FindPropertyRelative("count").intValue = 1;
                        row.FindPropertyRelative("priceFactor").floatValue = 1;
                    });
                    Content.Create<TravelConfigDefinition>("config_travel", so => so.FindProperty("fuelItem").objectReferenceValue = item);
                    var gameConfig = Content.Create<GameConfigDefinition>("config_economy_game");
                    var narrativeConfig = Content.Create<NarrativeConfigDefinition>("narrative_economy");
                    foreach (var body in new[] { Content.Star, Content.Planet, Content.Moon })
                    { using var so = new SerializedObject(body); so.FindProperty("hasCollider").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                    var report = new ValidationReport("ECO-008 real owners"); Catalog = ContentCatalog.Build(Content.Definitions, report);
                    Assert.IsFalse(report.HasErrors, report.ToText());
                    Ecs.AddGameplayActive(); Ecs.AddWorldSceneActive(); Ecs.AddSimClock(); Ecs.AddSingleton(default(PlayerControlLock));
                    originEvents = new NativeQueue<ManagedEventEnvelope<OriginShifted>>(Allocator.Persistent);
                    Ecs.AddSingleton(new ManagedEventQueue<OriginShifted> { Queue = originEvents });
                    var player = Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform), typeof(UniversePosition));
                    Em.SetComponentData(player, LocalTransform.Identity);
                    FactionContent.CreateSingletons(Em, Catalog, encounters, factionEntities); Factions = new FactionService(Ecs.World, Catalog);
                    Galaxy = new GalaxyService(Catalog, state, Ecs.World, new GalaxyFrameTicker()); Galaxy.NewGame(42);
                    blueprints = new BlueprintLibrary(Catalog, default); registry = VoxelStructureRegistry.CreateRuntime(blueprints);
                    session = new GameSession(Ecs.World, gameConfig);
                    var docking = new DockingService(); Loader = new StarSystemLoader(Galaxy, Ecs.World, registry, blueprints, session, Catalog, docking);
                    audio = new AudioService(null, null, AudioVolumes.Default, null);
                    Travel = new TravelService(Galaxy, Loader, docking, Ecs.World, Catalog, pause, audio);
                    Territory = new TerritoryService(Galaxy, Factions, Catalog);
                    spawner = new ShipSpawnService(Ecs.World, registry, Catalog, session, Galaxy);
                    Director = new Game.Galaxy.Services.EncounterDirectorService(Galaxy, Loader, Travel, docking, spawner, Factions, Territory, Ecs.World, Catalog) { Enabled = false };
                    Load(Galaxy.Galaxy.StartStarSystem);
                    Economy = new EconomyService(Catalog, config, () => 42, () => 0); Economy.Inventory.SetCapacity(20); Economy.Wallet.Set(10000);
                    input = new InputService(new ControlSettingsData()); inputBridge = new InputServiceBridge(input);
                    documentRoot = new GameObject("ECO-008 merchant UI"); var document = documentRoot.AddComponent<UIDocument>();
                    var uiConfig = AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset");
                    UI = new UIRoot(document, uiConfig, pause, inputBridge, new NullUISoundSink(), new UnityFrameClock());
                    Presenter = new MerchantPresenter(UI, Model, Economy.Inventory, Economy.Wallet);
                    View = new MerchantView(Model, Presenter); UI.Register(View); Presenter.Bind();
                    Register(Factions); Register(Territory); Register(Loader); Register(Director); Register(Presenter);
                    Launcher = new MerchantLauncher(Economy, new MerchantDirectory(Catalog, Galaxy), Factions, Territory, Director, Loader, UI.Navigator); Register(Launcher);
                    Adapter = new LogicWorldAdapter(Catalog, session, Galaxy, blueprints, audio, UI.Notifications, Ecs.World, narrativeConfig);
                }
                catch { Dispose(); throw; }
            }
            internal void Load(StarSystemId system)
            {
                state.CurrentStarSystem = system; state.HasPlayerLocation = true; state.PlayerLocation = new PlayerLocation { StarSystem = system };
                Loader.OnSceneReady(new SceneReadyContext(GameScene.World, SceneEntryReason.NewGame, default));
                Ecs.UpdateSystem<OriginRebaseSystem>(); Ecs.CompleteAll(); Loader.Tick(new GalaxyFrameContext(Em, 0, 0, 0, 0));
                Assert.IsTrue(Loader.IsReady); Assert.AreEqual(system, Loader.Loaded); EventHub.DrainAll();
            }
            void Register<T>(T value) where T : class
            {
                Services.TryGet<T>(out var previous); if (previous != null) Services.Unregister(previous); Services.Register(value);
                restoreServices.Add(() => { Services.Unregister(value); if (previous != null && !Services.TryGet<T>(out _)) Services.Register(previous); });
            }
            public void Dispose()
            {
                Ecs.CompleteAll(); Presenter?.Dispose(); UI?.Dispose();
                for (int i = restoreServices.Count - 1; i >= 0; i--) restoreServices[i]();
                Adapter?.Dispose(); Director?.Dispose(); Travel?.Dispose(); Loader?.Dispose(); spawner?.Dispose();
                ((object)Territory as IDisposable)?.Dispose();
                session?.Dispose(); registry?.Dispose(); if (registry != null) Object.DestroyImmediate(registry); blueprints?.Dispose();
                Factions?.Dispose(); Galaxy?.Dispose(); state.Dispose();
                foreach (var entity in factionEntities)
                    if (Em.Exists(entity) && Em.HasComponent<FactionRelations>(entity)) Em.GetComponentData<FactionRelations>(entity).Dispose();
                Ecs.Dispose(); if (originEvents.IsCreated) originEvents.Dispose();
                inputBridge?.Dispose(); input?.Dispose(); pause.Dispose(); audio?.Dispose();
                if (documentRoot != null) Object.DestroyImmediate(documentRoot); Content.Dispose();
            }
        }
        [Test] public void Launcher_HostileRefused()
        {
            using var f = new MerchantFixture();
            Assert.AreSame(f.Owner, f.Territory.GetOwner(f.Loader.Loaded)); Assert.AreEqual(FactionStance.Hostile, f.Factions.GetPlayerStance(f.Owner));
            Assert.IsTrue(f.Galaxy.TryResolveBody(f.Content.Start.Id, "planet", out var body));
            int closed = 0; Assert.IsTrue(f.Launcher.TryOpenForBody(body, () => closed++));
            var session = f.Economy.Merchants.Current; Assert.NotNull(session); Assert.IsTrue(session.Refused); Assert.AreEqual(-50, session.Relation);
            Assert.AreEqual("They refuse to trade with you.", session.RefusalReason); Assert.IsTrue(f.View.IsOpen); Assert.IsTrue(f.Model.Refused);
            Assert.IsFalse(session.TryBuy(0, 1, out var reason)); Assert.AreEqual(session.RefusalReason, reason);
            f.UI.Navigator.Pop(f.View); Assert.AreEqual(1, closed); Assert.IsNull(f.Economy.Merchants.Current);
            Assert.IsFalse(f.Launcher.TryOpenForBody(default, () => closed++)); Assert.AreEqual(1, closed);
            // A member lookup never assigns an unrelated local territory faction to an unknown ship.
            f.Launcher.Open(f.Merchant, null, ulong.MaxValue); Assert.IsFalse(f.Economy.Merchants.Current.Refused); Assert.AreEqual(0, f.Economy.Merchants.Current.Relation);
            f.UI.Navigator.Pop(f.View);
            Assert.IsTrue(f.Galaxy.TryResolveStarSystem(f.Content.Generation.AuthoredStarSystems[1].Id, out var unowned));
            f.Load(unowned);
            f.Launcher.Open(f.Merchant, null); Assert.IsFalse(f.Economy.Merchants.Current.Refused); Assert.AreEqual(0, f.Economy.Merchants.Current.Relation);
            f.UI.Navigator.Pop(f.View);
        }
        [Test] public void OpenMerchant_CallsOnClosed()
        {
            using var f = new MerchantFixture();
            int callbacks = 0;
            Action closed = () => { callbacks++; Assert.IsFalse(f.View.IsOpen); Assert.IsNull(f.Economy.Merchants.Current); };
            f.Adapter.OpenMerchant(f.Catalog.IndexOf(f.Merchant), default, closed);
            Assert.IsTrue(f.View.IsOpen); Assert.AreEqual(0, callbacks); Assert.NotNull(f.Economy.Merchants.Current);
            f.UI.Navigator.Pop(f.View); Assert.AreEqual(1, callbacks); f.Presenter.ResetSession(); Assert.AreEqual(1, callbacks);
            CollectionAssert.DoesNotContain(LogicWorldAdapter.UnwiredMembers, nameof(LogicWorldAdapter.OpenMerchant));
            // Presenter reset also pops the screen before running its continuation exactly once.
            f.Adapter.OpenMerchant(f.Catalog.IndexOf(f.Merchant), default, closed);
            Assert.AreEqual(1, callbacks); f.Presenter.ResetSession(); Assert.AreEqual(2, callbacks);
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
