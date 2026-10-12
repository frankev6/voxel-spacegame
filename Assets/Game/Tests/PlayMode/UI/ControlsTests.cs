using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.UI.Controls;
using Game.UI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    public sealed class ControlsTests
    {
        const string GalleryPath = "Assets/Game/Tests/PlayMode/UI/Fixtures/ControlsGallery.uxml";
        UIInputTestContext context;
        [UnitySetUp] public IEnumerator SetUp() { context = new UIInputTestContext(); yield return null; }
        [UnityTearDown] public IEnumerator TearDown() { context.Dispose(); yield return null; }
        VisualElement LoadGallery()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GalleryPath);
            Assert.IsNotNull(asset);
            var tree = asset.CloneTree(); context.Root.Root.Add(tree); return tree;
        }
        static Color ThemeColor(string token)
        {
            string css = File.ReadAllText("Assets/Game/UI/Theme/tokens.uss");
            string value = Regex.Match(css, Regex.Escape(token) + @"\s*:\s*(#[0-9A-Fa-f]+)").Groups[1].Value;
            Assert.IsTrue(ColorUtility.TryParseHtmlString(value, out var color), token);
            return color;
        }
        [UnityTest] public IEnumerator KeyHint_Refresh_ShowsResolverText()
        {
            var tree = LoadGallery(); var hint = tree.Q<KeyHint>("gallery-key-hint");
            Assert.IsNotNull(hint); Assert.AreEqual("Flight/Interact", hint.ActionPath);
            string requested = null;
            hint.Refresh(path => { requested = path; return "F"; });
            yield return null;
            Assert.AreEqual("Flight/Interact", requested); Assert.AreEqual("F", hint.text);
            Assert.IsTrue(hint.ClassListContains(UIStyleClasses.KeyHint));
            hint.Refresh(path => "Mouse 4"); Assert.AreEqual("Mouse 4", hint.text);
        }
        [UnityTest] public IEnumerator Gallery_Loads_NoMissingClasses()
        {
            var selectors = new HashSet<string>();
            foreach (string file in Directory.GetFiles("Assets/Game/UI/Theme", "*.uss", SearchOption.AllDirectories))
            {
                string css = Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline);
                foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{"))
                    foreach (Match name in Regex.Matches(rule.Groups[1].Value, @"\.([a-z][a-z0-9_-]*)"))
                        selectors.Add(name.Groups[1].Value);
            }
            foreach (Match attribute in Regex.Matches(File.ReadAllText(GalleryPath), "class=\"([^\"]+)\""))
                foreach (string name in attribute.Groups[1].Value.Split(' '))
                    Assert.IsTrue(selectors.Contains(name), "Gallery class has no USS selector: " + name);
            var tree = LoadGallery(); yield return null;
            var field = tree.Q<TextField>("gallery-input");
            var input = field.Q<VisualElement>("unity-text-input");
            Assert.IsNotNull(input);
            var background = ThemeColor("--color-surface-2");
            var foreground = ThemeColor("--color-text");
            Assert.AreEqual(background, input.resolvedStyle.backgroundColor);
            Assert.AreEqual(foreground, input.resolvedStyle.color);
            Assert.AreNotEqual(foreground, background, "Editable text must remain readable");
            Assert.IsFalse(tree.Q<Button>("gallery-disabled").enabledInHierarchy);
        }
    }
}
