using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Galaxy.Services;
using NUnit.Framework;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class PlocTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() => EventHub.Reset();
        [Test] public void Docked_Restored()
        {
            var docking = new DockingService();
            using var fixture = new StarSystemLoadTests.Fixture(true, docking: docking);
            var body = fixture.Station.Id;
            docking.Dock(body); EventBus<DockStateChanged>.Drain();
            var participant = new PlayerLocationSaveParticipant(fixture.State, docking);
            using var writer = new SaveWriter(); participant.Capture(writer); byte[] saved = writer.ToArray();
            docking.Undock(); EventBus<DockStateChanged>.Drain();
            int notifications = 0;
            EventBus<DockStateChanged>.Subscribe(value => { Assert.IsTrue(value.Docked); notifications++; });
            participant.Restore(new SaveReader(saved), 1);
            fixture.Load(); fixture.Stream(0); EventBus<DockStateChanged>.Drain();
            Assert.IsTrue(docking.IsDocked, "Loader teardown must preserve pending restored docking");
            Assert.AreEqual(body, docking.DockedBody); Assert.AreEqual(0, notifications);
            EventBus<SceneTransitionFinished>.Publish(new SceneTransitionFinished(GameScene.World, SceneEntryReason.LoadGame, false));
            EventBus<SceneTransitionFinished>.Drain(); EventBus<DockStateChanged>.Drain(); Assert.AreEqual(0, notifications);
            for (int i = 0; i < 2; i++)
            {
                EventBus<SceneTransitionFinished>.Publish(new SceneTransitionFinished(GameScene.World, SceneEntryReason.LoadGame, true));
                EventBus<SceneTransitionFinished>.Drain(); EventBus<DockStateChanged>.Drain();
            }
            Assert.AreEqual(1, notifications); Assert.IsFalse(docking.HasPendingRestore);
            using var after = new SaveWriter(); participant.Capture(after); CollectionAssert.AreEqual(saved, after.ToArray());
            participant.Restore(new SaveReader(saved), 1);
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Cleared)); EventBus<SessionReset>.Drain();
            Assert.IsFalse(docking.IsDocked); Assert.IsFalse(docking.HasPendingRestore);
        }
    }
}
