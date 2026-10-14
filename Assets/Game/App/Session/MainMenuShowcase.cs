using Game.Core;
using Game.Core.Flow;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.App.Session
{
    /// <summary>QA-001: the main menu shows the NOCTURNE world instead of an empty void: the player's starter in front of the
    /// menu camera and a BROODMOTHER waiting in the dark behind it (DESIGN-GUIDE §2 "something black and living out in the
    /// dark"). The menu has no gameplay world, so the hulls are meshed once by the real brick mesher and drawn as scene
    /// objects with the Lab's materials; they belong to the MainMenu scene and unload with it.</summary>
    public sealed class MainMenuShowcase : ISceneReady
    {
        readonly VoxelStructureRegistry registry; readonly ContentService content; readonly Material brickMaterial, propMaterial;
        public int SceneReadyOrder => SceneHookOrder.Camera + 1;
        public MainMenuShowcase(VoxelStructureRegistry registry, ContentService content, Material brickMaterial)
        {
            this.registry = registry; this.content = content; this.brickMaterial = brickMaterial;
            foreach (var render in content.Catalog.All<VoxelRenderConfigDefinition>()) { propMaterial = render.PropMaterial; break; }
        }
        public void OnSceneReady(in SceneReadyContext context)
        {
            if (context.Scene != GameScene.MainMenu || brickMaterial == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var root = new GameObject("Main menu showcase");
            try
            {
                // The menu camera orbits the starter at ~48 m (CameraConfig.MenuPosition); the BROODMOTHER waits on the opening
                // sightline beyond it and comes back into frame once per orbit.
                Show(root.transform, "ship_fighter", new Vector3(0, 0, 0), 205);
                Show(root.transform, "ship_xeno_broodmother", new Vector3(30, -24, 190), 120);
            }
            catch (System.Exception e) { GameLog.Exception(LogCategories.Flow, e, "Main menu showcase"); }
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context) => true;

        [BurstCompile]
        struct MeshBrickJob : IJob
        {
            [ReadOnly] public VoxelStructureData Data;
            [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
            [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
            [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
            public int3 Coord;
            public NativeArray<uint> Snapshot;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public void Execute()
            {
                BrickSnapshotUtility.FromStructure(Data, Coord, ref Blocks.Value, ref Orientations.Value, Snapshot);
                BrickMesher.MeshLod0(Snapshot, ref Shapes.Value, Vertices, Indices, out _, out _);
            }
        }

        void Show(Transform parent, string blueprint, Vector3 position, float yawDeg)
        {
            if (!content.Catalog.TryGet<ShipBlueprintDefinition>(new ContentId(blueprint), out var definition) ||
                !registry.TryGetBlueprint(definition.Id.Hash, out var structure)) return;
            float cell = structure.CellSize;
            var ship = new GameObject(blueprint).transform;
            ship.SetParent(parent, false); ship.SetPositionAndRotation(position, Quaternion.Euler(0, yawDeg, 0)); ship.localScale = Vector3.one * cell;
            // The brick shader plates the hull in grid cells; this ship's world-to-cells matrix keeps the plating continuous.
            var cells = new MaterialPropertyBlock();
            cells.SetMatrix("_DraftWorldToCells", ship.worldToLocalMatrix); cells.SetFloat("_DraftCellsValid", 1);
            var shapes = BrickShapeTablesBuilder.Build(ref content.Orientations.Value);
            var snapshot = new NativeArray<uint>(MeshCell.SnapCells, Allocator.TempJob);
            var vertices = new NativeList<PackedBrickVertex>(4096, Allocator.TempJob); var indices = new NativeList<uint>(8192, Allocator.TempJob);
            var coords = structure.Data.BrickIndex.GetKeyArray(Allocator.Temp);
            try
            {
                foreach (var coord in coords)
                {
                    new MeshBrickJob { Data = structure.Data, Blocks = content.BlockDefs, Orientations = content.Orientations, Shapes = shapes,
                        Coord = coord, Snapshot = snapshot, Vertices = vertices, Indices = indices }.Run();
                    if (indices.Length == 0) continue;
                    var mesh = new Mesh { name = blueprint + " brick " + coord };
                    mesh.SetVertexBufferParams(vertices.Length, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.UNorm8, 4),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm8, 4));
                    mesh.SetVertexBufferData(vertices.AsArray(), 0, 0, vertices.Length);
                    mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32); mesh.SetIndexBufferData(indices.AsArray(), 0, 0, indices.Length);
                    mesh.subMeshCount = 1;
                    mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length) { vertexCount = vertices.Length }, MeshUpdateFlags.DontRecalculateBounds);
                    mesh.bounds = new Bounds(Vector3.one * 8, Vector3.one * 18);
                    var brick = new GameObject("brick " + coord, typeof(MeshFilter), typeof(MeshRenderer));
                    brick.transform.SetParent(ship, false); brick.transform.localPosition = (Vector3)(float3)(coord * 16);
                    brick.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = brick.GetComponent<MeshRenderer>(); renderer.sharedMaterial = brickMaterial; renderer.SetPropertyBlock(cells);
                }
                if (propMaterial == null) return;
                var instances = structure.Data.Instances;
                for (int i = 0; i < instances.Length; i++)
                {
                    var instance = instances[i];
                    if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0) continue;
                    ref var def = ref content.BlockDefs.Value.Blocks[instance.Block];
                    if ((uint)def.PropMeshIndex >= (uint)content.PropMeshes.Count) continue;
                    var prop = new GameObject("prop " + i, typeof(MeshFilter), typeof(MeshRenderer));
                    prop.transform.SetParent(ship, false);
                    prop.transform.localPosition = (Vector3)((float3)instance.Origin + .5f);
                    prop.transform.localRotation = content.Orientations.Value.Rotations[instance.Orient];
                    prop.GetComponent<MeshFilter>().sharedMesh = content.PropMeshes[def.PropMeshIndex];
                    var renderer = prop.GetComponent<MeshRenderer>(); renderer.sharedMaterial = propMaterial;
                    var layer = new MaterialPropertyBlock(); layer.SetFloat("_PropLayer", def.MaterialLayer); renderer.SetPropertyBlock(layer);
                }
            }
            finally { coords.Dispose(); snapshot.Dispose(); vertices.Dispose(); indices.Dispose(); shapes.Dispose(); }
        }
    }
}
