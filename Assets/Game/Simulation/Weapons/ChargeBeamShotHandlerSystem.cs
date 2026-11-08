using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §4.6d (WPN-019): the charge beam's delivery handler. A ChargeBeam shot starts a charge on an idle weapon,
    /// pointing where the round was fired; ChargeBeamStepSystem runs the rest.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(WeaponFireSystem)), UpdateBefore(typeof(AbilityActivationGroup))]
    public partial struct ChargeBeamShotHandlerSystem : ISystem
    {
        ComponentLookup<ChargeBeamState> charges;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            charges = state.GetComponentLookup<ChargeBeamState>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponShots>(); state.RequireForUpdate<WeaponDefsRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            charges.Update(ref state); var shots = SystemAPI.GetSingleton<WeaponShots>();
            state.Dependency = new StartChargeJob { Shots = shots.Shots, Charges = charges, Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value }
                .Schedule(shots.Access.BeginRead(state.Dependency));
            shots.Access.EndRead(state.Dependency);
        }
        [BurstCompile] struct StartChargeJob : IJob
        {
            [ReadOnly] public NativeList<WeaponShot> Shots;
            [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;
            public ComponentLookup<ChargeBeamState> Charges;
            public void Execute()
            {
                ref var blob = ref Weapons.Value;
                foreach (var shot in Shots)
                {
                    if (shot.Delivery != WeaponDelivery.ChargeBeam || !Charges.HasComponent(shot.Weapon)) continue;
                    if (shot.Def == 0 || shot.Def >= blob.Weapons.Length || shot.Def >= blob.ChargeBeams.Length) continue;
                    ref var charge = ref blob.ChargeBeams[shot.Def];
                    var current = Charges[shot.Weapon];
                    if (charge.ChargeTicks == 0 || current.Phase != ChargeBeamPhase.Idle) continue;
                    ref var weapon = ref blob.Weapons[shot.Def];
                    ushort ticks = (ushort)math.min(charge.ChargeTicks, ushort.MaxValue);
                    Charges[shot.Weapon] = new ChargeBeamState
                    {
                        Phase = ChargeBeamPhase.Charging, TicksLeft = ticks, PhaseTicks = ticks, Weapon = shot.Def,
                        DirWorld = math.normalizesafe(shot.DirWorld), EndWorld = shot.MuzzleWorld, Width = weapon.BeamWidth, Fx = weapon.HitFx,
                    };
                }
            }
        }
    }
}
