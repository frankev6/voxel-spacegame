using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation.Power;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>SPEC-24 §5.2: owns requested shield toggles and the toggle slots' Active/Blocked fields.</summary>
    [BurstCompile, UpdateInGroup(typeof(AbilityActivationGroup)), UpdateAfter(typeof(AbilitySystem))]
    public partial struct ShieldToggleEffectSystem : ISystem
    {
        ToggleJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AbilityDefsRef>();
            job.Spawning = state.GetComponentLookup<GridSpawning>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Spawning.Update(ref state); job.Definitions = SystemAPI.GetSingleton<AbilityDefsRef>().Value;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] partial struct ToggleJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<AbilityDefsBlob> Definitions;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            void Execute(Entity root, DynamicBuffer<AbilitySlot> slots, ref ShieldControl control, in ShieldState shield)
            {
                if (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) return;
                bool toggle = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.AbilityIndex == 0 || slot.AbilityIndex >= Definitions.Value.Abilities.Length ||
                        Definitions.Value.Abilities[slot.AbilityIndex].Kind != AbilityKind.ShieldToggle || slot.State == AbilitySlotState.Unavailable) continue;
                    toggle |= (slot.Flags & AbilitySlotFlags.ActivatedThisTick) != 0;
                }
                if (toggle) control.RequestedOn ^= 1;
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.AbilityIndex == 0 || slot.AbilityIndex >= Definitions.Value.Abilities.Length) continue;
                    ref var definition = ref Definitions.Value.Abilities[slot.AbilityIndex];
                    if (definition.Kind != AbilityKind.ShieldToggle) continue;
                    bool unavailable = slot.State == AbilitySlotState.Unavailable;
                    slot.Active = unavailable ? (byte)0 : control.RequestedOn;
                    slot.Blocked = unavailable || control.RequestedOn == 0 &&
                        !(shield.MaxHp > 0 && shield.Hp >= definition.ShieldEnableThreshold * shield.MaxHp) ? (byte)1 : (byte)0;
                    slots[i] = slot;
                }
            }
        }
    }
}
