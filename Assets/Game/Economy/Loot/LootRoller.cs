using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;

namespace Game.Economy
{
    /// <summary>SPEC-50 §5.3: purpose-separated seeded draws, independent chances or weighted picks, merged in appearance order.</summary>
    public static class LootRoller
    {
        public static void Roll(LootTableDefinition table, int picks, ulong seed, List<(ItemDefinition item, int count)> into)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            var entries = table.Entries;
            if (entries == null) throw new ArgumentException("Loot entries must be present.", nameof(table));
            double total = 0;
            foreach (var entry in entries)
            {
                if (entry.item == null || entry.min < 1 || entry.max < entry.min || entry.max > 10000 ||
                    float.IsNaN(entry.chancePercent) || float.IsInfinity(entry.chancePercent) || entry.chancePercent < 0 || entry.chancePercent > 100)
                    throw new ArgumentException("Loot entry is invalid.", nameof(table));
                total += entry.chancePercent;
            }
            if (entries.Count == 0 || total == 0)
            {
                GameLog.WarnOnce("Loot.Empty:" + table.IdString, LogCategories.Economy, $"Loot table '{table.IdString}' has no rollable entries.");
                return;
            }
            int count = picks > 0 ? picks : table.DefaultPicks;
            if (count < 0) throw new ArgumentException("Default loot picks cannot be negative.", nameof(table));
            var rng = RngStreams.For(seed, RngPurpose.Loot, 0);
            if (count == 0)
            {
                foreach (var entry in entries)
                {
                    float chance = rng.NextFloat(); int amount = rng.NextInt(entry.min, entry.max + 1);
                    if (chance * 100 < entry.chancePercent) Merge(into, entry.item, amount);
                }
            }
            else for (int pick = 0; pick < count; pick++)
            {
                double target = rng.NextFloat() * total, cumulative = 0;
                var chosen = entries[entries.Count - 1];
                foreach (var entry in entries)
                {
                    cumulative += entry.chancePercent;
                    if (cumulative > target) { chosen = entry; break; }
                }
                Merge(into, chosen.item, rng.NextInt(chosen.min, chosen.max + 1));
            }
        }
        static void Merge(List<(ItemDefinition item, int count)> into, ItemDefinition item, int count)
        {
            for (int i = 0; i < into.Count; i++) if (into[i].item == item)
            { into[i] = (item, checked(into[i].count + count)); return; }
            into.Add((item, count));
        }
    }
}
