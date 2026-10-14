using System;
using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.ShipEditor.Rendering
{
    /// <summary>SPEC-51 §5.13: snapshot scheduling and reused draft meshes; borrows session/content/materials.</summary>
    public sealed class DraftRenderer : IDisposable
    {
        readonly EditSession session;
        readonly EditorContext content;
        readonly Dictionary<int3, BrickRenderSlot> slots = new();
        readonly HashSet<int3> dirty = new();
        readonly List<int3> ordered = new();
        readonly List<BrickRenderSlot> asynchronous = new(), synchronous = new();
        readonly ProfilerMarker remesh = new("DraftRenderer.Remesh"), draw = new("DraftRenderer.Draw");
        NativeList<int3> incoming = new(Allocator.Persistent);
        BlobAssetReference<BrickShapeTables> shapes;
        readonly Material brickMaterial, xrayMaterial;
        bool disposed, sliceEnabled;
        int sliceLevel;
        public Matrix4x4 GridToWorld { get; set; } = Matrix4x4.identity;
        public float3 CameraPivotCells { get; set; }
        public bool XRay { get; set; }
        public int RemeshCount { get; private set; }
        internal int PendingJobs => asynchronous.Count;
        internal IReadOnlyDictionary<int3, BrickRenderSlot> Slots => slots;
        public DraftRenderer(EditSession session, EditorContext content, Material brickMaterial, Material xrayMaterial)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.brickMaterial = brickMaterial; this.xrayMaterial = xrayMaterial;
            shapes = BrickShapeTablesBuilder.Build(ref content.Orientations.Value);
            // Includes existing content even when another preview previously drained the session's dirty queue.
            foreach (var brick in session.Draft.Data.Bricks) if (brick.SolidCount != 0) dirty.Add(brick.Coord);
            // TODO(EDT-018): subscribe incremental prop transactions and populate per-mesh instance sets.
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
            var parameters = new RenderParams(material) { camera = camera };
            float cellSize = session.Draft.CellSize;
            foreach (var entry in slots)
            {
                var slot = entry.Value;
                if (slot.Empty || sliceEnabled && (long)entry.Key.y * 16 > sliceLevel) continue;
                var local = Matrix4x4.TRS((Vector3)((float3)entry.Key * (16 * cellSize)), Quaternion.identity, Vector3.one * cellSize);
                Graphics.RenderMesh(parameters, slot.Mesh, 0, GridToWorld * local);
            }
            // TODO(EDT-018): draw visible prop/functional sets in chunks of at most 1,023.
        }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(DraftRenderer)); }
        public void Dispose()
        {
            if (disposed) return;
            foreach (var slot in slots.Values) slot.Dispose(); slots.Clear(); asynchronous.Clear(); synchronous.Clear();
            if (shapes.IsCreated) shapes.Dispose(); if (incoming.IsCreated) incoming.Dispose();
            dirty.Clear(); ordered.Clear(); disposed = true;
        }
    }
}
