using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Spatial
{
    /// <summary>SPEC-23 / ADR-0064: read-only query data for jobs, without main-thread handle bookkeeping.</summary>
    public struct SpatialHashQueryData
    {
        [ReadOnly] public NativeList<SpatialProxy> Proxies;
        [ReadOnly] public NativeList<int> Large;
        [ReadOnly] public NativeHashMap<long, int2> Cells;
        [ReadOnly] public NativeList<int> EntryIndices;
        [ReadOnly] public NativeReference<SpatialHashState> State;
        public SpatialProxy Get(int index) => Proxies[index];
        public int QuerySegment(float3 p0, float3 p1, float inflate, ref FixedList512Bytes<int> output)
            => Query(p0, p1, inflate, true, ref output);
        public int QuerySphere(float3 center, float radius, ref FixedList512Bytes<int> output)
            => Query(center, center, radius, false, ref output);
        public int FindIndex(Entity e)
        {
            int lo = 0, hi = Proxies.Length - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                Entity candidate = Proxies[mid].Entity;
                int order = candidate.Index.CompareTo(e.Index);
                if (order == 0) order = candidate.Version.CompareTo(e.Version);
                if (order == 0) return mid;
                if (order < 0) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }

        // SPEC-23 Q7: queries remain read-only, including overflow; no shared counter writes.
        int Query(float3 start, float3 end, float radius, bool segment, ref FixedList512Bytes<int> output)
        {
            output.Clear();
            var min = math.floor((math.min(start.xz, end.xz) - radius) / State.Value.CellSize);
            var max = math.floor((math.max(start.xz, end.xz) + radius) / State.Value.CellSize);
            // Bound enumeration and avoid integer wrap for very long beams/large sensor ranges.
            double width = (double)max.x - min.x + 1, height = (double)max.y - min.y + 1;
            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max))
                || min.x <= int.MinValue || min.y <= int.MinValue || max.x >= int.MaxValue || max.y >= int.MaxValue
                || width * height > 4096)
                return Scan(start, end, radius, segment, ref output);
            var scratch = new FixedList4096Bytes<int>();
            int2 first = (int2)min, last = (int2)max;
            for (int x = first.x; x <= last.x; x++)
                for (int z = first.y; z <= last.y; z++)
                    if (Cells.TryGetValue(SpatialHash.CellKey(new int2(x, z)), out int2 range))
                        for (int j = 0; j < range.y; j++)
                        {
                            if (scratch.Length == scratch.Capacity) return Scan(start, end, radius, segment, ref output);
                            scratch.Add(EntryIndices[range.x + j]);
                        }
            for (int j = 0; j < Large.Length; j++)
            {
                if (scratch.Length == scratch.Capacity) return Scan(start, end, radius, segment, ref output);
                scratch.Add(Large[j]);
            }
            scratch.Sort();
            int count = 0, previous = -1;
            for (int j = 0; j < scratch.Length; j++)
            {
                int index = scratch[j];
                if (index == previous) continue;
                previous = index;
                if (!Intersects(Proxies[index], start, end, radius, segment)) continue;
                count++;
                if (output.Length < output.Capacity) output.Add(index);
            }
            return count;
        }
        int Scan(float3 start, float3 end, float radius, bool segment, ref FixedList512Bytes<int> output)
        {
            output.Clear(); int count = 0;
            for (int i = 0; i < Proxies.Length; i++)
            {
                if (!Intersects(Proxies[i], start, end, radius, segment)) continue;
                count++;
                if (output.Length < output.Capacity) output.Add(i);
            }
            return count;
        }
        static bool Intersects(in SpatialProxy proxy, float3 start, float3 end, float radius, bool segment)
        {
            float3 closest = start;
            if (segment)
            {
                float3 direction = end - start;
                float lengthSq = math.lengthsq(direction);
                float t = lengthSq > 0 ? math.saturate(math.dot(proxy.Center - start, direction) / lengthSq) : 0;
                closest += t * direction;
            }
            float reach = proxy.Radius + radius;
            return math.distancesq(proxy.Center, closest) <= reach * reach;
        }
    }
}
