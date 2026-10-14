using System;
using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.App.Spawning;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Flight;
using Game.Tests.Common;
using Game.Voxel.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.DevTools
{
    /// <summary>One-cell sandbox World, real spawn/profile/origin data and optional real Unity Physics; no loader readiness substitute.</summary>
    internal sealed class WorldCommandFixture : IDisposable
    {
        internal readonly Game.App.Session.GameSession Session;
        sealed class Slot<T> : IDisposable where T : class
        {
            readonly T previous, current;
            internal Slot(T current)
            {
                this.current = current; Services.TryGet(out previous);
                if (previous != null) Services.Unregister(previous);
                Services.Register(current);
            }
            public void Dispose()
            { Services.Unregister(current); if (previous != null && !Services.TryGet<T>(out _)) Services.Register(previous); }
        }
        internal readonly EcsTestWorld Test;
        internal EntityManager Em => Test.EntityManager;
        internal readonly ContentService Content;
        internal readonly ShipBlueprintDefinition Blueprint;
        internal readonly GalaxyState State = new();
        internal readonly GalaxyService Galaxy;
        internal readonly ShipSpawnService Spawner;
        internal readonly DevRuntimeHost Host;
        internal readonly Entity Player;
        internal static readonly double3 Origin = new(1000, 0, -2000);
        readonly VoxelStructure source = new();
        readonly BlueprintLibrary library;
        readonly VoxelStructureRegistry registry;
        readonly List<IDisposable> slots = new();

        internal WorldCommandFixture(bool physics = false)
        {
            var definitions = new List<ContentDefinition> {
                AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"),
                AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_hull.asset") };
            Blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); Blueprint.name = "ship_dev_fixture";
            var so = new SerializedObject(Blueprint); so.FindProperty("id").stringValue = Blueprint.name; so.ApplyModifiedPropertiesWithoutUndo();
            using (var seed = ContentService.CreateForTests(definitions))
            {
                ushort hull = seed.Catalog.GetBlockIndex(seed.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                source.SetCell(int3.zero, Cell.Palette(source.FindOrAddPalette(new PaletteEntry { Block = hull })));
                Blueprint.EditorSetPayload(VoxelStructureCodec.Encode(source, VoxsBlockNames.FromCatalog(seed.Catalog), VoxsEncodeOptions.ForAsset), 1, Vector3Int.zero, Vector3Int.zero);
            }
            definitions.Add(Blueprint); definitions.Add(AssetDatabase.LoadAssetAtPath<AiProfileDefinition>("Assets/Game/Content/Ai/ai_skittish.asset"));
            Content = ContentService.CreateForTests(definitions);
            var systems = new List<Type> { typeof(GridSpawnSystem), typeof(GridBodyInitSystem) };
            if (physics)
                systems.AddRange(new[] { typeof(GridSpawnBodyInitSystem), typeof(GridMassUpdateSystem),
                    typeof(BrickDirtyScheduleSystem), typeof(ColliderSnapshotSystem), typeof(BrickColliderBuildSystem),
                    typeof(ColliderSwapSystem), typeof(GridColliderCleanupSystem), typeof(DeferredDisposalSystem),
                    typeof(GameplayEndCommandBufferSystem) });
            if (physics)
                foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                    if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics", StringComparison.Ordinal) || type.Namespace == "Unity.Transforms")) systems.Add(type);
            Test = EcsTestWorld.Create(systems.ToArray()); Test.AddGameplayActive(); Test.AddWorldSceneActive(); Test.AddSimClock();
            Content.CreateSingletons(Em);
            library = new BlueprintLibrary(Content.Catalog, new VoxelContent { Blocks = Content.BlockDefs, Orientations = Content.Orientations });
            registry = VoxelStructureRegistry.CreateRuntime(library); Em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
            Em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 32, DefaultCellSize = .5f, RequireRenderer = 0 });
            Em.CreateSingleton(PhysicsVoxelConfig.Default);
            if (physics) { var step = PhysicsStep.Default; step.Gravity = float3.zero; step.SynchronizeCollisionWorld = 1; Em.CreateSingleton(step); }
            using (var query = Em.CreateEntityQuery(ComponentType.ReadOnly<RootComponentRegistration>()))
            {
                var registrations = query.GetSingletonBuffer<RootComponentRegistration>(false);
                FlightSetup.Register(registrations); CombatGridSetup.Register(registrations); AiSetup.Register(registrations);
            }
            var generation = AssetDatabase.LoadAssetAtPath<GalaxyGenerationDefinition>("Assets/Game/Content/Galaxy/galaxy_main.asset");
            Assert.IsNotNull(generation);
            var galaxyDefinitions = new List<ContentDefinition>(definitions) { generation };
            galaxyDefinitions.AddRange(generation.AuthoredStarSystems);
            foreach (string guid in AssetDatabase.FindAssets("t:BodyTemplateDefinition", new[] { "Assets/Game/Content/Bodies" }))
                galaxyDefinitions.Add(AssetDatabase.LoadAssetAtPath<BodyTemplateDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            var report = new ValidationReport("Dev world galaxy"); var catalog = ContentCatalog.Build(galaxyDefinitions, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            Galaxy = new GalaxyService(catalog, State, Test.World, new GalaxyFrameTicker()); Galaxy.NewGame(7);
            Em.CreateSingleton(new FrameOrigin { StarSystem = State.CurrentStarSystem, OriginInStarSystem = Origin });
            Session = new Game.App.Session.GameSession(Test.World,
                AssetDatabase.LoadAssetAtPath<GameConfigDefinition>("Assets/Game/Content/Config/config_game.asset"));
            Spawner = new ShipSpawnService(Test.World, registry, Content.Catalog, Session, Galaxy);
            var playerId = Spawner.Spawn(new ShipSpawnArgs { Blueprint = Blueprint.Id, IsPlayer = true, Allegiance = Allegiance.Player, Rotation = quaternion.identity });
            Test.StepTicks(1); Assert.IsTrue(Spawner.TryGetRoot(playerId, out Player));
            Assert.IsTrue(Em.HasComponent<VoxelGrid>(Player));
            Host = DevRuntimeHost.Create(null); Host.enabled = false;
            slots.Add(new Slot<ContentService>(Content)); slots.Add(new Slot<GalaxyService>(Galaxy));
            slots.Add(new Slot<ShipSpawnService>(Spawner)); slots.Add(new Slot<DevRuntimeHost>(Host));
        }
        internal DevCommandResult Command(string line) => Host.Commands.Execute(line, Host.Context);
        public void Dispose()
        {
            Em.CompleteAllTrackedJobs(); registry.CancelSpawnReads();
            for (int i = slots.Count - 1; i >= 0; i--) slots[i].Dispose();
            Host.Dispose(); Spawner.Dispose(); Galaxy.Dispose(); State.Dispose(); Session.Dispose();
            Test.Dispose(); registry.Dispose(); Object.DestroyImmediate(registry); library.Dispose(); Content.Dispose(); source.Dispose(); Object.DestroyImmediate(Blueprint);
        }
    }
}
