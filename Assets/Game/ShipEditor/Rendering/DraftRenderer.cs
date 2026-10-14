using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.ShipEditor.Rendering
{
    /// <summary>SPEC-51 §5.13: snapshot scheduling and reused draft meshes; borrows session/content/materials.</summary>
    public sealed class DraftRenderer : IDisposable
    {
        readonly EditSession session;
        readonly EditorContext content;
        readonly Dictionary<int3, BrickRenderSlot> slots = new();
        readonly Dictionary<int, InstancedPropSet> props = new();
        readonly IReadOnlyList<Mesh> propMeshes;
        readonly HashSet<int3> dirty = new();
        readonly List<int3> ordered = new();
        readonly List<BrickRenderSlot> asynchronous = new(), synchronous = new();
        readonly ProfilerMarker remesh = new("DraftRenderer.Remesh"), draw = new("DraftRenderer.Draw");
        NativeList<int3> incoming = new(Allocator.Persistent);
        BlobAssetReference<BrickShapeTables> shapes;
        readonly Material brickMaterial, xrayMaterial, propMaterial;
        // Below the 1,023 API ceiling and within the shader's conventional 500-instance buffer.
        readonly Matrix4x4[] drawMatrices = new Matrix4x4[500];
        readonly float[] drawPaint = new float[500];
        readonly MaterialPropertyBlock propProperties = new();
        readonly MaterialPropertyBlock brickProperties = new();
        bool disposed, sliceEnabled;
        bool rebuildProps;
        int sliceLevel;
        public Matrix4x4 GridToWorld { get; set; } = Matrix4x4.identity;
        public float3 CameraPivotCells { get; set; }
        public bool XRay { get; set; }
        public int RemeshCount { get; private set; }
        internal int PendingJobs => asynchronous.Count;
        internal IReadOnlyDictionary<int3, BrickRenderSlot> Slots => slots;
        internal IReadOnlyDictionary<int, InstancedPropSet> Props => props;
        public DraftRenderer(EditSession session, EditorContext content, Material brickMaterial, Material xrayMaterial,
            IReadOnlyList<Mesh> propMeshes = null, Material propMaterial = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.brickMaterial = brickMaterial; this.xrayMaterial = xrayMaterial;
            this.propMaterial = propMaterial;
            this.propMeshes = propMeshes ?? Array.Empty<Mesh>();
            try
            {
                shapes = BrickShapeTablesBuilder.Build(ref content.Orientations.Value);
                // Includes existing content even when another preview previously drained the session's dirty queue.
                foreach (var brick in session.Draft.Data.Bricks) if (brick.SolidCount != 0) dirty.Add(brick.Coord);
                RebuildProps();
                session.DraftApplied += OnDraftApplied;
                session.Changed += OnSessionChanged;
            }
            catch { Dispose(); throw; }
        }
        void RebuildProps()
        {
            foreach (var set in props.Values) set.Clear();
            using var records = new NativeList<BlockRecord>(Allocator.TempJob);
            new DraftReader(session.Draft, content).Enumerate(records);
            foreach (var record in records) UpdateProp(record, true);
            rebuildProps = false;
        }
        void UpdateProp(BlockRecord record, bool add)
        {
            ref var definition = ref content.Definitions.Value.Blocks[record.Block];
            int mesh = definition.PropMeshIndex;
            if (definition.Kind == BlockKind.Structural || mesh < 0) return;
            if (mesh >= propMeshes.Count || propMeshes[mesh] == null)
                throw new InvalidOperationException("EDT: draft prop mesh table does not match content.");
            if (!props.TryGetValue(mesh, out var set))
            {
                if (!add) throw new InvalidOperationException("EDT: missing draft prop instance set.");
                set = new InstancedPropSet(propMeshes[mesh]); props.Add(mesh, set);
            }
            if (add) set.Add(record, content, session.Draft.CellSize);
            else if (!set.Remove(record)) throw new InvalidOperationException("EDT: missing draft prop identity.");
        }
        void OnDraftApplied(DiffCommand command, bool forward)
        {
            if (rebuildProps) return;
            try
            {
                var ops = command.Ops;
                for (int n = 0; n < ops.Length; n++)
                {
                    var op = ops[forward ? n : ops.Length - 1 - n];
                    UpdateProp(op.Record, (op.Kind == DiffOpKind.Add) == forward);
                }
            }
            catch (Exception error)
            {
                // The draft already changed: never prevent the corresponding history cursor update.
                rebuildProps = true; Debug.LogException(error);
            }
        }
        void OnSessionChanged(EditChange change)
        {
            if (change.Kind == EditChangeKind.Discarded) rebuildProps = true;
        }
        public void SetSlice(bool enabled, int level)
        {
            CheckAlive(); if (sliceEnabled == enabled && sliceLevel == level) return;
            foreach (var entry in slots)
            {
                long lo = (long)entry.Key.y * 16, hi = lo + 15;
                if (sliceEnabled && sliceLevel >= lo && sliceLevel <= hi || enabled && level >= lo && level <= hi)
                    dirty.Add(entry.Key);
            }
            sliceEnabled = enabled; sliceLevel = level;
        }
        internal void UpdateMeshes()
        {
            CheckAlive(); using var marker = remesh.Auto();
            if (rebuildProps) RebuildProps();
            // Every outstanding snapshot is joined next frame. Later edits remain in the session dirty queue.
            foreach (var slot in asynchronous) slot.CompleteAndApply();
            asynchronous.Clear();
            session.DrainDirtyBricks(incoming); foreach (var brick in incoming) dirty.Add(brick);
            ordered.Clear();
            foreach (var brick in dirty)
                if (!sliceEnabled || (long)brick.y * 16 <= sliceLevel) ordered.Add(brick);
            ordered.Sort(CompareDistance);
            int syncCount = math.min(math.max(0, content.Config.SyncRemeshBricksPerFrame), ordered.Count);
            int asyncCount = math.min(math.max(0, content.Config.AsyncRemeshBricksPerFrame), ordered.Count - syncCount);
            synchronous.Clear();
            for (int i = 0; i < syncCount + asyncCount; i++)
            {
                int3 brick = ordered[i];
                if (!slots.TryGetValue(brick, out var slot)) { slot = new BrickRenderSlot(brick); slots.Add(brick, slot); }
                slot.Schedule(session, content, shapes, sliceEnabled ? sliceLevel : int.MaxValue);
                dirty.Remove(brick); RemeshCount++;
                if (i < syncCount) synchronous.Add(slot); else asynchronous.Add(slot);
            }
            using var handles = new NativeArray<JobHandle>(synchronous.Count, Allocator.Temp);
            var writableHandles = handles;
            for (int i = 0; i < synchronous.Count; i++) writableHandles[i] = synchronous[i].Handle;
            JobHandle.CompleteAll(handles);
            foreach (var slot in synchronous) slot.CompleteAndApply();
            synchronous.Clear();
        }
        int CompareDistance(int3 a, int3 b)
        {
            double da = math.lengthsq((double3)a * 16 + 8 - (double3)CameraPivotCells);
            double db = math.lengthsq((double3)b * 16 + 8 - (double3)CameraPivotCells);
            int order = da.CompareTo(db); if (order != 0) return order;
            order = a.x.CompareTo(b.x); if (order != 0) return order;
            order = a.y.CompareTo(b.y); return order != 0 ? order : a.z.CompareTo(b.z);
        }
        public void Render(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            UpdateMeshes(); using var marker = draw.Auto();
            var material = XRay ? xrayMaterial : brickMaterial;
            if (material == null) throw new InvalidOperationException("EDT: draft rendering requires its configured shared material.");
            float cellSize = session.Draft.CellSize;
            // QA-001: the brick shader plates the hull in grid cells; per-brick meshes carry brick-local positions only.
            brickProperties.SetMatrix("_DraftWorldToCells", Matrix4x4.Scale(Vector3.one / cellSize) * GridToWorld.inverse);
            brickProperties.SetFloat("_DraftCellsValid", 1);
            var parameters = new RenderParams(material) { camera = camera, matProps = brickProperties, lightProbeUsage = LightProbeUsage.BlendProbes, reflectionProbeUsage = ReflectionProbeUsage.BlendProbes };
            foreach (var entry in slots)
            {
                var slot = entry.Value;
                if (slot.Empty || sliceEnabled && (long)entry.Key.y * 16 > sliceLevel) continue;
                var local = Matrix4x4.TRS((Vector3)((float3)entry.Key * (16 * cellSize)), Quaternion.identity, Vector3.one * cellSize);
                var world = GridToWorld * local;
                parameters.worldBounds = TransformBounds(slot.Mesh.bounds, world);
                Graphics.RenderMesh(parameters, slot.Mesh, 0, world);
            }
            DrawProps(camera, cellSize);
        }
        void DrawProps(Camera camera, float cellSize)
        {
            propProperties.SetFloat("_DraftSliceEnabled", sliceEnabled ? 1 : 0);
            propProperties.SetFloat("_DraftSliceTop", (float)((double)sliceLevel + 1));
            propProperties.SetMatrix("_DraftWorldToCells", Matrix4x4.Scale(Vector3.one / cellSize) * GridToWorld.inverse);
            foreach (var set in props.Values)
            {
                if (set.Count == 0) continue;
                if (propMaterial == null || !propMaterial.enableInstancing)
                    throw new InvalidOperationException("EDT: draft props require a shared instancing-enabled material.");
                int count = 0; Bounds bounds = default;
                var records = set.Records; var matrices = set.Matrices;
                for (int i = 0; i < set.Count; i++)
                {
                    var record = records[i];
                    if (sliceEnabled && (long)record.Origin.y + content.Footprints.Get(record.Block, record.Orient).Min.y > sliceLevel) continue;
                    var matrix = GridToWorld * matrices[i];
                    var instanceBounds = TransformBounds(set.Mesh.bounds, matrix);
                    if (count == 0) bounds = instanceBounds; else bounds.Encapsulate(instanceBounds);
                    drawMatrices[count] = matrix; drawPaint[count] = record.Color; count++;
                    if (count == drawMatrices.Length) { FlushProps(camera, set.Mesh, count, bounds); count = 0; }
                }
                if (count != 0) FlushProps(camera, set.Mesh, count, bounds);
            }
        }
        void FlushProps(Camera camera, Mesh mesh, int count, Bounds bounds)
        {
            propProperties.SetFloatArray("_DraftPaintIndex", drawPaint);
            var parameters = new RenderParams(propMaterial) { camera = camera, matProps = propProperties, worldBounds = bounds, lightProbeUsage = LightProbeUsage.BlendProbes, reflectionProbeUsage = ReflectionProbeUsage.BlendProbes };
            Graphics.RenderMeshInstanced(parameters, mesh, 0, drawMatrices, count);
        }
        static Bounds TransformBounds(Bounds local, Matrix4x4 transform)
        {
            var x = transform.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            var y = transform.MultiplyVector(new Vector3(0, local.extents.y, 0));
            var z = transform.MultiplyVector(new Vector3(0, 0, local.extents.z));
            var extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(transform.MultiplyPoint3x4(local.center), extent * 2);
        }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(DraftRenderer)); }
        public void Dispose()
        {
            if (disposed) return;
            session.DraftApplied -= OnDraftApplied; session.Changed -= OnSessionChanged;
            foreach (var set in props.Values) set.Dispose(); props.Clear();
            foreach (var slot in slots.Values) slot.Dispose(); slots.Clear(); asynchronous.Clear(); synchronous.Clear();
            if (shapes.IsCreated) shapes.Dispose(); if (incoming.IsCreated) incoming.Dispose();
            dirty.Clear(); ordered.Clear(); disposed = true;
        }
    }
}
