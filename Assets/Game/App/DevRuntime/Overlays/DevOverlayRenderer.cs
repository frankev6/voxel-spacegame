#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core.Components;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Voxel.Model;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Voxel.PhysicsBody;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;
namespace Game.App.DevRuntime.Overlays
{
    [Flags] public enum DevOverlayFlags : byte { None = 0, Bricks = 1, Dirty = 2, Colliders = 4, Islands = 8, Spatial = 16, Interaction = 32, All = Bricks | Dirty | Colliders | Islands | Spatial | Interaction }
    /// <summary>Read-only live inspection after world updates; one bounded mesh and persistent line output per host.</summary>
    public sealed class DevOverlayRenderer : IDisposable
    {
        DevOverlayFlags enabled;
        ulong islandTargetId;
        readonly IslandOverlayCache islands = new();
        string spatialStatus = "spatial hash unavailable";
        public DevOverlayFlags Enabled
        {
            get => enabled;
            set { if (((enabled ^ value) & DevOverlayFlags.Islands) != 0) islands.Invalidate(); enabled = value; }
        }
        public ulong IslandTargetId
        {
            get => islandTargetId;
            set { if (islandTargetId != value) islands.Clear(); islandTargetId = value; }
        }
        public DevLineBatch Lines;
        public bool IsDisposed { get; private set; }
        World queryWorld;
        EntityQuery grids, bricks, dirty, colliders, spatial, interactables, interactionTarget;
        Mesh mesh;
        Material material;
        public DevOverlayRenderer() => Lines = new DevLineBatch(Allocator.Persistent);
        public string Status => "overlays=" + Enabled + " edges=" + (Lines.IsCreated ? Lines.Vertices.Length / 2 : 0) + (Lines.Truncated ? " truncated" : "")
            + ((Enabled & DevOverlayFlags.Islands) != 0 ? "; " + islands.Status : "")
            + ((Enabled & DevOverlayFlags.Spatial) != 0 ? "; " + spatialStatus : "");
        void Bind(World world)
        {
            if (queryWorld == world) return;
            ReleaseQueries(); queryWorld = world;
            var em = world.EntityManager;
            grids = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalToWorld>());
            bricks = em.CreateEntityQuery(ComponentType.ReadOnly<Brick>());
            dirty = em.CreateEntityQuery(ComponentType.ReadOnly<Brick>(), ComponentType.ReadOnly<BrickDirty>());
            colliders = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<PhysicsCollider>());
            spatial = em.CreateEntityQuery(ComponentType.ReadOnly<SpatialHash>());
            interactables = em.CreateEntityQuery(ComponentType.ReadOnly<Interactable>(), ComponentType.ReadOnly<LocalTransform>());
            interactionTarget = em.CreateEntityQuery(ComponentType.ReadOnly<InteractionTarget>());
        }
        void ReleaseQueries()
        {
            if (queryWorld != null && queryWorld.IsCreated)
            { grids.Dispose(); bricks.Dispose(); dirty.Dispose(); colliders.Dispose(); spatial.Dispose(); interactables.Dispose(); interactionTarget.Dispose(); }
            queryWorld = null;
            islands.Clear(); spatialStatus = "spatial hash unavailable";
        }
        public void Gather(World world, float3 playerPosition)
        {
            if (IsDisposed) return;
            Lines.Clear();
            if (world == null || !world.IsCreated) { ReleaseQueries(); return; }
            if (Enabled == DevOverlayFlags.None) return;
            Bind(world); world.EntityManager.CompleteAllTrackedJobs();
            if ((Enabled & DevOverlayFlags.Interaction) != 0) GatherInteraction(world.EntityManager, playerPosition);
            using var roots = grids.ToEntityArray(Allocator.TempJob);
            using var gridData = grids.ToComponentDataArray<VoxelGrid>(Allocator.TempJob);
            using var transforms = grids.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
            using var nearby = new NativeParallelHashMap<Entity, DevOverlayRoot>(math.max(1, roots.Length), Allocator.TempJob);
            for (int i = 0; i < roots.Length; i++)
                if (math.distancesq(transforms[i].Position, playerPosition) <= 300f * 300f)
                    nearby.Add(roots[i], new DevOverlayRoot { Transform = transforms[i].Value, CellSize = gridData[i].CellSize });
            if ((Enabled & DevOverlayFlags.Islands) != 0)
            {
                var em = world.EntityManager; bool found = false;
                for (int i = 0; i < roots.Length; i++)
                {
                    bool matches = IslandTargetId == 0 ? em.HasComponent<PlayerShip>(roots[i]) :
                        em.HasComponent<PersistentId>(roots[i]) && em.GetComponentData<PersistentId>(roots[i]).Value == IslandTargetId;
                    if (!matches) continue;
                    islands.Gather(em, roots[i], gridData[i], transforms[i].Value, Lines); found = true; break;
                }
                if (!found) islands.Clear();
            }
            if ((Enabled & DevOverlayFlags.Bricks) != 0)
            {
                using var data = bricks.ToComponentDataArray<Brick>(Allocator.TempJob);
                new BrickOverlayJob { Bricks = data, Roots = nearby, Lines = Lines }.Run();
            }
            if ((Enabled & DevOverlayFlags.Dirty) != 0)
            {
                using var data = dirty.ToComponentDataArray<Brick>(Allocator.TempJob);
                using var flags = dirty.ToComponentDataArray<BrickDirty>(Allocator.TempJob);
                new DirtyOverlayJob { Bricks = data, Dirty = flags, Roots = nearby, Lines = Lines }.Run();
            }
            if ((Enabled & DevOverlayFlags.Colliders) != 0)
            {
                using var entities = colliders.ToEntityArray(Allocator.TempJob);
                using var data = colliders.ToComponentDataArray<PhysicsCollider>(Allocator.TempJob);
                new ColliderOverlayJob { Entities = entities, Colliders = data, Roots = nearby, Lines = Lines }.Run();
            }
            if ((Enabled & DevOverlayFlags.Spatial) != 0)
            {
                spatialStatus = "spatial hash unavailable";
                if (spatial.CalculateEntityCount() == 1)
                {
                    var hash = spatial.GetSingleton<SpatialHash>();
                    int start = Lines.Vertices.Length;
                    var handle = new SpatialHashOverlayJob { Hash = hash.AsReadOnly(), PlayerPosition = playerPosition, Lines = Lines }
                        .Schedule(hash.BeginRead(default));
                    hash.EndRead(handle); handle.Complete();
                    spatialStatus = "spatial cells=" + (Lines.Vertices.Length - start) / 8;
                }
            }
        }
        void GatherInteraction(EntityManager em, float3 playerPosition)
        {
            using var entities = interactables.ToEntityArray(Allocator.TempJob);
            using var data = interactables.ToComponentDataArray<Interactable>(Allocator.TempJob);
            using var transforms = interactables.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            using var storage = new NativeArray<InteractionOverlayItem>(entities.Length, Allocator.TempJob);
            var items = storage;
            for (int i = 0; i < entities.Length; i++)
            {
                Entity body = entities[i]; var center = transforms[i].Position; float radius = 0, range = data[i].DistanceM;
                if (!em.HasComponent<StarSystemBody>(body) && em.HasComponent<BodyGridOwner>(body))
                    body = em.GetComponentData<BodyGridOwner>(body).Anchor;
                if (em.Exists(body) && em.HasComponent<StarSystemBody>(body))
                {
                    var spec = em.GetComponentData<StarSystemBody>(body); radius = math.max(0, spec.RadiusM);
                    if ((spec.Flags & BodyFlags.Destroyed) != 0) range = -1;
                    if (em.HasComponent<LocalTransform>(body)) center = em.GetComponentData<LocalTransform>(body).Position;
                }
                items[i] = new InteractionOverlayItem { Center = center, SurfaceRadius = radius, Range = range };
            }
            bool found = interactionTarget.TryGetSingleton<InteractionTarget>(out var target) && target.Target != Entity.Null &&
                em.Exists(target.Target) && em.HasComponent<LocalTransform>(target.Target);
            new InteractionOverlayJob { Items = items, Player = playerPosition, HasTarget = found, InRange = target.InRange != 0,
                Target = found ? em.GetComponentData<LocalTransform>(target.Target).Position : default,
                ReservedVertices = (Enabled & DevOverlayFlags.Islands) != 0 ? 24 : 0, Lines = Lines }.Run();
        }
        public void Draw(DevContext context)
        {
            if (IsDisposed || Enabled == DevOverlayFlags.None) return;
            var world = context.World;
            if (world == null || !world.IsCreated) { ReleaseQueries(); Lines.Clear(); return; }
            world.EntityManager.CompleteAllTrackedJobs();
            var player = context.PlayerShipRoot;
            if (player == Entity.Null || !world.EntityManager.HasComponent<LocalTransform>(player))
            { islands.Clear(); spatialStatus = "spatial player unavailable"; Lines.Clear(); return; }
            Gather(world, world.EntityManager.GetComponentData<LocalTransform>(player).Position);
            int count = Lines.Vertices.Length;
            var camera = context.GameplayCamera;
            if (count == 0 || camera == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            if (mesh == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) { Game.Core.GameLog.WarnOnce("DEV.Overlay.Shader", "Dev", "Overlay shader unavailable."); return; }
                material = new Material(shader) { name = "DevOverlay", hideFlags = HideFlags.HideAndDontSave };
                material.SetInt("_ZTest", (int)CompareFunction.Always); material.SetInt("_ZWrite", 0); material.SetInt("_Cull", (int)CullMode.Off);
                mesh = new Mesh { name = "DevOverlayLines", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave }; mesh.MarkDynamic();
                mesh.SetVertexBufferParams(DevLineBatch.MaxVertices, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4));
                mesh.SetIndexBufferParams(DevLineBatch.MaxVertices, IndexFormat.UInt32);
                var indices = new NativeArray<uint>(DevLineBatch.MaxVertices, Allocator.Temp);
                try
                {
                    for (int i = 0; i < indices.Length; i++) indices[i] = (uint)i;
                    mesh.SetIndexBufferData(indices, 0, 0, indices.Length); mesh.subMeshCount = 1;
                }
                finally { indices.Dispose(); }
            }
            var vertices = Lines.Vertices.AsArray();
            float3 min = vertices[0].Position, max = min;
            for (int i = 1; i < count; i++) { min = math.min(min, vertices[i].Position); max = math.max(max, vertices[i].Position); }
            var bounds = new Bounds((min + max) * .5f, math.max(max - min, new float3(.01f)));
            mesh.SetVertexBufferData(vertices, 0, 0, count, 0, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, count, MeshTopology.Lines) { bounds = bounds, vertexCount = count }, MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = bounds;
            var parameters = new RenderParams(material) { camera = camera, worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMesh(parameters, mesh, 0, Matrix4x4.identity);
        }
        public void Dispose()
        {
            if (IsDisposed) return; IsDisposed = true; ReleaseQueries(); islands.Dispose(); Lines.Dispose();
            if (Application.isPlaying) { UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(material); }
            else { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(material); }
            mesh = null; material = null;
        }
    }
}
#endif
