using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.7 pulse lifecycle, brought forward for WPN-007's duration contract.</summary>
    // TODO(WPN-009): parallel first-hit casts, shield/hull damage streams, hit endpoints and impact effects.
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ProjectileStepSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public partial struct BeamStepSystem : ISystem
    {
        PulseJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Transforms = state.GetComponentLookup<LocalTransform>(true); job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Power = state.GetComponentLookup<Powered>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Transforms.Update(ref state); job.Hulks.Update(ref state); job.Power.Update(ref state);
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] partial struct PulseJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            [ReadOnly] public ComponentLookup<Powered> Power;
            [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;
            void Execute(Entity entity, in FunctionalBlock functional, in WeaponMount mount, in WeaponAim aim, ref BeamState beam)
            {
                if (beam.Active == 0) return;
                if (!Transforms.HasComponent(functional.Grid) || Hulks.HasComponent(functional.Grid) ||
                    (Power.HasComponent(entity) && !Power.IsComponentEnabled(entity)) || beam.TicksLeft == 0 ||
                    mount.WeaponIndex == 0 || mount.WeaponIndex >= Weapons.Value.Weapons.Length)
                { beam.Active = 0; beam.TicksLeft = 0; return; }
                var transform = Transforms[functional.Grid];
                beam.MuzzleLocal = mount.PivotLocal + math.rotate(quaternion.AxisAngle(mount.UpLocal, aim.Yaw), mount.MuzzleLocal - mount.PivotLocal);
                beam.EndWorld = transform.TransformPoint(beam.MuzzleLocal) + math.rotate(transform.Rotation, aim.FireDirLocal) * Weapons.Value.Weapons[mount.WeaponIndex].BeamRange;
                beam.HitSomething = 0;
                // TODO(WPN-009): resolve the first physical hit before advancing this pulse clock.
                beam.TicksLeft--;
                if (beam.TicksLeft == 0) beam.Active = 0;
            }
        }
    }
}
