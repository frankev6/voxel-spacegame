using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-008: seeded fine-step reference, deterministic ties, empty-brick queries and ECS parity.</summary>
    public sealed class VoxelRaycastTests
    {
        struct Ray { public float3 Origin, Direction; public float Length; }
        struct Result { public bool Found; public VoxelRayHit Hit; }
        static VoxelStructure RandomStructure(NativeArray<byte> solids)
        {
            var rng = new Random(7); var structure = new VoxelStructure();
            for (int z = 0; z < 64; z++) for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                if (rng.NextInt(10) == 0) { solids[x + 64 * (y + 64 * z)] = 1; structure.SetCell(new int3(x, y, z), Cell.Palette(1)); }
            return structure;
        }
        static void FillRays(NativeArray<Ray> rays)
        {
            var rng = new Random(123);
            for (int i = 0; i < rays.Length; i++) rays[i] = new Ray
            { Origin = rng.NextFloat3(-8, 72), Direction = rng.NextFloat3Direction() * rng.NextFloat(0.1f, 4), Length = 110 };
        }
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
        struct CompareJob : IJobParallelFor
        {
            [ReadOnly] public VoxelStructureData Data;
            [ReadOnly] public NativeArray<byte> Solids;
            [ReadOnly] public NativeArray<Ray> Rays;
            public NativeArray<Result> Actual, Expected;
            public void Execute(int i)
            {
                var ray = Rays[i]; var query = new StructureCellQuery { Data = Data };
                bool found = VoxelRaycast.Cast(ref query, ray.Origin, ray.Direction, ray.Length, out var hit);
                Actual[i] = new Result { Found = found, Hit = hit };
                Expected[i] = Reference(ray);
            }
            bool Solid(int3 cell) => math.all(cell >= 0) && math.all(cell < 64) && Solids[cell.x + 64 * (cell.y + 64 * cell.z)] != 0;
            Result Reference(Ray ray)
            {
                double3 origin = ray.Origin, direction = math.normalize((double3)ray.Direction);
                int3 previous = (int3)math.floor(origin);
                if (Solid(previous)) return new Result { Found = true, Hit = new VoxelRayHit { Cell = previous, EnteredFace = 255 } };
                // Independent 0.001-cell sampling. At a multi-axis crossing, test all cells in the tiny
                // segment's bounding box with slabs: point samples alone can miss a <0.001-cell grazing hit.
                for (double begin = 0; begin < ray.Length; begin += 0.001)
                {
                    double end = math.min(begin + 0.001, ray.Length);
                    int3 next = (int3)math.floor(origin + direction * end);
                    if (math.all(previous == next)) continue;
                    int3 min = math.min(previous, next), max = math.max(previous, next);
                    double best = double.PositiveInfinity; VoxelRayHit bestHit = default;
                    for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
                    {
                        int3 cell = new int3(x, y, z); if (!Solid(cell)) continue;
                        double entry = double.NegativeInfinity, exit = double.PositiveInfinity; byte face = 255;
                        for (int axis = 0; axis < 3; axis++)
                        {
                            double a = (cell[axis] - origin[axis]) / direction[axis];
                            double b = (cell[axis] + 1.0 - origin[axis]) / direction[axis];
                            double near = math.min(a, b), far = math.max(a, b);
                            if (near >= entry) { entry = near; face = (byte)(axis * 2 + (direction[axis] > 0 ? 1 : 0)); }
                            exit = math.min(exit, far);
                        }
                        if (entry >= begin - 1e-9 && entry <= end && entry <= exit && entry < best)
                        { best = entry; bestHit = new VoxelRayHit { Cell = cell, EnteredFace = face, DistanceCells = (float)entry }; }
                    }
                    if (best <= end) return new Result { Found = true, Hit = bestHit };
                    previous = next;
                }
                return default;
            }
        }
        [Test]
        public void Random_MatchesBruteForce()
        {
            using var solids = new NativeArray<byte>(64 * 64 * 64, Allocator.TempJob);
            using var structure = RandomStructure(solids);
            using var rays = new NativeArray<Ray>(10000, Allocator.TempJob); FillRays(rays);
            using var actual = new NativeArray<Result>(rays.Length, Allocator.TempJob);
            using var expected = new NativeArray<Result>(rays.Length, Allocator.TempJob);
            new CompareJob { Data = structure.Data, Solids = solids, Rays = rays, Actual = actual, Expected = expected }.Schedule(rays.Length, 32).Complete();
            int hits = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                Assert.AreEqual(expected[i].Found, actual[i].Found, "ray " + i);
                if (!actual[i].Found) continue;
                hits++; Assert.AreEqual(expected[i].Hit.Cell, actual[i].Hit.Cell, "ray " + i);
                Assert.AreEqual(expected[i].Hit.EnteredFace, actual[i].Hit.EnteredFace, "ray " + i);
                Assert.That(actual[i].Hit.DistanceCells, Is.EqualTo(expected[i].Hit.DistanceCells).Within(0.0001f), "ray " + i);
            }
            Assert.Greater(hits, 5000); TestContext.WriteLine("10,000 rays: " + hits + " hits match the 0.001-cell reference");
        }
        [Test]
        public void StartInsideSolid_DistanceZero()
        {
            using var structure = new VoxelStructure(); structure.SetCell(new int3(-1, 2, 3), Cell.Palette(1));
            var query = new StructureCellQuery { Data = structure.Data };
            Assert.IsTrue(VoxelRaycast.Cast(ref query, new float3(-0.5f, 2.5f, 3.5f), new float3(10, -3, 2), 100, out var hit));
            Assert.AreEqual(new int3(-1, 2, 3), hit.Cell); Assert.AreEqual(0, hit.DistanceCells); Assert.AreEqual(255, hit.EnteredFace);
            Assert.IsFalse(VoxelRaycast.Cast(ref query, new float3(-0.5f, 2.5f, 3.5f), float3.zero, 100, out _));
        }
        [Test]
        public void TieBreak_XYZ()
        {
            foreach (int sign in new[] { -1, 1 })
            {
                using var structure = new VoxelStructure();
                int3 x = new int3(sign, 0, 0), y = new int3(sign, sign, 0), z = new int3(sign);
                structure.SetCell(x, Cell.Palette(1)); structure.SetCell(y, Cell.Palette(1)); structure.SetCell(z, Cell.Palette(1));
                var query = new StructureCellQuery { Data = structure.Data };
                foreach (var expected in new[] { x, y, z })
                {
                    Assert.IsTrue(VoxelRaycast.Cast(ref query, new float3(0.5f), new float3(sign), 10, out var hit));
                    Assert.AreEqual(expected, hit.Cell);
                    int axis = math.all(expected == x) ? 0 : math.all(expected == y) ? 1 : 2;
                    Assert.AreEqual(axis * 2 + (sign > 0 ? 1 : 0), hit.EnteredFace);
                    structure.SetCell(expected, default);
                }
            }
            using var boundary = new VoxelStructure(); boundary.SetCell(new int3(0, 1, 1), Cell.Palette(1));
            var boundaryQuery = new StructureCellQuery { Data = boundary.Data };
            Assert.IsTrue(VoxelRaycast.Cast(ref boundaryQuery, new float3(1), new float3(-1), 0, out var boundaryHit));
            Assert.AreEqual(new int3(0, 1, 1), boundaryHit.Cell); Assert.AreEqual((byte)BlockFace.PosX, boundaryHit.EnteredFace);
        }
        struct CountQuery : ICellSolidQuery
        {
            public int SolidTests, BrickTests;
            public bool IsSolid(int3 cell) { SolidTests++; return cell.x == 1600; }
            public bool BrickExists(int3 brick) { BrickTests++; return brick.x == 100; }
        }
        [Test]
        public void EmptyBricks_Skipped()
        {
            var query = new CountQuery();
            Assert.IsTrue(VoxelRaycast.Cast(ref query, new float3(0.5f), new float3(2, 0, 0), 1600, out var hit));
            Assert.AreEqual(new int3(1600, 0, 0), hit.Cell); Assert.AreEqual((byte)BlockFace.NegX, hit.EnteredFace);
            Assert.AreEqual(1599.5f, hit.DistanceCells); Assert.LessOrEqual(query.SolidTests + query.BrickTests, 300);
            TestContext.WriteLine("100 empty bricks: " + query.SolidTests + " cell queries, " + query.BrickTests + " brick queries");
        }
        [DisableAutoCreation]
        partial struct LookupOwnerSystem : ISystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
        }
        [BurstCompile(CompileSynchronously = true)]
        struct EcsCompareJob : IJobParallelFor
        {
            [ReadOnly] public VoxelStructureData Data;
            public GridCellReader Reader;
            public Entity Root;
            [ReadOnly] public NativeArray<Ray> Rays;
            public NativeArray<Result> Structure, Ecs;
            public void Execute(int i)
            {
                var ray = Rays[i]; var structure = new StructureCellQuery { Data = Data }; var ecs = new GridCellQuery { Reader = Reader, Grid = Root };
                bool a = VoxelRaycast.Cast(ref structure, ray.Origin, ray.Direction, ray.Length, out var ah);
                bool b = VoxelRaycast.Cast(ref ecs, ray.Origin, ray.Direction, ray.Length, out var bh);
                Structure[i] = new Result { Found = a, Hit = ah }; Ecs[i] = new Result { Found = b, Hit = bh };
            }
        }
        [Test]
        public void EcsAndStructure_Agree()
        {
            using var solids = new NativeArray<byte>(64 * 64 * 64, Allocator.TempJob);
            using var structure = RandomStructure(solids);
            // Add a uniform negative-coordinate brick; the remaining negative slots stay empty.
            structure.SetBrickUniform(new int3(-1), Cell.Palette(1));
            using var world = new World("VoxelRaycastTests"); var em = world.EntityManager;
            var root = em.CreateEntity(typeof(VoxelGrid), typeof(BrickSlot));
            var grid = new VoxelGrid { BrickMin = new int3(-1), BrickDims = new int3(5) }; em.SetComponentData(root, grid);
            em.GetBuffer<BrickSlot>(root).Resize(125, NativeArrayOptions.ClearMemory);
            foreach (var record in structure.Data.Bricks)
            {
                var entity = em.CreateEntity(typeof(BrickCells), typeof(BrickSolidMask));
                var cells = em.GetBuffer<BrickCells>(entity); cells.ResizeUninitialized(4096);
                for (int i = 0; i < 4096; i++) cells[i] = new BrickCells { Value = structure.GetCell(BrickMath.CellOf(record.Coord, i)) };
                var masks = em.GetBuffer<BrickSolidMask>(entity); masks.ResizeUninitialized(64);
                structure.Data.Masks[structure.Data.BrickIndex[record.Coord]].CopyTo(masks);
                var slots = em.GetBuffer<BrickSlot>(root); slots[BrickMath.SlotIndex(record.Coord, grid.BrickMin, grid.BrickDims)] = new BrickSlot { Brick = entity };
            }
            var handle = world.CreateSystem<LookupOwnerSystem>(); ref var state = ref world.Unmanaged.ResolveSystemStateRef(handle);
            using var rays = new NativeArray<Ray>(1000, Allocator.TempJob); FillRays(rays);
            using var a = new NativeArray<Result>(rays.Length, Allocator.TempJob); using var b = new NativeArray<Result>(rays.Length, Allocator.TempJob);
            new EcsCompareJob { Data = structure.Data, Reader = GridCellReader.Create(ref state), Root = root, Rays = rays, Structure = a, Ecs = b }.Schedule(rays.Length, 32).Complete();
            for (int i = 0; i < rays.Length; i++) Assert.AreEqual(a[i], b[i], "ray " + i);
        }
    }
}
