using System;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Bodies;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>SPEC-31 SYS-003: four bounded structure checks; no world, gameplay ticks, scenes or timed waits.</summary>
    public sealed class StructureBuilderTests : GameTestBase
    {
        sealed class Fixture : IDisposable
        {
            readonly ContentService service;
            public readonly BlockDefinition Hull, Core, WideCore;
            public readonly ProceduralShapeBuilder Shapes;
            public readonly AsteroidBuilder Asteroids;
            public readonly VoxsBlockNames Names;
            public VoxelContent Content => new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            public Fixture()
            {
                Hull = TestContentFactory.Block("block_builder_hull", BlockKind.Structural);
                var modules = new BlockModule[] { new CoreModule { Params = new CoreParams { MaxCells = 1000000, MaxExtentCells = 1000 } } };
                Core = TestContentFactory.Block("block_builder_core", BlockKind.Functional, modules: modules,
                    edit: so => TestContentFactory.Set(so, "blockCategory", (int)BlockCategory.Core));
                WideCore = TestContentFactory.Block("block_builder_core_wide", BlockKind.Functional,
                    new[] { Vector3Int.zero, new Vector3Int(1,0,0) }, modules,
                    so => TestContentFactory.Set(so, "blockCategory", (int)BlockCategory.Core));
                try
                {
                    service = ContentService.CreateForTests(new ContentDefinition[]
                    { TestContentFactory.Block("block_missing", BlockKind.Structural), TestContentFactory.Item("item_missing"), Hull, Core, WideCore });
                    Shapes = new ProceduralShapeBuilder(service.Catalog, Content);
                    Asteroids = new AsteroidBuilder(service.Catalog, Content); Names = VoxsBlockNames.FromCatalog(service.Catalog);
                }
                catch { service?.Dispose(); TestContentFactory.DestroyAll(); throw; }
            }
            public ProceduralShapeParams Params(ShapeKind kind, int wall = 2, bool core = false, bool large = false)
            {
                var size = kind switch
                {
                    ShapeKind.Ring => large ? new Vector3Int(64,32,32) : new Vector3Int(32,16,16),
                    ShapeKind.Cylinder => large ? new Vector3Int(64,48,64) : new Vector3Int(32,24,32),
                    ShapeKind.Box => large ? new Vector3Int(64,48,32) : new Vector3Int(32,24,16),
                    _ => large ? new Vector3Int(64,64,64) : new Vector3Int(32,32,32)
                };
                return new ProceduralShapeParams { kind = kind, sizeCells = size, wallThicknessCells = wall,
                    hullBlock = Hull, coreBlock = core ? Core : null, colorIndex = 7 };
            }
            public void Dispose() { service.Dispose(); TestContentFactory.DestroyAll(); }
        }
        static double Volume(in ProceduralShapeParams p)
        {
            double x = p.sizeCells.x, y = p.sizeCells.y, z = p.sizeCells.z, t = p.wallThicknessCells, r = x / 2, inner = Math.Max(0, r - t);
            if (p.kind == ShapeKind.Box) return x * y * z - (t == 0 ? 0 : Math.Max(0, x - 2*t) * Math.Max(0, y - 2*t) * Math.Max(0, z - 2*t));
            if (p.kind == ShapeKind.Sphere) return 4 * Math.PI / 3 * (r*r*r - (t == 0 ? 0 : inner*inner*inner));
            if (p.kind == ShapeKind.Cylinder) return Math.PI * (r*r*y - (t == 0 ? 0 : inner*inner*Math.Max(0, y - 2*t)));
            double hole = z / 2, enlargedHole = hole + t;
            return Math.PI * ((r*r - hole*hole)*y - (t == 0 ? 0 : Math.Max(0, inner*inner - enlargedHole*enlargedHole)*Math.Max(0, y - 2*t)));
        }
        static int3 NearestSolid(VoxelStructure source)
        {
            Assert.IsTrue(source.TryGetCellBounds(out var min, out var max));
            // Reference fixtures are symmetric; their geometric centre is the centre of the measured outer cell bounds.
            double3 centre = ((double3)min + max + 1) / 2;
            double nearest = double.MaxValue; int3 found = default;
            for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
            {
                var cell = new int3(x,y,z); if (source.GetCell(cell).IsAir) continue;
                double distance = math.lengthsq((double3)cell + .5 - centre);
                if (distance < nearest) { nearest = distance; found = cell; } // ascending z,y,x keeps the first tie
            }
            return found;
        }
        static void CheckCore(Fixture f, VoxelStructure source, BlockDefinition core, int3 expected)
        {
            Assert.AreEqual(1, source.Data.Instances.Length); var instance = source.Data.Instances[0];
            Assert.AreEqual(core.Id.Hash, f.Content.Blocks.Value.Blocks[instance.Block].IdHash);
            Assert.AreEqual(expected, instance.Origin); Assert.AreEqual(7, instance.Color); Assert.AreEqual(0, instance.Orient);
            var content = f.Content;
            ref var definition = ref content.Blocks.Value.Blocks[instance.Block];
            Assert.AreEqual(definition.Cells.Length, instance.CellsAlive); int roots = 0;
            for (int i = 0; i < definition.Cells.Length; i++)
            {
                var cell = source.GetCell(instance.Origin + definition.Cells[i]);
                Assert.IsTrue(cell.IsInstance); Assert.AreEqual(0, cell.InstanceIndex); Assert.AreEqual(255, cell.Hp);
                if ((cell.Flags & (byte)CellFlags.InstanceRoot) != 0) roots++;
            }
            Assert.AreEqual(1, roots); Assert.AreEqual(definition.FunctionHp, instance.FunctionHp);
        }
        [Test] public void Deterministic()
        {
            using var f = new Fixture();
            foreach (ShapeKind kind in Enum.GetValues(typeof(ShapeKind)))
            {
                var p = f.Params(kind, core: true);
                using var a = f.Shapes.Build(p, 42); using var b = f.Shapes.Build(p, 42);
                Assert.AreEqual(a.ComputeContentHash(f.Names), b.ComputeContentHash(f.Names), kind.ToString());
                Assert.AreEqual(a.SolidCellCount, b.SolidCellCount);
            }
            foreach (ulong seed in new[] { 0UL, 42UL, ulong.MaxValue })
            {
                using var a = f.Asteroids.Build(6, .5f, f.Hull, .25f, .4f, seed);
                using var b = f.Asteroids.Build(6, .5f, f.Hull, .25f, .4f, seed);
                Assert.AreEqual(a.ComputeContentHash(f.Names), b.ComputeContentHash(f.Names));
            }
        }
        [Test] public void ShapeCounts()
        {
            using var f = new Fixture();
            foreach (ShapeKind kind in Enum.GetValues(typeof(ShapeKind))) foreach (int wall in new[] { 0, 4 })
            {
                var p = f.Params(kind, wall, large: true); using var source = f.Shapes.Build(p, 123);
                double expected = Volume(p);
                Assert.That(source.SolidCellCount, Is.EqualTo(expected).Within(expected * .02), $"{kind}, wall={wall}");
                if (kind == ShapeKind.Box) Assert.AreEqual(expected, source.SolidCellCount);
                Assert.AreEqual(0, source.Data.Instances.Length);
                for (int i = 1; i < source.Data.Palette.Length; i++) Assert.AreEqual(7, source.Data.Palette[i].Color);
                TestContext.WriteLine($"SYS-003 {kind} wall={wall}: cells={source.SolidCellCount}, reference={expected:F3}, bricks={source.BrickCount}");
            }
        }
        [Test] public void OneCore()
        {
            using var f = new Fixture();
            foreach (ShapeKind kind in Enum.GetValues(typeof(ShapeKind)))
            {
                var p = f.Params(kind);
                using var hull = f.Shapes.Build(p, 42); int3 expected = NearestSolid(hull);
                p.coreBlock = f.Core; using var station = f.Shapes.Build(p, 42);
                CheckCore(f, station, f.Core, expected); Assert.AreEqual(hull.SolidCellCount, station.SolidCellCount);
            }
            var solid = f.Params(ShapeKind.Box, wall: 0); solid.sizeCells = new Vector3Int(7,5,9);
            using var oddHull = f.Shapes.Build(solid, 0); var origin = NearestSolid(oddHull);
            solid.coreBlock = f.WideCore; using var wide = f.Shapes.Build(solid, 0);
            CheckCore(f, wide, f.WideCore, origin); Assert.AreEqual(oddHull.SolidCellCount, wide.SolidCellCount);
            var even = f.Params(ShapeKind.Box, wall: 0, core: true);
            using var tied = f.Shapes.Build(even, 0); Assert.AreEqual(new int3(-1), tied.Data.Instances[0].Origin, "Lowest z,y,x resolves the eight centre-cell ties.");
        }
        [Test] public void Asteroid_RadiusAndNoise()
        {
            using var f = new Fixture(); const float radius = 6, cellSize = .5f, amplitude = .25f;
            using var sphere = f.Asteroids.Build(radius, cellSize, f.Hull, 0, .4f, 42);
            double reference = 4 * Math.PI / 3 * Math.Pow(radius / cellSize, 3);
            Assert.That(sphere.SolidCellCount, Is.EqualTo(reference).Within(reference * .02));
            using var noisy = f.Asteroids.Build(radius, cellSize, f.Hull, amplitude, .4f, 42);
            using var otherSeed = f.Asteroids.Build(radius, cellSize, f.Hull, amplitude, .4f, 77);
            using var otherFrequency = f.Asteroids.Build(radius, cellSize, f.Hull, amplitude, .9f, 42);
            Assert.AreNotEqual(noisy.ComputeContentHash(f.Names), otherSeed.ComputeContentHash(f.Names));
            Assert.AreNotEqual(noisy.ComputeContentHash(f.Names), otherFrequency.ComputeContentHash(f.Names));
            Assert.AreEqual(0, sphere.Data.Instances.Length); Assert.AreEqual(0, noisy.Data.Instances.Length);
            int extent = (int)Math.Ceiling(radius * (1 + amplitude) / cellSize); int outsideNominal = 0, holesInsideNominal = 0;
            int sphereMismatches = 0, missingInterior = 0, solidsBeyondLimit = 0;
            for (int z = -extent - 1; z <= extent; z++) for (int y = -extent - 1; y <= extent; y++) for (int x = -extent - 1; x <= extent; x++)
            {
                var cell = new int3(x,y,z); float distance = math.length(BrickMath.CellCenter(cell, cellSize));
                bool solid = !noisy.GetCell(cell).IsAir;
                if ((distance <= radius) != !sphere.GetCell(cell).IsAir) sphereMismatches++;
                if (distance <= radius * (1 - amplitude) && !solid) missingInterior++;
                if (distance > radius * (1 + amplitude) && solid) solidsBeyondLimit++;
                if (solid && distance > radius) outsideNominal++;
                if (!solid && distance < radius) holesInsideNominal++;
            }
            Assert.AreEqual(0, sphereMismatches, "Zero amplitude is an exact cell-centre sphere.");
            Assert.AreEqual(0, missingInterior); Assert.AreEqual(0, solidsBeyondLimit);
            Assert.Greater(outsideNominal, 0); Assert.Greater(holesInsideNominal, 0, "Noise must vary the boundary rather than merely translating a sphere.");
            TestContext.WriteLine($"SYS-003 asteroid: solid={noisy.SolidCellCount}, nominal sphere={sphere.SolidCellCount}, protrusions={outsideNominal}, indentations={holesInsideNominal}; 0 gameplay ticks/waits.");
        }
    }
}
