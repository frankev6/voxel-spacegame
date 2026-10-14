using System;
using System.Collections.Generic;

namespace Game.ShipEditor.Validation
{
    /// <summary>SPEC-51 §4.3: exact occupied bounds, including removal of an extreme cell.</summary>
    internal sealed class AxisHistogram
    {
        const int Offset = 512;
        readonly int[] counts = new int[1024];
        // Imported drafts may already exceed editor limits. They must remain removable.
        readonly SortedDictionary<int, int> outside = new();
        int min = 1024, max = -1;
        internal int Total { get; private set; }
        internal void Clear() { Array.Clear(counts, 0, counts.Length); outside.Clear(); min = 1024; max = -1; Total = 0; }
        internal void Add(int coordinate, int delta)
        {
            long index = (long)coordinate + Offset;
            if (index < 0 || index >= counts.Length)
            {
                outside.TryGetValue(coordinate, out int count); count += delta;
                if (count < 0) throw new InvalidOperationException("EDT: negative axis occupancy.");
                if (count == 0) outside.Remove(coordinate); else outside[coordinate] = count;
            }
            else
            {
                int i = (int)index, count = counts[i] + delta;
                if (count < 0) throw new InvalidOperationException("EDT: negative axis occupancy.");
                counts[i] = count;
                if (count > 0)
                { if (min > max) min = max = i; else { min = Math.Min(min, i); max = Math.Max(max, i); } }
                else
                {
                    while (min <= max && counts[min] == 0) min++;
                    while (max >= min && counts[max] == 0) max--;
                }
            }
            Total += delta;
        }
        internal bool TryGetBounds(out int first, out int last)
        {
            first = int.MaxValue; last = int.MinValue;
            if (Total == 0) return false;
            if (min <= max) { first = min - Offset; last = max - Offset; }
            foreach (var pair in outside) { first = Math.Min(first, pair.Key); last = Math.Max(last, pair.Key); }
            return true;
        }
    }
}
