using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Converters;
using Game.Editor.Migration.Dto;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Editor.Migration
{
    public sealed class LegacyShipBuilderTests
    {
        readonly Dictionary<string, BlockDefinition> blocks = new();
        ValidationReport report;
        [SetUp] public void SetUp()
        {
            report = new ValidationReport("MIG");
            blocks.Add("hull", TestContentFactory.Block("block_hull", BlockKind.Structural));
            blocks.Add("thruster", TestContentFactory.Block("block_thruster", BlockKind.Functional, new[] { Vector3Int.zero, Vector3Int.forward }));
            blocks.Add("core", TestContentFactory.Block("block_core", BlockKind.Functional, modules: new BlockModule[] { new CoreModule { Params = new CoreParams { MaxCells = 100, MaxExtentCells = 20 } } }));
        }
        [TearDown] public void Clean() { blocks.Clear(); TestContentFactory.DestroyAll(); }
        LegacyShipBuildResult Build(params LegacyVoxelDto[] voxels) => LegacyShipBuilder.Build(
            new LegacyShipItemDto { RegistryKey = "ship_test", ShipVoxelData = new LegacyShipVoxelDataDto { Voxels = voxels } },
            key => blocks.TryGetValue(key, out var def) ? (def, (ushort)(key == "hull" ? 1 : key == "thruster" ? 2 : 3)) : default, report, "test");
        static LegacyVoxelDto V(string key, int x = 0, int y = 0, int z = 0) => new() { BlockKey = key, X = x, Y = y, Z = z, ModuleSlots = Array.Empty<string>() };
        [Test] public void Overlap_FunctionalFirst()
        {
            using var built = Build(V("hull"), V("thruster"), V("thruster", z: 1));
            Assert.AreEqual(1, built.Statistics.Placed); Assert.AreEqual(2, built.Statistics.DroppedOverlaps); Assert.AreEqual(2, built.Statistics.Cells);
            Assert.AreEqual(new int3(0), built.Source.Instances[0].Origin); Assert.AreEqual(2, built.Source.Instances[0].Block);
            Assert.AreEqual(2, report.Entries.Count(e => e.Code == "MIG-W030"));
        }
        [Test] public void NegativeCoordinates_BrickMath()
        {
            using var built = Build(V("hull", -1)); Assert.AreEqual(new int3(-1, 0, 0), built.Source.BrickCoords[0]);
            Assert.AreEqual((ushort)1, built.Source.Cells[VoxsCodec.CellIndex(new int3(15, 0, 0))].Ref);
            Assert.AreEqual(new int3(-1, 0, 0), built.BoundsMin); Assert.AreEqual(built.BoundsMin, built.BoundsMax);
        }
        [Test] public void MultiCellBlocks_ReferenceInstance()
        {
            using var built = Build(V("thruster")); Assert.AreEqual(1, built.Source.Instances.Length);
            Assert.AreEqual(2, built.Source.Instances[0].CellsAlive);
            Cell root = built.Source.Cells[0], child = built.Source.Cells[256];
            Assert.IsTrue(root.IsInstance); Assert.AreEqual(0, root.InstanceIndex); Assert.AreEqual(0, child.InstanceIndex);
            Assert.AreEqual((byte)CellFlags.InstanceRoot, root.Flags); Assert.AreEqual(0, child.Flags); Assert.AreEqual(255, child.Hp);
        }
        [Test] public void Anchor_FromSingleCore()
        {
            using var single = Build(V("thruster"), V("core", x: 4)); Assert.AreEqual(1, single.Source.AnchorInstance);
            using var multiple = Build(V("core"), V("core", x: 4)); Assert.AreEqual(-1, multiple.Source.AnchorInstance);
            using var none = Build(V("hull")); Assert.AreEqual(-1, none.Source.AnchorInstance);
        }
    }
}
