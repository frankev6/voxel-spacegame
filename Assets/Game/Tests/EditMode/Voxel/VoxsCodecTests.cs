using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Core.Save;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>SPEC-05 PERS-007: exact layout, content drift, limits, and managed/Burst equivalence.</summary>
    public sealed class VoxsCodecTests
    {
        internal static ContentCatalog Catalog(bool hullInstance = false, bool multiInstance = true, bool hullMissing = false)
        {
            var defs = new List<ContentDefinition> {
                TestContentFactory.Item("item_missing"), TestContentFactory.Block("block_missing", BlockKind.Structural),
                TestContentFactory.Block("block_thruster_small", BlockKind.Functional),
                TestContentFactory.Block("block_multi", multiInstance ? BlockKind.Prop : BlockKind.Structural,
                    multiInstance ? new[] {Vector3Int.zero, Vector3Int.right} : null) };
            if (!hullMissing) defs.Add(TestContentFactory.Block("block_hull", hullInstance ? BlockKind.Functional : BlockKind.Structural));
            var r = new ValidationReport("VOXS tests"); var c = ContentCatalog.Build(defs,r);
            Assert.AreEqual(0,r.ErrorCount,r.ToText()); return c;
        }
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        static byte[] Encode(VoxsSource s, ContentCatalog c, bool deflate=false) => VoxsCodec.Encode(s,VoxsBlockNames.FromCatalog(c),new VoxsEncodeOptions {Deflate=deflate});
        static VoxsDecoded Decode(byte[] bytes, ContentCatalog c)
        {
            Assert.IsTrue(VoxsCodec.TryDecode(bytes,c,Allocator.Persistent,out var d,out string error),error); return d;
        }
        static void RoundTrip(VoxsSource s, ContentCatalog c, bool compressed=false)
        {
            byte[] a=Encode(s,c,compressed); using var d=Decode(a,c);
            CollectionAssert.AreEqual(a,Encode(VoxsTestStructures.View(d),c,compressed));
            Assert.IsTrue(VoxsCodec.TryPeek(a,out ushort version,out bool partial,out int bricks));
            Assert.AreEqual(1,version); Assert.AreEqual(s.Partial,partial); Assert.AreEqual(d.BrickCoords.Length,bricks);
        }
        [Test] public void RoundTrip_EdgeStructures()
        {
            var c=Catalog();
            using(var empty=new VoxsTestStructures(0,0)) RoundTrip(empty.Source,c);
            using(var s=new VoxsTestStructures(1))
            {
                s.Source.Palette[1]=new PaletteEntry {Block=VoxsTestStructures.Block(c,"block_hull")};
                s.Source.Cells[0]=Cell.Palette(1); RoundTrip(s.Source,c);
                for(int i=0;i<4096;i++) s.Source.Cells[i]=Cell.Palette(1);
                RoundTrip(s.Source,c);
                s.Source.Partial=true; s.Source.RemovedBricks.Dispose();
                s.Source.RemovedBricks=new NativeArray<int3>(new[] {new int3(-100,2,3),new int3(4,-2,-9)},Allocator.Persistent);
                RoundTrip(s.Source,c);
            }
            using var ship=VoxsTestStructures.ReferenceShip(c); RoundTrip(ship.Source,c);
        }
        [Test] public void RoundTrip_RandomStructures()
        {
            var c=Catalog();
            for(uint seed=1;seed<=200;seed++) { using var s=VoxsTestStructures.Random(c,seed); RoundTrip(s.Source,c); }
        }
        [Test] public void Decode_EqualsSource()
        {
            var c=Catalog(); using var s=VoxsTestStructures.Random(c,41); using var d=Decode(Encode(s.Source,c),c);
            CollectionAssert.AreEqual(s.Source.Palette.ToArray(),d.Palette.AsArray().ToArray());
            CollectionAssert.AreEqual(s.Source.Instances.ToArray(),d.Instances.AsArray().ToArray());
            Assert.AreEqual(s.Source.AnchorInstance,d.AnchorInstance); Assert.AreEqual(s.Source.CellSize,d.CellSize);
            int[] order=Enumerable.Range(0,s.Source.BrickCoords.Length).OrderBy(b=>s.Source.BrickCoords[b].z).ThenBy(b=>s.Source.BrickCoords[b].y).ThenBy(b=>s.Source.BrickCoords[b].x).ToArray();
            for(int b=0;b<order.Length;b++)
            {
                Assert.AreEqual(s.Source.BrickCoords[order[b]],d.BrickCoords[b]);
                CollectionAssert.AreEqual(s.Source.Cells.GetSubArray(order[b]*4096,4096).ToArray(),d.Cells.AsArray().GetSubArray(b*4096,4096).ToArray());
            }
        }
        static int BrickStart(byte[] bytes)
        {
            var r=new SaveReader(bytes); r.Skip(16);
            int blocks=(int)r.ReadVarUInt32(); for(int i=0;i<blocks;i++) r.Skip((int)r.ReadVarUInt32());
            int palette=(int)r.ReadVarUInt32(); for(int i=0;i<palette;i++) {r.ReadVarUInt32();r.Skip(2);}
            Assert.AreEqual(0,r.ReadVarUInt32()); Assert.AreEqual(1,r.ReadVarUInt32());
            r.ReadVarInt32();r.ReadVarInt32();r.ReadVarInt32();return r.Position;
        }
        [Test] public void BrickOrder_PicksShortest()
        {
            var c=Catalog(); using var s=new VoxsTestStructures(1); s.Source.Palette[1]=new PaletteEntry {Block=VoxsTestStructures.Block(c,"block_hull")};
            for(int axis=0;axis<4;axis++)
            {
                for(int i=0;i<4096;i++) s.Source.Cells[i]=default;
                for(int i=0;i<(axis==3?4096:16);i++) s.Source.Cells[axis==0?i:axis==1?i*16:axis==2?i*256:i]=Cell.Palette(1);
                byte[] bytes=Encode(s.Source,c); Assert.AreEqual(axis,bytes[BrickStart(bytes)]); RoundTrip(s.Source,c);
            }
        }
        [Test] public void UnknownBlock_BecomesMissing()
        {
            var old=Catalog(); using var s=new VoxsTestStructures(1); s.Source.Palette[1]=new PaletteEntry {Block=VoxsTestStructures.Block(old,"block_hull"),Orient=4,Color=12};s.Source.Cells[0]=Cell.Palette(1);
            byte[] bytes=Encode(s.Source,old);var changed=Catalog(hullMissing:true);using var d=Decode(bytes,changed);
            Assert.AreEqual(changed.GetBlockIndex(changed.MissingBlock),d.Palette[1].Block);
            Assert.AreEqual(4,d.Palette[1].Orient);Assert.AreEqual(12,d.Palette[1].Color);Assert.AreEqual(1,changed.Unresolved.Count);Assert.AreEqual(1,d.DriftWarningCount);
        }
        [Test] public void Drift_D2_D3_Compaction()
        {
            var old=Catalog();using var s=new VoxsTestStructures(1,2,3);
            s.Source.Palette[1]=new PaletteEntry {Block=VoxsTestStructures.Block(old,"block_hull")}; s.Source.Cells[0]=Cell.Palette(1);
            for(int i=0;i<3;i++)
            {
                s.Source.Instances[i]=new BlockInstance {Block=VoxsTestStructures.Block(old,i==1?"block_thruster_small":"block_multi"),FunctionHp=50};
                s.Source.Cells[i+1]=Cell.Instance(i);
            }
            s.Source.AnchorInstance=1;
            var changed=Catalog(hullInstance:true,multiInstance:false);
            using(var d=Decode(Encode(s.Source,old),changed))
            {
                Assert.AreEqual(changed.GetBlockIndex(changed.MissingBlock),d.Palette[1].Block);
                Assert.AreEqual(1,d.Instances.Length);Assert.AreEqual(0,d.AnchorInstance);Assert.AreEqual(0x8000,d.Cells[2].Ref);
                Assert.AreEqual(d.Cells[1].Ref,d.Cells[3].Ref);Assert.IsFalse(d.Cells[1].IsInstance);Assert.AreEqual(3,d.DriftWarningCount);
            }
            s.Source.AnchorInstance=0; using var removed=Decode(Encode(s.Source,old),changed); Assert.AreEqual(-1,removed.AnchorInstance);
        }
        [Test] public void Drift_FullPalette_UsesDeterministicFallback()
        {
            var old = Catalog();
            using var s = new VoxsTestStructures(1, 32768, 2);
            ushort hull = VoxsTestStructures.Block(old, "block_hull");
            for (int i = 1; i < s.Source.Palette.Length; i++)
                s.Source.Palette[i] = new PaletteEntry { Block = hull };
            for (int i = 0; i < 2; i++)
            {
                s.Source.Instances[i] = new BlockInstance { Block = VoxsTestStructures.Block(old, "block_multi"), Color = (byte)i };
                s.Source.Cells[i] = Cell.Instance(i);
            }
            s.Source.Cells[2] = Cell.Palette(32767);
            var changed = Catalog(multiInstance: false);
            using var d = Decode(Encode(s.Source, old), changed);
            Assert.AreEqual(32768, d.Palette.Length);
            Assert.AreEqual(0, d.Instances.Length);
            Assert.AreEqual(3, d.DriftWarningCount);
            Assert.AreEqual(changed.GetBlockIndex(changed.MissingBlock), d.Palette[32767].Block);
            for (int i = 0; i < 3; i++) Assert.AreEqual(32767, d.Cells[i].Ref);
        }
        internal static byte[] Crafted(Action<SaveWriter> write)
        {
            using var w=new SaveWriter();w.WriteUInt32(0x53584f56);w.WriteUInt16(1);w.WriteUInt16(0);w.WriteSingle(.5f);w.WriteInt32(-1);write(w);return w.ToArray();
        }
        static void Prefix(SaveWriter w)
        {
            w.WriteVarUInt32(1);w.WriteByteArray(Encoding.UTF8.GetBytes("block_hull"));
            w.WriteVarUInt32(2);w.WriteVarUInt32(0);w.WriteUInt16(0);w.WriteVarUInt32(1);w.WriteUInt16(0);w.WriteVarUInt32(0);
        }
        [Test] public void Limits_RejectMalformed()
        {
            var c=Catalog();var cases=new List<byte[]> {
                Crafted(w=>w.WriteVarUInt32(65536)),
                Crafted(w=>{w.WriteVarUInt32(1);w.WriteVarUInt32(257);w.WriteBytes(new byte[257]);}),
                Crafted(w=>{w.WriteVarUInt32(0);w.WriteVarUInt32(32769);}),
                Crafted(w=>{w.WriteVarUInt32(0);w.WriteVarUInt32(0);w.WriteVarUInt32(32769);}),
                Crafted(w=>{Prefix(w);w.WriteVarUInt32(1048577);}),
                Crafted(w=>{w.WriteVarUInt32(0);w.WriteVarUInt32(1);w.WriteVarUInt32(1);w.WriteUInt16(0);}),
                Crafted(w=>{w.WriteVarUInt32(0);w.WriteVarUInt32(1);w.WriteVarUInt32(0);w.WriteByte(24);w.WriteByte(0);}),
                Crafted(w=>{w.WriteVarUInt32(1);w.WriteVarUInt32(1);w.WriteByte(255);}),
            };
            foreach(ushort reference in new ushort[] {2,0x8000}) cases.Add(Crafted(w=>{Prefix(w);w.WriteVarUInt32(1);w.WriteBytes(new byte[3]);w.WriteByte(3);w.WriteUInt16(reference);w.WriteUInt16(0);w.WriteByte(0);}));
            cases.Add(Crafted(w=>{Prefix(w);w.WriteVarUInt32(1);w.WriteBytes(new byte[3]);w.WriteByte(0);w.WriteUInt16(1);w.WriteVarUInt32(4096);w.WriteUInt16(0);w.WriteByte(0);}));
            foreach(int field in new[]{1,2}) cases.Add(Crafted(w=>{Prefix(w);w.WriteVarUInt32(1);w.WriteBytes(new byte[3]);w.WriteByte(3);w.WriteUInt16(1);if(field==2)w.WriteByte(0);w.WriteByte(1);w.WriteByte(12);w.WriteVarUInt32(4096);w.WriteUInt16(0);}));
            cases.Add(Crafted(w=>{Prefix(w);w.WriteVarUInt32(2);for(int i=0;i<2;i++){w.WriteBytes(new byte[3]);w.WriteByte(3);w.WriteUInt16(1);w.WriteUInt16(0);}w.WriteByte(0);}));
            using var s=new VoxsTestStructures(1);s.Source.Palette[1]=new PaletteEntry {Block=VoxsTestStructures.Block(c,"block_hull")};s.Source.Cells[0]=Cell.Palette(1);
            byte[] valid=Encode(s.Source,c),compressed=Encode(s.Source,c,true);
            cases.Add(valid.Concat(new byte[]{0}).ToArray());cases.Add(compressed.Concat(new byte[]{0}).ToArray());
            for(int n=0;n<valid.Length;n++)cases.Add(valid.Take(n).ToArray());
            for(int n=0;n<compressed.Length;n++)cases.Add(compressed.Take(n).ToArray());
            foreach(int offset in new[]{0,4,6,BrickStart(valid)}) {byte[] bad=(byte[])valid.Clone();bad[offset]=255;cases.Add(bad);}
            foreach(byte[] bytes in cases)
            {
                bool ok=VoxsCodec.TryDecode(bytes,c,Allocator.Persistent,out var d,out string error);d?.Dispose();
                Assert.IsFalse(ok,BitConverter.ToString(bytes));StringAssert.StartsWith("VOXS offset ",error);
            }
        }
        [Test] public void Deflate_RoundTrip()
        {
            var c=Catalog();using var s=VoxsTestStructures.ReferenceShip(c);
            Assert.Less(Encode(s.Source,c,true).Length,Encode(s.Source,c).Length);RoundTrip(s.Source,c,true);
        }
        [Test] public void Encode_InvalidSource_Rejects()
        {
            var c=Catalog();using var s=new VoxsTestStructures(1);s.Source.Palette[1]=new PaletteEntry {Block=VoxsTestStructures.Block(c,"block_hull")};
            s.Source.Cells[0]=new Cell{Hp=1};Assert.Throws<ArgumentException>(()=>Encode(s.Source,c));
            s.Source.Cells[0]=Cell.Palette(9);Assert.Throws<ArgumentException>(()=>Encode(s.Source,c));
            s.Source.Cells[0]=Cell.Palette(1);s.Source.Palette[1]=new PaletteEntry{Block=65535};Assert.Throws<ArgumentException>(()=>Encode(s.Source,c));
        }
        [Test] public void BurstDisabled_IdenticalBytesAndNoLeaks()
        {
            var c=Catalog();using var s=VoxsTestStructures.Random(c,17);byte[] expected=Encode(s.Source,c);
            using(var warm=Decode(expected,c)){}
            var old=NativeLeakDetection.Mode;NativeLeakDetection.Mode=NativeLeakDetectionMode.EnabledWithStackTrace;
            UnsafeUtility.CheckForLeaks();bool enabled=BurstCompiler.Options.EnableBurstCompilation;
            try
            {
                BurstCompiler.Options.EnableBurstCompilation=false;CollectionAssert.AreEqual(expected,Encode(s.Source,c));
                var d=Decode(expected,c);d.Dispose();d.Dispose();
                Assert.AreEqual(0,UnsafeUtility.CheckForLeaks());
            }
            finally{BurstCompiler.Options.EnableBurstCompilation=enabled;NativeLeakDetection.Mode=old;}
        }
    }
}
