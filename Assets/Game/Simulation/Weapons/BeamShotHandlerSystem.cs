using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.5b: starts beam pulses from the shared delivery-agnostic shot list.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(WeaponFireSystem)), UpdateBefore(typeof(AbilityActivationGroup))]
    public partial struct BeamShotHandlerSystem : ISystem
    {
        ComponentLookup<BeamState> beams;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            beams = state.GetComponentLookup<BeamState>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponShots>(); state.RequireForUpdate<WeaponDefsRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            beams.Update(ref state); var shots = SystemAPI.GetSingleton<WeaponShots>();
            state.Dependency = new StartPulseJob { Shots = shots.Shots, Beams = beams, Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value }
                .Schedule(shots.Access.BeginRead(state.Dependency));
            shots.Access.EndRead(state.Dependency);
        }
        [BurstCompile] struct StartPulseJob : IJob
        {
            [ReadOnly] public NativeList<WeaponShot> Shots;
            [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;
            public ComponentLookup<BeamState> Beams;
            public void Execute()
            {
                foreach (var shot in Shots)
                {
                    if (shot.Delivery != WeaponDelivery.Beam || !Beams.HasComponent(shot.Weapon)) continue;
                    ref var weapon = ref Weapons.Value.Weapons[shot.Def];
                    var beam = Beams[shot.Weapon]; beam.Active = 1; beam.TicksLeft = (ushort)weapon.BeamPulseTicks;
                    beam.Width = weapon.BeamWidth; beam.Fx = weapon.HitFx; beam.HitSomething = 0;
                    Beams[shot.Weapon] = beam;
                    // TODO(WPN-009): cast, resolve first hit and decrement this pulse in BeamStepSystem.
                }
            }
        }
    }
}
