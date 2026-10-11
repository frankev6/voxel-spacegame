using System;
using System.Collections.Generic;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-006 analytic inertia, deterministic eigensystems and unchanged body-point motion.</summary>
    public sealed class GridMassMathTests
    {
        // Independent cell sum: VOX-007 owns the runtime accumulator and is not a dependency of PHY-006.
        static StructureMassData Sum(List<(int3 Cell, float Mass)> cells, float size)
        {
            var result = new StructureMassData();
            foreach (var cell in cells)
            {
                double3 p = ((double3)cell.Cell + 0.5) * size;
                result.Mass += cell.Mass; result.FirstMoment += cell.Mass * p;
                result.SecondDiag += cell.Mass * p * p;
                result.SecondOff += cell.Mass * new double3(p.x * p.y, p.y * p.z, p.z * p.x);
                result.CellInertia += cell.Mass * (double)size * size / 6;
                result.CellCount++;
            }
            return result;
        }
        static double3x3 Tensor(List<(int3 Cell, float Mass)> cells, float size)
        {
            var d = Sum(cells, size); double3 centre = d.FirstMoment / d.Mass;
            var tensor = new double3x3();
            foreach (var cell in cells)
            {
                double3 r = ((double3)cell.Cell + 0.5) * size - centre;
                double own = size * (double)size / 6;
                for (int col = 0; col < 3; col++) for (int row = 0; row < 3; row++)
                    tensor[col][row] += cell.Mass * ((col == row ? math.lengthsq(r) + own : 0) - r[col] * r[row]);
            }
            return tensor;
        }
        static List<(int3 Cell, float Mass)> Box(int3 dimensions, float mass)
        {
            var cells = new List<(int3, float)>();
            for (int z = 0; z < dimensions.z; z++) for (int y = 0; y < dimensions.y; y++) for (int x = 0; x < dimensions.x; x++)
                cells.Add((new int3(x, y, z), mass));
            return cells;
        }
        static double Error(double3x3 a, double3x3 b) => math.sqrt(math.lengthsq(a.c0 - b.c0) + math.lengthsq(a.c1 - b.c1) + math.lengthsq(a.c2 - b.c2));
        static double3x3 Diagonal(double3 d) => new double3x3(new double3(d.x, 0, 0), new double3(0, d.y, 0), new double3(0, 0, d.z));
        static double3x3 Reconstruct(double3 values, double3x3 vectors) => math.mul(math.mul(vectors, Diagonal(values)), math.transpose(vectors));

        [Test]
        public void Box_InertiaMatchesFormula()
        {
            foreach (var dimensions in new[] { new int3(1), new int3(2, 3, 7), new int3(13, 4, 2) })
            foreach (float scale in new[] { 0.3f, 1f, 2f })
            {
                var d = Sum(Box(dimensions, 7), scale);
                Assert.IsTrue(GridMassMath.TryGetMassProperties(d, false, out var mass, out var com, out var orientation, out var inertia));
                double3 squared = (double3)dimensions * dimensions;
                var expected = Diagonal(d.Mass * scale * (double)scale / 12 * new double3(squared.y + squared.z, squared.x + squared.z, squared.x + squared.y));
                var rotation = new float3x3(orientation);
                var actual = Reconstruct(inertia, new double3x3(rotation.c0, rotation.c1, rotation.c2));
                Assert.Less(Error(actual, expected) / Error(expected, default), 1e-4);
                Assert.AreEqual((float)d.Mass, mass);
                Assert.Less(math.distance((float3)dimensions * scale * 0.5f, com), 1e-5f);
                var dynamicMass = GridMassMath.CreateDynamicMass(d, int3.zero, dimensions - 1, scale, false);
                Assert.AreEqual(1f / mass, dynamicMass.InverseMass); Assert.AreEqual(1f / inertia, dynamicMass.InverseInertia);
                Assert.AreEqual(com, dynamicMass.CenterOfMass); Assert.AreEqual(orientation, dynamicMass.InertiaOrientation);
                Assert.That(dynamicMass.AngularExpansionFactor, Is.EqualTo(math.length((float3)dimensions * scale * 0.5f)).Within(1e-5f));
                var kinematic = GridMassMath.CreateKinematicMass(d);
                Assert.AreEqual(com, kinematic.CenterOfMass); Assert.AreEqual(orientation, kinematic.InertiaOrientation);
                Assert.AreEqual(0, kinematic.InverseMass); Assert.AreEqual(float3.zero, kinematic.InverseInertia);
            }
        }
        static List<(int3 Cell, float Mass)> Structure(ref Random random, int sample)
        {
            if (sample == 0) return new List<(int3, float)> { (new int3(0), 2), (new int3(1, 0, 0), 2), (new int3(0, 1, 0), 2) };
            var cells = new List<(int3, float)>();
            for (int i = 0; i < 32; i++) cells.Add((random.NextInt3(-10, 11), random.NextFloat(1, 50)));
            return cells;
        }
        [Test]
        public void Jacobi_Reconstructs_Tensor()
        {
            var random = new Random(0xAC0B1u);
            for (int sample = 0; sample < 501; sample++)
            {
                var cells = Structure(ref random, sample); var tensor = Tensor(cells, 0.5f);
                GridMassMath.JacobiEigen(tensor, out var values, out var vectors);
                Assert.Less(Error(Reconstruct(values, vectors), tensor) / Error(tensor, default), 1e-9);
                Assert.That(math.determinant(vectors), Is.EqualTo(1).Within(1e-12));
                Assert.IsTrue(values.x <= values.y && values.y <= values.z);
                var d = Sum(cells, 0.5f);
                GridMassMath.TryGetMassProperties(d, false, out _, out _, out var orientation, out var inertia);
                var r = new float3x3(orientation);
                Assert.Less(Error(Reconstruct(inertia, new double3x3(r.c0, r.c1, r.c2)), tensor) / Error(tensor, default), 1e-5);
            }
        }
        [BurstCompile(CompileSynchronously = true)]
        struct EigenJob : IJob
        {
            [ReadOnly] public NativeArray<double3x3> Input;
            public NativeArray<double3> Values;
            public NativeArray<double3x3> Vectors;
            public void Execute()
            {
                for (int i = 0; i < Input.Length; i++)
                { GridMassMath.JacobiEigen(Input[i], out var values, out var vectors); Values[i] = values; Vectors[i] = vectors; }
            }
        }
        static void SameBits(double3 a, double3 b)
        {
            for (int i = 0; i < 3; i++) Assert.AreEqual(BitConverter.DoubleToInt64Bits(a[i]), BitConverter.DoubleToInt64Bits(b[i]));
        }
        [Test]
        public void Jacobi_Deterministic()
        {
            var matrices = new double3x3[128]; var random = new Random(0xDE7E2u);
            matrices[0] = double3x3.identity; matrices[1] = Diagonal(new double3(3, 2, 1));
            for (int i = 2; i < matrices.Length; i++) matrices[i] = Tensor(Structure(ref random, i), 0.3f);
            foreach (var matrix in matrices)
            {
                GridMassMath.JacobiEigen(matrix, out var values, out var vectors);
                GridMassMath.JacobiEigen(matrix, out var again, out var axes);
                SameBits(values, again); for (int c = 0; c < 3; c++) SameBits(vectors[c], axes[c]);
            }
            using var input = new NativeArray<double3x3>(matrices, Allocator.TempJob);
            using var valuesA = new NativeArray<double3>(matrices.Length, Allocator.TempJob);
            using var vectorsA = new NativeArray<double3x3>(matrices.Length, Allocator.TempJob);
            using var valuesB = new NativeArray<double3>(matrices.Length, Allocator.TempJob);
            using var vectorsB = new NativeArray<double3x3>(matrices.Length, Allocator.TempJob);
            new EigenJob { Input = input, Values = valuesA, Vectors = vectorsA }.Schedule().Complete();
            new EigenJob { Input = input, Values = valuesB, Vectors = vectorsB }.Schedule().Complete();
            for (int i = 0; i < matrices.Length; i++)
            {
                SameBits(valuesA[i], valuesB[i]); for (int c = 0; c < 3; c++) SameBits(vectorsA[i][c], vectorsB[i][c]);
                Assert.Less(Error(Reconstruct(valuesA[i], vectorsA[i]), matrices[i]) / Error(matrices[i], default), 1e-9);
            }
        }
        [Test]
        public void Planar_LocksXZ()
        {
            var d = Sum(Box(new int3(3, 1, 7), 5), 0.5f);
            var mass = GridMassMath.CreateDynamicMass(d, int3.zero, new int3(2, 0, 6), 0.5f, true);
            Assert.AreEqual(quaternion.identity, mass.InertiaOrientation);
            Assert.AreEqual(0, mass.InverseInertia.x); Assert.AreEqual(0, mass.InverseInertia.z);
            Assert.That(mass.InverseInertia.y, Is.EqualTo(1 / (d.Mass * (9 + 49) * 0.25 / 12)).Within(1e-7));
        }
        static float3 PointVelocity(PhysicsVelocity velocity, PhysicsMass mass, quaternion rotation, float3 localPoint)
        {
            float3 worldOmega = math.rotate(math.mul(rotation, mass.InertiaOrientation), velocity.Angular);
            return velocity.Linear + math.cross(worldOmega, math.rotate(rotation, localPoint - mass.CenterOfMass));
        }
        [Test]
        public void Reexpress_PreservesPointVelocities()
        {
            var random = new Random(0xBEF2E55u);
            for (int sample = 0; sample < 1000; sample++)
            {
                bool planar = sample % 2 == 0;
                var rotation = planar ? quaternion.RotateY(random.NextFloat(-3, 3)) : random.NextQuaternionRotation();
                var oldMass = new PhysicsMass { Transform = new RigidTransform(planar ? quaternion.identity : random.NextQuaternionRotation(), random.NextFloat3(-3, 3)) };
                var newMass = new PhysicsMass { Transform = new RigidTransform(planar ? quaternion.identity : random.NextQuaternionRotation(), random.NextFloat3(-3, 3)) };
                var before = new PhysicsVelocity { Linear = random.NextFloat3(-10, 10), Angular = random.NextFloat3(-3, 3) };
                if (planar) { before.Angular.x = 0; before.Angular.z = 0; }
                var after = before; GridMassMath.Reexpress(ref after, oldMass, newMass, rotation, planar);
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new float3((corner & 1) == 0 ? -10 : 10, (corner & 2) == 0 ? -10 : 10, (corner & 4) == 0 ? -10 : 10);
                    Assert.Less(math.distance(PointVelocity(before, oldMass, rotation, point), PointVelocity(after, newMass, rotation, point)), 1e-4f);
                }
                if (planar) { Assert.AreEqual(0, after.Angular.x); Assert.AreEqual(0, after.Angular.z); }
            }
        }
        [Test]
        public void Empty_ReturnsFalse()
        {
            Assert.IsFalse(GridMassMath.TryGetMassProperties(default, false, out _, out _, out _, out _));
            Assert.IsFalse(GridMassMath.TryGetMassProperties(new StructureMassData { CellCount = 1 }, false, out _, out _, out _, out _));
            var kinematic = GridMassMath.CreateKinematicMass(default);
            Assert.AreEqual(RigidTransform.identity, kinematic.Transform);
            Assert.AreEqual(0, kinematic.InverseMass); Assert.AreEqual(float3.zero, kinematic.InverseInertia);
        }
    }
}
