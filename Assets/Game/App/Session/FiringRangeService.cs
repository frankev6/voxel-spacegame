using System;
using Game.Core;
using Game.Core.Flow;
using Game.Simulation.Commands;
using Unity.Collections;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.Session
{
    /// <summary>FND-020/FND-021 live range; the normal spawn pipeline owns target creation and runtime cells.</summary>
    public sealed class FiringRangeService : ISceneReady, ISceneExiting, IDisposable
    {
        readonly World world; readonly ContentCatalog catalog; readonly VoxelContent content; readonly VoxelStructureRegistry registry;
        readonly IPersistentIdAllocator ids; readonly FiringRangeConfigDefinition config;
        VoxelStructure source; int handle = -1;
        public Entity Target { get; private set; }
        public int SceneExitOrder => SceneHookOrder.PlayerShip;
        public int SceneReadyOrder => SceneHookOrder.PlayerShip + 1;
        public void OnSceneReady(in SceneReadyContext context)
        {
            // QA-001: campaigns no longer get the range station (config off); the sandbox playground's combat pad still does.
            bool sandbox = ids is GameSession session && session.IsSandbox;
            if (context.Scene != GameScene.World || !(config.AutoSpawnInWorld || sandbox)) return;
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalTransform>());
            using var players = query.ToEntityArray(Allocator.Temp);
            if (players.Length == 1) Spawn(players[0]);
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context)
        {
            if (context.Scene != GameScene.World || Target == Entity.Null) return true;
            var em = world.EntityManager;
            if (!em.Exists(Target)) throw new InvalidOperationException("Station spawn request was rejected");
            return em.HasComponent<GridSpawning>(Target) && !em.IsComponentEnabled<GridSpawning>(Target);
        }
        public FiringRangeService(World world, ContentCatalog catalog, VoxelContent content, VoxelStructureRegistry registry, IPersistentIdAllocator ids, FiringRangeConfigDefinition config)
        { this.world = world; this.catalog = catalog; this.content = content; this.registry = registry; this.ids = ids; this.config = config; }
        public Entity Spawn(Entity player)
        {
            if (source != null && world.EntityManager.Exists(Target)) return Target;
            if (source != null) throw new InvalidOperationException("Range already created; start a new game to reset it.");
            var em = world.EntityManager; var playerPose = em.GetComponentData<LocalTransform>(player);
            source = BuildStation(config, catalog, content);
            handle = registry.Register(source);
            try
            {
                Target = GridSpawnApi.RequestSpawn(em, registry, handle, new GridSpawnParams { PersistentId = ids.Allocate(),
                    Pose = new RigidTransform(math.mul(playerPose.Rotation, quaternion.RotateY(math.radians(config.TargetYaw))),
                        playerPose.TransformPoint((float3)config.TargetOffset + new float3(0, 0, BowClearance(em, player)))),
                    Kind = GridKind.Station, Motion = GridMotion.Static, Profile = GridProfile.None });
                return Target;
            }
            catch { Release(); throw; }
        }
        // QA-001 (owner: "its literally overlapping the station wtf??"): the offset was tuned for a fighter's ~8 m bow; a longer
        // ship pushes the station out by the rest of its bow so a cruiser never spawns inside it.
        const float TunedBowMeters = 10;
        float BowClearance(EntityManager em, Entity player)
        {
            float bow = 0;
            if (em.HasComponent<VoxelGrid>(player)) { var grid = em.GetComponentData<VoxelGrid>(player); bow = (grid.CellMax.z + 1) * grid.CellSize; }
            else if (em.HasComponent<GridSpawnRequest>(player))
            {
                var request = em.GetComponentData<GridSpawnRequest>(player);
                var structure = request.StructureHandle >= 0 ? registry.Get(request.StructureHandle) : null;
                if (structure == null && !request.Blueprint.IsNone) registry.TryGetBlueprint(request.Blueprint, out structure);
                if (structure != null && structure.TryGetCellBounds(out _, out var max)) bow = (max.z + 1) * structure.CellSize;
            }
            return math.max(0, bow - TunedBowMeters);
        }
        /// <summary>Owner-requested test structure, built from real content through normal placement.</summary>
        public static VoxelStructure BuildStation(FiringRangeConfigDefinition config, ContentCatalog catalog, VoxelContent content)
        {
            var station = new VoxelStructure(config.TargetCellSize);
            int scale = config.StationScale;
            try
            {
                ushort hull = catalog.IndexOf<BlockDefinition>(config.Hull);
                ushort plate = station.FindOrAddPalette(new PaletteEntry { Block = hull, Color = config.HullColor });
                ushort bridge = station.FindOrAddPalette(new PaletteEntry { Block = hull, Color = config.BridgeColor });
                void Box(int3 min, int3 max, ushort palette, bool shell = false)
                {
                    min *= scale; max = (max + 1) * scale - 1;
                    for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
                    {
                        int3 c = new int3(x, y, z);
                        if (!shell || math.any(c == min) || math.any(c == max)) station.SetCell(c, Cell.Palette(palette));
                    }
                }
                void Place(BlockDefinition block, int3 origin, byte orient = 0)
                {
                    var result = station.TryPlaceBlock(new BlockPlacement { Block = catalog.IndexOf<BlockDefinition>(block), Origin = origin * scale,
                        Orient = orient, Color = config.ModuleColor }, content);
                    if (result != PlaceResult.Ok) throw new InvalidOperationException("Station placement " + block.IdString + ": " + result);
                }
                // Central layered hull bay: a floor and front/back walls, open above so internals remain visible.
                Box(new int3(-6, 0, -6), new int3(6, 0, 6), plate);
                Box(new int3(-6, 1, -6), new int3(6, 4, -5), plate);
                Box(new int3(-6, 1, 5), new int3(6, 3, 6), plate);
                Place(config.Core, new int3(0, 1, 0));
                Place(config.Cargo, new int3(-4, 1, -2));
                Place(config.Reactor, new int3(3, 1, 2));
                // Port pod: one thin bridge. Severing any complete cross-section frees the whole pod.
                Box(new int3(-16, 0, 0), new int3(-7, 0, 0), bridge);
                Box(new int3(-25, 0, -4), new int3(-17, 2, 4), plate, true);
                Place(config.Thruster, new int3(-22, 3, -1));
                // Starboard pod: two independent bridges; breaking only one must NOT detach it.
                Box(new int3(7, 0, -2), new int3(16, 0, -2), bridge);
                Box(new int3(7, 0, 2), new int3(16, 0, 2), bridge);
                Box(new int3(17, 0, -4), new int3(25, 0, 4), plate);
                Place(config.Cargo, new int3(20, 1, -2)); Place(config.Reactor, new int3(23, 1, 2));
                // Rear reactor bay: multi-cell instances, two nearby reactors for blast-chain checks.
                Box(new int3(-1, 0, 7), new int3(1, 0, 15), bridge);
                Box(new int3(-5, 0, 16), new int3(5, 0, 24), plate);
                Place(config.Reactor, new int3(-3, 1, 18)); Place(config.Reactor, new int3(1, 1, 18));
                // Front-facing shape samples stay attached to the central floor; long slopes cross cell boundaries.
                for (int x = -5; x <= 5; x += 2) Place(config.Slope, new int3(x, 1, -4));
                Place(config.LongSlope, new int3(-2, 1, 2));
                station.OptimizeStorage(); return station;
            }
            catch { station.Dispose(); throw; }
        }
        public void OnSceneExiting(in SceneExitContext context) { if (context.Scene == GameScene.World) Release(); }
        void Release()
        {
            if (handle >= 0) registry.Release(handle); handle = -1;
            source?.Dispose(); source = null; Target = Entity.Null;
        }
        public void Dispose() => Release();
    }
}
