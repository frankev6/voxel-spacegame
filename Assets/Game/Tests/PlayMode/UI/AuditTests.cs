using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.UI.Data;
using Game.UI.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    public sealed class AuditTests
    {
        UITestPanel panel;
        readonly List<LayoutIssue> layout = new List<LayoutIssue>();
        readonly List<ContrastIssue> contrast = new List<ContrastIssue>();
        [UnityTearDown] public IEnumerator Cleanup() { panel?.Dispose(); panel = null; yield return null; }
        VisualElement CreatePanel()
        {
            panel = new UITestPanel(1280, 720);
            var box = Box("audit-panel", 20, 20, 300, 200);
            box.AddToClassList(UIStyleClasses.Panel);
            panel.Root.Add(box);
            return box;
        }
        static VisualElement Box(string name, float x, float y, float width, float height)
        {
            var element = new VisualElement { name = name };
            Position(element, x, y, width, height);
            return element;
        }
        static void Position(VisualElement element, float x, float y, float width, float height)
        {
            element.style.position = UnityEngine.UIElements.Position.Absolute;
            element.style.left = x; element.style.top = y;
            element.style.width = width; element.style.height = height;
        }
        [UnityTest] public IEnumerator Layout_ButtonOutsidePanel_Reported()
        {
            var box = CreatePanel();
            var button = new Button { name = "outside-button", text = "Outside" };
            Position(button, 310, 20, 100, 40); box.Add(button);
            yield return null;
            UILayoutAudit.Run(panel.Root, panel.Viewport, layout);
            Assert.IsTrue(layout.Exists(x => x.Kind == LayoutIssueKind.OutsidePanel && x.ElementPath.EndsWith("outside-button")));
        }
        [UnityTest] public IEnumerator Layout_OverlappingRegions_Reported()
        {
            CreatePanel();
            var a = Box("region-a", 400, 30, 100, 100); a.AddToClassList(UIStyleClasses.HudRegion);
            var b = Box("region-b", 450, 60, 100, 100); b.AddToClassList(UIStyleClasses.HudRegion);
            panel.Root.Add(a); panel.Root.Add(b);
            yield return null;
            UILayoutAudit.Run(panel.Root, panel.Viewport, layout);
            Assert.IsTrue(layout.Exists(x => x.Kind == LayoutIssueKind.RegionOverlap));
        }
        [UnityTest] public IEnumerator Layout_TextOverflow_Reported()
        {
            var box = CreatePanel();
            var label = new Label(new string('W', 200)) { name = "overflow-label" };
            Position(label, 20, 20, 100, 40); label.style.whiteSpace = WhiteSpace.NoWrap;
            label.AddToClassList("text-m"); box.Add(label);
            yield return null;
            UILayoutAudit.Run(panel.Root, panel.Viewport, layout);
            Assert.IsTrue(layout.Exists(x => x.Kind == LayoutIssueKind.TextOverflow && x.ElementPath.EndsWith("overflow-label")));
        }
        [UnityTest] public IEnumerator Layout_CleanFixture_NoIssues()
        {
            var box = CreatePanel();
            var label = new Label("Ready") { name = "clean-label" };
            Position(label, 30, 30, 180, 40); label.AddToClassList("text-m"); box.Add(label);
            yield return null;
            UILayoutAudit.Run(panel.Root, panel.Viewport, layout);
            Assert.IsEmpty(layout);
        }
        [UnityTest] public IEnumerator Contrast_WhiteOnLightGrey_Reported()
        {
            var box = CreatePanel();
            var label = new Label("Unreadable") { name = "low-contrast" };
            Position(label, 20, 20, 220, 50);
            label.style.color = Color.white; label.style.backgroundColor = (Color)new Color32(240, 240, 240, 255);
            label.style.fontSize = 15; box.Add(label);
            yield return null;
            ContrastAudit.Run(panel.Root, contrast);
            Assert.IsTrue(contrast.Exists(x => x.ElementPath.EndsWith("low-contrast") && x.Ratio < x.Required));
            label.style.color = Token("--color-text"); label.style.backgroundColor = Token("--color-surface-2");
            yield return null;
            ContrastAudit.Run(panel.Root, contrast);
            Assert.IsEmpty(contrast);
        }
        [UnityTest] public IEnumerator Contrast_ControlsGallery_AllPass_AllSizes()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Game/Tests/PlayMode/UI/Fixtures/ControlsGallery.uxml");
            Assert.IsNotNull(asset);
            foreach (var size in UITestPanel.LogicalSizes)
            {
                panel = new UITestPanel(size.x, size.y);
                var tree = panel.Load(asset);
                yield return null;
                ContrastAudit.Run(tree, contrast);
                foreach (var issue in contrast)
                    Assert.Fail(size + ": " + issue.ElementPath + " ratio " + issue.Ratio + " requires " + issue.Required);
                panel.Dispose(); panel = null;
            }
        }
        [Test] public void Allegiance_CurrentTokens_Distinguishable()
            => Assert.GreaterOrEqual(AllegianceColorCheck.Run(Token("--color-ally"), Token("--color-enemy")), 20);
        [Test] public void Allegiance_RedGreen_Fails()
            => Assert.Less(AllegianceColorCheck.Run(new Color32(208, 64, 64, 255), new Color32(64, 160, 64, 255)), 20);
        static Color Token(string name)
        {
            string css = File.ReadAllText("Assets/Game/UI/Theme/tokens.uss");
            string hex = Regex.Match(css, Regex.Escape(name) + @"\s*:\s*(#[0-9A-Fa-f]+)").Groups[1].Value;
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var color), name);
            return color;
        }
    }
}
