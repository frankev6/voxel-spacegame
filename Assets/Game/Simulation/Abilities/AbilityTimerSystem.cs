using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Abilities
{
    /// <summary>ADR-0117 derivation, every machine: the per-tick part of the SPEC-24 §5.1 lifecycle that follows from the slots alone.
    /// Clears last tick's flags, makes a hulk's slots unavailable, and advances cooldowns, charge recharge and boost expiry. Acting on
    /// presses and power grants is AbilitySystem's (authority), which runs right after it; a co-op guest's slots then come from the
    /// host's decisions (AbilitySlotApplySystem) and keep counting down here between them.</summary>
    [BurstCompile, UpdateInGroup(typeof(AbilityActivationGroup)), UpdateBefore(typeof(AbilitySystem))]
    public partial struct AbilityTimerSystem : ISystem
    {
        TimerJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Hulks = state.GetComponentLookup<Hulk>(true); job.Spawning = state.GetComponentLookup<GridSpawning>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AbilityDefsRef>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Hulks.Update(ref state); job.Spawning.Update(ref state);
            job.Definitions = SystemAPI.GetSingleton<AbilityDefsRef>().Value; job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        partial struct TimerJob : IJobEntity
        {
            public uint Tick;
            [ReadOnly] public BlobAssetReference<AbilityDefsBlob> Definitions;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            void Execute(Entity root, ref AbilityState state, DynamicBuffer<AbilitySlot> slots)
            {
                if (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) return;
                state.Resynced = 0;
                for (int i = 0; i < slots.Length; i++) { var slot = slots[i]; slot.Flags = 0; slots[i] = slot; }
                if (Hulks.HasComponent(root))
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var slot = slots[i];
                        if (slot.State != AbilitySlotState.Unavailable) { state.Resynced = 1; slot.Flags = AbilitySlotFlags.Resynced; }
                        slot.State = AbilitySlotState.Unavailable; slot.Active = 0; slot.PendingCost = 0;
                        slots[i] = slot;
                    }
                    return;
                }
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.AbilityIndex >= Definitions.Value.Abilities.Length) continue;
                    AbilityLifecycle.Advance(ref slot, ref Definitions.Value.Abilities[slot.AbilityIndex], Tick);
                    slots[i] = slot;
                }
            }
        }
    }

    /// <summary>The shared slot rules (SPEC-24 §5.1): timers, used by AbilityTimerSystem, and the state a slot's timers imply, used by it
    /// and by AbilitySystem.</summary>
    public static class AbilityLifecycle
    {
        public static void Advance(ref AbilitySlot slot, ref AbilityDef def, uint tick)
        {
            slot.CooldownRemainingTicks = math.max(0, slot.CooldownRemainingTicks - 1);
            if (def.MaxCharges == 1)
            { if (slot.CooldownRemainingTicks == 0) slot.Charges = 1; }
            else if (slot.Charges < def.MaxCharges)
            {
                if (slot.ChargeRechargeRemainingTicks <= 0) slot.ChargeRechargeRemainingTicks = math.max(1, def.ChargeRechargeTicks);
                else slot.ChargeRechargeRemainingTicks -= 1;
                if (slot.ChargeRechargeRemainingTicks <= 0)
                { slot.Charges++; slot.ChargeRechargeRemainingTicks = slot.Charges < def.MaxCharges ? math.max(1, def.ChargeRechargeTicks) : 0; }
            }
            if (def.DurationTicks > 0 && slot.Active != 0 && tick >= slot.ActiveUntilTick) slot.Active = 0;
            RefreshState(ref slot, ref def);   // expired timers make a slot available before this tick's press
        }

        public static void RefreshState(ref AbilitySlot slot, ref AbilityDef def)
        {
            if (slot.State == AbilitySlotState.PendingPower || slot.State == AbilitySlotState.Unavailable) return;
            slot.State = slot.Active != 0 && def.DurationTicks > 0 ? AbilitySlotState.Active :
                slot.CooldownRemainingTicks > 0 || slot.Charges == 0 ? AbilitySlotState.Cooldown : AbilitySlotState.Ready;
        }
    }
}
