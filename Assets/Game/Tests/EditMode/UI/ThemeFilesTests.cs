using System.IO;
using System.Text.RegularExpressions;
using Game.UI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests.EditMode.UI
{
    /// <summary>SPEC-60 UI-002: real panel assets, imported root layout and token-only theme styling.</summary>
    public sealed class ThemeFilesTests
    {
        const string Theme = "Assets/Game/UI/Theme/";
        [Test] public void PanelSettings_MatchesDecision3()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(Theme + "GamePanelSettings.asset");
            Assert.IsNotNull(panel);
            Assert.AreEqual(PanelScaleMode.ScaleWithScreenSize, panel.scaleMode);
            Assert.AreEqual(new Vector2Int(1920, 1080), panel.referenceResolution);
            Assert.AreEqual(PanelScreenMatchMode.MatchWidthOrHeight, panel.screenMatchMode);
            Assert.AreEqual(0.5f, panel.match);
            Assert.AreEqual(0, panel.sortingOrder); Assert.IsFalse(panel.clearColor);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Theme + "Game.tss"), panel.themeStyleSheet);
            var config = AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset");
            Assert.IsNotNull(config); Assert.AreSame(panel, config.PanelSettings); Assert.IsNotNull(config.RootUxml);
        }
        [Test] public void RootUxml_HasSixLayersInOrder()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Game/UI/Views/Uxml/UIRoot.uxml");
            Assert.IsNotNull(asset);
            var tree = asset.CloneTree(); var root = tree.Q<VisualElement>("ui-root");
            Assert.IsNotNull(root); Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.AreEqual(6, root.childCount);
            for (int i = 0; i < root.childCount; i++)
            {
                Assert.AreEqual(UINames.Layer((UILayer)i), root[i].name);
                Assert.AreEqual(PickingMode.Ignore, root[i].pickingMode);
                Assert.IsTrue(root[i].ClassListContains(UIStyleClasses.Layer));
            }
        }
        [Test] public void ThemeUss_NoLiteralColorsOrFonts()
        {
            foreach (string path in Directory.GetFiles(Theme, "*.uss", SearchOption.AllDirectories))
            {
                // SPEC-60 Q11: SPEC-80's alternative direction token sheets are token sources, not UI styles.
                if (Path.GetFileName(path) == "tokens.uss" || Path.GetFileName(path) == "accessibility.uss"
                    || path.Replace('\\', '/').StartsWith(Theme + "Directions/", System.StringComparison.Ordinal)) continue;
                string text = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", "", RegexOptions.Singleline);
                Assert.IsFalse(Regex.IsMatch(text, @"#[0-9a-fA-F]{3,8}\b|rgba?\s*\(|url\s*\(|font-size\s*:\s*(?!\s*var\s*\()[^\s]", RegexOptions.IgnoreCase), path);
            }
        }
    }
}
