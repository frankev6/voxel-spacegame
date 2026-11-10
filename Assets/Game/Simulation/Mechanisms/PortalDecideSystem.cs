using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Hazards;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Mechanisms
{
    /// <summary>SPEC-91 HAZ-021 (ADR-0119 decision 3), on the authority. A ship that crosses an open portal's entry gate along its forward
    /// gets a PortalTransit to the paired exit, and the portal jams for its JamSeconds: a ship behind it within that time flies straight
    /// through. PortalJam mechanism actions close the portals of their group (Value seconds, or the portal's own JamSeconds when 0).
    /// Only live ship grids transit; debris, devices and hulks never do (the gate effect affects ships only, and hulks are checked),
    /// except a ship's towed load, which goes through with it when the hold can take it (SPEC-93 decision 4).</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ZoneSystem))]
    public partial struct PortalDecideSystem : ISystem
    {
        uint appliedActions;
        EntityQuery portals;

        public void OnCreate(ref SystemState state)
        {
            appliedActions = uint.MaxValue;
            portals = state.GetEntityQuery(ComponentType.ReadWrite<PortalLink>(), ComponentType.ReadOnly<WorldZone>());
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate<PortalTransits>(); state.RequireForUpdate(portals);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            // read-write, so CompleteDependency waits for ZoneSystem's job that fills the list
            bool hasContacts = SystemAPI.TryGetSingletonRW<ZoneContacts>(out var contactsRW);
            state.CompleteDependency();
            using var entities = portals.ToEntityArray(Allocator.Temp);

            if (SystemAPI.TryGetSingleton<MechanismActions>(out var actions) && actions.Current.IsCreated && actions.Current.Length > 0 && actions.Tick != appliedActions)
            {
                appliedActions = actions.Tick;
                foreach (var a in actions.Current)
                {
                    if (a.Kind != MechanismActionKind.PortalJam) continue;
                    foreach (var e in entities)
                    {
                        if (em.GetComponentData<WorldZone>(e).Group != a.Group) continue;
                        var link = em.GetComponentData<PortalLink>(e);
                        link.JammedUntilTick = math.max(link.JammedUntilTick, tick + JamTicks(a.Value > 0 ? a.Value : link.JamSeconds));
                        em.SetComponentData(e, link);
                    }
                }
            }

            if (!hasContacts) return;
            var contacts = contactsRW.ValueRO;
            if (!contacts.Current.IsCreated || contacts.Current.Length == 0) return;
            var transits = SystemAPI.GetSingleton<PortalTransits>();
            // contacts come in zone (PersistentId) then body order from ZoneSystem, so the first ship through an open portal is the same everywhere
            foreach (var c in contacts.Current)
            {
                if (c.Kind != Game.Data.Hazards.ZoneEffectKind.Gate || c.Crossed <= 0) continue;
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    var zone = em.GetComponentData<WorldZone>(e);
                    if (zone.PersistentId != c.Zone) continue;
                    var link = em.GetComponentData<PortalLink>(e);
                    if (link.JammedUntilTick > tick || !IsShip(em, c.Body)) break;
                    // SPEC-93 decision 4: the cargo rule. A ship whose towed load outweighs its hold's free tonnes is refused (no jam);
                    // otherwise the load goes through with it, the same offset from the gate.
                    if (!Extraction.StowApi.CanJump(em, c.Body, out float loadT, out float freeT))
                    {
                        var refused = new Extraction.CargoRefused { Tick = tick, LoadT = loadT, FreeT = freeT };
                        if (em.HasComponent<Extraction.CargoRefused>(c.Body)) em.SetComponentData(c.Body, refused); else em.AddComponentData(c.Body, refused);
                        break;
                    }
                    transits.Pending.Add(new PortalTransit { Body = c.Body, From = zone.PersistentId, To = link.Other, Tick = tick });
                    var load = Extraction.TowApi.Load(em, c.Body);
                    if (load != Entity.Null && em.Exists(load)) transits.Pending.Add(new PortalTransit { Body = load, From = zone.PersistentId, To = link.Other, Tick = tick });
                    link.JammedUntilTick = tick + JamTicks(link.JamSeconds);
                    em.SetComponentData(e, link);
                    break;
                }
            }
        }

        public static uint JamTicks(float seconds) => (uint)math.max(0, math.round(seconds * 60));

        static bool IsShip(EntityManager em, Entity body)
            => em.Exists(body) && em.HasComponent<VoxelGrid>(body) && em.GetComponentData<VoxelGrid>(body).Kind == GridKind.Ship
               && !em.HasComponent<Game.Voxel.Damage.Hulk>(body);
    }
}
