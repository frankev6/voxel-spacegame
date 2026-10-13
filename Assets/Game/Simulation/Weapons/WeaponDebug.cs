#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 WPN-015: read-only hooks, called on the main thread between world updates.</summary>
    public static class WeaponDebug
    {
        /// <summary>Clears and fills into in instance-buffer order. Invalid roots and stale/destroyed/reparented links yield no rows.
        /// Includes inactive and uninitialized mounted weapons; never call from a fixed-step system.</summary>
        public static void Snapshot(EntityManager em, Entity root, List<WeaponDebugRow> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (root == Entity.Null || !em.Exists(root) || !em.HasBuffer<BlockInstance>(root)) return;
            em.CompleteDependencyBeforeRO<BlockInstance>();
            em.CompleteDependencyBeforeRO<FunctionalBlock>();
            em.CompleteDependencyBeforeRO<WeaponMount>();
            em.CompleteDependencyBeforeRO<WeaponState>();
            em.CompleteDependencyBeforeRO<WeaponAim>();
            em.CompleteDependencyBeforeRO<BeamState>();
            var instances = em.GetBuffer<BlockInstance>(root, true);
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i]; var weapon = instance.Functional;
                if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0 || weapon == Entity.Null || !em.Exists(weapon) ||
                    !em.HasComponent<FunctionalBlock>(weapon) || !em.HasComponent<WeaponMount>(weapon) ||
                    !em.HasComponent<WeaponState>(weapon) || !em.HasComponent<WeaponAim>(weapon) || !em.HasComponent<BeamState>(weapon)) continue;
                var link = em.GetComponentData<FunctionalBlock>(weapon);
                if (link.Grid != root || link.InstanceIndex != i) continue;
                into.Add(new WeaponDebugRow(weapon, em.GetComponentData<WeaponMount>(weapon), em.GetComponentData<WeaponState>(weapon),
                    em.GetComponentData<WeaponAim>(weapon), em.GetComponentData<BeamState>(weapon)));
            }
        }

        /// <summary>Copies the sole store's authoritative counters after completing its native-memory guard. False/default when unavailable
        /// or ambiguous; never repairs, clears or recomputes the store. Main thread outside the fixed step only.</summary>
        public static bool TryGetProjectileStats(EntityManager em, out ProjectileStoreStats stats)
        {
            stats = default;
            em.CompleteDependencyBeforeRO<ProjectileStore>();
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileStore>());
            if (query.CalculateEntityCount() != 1) return false;
            var store = query.GetSingleton<ProjectileStore>();
            if (!store.Access.IsCreated || !store.Stats.IsCreated) return false;
            store.CompleteAccess();
            stats = store.Stats.Value;
            return true;
        }
    }
}
#endif
