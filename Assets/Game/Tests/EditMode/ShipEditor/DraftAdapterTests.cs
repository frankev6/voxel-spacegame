using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Data.Blocks;
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
    public sealed class DraftAdapterTests
    {
        sealed class Fixture : IDisposable
        {
            readonly BlobAssetReference<BlockDefsBlob> definitions;
            readonly BlobAssetReference<BlockModulesBlob> modules;
            readonly BlobAssetReference<OrientationTablesBlob> rotations;
            readonly ShipEditorConfig config;
            public readonly EditorContext Context;
            public readonly VoxelStructure Draft = new();
            public readonly DraftReader Reader;
            public readonly DraftWriter Writer;
            public readonly ushort Multi, Single, Hull;
            public VoxelContent Content => new VoxelContent { Blocks = definitions, Orientations = rotations };
            public Fixture()
            {
                var defs = BlockDefsBlobBuilderTests.Defs(out _, out _);
                var single = TestContentFactory.Block("block_single_function", BlockKind.Functional); defs.Add(single);
                definitions = BlockDefsBlobBuilderTests.Build(defs, out var catalog, out modules);
                rotations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
                config = ScriptableObject.CreateInstance<ShipEditorConfig>();
                Context = new EditorContext(config, catalog, definitions, rotations, 16);
                Multi = catalog.GetBlockIndex(catalog.Get<Game.Data.Definitions.BlockDefinition>(new ContentId("block_capacitor_small")));
                Single = catalog.GetBlockIndex(single);
                Hull = catalog.GetBlockIndex(catalog.Get<Game.Data.Definitions.BlockDefinition>(new ContentId("block_hull")));
                Reader = new DraftReader(Draft, Context); Writer = new DraftWriter(Draft, Context);
            }
            public void Edits(params VoxelEdit[] values)
            {
                using var edits = new NativeArray<VoxelEdit>(values, Allocator.Temp);
                using var touched = new NativeList<int3>(Allocator.Temp);
                Assert.IsTrue(Draft.ApplyEdits(edits, Content, touched));
            }
            public void Dispose()
            { Context.Dispose(); Draft.Dispose(); rotations.Dispose(); modules.Dispose(); definitions.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void DamagedBlock_RoundTrip_HashEqual()
        {
            using var f = new Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Color = 2 },
                new VoxelEdit { Kind = VoxelEditKind.ClearCell },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(1, 0, 0), Hp = 41 },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Multi, FunctionHp = 13 });
            Assert.IsTrue(f.Reader.TryGetBlockAt(new int3(1, 0, 0), out var record));
            using var damage = new NativeList<byte>(Allocator.TempJob);
            record.DamageOffset = f.Reader.CaptureDamage(record, damage); Assert.AreEqual(0, record.DamageOffset);
            ulong before = f.Reader.ComputeDraftHash();
            using var records = new NativeList<BlockRecord>(Allocator.TempJob); f.Reader.Enumerate(records);
            Assert.AreEqual(1, records.Length); Assert.AreEqual(int3.zero, records[0].Origin);
            using var operations = new NativeArray<DiffOp>(1, Allocator.Temp);
            operations[0] = new DiffOp { Kind = DiffOpKind.Remove, Record = record };
            using var touched = new NativeList<int3>(Allocator.Temp);
            f.Writer.Apply(operations, damage.AsArray(), true, touched); Assert.AreEqual(0, f.Draft.SolidCellCount);
            f.Writer.Apply(operations, damage.AsArray(), false, touched); Assert.AreEqual(before, f.Reader.ComputeDraftHash());
            Assert.IsFalse(f.Draft.IsPinned);
            using var invalid = new NativeArray<VoxelEdit>(new[] {
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(1, 0, 0), Hp = 1 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Cell = new int3(1, 0, 0) } }, Allocator.Temp);
            int count = touched.Length; Assert.IsFalse(f.Draft.ApplyEdits(invalid, f.Content, touched));
            Assert.AreEqual(count, touched.Length); Assert.AreEqual(before, f.Reader.ComputeDraftHash());
        }
        [Test] public void PaletteOrder_DoesNotChangeHash()
        {
            using var f = new Fixture(); using var other = new VoxelStructure();
            var a = new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Color = 1 };
            var b = new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Color = 2, Cell = new int3(3, 0, 0) };
            f.Edits(a, b);
            using var edits = new NativeArray<VoxelEdit>(new[] { b, a }, Allocator.Temp);
            using var touched = new NativeList<int3>(Allocator.Temp);
            Assert.IsTrue(other.ApplyEdits(edits, f.Content, touched));
            Assert.AreNotEqual(f.Draft.GetCell(int3.zero).Ref, other.GetCell(int3.zero).Ref);
            Assert.AreEqual(f.Reader.ComputeDraftHash(), new DraftReader(other, f.Context).ComputeDraftHash());
        }
        [Test] public void MissingCell_CapturedAsZero()
        {
            using var f = new Fixture(); f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi }, new VoxelEdit { Kind = VoxelEditKind.ClearCell });
            Assert.IsTrue(f.Reader.TryGetBlockAt(new int3(1, 0, 0), out var record));
            using var damage = new NativeList<byte>(Allocator.Temp); int offset = f.Reader.CaptureDamage(record, damage);
            Assert.AreEqual(0, damage[offset]); Assert.AreEqual(255, damage[offset + 1]);
            // A different functional block occupies the missing root; A's identity must still select A.
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Single });
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.RemoveBlock, Block = f.Multi });
            Assert.IsTrue(f.Reader.TryGetBlockAt(int3.zero, out var survivor)); Assert.AreEqual(f.Single, survivor.Block);
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.ClearCell }, new VoxelEdit { Kind = VoxelEditKind.RemoveBlock, Block = f.Single });
            Assert.AreEqual(0, f.Draft.SolidCellCount);
        }
        [Test] public void OnlyApplyEditsUsed_SourceScan()
        {
            string root = Path.Combine(Application.dataPath, "Game/ShipEditor"); int writers = 0;
            var forbidden = new Regex(@"\.(SetCell|TryPlaceBlock|TryRemoveBlockAt|SetBrickDense|SetBrickUniform|RemoveBrick|FindOrAddPalette|CopyRuntimeMetadata)\s*\(");
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file); Assert.IsFalse(forbidden.IsMatch(source), file);
                if (source.Contains(".ApplyEdits(")) { writers++; Assert.AreEqual("DraftWriter.cs", Path.GetFileName(file)); }
            }
            Assert.AreEqual(1, writers);
        }
    }
}
