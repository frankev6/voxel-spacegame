#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Voxel.Model;
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
    [Flags] public enum DevOverlayFlags : byte { None = 0, Bricks = 1, Dirty = 2, Colliders = 4, All = Bricks | Dirty | Colliders }
    /// <summary>Read-only live inspection after world updates; one bounded mesh and persistent line output per host.</summary>
    public sealed class DevOverlayRenderer : IDisposable
    {
        public DevOverlayFlags Enabled { get; set; }
        public DevLineBatch Lines;
        public bool IsDisposed { get; private set; }
        World queryWorld;
        EntityQuery grids, bricks, dirty, colliders;
        Mesh mesh;
        Material material;
        public DevOverlayRenderer() => Lines = new DevLineBatch(Allocator.Persistent);
        public string Status => "overlays=" + Enabled + " edges=" + (Lines.IsCreated ? Lines.Vertices.Length / 2 : 0) + (Lines.Truncated ? " truncated" : "");
        void Bind(World world)
        {
            if (queryWorld == world) return;
            ReleaseQueries(); queryWorld = world;
            var em = world.EntityManager;
            grids = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalToWorld>());
            bricks = em.CreateEntityQuery(ComponentType.ReadOnly<Brick>());
            dirty = em.CreateEntityQuery(ComponentType.ReadOnly<Brick>(), ComponentType.ReadOnly<BrickDirty>());
            colliders = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<PhysicsCollider>());
        }
        void ReleaseQueries()
        {
            if (queryWorld != null && queryWorld.IsCreated)
            { grids.Dispose(); bricks.Dispose(); dirty.Dispose(); colliders.Dispose(); }
            queryWorld = null;
        }
        public void Gather(World world, float3 playerPosition)
        {
            if (IsDisposed) return;
            Lines.Clear();
            if (Enabled == DevOverlayFlags.None || world == null || !world.IsCreated) return;
            Bind(world); world.EntityManager.CompleteAllTrackedJobs();
            using var roots = grids.ToEntityArray(Allocator.TempJob);
            using var gridData = grids.ToComponentDataArray<VoxelGrid>(Allocator.TempJob);
            using var transforms = grids.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
            using var nearby = new NativeParallelHashMap<Entity, DevOverlayRoot>(math.max(1, roots.Length), Allocator.TempJob);
            for (int i = 0; i < roots.Length; i++)
                if (math.distancesq(transforms[i].Position, playerPosition) <= 300f * 300f)
                    nearby.Add(roots[i], new DevOverlayRoot { Transform = transforms[i].Value, CellSize = gridData[i].CellSize });
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
        }
        public void Draw(DevContext context)
        {
            if (IsDisposed || Enabled == DevOverlayFlags.None) return;
            var world = context.World;
            if (world == null || !world.IsCreated) { Lines.Clear(); return; }
            world.EntityManager.CompleteAllTrackedJobs();
            var player = context.PlayerShipRoot;
            if (player == Entity.Null || !world.EntityManager.HasComponent<LocalTransform>(player)) { Lines.Clear(); return; }
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
            if (IsDisposed) return; IsDisposed = true; ReleaseQueries(); Lines.Dispose();
            if (Application.isPlaying) { UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(material); }
            else { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(material); }
            mesh = null; material = null;
        }
    }
}
#endif
