using System.Collections;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.App
{
    public sealed class DevUiQueryTests
    {
        GameObject go;
        PanelSettings panel;
        RenderTexture texture;
        [UnityTearDown] public IEnumerator TearDown()
        {
            Object.Destroy(go); Object.Destroy(panel); Object.Destroy(texture); yield return null;
        }
        [UnityTest] public IEnumerator Click_ByText_InvokesButton()
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            texture = new RenderTexture(512, 256, 0); panel.targetTexture = texture;
            go = new GameObject("Dev UI test");
            var doc = go.AddComponent<UIDocument>(); doc.panelSettings = panel;
            int clicks = 0;
            var button = new Button(() => clicks++) { name = "new-game", text = "New Game" };
            button.style.width = 200; button.style.height = 60; // Test-only geometry, no production theme or art.
            doc.rootVisualElement.Add(button);
            var query = new DevUiQuery(() => doc.rootVisualElement);
            double limit = Time.realtimeSinceStartupAsDouble + 5;
            while (!DevUiQuery.Shown(button) && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Assert.IsTrue(DevUiQuery.Shown(button), "real panel layout completed");
            StringAssert.Contains("New Game", query.Text());
            var result = query.Click("New Game"); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            yield return null;
            Assert.AreEqual(1, clicks);
        }
    }
}
