using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Spatial
{
    /// <summary>SPEC-23 §5.2: one deterministic stream partition per unfiltered chunk.</summary>
    [BurstCompile]
    public struct ProxyGatherJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<SpatialBody> Bodies;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<ShieldState> Shields;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<Allegiance> Allegiances;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<Dormant> Dormancy;
        [ReadOnly] public ComponentLookup<PlayerShip> Players;
        [ReadOnly] public ComponentLookup<AiBrain> Brains;
        public NativeStream.Writer Output;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Output.BeginForEachIndex(unfilteredChunkIndex);
            var entities = chunk.GetNativeArray(Entities);
            var transforms = chunk.GetNativeArray(ref Transforms);
            var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (iterator.NextEntityIndex(out int i))
            {
                Entity entity = entities[i]; var pose = transforms[i];
                if (!Grids.HasComponent(entity))
                {
                    float radius = Bodies[entity].Radius;
                    Output.Write(new SpatialProxy { Entity = entity, Center = pose.Position,
                        Radius = radius, HullRadius = radius, Kind = ProxyKind.Celestial, Allegiance = Allegiance.None });
                    continue;
                }
                if (Spawning.HasComponent(entity) && Spawning.IsComponentEnabled(entity)) continue;
                var grid = Grids[entity];
                float3 lo = (float3)grid.CellMin * grid.CellSize, hi = (float3)(grid.CellMax + 1) * grid.CellSize;
                float3 center = (lo + hi) * .5f;
                float hullRadius = math.length(hi - lo) * .5f, reach = 0;
                ProxyFlags flags = ProxyFlags.None;
                if (Players.HasComponent(entity)) flags |= ProxyFlags.PlayerShip;
                if (Brains.HasComponent(entity)) flags |= ProxyFlags.HasAi;
                if (Dormancy.HasComponent(entity) && Dormancy.IsComponentEnabled(entity)) flags |= ProxyFlags.Dormant;
                if (Shields.HasComponent(entity))
                {
                    var shield = Shields[entity];
                    if (shield.Active == 1) flags |= ProxyFlags.Shielded;
                    if (shield.Active == 1 || shield.MaxHp > 0)
                        reach = math.distance(shield.Center, center) + shield.BoundingRadius;
                }
                Output.Write(new SpatialProxy { Entity = entity, Center = pose.TransformPoint(center),
                    HullRadius = hullRadius, Radius = math.max(hullRadius, reach),
                    Kind = Hulks.HasComponent(entity) ? ProxyKind.Hulk : (ProxyKind)grid.Kind,
                    Flags = flags, Velocity = Velocities.HasComponent(entity) ? Velocities[entity].Linear : float3.zero,
                    Allegiance = Allegiances.HasComponent(entity) ? Allegiances[entity] : Allegiance.None });
            }
            Output.EndForEachIndex();
        }
    }
}
