using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Grid
{
    /// <summary>Serial append into one root's buffers after its parallel fill finishes (SPEC-10 §5.7).</summary>
    [BurstCompile]
    internal struct LinkCreatedJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<BrickRecord> Records;
        [ReadOnly] public NativeArray<int> InstanceIndices;
        public Entity Root;
        public VoxelGrid Grid;
        public int Start;
        public byte Functional;
        public BufferLookup<LinkedEntityGroup> Groups;
        public BufferLookup<BrickSlot> Slots;
        public BufferLookup<BlockInstance> Instances;
        public ComponentLookup<GridSpawning> Spawning;
        public void Execute()
        {
            var group = Groups[Root];
            if (Functional != 0)
            {
                var instances = Instances[Root];
                for (int i = 0; i < Entities.Length; i++)
                {
                    int index = InstanceIndices[i]; var instance = instances[index]; instance.Functional = Entities[i]; instances[index] = instance;
                    group.Add(new LinkedEntityGroup { Value = Entities[i] });
                }
            }
            else
            {
                var slots = Slots[Root];
                for (int i = 0; i < Entities.Length; i++)
                {
                    slots[BrickMath.SlotIndex(Records[Start + i].Coord, Grid.BrickMin, Grid.BrickDims)] = new BrickSlot { Brick = Entities[i] };
                    group.Add(new LinkedEntityGroup { Value = Entities[i] });
                }
                int next = Start + Entities.Length;
                Spawning[Root] = new GridSpawning { NextBrick = next };
                Spawning.SetComponentEnabled(Root, next < Records.Length);
            }
        }
    }
}
