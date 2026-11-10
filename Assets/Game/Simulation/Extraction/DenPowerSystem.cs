using Game.Core.Systems;
using Game.Simulation.Ai;
using Game.Simulation.Weapons;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 decision 10, authority: an unpowered den gun holds fire (its AI may track, it does not shoot). After the AI's
    /// decisions, before weapons read WeaponControl.</summary>
    [UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(AiDecisionSystem))]
    public partial struct DenPowerSystem : ISystem
    {
        EntityQuery guns;
        public void OnCreate(ref SystemState state)
        {
            guns = SystemAPI.QueryBuilder().WithAll<DenPowered>().WithAllRW<WeaponControl>().Build();
            state.RequireForUpdate(guns);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            using var all = guns.ToEntityArray(Allocator.Temp);
            foreach (var g in all)
            {
                if (em.GetComponentData<DenPowered>(g).On != 0) continue;
                var c = em.GetComponentData<WeaponControl>(g);
                if (c.Autofire == 0) continue;
                c.Autofire = 0; em.SetComponentData(g, c);
            }
        }
    }
}
