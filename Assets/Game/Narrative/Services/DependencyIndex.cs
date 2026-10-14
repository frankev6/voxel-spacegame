using System;
using System.Collections.Generic;
using Game.Data.Logic;

namespace Game.Narrative
{
    /// <summary>SPEC-40 §5.3: per-system CSR subscriptions from dependency keys to content-owner indices.</summary>
    internal sealed class DependencyIndex
    {
        const int KindCount = (int)LogicDependencyKind.Relation + 1;
        readonly int[][] starts = new int[KindCount][];
        readonly int[][] subscribers = new int[KindCount][];
        internal DependencyIndex(IReadOnlyList<IReadOnlyList<LogicDependency>> owners)
        {
            if (owners == null) throw new ArgumentNullException(nameof(owners));
            var unique = new HashSet<LogicDependency>[owners.Count];
            var maximum = new int[KindCount];
            for (int kind = 0; kind < KindCount; kind++) maximum[kind] = -1;
            for (int owner = 0; owner < owners.Count; owner++)
            {
                var dependencies = owners[owner] ?? throw new ArgumentException("Owner dependencies cannot be null.", nameof(owners));
                unique[owner] = new HashSet<LogicDependency>();
                for (int i = 0; i < dependencies.Count; i++)
                {
                    var dependency = dependencies[i]; int kind = (int)dependency.Kind;
                    if ((uint)kind >= KindCount || dependency.Key < 0 || dependency.Key > int.MaxValue - 2)
                        throw new ArgumentException("Dependency must have a supported kind and bound nonnegative key.", nameof(owners));
                    unique[owner].Add(dependency);
                    maximum[kind] = Math.Max(maximum[kind], dependency.Key);
                }
            }
            for (int kind = 0; kind < KindCount; kind++) starts[kind] = new int[maximum[kind] + 2];
            for (int owner = 0; owner < unique.Length; owner++)
                foreach (var dependency in unique[owner]) starts[(int)dependency.Kind][dependency.Key + 1]++;
            var next = new int[KindCount][];
            for (int kind = 0; kind < KindCount; kind++)
            {
                var start = starts[kind];
                for (int key = 1; key < start.Length; key++) start[key] = checked(start[key] + start[key - 1]);
                subscribers[kind] = new int[start[start.Length - 1]];
                next[kind] = (int[])start.Clone();
            }
            // Owner-order insertion makes each CSR range canonical, independent of hash-set traversal order.
            for (int owner = 0; owner < unique.Length; owner++)
                foreach (var dependency in unique[owner])
                {
                    int kind = (int)dependency.Kind;
                    subscribers[kind][next[kind][dependency.Key]++] = owner;
                }
        }
        internal void Collect(IReadOnlyList<LogicDependency> changed, HashSet<int> dirtyOwners)
        {
            if (changed == null) throw new ArgumentNullException(nameof(changed));
            if (dirtyOwners == null) throw new ArgumentNullException(nameof(dirtyOwners));
            for (int i = 0; i < changed.Count; i++)
            {
                var dependency = changed[i]; int kind = (int)dependency.Kind;
                if ((uint)kind >= KindCount) continue;
                var start = starts[kind];
                if ((uint)dependency.Key >= (uint)(start.Length - 1)) continue;
                for (int at = start[dependency.Key]; at < start[dependency.Key + 1]; at++) dirtyOwners.Add(subscribers[kind][at]);
            }
        }
    }
}
