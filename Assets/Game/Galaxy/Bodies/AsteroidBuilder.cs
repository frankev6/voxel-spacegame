using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Galaxy.Bodies
{
    /// <summary>SPEC-31 §5.8: seeded simplex-noise asteroid fills in worker jobs; caller owns the assembled source.</summary>
    public sealed class AsteroidBuilder
    {
        static readonly ProfilerMarker AssembleMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AsteroidBuilder.Assemble");
        readonly ContentCatalog catalog;
        readonly VoxelContent content;
        public AsteroidBuilder(ContentCatalog catalog, in VoxelContent content)
        { StructureBuilderAssembly.CheckContext(catalog, content); this.catalog = catalog; this.content = content; }
        public VoxelStructure Build(float radiusM, float cellSize, BlockDefinition block, float noiseAmplitude, float noiseFrequency, ulong seed)
        {
            using var pending = Schedule(radiusM, cellSize, block, noiseAmplitude, noiseFrequency, seed);
            pending.Wait(); // Existing synchronous authoring API; streaming uses TryTake on later frames.
            if (!pending.TryTake(out var source)) throw new InvalidOperationException("Completed asteroid job was unavailable.");
            return source;
        }

        internal PendingShape Schedule(float radiusM, float cellSize, BlockDefinition block, float noiseAmplitude, float noiseFrequency, ulong seed)
        {
            StructureBuilderAssembly.Positive(radiusM, nameof(radiusM)); StructureBuilderAssembly.Positive(cellSize, nameof(cellSize));
            if (!math.isfinite(noiseAmplitude) || noiseAmplitude < 0) throw new ArgumentOutOfRangeException(nameof(noiseAmplitude));
            if (!math.isfinite(noiseFrequency) || noiseFrequency < 0) throw new ArgumentOutOfRangeException(nameof(noiseFrequency));
            ushort fill = StructureBuilderAssembly.Structural(block, catalog, content);
            double extent = Math.Ceiling((double)radiusM * (1 + (double)noiseAmplitude) / cellSize);
            if (extent > 1048576) throw new ArgumentOutOfRangeException(nameof(radiusM), "Asteroid exceeds structure coordinate limits.");
            int3 min = new int3(-(int)extent), size = new int3((int)extent * 2);
            int count = StructureBuilderAssembly.Layout(min, size, out var brickMin, out var brickDims);
            var rng = RngStreams.For(seed, RngPurpose.AsteroidShape, 0);
            float3 offset = rng.NextFloat3(-1024, 1024); // fixed three draws, independent of amplitude and brick scheduling
            // Streaming can retain work beyond four frames, including pauses; this owner releases the persistent buffer.
            var cells = new NativeArray<Cell>(checked(count * BrickMath.CellsPerBrick), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            JobHandle handle = default;
            try
            {
                handle = new AsteroidFillJob { Radius = radiusM, CellSize = cellSize, Amplitude = noiseAmplitude, Frequency = noiseFrequency,
                    NoiseOffset = offset, Min = min, Size = size, BrickMin = brickMin, BrickDims = brickDims, Cells = cells }
                    .Schedule(count, 1);
                return new PendingShape(cells, handle, fill, cellSize, brickMin, brickDims);
            }
            catch { handle.Complete(); cells.Dispose(); throw; }
        }

        /// <summary>Single-owner scheduled fill. Taking the source transfers ownership; cancellation joins before freeing cells.</summary>
        internal sealed class PendingShape : IDisposable
        {
            NativeArray<Cell> cells;
            readonly JobHandle handle;
            readonly ushort fill;
            readonly float cellSize;
            readonly int3 brickMin, brickDims;
            bool disposed;
            internal PendingShape(NativeArray<Cell> cells, JobHandle handle, ushort fill, float cellSize, int3 brickMin, int3 brickDims)
            { this.cells = cells; this.handle = handle; this.fill = fill; this.cellSize = cellSize; this.brickMin = brickMin; this.brickDims = brickDims; }
            internal bool IsCompleted => !disposed && handle.IsCompleted;
            internal void Wait()
            { if (disposed) throw new ObjectDisposedException(nameof(PendingShape)); handle.Complete(); }
            internal bool TryTake(out VoxelStructure source)
            {
                source = null;
                if (disposed) throw new ObjectDisposedException(nameof(PendingShape));
                if (!handle.IsCompleted) return false;
                handle.Complete();
                try
                {
                    using var assembly = AssembleMarker.Auto();
                    source = StructureBuilderAssembly.Assemble(fill, 0, cellSize, brickMin, brickDims, cells);
                    source.OptimizeStorage();
                    return true;
                }
                catch { source?.Dispose(); source = null; throw; }
                finally { Dispose(); }
            }
            public void Dispose()
            {
                if (disposed) return;
                handle.Complete();
                if (cells.IsCreated) cells.Dispose();
                disposed = true;
            }
        }
    }
}
