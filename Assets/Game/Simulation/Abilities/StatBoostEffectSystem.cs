using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation.Power;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>SPEC-24 §5.3 / ADR-0069: reconciles ability-owned modifier keys through the sole status writer.</summary>
    [BurstCompile, UpdateInGroup(typeof(AbilityActivationGroup)), UpdateAfter(typeof(AbilitySystem))]
    public partial struct StatBoostEffectSystem : ISystem
    {
        EntityQuery roots;
        BoostJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            roots = new EntityQueryBuilder(Allocator.Temp).WithAll<AbilityState>().WithAllRW<AbilitySlot>().Build(ref state);
            job.Spawning = state.GetComponentLookup<GridSpawning>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AbilityDefsRef>(); state.RequireForUpdate<StatusChannels>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            if (roots.IsEmptyIgnoreFilter) return;
            job.Spawning.Update(ref state); job.Definitions = SystemAPI.GetSingleton<AbilityDefsRef>().Value;
            var channel = SystemAPI.GetSingleton<StatusChannels>().StatModifiers;
            job.Requests = channel.BeginWrite(ref state, ProducerKind.Ability, roots.CalculateChunkCountWithoutFiltering());
            state.Dependency = job.ScheduleParallel(roots, state.Dependency);
            channel.EndWrite(state.Dependency);
        }
        [BurstCompile] partial struct BoostJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            [ReadOnly] public BlobAssetReference<AbilityDefsBlob> Definitions;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            public NativeStream.Writer Requests;
            // The generated chunk job owns one unfiltered chunk index; its stream lane includes all of that chunk's roots.
            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            { Requests.BeginForEachIndex(unfilteredChunkIndex); return true; }
            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
                => Requests.EndForEachIndex();
            void Execute(Entity root, in AbilityState state, DynamicBuffer<AbilitySlot> slots)
            {
                if (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) return;
                bool resynced = state.Resynced != 0;
                for (int i = 0; i < slots.Length; i++) resynced |= (slots[i].Flags & AbilitySlotFlags.Resynced) != 0;
                if (resynced)
                    for (ushort slot = 0; slot < 8; slot++)
                        for (int stat = 0; stat <= (int)StatId.TurnRate; stat++)
                            Requests.Write(new StatModifierRequest { Root = root, Producer = ProducerKind.Ability, SourceId = slot,
                                Stat = (StatId)stat, Multiplier = 1, Sequence = (uint)(slot * 16 + stat) });
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.AbilityIndex == 0 || slot.AbilityIndex >= Definitions.Value.Abilities.Length) continue;
                    ref var definition = ref Definitions.Value.Abilities[slot.AbilityIndex];
                    if (definition.Kind != AbilityKind.StatBoost) continue;
                    slot.Blocked = 0; slots[i] = slot;
                    if (slot.State == AbilitySlotState.Unavailable || (resynced ? slot.Active == 0 : (slot.Flags & AbilitySlotFlags.ActivatedThisTick) == 0)) continue;
                    ref var mods = ref definition.Mods;
                    for (int mod = 0; mod < mods.Length; mod++)
                        Requests.Write(new StatModifierRequest { Root = root, Producer = ProducerKind.Ability, SourceId = (ushort)i,
                            Stat = mods[mod].Stat, Multiplier = mods[mod].Multiplier, UntilTick = slot.ActiveUntilTick,
                            Sequence = (uint)(1000 + i * 8 + mod) });
                }
            }
        }
    }
}
