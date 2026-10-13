using System;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Galaxy.Bodies
{
    /// <summary>SPEC-31 §5.8: Burst-filled geometric shells and one optional core; caller owns each returned source.</summary>
    public sealed class ProceduralShapeBuilder
    {
        static readonly ProfilerMarker AssembleMarker = new ProfilerMarker(ProfilerCategory.Scripts, "ProceduralShapeBuilder.Assemble");
        readonly ContentCatalog catalog;
        readonly VoxelContent content;
        readonly float cellSize;
        public ProceduralShapeBuilder(ContentCatalog catalog, in VoxelContent content, float cellSize = .5f)
        {
            StructureBuilderAssembly.CheckContext(catalog, content); StructureBuilderAssembly.Positive(cellSize, nameof(cellSize));
            this.catalog = catalog; this.content = content; this.cellSize = cellSize;
        }
        public VoxelStructure Build(in ProceduralShapeParams p, ulong seed)
        {
            // Closed-form shapes consume no randomness; seed remains part of the loader's template/source cache identity.
            int3 size = p.kind switch
            {
                ShapeKind.Box => new int3(p.sizeCells.x, p.sizeCells.y, p.sizeCells.z),
                ShapeKind.Cylinder or ShapeKind.Ring => new int3(p.sizeCells.x, p.sizeCells.y, p.sizeCells.x),
                ShapeKind.Sphere => new int3(p.sizeCells.x),
                _ => throw new ArgumentOutOfRangeException(nameof(p), "Unknown shape kind.")
            };
            if (p.wallThicknessCells < 0 || p.kind == ShapeKind.Ring && (p.sizeCells.z < 0 || p.sizeCells.z >= size.x))
                throw new ArgumentOutOfRangeException(nameof(p), "Wall thickness/inner diameter is invalid.");
            int3 min = -size / 2;
            int count = StructureBuilderAssembly.Layout(min, size, out var brickMin, out var brickDims);
            ushort hull = StructureBuilderAssembly.Structural(p.hullBlock, catalog, content);
            ushort core = p.coreBlock == null ? (ushort)0 : StructureBuilderAssembly.Index(p.coreBlock, catalog, content);
            if (core != 0 && (content.Blocks.Value.Blocks[core].Kind != BlockKind.Functional || !content.Blocks.Value.Blocks[core].IsCore))
                throw new ArgumentException("coreBlock must be a functional Core block.", nameof(p));
            using var cells = new NativeArray<Cell>(checked(count * BrickMath.CellsPerBrick), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            using var closest = new NativeArray<NearestShapeCell>(count, Allocator.TempJob);
            new ShapeFillJob { Kind = p.kind, Min = min, Size = size, InnerRadius = p.sizeCells.z * .5f,
                Thickness = p.wallThicknessCells, BrickMin = brickMin, BrickDims = brickDims, Cells = cells, Closest = closest }
                .Schedule(count, 1).Complete();
            using var assembly = AssembleMarker.Auto();
            var source = StructureBuilderAssembly.Assemble(hull, p.colorIndex, cellSize, brickMin, brickDims, cells);
            try
            {
                if (core != 0)
                {
                    NearestShapeCell nearest = default;
                    foreach (var candidate in closest) if (candidate.Before(nearest)) nearest = candidate;
                    if (nearest.Found == 0) throw new ArgumentException("A core requires a solid shape cell.", nameof(p));
                    ref var definition = ref content.Blocks.Value.Blocks[core];
                    // Replace hull in the entire canonical core footprint; TryPlaceBlock owns instance/root-cell creation.
                    for (int i = 0; i < definition.Cells.Length; i++) source.TryRemoveBlockAt(nearest.Cell + definition.Cells[i], content, out _);
                    var placed = source.TryPlaceBlock(new BlockPlacement { Block = core, Origin = nearest.Cell, Color = p.colorIndex }, content);
                    if (placed != PlaceResult.Ok) throw new InvalidOperationException("Core placement failed: " + placed);
                }
                source.OptimizeStorage(); return source;
            }
            catch { source.Dispose(); throw; }
        }
    }

    internal static class StructureBuilderAssembly
    {
        public static void CheckContext(ContentCatalog catalog, in VoxelContent content)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!content.Blocks.IsCreated || !content.Orientations.IsCreated) throw new ArgumentException("Baked voxel content is required.", nameof(content));
        }
        public static void Positive(float value, string name)
        { if (!math.isfinite(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
        public static ushort Index(BlockDefinition block, ContentCatalog catalog, in VoxelContent content)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            ushort index = catalog.GetBlockIndex(block);
            if (index == 0 || index >= content.Blocks.Value.Blocks.Length || content.Blocks.Value.Blocks[index].IdHash != block.Id.Hash)
                throw new ArgumentException("Block must belong to the supplied baked catalog.", nameof(block));
            if ((content.Blocks.Value.Blocks[index].AllowedOrientations & 1) == 0)
                throw new ArgumentException("Builder blocks must permit identity orientation.", nameof(block));
            return index;
        }
        public static ushort Structural(BlockDefinition block, ContentCatalog catalog, in VoxelContent content)
        {
            ushort index = Index(block, catalog, content); ref var definition = ref content.Blocks.Value.Blocks[index];
            if (definition.Kind != BlockKind.Structural || definition.RequiresInstance || definition.Cells.Length != 1)
                throw new ArgumentException("Fill block must be single-cell Structural content.", nameof(block));
            return index;
        }
        public static int Layout(int3 min, int3 size, out int3 brickMin, out int3 brickDims)
        {
            if (math.any(size <= 0) || math.any(size > 2097152)) throw new ArgumentOutOfRangeException(nameof(size));
            int3 max = min + size - 1;
            if (math.any(min < -1048576) || math.any(max > 1048575)) throw new ArgumentOutOfRangeException(nameof(min));
            brickMin = BrickMath.BrickOf(min); brickDims = BrickMath.BrickOf(max) - brickMin + 1;
            long count = (long)brickDims.x * brickDims.y * brickDims.z;
            if (count > int.MaxValue / BrickMath.CellsPerBrick) throw new ArgumentOutOfRangeException(nameof(size), "Dense job staging exceeds native-array indexing.");
            return (int)count;
        }
        public static int3 Coord(int index, int3 min, int3 dims)
            => min + new int3(index % dims.x, index / dims.x % dims.y, index / (dims.x * dims.y));
        public static VoxelStructure Assemble(ushort block, byte color, float cellSize, int3 brickMin, int3 brickDims, NativeArray<Cell> cells)
        {
            var source = new VoxelStructure(cellSize);
            try
            {
                source.FindOrAddPalette(new PaletteEntry { Block = block, Color = color }); // fresh source: palette 1, matching fill jobs
                for (int i = 0; i < cells.Length / BrickMath.CellsPerBrick; i++)
                    source.SetBrickDense(Coord(i, brickMin, brickDims), cells.GetSubArray(i * BrickMath.CellsPerBrick, BrickMath.CellsPerBrick));
                return source;
            }
            catch { source.Dispose(); throw; }
        }
    }
}
