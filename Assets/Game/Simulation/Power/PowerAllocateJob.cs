using Game.Core;
using Game.Simulation.Abilities;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Power
{
    /// <summary>Per-root deterministic energy allocation, split around one-shot ability costs (SPEC-22 §5.3).</summary>
    [BurstCompile]
    internal partial struct PowerAllocateJob : IJobEntity
    {
        public uint Tick;
        public PowerTuning Tuning;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public BufferLookup<PowerRequest> Requests;
        [ReadOnly] public BufferLookup<AbilitySlot> Abilities;
        [ReadOnly] public ComponentLookup<Inhibited> Inhibits;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;

        void Execute(Entity root, ref ShipPower power, DynamicBuffer<PowerConsumer> consumers, in PowerStats stats)
        {
            if ((power.Flags & ShipPowerFlags.Initialized) == 0 || Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) return;
            if (Hulks.HasComponent(root))
            {
                power.Stored = power.Generation = power.Capacity = power.DemandPerSecond = power.GrantedPerSecond = 0;
                power.AbilityGrantMask = 0; power.AbilityGrantTick = Tick; power.Flags |= ShipPowerFlags.Dead;
                for (int i = 0; i < consumers.Length; i++)
                { var consumer = consumers[i]; consumer.Flags = Alive(root, consumer) ? PowerConsumerFlags.Alive : 0; consumers[i] = consumer; }
                return;
            }
            if (stats.SourceRevision != 0)
            { power.Generation = stats.Generation; power.Capacity = stats.Capacity; power.Stored = math.min(power.Stored, power.Capacity); }
            float budget = power.Stored + power.Generation * SimTime.FixedTimestep;
            float demand = 0, granted = 0; bool brownout = false;
            int split = 0;
            while (split < consumers.Length && consumers[split].Priority > Tuning.PriorityAbility)
            { Allocate(root, consumers, split, ref budget, ref demand, ref granted, ref brownout); split++; }
            uint mask = 0;
            if (Abilities.HasBuffer(root))
            {
                var slots = Abilities[root];
                for (int i = 0; i < math.min(32, slots.Length); i++)
                    if (slots[i].State == AbilitySlotState.PendingPower && slots[i].PendingCost <= budget)
                    { budget -= slots[i].PendingCost; mask |= 1u << i; }
            }
            for (int i = split; i < consumers.Length; i++) Allocate(root, consumers, i, ref budget, ref demand, ref granted, ref brownout);
            power.AbilityGrantMask = mask; power.AbilityGrantTick = Tick;
            power.Stored = math.clamp(budget, 0, power.Capacity); power.DemandPerSecond = demand; power.GrantedPerSecond = granted;
            power.Flags = ShipPowerFlags.Initialized | (brownout ? ShipPowerFlags.Brownout : 0);
        }

        bool Alive(Entity root, in PowerConsumer consumer)
        {
            if (!Instances.HasBuffer(root)) return false;
            var instances = Instances[root];
            if ((uint)consumer.InstanceIndex >= (uint)instances.Length) return false;
            var instance = instances[consumer.InstanceIndex];
            return instance.Block != 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0 &&
                (!Inhibits.HasComponent(consumer.Functional) || Tick >= Inhibits[consumer.Functional].UntilTick);
        }

        void Allocate(Entity root, DynamicBuffer<PowerConsumer> consumers, int index, ref float budget,
            ref float demand, ref float grantedDraw, ref bool brownout)
        {
            var consumer = consumers[index]; bool alive = Alive(root, consumer), requesting = false, granted = false;
            if (alive && Requests.HasBuffer(consumer.Functional))
            { var requests = Requests[consumer.Functional]; requesting = consumer.RequestIndex < requests.Length && requests[consumer.RequestIndex].Requesting == 1; }
            if (requesting)
            {
                demand += consumer.Draw;
                if (Tick >= consumer.DeniedUntilTick)
                {
                    float need = consumer.Draw * SimTime.FixedTimestep;
                    if (need <= budget) { budget -= need; grantedDraw += consumer.Draw; granted = true; }
                    else { consumer.DeniedUntilTick = Tick + (uint)Tuning.BrownoutHoldTicks; brownout = true; }
                }
            }
            consumer.Flags = (alive ? PowerConsumerFlags.Alive : 0) | (requesting ? PowerConsumerFlags.Requesting : 0) |
                (granted ? PowerConsumerFlags.Granted : 0) | (Tick < consumer.DeniedUntilTick ? PowerConsumerFlags.BrownedOut : 0);
            consumers[index] = consumer;
        }
    }
}
