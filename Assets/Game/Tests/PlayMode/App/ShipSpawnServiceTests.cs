using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.App.Spawning;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Voxel.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.App
{
    /// <summary>SPEC-01 FND-012: one-cell real VOXS, canonical service/profile APIs, one direct conversion update.</summary>
    public sealed class ShipSpawnServiceTests
    {
        sealed class Ids : IPersistentIdAllocator
        {
            internal ulong Next = 1;
            public ulong Allocate() => Next++;
        }
        sealed class Fixture : IDisposable
        {
            internal readonly World World = new("FND-012 minimal spawn");
            internal EntityManager EM => World.EntityManager;
            internal readonly ContentService Content;
            internal readonly VoxelStructure Source = new();
            internal readonly ShipBlueprintDefinition Blueprint;
            internal readonly AiProfileDefinition AiProfile;
            internal readonly GalaxyState State;
            internal readonly GalaxyService Galaxy;
            internal readonly VoxelStructureRegistry Registry;
            internal readonly ShipSpawnService Spawner;
            internal readonly Ids Allocator = new();
            internal readonly StarSystemId OtherSystem;
            readonly BlueprintLibrary library;
            readonly SystemHandle spawn;
            internal Fixture()
            {
                var defs = new List<ContentDefinition> {
                    AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_hull.asset") };
                Blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); Blueprint.name = "ship_spawn_fixture";
                var serialized = new SerializedObject(Blueprint); serialized.FindProperty("id").stringValue = Blueprint.name; serialized.ApplyModifiedPropertiesWithoutUndo();
                using (var seed = ContentService.CreateForTests(defs))
                {
                    ushort hull = seed.Catalog.GetBlockIndex(seed.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                    ushort palette = Source.FindOrAddPalette(new PaletteEntry { Block = hull }); Source.SetCell(int3.zero, Cell.Palette(palette));
                    Blueprint.EditorSetPayload(VoxelStructureCodec.Encode(Source, VoxsBlockNames.FromCatalog(seed.Catalog), VoxsEncodeOptions.ForAsset), 1, Vector3Int.zero, Vector3Int.zero);
                }
                AiProfile = AssetDatabase.LoadAssetAtPath<AiProfileDefinition>("Assets/Game/Content/Ai/ai_skittish.asset"); Assert.IsNotNull(AiProfile);
                defs.Add(Blueprint); defs.Add(AiProfile); Content = ContentService.CreateForTests(defs);
                library = new BlueprintLibrary(Content.Catalog, new VoxelContent { Blocks = Content.BlockDefs, Orientations = Content.Orientations });
                Registry = VoxelStructureRegistry.CreateRuntime(library);
                Content.CreateSingletons(EM); EM.CreateSingleton(new VoxelStructureRegistryRef { Value = Registry });
                EM.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1, DefaultCellSize = .5f, RequireRenderer = 0 });
                EM.CreateEntity(typeof(GameplayActive)); spawn = World.CreateSystem<GridSpawnSystem>();
                using (var query = EM.CreateEntityQuery(ComponentType.ReadOnly<RootComponentRegistration>()))
                {
                    var registrations = query.GetSingletonBuffer<RootComponentRegistration>(false);
                    FlightSetup.Register(registrations); CombatGridSetup.Register(registrations); AiSetup.Register(registrations);
                }
                // Use the shipped definition and real GalaxyService. No alternate production galaxy/spawner implementation.
                var definition = AssetDatabase.LoadAssetAtPath<GalaxyGenerationDefinition>("Assets/Game/Content/Galaxy/galaxy_main.asset"); Assert.IsNotNull(definition);
                Assert.GreaterOrEqual(definition.AuthoredStarSystems.Count, 2);
                State = new GalaxyState { CurrentStarSystem = StarSystemId.FromAuthored(definition.AuthoredStarSystems[0].Id.Hash) };
                OtherSystem = StarSystemId.FromAuthored(definition.AuthoredStarSystems[1].Id.Hash);
                var galaxyDefs = new List<ContentDefinition>(defs) { definition }; var report = new ValidationReport("FND-012 galaxy constructor");
                var catalog = ContentCatalog.Build(galaxyDefs, report); Assert.IsFalse(report.HasErrors, report.ToText());
                Galaxy = new GalaxyService(catalog, State, World, new GalaxyFrameTicker());
                Spawner = new ShipSpawnService(World, Registry, Content.Catalog, Allocator, Galaxy);
            }
            internal ShipSpawnArgs Args(bool player = false, bool ai = false) => new()
            { Blueprint = Blueprint.Id, Position = new float3(9, 0, -7), Rotation = quaternion.identity, IsPlayer = player, WithAi = ai,
                AiProfile = AiProfile.Id, Allegiance = player ? Allegiance.Player : new Allegiance { Faction = 2, Team = 5 } };
            internal Entity Convert(PersistentId id)
            { spawn.Update(World.Unmanaged); Assert.IsTrue(Spawner.TryGetRoot(id, out var root)); Assert.IsFalse(EM.IsComponentEnabled<GridSpawning>(root)); return root; }
            internal void Update() => spawn.Update(World.Unmanaged);
            public void Dispose()
            {
                EM.CompleteAllTrackedJobs(); Registry.CancelSpawnReads(); Spawner.Dispose(); Galaxy.Dispose(); State.Dispose(); World.Dispose();
                Registry.Dispose(); Object.DestroyImmediate(Registry); library.Dispose(); Content.Dispose(); Source.Dispose(); Object.DestroyImmediate(Blueprint);
            }
        }
        static void FlyingProfile(EntityManager em, Entity root)
        { Assert.IsTrue(em.HasComponent<ShipCommand>(root)); Assert.IsTrue(em.HasComponent<FlightState>(root)); Assert.IsTrue(em.HasComponent<PlanarConstraint>(root)); }
        [Test] public void GalaxyArgs_UsesStarSystem()
        {
            using var f = new Fixture(); IShipSpawner spawner = f.Spawner;
            var args = new GalaxyShipSpawnArgs { Blueprint = f.Blueprint.Id, Position = new float3(3, 0, 8), Rotation = quaternion.identity,
                LinearVelocity = new float3(1, 0, -2), Allegiance = new Allegiance { Faction = 3, Team = 7 }, StarSystem = f.OtherSystem };
            var id = spawner.Spawn(args); Assert.IsTrue(spawner.TryGetRoot(id.Value, out var request));
            Assert.AreEqual(GridProfile.Dummy, f.EM.GetComponentData<GridSpawnRequest>(request).Profile);
            Assert.AreEqual(args.LinearVelocity, f.EM.GetComponentData<GridSpawnRequest>(request).LinearVelocity);
            var root = f.Convert(new PersistentId(id.Value));
            Assert.AreEqual(f.OtherSystem, f.EM.GetSharedComponent<StarSystemMember>(root).Id);
            Assert.AreNotEqual(f.State.CurrentStarSystem, f.OtherSystem); Assert.AreEqual(args.Allegiance, f.EM.GetComponentData<Allegiance>(root)); FlyingProfile(f.EM, root);
            Assert.IsTrue(spawner.Despawn(id.Value)); f.Update(); Assert.IsFalse(spawner.TryGetRoot(id.Value, out _));
        }
        [Test] public void Npc_HasStarSystemMember()
        {
            using var f = new Fixture(); var args = f.Args(ai: true); var id = f.Spawner.Spawn(args);
            Assert.IsTrue(f.Spawner.TryGetRoot(id, out var request)); Assert.AreEqual(GridProfile.NpcShip, f.EM.GetComponentData<GridSpawnRequest>(request).Profile);
            ushort profile = f.Content.Catalog.IndexOf(f.AiProfile);
            Assert.AreEqual(profile, f.EM.GetComponentData<AiBrain>(request).ProfileIndex);
            var root = f.Convert(id); Assert.AreEqual(f.State.CurrentStarSystem, f.EM.GetSharedComponent<StarSystemMember>(root).Id);
            Assert.AreEqual(profile, f.EM.GetComponentData<AiBrain>(root).ProfileIndex); Assert.AreEqual(args.Allegiance, f.EM.GetComponentData<Allegiance>(root)); FlyingProfile(f.EM, root);
        }
        [Test] public void Player_NoMember()
        {
            using var f = new Fixture(); var args = f.Args(player: true); args.Structure = f.Source; args.PersistentId = 79;
            var id = f.Spawner.Spawn(args); Assert.AreEqual(79, id.Value); Assert.AreEqual(1, f.Allocator.Next);
            Assert.IsTrue(f.Spawner.TryGetRoot(id, out var request)); var pending = f.EM.GetComponentData<GridSpawnRequest>(request);
            Assert.AreEqual(GridProfile.PlayerShip, pending.Profile); Assert.AreEqual(GridSpawnFlags.WriteBack, pending.Flags);
            var root = f.Convert(id); Assert.IsFalse(f.EM.HasComponent<StarSystemMember>(root)); Assert.IsTrue(f.EM.HasComponent<PlayerShip>(root));
            Assert.AreEqual(Allegiance.Player, f.EM.GetComponentData<Allegiance>(root)); FlyingProfile(f.EM, root);
            Assert.AreSame(f.Source, f.Registry.Get(f.EM.GetComponentData<GridSource>(root).StructureHandle));
        }
        [Test] public void YawOnly()
        {
            using var f = new Fixture(); var rotation = quaternion.EulerXYZ(.3f, .8f, -.4f);
            var id = f.Spawner.Spawn(f.Blueprint.Id, float3.zero, rotation, Allegiance.None, false);
            var root = f.Convert(id); var yaw = f.EM.GetComponentData<LocalTransform>(root).Rotation;
            Assert.Less(math.distance(math.mul(yaw, new float3(0, 1, 0)), new float3(0, 1, 0)), 1e-6f);
            Assert.AreEqual(Planar.YawFromForward(math.mul(rotation, new float3(0, 0, 1))), Planar.YawFromForward(math.mul(yaw, new float3(0, 0, 1))), 1e-6f);
        }
        [Test] public void UnknownBlueprint()
        {
            using var f = new Fixture(); int before = f.EM.UniversalQuery.CalculateEntityCount(); var args = f.Args(); args.Blueprint = new ContentId("ship_unknown");
            LogAssert.Expect(LogType.Error, new Regex("Unknown ship blueprint 'ship_unknown'")); var id = f.Spawner.Spawn(args);
            Assert.AreEqual(0, id.Value); Assert.AreEqual(before, f.EM.UniversalQuery.CalculateEntityCount()); Assert.AreEqual(1, f.Allocator.Next);
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void RootLookup()
        {
            using var f = new Fixture(); var args = f.Args(); args.Structure = f.Source;
            var id = f.Spawner.Spawn(args); var root = f.Convert(id); int handle = f.EM.GetComponentData<GridSource>(root).StructureHandle;
            Assert.IsTrue(f.Registry.SpawnHooks.Contains(f.Spawner)); Assert.IsTrue(f.Spawner.TryGetRoot(id, out var found)); Assert.AreEqual(root, found);
            Assert.IsTrue(f.Spawner.Despawn(id)); f.Update(); Assert.IsFalse(f.Spawner.TryGetRoot(id, out _)); Assert.IsNull(f.Registry.Get(handle));
            Assert.IsFalse(f.Spawner.Despawn(id)); Assert.IsTrue(f.Source.Data.IsCreated, "Despawn releases the borrow; the caller still owns the structure.");
        }
    }
}
