using System.Collections;
using Game.App.EditorHost;
using Game.App.Presenters;
using Game.App.Session;
using Game.Core;
using Game.Core.Flow;
using Game.Input;
using Game.Tests.Common;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>HUD-002's four production-root/lifetime cases, using real scene transitions.</summary>
    public sealed class UiCompositionTests : GameFlowFixture
    {
        IEnumerator Flow(Awaitable<FlowResult> request)
        {
            var operation = request.GetAwaiter();
            yield return WaitUntil(() => operation.IsCompleted, 10, "Root transition");
            Assert.AreEqual(FlowResult.Ok, operation.GetResult());
        }
        void Root<T>(InputContext context) where T : UIScreen
        {
            var ui = Service<UIRoot>();
            Assert.AreEqual(1, ui.Navigator.Count); Assert.IsInstanceOf<T>(ui.Navigator.Top);
            Assert.IsTrue(ui.Navigator.Top.IsOpen); Assert.AreEqual(context, Service<InputService>().CurrentContext);
            Assert.AreEqual(0, Service<IPauseService>().ActiveTokenCount);
        }
        [UnityTest] public IEnumerator BootEachScene_OneRootVisible()
        {
            Root<MainMenuView>(InputContext.Menu);
            yield return EnterSandbox(); Root<FlightHudView>(InputContext.Flight);
            var session = Service<GameSession>();
            yield return Flow(session.EnterBlueprintLabAsync(new BlueprintLabArgs(BlueprintLabMode.FleetShip, session.Fleet.Flagship.PersistentId, ContentId.None)));
            Root<ShipEditorView>(InputContext.Editor);
            Assert.IsNotNull(Object.FindAnyObjectByType<ShipEditorHost>());
            yield return Flow(session.ExitBlueprintLabAsync()); Root<FlightHudView>(InputContext.Flight);
        }
        [UnityTest] public IEnumerator SceneRoundTrip_NoPauseLeak_ContextMatchesRoot()
        {
            yield return EnterSandbox(); var session = Service<GameSession>();
            ulong id = session.Fleet.Flagship.PersistentId;
            yield return Flow(session.EnterBlueprintLabAsync(new BlueprintLabArgs(BlueprintLabMode.FleetShip, id, ContentId.None)));
            Root<ShipEditorView>(InputContext.Editor);
            yield return Flow(session.ExitBlueprintLabAsync());
            yield return WaitUntil(() => PlayerReady, 10, "Returned player"); Root<FlightHudView>(InputContext.Flight);
            Assert.AreEqual(id, session.Fleet.Flagship.PersistentId);
            yield return Flow(session.QuitToMainMenuAsync(false)); Root<MainMenuView>(InputContext.Menu);
        }
        [UnityTest] public IEnumerator SessionReset_ViewModelsKept()
        {
            var screens = Service<UiComposition>();
            var before = new object[] { screens.MainMenuModel, screens.HudModel, screens.ShipEditorModel, screens.InventoryModel, screens.FleetModel, screens.SettingsModel, screens.SaveSlotsModel };
            yield return EnterSandbox(); yield return Flow(Service<GameSession>().QuitToMainMenuAsync(false));
            yield return EnterSandbox();
            var after = new object[] { screens.MainMenuModel, screens.HudModel, screens.ShipEditorModel, screens.InventoryModel, screens.FleetModel, screens.SettingsModel, screens.SaveSlotsModel };
            for (int i = 0; i < before.Length; i++) Assert.AreSame(before[i], after[i]);
            Assert.IsTrue(screens.HudModel.HasShip);
        }
        [UnityTest] public IEnumerator EditorRoot_FlightHudHidden()
        {
            yield return EnterSandbox(); var session = Service<GameSession>(); var ui = Service<UIRoot>();
            yield return Flow(session.EnterBlueprintLabAsync(new BlueprintLabArgs(BlueprintLabMode.FleetShip, session.Fleet.Flagship.PersistentId, ContentId.None)));
            Assert.IsFalse(ui.Navigator.Get<FlightHudView>().IsOpen); Root<ShipEditorView>(InputContext.Editor);
            Assert.AreSame(Service<EditorHostServices>().View, ui.Navigator.Top);
            Assert.AreSame(Service<EditorHostServices>().Model, Service<UiComposition>().ShipEditorModel);
        }
    }
}
