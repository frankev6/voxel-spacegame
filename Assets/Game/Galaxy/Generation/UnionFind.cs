using Unity.Collections;

namespace Game.Galaxy.Generation
{
    /// <summary>SPEC-30 §5.3 spanning forest with path halving and stable root selection.</summary>
    internal struct UnionFind : System.IDisposable
    {
        NativeArray<int> parents;
        internal UnionFind(int count, Allocator allocator)
        {
            parents = new NativeArray<int>(count, allocator);
            for (int i = 0; i < count; i++) parents[i] = i;
        }
        internal int Find(int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }
            return index;
        }
        internal bool Join(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return false;
            if (a < b) parents[b] = a;
            else parents[a] = b;
            return true;
        }
        public void Dispose() { if (parents.IsCreated) parents.Dispose(); }
    }
}
