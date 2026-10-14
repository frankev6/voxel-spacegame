using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.ShipEditor
{
    /// <summary>EDT-010: four required cases, tiny real definitions/structures and direct edits, no gameplay waits.</summary>
    public sealed class StatsCalculatorTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        sealed class Fixture : IDisposable
        {
            readonly BlobAssetReference<BlockDefsBlob> definitions;
            readonly BlobAssetReference<BlockModulesBlob> modules;
            readonly BlobAssetReference<OrientationTablesBlob> orientations;
            readonly ShipEditorConfig config;
            internal readonly EditorContext Context;
            internal readonly VoxelStructure Source = new();
            internal readonly ushort Hull, Core, Utility, Thruster;
            internal Fixture()
            {
                var weapon = TestContentFactory.Create<WeaponDefinition>("wpn_stats", so =>
                { TestContentFactory.Set(so, "damage", 8f); TestContentFactory.Set(so, "explosionDamage", 4f);
                    TestContentFactory.Set(so, "fireRate", 2f); TestContentFactory.Set(so, "powerDraw", 5f); });
                var hull = TestContentFactory.Block("block_stats_hull", BlockKind.Structural, edit: so =>
                { TestContentFactory.Set(so, "cellMass", 1.0004f); TestContentFactory.Set(so, "basePrice", 17); });
                var core = TestContentFactory.Block("block_stats_core", BlockKind.Functional, modules: new BlockModule[]
                    { new CoreModule { Params = new CoreParams { MaxCells = 100, MaxExtentCells = 16 } } }, edit: so =>
                { TestContentFactory.Set(so, "cellMass", 10.0004f); TestContentFactory.Set(so, "basePrice", 101); });
                var utility = TestContentFactory.Block("block_stats_utility", BlockKind.Functional,
                    new[] { Vector3Int.zero, Vector3Int.right }, new BlockModule[]
                    {
                        new PowerSourceModule { Params = new PowerSourceParams { Generation = 12.5f } },
                        new PowerStorageModule { Params = new PowerStorageParams { Storage = 33 } },
                        new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 150, RegenPerSecond = 2.5f, PowerDraw = 3 } },
                        new CargoModule { Params = new CargoParams { Capacity = 24 } },
                        new WeaponMountModule { Weapon = weapon }
                    }, so => { TestContentFactory.Set(so, "cellMass", 3.0003f); TestContentFactory.Set(so, "basePrice", 43); });
                var thruster = TestContentFactory.Block("block_stats_thruster", BlockKind.Functional, modules: new BlockModule[]
                    { new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 90.125f, PowerDraw = 2 } } }, edit: so =>
                { TestContentFactory.Set(so, "cellMass", 25.125f); TestContentFactory.Set(so, "basePrice", 59); });
                var content = new List<ContentDefinition> { TestContentFactory.Block("block_missing", BlockKind.Structural),
                    TestContentFactory.Item("item_missing"), hull, core, utility, thruster, weapon };
                definitions = BlockDefsBlobBuilderTests.Build(content, out var catalog, out modules);
                orientations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
                config = ScriptableObject.CreateInstance<ShipEditorConfig>(); Context = new EditorContext(config, catalog, definitions, orientations, 16);
                Hull = catalog.GetBlockIndex(hull); Core = catalog.GetBlockIndex(core);
                Utility = catalog.GetBlockIndex(utility); Thruster = catalog.GetBlockIndex(thruster);
            }
            internal void Seed(params VoxelEdit[] edits)
            {
                using var input = new NativeArray<VoxelEdit>(edits, Allocator.Temp); using var touched = new NativeList<int3>(Allocator.Temp);
                Assert.IsTrue(Source.ApplyEdits(input, new VoxelContent { Blocks = definitions, Orientations = orientations }, touched));
            }
            internal EditSession Session() => new(new ShipEditorEntryArgs { Source = Source,
                Options = new EditSessionOptions { Creative = true } }, Context);
            public void Dispose()
            { Context.Dispose(); Source.Dispose(); orientations.Dispose(); modules.Dispose(); definitions.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }
        static BlockRecord Record(ushort block, int x, byte orient = 0) => new()
            { Block = block, Origin = new int3(x, 0, 0), Orient = orient, DamageOffset = -1 };
        static VoxelEdit Place(ushort block, int x, byte orient = 0) => new()
            { Kind = VoxelEditKind.PlaceBlock, Block = block, Cell = new int3(x, 0, 0), Orient = orient };
        static void Apply(EditSession session, EditBatch batch)
            => Assert.IsTrue(session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "Stats check", new ApplyReport()));
        static void SameBits(StatTotals expected, StatTotals actual)
        {
            Assert.AreEqual(expected.Blocks, actual.Blocks); Assert.AreEqual(expected.Cells, actual.Cells); Assert.AreEqual(expected.Cost, actual.Cost);
            double[] a = { expected.MassKg, expected.ForwardThrustN, expected.PowerGen, expected.PowerUse, expected.PowerStorage, expected.Dps,
                expected.ShieldCapacity, expected.ShieldRegen, expected.CargoUnits, expected.CenterOfMassCells.x, expected.CenterOfMassCells.y, expected.CenterOfMassCells.z };
            double[] b = { actual.MassKg, actual.ForwardThrustN, actual.PowerGen, actual.PowerUse, actual.PowerStorage, actual.Dps,
                actual.ShieldCapacity, actual.ShieldRegen, actual.CargoUnits, actual.CenterOfMassCells.x, actual.CenterOfMassCells.y, actual.CenterOfMassCells.z };
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(BitConverter.DoubleToInt64Bits(a[i]), BitConverter.DoubleToInt64Bits(b[i]), "Stat field " + i);
        }
        static List<StatsCalculator.Row> Rows(EditSession session)
        {
            var rows = new List<StatsCalculator.Row>();
            StatsCalculator.GetRows(session.GetStats(true), session.GetStats(false), new double3(.5), new double3(.5), 2, rows); return rows;
        }
        static StatsCalculator.Row Row(List<StatsCalculator.Row> rows, string id) => rows.Find(row => row.Id == id);

        [Test] public void ApplyUndo_Exact()
        {
            using var f = new Fixture();
            f.Seed(Place(f.Core, 0), Place(f.Utility, 1), Place(f.Thruster, 3, 8),
                new VoxelEdit { Kind = VoxelEditKind.ClearCell, Cell = new int3(2, 0, 0) });
            using var session = f.Session(); var before = session.GetStats(false); SameBits(before, session.GetStats(true));
            Assert.AreEqual(3, before.Blocks); Assert.AreEqual(3, before.Cells);
            Assert.AreEqual(41.126, before.MassKg, 0.00001); Assert.AreEqual(90.125, before.ForwardThrustN);
            Assert.AreEqual(12.5, before.PowerGen); Assert.AreEqual(10, before.PowerUse); Assert.AreEqual(33, before.PowerStorage);
            Assert.AreEqual(20, before.Dps); Assert.AreEqual(150, before.ShieldCapacity); Assert.AreEqual(2.5, before.ShieldRegen);
            Assert.AreEqual(24, before.CargoUnits); Assert.AreEqual(203, before.Cost);
            Assert.AreEqual((10000 * .5 + 6001 * 2 + 25125 * 3.5) / 41126, before.CenterOfMassCells.x,
                "CoM uses rounded milli-mass and the full footprint even with a missing cell.");
            using var batch = new EditBatch(); var utility = Record(f.Utility, 1);
            utility.DamageOffset = new DraftReader(session.Draft, f.Context).CaptureDamage(utility, batch.Damage);
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = utility }); utility.Orient = 4;
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = utility });
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = Record(f.Thruster, 3, 8) });
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Thruster, 2) });
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Hull, 3) });
            Apply(session, batch); var edited = session.GetStats(false); Assert.AreEqual(-90.125, edited.ForwardThrustN);
            SameBits(before, session.GetStats(true)); Assert.IsTrue(session.Undo()); SameBits(before, session.GetStats(false));
            Assert.IsTrue(session.Redo()); SameBits(edited, session.GetStats(false));
            session.Discard(); SameBits(before, session.GetStats(false)); SameBits(before, session.GetStats(true));
            batch.Clear(); batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = Record(f.Core, 0) });
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = Record(f.Utility, 1) });
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = Record(f.Thruster, 3, 8) });
            Apply(session, batch); SameBits(default, session.GetStats(false));
            Assert.IsTrue(session.Undo()); SameBits(before, session.GetStats(false));
        }

        [Test] public void SymmetricShip_ComOnPlane()
        {
            using var f = new Fixture(); f.Seed(Place(f.Core, 0), Place(f.Hull, -1), Place(f.Hull, 1), Place(f.Utility, -3), Place(f.Utility, 3, 8));
            using var session = f.Session(); var symmetric = session.GetStats(false);
            Assert.AreEqual(new double3(.5), symmetric.CenterOfMassCells, "Both differently oriented two-cell footprints mirror through cell centre x=.5.");
            using var batch = new EditBatch(); batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Hull, 4) });
            Apply(session, batch); Assert.Greater(session.GetStats(false).CenterOfMassCells.x, .5);
            Assert.IsTrue(session.Undo()); SameBits(symmetric, session.GetStats(false));
        }

        [Test] public void AddThruster_ForwardThrustIncreases_DeltaPositive()
        {
            using var f = new Fixture(); f.Seed(Place(f.Core, 0)); using var session = f.Session(); using var batch = new EditBatch();
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Thruster, 1, 8) }); Apply(session, batch);
            Assert.AreEqual(90.125, session.GetStats(false).ForwardThrustN); var rows = Rows(session); Assert.AreEqual(11, rows.Count);
            var thrust = Row(rows, "forward-thrust"); Assert.AreEqual(.090125, thrust.Value); Assert.AreEqual(.090125, thrust.Delta); Assert.AreEqual(1, thrust.DeltaSign);
            Assert.AreEqual(1, Row(rows, "thrust-to-weight").DeltaSign); Assert.AreEqual(1, Row(rows, "acceleration").DeltaSign);
            Assert.AreEqual(-1, Row(rows, "power-net").DeltaSign); Assert.AreEqual(0, Row(rows, "shield").DeltaSign);
            // A sideways thruster contributes power and mass but no grid +Z thrust.
            batch.Clear(); batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Thruster, 2, 4) }); Apply(session, batch);
            Assert.AreEqual(90.125, session.GetStats(false).ForwardThrustN); Assert.AreEqual(4, session.GetStats(false).PowerUse);
        }

        [Test] public void AddMass_DeltaNegativeForTw()
        {
            using var f = new Fixture(); f.Seed(Place(f.Core, 0), Place(f.Thruster, 1, 8)); using var session = f.Session();
            double tw = session.GetStats(false).ThrustToWeight; using var batch = new EditBatch();
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Hull, 2) }); Apply(session, batch);
            Assert.Less(session.GetStats(false).ThrustToWeight, tw); Assert.AreEqual(90.125, session.GetStats(false).ForwardThrustN);
            var rows = Rows(session); Assert.Less(Row(rows, "thrust-to-weight").Delta, 0); Assert.AreEqual(-1, Row(rows, "thrust-to-weight").DeltaSign);
            Assert.Greater(Row(rows, "mass").Delta, 0); Assert.AreEqual(-1, Row(rows, "mass").DeltaSign);
            Assert.AreEqual(-1, Row(rows, "cost").DeltaSign); Assert.AreEqual(-1, Row(rows, "com-offset").DeltaSign);
            Assert.AreEqual(0, Row(rows, "forward-thrust").DeltaSign); Assert.AreEqual(0, Row(rows, "power-storage").DeltaSign);
            Assert.IsTrue(session.Undo()); foreach (var row in Rows(session)) { Assert.AreEqual(0, row.Delta); Assert.AreEqual(0, row.DeltaSign); }
        }
    }
}
