using System;
using System.Collections.Generic;
using Game.App.Session;
using Game.Core;
using Game.Core.Save;
using Game.Core.Flow;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.App
{
    /// <summary>FND-010 ownership/events, FLET bytes and recoverable structure failures (SPEC-01 §4.3, §5.10).</summary>
    public sealed class FleetTests
    {
        sealed class Ids : IPersistentIdAllocator { public ulong Next = 1000; public ulong Allocate() => Next++; }
        sealed class Fixture : IDisposable
        {
            public readonly ContentService Content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            public readonly FleetModel Fleet = new FleetModel();
            public readonly Ids Ids = new Ids();
            public readonly List<string> Warnings = new List<string>();
            public readonly BlueprintLibrary Blueprints;
            public readonly GameConfigDefinition Config;
            public readonly FleetSaveParticipant Save;
            public Fixture()
            {
                Blueprints = new BlueprintLibrary(Content.Catalog, new VoxelContent { Blocks = Content.BlockDefs, Orientations = Content.Orientations });
                Config = Content.Catalog.Get<GameConfigDefinition>(new ContentId("config_game"));
                Save = new FleetSaveParticipant(Fleet, Content.Catalog, Blueprints, Config, Ids, Warnings.Add);
            }
            public FleetShipRecord Ship(ulong id)
            {
                var hull = Content.Catalog.Get<BlockDefinition>(new ContentId("block_hull"));
                return new FleetShipRecord { PersistentId = id, SourceBlueprint = Config.StartBlueprint.Id, Name = "Ship " + id,
                    Structure = BenchmarkStructures.Ship20k(Content.Catalog.GetBlockIndex(hull)) };
            }
            public byte[] Capture() { using var writer = new SaveWriter(); Save.Capture(writer); return writer.ToArray(b => b.Encode()); }
            public void Dispose() { Fleet.Dispose(); Blueprints.Dispose(); Content.Dispose(); }
        }

        [Test]
        public void Changed_OncePerMutation()
        {
            using var f = new Fixture(); int calls = 0; uint observed = 0;
            f.Fleet.Changed += () => { calls++; Assert.Greater(f.Fleet.Revision, observed); observed = f.Fleet.Revision; };
            f.Fleet.Add(f.Ship(2), true); Assert.AreEqual(1, calls);
            f.Fleet.Add(f.Ship(1), false); Assert.AreEqual(2, calls);
            var replacement = f.Ship(3);
            f.Fleet.ReplaceStructure(2, replacement.Structure); replacement.Structure = null; replacement.Dispose();
            Assert.AreEqual(3, calls);
            Assert.IsTrue(f.Fleet.Remove(1)); Assert.AreEqual(4, calls);
            Assert.IsFalse(f.Fleet.Remove(2)); Assert.AreEqual(4, calls);
            f.Fleet.ResetToEmpty(); Assert.AreEqual(5, calls); Assert.AreEqual(0, f.Fleet.Ships.Count); Assert.IsNull(f.Fleet.Flagship);
        }

        [Test]
        public void FlagshipRules()
        {
            using var f = new Fixture(); var first = f.Ship(20); var other = f.Ship(10);
            f.Fleet.Add(first, false); Assert.AreSame(first, f.Fleet.Flagship);
            f.Fleet.Add(other, true); Assert.AreSame(other, f.Fleet.Flagship);
            Assert.AreSame(other, f.Fleet.Ships[0]); Assert.AreSame(first, f.Fleet.Ships[1]);
            Assert.IsFalse(f.Fleet.Remove(10)); Assert.IsFalse(f.Fleet.Remove(99));
            using var duplicate = f.Ship(10);
            Assert.Throws<InvalidOperationException>(() => f.Fleet.Add(duplicate, false));
            Assert.IsTrue(duplicate.Structure.Data.IsCreated, "A refused Add does not take ownership");
            var registry = VoxelStructureRegistry.CreateRuntime(f.Blueprints);
            try
            {
                int handle = other.Register(registry);
                Assert.Throws<InvalidOperationException>(() => f.Fleet.ReplaceStructure(10, duplicate.Structure));
                other.Release(); Assert.IsNull(registry.Get(handle)); Assert.AreEqual(-1, other.StructureHandle);
            }
            finally { UnityEngine.Object.DestroyImmediate(registry); }
            var owned = first.Structure;
            Assert.IsTrue(f.Fleet.Remove(20)); Assert.IsFalse(owned.Data.IsCreated);
        }

        [Test]
        public void FLET_RoundTrip()
        {
            using var f = new Fixture(); f.Fleet.Add(f.Ship(20), true); f.Fleet.Add(f.Ship(10), false);
            byte[] first = f.Capture(); var sameModel = f.Fleet;
            f.Save.Restore(new SaveReader(first), 1);
            Assert.AreSame(sameModel, f.Fleet); Assert.AreEqual(20ul, f.Fleet.Flagship.PersistentId);
            Assert.AreEqual(10ul, f.Fleet.Ships[0].PersistentId);
            CollectionAssert.AreEqual(first, f.Capture()); Assert.AreEqual(0, f.Warnings.Count);
        }

        [Test]
        public void CorruptStructure_Fallback()
        {
            using var f = new Fixture();
            foreach (var blueprint in new[] { f.Config.StartBlueprint.Id, new ContentId("ship_not_installed") })
            {
                using var writer = new SaveWriter(); writer.WriteVarUInt32(1); writer.WriteUInt64(71);
                writer.WriteContentId(blueprint); writer.WriteString("Flagship"); writer.WriteBool(true);
                writer.WriteByteArray(new byte[] { 1, 2, 3 });
                f.Save.Restore(new SaveReader(writer.ToArray()), 1);
                Assert.AreEqual(71ul, f.Fleet.Flagship.PersistentId);
                Assert.AreEqual(f.Config.StartBlueprint.CellCount, f.Fleet.Flagship.Structure.SolidCellCount);
                Assert.AreEqual(f.Config.StartBlueprint.Id, f.Fleet.Flagship.SourceBlueprint);
                Assert.IsFalse(f.Fleet.Flagship.Structure.IsReadOnly);
            }
            Assert.GreaterOrEqual(f.Warnings.Count, 3);
        }

        [Test]
        public void MissingOrEmptyFleet_RestoresStartShipWithNewId()
        {
            using var f = new Fixture(); f.Save.Restore(SaveReader.Empty, 0);
            Assert.AreEqual(1000ul, f.Fleet.Flagship.PersistentId);
            using var writer = new SaveWriter(); writer.WriteVarUInt32(0);
            f.Save.Restore(new SaveReader(writer.ToArray()), 1);
            Assert.AreEqual(1001ul, f.Fleet.Flagship.PersistentId);
            Assert.AreEqual(1, f.Fleet.Ships.Count);
        }

        [Test]
        public void MalformedRecords_RejectAndReleaseStagedStructures()
        {
            using var f = new Fixture(); f.Fleet.Add(f.Ship(10), true);
            byte[] valid = f.Capture(); Array.Resize(ref valid, valid.Length - 1);
            Assert.Throws<SaveFormatException>(() => f.Save.Restore(new SaveReader(valid), 1));
            Assert.AreEqual(0, f.Fleet.Ships.Count);
            using var invalidCount = new SaveWriter(); invalidCount.WriteVarUInt32(uint.MaxValue);
            Assert.Throws<SaveFormatException>(() => f.Save.Restore(new SaveReader(invalidCount.ToArray()), 1));
            f.Save.Restore(SaveReader.Empty, 0); Assert.IsNotNull(f.Fleet.Flagship);
        }

        [TestCase("missing")]
        [TestCase("damaged")]
        [TestCase("low")]
        public void RestoredFleet_RepairsSessionCounter(string sessionState)
        {
            Game.Core.Events.EventHub.Reset();
            using var f = new Fixture();
            var registry = VoxelStructureRegistry.CreateRuntime(f.Blueprints);
            try
            {
                using var s = new SessionFixture(); s.Begin(); s.SetScene(GameScene.World);
                s.Session.BindFleet(f.Blueprints, registry, f.Content.Catalog); s.Save.Register(s.Session.FleetParticipant);
                s.Session.Fleet.Add(f.Ship(71), true);
                var slot = new SlotRef(s.Save.ActiveProfile, new SlotId("manual_001"));
                Assert.IsTrue(s.Save.WriteCaptured(s.Save.CaptureNow(slot, SaveReason.Manual, null)).Success);
                var saved = s.Save.ReadNow(slot).Save;
                var chunks = new List<RawChunk>(saved.Chunks);
                if (sessionState == "missing") chunks.RemoveAll(c => c.Key == "SESS");
                if (sessionState == "damaged") chunks.Find(c => c.Key == "SESS").Payload = new byte[] { 255 };
                var changed = new LoadedSave(saved.Slot, saved.Header, saved.Meta, saved.FromBackup, chunks,
                    saved.Report, saved.Palette, saved.StagedThumbnails);
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = sessionState == "damaged";
                var report = s.Save.Restore(changed);
                Assert.AreEqual(71ul, s.Session.Fleet.Flagship.PersistentId);
                Assert.AreEqual(72ul, s.Session.Allocate(), "Restored identities cannot be issued again even when SESS was reset");
                Assert.IsTrue(System.Linq.Enumerable.Any(report.Entries, e => e.Code == "FND-W002"));
                var keep = new HashSet<string>();
                s.Session.CollectThumbnails(new List<ThumbnailRequest>(), keep);
                CollectionAssert.AreEquivalent(new[] { "ship_71" }, keep);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
                UnityEngine.Object.DestroyImmediate(registry);
                Game.Core.Events.EventHub.Reset();
            }
        }
    }
}
