using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-006: BlockDefsBlobBuilder (SPEC-04 §5.5). Every blob is disposed in the test.</summary>
    public sealed class BlockDefsBlobBuilderTests
    {
        [TearDown] public void TearDown() => DestroyAll();

        internal static readonly Vector3Int[] Capacitor = { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0) };
        internal static readonly Vector3Int[] ThrusterCross = { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0),
                                                               new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(2, 0, 1), new Vector3Int(1, 1, 1), new Vector3Int(1, -1, 1) };
        internal static readonly Vector3Int[] Chiral = { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, 1, 1) };
        internal static readonly Vector3Int[] ChiralMirror = Chiral.Select(c => new Vector3Int(-c.x + 1, c.y, c.z)).ToArray();   // Sx + (1,0,0): contains the root

        internal static List<ContentDefinition> Defs(out BlockDefinition left, out BlockDefinition right, Mesh shared = null)
        {
            var defs = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing") };
            var mesh = shared ?? TestContentFactory.Mesh();
            defs.Add(Block("block_hull", BlockKind.Structural));
            defs.Add(Block("block_capacitor_small", BlockKind.Functional, Capacitor, edit: so => Set(so, "propMesh", mesh)));
            defs.Add(Block("block_thruster_medium", BlockKind.Functional, ThrusterCross,
                new BlockModule[] { new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 40000, PowerDraw = 0 } } }, so => Set(so, "propMesh", mesh)));
            left = Block("block_chiral_l", BlockKind.Prop, Chiral);
            right = Block("block_chiral_r", BlockKind.Prop, ChiralMirror);
            var l = left; var r = right;
            Edit(l, so => Set(so, "mirrorPartner", r)); Edit(r, so => Set(so, "mirrorPartner", l));
            defs.Add(left); defs.Add(right);
            return defs;
        }

        internal static BlobAssetReference<BlockDefsBlob> Build(List<ContentDefinition> defs, out ContentCatalog catalog, out BlobAssetReference<BlockModulesBlob> modules, List<Mesh> meshes = null)
        {
            var report = new ValidationReport("test");
            catalog = ContentCatalog.Build(defs, report);
            var blob = BlockDefsBlobBuilder.Build(catalog, report, meshes ?? new List<Mesh>(), Allocator.Persistent, out modules);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            return blob;
        }

        [Test]
        public void AirEntry_IsIndexZero()
        {
            var blob = Build(Defs(out _, out _), out var catalog, out var modules);
            try
            {
                ref var air = ref blob.Value.Blocks[0];
                Assert.AreEqual(0, air.Index); Assert.AreEqual(0, air.MirrorIndex); Assert.AreEqual(-1, air.PropMeshIndex);
                Assert.AreEqual(0, air.Cells.Length); Assert.AreEqual(0UL, air.ModuleMask); Assert.AreEqual(BlockCategory.Structure, air.Category);
                Assert.AreEqual(catalog.BlockCount + 1, blob.Value.Blocks.Length);
                for (int k = 1; k < blob.Value.Blocks.Length; k++)
                {
                    Assert.AreEqual(k, blob.Value.Blocks[k].Index);
                    Assert.AreEqual(catalog.GetBlock((ushort)k).Id.Hash, blob.Value.Blocks[k].IdHash);
                }
                Assert.AreEqual(catalog.GetBlockIndex(catalog.MissingBlock), blob.Value.MissingBlockIndex);
            }
            finally { blob.Dispose(); modules.Dispose(); }
        }

        [Test]
        public void Cells_RootFirstThenSorted()
        {
            var blob = Build(Defs(out _, out _), out var catalog, out var modules);
            try
            {
                ref var thruster = ref blob.Value.Blocks[catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new Game.Core.ContentId("block_thruster_medium")))];
                Assert.AreEqual(int3.zero, thruster.Cells[0]);
                var rest = new List<int3>(); for (int i = 1; i < thruster.Cells.Length; i++) rest.Add(thruster.Cells[i]);
                var sorted = rest.OrderBy(c => c.z).ThenBy(c => c.y).ThenBy(c => c.x).ToList();
                CollectionAssert.AreEqual(sorted, rest);
                Assert.AreEqual(new int3(0, -1, 0), thruster.BoundsMin); Assert.AreEqual(new int3(2, 1, 1), thruster.BoundsMax);
                Assert.IsTrue(thruster.HasModule(BlockModuleId.Thruster)); Assert.IsFalse(thruster.IsCore);
                Assert.IsTrue(thruster.RequiresInstance);
                Assert.IsTrue(modules.Value.TryGet<ThrusterParams>(BlockModuleId.Thruster, thruster.Index, out var row));
                Assert.AreEqual(40000f, row.ThrustNewtons);
                Assert.IsFalse(modules.Value.Has(BlockModuleId.Thruster, 1)); Assert.IsFalse(modules.Value.TryGet<CoreParams>(BlockModuleId.Core, thruster.Index, out _));
            }
            finally { blob.Dispose(); modules.Dispose(); }
        }

        [Test]
        public void BlockDefsHash_IsDeterministic()
        {
            var defs = Defs(out _, out _);
            var a = Build(defs, out _, out var ma); var reversed = Enumerable.Reverse(defs).ToList();
            var b = Build(reversed, out _, out var mb);
            try
            {
                Assert.AreEqual(a.Value.BlockDefsHash, b.Value.BlockDefsHash);
                var hull = (BlockDefinition)defs.First(d => d.IdString == "block_hull");
                Edit(hull, so => Set(so, "cellHp", 101f));
                var c = Build(defs, out _, out var mc);
                Assert.AreNotEqual(a.Value.BlockDefsHash, c.Value.BlockDefsHash, "cellHp is hashed");
                c.Dispose(); mc.Dispose();
                var thruster = (BlockDefinition)defs.First(d => d.IdString == "block_thruster_medium");
                Edit(thruster, so => so.FindProperty("modules").GetArrayElementAtIndex(0).managedReferenceValue = new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 1, PowerDraw = 0 } });
                var e = Build(defs, out _, out var me);
                Assert.AreNotEqual(a.Value.BlockDefsHash, e.Value.BlockDefsHash, "module rows are hashed");
                e.Dispose(); me.Dispose();
            }
            finally { a.Dispose(); ma.Dispose(); b.Dispose(); mb.Dispose(); }
        }

        [Test]
        public void BlockDefsHash_IgnoresPropMesh()
        {
            var defs = Defs(out _, out _);
            var a = Build(defs, out _, out var ma);
            Edit(defs.First(d => d.IdString == "block_capacitor_small"), so => Set(so, "propMesh", TestContentFactory.Mesh()));
            var b = Build(defs, out _, out var mb);
            try { Assert.AreEqual(a.Value.BlockDefsHash, b.Value.BlockDefsHash); }
            finally { a.Dispose(); ma.Dispose(); b.Dispose(); mb.Dispose(); }
        }

        [Test]
        public void MirrorIndexAndOffset_Computed()
        {
            var blob = Build(Defs(out var left, out var right), out var catalog, out var modules);
            try
            {
                ushort li = catalog.GetBlockIndex(left), ri = catalog.GetBlockIndex(right), cap = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new Game.Core.ContentId("block_capacitor_small")));
                Assert.AreEqual(ri, blob.Value.Blocks[li].MirrorIndex); Assert.AreEqual(li, blob.Value.Blocks[ri].MirrorIndex);
                Assert.AreEqual(cap, blob.Value.Blocks[cap].MirrorIndex, "achiral: itself");
                Assert.AreEqual(new int3(1, 0, 0), blob.Value.Blocks[cap].MirrorOffset);
                Assert.AreEqual(new int3(1, 0, 0), blob.Value.Blocks[li].MirrorOffset);
            }
            finally { blob.Dispose(); modules.Dispose(); }
        }

        [Test]
        public void PropMeshTable_DeduplicatesByReference()
        {
            var shared = TestContentFactory.Mesh();
            var meshes = new List<Mesh>();
            var blob = Build(Defs(out _, out _, shared), out var catalog, out var modules, meshes);
            try
            {
                Assert.AreEqual(1 + 2, meshes.Count, "the shared mesh once, plus the two chiral props' own meshes");
                Assert.AreSame(shared, meshes[0]);
                for (ushort k = 1; k <= catalog.BlockCount; k++)
                {
                    var def = catalog.GetBlock(k);
                    int mi = blob.Value.Blocks[k].PropMeshIndex;
                    if (def.PropMesh == null) Assert.AreEqual(-1, mi); else Assert.AreSame(def.PropMesh, meshes[mi]);
                }
            }
            finally { blob.Dispose(); modules.Dispose(); }
        }
    }
}
