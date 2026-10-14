using System.Collections.Generic;
using Unity.Entities;
namespace Game.Galaxy.Salvage
{
    internal sealed class SalvageLedger
    {
        internal sealed class Entry
        {
            internal Entity Root;
            internal readonly List<SalvageYield> Yields = new();
        }
        internal readonly Dictionary<ulong, Entry> Entries = new();
        readonly List<ulong> stale = new();
        internal void Prune(EntityManager manager)
        {
            stale.Clear();
            foreach (var pair in Entries) if (!manager.Exists(pair.Value.Root)) stale.Add(pair.Key);
            foreach (ulong key in stale) Entries.Remove(key);
        }
        internal void Clear() => Entries.Clear();
    }
}
