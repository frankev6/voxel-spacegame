#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using Game.Core.Components;
using Game.Galaxy.Interaction;
using Game.Galaxy.Salvage;
using Game.Simulation;
using Game.Simulation.Abilities;
using Game.Simulation.Ai;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;

namespace Game.App.DevRuntime
{
    /// <summary>ADR-0117 §7, NET-018: one line of what a machine derives about one pilot's ship and the world around it, without poses,
    /// timers or distances (those lag by design). tools/playtest/coop-parity.sh prints it on the host and a guest and diffs it.</summary>
    internal static class DevParity
    {
        internal static string Describe(DevContext ctx, Entity ship)
        {
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            var text = new StringBuilder("parity");
            void Add(string key, object value) => text.Append(' ').Append(key).Append('=').Append(value);
            if (ship == Entity.Null || !em.Exists(ship)) { Add("ship", "missing"); return text.ToString(); }
            Add("ship", em.HasComponent<PersistentId>(ship) ? em.GetComponentData<PersistentId>(ship).Value : 0);
            if (em.HasComponent<VoxelGrid>(ship)) Add("kind", em.GetComponentData<VoxelGrid>(ship).Kind);
            Add("hulk", em.HasComponent<Hulk>(ship) ? 1 : 0);
            Add("cells", Game.Voxel.Replica.ReplicaVoxelApi.HashCells(em, ship).ToString("x16"));
            if (em.HasBuffer<BlockInstance>(ship))
            {
                int working = 0; foreach (var instance in em.GetBuffer<BlockInstance>(ship, true)) if ((instance.Flags & BlockInstanceFlags.Destroyed) == 0 && instance.Block != 0) working++;
                Add("blocks", working);
            }
            Add("collider", DevEcs.Collider(em, ship).Replace(' ', '_'));
            if (em.HasBuffer<AbilitySlot>(ship))
            {
                var slots = new StringBuilder();
                foreach (var slot in em.GetBuffer<AbilitySlot>(ship, true))
                    slots.Append(slots.Length == 0 ? "" : ",").Append(slot.AbilityIndex).Append(':').Append(slot.State == AbilitySlotState.PendingPower ? AbilitySlotState.Ready : slot.State)
                        .Append(':').Append(slot.Charges).Append(':').Append(slot.Active);
                Add("slots", slots.Length == 0 ? "-" : slots.ToString());
            }
            if (em.HasComponent<ShieldState>(ship)) { var shield = em.GetComponentData<ShieldState>(ship); Add("shield", shield.Active + "/" + shield.MaxHp); }
            if (em.HasComponent<ShieldControl>(ship)) Add("shieldRequest", em.GetComponentData<ShieldControl>(ship).RequestedOn);
            if (em.HasComponent<PilotCombatStatus>(ship)) Add("hostiles", em.GetComponentData<PilotCombatStatus>(ship).HostilesEngaging);
            if (em.HasComponent<PilotInteraction>(ship))
            {
                var target = em.GetComponentData<PilotInteraction>(ship).Value;
                Add("target", target.Target == Entity.Null ? "-" : target.GridPersistentId + ":" + target.Kind);
            }
            Add("grids", Count<VoxelGrid, PersistentId>(em)); Add("hulks", Count<Hulk, VoxelGrid>(em));
            Add("collected", Count<SalvageCollected, VoxelGrid>(em));
            int salvage = 0;
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<Interactable>()))
            using (var values = query.ToComponentDataArray<Interactable>(Allocator.Temp))
                foreach (var value in values) if (value.Kind == Game.Data.Definitions.InteractionKind.Salvage) salvage++;
            Add("salvageable", salvage);
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>()))
            {
                if (query.CalculateEntityCount() == 1)
                {
                    var relations = query.GetSingleton<FactionRelations>(); ulong h = 1469598103934665603UL;
                    for (int i = 0; relations.Stance.IsCreated && i < relations.Stance.Length; i++) h = (h ^ relations.Stance[i]) * 1099511628211UL;
                    Add("factions", h.ToString("x16"));
                }
            }
            return text.ToString();
        }

        static int Count<A, B>(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<A>(), ComponentType.ReadOnly<B>());
            return query.CalculateEntityCount();
        }
    }
}
#endif
