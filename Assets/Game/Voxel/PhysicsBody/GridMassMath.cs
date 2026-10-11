using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public static class GridMassMath
    {
        /// False if d.CellCount == 0 or d.Mass <= 0. com in grid-local metres; inertia about com.
        public static bool TryGetMassProperties(in StructureMassData d, bool planar,
                                                out float mass, out float3 centerOfMass, out quaternion inertiaOrientation, out float3 inertiaDiagonal) => throw new System.NotImplementedException("PHY-006");
        public static PhysicsMass CreateDynamicMass(in StructureMassData d, int3 cellMin, int3 cellMax, float cellSize, bool planar) => throw new System.NotImplementedException("PHY-006"); // requires CellCount > 0
        public static PhysicsMass CreateKinematicMass(in StructureMassData d) => throw new System.NotImplementedException("PHY-006");   // InverseMass 0, InverseInertia 0; Transform from d, identity if empty
        public static float AngularExpansion(float3 centerOfMass, int3 cellMin, int3 cellMax, float cellSize) => throw new System.NotImplementedException("PHY-006");
        /// Keeps every body point's world velocity when the mass frame changes (decision 12).
        public static void Reexpress(ref PhysicsVelocity v, in PhysicsMass oldMass, in PhysicsMass newMass, quaternion bodyRotation, bool planar) => throw new System.NotImplementedException("PHY-006");
        /// Deterministic cyclic Jacobi; values ascending; vectors are the columns, right-handed. §5.11 step 4.
        public static void JacobiEigen(double3x3 symmetric, out double3 values, out double3x3 vectors) => throw new System.NotImplementedException("PHY-006");
    }
}
