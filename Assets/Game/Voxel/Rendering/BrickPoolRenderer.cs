using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Explicitly owned GPU pool (ADR-0032). Main-thread mutation/submission; Burst culling.</summary>
    public sealed class BrickPoolRenderer : IDisposable
    {
        struct RetiredRange { public PoolRange Vertices, Indices; public int Slot; public uint Due; }
        struct RetiredBuffer { public GraphicsBuffer Buffer; public uint Due; public long Bytes; }
        PageAllocator vertices, indices;
        NativeArray<BrickPoolItem> items;
        NativeArray<float4x4> worldMatrices, uploadedMatrices;
        NativeArray<float4> planes;
        NativeList<GraphicsBuffer.IndirectDrawIndexedArgs> arguments;
        NativeReference<BrickCullSummary> summary;
        NativeList<int2> matrixRuns;
        NativeList<PoolRange> visibleRanges;
        NativeList<int> freeSlots;
        NativeList<RetiredRange> retiredRanges;
        readonly List<RetiredBuffer> retiredBuffers = new List<RetiredBuffer>(16);
        readonly Plane[] cameraPlanes = new Plane[6];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly Material material;
        readonly ComputeShader copy;
        readonly int copyKernel, fillKernel;
        GraphicsBuffer vertexBuffer, indexBuffer, matrixBuffer, argumentBuffer, ownerBuffer, uploadBuffer;
        readonly CommandBuffer transfer;
        uint frame;
        bool disposed;
        static readonly ProfilerMarker CullMarker = new ProfilerMarker("BrickPoolRenderer.Cull");
        static readonly ProfilerMarker RenderMarker = new ProfilerMarker("BrickPoolRenderer.Render");
        static readonly ProfilerMarker UploadMarker = new ProfilerMarker("BrickPoolRenderer.Upload");
        static readonly int VertexId = Shader.PropertyToID("_BrickVertices"), MatrixId = Shader.PropertyToID("_BrickMatrices"), OwnerId = Shader.PropertyToID("_BrickOwners");
        static readonly int CopySourceId = Shader.PropertyToID("_CopySource"), CopyDestinationId = Shader.PropertyToID("_CopyDestination");
        static readonly int CopyStartId = Shader.PropertyToID("_CopyStart"), CopyEndId = Shader.PropertyToID("_CopyEnd");
        static readonly int CopyOffsetId = Shader.PropertyToID("_CopyOffset");
        static readonly int CopyAddId = Shader.PropertyToID("_CopyAdd"), OwnerSlotId = Shader.PropertyToID("_OwnerSlot");
        public int VisibleCount { get; private set; }
        public int DrawCount { get; private set; }
        public int MatrixUploadCount { get; private set; }
        public int LiveCount { get; private set; }
        public int VertexCapacity => vertices.Capacity;
        public int IndexCapacity => indices.Capacity;
        public int RetiredRangeCount => retiredRanges.Length;
        /// <summary>Caller jobs may write live slot transforms; complete them before mutation, reserve or Render. Never cache across Reserve.</summary>
        public NativeArray<float4x4> WorldMatrices => worldMatrices;
        public long GpuBytes
        {
            get
            {
                long bytes = Bytes(vertexBuffer) + Bytes(indexBuffer) + Bytes(matrixBuffer) + Bytes(argumentBuffer) + Bytes(ownerBuffer) + Bytes(uploadBuffer);
                foreach (var retired in retiredBuffers) bytes += retired.Bytes;
                return bytes;
            }
        }
        public long NativeBytes => vertices.NativeBytes + indices.NativeBytes + (long)items.Length * UnsafeUtility.SizeOf<BrickPoolItem>()
            + (long)(worldMatrices.Length + uploadedMatrices.Length) * 64 + (long)planes.Length * 16
            + (long)arguments.Capacity * GraphicsBuffer.IndirectDrawIndexedArgs.size + (long)freeSlots.Capacity * 4
            + (long)retiredRanges.Capacity * UnsafeUtility.SizeOf<RetiredRange>() + UnsafeUtility.SizeOf<BrickCullSummary>()
            + (long)(matrixRuns.Capacity + visibleRanges.Capacity) * 8 + 5L * UnsafeUtility.SizeOf<UnsafeList<int>>();
        public long ReservedBytes => GpuBytes + NativeBytes;
        public BrickPoolRenderer(Material material, ComputeShader copy, int vertexCapacity = 4096 * 160, int indexCapacity = 6144 * 512, int slotCapacity = 1024)
        {
            if (!material || !copy) throw new ArgumentNullException(!material ? nameof(material) : nameof(copy));
            this.material = material; this.copy = copy; copyKernel = copy.FindKernel("CopyWords"); fillKernel = copy.FindKernel("FillOwners");
            transfer = new CommandBuffer { name = "BrickPoolRenderer.Transfer" };
            vertices = new PageAllocator(4096, 0); indices = new PageAllocator(6144, 0);
            planes = new NativeArray<float4>(6, Allocator.Persistent);
            arguments = new NativeList<GraphicsBuffer.IndirectDrawIndexedArgs>(math.max(1, slotCapacity), Allocator.Persistent);
            summary = new NativeReference<BrickCullSummary>(Allocator.Persistent);
            matrixRuns = new NativeList<int2>(math.max(1, slotCapacity), Allocator.Persistent);
            visibleRanges = new NativeList<PoolRange>(math.max(1, slotCapacity), Allocator.Persistent);
            freeSlots = new NativeList<int>(math.max(1, slotCapacity), Allocator.Persistent);
            retiredRanges = new NativeList<RetiredRange>(128, Allocator.Persistent);
            try { Reserve(math.max(1, vertexCapacity), math.max(1, indexCapacity), math.max(1, slotCapacity)); }
            catch { Dispose(); throw; }
        }
        static long Bytes(GraphicsBuffer buffer) => buffer == null ? 0 : (long)buffer.count * buffer.stride;
        void Alive() { if (disposed) throw new ObjectDisposedException(nameof(BrickPoolRenderer)); }
        int Validate(BrickPoolHandle handle)
        {
            Alive();
            if (handle.Slot <= 0 || handle.Slot >= items.Length || items[handle.Slot].Allocated == 0 || items[handle.Slot].Generation != handle.Generation)
                throw new InvalidOperationException("Stale or invalid brick pool handle.");
            return handle.Slot;
        }
        internal AABB GetBounds(BrickPoolHandle handle) => items[Validate(handle)].Bounds;
        public void GetRanges(BrickPoolHandle handle, out PoolRange vertexRange, out PoolRange indexRange)
        { var item = items[Validate(handle)]; vertexRange = item.Vertices; indexRange = item.Indices; }
        public void SetTransform(BrickPoolHandle handle, float4x4 matrix) => worldMatrices[Validate(handle)] = matrix;
        public void SetVisible(BrickPoolHandle handle, bool visible)
        { int slot = Validate(handle); var item = items[slot]; item.Visible = (byte)(visible ? 1 : 0); items[slot] = item; }
        public BrickPoolHandle Upload(BrickPoolHandle handle, NativeArray<PackedBrickVertex> sourceVertices, NativeArray<uint> sourceIndices, AABB bounds, float4x4 matrix)
        {
            using var marker = UploadMarker.Auto(); Alive();
            if ((sourceVertices.Length == 0) != (sourceIndices.Length == 0)) throw new ArgumentException("Empty geometry must have no vertices and no indices.");
            int slot;
            if (handle.Slot == 0)
            {
                if (freeSlots.Length == 0) Reserve(vertices.Capacity, indices.Capacity, math.max(1, (items.Length - 1) * 2));
                slot = freeSlots[freeSlots.Length - 1]; freeSlots.RemoveAt(freeSlots.Length - 1);
                var fresh = items[slot]; fresh.Allocated = 1; fresh.Generation = fresh.Generation == uint.MaxValue ? 1 : fresh.Generation + 1;
                items[slot] = fresh; handle = new BrickPoolHandle { Slot = slot, Generation = fresh.Generation }; LiveCount++;
            }
            else slot = Validate(handle);
            var item = items[slot];
            bool replaceVertices = sourceVertices.Length == 0 || sourceVertices.Length > item.Vertices.Count;
            bool replaceIndices = sourceIndices.Length == 0 || sourceIndices.Length > item.Indices.Count;
            PoolRange oldVertices = replaceVertices ? item.Vertices : default, oldIndices = replaceIndices ? item.Indices : default;
            if (replaceVertices) item.Vertices = Allocate(ref vertices, sourceVertices.Length, true);
            if (replaceIndices) item.Indices = Allocate(ref indices, sourceIndices.Length, false);
            if (oldVertices.Count != 0 || oldIndices.Count != 0) retiredRanges.Add(new RetiredRange { Vertices = oldVertices, Indices = oldIndices, Due = frame + 3 });
            if (sourceVertices.Length > 0)
            {
                // CPU uploads never touch GPU-written pool buffers: Metal can discard their copied ranges.
                int vertexWords = sourceVertices.Length * 2, words = vertexWords + sourceIndices.Length;
                if (uploadBuffer == null || uploadBuffer.count < words)
                {
                    Retire(uploadBuffer);
                    uploadBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, checked((words + 4095) / 4096 * 4096), 4);
                }
                transfer.Clear();
                transfer.SetBufferData(uploadBuffer, sourceVertices.Reinterpret<uint>(8), 0, 0, vertexWords);
                transfer.SetBufferData(uploadBuffer, sourceIndices, 0, vertexWords, sourceIndices.Length);
                CopyWords(uploadBuffer, vertexBuffer, 0, vertexWords, item.Vertices.Start * 2);
                CopyWords(uploadBuffer, indexBuffer, vertexWords, sourceIndices.Length, item.Indices.Start, item.Vertices.Start);
                transfer.SetComputeBufferParam(copy, fillKernel, CopyDestinationId, ownerBuffer);
                transfer.SetComputeIntParam(copy, CopyStartId, item.Vertices.Start); transfer.SetComputeIntParam(copy, CopyEndId, item.Vertices.Start + sourceVertices.Length);
                transfer.SetComputeIntParam(copy, OwnerSlotId, slot);
                transfer.DispatchCompute(copy, fillKernel, (sourceVertices.Length + 255) / 256, 1, 1);
                Graphics.ExecuteCommandBuffer(transfer); transfer.Clear();
            }
            item.VertexCount = sourceVertices.Length; item.IndexCount = sourceIndices.Length; item.Bounds = bounds; item.Visible = 1;
            items[slot] = item; worldMatrices[slot] = matrix; return handle;
        }
        PoolRange Allocate(ref PageAllocator allocator, int count, bool vertex)
        {
            if (allocator.TryAllocate(count, out var range)) return range;
            int capacity = checked(math.max(allocator.Capacity + count, allocator.Capacity + allocator.Capacity / 2));
            if (vertex) Reserve(capacity, indices.Capacity, items.Length - 1);
            else Reserve(vertices.Capacity, capacity, items.Length - 1);
            if (!allocator.TryAllocate(count, out range)) throw new InvalidOperationException("Pool growth did not satisfy allocation.");
            return range;
        }
        public void Release(BrickPoolHandle handle)
        {
            if (handle.Slot == 0) return;
            int slot = Validate(handle); var item = items[slot];
            retiredRanges.Add(new RetiredRange { Vertices = item.Vertices, Indices = item.Indices, Slot = slot, Due = frame + 3 });
            item.Allocated = 0; item.Visible = 0; item.Vertices = default; item.Indices = default; item.VertexCount = item.IndexCount = 0;
            items[slot] = item; LiveCount--;
        }
        public void AdvanceFrame(uint value)
        {
            Alive(); if ((int)(value - frame) < 0) throw new ArgumentOutOfRangeException(nameof(value)); frame = value;
            for (int i = retiredRanges.Length - 1; i >= 0; i--)
            {
                var retired = retiredRanges[i]; if ((int)(frame - retired.Due) < 0) continue;
                vertices.Free(retired.Vertices); indices.Free(retired.Indices);
                if (retired.Slot != 0) freeSlots.Add(retired.Slot);
                retiredRanges.RemoveAtSwapBack(i);
            }
            for (int i = retiredBuffers.Count - 1; i >= 0; i--)
                if ((int)(frame - retiredBuffers[i].Due) >= 0) { retiredBuffers[i].Buffer.Dispose(); retiredBuffers.RemoveAt(i); }
        }
        public void Reserve(int vertexCapacity, int indexCapacity, int slotCapacity)
        {
            Alive();
            int nextVertices = vertices.RoundedCapacity(math.max(1, vertexCapacity));
            int nextIndices = indices.RoundedCapacity(math.max(1, indexCapacity));
            if (nextVertices > vertices.Capacity)
            {
                vertexBuffer = GrowBuffer(vertexBuffer, checked(nextVertices * 2), GraphicsBuffer.Target.Raw);
                ownerBuffer = GrowBuffer(ownerBuffer, nextVertices, GraphicsBuffer.Target.Raw);
                vertices.GrowTo(nextVertices); properties.SetBuffer(VertexId, vertexBuffer); properties.SetBuffer(OwnerId, ownerBuffer);
            }
            if (nextIndices > indices.Capacity)
            { indexBuffer = GrowBuffer(indexBuffer, nextIndices, GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Index); indices.GrowTo(nextIndices); }
            int oldSlots = items.IsCreated ? items.Length - 1 : 0;
            if (slotCapacity <= oldSlots) return;
            Resize(ref items, slotCapacity + 1); Resize(ref worldMatrices, slotCapacity + 1); Resize(ref uploadedMatrices, slotCapacity + 1);
            for (int slot = slotCapacity; slot > oldSlots; slot--) freeSlots.Add(slot);
            arguments.Capacity = math.max(arguments.Capacity, slotCapacity);
            matrixRuns.Capacity = math.max(matrixRuns.Capacity, slotCapacity);
            visibleRanges.Capacity = math.max(visibleRanges.Capacity, slotCapacity);
            Retire(matrixBuffer); Retire(argumentBuffer);
            matrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, slotCapacity + 1, 64); matrixBuffer.SetData(uploadedMatrices);
            argumentBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, slotCapacity, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            properties.SetBuffer(MatrixId, matrixBuffer);
        }
        static void Resize<T>(ref NativeArray<T> array, int count) where T : struct
        {
            var next = new NativeArray<T>(count, Allocator.Persistent);
            if (array.IsCreated) { NativeArray<T>.Copy(array, next, array.Length); array.Dispose(); }
            array = next;
        }
        void Retire(GraphicsBuffer buffer)
        { if (buffer != null) retiredBuffers.Add(new RetiredBuffer { Buffer = buffer, Due = frame + 3, Bytes = Bytes(buffer) }); }
        GraphicsBuffer GrowBuffer(GraphicsBuffer old, int words, GraphicsBuffer.Target target)
        {
            var next = new GraphicsBuffer(target, words, 4);
            if (old != null)
            {
                transfer.Clear();
                CopyWords(old, next, 0, old.count, 0);
                Graphics.ExecuteCommandBuffer(transfer); transfer.Clear();
                Retire(old);
            }
            return next;
        }
        void CopyWords(GraphicsBuffer source, GraphicsBuffer destination, int sourceStart, int count, int destinationStart, int add = 0)
        {
            transfer.SetComputeBufferParam(copy, copyKernel, CopySourceId, source);
            transfer.SetComputeBufferParam(copy, copyKernel, CopyDestinationId, destination);
            transfer.SetComputeIntParam(copy, CopyOffsetId, destinationStart - sourceStart);
            transfer.SetComputeIntParam(copy, CopyAddId, add);
            const int maxWords = 65535 * 256;
            for (int start = sourceStart; start < sourceStart + count; start += maxWords)
            {
                int end = math.min(sourceStart + count, start + maxWords);
                transfer.SetComputeIntParam(copy, CopyStartId, start); transfer.SetComputeIntParam(copy, CopyEndId, end);
                transfer.DispatchCompute(copy, copyKernel, (end - start + 255) / 256, 1, 1);
            }
        }
        public void Render(Camera view, float shadowDistance)
        {
            using var render = RenderMarker.Auto(); Alive(); if (!view) throw new ArgumentNullException(nameof(view));
            GeometryUtility.CalculateFrustumPlanes(view, cameraPlanes);
            for (int i = 0; i < 6; i++) planes[i] = new float4(cameraPlanes[i].normal, cameraPlanes[i].distance);
            using (CullMarker.Auto())
                new BrickCullJob { Items = items, WorldMatrices = worldMatrices, UploadedMatrices = uploadedMatrices, Planes = planes,
                    Arguments = arguments, Summary = summary, MatrixRuns = matrixRuns, VisibleRanges = visibleRanges, CameraPosition = view.transform.position, ShadowDistanceSquared = shadowDistance * shadowDistance }.Schedule().Complete();
            var cull = summary.Value; DrawCount = arguments.Length; VisibleCount = cull.VisibleCount; MatrixUploadCount = 0;
            for (int i = 0; i < matrixRuns.Length; i++)
            {
                var run = matrixRuns[i]; int count = run.y - run.x; MatrixUploadCount += count;
                matrixBuffer.SetData(uploadedMatrices, run.x, run.x, count);
            }
            if (DrawCount == 0) return;
            argumentBuffer.SetData(arguments.AsArray(), 0, 0, DrawCount);
            var parameters = new RenderParams(material) { camera = view, matProps = properties, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true,
                worldBounds = new Bounds((cull.BoundsMin + cull.BoundsMax) * .5f, cull.BoundsMax - cull.BoundsMin) };
            Graphics.RenderPrimitivesIndexedIndirect(parameters, MeshTopology.Triangles, indexBuffer, argumentBuffer, DrawCount);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            vertexBuffer?.Dispose(); indexBuffer?.Dispose(); matrixBuffer?.Dispose(); argumentBuffer?.Dispose(); ownerBuffer?.Dispose(); uploadBuffer?.Dispose(); transfer?.Dispose();
            foreach (var retired in retiredBuffers) retired.Buffer.Dispose(); retiredBuffers.Clear();
            vertices.Dispose(); indices.Dispose();
            if (items.IsCreated) items.Dispose(); if (worldMatrices.IsCreated) worldMatrices.Dispose(); if (uploadedMatrices.IsCreated) uploadedMatrices.Dispose();
            if (visibleRanges.IsCreated) visibleRanges.Dispose();
            if (matrixRuns.IsCreated) matrixRuns.Dispose();
            if (planes.IsCreated) planes.Dispose(); if (arguments.IsCreated) arguments.Dispose(); if (summary.IsCreated) summary.Dispose();
            if (freeSlots.IsCreated) freeSlots.Dispose(); if (retiredRanges.IsCreated) retiredRanges.Dispose();
        }
    }
}
