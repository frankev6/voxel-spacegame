using System.Collections.Generic;
using System.Linq;
using Game.Data.Blocks;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-003: the 24-orientation group against SPEC-04 §5.3.</summary>
    public sealed class OrientationTablesTests
    {
        static readonly OrientationData D = OrientationTableGenerator.Generate();
        static readonly int3[] Faces = { new int3(1, 0, 0), new int3(-1, 0, 0), new int3(0, 1, 0), new int3(0, -1, 0), new int3(0, 0, 1), new int3(0, 0, -1) };

        static int Det(int3x3 m) => m.c0.x * (m.c1.y * m.c2.z - m.c1.z * m.c2.y) - m.c1.x * (m.c0.y * m.c2.z - m.c0.z * m.c2.y) + m.c2.x * (m.c0.y * m.c1.z - m.c0.z * m.c1.y);

        [Test]
        public void Generate_Produces24DistinctProperRotations()
        {
            Assert.AreEqual(24, D.Matrices.Length);
            var seen = new HashSet<string>();
            for (int o = 0; o < 24; o++)
            {
                var m = D.Matrices[o];
                Assert.AreEqual(1, Det(m), $"det of {o}");
                Assert.IsTrue(math.all(math.mul(math.transpose(m), m).c0 == new int3(1, 0, 0)) && math.all(math.mul(math.transpose(m), m).c1 == new int3(0, 1, 0)) &&
                              math.all(math.mul(math.transpose(m), m).c2 == new int3(0, 0, 1)), $"orthonormal {o}");
                Assert.IsTrue(seen.Add(m.ToString()), $"duplicate {o}");
            }
            Assert.AreEqual(576, D.ComposeTable.Length); Assert.AreEqual(24, D.InverseTable.Length); Assert.AreEqual(72, D.MirrorTable.Length);
            Assert.AreEqual(144, D.FaceTable.Length); Assert.AreEqual(12, D.AxisTurnTable.Length); Assert.AreEqual(36, D.ForwardUpTable.Length);
        }

        [Test]
        public void Identity_IsZero_AndYawIsMultipleOfFour()
        {
            Assert.AreEqual(int3x3.identity, OrientationTables.Matrix(0));
            Assert.AreEqual(new int3(1, 0, 0), OrientationTables.Rotate(new int3(0, 0, 1), 4), "yaw +90: +Z → +X");
            Assert.AreEqual(new int3(0, 0, -1), OrientationTables.Rotate(new int3(0, 0, 1), 8));
            Assert.AreEqual(new int3(-1, 0, 0), OrientationTables.Rotate(new int3(0, 0, 1), 12));
            foreach (byte o in new byte[] { 0, 4, 8, 12 }) Assert.AreEqual(new int3(0, 1, 0), OrientationTables.Rotate(new int3(0, 1, 0), o), "yaw keeps up");
            // Orientation 1 / 5 / 16 / 20 vectors.
            (byte o, int3 fwd, int3 up)[] v = { (1, new int3(0, 0, 1), new int3(-1, 0, 0)), (5, new int3(1, 0, 0), new int3(0, 0, 1)),
                                                (16, new int3(0, 1, 0), new int3(0, 0, -1)), (20, new int3(0, -1, 0), new int3(0, 0, 1)) };
            foreach (var (o, fwd, up) in v) { Assert.AreEqual(fwd, D.Matrices[o].c2, $"{o} fwd"); Assert.AreEqual(up, D.Matrices[o].c1, $"{o} up"); }
        }

        [Test]
        public void Compose_IsClosedAndAssociative()
        {
            for (byte a = 0; a < 24; a++) for (byte b = 0; b < 24; b++)
                {
                    byte ab = OrientationTables.Compose(a, b);
                    Assert.Less(ab, 24);
                    for (byte c = 0; c < 24; c++)
                        if (OrientationTables.Compose(ab, c) != OrientationTables.Compose(a, OrientationTables.Compose(b, c))) Assert.Fail($"({a}{b}){c}");
                }
            Assert.AreEqual(8, OrientationTables.Compose(4, 4)); Assert.AreEqual(12, OrientationTables.Compose(4, 8)); Assert.AreEqual(2, OrientationTables.Compose(1, 1));
        }

        [Test]
        public void Compose_MatchesSequentialRotate()
        {
            var rng = new Random(1234);
            for (int i = 0; i < 64; i++)
            {
                var v = rng.NextInt3(-20, 20);
                byte a = (byte)rng.NextInt(24), b = (byte)rng.NextInt(24);
                Assert.AreEqual(OrientationTables.Rotate(OrientationTables.Rotate(v, b), a), OrientationTables.Rotate(v, OrientationTables.Compose(a, b)));
            }
        }

        [Test]
        public void Inverse_ComposesToIdentity()
        {
            CollectionAssert.AreEqual(new byte[] { 0, 3, 2, 1, 12, 19, 6, 21, 8, 9, 10, 11, 4, 23, 14, 17, 20, 15, 18, 5, 16, 7, 22, 13 }, D.InverseTable.ToArray());
            for (byte o = 0; o < 24; o++) { Assert.AreEqual(0, OrientationTables.Compose(o, OrientationTables.Inverse(o))); Assert.AreEqual(0, OrientationTables.Compose(OrientationTables.Inverse(o), o)); }
        }

        [Test]
        public void Mirror_IsInvolution_AllAxes()
        {
            for (byte o = 0; o < 24; o++) for (int axis = 0; axis < 3; axis++) Assert.AreEqual(o, OrientationTables.Mirror(OrientationTables.Mirror(o, axis), axis), $"{o} axis {axis}");
        }

        [Test]
        public void Mirror_MatchesTestVectors()
        {
            byte[] Column(int axis) => Enumerable.Range(0, 24).Select(o => OrientationTables.Mirror((byte)o, axis)).ToArray();
            CollectionAssert.AreEqual(new byte[] { 0, 3, 2, 1, 12, 15, 14, 13, 8, 11, 10, 9, 4, 7, 6, 5, 16, 19, 18, 17, 20, 23, 22, 21 }, Column(0), "X");
            CollectionAssert.AreEqual(new byte[] { 2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15, 22, 21, 20, 23, 18, 17, 16, 19 }, Column(1), "Y");
            CollectionAssert.AreEqual(new byte[] { 8, 11, 10, 9, 4, 7, 6, 5, 0, 3, 2, 1, 12, 15, 14, 13, 18, 17, 16, 19, 22, 21, 20, 23 }, Column(2), "Z");
        }

        [Test]
        public void AxisTurn_MatchesTestVectors()
        {
            byte[] Row(int axis) => Enumerable.Range(0, 4).Select(q => OrientationTables.AxisTurn(axis, q)).ToArray();
            CollectionAssert.AreEqual(new byte[] { 0, 20, 10, 16 }, Row(0), "X");
            CollectionAssert.AreEqual(new byte[] { 0, 4, 8, 12 }, Row(1), "Y");
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, Row(2), "Z");
            Assert.AreEqual(OrientationTables.AxisTurn(1, 3), OrientationTables.AxisTurn(1, -1), "negative quarter turns");
            Assert.AreEqual(OrientationTables.AxisTurn(0, 1), OrientationTables.AxisTurn(0, 5));
        }

        [Test]
        public void FaceTable_MatchesRotate()
        {
            CollectionAssert.AreEqual(new[] { BlockFace.NegZ, BlockFace.PosZ, BlockFace.PosY, BlockFace.NegY, BlockFace.PosX, BlockFace.NegX },
                                      Enumerable.Range(0, 6).Select(f => OrientationTables.RotateFace((BlockFace)f, 4)).ToArray());
            for (byte o = 0; o < 24; o++)
                for (int f = 0; f < 6; f++)
                    Assert.AreEqual(Faces[(int)OrientationTables.RotateFace((BlockFace)f, o)], OrientationTables.Rotate(Faces[f], o));
        }

        [Test]
        public void FromForwardUp_RoundTrips()
        {
            int valid = 0;
            for (int fwd = 0; fwd < 6; fwd++)
                for (int up = 0; up < 6; up++)
                {
                    byte o = OrientationTables.FromForwardUp((BlockFace)fwd, (BlockFace)up);
                    bool perpendicular = math.dot(Faces[fwd], Faces[up]) == 0;
                    if (!perpendicular) { Assert.AreEqual(Orientation.Invalid, o); continue; }
                    valid++;
                    Assert.AreEqual(Faces[fwd], OrientationTables.Rotate(new int3(0, 0, 1), o)); Assert.AreEqual(Faces[up], OrientationTables.Rotate(new int3(0, 1, 0), o));
                }
            Assert.AreEqual(24, valid);
        }

        [Test]
        public void TryFromMatrix_RoundTripsAll24()
        {
            for (byte o = 0; o < 24; o++) { Assert.IsTrue(OrientationTables.TryFromMatrix(OrientationTables.Matrix(o), out var back)); Assert.AreEqual(o, back); }
            Assert.IsFalse(OrientationTables.TryFromMatrix(new int3x3(new int3(-1, 0, 0), new int3(0, 1, 0), new int3(0, 0, 1)), out var bad), "a reflection");
            Assert.AreEqual(Orientation.Invalid, bad);
        }

        [Test]
        public void Quaternion_MatchesMatrix()
        {
            var rng = new Random(99);
            for (byte o = 0; o < 24; o++)
                for (int i = 0; i < 4; i++)
                {
                    var v = rng.NextFloat3(-5, 5);
                    var a = math.mul(OrientationTables.ToQuaternion(o), v);
                    var b = OrientationTables.Rotate(v, o);
                    Assert.Less(math.length(a - b), 1e-4f, $"orientation {o}");
                }
        }

        [Test]
        public void Blob_EqualsManagedTables()
        {
            var blob = OrientationTableGenerator.BuildBlob(D, Allocator.Persistent);
            try
            {
                ref var t = ref blob.Value;
                Assert.AreEqual(24, t.Matrices.Length);
                for (int i = 0; i < 24; i++) { Assert.AreEqual(D.Matrices[i], t.Matrices[i]); Assert.AreEqual(D.Rotations[i], t.Rotations[i]); }
                for (int i = 0; i < 576; i++) Assert.AreEqual(D.ComposeTable[i], t.ComposeTable[i]);
                for (int i = 0; i < 24; i++) Assert.AreEqual(D.InverseTable[i], t.InverseTable[i]);
                for (int i = 0; i < 72; i++) Assert.AreEqual(D.MirrorTable[i], t.MirrorTable[i]);
                for (int i = 0; i < 144; i++) Assert.AreEqual(D.FaceTable[i], t.FaceTable[i]);
                for (int i = 0; i < 12; i++) Assert.AreEqual(D.AxisTurnTable[i], t.AxisTurnTable[i]);
                for (int i = 0; i < 36; i++) Assert.AreEqual(D.ForwardUpTable[i], t.ForwardUpTable[i]);
                Assert.AreEqual(OrientationTables.Compose(5, 17), t.Compose(5, 17));
                Assert.AreEqual(OrientationTables.Rotate(new int3(1, 2, 3), 13), t.Rotate(new int3(1, 2, 3), 13));
                Assert.AreEqual(OrientationTables.AxisTurn(2, -1), t.AxisTurn(2, -1));
                Assert.AreEqual(OrientationTables.FromForwardUp(BlockFace.PosX, BlockFace.PosY), t.FromForwardUp(BlockFace.PosX, BlockFace.PosY));
            }
            finally { blob.Dispose(); }
        }
    }
}
