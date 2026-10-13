using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
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

        [UnityTest] public IEnumerator Meter_SetValue_FillAndLabels()
        {
            var meter = new Meter { name = "controls-hull-meter", Caption = "HULL", Variant = "hull" };
            context.Root.Root.Add(meter); meter.SetValue(50, 200);
            yield return null;
            var fill = meter.Q<VisualElement>("controls-hull-meter-fill");
            Assert.IsNotNull(fill);
            Assert.AreEqual(LengthUnit.Percent, fill.style.width.value.unit);
            Assert.AreEqual(25, fill.style.width.value.value);
            Assert.AreEqual(.25f, meter.Fraction);
            Assert.AreEqual("HULL", meter.Q<Label>("controls-hull-meter-caption").text);
            Assert.AreEqual("50", meter.Q<Label>("controls-hull-meter-value").text);
            Assert.AreEqual("200", meter.Q<Label>("controls-hull-meter-max").text);
            Assert.IsFalse(meter.ClassListContains(UIStyleClasses.MeterLow), "Equal to the threshold is not low.");
            Assert.AreEqual(ThemeColor("--color-hull"), fill.resolvedStyle.backgroundColor, "The component sheet is imported into the active theme.");
            meter.SetFraction(.5014f);
            Assert.AreEqual(50.1f, fill.style.width.value.value, .0001f);
            Assert.AreEqual("50", meter.Q<Label>("controls-hull-meter-value").text, "SetFraction leaves number labels alone.");
            meter.ShowNumbers = false;
            Assert.IsTrue(meter.Q<Label>("controls-hull-meter-value").ClassListContains(UIStyleClasses.IsHidden));
            Assert.IsTrue(meter.Q<Label>("controls-hull-meter-max").ClassListContains(UIStyleClasses.IsHidden));
        }

        [Test] public void Meter_Low_ClassBelowThreshold()
        {
            var meter = new Meter();
            Assert.AreEqual(.25f, meter.LowThreshold);
            meter.SetValue(49, 200); Assert.IsTrue(meter.ClassListContains(UIStyleClasses.MeterLow));
            meter.SetValue(50, 200); Assert.IsFalse(meter.ClassListContains(UIStyleClasses.MeterLow));
            meter.SetValue(51, 200); Assert.IsFalse(meter.ClassListContains(UIStyleClasses.MeterLow));
            meter.LowThreshold = .3f; Assert.IsTrue(meter.ClassListContains(UIStyleClasses.MeterLow));
        }

        [UnityTest] public IEnumerator Meter_SetValue_ZeroAlloc()
        {
            var meter = new Meter { name = "controls-allocation-meter" }; var slot = new Slot { name = "controls-allocation-slot" };
            context.Root.Root.Add(meter); context.Root.Root.Add(slot);
            for (int i = 0; i < 10000; i++)
            {
                meter.SetValue(i, 9999); meter.SetFraction(i / 9999f);
                slot.SetCount(i); slot.SetCooldown(i / 9999f);
            }
            yield return null;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                meter.SetValue(i, 9999); meter.SetFraction(i / 9999f);
                slot.SetCount(i); slot.SetCooldown(i / 9999f);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "All four HUD setters must remain allocation-free in the cached number range.");
        }

        [UnityTest] public IEnumerator Slot_CountOne_HidesBadge()
        {
            var slot = new Slot { name = "controls-item-slot" }; context.Root.Root.Add(slot);
            slot.SetCount(2); yield return null;
            var badge = slot.Q<Label>("controls-item-slot-count");
            Assert.IsNotNull(badge); Assert.AreEqual("2", badge.text);
            Assert.IsFalse(badge.ClassListContains(UIStyleClasses.IsHidden));
            slot.SetCount(1); yield return null;
            Assert.AreEqual(DisplayStyle.None, badge.resolvedStyle.display);
            slot.SetCount(0); Assert.IsTrue(badge.ClassListContains(UIStyleClasses.IsHidden));
            slot.SetKeyHint("E"); Assert.AreEqual("E", slot.Q<Label>("controls-item-slot-key").text);
            slot.SetKeyHint(null); Assert.IsTrue(slot.Q<Label>("controls-item-slot-key").ClassListContains(UIStyleClasses.IsHidden));
        }

        [UnityTest] public IEnumerator Slot_Cooldown_OverlayHeight()
        {
            var slot = new Slot { name = "controls-ability-slot" }; context.Root.Root.Add(slot);
            slot.SetCooldown(.25f); yield return null;
            var overlay = slot.Q<VisualElement>("controls-ability-slot-cooldown");
            Assert.IsNotNull(overlay); Assert.AreEqual(LengthUnit.Percent, overlay.style.height.value.unit);
            Assert.AreEqual(25, overlay.style.height.value.value);
            Assert.IsFalse(overlay.ClassListContains(UIStyleClasses.IsHidden));
            Assert.Greater(overlay.worldBound.height, 0);
            slot.SetCooldown(0); yield return null;
            Assert.AreEqual(0, overlay.style.height.value.value);
            Assert.AreEqual(DisplayStyle.None, overlay.resolvedStyle.display);
        }

        [UnityTest] public IEnumerator TabBar_NineTabs_NoOverflowAt1280x720()
        {
            var parent = new VisualElement { name = "controls-tabs-panel" }; parent.AddToClassList(UIStyleClasses.Panel);
            var tabs = new TabBar { name = "controls-tabs" };
            var labels = new string[9]; var names = new string[9];
            for (int i = 0; i < 9; i++) { labels[i] = "ABCDEFGHIJKL"; names[i] = "controls-tab-" + i; }
            tabs.SetTabs(labels, names); parent.Add(tabs); context.Root.Root.Add(parent);
            yield return null;
            var viewport = new Rect(0, 0, 1280, 720);
            Assert.AreEqual(1280, context.UI.RootElement.worldBound.width, .5f);
            Assert.AreEqual(720, context.UI.RootElement.worldBound.height, .5f);
            Assert.AreEqual(9, tabs.TabCount);
            var scroll = tabs.Q<ScrollView>("controls-tabs-scroll");
            Assert.IsNotNull(scroll); Assert.AreEqual(ScrollViewMode.Horizontal, scroll.mode);
            Assert.AreEqual(ScrollerVisibility.Auto, scroll.horizontalScrollerVisibility);
            AssertInside(parent.worldBound, viewport, "Parent panel");
            AssertInside(tabs.worldBound, parent.worldBound, "Tab strip");
            AssertInside(scroll.worldBound, parent.worldBound, "Scroll view");
            AssertInside(scroll.contentViewport.worldBound, parent.worldBound, "Clipping viewport");
            Assert.Greater(scroll.contentContainer.worldBound.width, scroll.contentViewport.worldBound.width, "The fixture must actually exercise horizontal overflow.");
            Assert.AreEqual(DisplayStyle.Flex, scroll.horizontalScroller.resolvedStyle.display);
            AssertInside(scroll.horizontalScroller.worldBound, parent.worldBound, "Scrollbar");
            for (int i = 0; i < 9; i++)
            {
                var tab = tabs.Q<Button>(names[i]); Assert.IsNotNull(tab); Assert.AreEqual(labels[i], tab.text);
                Assert.AreEqual(WhiteSpace.NoWrap, tab.resolvedStyle.whiteSpace);
                Assert.GreaterOrEqual(tab.contentRect.width + .5f,
                    tab.MeasureTextSize(tab.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x,
                    "Tabs scroll instead of clipping or compressing labels.");
            }
            var last = tabs.Q<Button>(names[8]); scroll.ScrollTo(last); yield return null;
            Assert.Greater(scroll.scrollOffset.x, 0);
            AssertInside(last.worldBound, scroll.contentViewport.worldBound, "Last tab after scrolling");
            AssertInside(tabs.worldBound, parent.worldBound, "Tab strip after scrolling");
            var issues = new List<LayoutIssue>();
            UILayoutAudit.Run(parent, new Rect(0, 0, 1280, 720), issues);
            Assert.IsEmpty(issues, "Canonical layout audit must agree with the retained scroll viewport checks.");
        }

        [Test] public void TabBar_SelectSame_NoEvent()
        {
            var tabs = new TabBar();
            var labels = new[] { "Hull", "Weapons", "Power" }; var names = new[] { "controls-tab-hull", "controls-tab-weapons", "controls-tab-power" };
            tabs.SetTabs(labels, names); Assert.AreEqual(0, tabs.SelectedIndex);
            int changes = 0, selected = -1;
            tabs.SelectionChanged += index => { changes++; selected = index; };
            tabs.SelectedIndex = 0; Assert.AreEqual(0, changes);
            tabs.SelectedIndex = 1; Assert.AreEqual(1, changes); Assert.AreEqual(1, selected);
            Assert.IsFalse(tabs.Q<Button>(names[0]).ClassListContains(UIStyleClasses.IsSelected));
            Assert.IsTrue(tabs.Q<Button>(names[1]).ClassListContains(UIStyleClasses.IsSelected));
            tabs.SelectedIndex = 1; Assert.AreEqual(1, changes);
            tabs.SetTabs(labels, names); Assert.AreEqual(1, changes, "Rebuilding with the same selection does not emit a change.");
            Assert.IsTrue(tabs.Q<Button>(names[1]).ClassListContains(UIStyleClasses.IsSelected));
        }

        static void AssertInside(Rect bounds, Rect container, string label)
        {
            Assert.IsFalse(float.IsNaN(bounds.x) || float.IsNaN(bounds.y) || float.IsNaN(bounds.width) || float.IsNaN(bounds.height), label);
            Assert.Greater(bounds.width, 0, label); Assert.Greater(bounds.height, 0, label);
            Assert.GreaterOrEqual(bounds.xMin, container.xMin - .5f, label);
            Assert.GreaterOrEqual(bounds.yMin, container.yMin - .5f, label);
            Assert.LessOrEqual(bounds.xMax, container.xMax + .5f, label);
            Assert.LessOrEqual(bounds.yMax, container.yMax + .5f, label);
        }
    }
}
