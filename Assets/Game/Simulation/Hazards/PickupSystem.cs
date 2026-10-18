using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Hazards
{
    /// <summary>SPEC-91 HAZ-019 (ADR-0119 decision 7), on the authority: a ship whose bounding sphere comes within an energy canister's
    /// reach collects it. The nearest such ship (ties by Entity.Index) is granted the canister's energy through
    /// StatusChannels.EnergyGrants (applied by PowerAllocationSystem) and the canister despawns, so it grants once. A canister shot apart
    /// explodes through its Explosive block and grants nothing.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageStreamsSystem))]
    public partial struct PickupSystem : ISystem
    {
        EntityQuery canisters, ships;
        public void OnCreate(ref SystemState state)
        {
            canisters = state.GetEntityQuery(typeof(Canister), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<PhysicsMass>());
            ships = state.GetEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<PhysicsMass>(),
                ComponentType.ReadOnly<ShipPower>(), ComponentType.Exclude<Hulk>());
            state.RequireForUpdate(canisters); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<StatusChannels>();
            state.RequireForUpdate<BlockModulesRef>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            var status = SystemAPI.GetSingleton<StatusChannels>();
            ref var modules = ref SystemAPI.GetSingleton<BlockModulesRef>().Value.Value;
            using var entities = canisters.ToEntityArray(Allocator.Temp);
            using var shipEntities = ships.ToEntityArray(Allocator.Temp);
            var done = new NativeList<Entity>(Allocator.Temp);
            for (int c = 0; c < entities.Length; c++)
            {
                var e = entities[c];
                if (!em.HasBuffer<BlockInstance>(e)) continue;   // not spawned yet
                var canister = em.GetComponentData<Canister>(e);
                var instances = em.GetBuffer<BlockInstance>(e, true);
                if (canister.Instance < 0)
                {
                    for (int i = 0; i < instances.Length; i++)
                        if (instances[i].Block != 0 && modules.TryGet(BlockModuleId.Pickup, instances[i].Block, out PickupParams p))
                        { canister.Instance = i; canister.Energy = p.Energy; canister.Reach = p.Reach; break; }
                    if (canister.Instance < 0) { done.Add(e); continue; }
                    em.SetComponentData(e, canister);
                }
                var instance = instances[canister.Instance];
                if (instance.Block == 0 || instance.CellsAlive == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0) { done.Add(e); continue; }
                // A canister is one block: its grid's centre of mass is the block's centre.
                float3 centre = em.GetComponentData<LocalTransform>(e).TransformPoint(em.GetComponentData<PhysicsMass>(e).CenterOfMass);
                Entity best = Entity.Null; float bestD = float.MaxValue;
                foreach (var s in shipEntities)
                {
                    var g = em.GetComponentData<VoxelGrid>(s);
                    if (g.Kind != GridKind.Ship) continue;
                    float radius = em.HasComponent<ShipStats>(s) ? em.GetComponentData<ShipStats>(s).BoundingRadius
                        : math.length((float3)(g.CellMax - g.CellMin + 1)) * g.CellSize * .5f;
                    var sp = em.GetComponentData<LocalTransform>(s);
                    float d = math.distance(sp.TransformPoint(em.GetComponentData<PhysicsMass>(s).CenterOfMass).xz, centre.xz) - radius;
                    if (d > canister.Reach) continue;
                    if (d < bestD || d == bestD && s.Index < best.Index) { best = s; bestD = d; }
                }
                if (best == Entity.Null) continue;
                status.EnergyGrants.PostFromMainThread(new EnergyGrantRequest { Ship = best, Energy = canister.Energy, Producer = ProducerKind.Hazard, Sequence = (uint)c });
                done.Add(e);
                if (em.HasComponent<GridDespawnRequest>(e)) Game.Voxel.Grid.GridSpawnApi.RequestDespawn(em, e);
            }
            foreach (var e in done) em.RemoveComponent<Canister>(e);
            done.Dispose();
        }
    }
}
