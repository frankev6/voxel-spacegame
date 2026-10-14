using System.Collections;
using System.Collections.Generic;
using Game.App.EditorHost;
using Game.App.Session;
using Game.Core;

using Game.Core.Flow;
using Game.Input;
using Game.Persistence.Services;
using Game.ShipEditor.Data;
using Game.Tests.Common;
using Game.Tests.PlayMode.UI;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.ShipEditor
{
    /// <summary>EDT-023 production navigation and persisted blueprint acceptance.</summary>
    [Category("Scenario")]
    public sealed class EditorRoundTripScenarioTests : GameFlowFixture
    {
        [UnityTest]
        public IEnumerator Lab_FromMainMenu_SaveBlueprint()
        {
            var session = Service<GameSession>();
            var saves = Service<SaveService>();
            var ui = Service<UIRoot>();
            Assert.IsFalse(session.HasActiveSession);
            DialogTestContext.Activate(ui.Navigator.Get<MainMenuView>().Root.Q<Button>("main-menu-lab"));
            yield return WaitUntil(() => session.CurrentScene == GameScene.BlueprintLab && !session.IsTransitioning, 10, "Main menu opens actual Lab");
            var editor = Service<EditorHostServices>();
            var host = Object.FindAnyObjectByType<ShipEditorHost>();
            Assert.IsNotNull(host); Assert.AreSame(editor.View, ui.Navigator.Top);
            Assert.AreEqual(InputContext.Editor, Service<InputService>().CurrentContext);
            int cells = host.Session.Draft.SolidCellCount;
            Assert.Greater(cells, 0);
            editor.SaveBlueprint();
            yield return null;
            var prompt = ui.Navigator.Get<TextPromptView>();
            prompt.Root.Q<TextField>("text-prompt-field").value = "Lab scenario";
            DialogTestContext.Activate(prompt.Root.Q<Button>("text-prompt-ok"));
            var blueprints = new List<BlueprintInfo>();
            editor.Blueprints.List(blueprints);
            var saved = blueprints.FindAll(item => !item.ReadOnly);
            Assert.AreEqual(1, saved.Count);
            Assert.AreEqual("lab_scenario", saved[0].DisplayName); // SPEC-51 Q47: saved names use sanitized IDs.
            Assert.IsTrue(editor.Blueprints.TryLoad(saved[0].Id, out var restored, out var error), error);
            using (restored) Assert.AreEqual(cells, restored.SolidCellCount);
            Assert.IsEmpty(saves.ListSlots(saves.ActiveProfile), "Creative blueprint storage must not create a campaign save.");
            editor.RequestLeave();
            yield return WaitUntil(() => session.CurrentScene == GameScene.MainMenu && !session.IsTransitioning, 10, "Lab returns to MainMenu");
            Assert.IsFalse(session.HasActiveSession);
            Assert.IsInstanceOf<MainMenuView>(ui.Navigator.Top);
            Assert.AreEqual(1, ui.Navigator.Count);
            Assert.AreEqual(0, Service<IPauseService>().ActiveTokenCount);
            Assert.AreEqual(InputContext.Menu, Service<InputService>().CurrentContext);
            editor.Blueprints.List(blueprints);
            Assert.AreEqual(1, blueprints.FindAll(item => !item.ReadOnly).Count, "Stored blueprint survives session teardown.");
        }
        // TODO(EDT-023): actual fleet removal/commit pose, station interaction and three-trip scenarios.
        // TODO(EDT-023): FromDialog_DialogEndedFirst requires the production DLG-007 runner.
    }
}
