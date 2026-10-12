using System.Collections;
using Game.UI.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 UI-005 / 06-B9: real picking and the two-frame close-click guard.</summary>
    public sealed class WorldPointerGateTests
    {
        UIInputTestContext test;
        [UnitySetUp] public IEnumerator SetUp() { test = new UIInputTestContext(); yield return null; }
        [UnityTearDown] public IEnumerator TearDown() { test.Dispose(); yield return null; }
        [UnityTest] public IEnumerator OverButton_NotAllowed()
        {
            var button = new Button { text = "Test", style = { position = Position.Absolute, left = 100, top = 100, width = 120, height = 40 } };
            test.UI.GetLayer(UILayer.Hud).Add(button); yield return null;
            var center = button.worldBound.center;
            Assert.AreSame(button, test.UI.Panel.Pick(center));
            Assert.IsFalse(test.UI.IsWorldPointerAllowed(UIInputTestContext.ToScreen(center)));
        }
        [UnityTest] public IEnumerator OverEmptyHudLayer_Allowed()
        {
            yield return null;
            Assert.IsTrue(test.UI.IsWorldPointerAllowed(UIInputTestContext.ToScreen(new Vector2(400, 300))));
        }
        [UnityTest] public IEnumerator BlockingScreenOpen_NotAllowed()
        {
            test.Overlay.BlockPointer = true; test.UI.Navigator.Push(test.Overlay);
            test.Frames.FrameCount += 2; yield return null;
            Assert.IsFalse(test.UI.IsWorldPointerAllowed(UIInputTestContext.ToScreen(new Vector2(400, 300))));
        }
        [UnityTest] public IEnumerator FrameOfPopAndNext_NotAllowed()
        {
            test.UI.Navigator.Push(test.Overlay); yield return null;
            test.Frames.FrameCount += 2; test.UI.Navigator.Pop(test.Overlay); yield return null;
            var position = UIInputTestContext.ToScreen(new Vector2(400, 300));
            Assert.IsFalse(test.UI.IsWorldPointerAllowed(position), "Pop frame");
            test.Frames.FrameCount++;
            Assert.IsFalse(test.UI.IsWorldPointerAllowed(position), "Following frame");
            test.Frames.FrameCount++;
            Assert.IsTrue(test.UI.IsWorldPointerAllowed(position), "Two frames later");
        }
    }
}
