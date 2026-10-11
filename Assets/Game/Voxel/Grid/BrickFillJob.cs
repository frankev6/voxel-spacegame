using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Voxel.Grid
{
    /// <summary>Copies pinned source pages into distinct freshly instantiated brick entities (SPEC-10 §5.7).</summary>
    [BurstCompile]
    internal unsafe struct BrickFillJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<BrickRecord> Records;
        [ReadOnly] public NativeArray<Cell> Pages;
        [ReadOnly] public NativeArray<BrickMaskBits> Masks;
        public Entity Root;
        public int Start;
        public float CellSize;
        // Each iteration owns exactly one new entity; no lookup destination is shared by workers.
        [NativeDisableParallelForRestriction] public ComponentLookup<Brick> Bricks;
        [NativeDisableParallelForRestriction] public ComponentLookup<BrickDirty> Dirty;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickCells> Cells;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickSolidMask> SolidMasks;
        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<Parent> Parents;
        public void Execute(int index)
        {
            Entity entity = Entities[index]; Dirty.SetComponentEnabled(entity, false); int sourceIndex = Start + index; var record = Records[sourceIndex];
            Bricks[entity] = new Brick { Grid = Root, Coord = record.Coord, SolidCount = record.SolidCount };
            var cells = Cells[entity]; cells.ResizeUninitialized(BrickMath.CellsPerBrick);
            if (record.Page < 0) UnsafeUtility.MemCpyReplicate(cells.GetUnsafePtr(), &record.Uniform, UnsafeUtility.SizeOf<Cell>(), BrickMath.CellsPerBrick);
            else UnsafeUtility.MemCpy(cells.GetUnsafePtr(), (Cell*)Pages.GetUnsafeReadOnlyPtr() + record.Page * BrickMath.CellsPerBrick,
                BrickMath.CellsPerBrick * UnsafeUtility.SizeOf<Cell>());
            var mask = Masks[sourceIndex]; var solid = SolidMasks[entity]; solid.ResizeUninitialized(BrickMath.MaskWords);
            UnsafeUtility.MemCpy(solid.GetUnsafePtr(), mask.Words, BrickMath.MaskWords * sizeof(ulong));
            Transforms[entity] = LocalTransform.FromPositionRotationScale((float3)record.Coord * (16 * CellSize), quaternion.identity, CellSize);
            Parents[entity] = new Parent { Value = Root };
        }
    }
}
