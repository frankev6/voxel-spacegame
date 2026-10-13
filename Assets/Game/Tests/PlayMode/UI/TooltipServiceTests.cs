using System.Collections;
using Game.UI.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 UI-009: actual scheduler, panel geometry, pooled content and target lifecycle.</summary>
    public sealed class TooltipServiceTests
    {
        UIInputTestContext context;
        VisualElement target;
        int fills;
        VisualElement Tooltip => context.UI.GetLayer(UILayer.Tooltip).Q<VisualElement>("tooltip");
        [UnitySetUp] public IEnumerator SetUp()
        {
            fills = 0; context = new UIInputTestContext(0); AddTarget(); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() { context.Dispose(); yield return null; }
        void AddTarget()
        {
            target = new Label("Hover target") { name = "tooltip-test-target" };
            context.UI.GetLayer(UILayer.Hud).Add(target); context.UI.Tooltips.Attach(target, Fill);
        }
        void Fill(TooltipContent content)
        {
            fills++; content.Title = "PLASMA TURRET"; content.Subtitle = "Weapon";
            content.Body = "A mounted energy weapon with a short description.";
        }
        static Event PointerEvent(Vector2 position, EventType type = EventType.MouseMove)
            => new Event { type = type, mousePosition = position, button = 0 };
        void Enter(Vector2 position)
        { using var evt = PointerEnterEvent.GetPooled(PointerEvent(position)); evt.target = target; target.SendEvent(evt); }
        void Leave()
        { using var evt = PointerLeaveEvent.GetPooled(); evt.target = target; target.SendEvent(evt); }
        void Down()
        { using var evt = PointerDownEvent.GetPooled(PointerEvent(new Vector2(400, 200), EventType.MouseDown)); evt.target = target; target.SendEvent(evt); }
        void Move(Vector2 position)
        { using var evt = PointerMoveEvent.GetPooled(PointerEvent(position)); evt.target = target; target.SendEvent(evt); }
        IEnumerator AwaitVisible()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!context.UI.Tooltips.IsVisible && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(context.UI.Tooltips.IsVisible, "Tooltip scheduler did not complete within 5 seconds");
            // Position is recomputed after the displayed tooltip receives its actual layout.
            yield return null; yield return null;
            Assert.Greater(Tooltip.worldBound.width, 0); Assert.Greater(Tooltip.worldBound.height, 0);
            Assert.AreEqual(DisplayStyle.Flex, Tooltip.resolvedStyle.display);
        }
        void AssertInside()
        {
            Rect bounds = Tooltip.worldBound, panel = context.UI.RootElement.worldBound;
            Assert.Greater(bounds.width, 0); Assert.Greater(bounds.height, 0);
            Assert.LessOrEqual(bounds.width, 360.5f, "Shared tooltip maximum width");
            Assert.GreaterOrEqual(bounds.xMin, panel.xMin + 7.5f);
            Assert.GreaterOrEqual(bounds.yMin, panel.yMin + 7.5f);
            Assert.LessOrEqual(bounds.xMax, panel.xMax - 7.5f);
            Assert.LessOrEqual(bounds.yMax, panel.yMax - 7.5f);
        }
        [UnityTest] public IEnumerator Hover_AfterDelay_Shows()
        {
            context.Dispose(); context = new UIInputTestContext(); AddTarget(); yield return null;
            double started = Time.realtimeSinceStartupAsDouble; Enter(new Vector2(400, 200));
            Assert.IsFalse(context.UI.Tooltips.IsVisible); Assert.AreEqual(0, fills);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!context.UI.Tooltips.IsVisible && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(context.UI.Tooltips.IsVisible);
            Assert.GreaterOrEqual(Time.realtimeSinceStartupAsDouble - started, .325, "The default 350 ms hover delay must not be skipped (25 ms scheduler tolerance).");
            yield return null; yield return null;
            Assert.AreEqual(1, fills);
            Assert.AreEqual("PLASMA TURRET", Tooltip.Q<Label>("tooltip-title").text);
            Assert.AreEqual("Weapon", Tooltip.Q<Label>("tooltip-subtitle").text);
            Assert.AreEqual(1, context.UI.GetLayer(UILayer.Tooltip).childCount);
            AssertInside();
        }
        [UnityTest] public IEnumerator Leave_Hides()
        {
            Enter(new Vector2(400, 200)); Leave();
            yield return null; yield return null;
            Assert.IsFalse(context.UI.Tooltips.IsVisible); Assert.AreEqual(0, fills, "Leaving cancels a pending show");
            Enter(new Vector2(400, 200)); yield return AwaitVisible(); Leave();
            Assert.IsFalse(context.UI.Tooltips.IsVisible);
            Enter(new Vector2(400, 200)); yield return AwaitVisible(); Down();
            Assert.IsFalse(context.UI.Tooltips.IsVisible, "Pointer down hides");
            Enter(new Vector2(400, 200)); yield return AwaitVisible(); target.RemoveFromHierarchy();
            Assert.IsFalse(context.UI.Tooltips.IsVisible, "Removing the target hides");
            context.UI.GetLayer(UILayer.Hud).Add(target);
            Enter(new Vector2(400, 200)); yield return AwaitVisible(); context.UI.Tooltips.Detach(target);
            Assert.IsFalse(context.UI.Tooltips.IsVisible, "Detach unregisters the manipulator");
            int previous = fills; Enter(new Vector2(400, 200)); yield return null; yield return null;
            Assert.AreEqual(previous, fills); Assert.IsFalse(context.UI.Tooltips.IsVisible);
            Assert.AreEqual(DisplayStyle.None, Tooltip.resolvedStyle.display);
            context.UI.Tooltips.Attach(target, Fill);
            Enter(new Vector2(400, 200)); Down(); yield return null; yield return null;
            Assert.AreEqual(previous, fills, "Pointer down cancels pending shows");
            Enter(new Vector2(400, 200)); target.RemoveFromHierarchy(); yield return null; yield return null;
            Assert.AreEqual(previous, fills, "Target removal cancels pending shows");
            context.UI.GetLayer(UILayer.Hud).Add(target); context.UI.Dispose();
            Enter(new Vector2(400, 200)); yield return null; yield return null;
            Assert.AreEqual(previous, fills, "Disposal removes target callbacks and the scheduled show");
        }
        [UnityTest] public IEnumerator NearRightEdge_FlipsInside()
        {
            Vector2 pointer = new Vector2(1270, 200); Enter(pointer); yield return AwaitVisible();
            AssertInside(); Assert.LessOrEqual(Tooltip.worldBound.xMax, pointer.x - 15.5f, "Must flip left of the cursor");
            Move(new Vector2(300, 200)); yield return null; yield return null;
            Assert.AreEqual(316, Tooltip.worldBound.xMin, .5f, "Pointer moves reposition without rebuilding or refilling");
            Assert.AreEqual(1, fills);
        }
        [UnityTest] public IEnumerator NearBottomEdge_FlipsInside()
        {
            Vector2 pointer = new Vector2(400, 710); Enter(pointer); yield return AwaitVisible();
            AssertInside(); Assert.LessOrEqual(Tooltip.worldBound.yMax, pointer.y - 19.5f, "Must flip above the cursor");
        }
        [UnityTest] public IEnumerator StackChanged_Hides()
        {
            Enter(new Vector2(400, 200)); yield return AwaitVisible(); context.UI.Navigator.Push(context.Overlay);
            Assert.IsFalse(context.UI.Tooltips.IsVisible);
            context.UI.Navigator.Pop(context.Overlay);
            Enter(new Vector2(400, 200)); context.UI.Navigator.Push(context.Overlay);
            int previous = fills; yield return null; yield return null;
            Assert.IsFalse(context.UI.Tooltips.IsVisible); Assert.AreEqual(previous, fills, "Stack changes cancel pending shows too");
        }
        [UnityTest] public IEnumerator ShowAtScreen_FlipsY()
        {
            // The RenderTexture panel is deliberately different from Screen.height. Mapping is one-to-one.
            context.UI.Tooltips.ShowAtScreen(Fill, new Vector2(300, 520)); yield return AwaitVisible();
            Assert.AreEqual(316, Tooltip.worldBound.xMin, .5f);
            Assert.AreEqual(220, Tooltip.worldBound.yMin, .5f, "720 - 520 + the 20 px offset");
            AssertInside();
            Assert.IsNull(context.UI.Panel.Pick(Tooltip.worldBound.center), "Tooltip and its labels must not block world pointers");
            context.UI.Tooltips.HideScreenTooltip(); Assert.IsFalse(context.UI.Tooltips.IsVisible);
            // Check the conversion really uses the configured ScreenToPanel mapping and scales before flipping.
            // Exercise non-default UI scaling through the panel settings owned by this fixture.
            context.SetScreenMapping(position => position * .5f);
            context.UI.Tooltips.ShowAtScreen(Fill, new Vector2(600, 1040)); yield return AwaitVisible();
            Assert.AreEqual(316, Tooltip.worldBound.xMin, .5f); Assert.AreEqual(220, Tooltip.worldBound.yMin, .5f);
        }
        [UnityTest] public IEnumerator NineStats_ShowsEight()
        {
            TooltipContent first = null;
            context.UI.Tooltips.ShowAtScreen(content =>
            {
                first = content; Fill(content);
                for (int i = 0; i < 9; i++) content.Stats.Add(new TooltipStat { Label = "Stat " + i, Value = "Value " + i, Sign = i % 3 - 1 });
            }, new Vector2(400, 520)); yield return AwaitVisible();
            var stats = Tooltip.Q<VisualElement>("tooltip-stats"); Assert.AreEqual(8, stats.childCount);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(DisplayStyle.Flex, stats[i].resolvedStyle.display);
                var value = stats[i].Q<Label>("tooltip-stat-" + i + "-value");
                Assert.AreEqual("Value " + i, value.text);
                Assert.AreEqual(i % 3 == 2, value.ClassListContains(UIStyleClasses.TooltipStatValueBetter));
                Assert.AreEqual(i % 3 == 0, value.ClassListContains(UIStyleClasses.TooltipStatValueWorse));
            }
            var firstRow = stats[0];
            context.UI.Tooltips.ShowAtScreen(content =>
            {
                Assert.AreSame(first, content); Assert.AreEqual(0, content.Stats.Count); Assert.IsNull(content.Title);
                content.Title = "Replacement"; content.Stats.Add(new TooltipStat { Label = "Neutral", Value = "0" });
            }, new Vector2(400, 520)); yield return AwaitVisible();
            Assert.AreSame(firstRow, stats[0]); Assert.AreEqual(8, stats.childCount, "No row rebuild");
            Assert.AreEqual(DisplayStyle.Flex, stats[0].resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, stats[1].resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, Tooltip.Q<Label>("tooltip-subtitle").resolvedStyle.display);
            Assert.IsFalse(stats[0].Q<Label>("tooltip-stat-0-value").ClassListContains(UIStyleClasses.TooltipStatValueWorse));
            Assert.IsFalse(stats[0].Q<Label>("tooltip-stat-0-value").ClassListContains(UIStyleClasses.TooltipStatValueBetter));
        }
    }
}
