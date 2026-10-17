using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation.Power;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>ADR-0117: the authority's latest slot state for this ship, as a co-op guest received it. Written on the main thread by
    /// the network layer, applied once in the next fixed tick by <see cref="AbilitySlotApplySystem"/>. ActiveUntilTick holds the
    /// ticks the boost had left, since the two machines' clocks differ.</summary>
    [InternalBufferCapacity(0)]
    public struct AbilityHostSlot : IBufferElementData { public AbilitySlot Value; }

    public struct AbilityHostState : IComponentData
    {
        public byte Pending;             // 1 = AbilityHostSlot holds state not yet applied
        public byte ShieldRequestedOn;   // the authority's ShieldControl.RequestedOn; 2 = the ship has none
        public float ChargeTotal;        // E: one-shot costs of the authority's activations applied here, summed (written by the applier);
                                         // PowerAllocateJob charges what it has not charged yet, so the guest's energy meter pays them too
    }

    /// <summary>ADR-0117 applier for a co-op guest: copies the host's slot decisions into AbilitySlot, so the hotbar shows them and the
    /// shared appliers after it (shield toggle, stat boost, activation feedback) act on them exactly as they act on the host's own.
    /// A slot whose activation count moved is flagged ActivatedThisTick once; a changed slot list is flagged Resynced. Runs on every
    /// machine; only a guest's network layer writes the input.</summary>
    [BurstCompile, CreateAfter(typeof(GridSpawnSystem)), UpdateInGroup(typeof(AbilityActivationGroup)), UpdateAfter(typeof(AbilityTimerSystem)),
     UpdateBefore(typeof(AbilitySystem))]
    public partial struct AbilitySlotApplySystem : ISystem
    {
        ApplyJob job;
        public void OnCreate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonBuffer<RootComponentRegistration>(out var registrations)) AbilityGridSetup.RegisterHost(registrations);
            job.Shields = state.GetComponentLookup<ShieldControl>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AbilityDefsRef>(); state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<AbilityHostState>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Shields.Update(ref state);
            job.Definitions = SystemAPI.GetSingleton<AbilityDefsRef>().Value; job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        partial struct ApplyJob : IJobEntity
        {
            public uint Tick;
            [ReadOnly] public BlobAssetReference<AbilityDefsBlob> Definitions;
            public ComponentLookup<ShieldControl> Shields;
            void Execute(Entity root, ref AbilityHostState host, ref AbilityState state, DynamicBuffer<AbilitySlot> slots, in DynamicBuffer<AbilityHostSlot> incoming)
            {
                if (host.Pending == 0) return;
                host.Pending = 0;
                bool sameList = slots.Length == incoming.Length;
                for (int i = 0; sameList && i < slots.Length; i++) sameList = slots[i].AbilityIndex == incoming[i].Value.AbilityIndex;
                FixedList512Bytes<AbilitySlot> previous = default;
                for (int i = 0; i < slots.Length; i++) previous.Add(slots[i]);
                slots.Clear(); bool toggled = false;
                for (int i = 0; i < incoming.Length && i < 8; i++)
                {
                    var slot = incoming[i].Value;
                    bool known = false; byte seen = 0;
                    for (int j = 0; j < previous.Length; j++) if (previous[j].AbilityIndex == slot.AbilityIndex) { known = true; seen = previous[j].ActivationCount; break; }
                    slot.Flags = sameList ? AbilitySlotFlags.None : AbilitySlotFlags.Resynced;
                    // A slot first seen here (a join, a spawn, a refit) brings its history along: none of it is replayed.
                    if (known && slot.ActivationCount != seen)
                    {
                        slot.Flags |= AbilitySlotFlags.ActivatedThisTick;
                        // The authority's allocator drew the one-shot cost of each activation; this machine's allocator draws it next.
                        if (slot.AbilityIndex < Definitions.Value.Abilities.Length && Definitions.Value.Abilities[slot.AbilityIndex].EnergyCost > 0)
                            host.ChargeTotal += Definitions.Value.Abilities[slot.AbilityIndex].EnergyCost * (byte)(slot.ActivationCount - seen);
                        toggled |= slot.AbilityIndex < Definitions.Value.Abilities.Length && Definitions.Value.Abilities[slot.AbilityIndex].Kind == AbilityKind.ShieldToggle;
                    }
                    slot.ActiveUntilTick = Tick + slot.ActiveUntilTick;   // remaining ticks, rebased onto this machine's clock
                    // A power request is the authority's to grant; here it would only draw this machine's energy again.
                    if (slot.State == AbilitySlotState.PendingPower) slot.State = AbilitySlotState.Ready;
                    slot.PendingCost = 0;
                    slots.Add(slot);
                }
                state.SlotCount = (byte)slots.Length;
                if (!sameList) state.Resynced = 1;
                // ShieldToggleEffectSystem flips RequestedOn for this tick's toggle; start it from the value before that flip.
                if (host.ShieldRequestedOn <= 1 && Shields.HasComponent(root))
                {
                    var control = Shields[root]; control.RequestedOn = (byte)(host.ShieldRequestedOn ^ (toggled ? 1 : 0)); Shields[root] = control;
                }
            }
        }
    }
}
