using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 UI-005 / 06-B5: real TextField focus controls context and the two-step Back rule.</summary>
    public sealed class TextEntryTests
    {
        UIInputTestContext test;
        [UnitySetUp] public IEnumerator SetUp() { test = new UIInputTestContext(); yield return null; }
        [UnityTearDown] public IEnumerator TearDown() { test.Dispose(); yield return null; }
        [UnityTest] public IEnumerator FocusTextField_ContextTextEntry()
        {
            var field = test.OpenField(); yield return null;
            field.Focus(); yield return null;
            Assert.AreEqual(InputContext.TextEntry, test.UI.Navigator.CurrentContext);
            Assert.AreEqual(InputContext.TextEntry, test.Target.Current);
        }
        [UnityTest] public IEnumerator BlurTextField_ContextRestored()
        {
            var field = test.OpenField(); yield return null;
            field.Focus(); yield return null;
            Assert.AreEqual(InputContext.TextEntry, test.Target.Current);
            field.Blur(); yield return null;
            Assert.AreEqual(InputContext.Menu, test.UI.Navigator.CurrentContext);
            Assert.AreEqual(InputContext.Menu, test.Target.Current);
        }
        [UnityTest] public IEnumerator BackWhileTyping_BlursOnly()
        {
            var field = test.OpenField(); yield return null;
            field.Focus(); yield return null;
            Assert.AreEqual(InputContext.TextEntry, test.Target.Current);
            test.UI.HandleBackPressed(); yield return null;
            Assert.IsTrue(test.Overlay.IsOpen); Assert.AreEqual(0, test.Overlay.BackCount);
            Assert.AreEqual(InputContext.Menu, test.Target.Current);
            test.UI.HandleBackPressed(); yield return null;
            Assert.IsFalse(test.Overlay.IsOpen); Assert.AreEqual(1, test.Overlay.BackCount);
            Assert.AreSame(test.Root, test.UI.Navigator.Top);
            Assert.AreEqual(InputContext.Flight, test.Target.Current);
        }
    }
}
