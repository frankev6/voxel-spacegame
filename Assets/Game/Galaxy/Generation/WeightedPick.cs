using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>SPEC-30 §5.5.6: stable list order, strict cumulative boundary, optional per-entry limits.</summary>
    internal static class WeightedPick
    {
        internal static ushort Pick(NativeArray<WeightedIndex> entries, int2 range, float roll,
            NativeArray<int> used = default)
        {
            float total = 0;
            for (int i = range.x; i < range.x + range.y; i++)
                if (Eligible(entries[i], used, i)) total += entries[i].Weight;
            if (total <= 0) return GalaxyConstants.NoIndex;
            float target = roll * total, cumulative = 0;
            for (int i = range.x; i < range.x + range.y; i++)
            {
                var entry = entries[i];
                if (!Eligible(entry, used, i)) continue;
                cumulative += entry.Weight;
                if (cumulative > target) return entry.Index;
            }
            return GalaxyConstants.NoIndex;
        }

        static bool Eligible(WeightedIndex entry, NativeArray<int> used, int index)
            => entry.Weight > 0 && (entry.MaxCount == 0 || !used.IsCreated || used[index] < entry.MaxCount);
    }
}
