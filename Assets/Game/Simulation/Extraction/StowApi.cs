using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 decision 4: stow the towed load into the hold (it leaves the world and rides as cargo) and put it back out.
    /// Authority, main thread.</summary>
    public static class StowApi
    {
        /// <summary>Tonnes the ship's towed load weighs and its hold's free tonnes.</summary>
        public static bool CanJump(EntityManager em, Entity ship, out float loadT, out float freeT)
        {
            freeT = em.HasComponent<CargoHold>(ship) ? em.GetComponentData<CargoHold>(ship).FreeT : 0;
            loadT = 0;
            var load = TowApi.Load(em, ship);
            if (load != Entity.Null && em.Exists(load) && em.HasComponent<TowLoad>(load)) loadT = em.GetComponentData<TowLoad>(load).MassT;
            return CargoRule.Allows(loadT, freeT);
        }

        public static bool Stow(EntityManager em, Entity ship, out string reason)
        {
            var load = TowApi.Load(em, ship);
            if (load == Entity.Null) { reason = "nothing on the cable"; return false; }
            float massT = em.GetComponentData<TowLoad>(load).MassT;
            var hold = em.HasComponent<CargoHold>(ship) ? em.GetComponentData<CargoHold>(ship) : default;
            if (!CargoRule.Allows(massT, hold.FreeT)) { reason = $"the hold takes {hold.FreeT:0} t, the load is {massT:0} t"; return false; }
            TowApi.Release(em, ship);
            hold.StowedT += massT;
            if (em.HasComponent<CargoHold>(ship)) em.SetComponentData(ship, hold); else em.AddComponentData(ship, hold);
            em.AddComponentData(load, new Stowed { Ship = ship, MassT = massT });
            if (em.HasComponent<PhysicsVelocity>(load)) em.SetComponentData(load, new PhysicsVelocity());
            em.AddComponent<Disabled>(load);
            reason = null; return true;
        }

        /// <summary>Puts a stowed load back out `behind` metres behind the ship, at rest relative to it.</summary>
        public static bool Unstow(EntityManager em, Entity ship, Entity load, float behind = 30)
        {
            if (!em.Exists(load) || !em.HasComponent<Stowed>(load) || em.GetComponentData<Stowed>(load).Ship != ship) return false;
            var stowed = em.GetComponentData<Stowed>(load);
            em.RemoveComponent<Disabled>(load);
            em.RemoveComponent<Stowed>(load);
            var pose = em.GetComponentData<LocalTransform>(ship);
            var back = math.mul(pose.Rotation, new float3(0, 0, -1));
            var lp = em.GetComponentData<LocalTransform>(load); lp.Position = pose.Position + back * behind; em.SetComponentData(load, lp);
            if (em.HasComponent<PhysicsVelocity>(ship) && em.HasComponent<PhysicsVelocity>(load))
                em.SetComponentData(load, new PhysicsVelocity { Linear = em.GetComponentData<PhysicsVelocity>(ship).Linear });
            if (em.HasComponent<CargoHold>(ship)) { var h = em.GetComponentData<CargoHold>(ship); h.StowedT = math.max(0, h.StowedT - stowed.MassT); em.SetComponentData(ship, h); }
            return true;
        }
    }
}
