using System;
using System.IO;
using Game.App.Session;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Persistence.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    public sealed class SessionSaveParticipantTests
    {
        [SetUp] public void SetUp() => Game.Core.Events.EventHub.Reset();
        [TearDown] public void TearDown() => Game.Core.Events.EventHub.Reset();
        [Test]
        public void RoundTrip()
        {
            using var f = new SessionFixture(); f.Begin(creative: true); f.SetScene(GameScene.World);
            f.World.AddSingleton(new SimClock { UniversalTime = 135.25 });
            // The clock/writeback query requires both tags on the same entity, as SceneFlow creates them.
            using (var q = f.World.EntityManager.CreateEntityQuery(typeof(SimClock)))
                f.World.EntityManager.AddComponent<WorldSceneActive>(q.GetSingletonEntity());
            f.Session.OnSceneReady(new SceneReadyContext(GameScene.World, SceneEntryReason.NewGame, default));
            f.Session.Tick(12.25f); f.Session.GrantBlock(4096); f.Session.Allocate();
            var slot = new SlotRef(f.Save.ActiveProfile, new SlotId("manual_001"));
            var capture = f.Save.CaptureNow(slot, SaveReason.Manual, null);
            Assert.IsFalse(capture.Failure.HasValue); Assert.AreEqual(135.25, f.Session.UniversalTime, "PrepareCapture wrote back before SESS");
            Assert.IsTrue(f.Save.WriteCaptured(capture).Success);
            var participant = new SessionSaveParticipant(f.Session);
            using var before = new SaveWriter(); participant.Capture(before);
            var read = f.Save.ReadNow(slot); Assert.IsTrue(read.Success);
            f.Session.ApplySavedState("Changed", 1, 0, 0, 1, false);
            f.Session.RestoreFrom(read);
            using var after = new SaveWriter(); participant.Capture(after);
            CollectionAssert.AreEqual(before.ToArray(), after.ToArray());
            Assert.IsTrue(f.Session.HasActiveSession); Assert.AreEqual(12.25, f.Session.PlaytimeSeconds);
            Assert.AreEqual(4098ul, f.Session.NextPersistentId); Assert.IsTrue(f.Session.IsCreativeSession);
        }

        [Test]
        public void InvalidRecord_DoesNotPartiallyMutate()
        {
            using var f = new SessionFixture(); f.Begin(); var participant = new SessionSaveParticipant(f.Session);
            using var before = new SaveWriter(); participant.Capture(before);
            using var corrupt = new SaveWriter(); corrupt.WriteString("Corrupt"); corrupt.WriteUInt64(42);
            corrupt.WriteDouble(10); corrupt.WriteDouble(20); corrupt.WriteUInt64(0); corrupt.WriteBool(false);
            Assert.Throws<SaveFormatException>(() => participant.Restore(new SaveReader(corrupt.ToArray()), 1));
            var truncated = before.ToArray(); Array.Resize(ref truncated, truncated.Length - 1);
            Assert.Throws<SaveFormatException>(() => participant.Restore(new SaveReader(truncated), 1));
            using var after = new SaveWriter(); participant.Capture(after);
            CollectionAssert.AreEqual(before.ToArray(), after.ToArray()); Assert.IsTrue(f.Session.HasActiveSession);
        }

        [Test]
        public void MissingChunk_ResetsDefaultsAndGrants()
        {
            using var f = new SessionFixture(); f.Begin(); f.Session.GrantBlock(4096);
            var participant = new SessionSaveParticipant(f.Session); participant.Restore(SaveReader.Empty, 0);
            Assert.AreEqual(f.Config.DefaultCommanderName, f.Session.CommanderName);
            Assert.AreEqual(f.Config.DevQuickStartSeed, f.Session.CampaignSeed);
            Assert.AreEqual(1ul, f.Session.NextPersistentId); Assert.AreEqual(0, f.Session.UniversalTime);
            Assert.AreEqual(0, f.Session.PlaytimeSeconds); Assert.IsFalse(f.Session.IsCreativeSession);
            Assert.AreEqual(0ul, f.World.GetSingleton<PersistentIdAllocator>().End);
        }
    }
}
