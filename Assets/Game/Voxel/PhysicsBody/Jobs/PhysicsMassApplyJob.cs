using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.11: one mass write per changed dynamic root, preserving rigid point velocities.</summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    internal struct PhysicsMassApplyJob : IJob
    {
        [ReadOnly] public NativeList<Entity> Changed;
        [ReadOnly] public ComponentLookup<GridMass> Masses;
        [ReadOnly] public ComponentLookup<GridColliderState> States;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<GridSource> Sources;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        public ComponentLookup<PhysicsMass> PhysicsMasses;
        public ComponentLookup<PhysicsVelocity> Velocities;
        public void Execute()
        {
            using var seen = new NativeHashSet<Entity>(math.max(1, Changed.Length), Allocator.Temp);
            for (int i = 0; i < Changed.Length; i++)
            {
                var root = Changed[i];
                if (!seen.Add(root) || !PhysicsMasses.HasComponent(root) || !States.HasComponent(root) || States[root].Motion != GridMotion.Dynamic) continue;
                var mass = Masses[root].Data; if (mass.CellCount == 0) continue;
                var grid = Grids[root]; bool planar = (Sources[root].Flags & GridSpawnFlags.PlanarInertia) != 0;
                var next = GridMassMath.CreateDynamicMass(mass, grid.CellMin, grid.CellMax, grid.CellSize, planar); var old = PhysicsMasses[root];
                if (Equal(old, next)) continue;
                if (Velocities.HasComponent(root))
                {
                    var velocity = Velocities[root]; GridMassMath.Reexpress(ref velocity, old, next, Transforms[root].Rotation, planar); Velocities[root] = velocity;
                }
                PhysicsMasses[root] = next;
            }
        }
        static bool Equal(in PhysicsMass a, in PhysicsMass b) => a.InverseMass == b.InverseMass && a.AngularExpansionFactor == b.AngularExpansionFactor &&
            math.all(a.InverseInertia == b.InverseInertia) && math.all(a.Transform.pos == b.Transform.pos) && math.all(a.Transform.rot.value == b.Transform.rot.value);
    }
}
