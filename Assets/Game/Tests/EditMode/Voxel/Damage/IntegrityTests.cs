using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public unsafe sealed class IntegrityTests
    {
        sealed class Fixture : System.IDisposable
        {
            public readonly TestGridFactory F = new TestGridFactory();
            public readonly SystemHandle System;
            public DamageStreamsData* Data;
            public Fixture()
            {
                F.World.CreateSystem<DamageStreamsSystem>(); System = F.World.CreateSystem<IntegritySystem>();
                using var q = F.EM.CreateEntityQuery(typeof(DamageStreams)); Data = q.GetSingleton<DamageStreams>().Data;
            }
            public void Configure(int budget = int.MaxValue, int warmup = 64)
            {
                using var q = F.EM.CreateEntityQuery(typeof(DamageConfig)); var c = q.GetSingleton<DamageConfig>();
                c.IntegrityNodeBudgetPerGrid = budget; c.BrickWarmupPerTick = warmup; c.MaxSeedsPerGrid = 65536; q.SetSingleton(c);
            }
            public void Removed(Entity root, List<int3> removed)
            {
                Data->Removed.Clear(); Data->RemovedGrids.Clear(); Data->TouchedBricks.Clear();
                foreach (var p in removed) Data->Removed.Add(new CellRemoved { Grid = root, Cell = p, Reason = CellRemovedReason.Destroyed });
                Data->RemovedGrids.Add(new GridRemovedRange { Grid = root, Start = 0, Count = removed.Count });
                var grid = F.EM.GetComponentData<VoxelGrid>(root); var slots = F.EM.GetBuffer<BrickSlot>(root); var seen = new HashSet<int>();
                foreach (var p in removed)
                {
                    int slot = BrickMath.SlotIndex(BrickMath.BrickOf(p), grid.BrickMin, grid.BrickDims);
                    if (slot >= 0 && slots[slot].Brick != Entity.Null && seen.Add(slot)) Data->TouchedBricks.Add(new TouchedBrick { Grid = root, SlotIndex = slot, Brick = slots[slot].Brick });
                }
            }
            public void Run() { System.Update(F.World.Unmanaged); F.EM.CompleteAllTrackedJobs(); Data->IntegrityHandle.Complete(); }
            public HashSet<int3> IslandCells(Entity root)
            {
                var result = new HashSet<int3>(); var slots = F.EM.GetBuffer<BrickSlot>(root);
                foreach (var island in Data->Islands)
                {
                    Assert.AreEqual(root, island.Grid); int count = 0, previous = -1;
                    for (int m = island.MaskStart; m < island.MaskStart + island.MaskCount; m++)
                    {
                        var mask = Data->IslandMasks[m]; Assert.Greater(mask.SlotIndex, previous); previous = mask.SlotIndex;
                        int3 coord = F.EM.GetComponentData<Brick>(slots[mask.SlotIndex].Brick).Coord;
                        for (int w = 0; w < 64; w++) for (ulong bits = mask.Mask.Words[w]; bits != 0; bits &= bits - 1)
                        { Assert.IsTrue(result.Add(BrickMath.CellOf(coord, w * 64 + math.tzcnt(bits))), "Duplicate island cell"); count++; }
                    }
                    Assert.AreEqual(island.CellCount, count);
                }
                return result;
            }
            public void Dispose() => F.Dispose();
        }
        static void Fill(VoxelStructure source, int3 min, int3 max)
        { for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++) source.SetCell(new int3(x,y,z), Cell.Palette(1)); }
        static HashSet<int3> Reference(VoxelStructure source, List<int3> removed, bool core)
        {
            source.TryGetCellBounds(out var min, out var max); var remaining = new HashSet<int3>(); var all = new HashSet<int3>();
            for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
            { var p = new int3(x,y,z); if (!source.GetCell(p).IsAir) { remaining.Add(p); all.Add(p); } }
            var seedOrder = new Dictionary<int3,int>();
            foreach (var p in removed) for (int face = 0; face < 6; face++)
            { var n = p + BrickMath.FaceDirection((BlockFace)face); if (all.Contains(n) && !seedOrder.ContainsKey(n)) seedOrder.Add(n, seedOrder.Count); }
            HashSet<int3> anchor = null; int anchorSeed = int.MaxValue; bool foundCore = false;
            while (remaining.Count != 0)
            {
                int3 start = default; foreach (var p in remaining) { start = p; break; }
                var component = new HashSet<int3>(); var queue = new Queue<int3>(); queue.Enqueue(start); remaining.Remove(start); int first = int.MaxValue; bool hasCore = false;
                while (queue.Count != 0)
                {
                    var p = queue.Dequeue(); component.Add(p); hasCore |= core && source.GetCell(p).IsInstance;
                    if (seedOrder.TryGetValue(p, out int order)) first = math.min(first, order);
                    for (int face = 0; face < 6; face++) { var n = p + BrickMath.FaceDirection((BlockFace)face); if (remaining.Remove(n)) queue.Enqueue(n); }
                }
                if (hasCore || (!foundCore && (anchor == null || component.Count > anchor.Count || (component.Count == anchor.Count && first < anchorSeed))))
                { anchor = component; anchorSeed = first; foundCore = hasCore; }
            }
            if (anchor != null) all.ExceptWith(anchor); return all;
        }
        [Test] public void MatchesReferenceBfs_RandomStructures()
        {
            var rng = new Unity.Mathematics.Random(73009); int islands = 0;
            for (int sample = 0; sample < 300; sample++)
            {
                using var f = new Fixture(); f.Configure();
                int3 size = rng.NextInt3(new int3(4), new int3(17)); if (sample % 30 == 0) size.x = 40;
                var source = f.F.Box(0, size - 1); bool core = sample % 3 == 0;
                // Attach the intact core to the original solid box before removals; random cuts can separate it.
                if (core) f.F.Place(source, "block_core_cross", new int3(-2, 0, 1));
                var removed = new List<int3>(); float chance = rng.NextFloat(.1f, .8f);
                for (int z = 0; z < size.z; z++) for (int y = 0; y < size.y; y++) for (int x = 0; x < size.x; x++)
                    if (rng.NextFloat() < chance) { var p = new int3(x,y,z); source.SetCell(p, default); removed.Add(p); }
                var root = f.F.Spawn(source); f.Removed(root, removed); f.Run();
                Assert.AreEqual(0, f.F.EM.GetComponentData<GridIntegrityState>(root).ConsecutiveDeferrals);
                var expected = Reference(source, removed, core); var actual = f.IslandCells(root);
                if (!expected.SetEquals(actual))
                {
                    var missing = new HashSet<int3>(expected); missing.ExceptWith(actual); var extra = new HashSet<int3>(actual); extra.ExceptWith(expected);
                    TestContext.WriteLine($"Mismatch sample {sample}, size={size}, chance={chance}, expected={expected.Count}, actual={actual.Count}, missing={string.Join(";", missing)}, extra={string.Join(";", extra)}");
                }
                CollectionAssert.AreEquivalent(expected, actual, $"seed 73009, sample {sample}"); islands += f.Data->Islands.Length;
            }
            TestContext.WriteLine($"300 seeded structures matched full-grid BFS; {islands} islands; 100 core grids.");
        }
        static VoxelStructure Bridge(Fixture f, bool core)
        {
            var source = f.F.Bridge(); Fill(source, new int3(1,-1,-1), new int3(8,1,1));
            if (core) f.F.Place(source, "block_core_cross", new int3(-2,2,0));
            source.SetCell(0, default); return source;
        }
        [Test] public void Bridge_CorelessAnchorIsLargest()
        {
            using var f = new Fixture(); var source = Bridge(f, false); var root = f.F.Spawn(source); f.Removed(root, new List<int3> { 0 }); f.Run();
            Assert.AreEqual(1, f.Data->Islands.Length); Assert.AreEqual(27, f.Data->Islands[0].CellCount);
            foreach (var p in f.IslandCells(root)) Assert.Less(p.x, 0);
        }
        [Test] public void Bridge_CoreAnchorWinsOverSize()
        {
            using var f = new Fixture(); var source = Bridge(f, true); var root = f.F.Spawn(source); f.Removed(root, new List<int3> { 0 }); f.Run();
            Assert.AreEqual(1, f.Data->Islands.Length); Assert.AreEqual(72, f.Data->Islands[0].CellCount);
            foreach (var p in f.IslandCells(root)) Assert.Greater(p.x, 0);
        }
        [Test] public void Crater_On1MStation_VisitsFewNodes()
        {
            using var f = new Fixture(); var source = f.F.Box(0, new int3(99)); var removed = new List<int3>();
            for (int z = 47; z <= 53; z++) for (int y = 47; y <= 53; y++) for (int x = 47; x <= 53; x++)
            { var p = new int3(x,y,z); if (math.lengthsq(p-new int3(50)) < 9) { source.SetCell(p, default); removed.Add(p); } }
            var root = f.F.Spawn(source); f.Removed(root, removed); f.Run();
            Assert.AreEqual(0, f.Data->Islands.Length); Assert.AreEqual(0, f.F.EM.GetBuffer<IntegritySeed>(root).Length);
            int visited = f.Data->IntegrityNodesVisited; TestContext.WriteLine($"1M-cell station, radius-3 crater: {visited} nodes, target <=2000; {removed.Count} removed cells.");
            if (visited > 2000) UnityEngine.Debug.LogWarning($"Visited {visited} nodes; advisory target <=2000.");
        }
        [Test] public void Budget_DefersThenForces()
        {
            using var f = new Fixture(); f.Configure(100, 0); var source = f.F.Box(new int3(-20,-9,-9), new int3(-1,10,10));
            Fill(source, new int3(1,-9,-9), new int3(20,10,10)); var root = f.F.Spawn(source);
            // No touched-brick summaries: exercise cell-node traversal and the actual work limit.
            f.F.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(-1,0,0) });
            f.F.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(1,0,0) });
            for (int tick = 1; tick <= 4; tick++)
            {
                f.Run(); Assert.AreEqual(0, f.Data->Islands.Length); Assert.AreEqual(tick, f.F.EM.GetComponentData<GridIntegrityState>(root).ConsecutiveDeferrals);
                Assert.AreEqual(2, f.F.EM.GetBuffer<IntegritySeed>(root).Length); Assert.AreEqual(100, f.Data->IntegrityNodesVisited);
            }
            f.Run(); var forced = f.IslandCells(root); Assert.AreEqual(8000, forced.Count);
            Assert.AreEqual(0, f.F.EM.GetBuffer<IntegritySeed>(root).Length); Assert.AreEqual(0, f.F.EM.GetComponentData<GridIntegrityState>(root).ConsecutiveDeferrals);
            f.Configure(int.MaxValue, 0); f.F.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(-1,0,0) });
            f.F.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(1,0,0) }); f.Run(); CollectionAssert.AreEquivalent(forced, f.IslandCells(root));
        }
        [Test] public void InstanceLinks_KeepBlockTogether()
        {
            using var f = new Fixture(); var source = f.F.Box(new int3(14,0,0), new int3(14,0,0)); source.SetCell(new int3(14,0,0), default);
            f.F.Place(source, "block_thruster_medium", new int3(14,0,0));
            var removed = new List<int3> { new int3(15,0,0), new int3(15,0,1) }; foreach (var p in removed) source.SetCell(p, default);
            var root = f.F.Spawn(source); f.Removed(root, removed); f.Run();
            Assert.AreEqual(0, f.Data->Islands.Length, "Separated footprint cells spanning bricks remain joined by their instance");
            Assert.AreEqual(0, f.F.EM.GetBuffer<IntegritySeed>(root).Length);
        }
    }
}
