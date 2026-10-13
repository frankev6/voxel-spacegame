using System;
using Game.Core;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6. Spawn, rebuild, despawn and edit requests for managed callers (App, Galaxy, ShipEditor, tests).</summary>
    public static class GridSpawnApi
    {
        public static Entity RequestSpawn(EntityManager em, VoxelStructureRegistry registry, int structureHandle, in GridSpawnParams p)
        {
            var source = Resolve(registry, structureHandle);
            int token = registry.PinSpawnRead(source, structureHandle, default); Entity entity = Entity.Null;
            try
            {
                entity = em.CreateEntity(typeof(GridSpawnRequest), typeof(SpawnReadToken));
                em.SetComponentData(entity, Request(default, structureHandle, p));
                em.SetComponentData(entity, new SpawnReadToken { Value = token }); registry.BindRead(token, entity);
                return entity;
            }
            catch { registry.EndRead(token); if (entity != Entity.Null && em.Exists(entity)) em.DestroyEntity(entity); throw; }
        }
        public static Entity RequestSpawn(EntityCommandBuffer ecb, VoxelStructureRegistry registry, int structureHandle, in GridSpawnParams p)
        {
            var source = Resolve(registry, structureHandle); int token = registry.PinSpawnRead(source, structureHandle, default);
            try
            {
                Entity entity = ecb.CreateEntity(); ecb.AddComponent(entity, Request(default, structureHandle, p));
                ecb.AddComponent(entity, new SpawnReadToken { Value = token }); return entity;
            }
            catch { registry.EndRead(token); throw; }
        } // BeginInitialization ECB
        public static Entity RequestSpawnBlueprint(EntityManager em, ContentHash blueprint, in GridSpawnParams p)
        {
            Entity entity = em.CreateEntity(typeof(GridSpawnRequest)); em.SetComponentData(entity, Request(blueprint, -1, p)); return entity;
        }     // WriteBack is cleared
        public static Entity RequestSpawnBlueprint(EntityCommandBuffer ecb, ContentHash blueprint, in GridSpawnParams p)
        {
            Entity entity = ecb.CreateEntity(); ecb.AddComponent(entity, Request(blueprint, -1, p)); return entity;
        }
        static VoxelStructure Resolve(VoxelStructureRegistry registry, int handle)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var source = registry.Get(handle);
            if (source == null || !source.Data.IsCreated) throw new ArgumentException("Spawn handle must refer to a live structure", nameof(handle));
            return source;
        }
        static GridSpawnRequest Request(ContentHash blueprint, int handle, in GridSpawnParams p) => new GridSpawnRequest
        {
            Blueprint = blueprint, StructureHandle = handle, PersistentId = p.PersistentId, Pose = p.Pose,
            LinearVelocity = p.LinearVelocity, AngularVelocity = p.AngularVelocity, Kind = p.Kind, Motion = p.Motion,
            Flags = handle < 0 ? p.Flags & ~GridSpawnFlags.WriteBack : p.Flags, Profile = p.Profile
        };
        public static void RequestRebuild(EntityManager em, VoxelStructureRegistry registry, Entity root, int structureHandle, GridSpawnFlags flags)
        {
            if (!em.Exists(root) || !em.HasComponent<VoxelGrid>(root)) throw new ArgumentException("Rebuild requires a live grid", nameof(root));
            var source = Resolve(registry, structureHandle);
            int previous = em.HasComponent<RebuildReadToken>(root) ? em.GetComponentData<RebuildReadToken>(root).Value : 0;
            int token = registry.PinSpawnRead(source, structureHandle, default);
            try
            {
                var request = new GridRebuildRequest { StructureHandle = structureHandle, Flags = flags };
                if (em.HasComponent<GridRebuildRequest>(root)) em.SetComponentData(root, request); else em.AddComponentData(root, request);
                var read = new RebuildReadToken { Value = token };
                if (em.HasComponent<RebuildReadToken>(root)) em.SetComponentData(root, read); else em.AddComponentData(root, read);
                registry.BindRead(token, root, true); registry.EndRead(previous);
            }
            catch { registry.EndRead(token); throw; }
        }
        public static void RequestDespawn(EntityManager em, Entity root) { em.SetComponentEnabled<GridDespawnRequest>(root, true); }          // enables GridDespawnRequest
        /// <summary>Queues changed coordinates on a live registered editor preview; repeated calls coalesce in the spawn owner.</summary>
        public static void RequestEdit(EntityManager em, Entity root, NativeArray<int3> changedBrickCoords)
        {
            if (!em.Exists(root) || !em.HasComponent<VoxelGrid>(root) || !em.HasComponent<GridSource>(root)
                || !em.HasComponent<GridEditRequest>(root) || !em.HasBuffer<GridEditedBrick>(root))
                throw new ArgumentException("Edit requires a live preview grid", nameof(root));
            var source = em.GetComponentData<GridSource>(root);
            if ((source.Flags & GridSpawnFlags.EditorPreview) == 0 || source.StructureHandle < 0 || !source.Blueprint.IsNone)
                throw new ArgumentException("Edit requires a registered EditorPreview source", nameof(root));
            if (em.IsComponentEnabled<GridSpawning>(root) || em.HasComponent<GridRebuildRequest>(root)
                || em.IsComponentEnabled<GridDespawnRequest>(root))
                throw new InvalidOperationException("Wait for preview spawn, rebuild or despawn before editing");
            if (!changedBrickCoords.IsCreated) throw new ArgumentException("Changed coordinates must be allocated", nameof(changedBrickCoords));
            if (changedBrickCoords.Length == 0) return;
            var changed = em.GetBuffer<GridEditedBrick>(root);
            foreach (var coord in changedBrickCoords) changed.Add(new GridEditedBrick { Coord = coord });
            em.SetComponentEnabled<GridEditRequest>(root, true);
        }
    }
}
