using System.Collections;
using Game.Tests.PlayMode.UI.Fixtures;
using Game.UI.Data;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>UI-006: SPEC-60 §5.6 real UXML binding, group registration and percent conversion.</summary>
    public sealed class BindingTests
    {
        UIInputTestContext test;
        [UnitySetUp] public IEnumerator SetUp() { test = new UIInputTestContext(); yield return null; }
        [UnityTearDown] public IEnumerator TearDown() { test.Dispose(); yield return null; }

        [UnityTest] public IEnumerator UxmlBinding_UpdatesOnChange()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Game/Tests/PlayMode/UI/Fixtures/BindingSample.uxml");
            Assert.IsNotNull(asset);
            var tree = asset.Instantiate();
            var model = new BindingSampleViewModel(); tree.dataSource = model;
            test.Root.Root.Add(tree);
            var label = tree.Q<Label>("binding-hull");
            Assert.IsNotNull(label.GetBinding("text"), "The fixture must use UXML DataBinding");
            yield return null;
            Assert.AreEqual("100", label.text);

            int reads = model.HullReads;
            model.Hull = 73;
            yield return null;
            Assert.AreEqual("73", label.text, "Changed value binds within one frame");
            Assert.AreSame(NumberStringCache.Get(73), label.text, "UXML must apply the registered int-to-text converter");
            Assert.Greater(model.HullReads, reads);
            long hash = model.GetViewHashCode(); reads = model.HullReads;
            model.Hull = 73;
            yield return null;
            Assert.AreEqual("73", label.text);
            Assert.AreEqual(hash, model.GetViewHashCode());
            Assert.AreEqual(reads, model.HullReads, "An unchanged source must not be polled again");
        }

        [Test] public void Converters_RegisterTwice_NoThrow()
        {
            UIConverters.RegisterAll();
            string[] ids = { "fraction-to-percent", "int-to-text", "bool-to-display", "seconds-to-clock" };
            var groups = new ConverterGroup[ids.Length];
            for (int i = 0; i < ids.Length; i++) Assert.IsTrue(ConverterGroups.TryGetConverterGroup(ids[i], out groups[i]));
            Assert.DoesNotThrow(UIConverters.RegisterAll);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.IsTrue(ConverterGroups.TryGetConverterGroup(ids[i], out var current));
                Assert.AreSame(groups[i], current);
            }
            int count = -15;
            Assert.IsTrue(groups[1].TryConvert(ref count, out string text));
            Assert.AreSame(NumberStringCache.Get(count), text);
            bool visible = false;
            Assert.IsTrue(groups[2].TryConvert(ref visible, out StyleEnum<DisplayStyle> display));
            Assert.AreEqual(DisplayStyle.None, display.value);
            visible = true;
            Assert.IsTrue(groups[2].TryConvert(ref visible, out display));
            Assert.AreEqual(DisplayStyle.Flex, display.value);
            float seconds = 92.8f;
            Assert.IsTrue(groups[3].TryConvert(ref seconds, out text));
            Assert.AreEqual("01:32", text);
        }

        [Test] public void FractionToPercent_Clamps()
        {
            Assert.IsTrue(ConverterGroups.TryGetConverterGroup("fraction-to-percent", out var group));
            float[] fractions = { -0.25f, 0.375f, 1.5f };
            float[] expected = { 0f, 37.5f, 100f };
            for (int i = 0; i < fractions.Length; i++)
            {
                float value = fractions[i];
                Assert.IsTrue(group.TryConvert(ref value, out StyleLength length));
                Assert.AreEqual(LengthUnit.Percent, length.value.unit);
                Assert.AreEqual(expected[i], length.value.value);
            }
        }
    }
}
