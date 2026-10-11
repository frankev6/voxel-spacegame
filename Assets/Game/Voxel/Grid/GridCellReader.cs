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
        public static GridCellReader Create(ref SystemState state) { throw new System.NotImplementedException("VOX-008"); }   // call in OnCreate
        public void Update(ref SystemState state) { throw new System.NotImplementedException("VOX-008"); }                    // call first thing in OnUpdate
        public Entity GetBrick(Entity grid, int3 brickCoord) { throw new System.NotImplementedException("VOX-008"); }         // Entity.Null if outside or absent
        public bool TryGetCell(Entity grid, int3 cell, out Cell value) { throw new System.NotImplementedException("VOX-008"); }   // false and air if absent
        public bool IsSolid(Entity grid, int3 cell) { throw new System.NotImplementedException("VOX-008"); }                  // mask bit test
    }
}
