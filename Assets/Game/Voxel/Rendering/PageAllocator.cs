using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Voxel.Rendering
{
    /// <summary>ADR-0032: native best-fit ranges; pages govern capacity, never minimum allocation size.</summary>
    public struct PageAllocator : IDisposable
    {
        NativeList<PoolRange> free;
        public int Capacity { get; private set; }
        public int Used { get; private set; }
        public int PageSize { get; private set; }
        public long NativeBytes => free.IsCreated ? (long)free.Capacity * UnsafeUtility.SizeOf<PoolRange>() + UnsafeUtility.SizeOf<UnsafeList<PoolRange>>() : 0;
        public PageAllocator(int pageSize, int capacity)
        {
            if (pageSize <= 0 || capacity < 0) throw new ArgumentOutOfRangeException();
            PageSize = pageSize; Capacity = 0; Used = 0; free = new NativeList<PoolRange>(16, Allocator.Persistent);
            GrowTo(capacity);
        }
        public int RoundedCapacity(int count) => checked((int)(((long)count + PageSize - 1) / PageSize * PageSize));
        public void GrowTo(int count)
        {
            int next = RoundedCapacity(count);
            if (next <= Capacity) return;
            var tail = new PoolRange { Start = Capacity, Count = next - Capacity }; Capacity = next;
            InsertFree(tail);
        }
        public bool TryAllocate(int count, out PoolRange range)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            range = default; if (count == 0) return true;
            int best = -1;
            for (int i = 0; i < free.Length; i++)
                if (free[i].Count >= count && (best < 0 || free[i].Count < free[best].Count)) best = i;
            if (best < 0) return false;
            var old = free[best]; range = new PoolRange { Start = old.Start, Count = count };
            if (old.Count == count) free.RemoveAt(best);
            else free[best] = new PoolRange { Start = old.Start + count, Count = old.Count - count };
            Used += count; return true;
        }
        public void Free(PoolRange range)
        {
            if (range.Count == 0) return;
            if (range.Start < 0 || range.Count < 0 || (long)range.Start + range.Count > Capacity || range.Count > Used) throw new ArgumentOutOfRangeException(nameof(range));
            InsertFree(range); Used -= range.Count;
        }
        void InsertFree(PoolRange range)
        {
            int index = 0;
            while (index < free.Length && free[index].Start < range.Start) index++;
            if ((index > 0 && free[index - 1].End > range.Start) || (index < free.Length && range.End > free[index].Start))
                throw new InvalidOperationException("Overlapping pool free.");
            free.Add(default);
            for (int i = free.Length - 1; i > index; i--) free[i] = free[i - 1];
            free[index] = range;
            if (index > 0 && free[index - 1].End == range.Start)
            {
                var previous = free[index - 1]; previous.Count += range.Count; free[index - 1] = previous;
                free.RemoveAt(index); index--;
            }
            if (index + 1 < free.Length && free[index].End == free[index + 1].Start)
            {
                var current = free[index]; current.Count += free[index + 1].Count; free[index] = current; free.RemoveAt(index + 1);
            }
        }
        public void Dispose() { if (free.IsCreated) free.Dispose(); this = default; }
    }
}
