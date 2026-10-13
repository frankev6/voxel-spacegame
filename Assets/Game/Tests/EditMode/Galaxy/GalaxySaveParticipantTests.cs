using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Tests.EditMode.Persistence;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class GalaxySaveParticipantTests
    {
        const string FixturePath = "Assets/Game/Tests/EditMode/Galaxy/Fixtures/galaxy_v1.sav.bytes";
        sealed class Fixture : IDisposable
        {
            internal readonly ContentService Content = ContentService.CreateForTests(new ContentDefinition[] {
                AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"),
                AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_hull.asset") });
            internal readonly GalaxyState State = new();
            internal readonly GalaxySaveParticipant Participant;
            internal Fixture() => Participant = new GalaxySaveParticipant(State, Content.Catalog);
            internal VoxelStructure Structure()
            {
                var value = new VoxelStructure();
                ushort block = Content.Catalog.GetBlockIndex(Content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                ushort palette = value.FindOrAddPalette(new PaletteEntry { Block = block });
                value.SetCell(int3.zero, Cell.Palette(palette)); return value;
            }
            internal byte[] Capture(Func<ISaveDeferredBlock, byte[]> encoder = null)
            {
                using var writer = new SaveWriter();
                try { Participant.Capture(writer); return writer.ToArray(encoder ?? (block => block.Encode())); }
                finally { writer.DisposeDeferredBlocks(); }
            }
            public void Dispose() { State.Dispose(); Content.Dispose(); }
        }
        static void Fill(GalaxyState state, int count)
        {
            state.Seed = 42; state.GeneratorVersion = 1; state.RouteDestination = new StarSystemId(9);
            for (int i = count; i > 0; i--)
            {
                var id = new StarSystemId((uint)i); state.AddKnown(id); state.AddVisited(id);
                var delta = state.GetOrCreateDelta(id); delta.IncrementVisit();
                delta.MarkDestroyed(20); delta.MarkDestroyed(10); delta.MarkInteracted(30);
                delta.SetReplacement(40, new ContentId("body_removed_fixture"));
                delta.AddStorySpawn(new StorySpawnRecord { Template = new ContentId("body_unknown_fixture"),
                    AnchorBodyKey = 10, OffsetM = new double3(3, 0, -2), YawRad = .25f });
                delta.AddStorySpawn(new StorySpawnRecord { Template = new ContentId("body_unknown_fixture"), Removed = true });
            }
        }
        [Test] public void RoundTrip_ByteIdentical()
        {
            using var f = new Fixture(); Fill(f.State, 50);
            for (uint i = 1; i <= 10; i++) f.State.GetOrCreateDelta(new StarSystemId(i)).SetStructure(50, i, f.Structure());
            byte[] bytes = f.Capture(); f.Participant.Restore(new SaveReader(bytes), 1);
            CollectionAssert.AreEqual(bytes, f.Capture()); Assert.AreEqual(50, f.State.DeltasSorted().Count());
            Assert.AreEqual(10, f.State.DeltasSorted().Sum(d => d.StructureDiffs.Count));
            Assert.IsTrue(f.State.GetOrCreateDelta(new StarSystemId(1)).StorySpawns[1].Removed);
            Assert.AreEqual("body_removed_fixture", f.State.GetOrCreateDelta(new StarSystemId(1)).Replacements[0].Template.Value);
        }
        [Test] public void Restore_ReplacesState()
        {
            using var f = new Fixture(); Fill(f.State, 1); byte[] bytes = f.Capture();
            var old = f.Structure(); f.State.GetOrCreateDelta(new StarSystemId(99)).SetStructure(1, 1, old);
            f.State.AddKnown(new StarSystemId(99)); f.State.HasPlayerLocation = true;
            f.Participant.Restore(new SaveReader(bytes), 1);
            Assert.IsFalse(old.Data.IsCreated); Assert.IsFalse(f.State.TryGetDelta(new StarSystemId(99), out _));
            Assert.IsFalse(f.State.IsKnown(new StarSystemId(99))); Assert.IsFalse(f.State.HasPlayerLocation);
            CollectionAssert.AreEqual(bytes, f.Capture());
            f.Participant.Restore(SaveReader.Empty, 0); Assert.IsEmpty(f.State.Known); Assert.IsEmpty(f.State.DeltasSorted());
        }
        [Test] public void CorruptStructure_DropsRecord()
        {
            using var f = new Fixture(); Fill(f.State, 1); var delta = f.State.GetOrCreateDelta(new StarSystemId(1));
            delta.SetStructure(50, 7, f.Structure()); delta.SetStructure(60, 8, f.Structure()); int encoded = 0;
            byte[] bytes = f.Capture(block => ++encoded == 1 ? new byte[] { 0, 1, 2 } : block.Encode());
            LogAssert.Expect(LogType.Warning, new Regex("GLXY structure 1/50 dropped:"));
            f.Participant.Restore(new SaveReader(bytes), 1); delta = f.State.GetOrCreateDelta(new StarSystemId(1));
            Assert.IsFalse(delta.TryGetStructure(50, out _)); Assert.IsTrue(delta.TryGetStructure(60, out var surviving));
            Assert.AreEqual(8, surviving.Revision); Assert.AreEqual(1, surviving.Structure.SolidCellCount);
            Assert.AreEqual(2, delta.Destroyed.Count); LogAssert.NoUnexpectedReceived();
        }
        [Test] public void Restore_FromV1_Fixture()
        {
            Assert.IsTrue(File.Exists(FixturePath), "Create the immutable fixture with WriteV1Fixture before acceptance.");
            using var f = new Fixture(); var reader = SaveFixtureHarness.OpenChunk(FixturePath, "GLXY", out int version);
            Assert.AreEqual(1, version); f.Participant.Restore(reader, version);
            Assert.AreEqual(42ul, f.State.Seed); Assert.AreEqual(1, f.State.GeneratorVersion);
            Assert.AreEqual(new StarSystemId(9), f.State.RouteDestination); Assert.AreEqual(2, f.State.Known.Count);
            var delta = f.State.GetOrCreateDelta(new StarSystemId(1)); Assert.AreEqual(1, delta.VisitCount);
            Assert.AreEqual("body_unknown_fixture", delta.StorySpawns[0].Template.Value); Assert.IsTrue(delta.StorySpawns[1].Removed);
        }
        // Fixture authoring entry point; CreateNew in the harness prevents replacing a released fixture.
        public static void WriteV1Fixture()
        {
            using var f = new Fixture(); Fill(f.State, 2);
            SaveFixtureHarness.WriteFixture(FixturePath, "GLXY", 1, f.Participant.Capture);
        }
    }
}
