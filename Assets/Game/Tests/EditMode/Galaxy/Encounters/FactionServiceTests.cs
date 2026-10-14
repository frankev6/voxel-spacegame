using System;
using Game.Core;
using Game.Core.Save;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Fixture = Game.Tests.EditMode.Simulation.Factions.FactionStandingTests.Fixture;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    public sealed class FactionServiceTests : GameTestBase
    {
        [TearDown] public void CleanupContent() => TestContentFactory.DestroyAll();
        static byte[] Capture(FactionSaveParticipant participant)
        { using var writer = new SaveWriter(); participant.Capture(writer); return writer.ToArray(); }
        [Test] public void GetRelation_IncludesQueued()
        {
            using var f = new Fixture(); using var service = new FactionService(f.World.World, f.Catalog);
            service.AdjustRelation(f.Neutral, 90); service.AdjustRelation(f.Neutral, 90); service.AdjustRelation(f.Neutral, -70);
            Assert.AreEqual(30, service.GetRelation(f.Neutral), "Clamp every request like the fixed-step owner, not only the final sum");
            service.SetRelation(f.Neutral, -30); Assert.AreEqual(FactionStance.Hostile, service.GetPlayerStance(f.Neutral));
            Assert.AreEqual(FactionStance.Neutral, service.GetStance(0, f.Index), "Matrix changes at the next fixed tick");
            f.Tick(1); Assert.AreEqual(-30, service.GetRelation(f.Neutral)); Assert.AreEqual(FactionStance.Hostile, service.GetStance(0, f.Index));
        }
        [Test] public void Facn_RoundTrip()
        {
            using var f = new Fixture(); using var service = new FactionService(f.World.World, f.Catalog);
            var save = new FactionSaveParticipant(service, f.Catalog); service.SetRelation(f.Neutral, 67);
            byte[] before = Capture(save); service.InitializeNewGame(default); save.Restore(new SaveReader(before), 1);
            CollectionAssert.AreEqual(before, Capture(save)); Assert.AreEqual(67, service.GetRelation(f.Neutral));
        }
        [Test] public void Facn_UnknownPreserved()
        {
            using var f = new Fixture(); using var service = new FactionService(f.World.World, f.Catalog);
            var save = new FactionSaveParticipant(service, f.Catalog);
            using var writer = new SaveWriter(); writer.WriteVarUInt32(3);
            writer.WriteContentId(new ContentId("faction_locked")); writer.WriteInt32(-100);
            writer.WriteContentId(new ContentId("faction_missing")); writer.WriteInt32(37);
            writer.WriteContentId(new ContentId("faction_neutral")); writer.WriteInt32(42);
            byte[] before = writer.ToArray(); save.Restore(new SaveReader(before), 1);
            CollectionAssert.AreEqual(before, Capture(save));
            save.Restore(new SaveReader(Capture(save)), 1); CollectionAssert.AreEqual(before, Capture(save));
            service.InitializeNewGame(default); Assert.AreEqual(2u, new SaveReader(Capture(save)).ReadVarUInt32());
        }
        [Test] public void Restore_RebuildsMatrix()
        {
            using var f = new Fixture(); using var service = new FactionService(f.World.World, f.Catalog);
            var save = new FactionSaveParticipant(service, f.Catalog); service.SetRelation(f.Neutral, 80); f.Tick(1);
            byte[] friendly = Capture(save); service.SetRelation(f.Neutral, -100); f.Tick(2);
            Assert.AreEqual(FactionStance.Hostile, service.GetStance(0, f.Index));
            service.AdjustRelation(f.Neutral, -10); save.Restore(new SaveReader(friendly), 1);
            Assert.AreEqual(80, service.GetRelation(f.Neutral)); Assert.AreEqual(FactionStance.Friendly, service.GetStance(0, f.Index));
            Assert.AreEqual(FactionStance.Friendly, service.GetStance(f.Index, 0));
            using var empty = new SaveWriter(); empty.WriteVarUInt32(0); save.Restore(new SaveReader(empty.ToArray()), 1);
            Assert.AreEqual(0, service.GetRelation(f.Neutral), "Absent factions reset to base instead of retaining prior world state");
        }
        [Test] public void NewGame_BaseRelations()
        {
            using var f = new Fixture(); using var service = new FactionService(f.World.World, f.Catalog);
            service.SetRelation(f.Neutral, -40); f.Tick(1); service.AdjustRelation(f.Neutral, -20);
            service.InitializeNewGame(default);
            Assert.AreEqual(0, service.GetRelation(f.Neutral)); Assert.AreEqual(-100, service.GetRelation(f.Locked)); Assert.AreEqual(100, service.GetRelation(f.Ark));
            Assert.AreEqual(FactionStance.Neutral, service.GetStance(0, f.Index));
            var save = new FactionSaveParticipant(service, f.Catalog); service.SetRelation(f.Neutral, 90); save.Restore(SaveReader.Empty, 0);
            Assert.AreEqual(0, service.GetRelation(f.Neutral));
        }
        [Test] public void InvalidPayload_DoesNotMutate()
        {
            using var f = new Fixture(); using var service = new FactionService(f.World.World, f.Catalog);
            var save = new FactionSaveParticipant(service, f.Catalog); service.SetRelation(f.Neutral, 17); byte[] before = Capture(save);
            Assert.Throws<SaveFormatException>(() => save.Restore(new SaveReader(before.AsMemory(0, before.Length - 1)), 1));
            CollectionAssert.AreEqual(before, Capture(save));
            using var duplicate = new SaveWriter(); duplicate.WriteVarUInt32(2);
            for (int i = 0; i < 2; i++) { duplicate.WriteContentId(f.Neutral.Id); duplicate.WriteInt32(-30); }
            Assert.Throws<SaveFormatException>(() => save.Restore(new SaveReader(duplicate.ToArray()), 1));
            CollectionAssert.AreEqual(before, Capture(save));
        }
    }
}
