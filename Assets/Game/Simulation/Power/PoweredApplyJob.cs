using Game.Voxel.Model;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Chunk-owned enable-bit writes after allocation; never parallel random writes to shared enable masks (SPEC-22 §5.3).</summary>
    [BurstCompile]
    internal struct PoweredApplyJob : IJobChunk
    {
        public uint Tick;
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<FunctionalBlock> Functionals;
        [ReadOnly] public ComponentTypeHandle<PowerLink> Links;
        public ComponentTypeHandle<Powered> Powered;
        [ReadOnly] public ComponentLookup<ShipPower> Powers;
        [ReadOnly] public ComponentLookup<Inhibited> Inhibits;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public BufferLookup<PowerConsumer> Consumers;
        [ReadOnly] public BufferLookup<PowerRequest> Requests;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(Entities);
            var functionals = chunk.GetNativeArray(ref Functionals); var links = chunk.GetNativeArray(ref Links);
            // Query ignores Powered's enable state so previously denied consumers can recover.
            for (int i = 0; i < chunk.Count; i++)
                chunk.SetComponentEnabled(ref Powered, i, IsPowered(entities[i], functionals[i], links[i]));
        }
        bool IsPowered(Entity entity, in FunctionalBlock functional, in PowerLink link)
        {
            Entity root = functional.Grid;
            if (!Powers.HasComponent(root)) return true;
            var power = Powers[root];
            if ((power.Flags & ShipPowerFlags.Dead) != 0 || (power.Flags & ShipPowerFlags.Initialized) == 0) return false;
            if (link.ConsumerCount == 0)
            {
                if (!Instances.HasBuffer(root)) return false;
                var instances = Instances[root];
                if ((uint)functional.InstanceIndex >= (uint)instances.Length) return false;
                var instance = instances[functional.InstanceIndex];
                return instance.Block != 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0 &&
                    (!Inhibits.HasComponent(entity) || Tick >= Inhibits[entity].UntilTick);
            }
            if (!Requests.HasBuffer(entity) || !Consumers.HasBuffer(root)) return false;
            var requests = Requests[entity]; var consumers = Consumers[root];
            if (requests.Length == 0) return false;
            for (int i = 0; i < requests.Length; i++)
            {
                int index = requests[i].ConsumerIndex;
                if ((uint)index >= (uint)consumers.Length) return false;
                var flags = consumers[index].Flags;
                if ((flags & PowerConsumerFlags.Alive) == 0 || (flags & PowerConsumerFlags.BrownedOut) != 0) return false;
            }
            return true;
        }
    }
}
