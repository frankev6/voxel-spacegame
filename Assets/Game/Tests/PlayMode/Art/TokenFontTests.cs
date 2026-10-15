using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.Art
{
    /// <summary>ART-001 acceptance: a USS custom property can carry a font asset URL (SPEC-80 open question 9).</summary>
    public class TokenFontTests
    {
        GameObject host;
        PanelSettings panel;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (panel != null) Object.Destroy(panel);
        }

        [UnityTest]
        public IEnumerator FontBodyToken_ResolvesToInterFontAsset()
        {
#if UNITY_EDITOR
            var tokens = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Game/UI/Theme/tokens.uss");
            var rule = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Game/Tests/PlayMode/Art/TokenFontTest.uss");
            var expected = UnityEditor.AssetDatabase.LoadAssetAtPath<FontAsset>("Assets/Game/UI/Theme/Fonts/Inter/FNT_Inter-Regular_SDF.asset");
            Assert.IsNotNull(tokens, "tokens.uss");
            Assert.IsNotNull(rule, "TokenFontTest.uss");
            Assert.IsNotNull(expected, "FNT_Inter-Regular_SDF.asset");

            panel = ScriptableObject.CreateInstance<PanelSettings>();
            host = new GameObject("TokenFontTest");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            yield return null;

            var root = document.rootVisualElement;
            root.styleSheets.Add(tokens);
            root.styleSheets.Add(rule);
            var label = new Label("Token font");
            label.AddToClassList("token-font-test");
            root.Add(label);
            yield return null;
            yield return null;

            Assert.AreSame(expected, label.resolvedStyle.unityFontDefinition.fontAsset,
                "-unity-font-definition: var(--font-body) must resolve to the --font-body asset");
#else
            Assert.Ignore("Needs the AssetDatabase (Editor only).");
            yield break;
#endif
        }
    }
}
