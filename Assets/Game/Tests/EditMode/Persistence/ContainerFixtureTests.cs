using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-010: permanent on-disk compatibility fixtures.</summary>
    public sealed class ContainerFixtureTests
    {
        internal sealed class FixtureParticipant : ISaveParticipant
        {
            public string Key { get; }
            public int Version => 1;
            readonly ContentCatalog catalog;
            public int Value;
            public ContentId ItemId, BlockId;
            public string Text;
            public ItemDefinition Item;
            public BlockDefinition Block;
            public FixtureParticipant(string key, ContentCatalog catalog = null) { Key = key; this.catalog = catalog; }
            public void Capture(SaveWriter w)
            {
                if (Key == "TSTA") { w.WriteInt32(Value); w.WriteContentId(ItemId); w.WriteContentId(BlockId); }
                else w.WriteString(Text);
            }
            public void Restore(SaveReader r, int version)
            {
                if (version == 0) { Value = 0; Text = null; return; }
                if (Key == "TSTA")
                {
                    Value = r.ReadInt32(); ItemId = r.ReadContentId(); BlockId = r.ReadContentId();
                    if (catalog != null) { Item = catalog.ResolveItemOrMissing(ItemId, Key); Block = catalog.ResolveBlockOrMissing(BlockId, Key); }
                }
                else Text = r.ReadString();
            }
        }
        internal static SaveChunkRegistry Registry() => new SaveChunkRegistry(new[] {
            new ChunkRegistration("TSTA",1000,"test","a"),new ChunkRegistration("TSTB",1010,"test","b") });
        internal static SlotRef Slot => new SlotRef(new ProfileId("p001"),new SlotId("manual_001"));
        internal static string Install(string root, string kind)
        {
            var path = new SavePaths(root).WorldFile(Slot); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(SaveFixtureHarness.PathFor(kind),path); return path;
        }
        string root;
        [SetUp] public void SetUp() { EventHub.Reset();root=Path.Combine(Path.GetTempPath(),"ContainerFixtures_"+Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { EventHub.Reset();if(Directory.Exists(root))Directory.Delete(root,true); }
        [Test] public void Minimal_Loads()
        {
            Install(root,"minimal"); using var service=SaveFixtureHarness.CreateService(root,null,Registry());
            var a=new FixtureParticipant("TSTA");var b=new FixtureParticipant("TSTB");service.Register(a);service.Register(b);
            var loaded=service.ReadNow(Slot);Assert.IsTrue(loaded.Success,loaded.Message);var report=service.Restore(loaded.Save);
            Assert.AreEqual(0,report.ErrorCount);Assert.AreEqual(0,report.WarningCount);
            Assert.AreEqual(17,a.Value);Assert.AreEqual("item_missing",a.ItemId.Value);Assert.AreEqual("block_missing",a.BlockId.Value);Assert.AreEqual("container fixture",b.Text);
            var reader=SaveFixtureHarness.OpenChunk(SaveFixtureHarness.PathFor("minimal"),"TSTA",out int version);Assert.AreEqual(1,version);Assert.AreEqual(17,reader.ReadInt32());
            string custom=Path.Combine(root,"custom.sav.bytes");SaveFixtureHarness.WriteFixture(custom,"ABCD",3,w=>w.WriteInt32(123));
            Assert.AreEqual(123,SaveFixtureHarness.OpenChunk(custom,"ABCD",out version).ReadInt32());Assert.AreEqual(3,version);
            Assert.Throws<IOException>(()=>SaveFixtureHarness.WriteFixture(custom,"ABCD",4,w=>w.WriteInt32(0)),"never replace a committed fixture");
        }
        [Test] public void UnknownChunk_Preserved()
        {
            Install(root,"unknown_chunk"); using var service=SaveFixtureHarness.CreateService(root,null,Registry());
            service.Register(new FixtureParticipant("TSTA"));service.Register(new FixtureParticipant("TSTB"));
            var loaded=service.ReadNow(Slot);Assert.IsTrue(loaded.Success);var report=service.Restore(loaded.Save);
            Assert.AreEqual(0,report.ErrorCount);Assert.AreEqual(0,report.WarningCount);CollectionAssert.Contains(report.Entries.Select(e=>e.Code),"PERS-I025");
            Assert.IsTrue(service.WriteCaptured(service.CaptureNow(Slot,SaveReason.Manual,null)).Success);
            var original=SaveFixtureHarness.ReadChunks(File.ReadAllBytes(SaveFixtureHarness.PathFor("unknown_chunk"))).Single(c=>c.Key=="ZZZZ");
            var written=SaveFixtureHarness.ReadChunks(File.ReadAllBytes(new SavePaths(root).WorldFile(Slot))).Single(c=>c.Key=="ZZZZ");
            Assert.AreEqual(original.Version,written.Version);CollectionAssert.AreEqual(original.Payload,written.Payload);
        }
        [Test] public void CurrentWriter_MatchesFixtures()
        {
            foreach(string kind in new[]{"minimal","unknown_chunk","missing_ids"})
            {
                byte[] committed=File.ReadAllBytes(SaveFixtureHarness.PathFor(kind));
                Assert.IsTrue(SaveFileComparer.EqualIgnoringVolatile(committed,SaveFixtureHarness.CurrentContainer(kind),out string difference),kind+": "+difference);
            }
        }
    }
}
