using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Grid
{
    /// <summary>Collects one grid's existing voxel children for batch rebuild destruction (SPEC-10 §5.7).</summary>
    [BurstCompile]
    internal struct CollectGridChildrenJob : IJob
    {
        [ReadOnly] public NativeArray<BrickSlot> Slots;
        [ReadOnly] public NativeArray<BlockInstance> Instances;
        [ReadOnly] public EntityStorageInfoLookup Storage;
        public NativeList<Entity> Children;
        public void Execute()
        {
            foreach (var slot in Slots) if (slot.Brick != Entity.Null && Storage.Exists(slot.Brick)) Children.Add(slot.Brick);
            foreach (var instance in Instances) if (instance.Functional != Entity.Null && Storage.Exists(instance.Functional)) Children.Add(instance.Functional);
        }
    }
}
