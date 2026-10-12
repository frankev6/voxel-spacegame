using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.3.2: per-grid cell access across dense and sparse bricks.</summary>
    internal struct GridCellAccess
    {
        public Entity Grid;
        public VoxelGrid Definition;
        public DynamicBuffer<BrickSlot> Slots;
        public DynamicBuffer<PaletteEntry> Palette;
        public DynamicBuffer<BlockInstance> Instances;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        // Each grid's bricks and instances are written only by its own range (SPEC-13 §3 decision 4).
        [NativeDisableParallelForRestriction] public BufferLookup<BrickCells> Dense;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickSparseCells> Sparse;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickSolidMask> Masks;
        [NativeDisableParallelForRestriction] public ComponentLookup<Brick> Bricks;
        [NativeDisableParallelForRestriction] public ComponentLookup<BrickDirty> Dirty;
        Entity cachedBrick;
        DynamicBuffer<BrickCells> cachedDense;
        DynamicBuffer<BrickSparseCells> cachedSparse;
        public int3 Min => Definition.BrickMin * 16;
        public int3 Max => (Definition.BrickMin + Definition.BrickDims) * 16;
        public int Slot(int3 cell) => BrickMath.SlotIndex(BrickMath.BrickOf(cell), Definition.BrickMin, Definition.BrickDims);
        public bool TryGetCell(int3 c, out Cell cell, out Entity brick, out int local)
        {
            cell = default; brick = Entity.Null; local = 0;
            if (math.any(c < Min) || math.any(c >= Max)) return false;
            brick = Slots[Slot(c)].Brick;
            if (brick == Entity.Null) return false;
            Cache(brick); local = BrickMath.LocalIndex(BrickMath.LocalOf(c));
            cell = BrickWrite.Get(cachedDense, cachedSparse, local); return !cell.IsAir;
        }
        void Cache(Entity brick)
        {
            if (brick == cachedBrick) return;
            cachedBrick = brick; cachedDense = Dense[brick]; cachedSparse = Sparse[brick];
        }
        public PaletteEntry Appearance(Cell cell)
        {
            if (!cell.IsInstance) return Palette[cell.PaletteIndex];
            var instance = Instances[cell.InstanceIndex];
            return new PaletteEntry { Block = instance.Block, Orient = instance.Orient, Color = instance.Color };
        }
        public float CellHp(Cell cell) => Blocks.Value.Blocks[Appearance(cell).Block].CellHp;
        public byte ArmorClass(Cell cell) => Blocks.Value.Blocks[Appearance(cell).Block].ArmorClass;
        public bool Write(Entity entity, int local, Cell value, bool sameHpBin = false)
        {
            Cache(entity); var brick = Bricks[entity]; var dirty = Dirty[entity]; var priorFlags = dirty.Flags;
            bool changed = BrickWrite.Set(ref brick, cachedDense, cachedSparse, Masks[entity], ref dirty, local, value, Definition.Revision + 1);
            if (sameHpBin) dirty.Flags = priorFlags;
            Bricks[entity] = brick; Dirty[entity] = dirty; return changed;
        }
    }
}
