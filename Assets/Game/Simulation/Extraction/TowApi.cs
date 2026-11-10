using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 §6: the magnet tow's one writer of attach and release (main thread: dev console, the ship's magnet, scenarios).</summary>
    public static class TowApi
    {
        public const float DefaultBreakForce = 6.0e6f;   // N: a frigate can pull a station core, not tear a station off its anchor

        public static bool Attach(EntityManager em, Entity ship, Entity load, float length, uint tick,
            float3 shipAnchor = default, float3 loadAnchor = default, float breakForce = DefaultBreakForce)
        {
            if (ship == load || !em.Exists(ship) || !em.Exists(load)) return false;
            if (!em.HasComponent<PhysicsMass>(load) || !em.HasComponent<PhysicsVelocity>(load) || !em.HasComponent<PhysicsMass>(ship)) return false;
            if (em.HasComponent<TowLoad>(load)) return false;           // one cable per load
            Release(em, ship);
            var m = em.GetComponentData<PhysicsMass>(load);
            em.AddComponentData(ship, new TowLink { Load = load, ShipAnchor = shipAnchor, LoadAnchor = loadAnchor, Length = math.max(1, length),
                BreakForce = breakForce, SinceTick = tick });
            em.AddComponentData(load, new TowLoad { Tower = ship, MassT = m.InverseMass > 0 ? 1e-3f / m.InverseMass : 0 });
            return true;
        }

        public static void Release(EntityManager em, Entity ship)
        {
            if (!em.Exists(ship) || !em.HasComponent<TowLink>(ship)) return;
            var load = em.GetComponentData<TowLink>(ship).Load;
            if (load != Entity.Null && em.Exists(load) && em.HasComponent<TowLoad>(load) && em.GetComponentData<TowLoad>(load).Tower == ship)
                em.RemoveComponent<TowLoad>(load);
            em.RemoveComponent<TowLink>(ship);
        }

        public static bool IsTowing(EntityManager em, Entity ship) => em.Exists(ship) && em.HasComponent<TowLink>(ship);

        public static Entity Load(EntityManager em, Entity ship) => IsTowing(em, ship) ? em.GetComponentData<TowLink>(ship).Load : Entity.Null;
    }
}
