using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Simulation.Common;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Simulation.Flight
{
    /// <summary>Revision-cached functional thrust and change-filtered mass stats (SPEC-20 §5.7).</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(ShipStatsAggregateSystem)), UpdateBefore(typeof(PropulsionSystem))]
    public partial struct PropulsionStatsSystem : ISystem
    {
        StatsJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<FlightConfig>();
            state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<OrientationTablesRef>();
            job.Masses = state.GetComponentLookup<PhysicsMass>(true);
            job.Multipliers = state.GetComponentLookup<ShipStatMultipliers>(true);
            job.Stats = state.GetComponentLookup<ShipStats>(true);
            job.Spawning = state.GetComponentLookup<GridSpawning>(true);
            job.Dormancy = state.GetComponentLookup<Dormant>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Masses.Update(ref state); job.Multipliers.Update(ref state); job.Stats.Update(ref state);
            job.Spawning.Update(ref state); job.Dormancy.Update(ref state);
            job.Config = SystemAPI.GetSingleton<FlightConfig>(); job.Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value;
            job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; job.Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value;
            job.LastVersion = state.LastSystemVersion;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        [BurstCompile, WithNone(typeof(Hulk))] partial struct StatsJob : IJobEntity
        {
            public FlightConfig Config;
            public uint LastVersion;
            public BlobAssetReference<BlockModulesBlob> Modules;
            public BlobAssetReference<BlockDefsBlob> Blocks;
            public BlobAssetReference<OrientationTablesBlob> Orientations;
            [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
            [ReadOnly] public ComponentLookup<ShipStatMultipliers> Multipliers;
            [ReadOnly] public ComponentLookup<ShipStats> Stats;
            [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
            [ReadOnly] public ComponentLookup<Dormant> Dormancy;
            void Execute(Entity entity, ref PropulsionStats output, in ShipStats stats, in VoxelGrid grid, in DynamicBuffer<BlockInstance> instances)
            {
                if (Spawning.HasComponent(entity) && Spawning.IsComponentEnabled(entity) ||
                    Dormancy.HasComponent(entity) && Dormancy.IsComponentEnabled(entity)) return;
                var mass = Masses.HasComponent(entity) ? Masses[entity] : default;
                bool rebuild = output.SourceRevision != stats.FunctionalRevision;
                if (rebuild)
                {
                    output = default;
                    float left = 0, right = 0, positiveYaw = 0, negativeYaw = 0;
                    float3 com = Masses.HasComponent(entity) ? mass.CenterOfMass : stats.BoundsCenter;
                    ref var orientations = ref Orientations.Value;
                    for (int i = 0; i < instances.Length; i++)
                    {
                        var instance = instances[i];
                        if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0 ||
                            !Modules.Value.TryGet(BlockModuleId.Thruster, instance.Block, out ThrusterParams row)) continue;
                        float3 direction = GridSpaceMath.BlockDirToGrid(new float3(0, 0, -1), ref orientations, instance.Orient);
                        output.ForwardThrust += row.ThrustNewtons * math.max(0, direction.z);
                        output.ReverseThrust += row.ThrustNewtons * math.max(0, -direction.z);
                        right += row.ThrustNewtons * math.max(0, direction.x); left += row.ThrustNewtons * math.max(0, -direction.x);
                        ref var definition = ref Blocks.Value.Blocks[instance.Block];
                        float3 lever = GridSpaceMath.InstanceCenter(ref definition, ref orientations, instance.Orient, instance.Origin, grid.CellSize) - com;
                        float torque = math.cross(lever, row.ThrustNewtons * direction).y;
                        positiveYaw += math.max(0, torque); negativeYaw += math.max(0, -torque);
                        output.ThrusterDraw += row.PowerDraw; output.AliveThrusterCount++;
                    }
                    output.StrafeThrust = math.min(left, right); output.YawTorque = math.min(positiveYaw, negativeYaw);
                    output.SourceRevision = stats.FunctionalRevision;
                }
                if (!rebuild && !Stats.DidChange(entity, LastVersion) &&
                    !(Masses.HasComponent(entity) && Masses.DidChange(entity, LastVersion)) &&
                    !(Multipliers.HasComponent(entity) && Multipliers.DidChange(entity, LastVersion))) return;
                var multipliers = Multipliers.HasComponent(entity) ? Multipliers[entity] : default;
                float thrust = multipliers.Get(StatId.Thrust), turn = multipliers.Get(StatId.TurnRate);
                float inverseMass = stats.Mass > 0 ? 1 / stats.Mass : 0;
                output.ForwardAccel = output.ForwardThrust * thrust * inverseMass;
                output.ReverseAccel = output.ReverseThrust * thrust * inverseMass;
                output.StrafeAccel = output.StrafeThrust * thrust * inverseMass;
                output.GyroYawTorque = FlightMath.GyroYawTorque(Config, mass.InverseInertia.y);
                float yaw = inverseMass > 0 ? (output.YawTorque * thrust + output.GyroYawTorque) * turn * mass.InverseInertia.y : 0;
                output.YawAccelDeg = math.degrees(yaw);
                output.TurnRateDeg = yaw > 0 ? 90 / (2 * math.sqrt((math.PI / 2) / yaw)) : 0;
            }
        }
    }
}
