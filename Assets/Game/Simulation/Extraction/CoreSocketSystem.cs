using Game.Core.Components;
using Game.Core.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 EXT-008, authority (ADR-0117 decider): a free station core (not stowed, not installed) within a free socket's
    /// radius installs there: the cable lets go, CoreHoldSystem holds it at the socket, and the den's modules power up.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(CoreTetherSystem)), UpdateBefore(typeof(HeistDirector))]
    public partial struct CoreSocketSystem : ISystem
    {
        EntityQuery sockets, cores, modules;

        public void OnCreate(ref SystemState state)
        {
            sockets = SystemAPI.QueryBuilder().WithAllRW<CoreSocket>().WithAll<LocalTransform>().Build();
            cores = SystemAPI.QueryBuilder().WithAllRW<StationCore>().WithAll<LocalTransform>().Build();
            modules = SystemAPI.QueryBuilder().WithAllRW<DenPowered>().Build();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate(sockets);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            using var ss = sockets.ToEntityArray(Allocator.Temp);
            using var cs = cores.ToEntityArray(Allocator.Temp);
            foreach (var s in ss)
            {
                var socket = em.GetComponentData<CoreSocket>(s);
                if (socket.Core != Entity.Null) continue;
                float3 at = em.GetComponentData<LocalTransform>(s).Position;
                foreach (var c in cs)
                {
                    var core = em.GetComponentData<StationCore>(c);
                    if (core.Freed == 0 || core.Socket != Entity.Null || em.HasComponent<Stowed>(c)) continue;
                    if (math.distance(em.GetComponentData<LocalTransform>(c).Position, at) > socket.Radius) continue;
                    if (em.HasComponent<TowLoad>(c)) TowApi.Release(em, em.GetComponentData<TowLoad>(c).Tower);
                    core.Socket = s; em.SetComponentData(c, core);
                    socket.Core = c; socket.Powered = 1; socket.InstalledTick = tick; em.SetComponentData(s, socket);
                    Power(em, socket.Den, true);
                    break;
                }
            }
        }

        void Power(EntityManager em, ushort den, bool on)
        {
            using var ms = modules.ToEntityArray(Allocator.Temp);
            foreach (var m in ms)
            {
                var p = em.GetComponentData<DenPowered>(m);
                if (p.Den != den) continue;
                p.On = (byte)(on ? 1 : 0); em.SetComponentData(m, p);
            }
        }

        /// <summary>Whether the den has a powered socket (for new modules' starting state).</summary>
        public static bool DenHasPower(EntityManager em, ushort den)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CoreSocket>());
            using var all = q.ToComponentDataArray<CoreSocket>(Allocator.Temp);
            foreach (var s in all) if (s.Den == den && s.Powered != 0) return true;
            return false;
        }
    }
}
