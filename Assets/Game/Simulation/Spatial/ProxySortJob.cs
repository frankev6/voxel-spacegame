using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Simulation.Spatial
{
    /// <summary>SPEC-23 §5.2: gather streams become one canonical entity-ordered array.</summary>
    [BurstCompile]
    public struct ProxySortJob : IJob
    {
        [ReadOnly] public NativeStream Input;
        public NativeList<SpatialProxy> Proxies;
        public void Execute()
        {
            Proxies.Clear();
            var reader = Input.AsReader();
            for (int i = 0; i < reader.ForEachCount; i++)
            {
                int count = reader.BeginForEachIndex(i);
                for (int j = 0; j < count; j++) Proxies.Add(reader.Read<SpatialProxy>());
                reader.EndForEachIndex();
            }
            Proxies.AsArray().Sort(new EntityOrder());
        }
        struct EntityOrder : IComparer<SpatialProxy>
        {
            public int Compare(SpatialProxy a, SpatialProxy b)
            {
                int order = a.Entity.Index.CompareTo(b.Entity.Index);
                return order != 0 ? order : a.Entity.Version.CompareTo(b.Entity.Version);
            }
        }
    }
}
