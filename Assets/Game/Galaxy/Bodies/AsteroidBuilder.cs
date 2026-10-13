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
            using var cells = new NativeArray<Cell>(checked(count * BrickMath.CellsPerBrick), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            new AsteroidFillJob { Radius = radiusM, CellSize = cellSize, Amplitude = noiseAmplitude, Frequency = noiseFrequency,
                NoiseOffset = offset, Min = min, Size = size, BrickMin = brickMin, BrickDims = brickDims, Cells = cells }
                .Schedule(count, 1).Complete();
            using var assembly = AssembleMarker.Auto();
            var source = StructureBuilderAssembly.Assemble(fill, 0, cellSize, brickMin, brickDims, cells);
            try { source.OptimizeStorage(); return source; }
            catch { source.Dispose(); throw; }
        }
    }
}
