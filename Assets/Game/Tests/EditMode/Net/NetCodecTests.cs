using Game.Net.Protocol;
using Game.Net.Replication;
using Game.Net.Replication.Grids;
using Game.Simulation;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Net
{
    /// <summary>SPEC-90 NET-002/004/005: wire round trips and the interpolation buffer.</summary>
    [Category("Net")]
    public class NetCodecTests
    {
        [Test]
        public void Primitives_RoundTrip()
        {
            var w = new NetWriter(8);
            w.U8(7); w.U16(65000); w.I32(-12345); w.U64(ulong.MaxValue - 3); w.F32(1.5f); w.F64(-2.25); w.F3(new float3(1, 2, 3));
            w.D3(new double3(1e9, -4, 0.5)); w.Quat(quaternion.RotateY(1)); w.Str("pilot ✓"); w.Bool(true);
            var r = new NetReader(w.Data, 0, w.Length);
            Assert.AreEqual(7, r.U8()); Assert.AreEqual(65000, r.U16()); Assert.AreEqual(-12345, r.I32()); Assert.AreEqual(ulong.MaxValue - 3, r.U64());
            Assert.AreEqual(1.5f, r.F32()); Assert.AreEqual(-2.25, r.F64()); Assert.AreEqual(new float3(1, 2, 3), r.F3());
            Assert.AreEqual(new double3(1e9, -4, 0.5), r.D3()); Assert.AreEqual(quaternion.RotateY(1), r.Quat()); Assert.AreEqual("pilot ✓", r.Str()); Assert.IsTrue(r.Bool());
            Assert.AreEqual(0, r.Remaining);
            Assert.Throws<NetFormatException>(() => r.U8());
        }

        [Test]
        public void Deflated_RoundTrip()
        {
            var body = new NetWriter();
            for (int i = 0; i < 5000; i++) body.U32((uint)(i % 7));
            var w = new NetWriter(); w.U8(42); w.Deflated(body);
            Assert.Less(w.Length, body.Length / 4, "repetitive cells compress well");
            var r = new NetReader(w.Data, 0, w.Length);
            Assert.AreEqual(42, r.U8());
            var inner = r.Inflated();
            for (int i = 0; i < 5000; i++) Assert.AreEqual((uint)(i % 7), inner.U32());
        }

        [TestCase(10), TestCase(1024), TestCase(1025), TestCase(4096)]
        public void BrickCells_RoundTrip_SparseAndDense(int solid)
        {
            using var cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Temp);
            var c = cells;
            for (int i = 0; i < solid; i++) c[(i * 37) % 4096] = new Cell { Ref = (ushort)(1 + i % 5), Hp = (byte)(200 + i % 50), Flags = (byte)(i % 3) };
            var w = new NetWriter();
            GridCodec.WriteBrickCells(w, new int3(-2, 0, 5), cells);
            using var read = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Temp);
            var r = new NetReader(w.Data, 0, w.Length);
            Assert.AreEqual(new int3(-2, 0, 5), GridCodec.ReadBrickCells(r, read));
            for (int i = 0; i < 4096; i++) Assert.AreEqual(cells[i], read[i], "cell " + i);
            Assert.AreEqual(0, r.Remaining);
        }

        [Test]
        public void SpawnHeader_RoundTrip()
        {
            var h = new GridSpawnHeader { NetId = 99, OwnerSlot = 2, Kind = GridKind.Hulk, Motion = GridMotion.Kinematic, Role = GridRoleHint.Ship,
                HasAllegiance = 1, Allegiance = new Allegiance { Faction = 3, Team = 2 }, Flags = GridSpawnFlags.PlanarInertia, CellSize = .5f,
                Position = new float3(10, 0, -4), Rotation = quaternion.RotateY(.3f), Linear = new float3(1, 0, 2), AngularWorld = new float3(0, .2f, 0) };
            var w = new NetWriter(); GridCodec.WriteHeader(w, h);
            var back = GridCodec.ReadHeader(new NetReader(w.Data, 0, w.Length));
            Assert.AreEqual(h.NetId, back.NetId); Assert.AreEqual(h.OwnerSlot, back.OwnerSlot); Assert.AreEqual(h.Kind, back.Kind);
            Assert.AreEqual(h.Motion, back.Motion); Assert.AreEqual(h.Role, back.Role); Assert.IsTrue(h.Allegiance.Equals(back.Allegiance));
            Assert.AreEqual(h.Flags, back.Flags); Assert.AreEqual(h.Position, back.Position); Assert.AreEqual(h.Rotation, back.Rotation);
        }

        [Test]
        public void ChunkIds_DoNotCollideWithGrids()
        {
            ulong chunk = NetIds.Chunk(17, 123456);
            Assert.IsTrue(NetIds.IsChunk(chunk)); Assert.IsFalse(NetIds.IsChunk(1)); Assert.AreNotEqual(chunk, NetIds.Chunk(17, 123457));
        }

        [Test]
        public void Interpolation_LerpsBetweenSamples_AndExtrapolatesBounded()
        {
            var track = new ReplicaTrack();
            Assert.IsTrue(track.Add(new ReplicaSample { Tick = 100, Position = new double3(0, 0, 0), Rotation = quaternion.identity, Linear = new float3(60, 0, 0) }));
            Assert.IsTrue(track.Add(new ReplicaSample { Tick = 102, Position = new double3(2, 0, 0), Rotation = quaternion.identity, Linear = new float3(60, 0, 0) }));
            Assert.IsFalse(track.Add(new ReplicaSample { Tick = 101 }), "older samples are dropped");
            Assert.IsTrue(track.Sample(101, 12, out var mid)); Assert.AreEqual(1, mid.Position.x, 1e-6);
            Assert.IsTrue(track.Sample(108, 12, out var ahead)); Assert.AreEqual(2 + 6, ahead.Position.x, 1e-4, "60 m/s for 6 ticks");
            Assert.IsTrue(track.Sample(200, 12, out var far)); Assert.AreEqual(2 + 12, far.Position.x, 1e-4, "extrapolation stops at the cap");
            Assert.IsTrue(track.Sample(50, 12, out var before)); Assert.AreEqual(0, before.Position.x, 1e-6);
        }
    }
}
