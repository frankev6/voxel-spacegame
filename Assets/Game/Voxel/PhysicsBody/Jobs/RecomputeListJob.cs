using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.PhysicsBody
{
    internal struct RecomputeBrick { public int Root; public Entity Brick; public int3 Coord; }
    internal struct RecomputeRoot { public Entity Entity; public int Start, Count; public float CellSize; }

    /// <summary>SPEC-12 §5.11: canonical root/slot work order and deferred result sizing.</summary>
    [BurstCompile]
    internal struct RecomputeListJob : IJob
    {
        [ReadOnly] public NativeList<Entity> Entities;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public BufferLookup<BrickSlot> Slots;
        public NativeList<RecomputeRoot> Roots;
        public NativeList<RecomputeBrick> Work;
        public NativeList<StructureMassData> Results;
        struct OrderedRoot : System.IComparable<OrderedRoot>
        {
            public Entity Entity; public uint Serial;
            public int CompareTo(OrderedRoot other) => Serial.CompareTo(other.Serial);
        }
        public void Execute()
        {
            using var order = new NativeList<OrderedRoot>(Entities.Length, Allocator.Temp);
            for (int i = 0; i < Entities.Length; i++) order.Add(new OrderedRoot { Entity = Entities[i], Serial = Grids[Entities[i]].Serial });
            order.Sort();
            for (int i = 0; i < order.Length; i++)
            {
                var root = order[i].Entity; int start = Work.Length; var slots = Slots[root];
                for (int s = 0; s < slots.Length; s++)
                {
                    var brick = slots[s].Brick; if (brick == Entity.Null || !Bricks.HasComponent(brick)) continue;
                    Work.Add(new RecomputeBrick { Root = i, Brick = brick, Coord = Bricks[brick].Coord });
                }
                Roots.Add(new RecomputeRoot { Entity = root, Start = start, Count = Work.Length - start, CellSize = Grids[root].CellSize });
            }
            Results.ResizeUninitialized(Work.Length);
        }
    }
}
