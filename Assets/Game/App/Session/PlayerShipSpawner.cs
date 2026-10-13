using System;
using Game.Core;
using Game.Core.Flow;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.App.Session
{
    /// <summary>World-ready player spawn and registry borrow lifecycle (SPEC-01 §5.7; Q19 M1 path).</summary>
    public sealed class PlayerShipSpawner : ISceneReady, ISceneExiting, IDisposable
    {
        readonly GameSession session;
        readonly World world;
        readonly VoxelStructureRegistry registry;
        FleetShipRecord record;
        Entity root;
        internal PlayerShipSpawner(GameSession session, World world, VoxelStructureRegistry registry)
        { this.session = session; this.world = world; this.registry = registry; }
        public int SceneReadyOrder => SceneHookOrder.PlayerShip;
        public int SceneExitOrder => SceneHookOrder.PlayerShip;
        public void OnSceneReady(in SceneReadyContext context)
        {
            if (context.Scene != GameScene.World || !session.HasActiveSession || !session.SpawnPlayerInWorld) return;
            record = session.Fleet.Flagship;
            if (record == null) { GameLog.Error(LogCategories.Flow, "No flagship to spawn"); return; }
            int handle = record.Register(registry);
            try
            {
                if (Game.Core.Services.TryGet<Game.App.Spawning.ShipSpawnService>(out var spawner))
                {
                    float3 position = float3.zero; quaternion rotation = quaternion.identity;
                    if (!session.IsSandbox && Game.Core.Services.TryGet<Game.Galaxy.Services.GalaxyService>(out var galaxy))
                    {
                        if (!galaxy.State.HasPlayerLocation)
                        {
                            // World's restored SimClock exists now; orbit placement uses the saved time.
                            galaxy.State.PlayerLocation = galaxy.GetStartLocation(); galaxy.State.HasPlayerLocation = true;
                        }
                        using var origins = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Galaxy.Components.FrameOrigin>());
                        if (origins.IsEmptyIgnoreFilter) throw new InvalidOperationException("Player placement requires FrameOrigin.");
                        var location = galaxy.State.PlayerLocation;
                        position = MathD.ToLocal(location.PositionM, origins.GetSingleton<Game.Galaxy.Components.FrameOrigin>().OriginInStarSystem);
                        rotation = Planar.YawRotation(location.YawRad);
                    }
                    var id = spawner.SpawnRegistered(new Game.App.Spawning.ShipSpawnArgs
                    {
                        Structure = record.Structure, PersistentId = record.PersistentId, Position = position,
                        Rotation = rotation, IsPlayer = true, Allegiance = Game.Simulation.Allegiance.Player
                    }, handle);
                    if (!spawner.TryGetRoot(id, out root)) throw new InvalidOperationException("Player spawn request was rejected");
                }
                else
                {
                    // Retain the standalone M1 path until the real galaxy composition is installed.
                    root = GridSpawnApi.RequestSpawn(world.EntityManager, registry, handle, new GridSpawnParams
                    {
                        PersistentId = record.PersistentId, Pose = RigidTransform.identity, Kind = GridKind.Ship,
                        Motion = GridMotion.Dynamic, Flags = GridSpawnFlags.WriteBack, Profile = GridProfile.PlayerShip
                    });
                    world.EntityManager.AddComponentData(root, Game.Simulation.Allegiance.Player);
                }
            }
            catch { Release(); throw; }
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context)
        {
            if (context.Scene != GameScene.World || root == Entity.Null) return true;
            var em = world.EntityManager;
            if (!em.Exists(root)) throw new InvalidOperationException("Player spawn request was rejected");
            return em.HasComponent<GridSpawning>(root) && !em.IsComponentEnabled<GridSpawning>(root) && em.HasComponent<PhysicsCollider>(root);
        }
        internal bool TryGetRoot(out Entity entity)
        {
            entity = root;
            return world.IsCreated && root != Entity.Null && world.EntityManager.Exists(root);
        }
        public void OnSceneExiting(in SceneExitContext context) { if (context.Scene == GameScene.World) Release(); }
        internal void Release() { record?.Release(); record = null; root = Entity.Null; }
        public void Dispose() => Release();
    }
}
