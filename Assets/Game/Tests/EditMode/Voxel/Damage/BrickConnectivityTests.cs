using System.Collections.Generic;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public unsafe sealed class BrickConnectivityTests
    {
        sealed class Fixture : System.IDisposable
        {
            readonly World world = new World("Brick connectivity");
            public DynamicBuffer<BrickCells> Dense;
            public DynamicBuffer<BrickSparseCells> Sparse;
            public DynamicBuffer<BrickSolidMask> Mask;
            public Fixture()
            {
                var em = world.EntityManager;
                var entity = em.CreateEntity(typeof(BrickCells), typeof(BrickSparseCells), typeof(BrickSolidMask));
                Dense = em.GetBuffer<BrickCells>(entity); Sparse = em.GetBuffer<BrickSparseCells>(entity); Mask = em.GetBuffer<BrickSolidMask>(entity);
                Mask.ResizeUninitialized(64);
            }
            public BrickIntegrity Check(Cell[] cells, bool sparse = false)
            {
                Dense.Clear(); Sparse.Clear(); ushort count = 0;
                if (!sparse) Dense.ResizeUninitialized(4096);
                for (int word = 0; word < 64; word++) Mask[word] = default;
                for (int i = 0; i < 4096; i++)
                {
                    if (!sparse) Dense[i] = new BrickCells { Value = cells[i] };
                    if (cells[i].IsAir) continue;
                    count++; Mask[i >> 6] = new BrickSolidMask { Bits = Mask[i >> 6].Bits | (1UL << (i & 63)) };
                    if (sparse) Sparse.Add(new BrickSparseCells { Local = (ushort)i, Value = cells[i] });
                }
                return BrickConnectivityCheck.Evaluate(count, Mask, Dense, Sparse);
            }
            public void Dispose() => world.Dispose();
        }
        [Test] public void MatchesReferenceBfs_RandomMasks()
        {
            using var f = new Fixture(); var rng = new Unity.Mathematics.Random(82917);
            int connected = 0;
            for (int sample = 0; sample < 1000; sample++)
            {
                var cells = new Cell[4096]; float density = .1f + .8f * (sample % 9) / 8;
                for (int i = 0; i < cells.Length; i++) if (rng.NextFloat() < density) cells[i] = Cell.Palette(1);
                var result = f.Check(cells, sample % 2 == 0); var expected = Reference(cells);
                Assert.AreEqual(expected, result.State, $"seed 82917, sample {sample}, density {density}");
                Assert.AreEqual(0, result.HasInstanceCells);
                int solids = 0; foreach (var cell in cells) if (!cell.IsAir) solids++;
                Assert.AreEqual(solids, result.SolidCountAtCheck);
                if (result.State == BrickConnectivity.Connected) connected++;
            }
            Assert.Greater(connected, 0); Assert.Less(connected, 1000);
            TestContext.WriteLine($"1000 reference masks: {connected} connected, {1000-connected} split; dense/sparse alternate.");
        }
        static BrickConnectivity Reference(Cell[] cells)
        {
            var seen = new bool[4096]; var queue = new Queue<int>(); int components = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (seen[i] || cells[i].IsAir) continue;
                if (++components > 1) return BrickConnectivity.Split;
                seen[i] = true; queue.Enqueue(i);
                while (queue.Count != 0)
                {
                    int n = queue.Dequeue(), x = n & 15, y = (n >> 4) & 15, z = n >> 8;
                    if (x > 0) Visit(n-1); if (x < 15) Visit(n+1);
                    if (y > 0) Visit(n-16); if (y < 15) Visit(n+16);
                    if (z > 0) Visit(n-256); if (z < 15) Visit(n+256);
                }
            }
            return components == 1 ? BrickConnectivity.Connected : BrickConnectivity.Unknown;
            void Visit(int n) { if (seen[n] || cells[n].IsAir) return; seen[n] = true; queue.Enqueue(n); }
        }
        [Test] public void InstanceLink_Connects()
        {
            using var f = new Fixture(); var cells = new Cell[4096];
            cells[0] = Cell.Palette(1); cells[1] = Cell.Instance(7);
            cells[3] = Cell.Instance(7); cells[4] = Cell.Palette(1);
            Assert.AreEqual(BrickConnectivity.Split, Reference(cells));
            foreach (bool sparse in new[] { false, true })
            {
                var result = f.Check(cells, sparse); Assert.AreEqual(BrickConnectivity.Connected, result.State);
                Assert.AreEqual(1, result.HasInstanceCells); Assert.AreEqual(4, result.SolidCountAtCheck);
            }
        }
        [Test] public void TrivialBricks_ShortCircuit()
        {
            using (var f = new Fixture())
            {
                var cells = new Cell[4096]; cells[4095] = Cell.Instance(3);
                var one = f.Check(cells, true); Assert.AreEqual(BrickConnectivity.Connected, one.State);
                Assert.AreEqual(1, one.SolidCountAtCheck); Assert.AreEqual(1, one.HasInstanceCells);
                for (int i = 0; i < cells.Length; i++) cells[i] = Cell.Palette(1);
                var full = f.Check(cells); Assert.AreEqual(BrickConnectivity.Connected, full.State);
                Assert.AreEqual(4096, full.SolidCountAtCheck); Assert.AreEqual(0, full.HasInstanceCells);
            }
            // Production selection: budget shared by candidate order, lowest slots first, touched bricks outside warm-up quota.
            using var grids = new TestGridFactory();
            var a = grids.Spawn(grids.Box(0, new int3(47, 0, 0))); var b = grids.Spawn(grids.Box(0, new int3(47, 0, 0)));
            grids.World.CreateSystem<DamageStreamsSystem>();
            using var configQuery = grids.EM.CreateEntityQuery(typeof(DamageConfig));
            var config = configQuery.GetSingleton<DamageConfig>(); config.BrickWarmupPerTick = 3; config.MaxFullFloodCells = 1; configQuery.SetSingleton(config);
            grids.EM.GetBuffer<IntegritySeed>(a).Add(new IntegritySeed()); grids.EM.GetBuffer<IntegritySeed>(b).Add(new IntegritySeed());
            var system = grids.World.CreateSystem<IntegritySystem>();
            LogAssert.Expect(LogType.Warning, "[Damage] flood deferred; seeds kept. cap=1");
            LogAssert.Expect(LogType.Warning, "[Damage] flood deferred; seeds kept. cap=1");
            system.Update(grids.World.Unmanaged); grids.EM.CompleteAllTrackedJobs();
            var slotsA = grids.EM.GetBuffer<BrickSlot>(a); var slotsB = grids.EM.GetBuffer<BrickSlot>(b);
            Assert.AreEqual(BrickConnectivity.Connected, grids.EM.GetComponentData<BrickIntegrity>(slotsA[0].Brick).State);
            Assert.AreEqual(BrickConnectivity.Connected, grids.EM.GetComponentData<BrickIntegrity>(slotsA[1].Brick).State);
            Assert.AreEqual(BrickConnectivity.Unknown, grids.EM.GetComponentData<BrickIntegrity>(slotsA[2].Brick).State);
            Assert.AreEqual(BrickConnectivity.Connected, grids.EM.GetComponentData<BrickIntegrity>(slotsB[0].Brick).State);
            Assert.AreEqual(BrickConnectivity.Unknown, grids.EM.GetComponentData<BrickIntegrity>(slotsB[1].Brick).State);
            config.BrickWarmupPerTick = 0; configQuery.SetSingleton(config);
            using var streams = grids.EM.CreateEntityQuery(typeof(DamageStreams));
            streams.GetSingleton<DamageStreams>().Data->TouchedBricks.Add(new TouchedBrick { Grid = b, Brick = slotsB[2].Brick, SlotIndex = 2 });
            system.Update(grids.World.Unmanaged); grids.EM.CompleteAllTrackedJobs();
            slotsB = grids.EM.GetBuffer<BrickSlot>(b);
            Assert.AreEqual(BrickConnectivity.Connected, grids.EM.GetComponentData<BrickIntegrity>(slotsB[2].Brick).State);
            Assert.AreEqual(BrickConnectivity.Unknown, grids.EM.GetComponentData<BrickIntegrity>(slotsB[1].Brick).State);
        }
    }
}
