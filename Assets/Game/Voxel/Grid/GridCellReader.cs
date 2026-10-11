using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6. Read-only job helper; every lookup is [ReadOnly].</summary>
    public struct GridCellReader
    {
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids; [ReadOnly] public BufferLookup<BrickSlot> Slots;
        [ReadOnly] public BufferLookup<BrickCells> Cells; [ReadOnly] public BufferLookup<BrickSolidMask> Masks;
        public static GridCellReader Create(ref SystemState state) => new GridCellReader
        {
            Grids = state.GetComponentLookup<VoxelGrid>(true), Slots = state.GetBufferLookup<BrickSlot>(true),
            Cells = state.GetBufferLookup<BrickCells>(true), Masks = state.GetBufferLookup<BrickSolidMask>(true)
        };
        public void Update(ref SystemState state)
        {
            Grids.Update(ref state); Slots.Update(ref state); Cells.Update(ref state); Masks.Update(ref state);
        }
        public Entity GetBrick(Entity grid, int3 brickCoord)
        {
            if (!Grids.HasComponent(grid) || !Slots.HasBuffer(grid)) return Entity.Null;
            var info = Grids[grid];
            int index = BrickMath.SlotIndex(brickCoord, info.BrickMin, info.BrickDims);
            if (index < 0) return Entity.Null;
            var slots = Slots[grid];
            return index < slots.Length ? slots[index].Brick : Entity.Null;
        }
        public bool TryGetCell(Entity grid, int3 cell, out Cell value)
        {
            value = default;
            Entity brick = GetBrick(grid, BrickMath.BrickOf(cell));
            if (!Cells.HasBuffer(brick)) return false;
            value = BrickWrite.Get(Cells[brick], BrickMath.LocalIndex(BrickMath.LocalOf(cell)));
            return !value.IsAir;
        }
        public bool IsSolid(Entity grid, int3 cell)
        {
            Entity brick = GetBrick(grid, BrickMath.BrickOf(cell));
            if (!Masks.HasBuffer(brick)) return false;
            int local = BrickMath.LocalIndex(BrickMath.LocalOf(cell));
            return (Masks[brick][local >> 6].Bits & (1UL << (local & 63))) != 0;
        }
    }
}
