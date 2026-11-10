using Unity.Entities;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 decision 6: the crew's transponder. Authority, main thread (the toggle key, the console, place setup).</summary>
    public static class TransponderApi
    {
        public static void Set(EntityManager em, Entity ship, bool on, AccessCode code)
        {
            var t = new Transponder { On = (byte)(on ? 1 : 0), Code = code };
            if (em.HasComponent<Transponder>(ship)) em.SetComponentData(ship, t); else em.AddComponentData(ship, t);
            if (!em.HasComponent<Suspicion>(ship)) em.AddComponentData(ship, new Suspicion());
        }

        public static void ClearSuspicion(EntityManager em, Entity ship)
        {
            if (em.HasComponent<Suspicion>(ship)) em.SetComponentData(ship, new Suspicion());
            if (em.HasComponent<Wanted>(ship)) em.RemoveComponent<Wanted>(ship);
        }

        public static bool IsWantedBy(EntityManager em, Entity ship, byte faction)
            => faction < 64 && em.HasComponent<Wanted>(ship) && (em.GetComponentData<Wanted>(ship).FactionMask & (1ul << faction)) != 0;
    }
}
