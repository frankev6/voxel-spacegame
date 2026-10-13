using System.Collections.Generic;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Voxel.Damage;
using Unity.Collections;
using Unity.Entities;

namespace Game.Galaxy.Streaming
{
    /// <summary>Assigns detached roots and their children before streaming/unload (SPEC-31 §5.12).</summary>
    internal static class SplitAdoption
    {
        internal static void Apply(EntityManager em, EntityQuery query, StarSystemId loaded)
        {
            if (!loaded.IsValid || query.IsEmptyIgnoreFilter) return;
            using var roots = query.ToEntityArray(Allocator.Temp);
            roots.Sort(new RootOrder());
            var groups = new SortedDictionary<uint, List<Entity>>();
            var assigned = new HashSet<Entity>();
            foreach (var root in roots)
            {
                var parent = em.GetComponentData<SplitOrigin>(root).Parent;
                var system = em.Exists(parent) && em.HasComponent<StarSystemMember>(parent)
                    ? em.GetSharedComponent<StarSystemMember>(parent).Id : loaded;
                if (!groups.TryGetValue(system.Value, out var entities))
                { entities = new List<Entity>(); groups.Add(system.Value, entities); }
                if (assigned.Add(root)) entities.Add(root);
                if (!em.HasBuffer<LinkedEntityGroup>(root)) continue;
                var linked = em.GetBuffer<LinkedEntityGroup>(root, true);
                foreach (var item in linked)
                    if (em.Exists(item.Value) && assigned.Add(item.Value)) entities.Add(item.Value);
            }
            foreach (var group in groups)
            {
                using var entities = new NativeArray<Entity>(group.Value.ToArray(), Allocator.Temp);
                em.AddSharedComponent(entities, new StarSystemMember { Id = new StarSystemId(group.Key) });
            }
            em.RemoveComponent<SplitOrigin>(query);
        }
        readonly struct RootOrder : IComparer<Entity>
        {
            public int Compare(Entity a, Entity b)
            { int index = a.Index.CompareTo(b.Index); return index != 0 ? index : a.Version.CompareTo(b.Version); }
        }
    }
}
