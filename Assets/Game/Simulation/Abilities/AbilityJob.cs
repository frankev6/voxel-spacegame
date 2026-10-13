using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Abilities
{
    /// <summary>SPEC-24 §5.1. Each parallel invocation exclusively owns one root's slots and state.</summary>
    [BurstCompile]
    internal partial struct AbilityJob : IJobEntity
    {
        public uint Tick;
        [ReadOnly] public BlobAssetReference<AbilityDefsBlob> Definitions;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public ComponentLookup<ShipStats> Stats;
        [ReadOnly] public ComponentLookup<ShipPower> Power;
        [ReadOnly] public ComponentLookup<ShipCommand> Commands;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<PlayerShip> Players;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        // Atomic presentation-only writers, with drain/clear lifetime guards owned by AbilitySystem.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;

        void Execute(Entity root, ref AbilityState state, DynamicBuffer<AbilitySlot> slots)
        {
            if (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) return;
            state.Resynced = 0;
            FixedList512Bytes<AbilitySlot> previous = default;
            for (int i = 0; i < slots.Length; i++)
            { var slot = slots[i]; slot.Flags = 0; slots[i] = slot; previous.Add(slot); }
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
            uint revision = 1; bool sync = state.SyncedRevision == 0;
            if (Stats.HasComponent(root))
            {
                var stats = Stats[root]; revision = stats.FunctionalRevision;
                sync = (stats.Flags & ShipStatsFlags.Initialized) != 0 && revision != state.SyncedRevision;
            }
            if (sync && Instances.HasBuffer(root)) Resync(root, revision, ref state, slots, previous);
            var edges = Commands.HasComponent(root) ? Commands[root].Edges : ShipCommandEdges.None;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i]; ref var def = ref Definitions.Value.Abilities[slot.AbilityIndex];
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
                if (def.DurationTicks > 0 && slot.Active != 0 && Tick >= slot.ActiveUntilTick) slot.Active = 0;
                if (slot.State == AbilitySlotState.PendingPower && Tick > slot.PendingSinceTick)
                {
                    int grantIndex = i;
                    if (state.Resynced != 0)
                        for (int old = 0; old < previous.Length; old++)
                            if (previous[old].AbilityIndex == slot.AbilityIndex) { grantIndex = old; break; }
                    bool granted = !Power.HasComponent(root) || (Power[root].AbilityGrantTick == Tick && (Power[root].AbilityGrantMask & (1u << grantIndex)) != 0);
                    if (granted) Activate(root, ref slot, ref def);
                    else { slot.State = AbilitySlotState.Ready; slot.Flags |= AbilitySlotFlags.DeniedThisTick; slot.PendingCost = 0; }
                }
                // Expired timers make a slot available before processing this tick's edge.
                RefreshState(ref slot, ref def);
                if (i < 4 && ((ushort)edges & ((ushort)ShipCommandEdges.Ability1 << i)) != 0)
                {
                    if (slot.State != AbilitySlotState.Ready || slot.Charges == 0 || slot.CooldownRemainingTicks > 0 || slot.Blocked != 0)
                        slot.Flags |= AbilitySlotFlags.DeniedThisTick;
                    else if (def.EnergyCost > 0)
                    { slot.State = AbilitySlotState.PendingPower; slot.PendingCost = def.EnergyCost; slot.PendingSinceTick = Tick; }
                    else Activate(root, ref slot, ref def);
                }
                RefreshState(ref slot, ref def); slots[i] = slot;
            }
        }

        void Resync(Entity root, uint revision, ref AbilityState state, DynamicBuffer<AbilitySlot> slots, in FixedList512Bytes<AbilitySlot> previous)
        {
            slots.Clear(); var instances = Instances[root]; bool overflow = false;
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Block == 0 || instance.Block >= Blocks.Value.Blocks.Length || (instance.Flags & BlockInstanceFlags.Destroyed) != 0) continue;
                ref var abilities = ref Blocks.Value.Blocks[instance.Block].AbilityIndices;
                for (int a = 0; a < abilities.Length; a++)
                {
                    ushort index = abilities[a]; if (index == 0 || index >= Definitions.Value.Abilities.Length) continue;
                    bool exists = false; for (int j = 0; j < slots.Length; j++) if (slots[j].AbilityIndex == index) { exists = true; break; }
                    if (exists) continue;
                    if (slots.Length == 8) { overflow = true; continue; }
                    ref var def = ref Definitions.Value.Abilities[index];
                    var slot = new AbilitySlot { Ability = def.IdHash, AbilityIndex = index, Charges = def.MaxCharges, CooldownTicks = def.CooldownTicks, State = AbilitySlotState.Ready };
                    for (int j = 0; j < previous.Length; j++) if (previous[j].AbilityIndex == index) { slot = previous[j]; break; }
                    slot.FirstInstanceIndex = i; slot.Flags |= AbilitySlotFlags.Resynced; slots.Add(slot);
                }
            }
            state.SlotCount = (byte)slots.Length; state.SyncedRevision = revision; state.Resynced = 1;
            if (overflow && state.OverflowWarned == 0)
            { GameLog.Burst.Warn(new FixedString128Bytes("Ability slots exceed 8; retaining the first eight in block instance order.")); state.OverflowWarned = 1; }
        }

        void Activate(Entity root, ref AbilitySlot slot, ref AbilityDef def)
        {
            bool startRecharge = slot.Charges == def.MaxCharges;
            slot.Charges--; slot.CooldownRemainingTicks = slot.CooldownTicks; slot.PendingCost = 0;
            slot.State = AbilitySlotState.Ready; slot.Flags |= AbilitySlotFlags.ActivatedThisTick;
            if (def.MaxCharges > 1 && startRecharge) slot.ChargeRechargeRemainingTicks = math.max(1, def.ChargeRechargeTicks);
            if (def.DurationTicks > 0) { slot.Active = 1; slot.ActiveUntilTick = Tick + def.DurationTicks; }
            var transform = Transforms.HasComponent(root) ? Transforms[root] : LocalTransform.Identity;
            bool player = Players.HasComponent(root);
            if (def.ActivationCue.Value != 0) Sfx.Enqueue(new SfxEvent { Cue = def.ActivationCue, Pos = transform.Position, Volume = 1, Flags = player ? SfxEventFlags.PlayerSource : 0 });
            if (def.ActivationFx.Value != 0) Fx.Enqueue(new FxEvent { Kind = FxKind.Muzzle, Fx = def.ActivationFx, Pos = transform.Position,
                Dir = math.rotate(transform.Rotation, new float3(0, 0, 1)), Vel = Velocities.HasComponent(root) ? Velocities[root].Linear : default,
                Scale = 2, Flags = player ? FxEventFlags.PlayerSource : 0 });
        }
        static void RefreshState(ref AbilitySlot slot, ref AbilityDef def)
        {
            if (slot.State == AbilitySlotState.PendingPower || slot.State == AbilitySlotState.Unavailable) return;
            slot.State = slot.Active != 0 && def.DurationTicks > 0 ? AbilitySlotState.Active :
                slot.CooldownRemainingTicks > 0 || slot.Charges == 0 ? AbilitySlotState.Cooldown : AbilitySlotState.Ready;
        }
    }
}
