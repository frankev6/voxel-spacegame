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
            if (context.Scene != GameScene.World || !session.HasActiveSession) return;
            record = session.Fleet.Flagship;
            if (record == null) { GameLog.Error(LogCategories.Flow, "No flagship to spawn"); return; }
            int handle = record.Register(registry);
            try
            {
                // TODO(FND-012): route this request through ShipSpawnService, reusing this record's single registered handle.
                // TODO(GAL-008): bind the canonical player location once GalaxyState/streaming exist.
                root = GridSpawnApi.RequestSpawn(world.EntityManager, registry, handle, new GridSpawnParams
                {
                    PersistentId = record.PersistentId, Pose = RigidTransform.identity, Kind = GridKind.Ship,
                    Motion = GridMotion.Dynamic, Flags = GridSpawnFlags.WriteBack, Profile = GridProfile.PlayerShip
                });
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
