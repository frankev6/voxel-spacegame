using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.11: deterministic serial reduction of parallel brick results.</summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    internal struct RecomputeSumJob : IJob
    {
        [ReadOnly] public NativeList<RecomputeRoot> Roots;
        [ReadOnly] public NativeList<StructureMassData> Results;
        public ComponentLookup<GridMass> Masses;
        public ComponentLookup<GridMassRecompute> Recompute;
        public NativeList<Entity> Changed;
        public void Execute()
        {
            for (int i = 0; i < Roots.Length; i++)
            {
                var root = Roots[i]; var sum = new StructureMassData();
                for (int b = root.Start; b < root.Start + root.Count; b++) MassAccumulator.Add(ref sum, Results[b]);
                var mass = Masses[root.Entity]; mass.Data = sum; mass.Version++; Masses[root.Entity] = mass;
                Recompute.SetComponentEnabled(root.Entity, false); Changed.Add(root.Entity);
            }
        }
    }
}
