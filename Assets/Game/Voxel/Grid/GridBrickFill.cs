using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{
    /// <summary>ABL-006: a request entity asking GridSpawnSystem to create bricks a grid lost (a brick that reached zero cells is
    /// deleted, SPEC-13) already holding the given cells, and to recreate the functional entities of live instances that have none.
    /// Cells for bricks that still exist go through the exact-write channel instead (ADR-0117); a cell whose brick exists when the
    /// request runs is dropped and counted. Writers: the emergency repair on the authority, and a co-op guest receiving a brick it
    /// does not have. Instance health (CellsAlive, FunctionHp, flags) is the writer's, as exact instance writes.</summary>
    public struct GridBrickFillRequest : IComponentData
    {
        public Entity Grid;
        public byte ReviveFunctional;   // 1 = recreate missing functional entities after the bricks
        public byte Attempts;           // revive retries: the instance writes that revive blocks may land a tick after the request
    }

    /// <summary>One cell of a <see cref="GridBrickFillRequest"/>: grid cell coordinate and exact value.</summary>
    [InternalBufferCapacity(0)]
    public struct GridBrickFillCell : IBufferElementData
    {
        public int3 Cell;
        public Cell Value;
    }

    public static class GridBrickFillApi
    {
        /// <summary>Queues a request (main thread, structural); GridSpawnSystem runs it on its next update.</summary>
        public static Entity Request(EntityManager em, Entity grid, NativeArray<GridBrickFillCell> cells, bool reviveFunctional)
        {
            var entity = em.CreateEntity(typeof(GridBrickFillRequest), typeof(GridBrickFillCell));
            em.SetComponentData(entity, new GridBrickFillRequest { Grid = grid, ReviveFunctional = (byte)(reviveFunctional ? 1 : 0) });
            if (cells.IsCreated && cells.Length > 0) em.GetBuffer<GridBrickFillCell>(entity).AddRange(cells);
            return entity;
        }
        /// <summary>True when the grid has a live brick for this cell.</summary>
        public static bool HasBrick(EntityManager em, Entity grid, int3 cell)
        {
            var definition = em.GetComponentData<VoxelGrid>(grid);
            int slot = BrickMath.SlotIndex(BrickMath.BrickOf(cell), definition.BrickMin, definition.BrickDims);
            if (slot < 0) return false;
            var brick = em.GetBuffer<BrickSlot>(grid, true)[slot].Brick;
            return brick != Entity.Null && em.Exists(brick);
        }
    }
}
