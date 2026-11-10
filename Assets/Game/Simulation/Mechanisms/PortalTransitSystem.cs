using Game.Core.Systems;
using Game.Simulation.Hazards;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Simulation.Mechanisms
{
    /// <summary>Singleton: portal transits waiting to be applied. Writer: PortalDecideSystem (authority); applier: PortalTransitSystem.</summary>
    public struct PortalTransits : IComponentData
    {
        public NativeList<PortalTransit> Pending;
    }

    /// <summary>SPEC-91 HAZ-021 (ADR-0119 decision 3), on every machine, before propulsion: applies each pending transit. The root's
    /// pose goes from the entry gate's frame to the exit gate's (same offset, turned by the difference of their yaws), its linear
    /// velocity turns with it (angular velocity is body-local and stays) and graphical smoothing restarts at the new pose. The second
    /// writer of a root's LocalTransform (architecture §8), for transits only; ZoneSystem skips the jump's gate crossings (JumpMetres).</summary>
    // after the ship commands (SPEC-33 §5 decision 3: only FactionStandingSystem precedes ShipCommandGroup), still before propulsion
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup), OrderFirst = true), UpdateAfter(typeof(ShipCommandGroup))]
    public partial struct PortalTransitSystem : ISystem
    {
        Entity singleton;
        EntityQuery zones;

        public void OnCreate(ref SystemState state)
        {
            singleton = state.EntityManager.CreateEntity(typeof(PortalTransits));
            state.EntityManager.SetName(singleton, "PortalTransits");
            state.EntityManager.SetComponentData(singleton, new PortalTransits { Pending = new NativeList<PortalTransit>(4, Allocator.Persistent) });
            zones = state.GetEntityQuery(ComponentType.ReadOnly<WorldZone>(), ComponentType.ReadOnly<LocalTransform>());
        }

        public void OnDestroy(ref SystemState state)
        {
            if (state.EntityManager.Exists(singleton))
            {
                var t = state.EntityManager.GetComponentData<PortalTransits>(singleton);
                if (t.Pending.IsCreated) t.Pending.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var transits = state.EntityManager.GetComponentData<PortalTransits>(singleton);
            if (transits.Pending.Length == 0) return;
            var em = state.EntityManager;
            state.CompleteDependency();
            using var entities = zones.ToEntityArray(Allocator.Temp);
            foreach (var t in transits.Pending)
            {
                if (!em.Exists(t.Body) || !em.HasComponent<LocalTransform>(t.Body)) continue;
                if (!Find(em, entities, t.From, out var from) || !Find(em, entities, t.To, out var to)) continue;
                var pose = em.GetComponentData<LocalTransform>(t.Body);
                Map(from, to, ref pose, out quaternion turn);
                em.SetComponentData(t.Body, pose);
                if (em.HasComponent<PhysicsVelocity>(t.Body))
                {
                    var v = em.GetComponentData<PhysicsVelocity>(t.Body);
                    v.Linear = math.mul(turn, v.Linear);
                    em.SetComponentData(t.Body, v);
                    if (em.HasComponent<PhysicsGraphicalInterpolationBuffer>(t.Body))
                        em.SetComponentData(t.Body, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = new RigidTransform(pose.Rotation, pose.Position), PreviousVelocity = v });
                }
                if (em.HasComponent<Extraction.TowLoad>(t.Body))
                {   // the turn is the gate's, not a knock (SPEC-93 decision 3)
                    var towed = em.GetComponentData<Extraction.TowLoad>(t.Body); towed.ExpectedTick = 0; em.SetComponentData(t.Body, towed);
                }
                if (em.HasComponent<PhysicsGraphicalSmoothing>(t.Body))
                {
                    var s = em.GetComponentData<PhysicsGraphicalSmoothing>(t.Body); s.ApplySmoothing = 0; em.SetComponentData(t.Body, s);
                }
            }
            transits.Pending.Clear();
        }

        /// <summary>A gate's live frame: its LocalTransform position (rebased) and WorldZone yaw.</summary>
        public struct Frame { public float3 Position; public float Yaw; }

        static bool Find(EntityManager em, NativeArray<Entity> entities, ulong id, out Frame frame)
        {
            foreach (var e in entities)
            {
                var z = em.GetComponentData<WorldZone>(e);
                if (z.PersistentId != id) continue;
                frame = new Frame { Position = em.GetComponentData<LocalTransform>(e).Position, Yaw = z.Yaw };
                return true;
            }
            frame = default; return false;
        }

        /// <summary>The pose's offset from `from`, turned by the yaw difference and put at `to`; `turn` is that rotation.</summary>
        public static void Map(in Frame from, in Frame to, ref LocalTransform pose, out quaternion turn)
        {
            turn = quaternion.RotateY(to.Yaw - from.Yaw);
            float3 offset = pose.Position - from.Position;
            pose.Position = to.Position + math.mul(turn, new float3(offset.x, offset.y, offset.z));
            pose.Rotation = math.normalize(math.mul(turn, pose.Rotation));
        }
    }
}
