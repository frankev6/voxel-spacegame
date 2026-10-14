using System.Collections;
using Game.App.Boot;
using Game.App.Session;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Persistence.Services;
using Game.Tests.Common;
using Game.Tests.PlayMode.UI;
using Game.UI.Data;
using Game.UI.Services;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.App
{
    /// <summary>FND-015: one real session/save/load/menu case; defeat delay itself is asserted with direct unscaled ticks.</summary>
    public sealed class DefeatTests : GameFlowFixture
    {
        [UnityTest] public IEnumerator PlayerDeath_DialogAndLoad()
        {
            yield return NewGame(42); var session = Service<GameSession>(); var save = Service<SaveService>(); var ui = Service<UIRoot>();
            var saved = session.SaveGameAsync("manual_001", SaveReason.Manual).GetAwaiter();
            yield return WaitUntil(() => saved.IsCompleted, 10, "Real defeat fixture save completes"); Assert.AreEqual(FlowResult.Ok, saved.GetResult());
            string name = session.CommanderName;
            var watcher = GameRoot.Instance.Composition.Defeat;
            Assert.IsNotNull(watcher, "Production composition owns defeat handling.");
            EventBus<ShipDestroyed>.Publish(new ShipDestroyed(999, default, default, 0, 0, 0, 0, 0, 0, 0)); EventBus<ShipDestroyed>.Drain(); Assert.IsFalse(session.PlayerDefeated);
            EventBus<ShipDestroyed>.Publish(new ShipDestroyed(session.Fleet.Flagship.PersistentId, default, default, 0, 0, 1, 0, 0, 0, 0)); EventBus<ShipDestroyed>.Drain();
            Assert.AreEqual(SaveBlockReason.PlayerDead, session.GetSaveBlock());
            watcher.Tick(2); Assert.IsFalse(ui.Confirm.IsOpen, "No early defeat dialog."); watcher.Tick(1); Assert.IsTrue(ui.Confirm.IsOpen, "Production delay is three unscaled seconds.");
            yield return WaitUntil(() => ui.GetLayer(UILayer.Modals).Q<Label>("confirm-title").text == "Ship destroyed", 2, "Defeat title binding");
            var load = ui.GetLayer(UILayer.Modals).Q<Button>("defeat-load-last-save"); Assert.IsNotNull(load); Assert.IsTrue(load.enabledInHierarchy);
            DialogTestContext.Activate(load);
            yield return WaitUntil(() => { watcher.Tick(0); return session.CurrentScene == GameScene.World && !session.IsTransitioning && !session.PlayerDefeated && PlayerReady; }, 10, "Actual last save restores the player");
            Assert.AreEqual(name, session.CommanderName); Assert.IsFalse(ui.Confirm.IsOpen); Assert.AreNotEqual(SaveBlockReason.PlayerDead, session.GetSaveBlock());
            // A second real defeat with no readable save exercises the disabled choice and unsaved menu path.
            EventBus<SessionReset>.Drain(); EventBus<SceneTransitionStarted>.Drain();
            EventBus<ShipDestroyed>.Publish(new ShipDestroyed(session.Fleet.Flagship.PersistentId, default, default, 0, 0, 1, 0, 0, 0, 0)); EventBus<ShipDestroyed>.Drain();
            foreach (var slot in save.ListSlots(save.ActiveProfile)) Assert.IsTrue(save.DeleteSlot(slot.Ref));
            watcher.Tick(3); Assert.IsTrue(ui.Confirm.IsOpen);
            yield return null;
            Assert.IsFalse(ui.GetLayer(UILayer.Modals).Q<Button>("defeat-load-last-save").enabledInHierarchy);
            var menu = ui.GetLayer(UILayer.Modals).Q<Button>("defeat-main-menu"); Assert.IsNotNull(menu); DialogTestContext.Activate(menu);
            yield return WaitUntil(() => { watcher.Tick(0); return session.CurrentScene == GameScene.MainMenu && !session.IsTransitioning; }, 10, "Defeat menu choice skips save and reaches menu");
            Assert.IsFalse(session.HasActiveSession); Assert.IsFalse(session.PlayerDefeated);
        }
    }
}
