using Game.Core;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 EXT-004, every machine (applier): a core that is not free is held at its pose on the holder. After the tick's
    /// impulses its velocity is set to close the gap over HoldTicks and its spin is stopped, so a cable on a tethered core strains but
    /// does not move it, and a ship that bumps it sees it settle back. The second writer of a held core's PhysicsVelocity (after
    /// ImpulseApplySystem); a free core is never touched. An installed core is held the same way at its socket (EXT-008).</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ImpulseApplySystem))]
    public partial struct CoreHoldSystem : ISystem
    {
        const float HoldTicks = 6;
        EntityQuery cores;

        public void OnCreate(ref SystemState state)
        {
            cores = SystemAPI.QueryBuilder().WithAll<StationCore, LocalTransform, PhysicsVelocity>().Build();
            state.RequireForUpdate(cores);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            using var all = cores.ToEntityArray(Allocator.Temp);
            foreach (var c in all)
            {
                var core = em.GetComponentData<StationCore>(c);
                bool socketed = core.Socket != Entity.Null && em.Exists(core.Socket) && em.HasComponent<LocalTransform>(core.Socket);
                if (core.Freed != 0 && !socketed) continue;
                if (em.HasComponent<Stowed>(c)) continue;
                var pose = em.GetComponentData<LocalTransform>(c);
                float3 target = pose.Position;
                if (socketed) target = em.GetComponentData<LocalTransform>(core.Socket).Position;
                else if (core.Holder != Entity.Null && em.Exists(core.Holder) && em.HasComponent<LocalTransform>(core.Holder))
                    target = em.GetComponentData<LocalTransform>(core.Holder).TransformPoint(core.HoldLocal);
                em.SetComponentData(c, new PhysicsVelocity { Linear = (target - pose.Position) / (HoldTicks * SimTime.FixedTimestep), Angular = float3.zero });
            }
        }
    }
}
