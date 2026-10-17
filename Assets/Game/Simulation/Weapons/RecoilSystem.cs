using Game.Core.Systems;
using Game.Data.Combat;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-91 §5.1 step 1 (HAZ-004): every projectile or missile fired this tick pushes its shooter back at the muzzle with
    /// RecoilMomentum, so an off-centre mount also yaws the ship. One ImpulseRequest per pellet (Producer Recoil, Sequence = shot id);
    /// beams have none. Every machine (a guest's own ship feels its own recoil at once); bodies without mass drop the request.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(ImpulseApplySystem))]
    public partial struct RecoilSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state)
        { state.RequireForUpdate<WeaponShots>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<DamageStreams>(); }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var shots = SystemAPI.GetSingleton<WeaponShots>();
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            var job = new RecoilJob { Shots = shots.Shots, Defs = SystemAPI.GetSingleton<WeaponDefsRef>().Value,
                Impulses = streams.Impulses.BeginWrite(ref state, ProducerKind.Recoil, 1) };
            var handle = job.Schedule(shots.Access.BeginRead(state.Dependency));
            streams.Impulses.EndWrite(handle); shots.Access.EndRead(handle);
            state.Dependency = handle;
        }

        [BurstCompile] struct RecoilJob : IJob
        {
            [ReadOnly] public NativeList<WeaponShot> Shots;
            public BlobAssetReference<WeaponDefsBlob> Defs;
            public NativeStream.Writer Impulses;
            public void Execute()
            {
                Impulses.BeginForEachIndex(0);
                ref var weapons = ref Defs.Value.Weapons;
                for (int i = 0; i < Shots.Length; i++)
                {
                    var shot = Shots[i];
                    if (shot.Def == 0 || shot.Def >= weapons.Length) continue;
                    float j = weapons[shot.Def].RecoilMomentum;
                    if (!(j > 0) || shot.Grid == Entity.Null) continue;
                    Impulses.Write(new ImpulseRequest { Body = shot.Grid, WorldImpulse = -shot.DirWorld * j, WorldPoint = shot.MuzzleWorld,
                        Producer = ProducerKind.Recoil, Sequence = shot.ShotId });
                }
                Impulses.EndForEachIndex();
            }
        }
    }
}
