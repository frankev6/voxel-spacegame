using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.12: weapon-owned alive totals rebuilt only when the shared functional revision changes.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(ShipStatsAggregateSystem))]
    public partial struct WeaponStatsSystem : ISystem
    {
        StatsJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<BlockModulesRef>();
            job.Spawning = state.GetComponentLookup<GridSpawning>(true);
            job.Dormancy = state.GetComponentLookup<Dormant>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Spawning.Update(ref state); job.Dormancy.Update(ref state);
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value; job.Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        [BurstCompile, WithNone(typeof(Hulk))] partial struct StatsJob : IJobEntity
        {
            public BlobAssetReference<WeaponDefsBlob> Weapons;
            public BlobAssetReference<BlockModulesBlob> Modules;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            [ReadOnly] public ComponentLookup<Dormant> Dormancy;
            void Execute(Entity root, ref WeaponStats output, in ShipStats stats, in DynamicBuffer<BlockInstance> instances)
            {
                if (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root) || Dormancy.HasComponent(root) && Dormancy.IsComponentEnabled(root)) return;
                if (output.SourceRevision == stats.FunctionalRevision) return;
                bool first = output.SourceRevision == 0; int initial = output.InitialWeaponCount;
                output = default;
                ref var modules = ref Modules.Value; ref var weapons = ref Weapons.Value;
                foreach (var instance in instances)
                {
                    if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0 ||
                        !modules.TryGet(BlockModuleId.WeaponMount, instance.Block, out WeaponMountParams row) ||
                        row.WeaponIndex == 0 || row.WeaponIndex >= weapons.Weapons.Length) continue;
                    ref var weapon = ref weapons.Weapons[row.WeaponIndex];
                    output.SustainedDps += weapon.SustainedDps; output.MaxWeaponRange = math.max(output.MaxWeaponRange, weapon.Range);
                    output.WeaponDraw += weapon.PowerDraw; output.AliveWeaponCount++;
                }
                output.InitialWeaponCount = first ? output.AliveWeaponCount : initial;
                output.SourceRevision = stats.FunctionalRevision;
            }
        }
    }
}
