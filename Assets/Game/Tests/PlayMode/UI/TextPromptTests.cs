using System.Collections;
using Game.Core;
using Game.UI.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 UI-011: actual TextField validation, Return/KeypadEnter, cancellation and focus context.</summary>
    public sealed class TextPromptTests
    {
        DialogTestContext test;
        [UnitySetUp] public IEnumerator SetUp()
        {
            test = new DialogTestContext();
            yield return DialogTestContext.Until(() => test.UI.Panel != null && test.UI.RootElement.worldBound.width > 0, "Prompt panel layout");
        }
        [UnityTearDown] public IEnumerator TearDown() { test.Dispose(); yield return null; }
        TextField Field => test.PromptView.Root.Q<TextField>("text-prompt-field");
        Button Ok => test.PromptView.Root.Q<Button>("text-prompt-ok");
        Label Error => test.PromptView.Root.Q<Label>("text-prompt-error");
        [UnityTest] public IEnumerator Empty_OkDisabled()
        {
            int submitted = 0;
            test.UI.Prompt.Show(new TextPromptRequest { InitialText = "   " }, _ => submitted++, null);
            yield return DialogTestContext.Until(() => Error.text == "Required", "Required validation binding");
            Assert.IsFalse(Ok.enabledInHierarchy); Assert.IsTrue(Field.ClassListContains(UIStyleClasses.IsInvalid));
            DialogTestContext.Enter(Field); DialogTestContext.Activate(Ok); Assert.AreEqual(0, submitted); Assert.IsTrue(test.UI.Prompt.IsOpen);
            Field.value = "Name";
            yield return DialogTestContext.Until(() => Ok.enabledInHierarchy && Error.parent.ClassListContains(UIStyleClasses.IsHidden), "Valid text enables OK");
        }
        [UnityTest] public IEnumerator ValidatorError_Shown()
        {
            string validated = null;
            test.UI.Prompt.Show(new TextPromptRequest { InitialText = "  bad  ", Validate = value => { validated = value; return value == "bad" ? "Name already used" : null; } }, null, null);
            yield return DialogTestContext.Until(() => Error.text == "Name already used", "Validator error binding");
            Assert.AreEqual("bad", validated); Assert.IsFalse(Ok.enabledInHierarchy); Assert.IsFalse(Error.parent.ClassListContains(UIStyleClasses.IsHidden));
            Field.value = "Good";
            yield return DialogTestContext.Until(() => Ok.enabledInHierarchy && Error.parent.ClassListContains(UIStyleClasses.IsHidden), "Validator recovery");
        }
        [UnityTest] public IEnumerator Enter_SubmitsTrimmed()
        {
            string submitted = null; bool afterPop = false; int count = 0;
            test.UI.Prompt.Show(new TextPromptRequest { InitialText = "  Hull  ", OkLabel = "Rename", CancelLabel = "Keep" }, value =>
            { submitted = value; count++; afterPop = !test.UI.Prompt.IsOpen && test.UI.Navigator.Top == test.Root && test.Target.Current == InputContext.Flight && !test.Tokens.IsPaused; }, null);
            yield return DialogTestContext.Until(() => test.Target.Current == InputContext.TextEntry && Field.value == "  Hull  " && Ok.text == "Rename", "Prompt focus and labels");
            var cached = test.PromptView.Root; var firstField = Field;
            DialogTestContext.Enter(firstField);
            yield return DialogTestContext.Until(() => submitted == "Hull", "Return submits trimmed text"); Assert.IsTrue(afterPop); Assert.AreEqual(1, count);
            submitted = null;
            test.UI.Prompt.Show(new TextPromptRequest { InitialText = "  Second  " }, value => submitted = value, null);
            yield return DialogTestContext.Until(() => test.Target.Current == InputContext.TextEntry && Field.value == "  Second  ", "Reused prompt is focused");
            Assert.AreSame(cached, test.PromptView.Root); DialogTestContext.Enter(Field, KeyCode.KeypadEnter);
            yield return DialogTestContext.Until(() => submitted == "Second", "Keypad Enter submits");
        }
        [UnityTest] public IEnumerator Back_Cancels()
        {
            int cancelled = 0; bool afterPop = false;
            test.UI.Prompt.Show(new TextPromptRequest { InitialText = "Valid" }, null, () =>
            { cancelled++; afterPop = !test.UI.Prompt.IsOpen && test.UI.Navigator.Top == test.Root && test.Target.Current == InputContext.Flight; });
            yield return DialogTestContext.Until(() => test.Target.Current == InputContext.TextEntry, "Typing context");
            test.UI.HandleBackPressed();
            yield return DialogTestContext.Until(() => test.Target.Current == InputContext.Menu, "First Back blurs");
            Assert.IsTrue(test.UI.Prompt.IsOpen); Assert.AreEqual(0, cancelled);
            test.UI.HandleBackPressed();
            yield return DialogTestContext.Until(() => cancelled == 1, "Second Back cancels"); Assert.IsTrue(afterPop);
            test.UI.Prompt.Show(new TextPromptRequest(), null, () => cancelled++);
            DialogTestContext.Activate(test.PromptView.Root.Q<Button>("text-prompt-cancel"));
            yield return DialogTestContext.Until(() => cancelled == 2, "Cancel button callback");
        }
        [UnityTest] public IEnumerator Focus_SetsTextEntryContext()
        {
            test.UI.Prompt.Show(new TextPromptRequest { Title = "Name ship", Label = "Ship name", InitialText = "0123456789", MaxLength = 5 }, null, null);
            yield return DialogTestContext.Until(() => test.Target.Current == InputContext.TextEntry && Field.value == "01234", "Default field focus and maximum length");
            Assert.AreEqual(InputContext.TextEntry, test.UI.Navigator.CurrentContext); Assert.AreEqual(5, Field.maxLength);
            Assert.AreEqual(1, test.Tokens.ActiveTokenCount); Assert.IsTrue(test.UI.Navigator.WorldPointerBlocked);
            Field.Blur();
            yield return DialogTestContext.Until(() => test.Target.Current == InputContext.Menu, "Blur restores modal context");
            Field.Focus();
            yield return DialogTestContext.Until(() => test.Target.Current == InputContext.TextEntry, "Refocus restores typing context");
        }
    }
}
