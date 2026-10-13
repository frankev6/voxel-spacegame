using System.Collections;
using Game.Core;
using Game.UI.Data;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 UI-011: real named controls, Back policy, post-pop callbacks and queued cached dialogs.</summary>
    public sealed class ConfirmDialogTests
    {
        DialogTestContext test;
        [UnitySetUp] public IEnumerator SetUp()
        {
            test = new DialogTestContext();
            yield return DialogTestContext.Until(() => test.UI.Panel != null && test.UI.RootElement.worldBound.width > 0, "Dialog panel layout");
        }
        [UnityTearDown] public IEnumerator TearDown() { test.Dispose(); yield return null; }
        [UnityTest] public IEnumerator ThreeOptions_RenderInOrder()
        {
            int chosen = -1;
            test.UI.Confirm.Show(new ConfirmRequest
            {
                Title = "Commit changes", Body = "Choose what to keep", DefaultFocusIndex = 1, CancelIndex = 2,
                Options = new[]
                {
                    new ConfirmOption { Label = "Commit", ElementName = "confirm-commit", Variant = ButtonVariant.Primary, Enabled = true },
                    new ConfirmOption { Label = "Delete", ElementName = "confirm-delete", Variant = ButtonVariant.Danger, Enabled = false, DisabledReason = "Ship is in use" },
                    new ConfirmOption { Label = "Cancel", Variant = ButtonVariant.Ghost, Enabled = true }
                }
            }, i => chosen = i);
            var root = test.ConfirmView.Root;
            var accept = root.Q<Button>("confirm-commit"); var disabled = root.Q<Button>("confirm-delete"); var cancel = root.Q<Button>("confirm-option-2");
            yield return DialogTestContext.Until(() => accept.text == "Commit" && disabled.text == "Delete" && cancel.text == "Cancel" && cancel.worldBound.width > 0, "Three option bindings/layout");
            Assert.AreSame(accept, root.Q<VisualElement>("confirm-actions")[0]); Assert.AreSame(disabled, root.Q<VisualElement>("confirm-actions")[1]); Assert.AreSame(cancel, root.Q<VisualElement>("confirm-actions")[2]);
            Assert.Less(accept.worldBound.x, disabled.worldBound.x); Assert.Less(disabled.worldBound.x, cancel.worldBound.x);
            Assert.IsTrue(accept.ClassListContains(UIStyleClasses.BtnPrimary)); Assert.IsTrue(disabled.ClassListContains(UIStyleClasses.BtnDanger)); Assert.IsTrue(cancel.ClassListContains(UIStyleClasses.BtnGhost));
            Assert.IsFalse(disabled.enabledInHierarchy); Assert.AreEqual("Ship is in use", disabled.tooltip);
            DialogTestContext.Hover(disabled);
            yield return DialogTestContext.Until(() => test.UI.Tooltips.IsVisible, "Disabled option runtime tooltip");
            DialogTestContext.Leave(disabled);
            yield return DialogTestContext.Until(() => !test.UI.Tooltips.IsVisible, "Disabled option tooltip leaves");
            Assert.AreEqual(InputContext.Menu, test.Target.Current); Assert.AreEqual(1, test.Tokens.ActiveTokenCount); Assert.IsTrue(test.UI.Navigator.WorldPointerBlocked);
            DialogTestContext.Activate(disabled); Assert.AreEqual(-1, chosen); Assert.IsTrue(test.UI.Confirm.IsOpen);
            DialogTestContext.Activate(accept);
            yield return DialogTestContext.Until(() => chosen == 0, "Enabled choice completes"); Assert.IsFalse(test.UI.Confirm.IsOpen);
        }
        [UnityTest] public IEnumerator Back_ChoosesCancelIndex()
        {
            int chosen = -1; bool afterPop = false;
            test.UI.Confirm.Show(DialogTestContext.Confirm(cancel: 1), i => { chosen = i; afterPop = !test.UI.Confirm.IsOpen && test.UI.Navigator.Top == test.Root && !test.Tokens.IsPaused && test.Target.Current == InputContext.Flight; });
            test.UI.HandleBackPressed();
            yield return DialogTestContext.Until(() => chosen == 1, "Back chooses cancellation");
            Assert.IsTrue(afterPop); Assert.IsFalse(test.ConfirmView.IsOpen);
        }
        [UnityTest] public IEnumerator Back_NoCancel_Consumed()
        {
            int calls = 0;
            test.UI.Confirm.Show(DialogTestContext.Confirm(cancel: -1), _ => calls++);
            test.UI.HandleBackPressed(); test.UI.HandleBackPressed();
            Assert.IsTrue(test.UI.Confirm.IsOpen); Assert.AreEqual(0, calls); Assert.AreSame(test.ConfirmView, test.UI.Navigator.Top);
            DialogTestContext.Activate(test.ConfirmView.Root.Q<Button>("confirm-option-0"));
            yield return DialogTestContext.Until(() => calls == 1, "Choice closes a noncancelable modal");
        }
        [UnityTest] public IEnumerator CallbackOpensAnother_Works()
        {
            bool afterPop = false; int first = -1, second = -1;
            test.UI.Confirm.Show(DialogTestContext.Confirm("First"), i =>
            {
                first = i; afterPop = !test.UI.Confirm.IsOpen && test.UI.Navigator.Top == test.Root && !test.Tokens.IsPaused;
                test.UI.Confirm.Show(DialogTestContext.Confirm("Second"), j => second = j);
            });
            var cached = test.ConfirmView.Root;
            DialogTestContext.Activate(cached.Q<Button>("confirm-option-0"));
            yield return DialogTestContext.Until(() => first == 0 && test.ConfirmView.IsOpen && cached.Q<Label>("confirm-title").text == "Second", "Callback opens the next cached confirm");
            Assert.IsTrue(afterPop); Assert.AreSame(cached, test.ConfirmView.Root); Assert.AreEqual(1, test.Tokens.ActiveTokenCount);
            test.UI.HandleBackPressed();
            yield return DialogTestContext.Until(() => second == 1, "Second callback completes");
        }
        [UnityTest] public IEnumerator SecondRequest_Queued()
        {
            int first = -1, second = -1, third = -1;
            test.UI.Confirm.Show(DialogTestContext.Confirm("First"), i => first = i);
            var cached = test.ConfirmView.Root;
            test.UI.Confirm.Show(DialogTestContext.Confirm("Second"), i => second = i);
            test.UI.Confirm.Show(DialogTestContext.Confirm("Third"), i => third = i);
            yield return DialogTestContext.Until(() => cached.Q<Label>("confirm-title").text == "First", "First request remains visible");
            Assert.AreEqual(-1, second); Assert.AreEqual(-1, third);
            test.UI.HandleBackPressed();
            yield return DialogTestContext.Until(() => first == 1 && cached.Q<Label>("confirm-title").text == "Second", "Second queued request");
            Assert.AreEqual(-1, third); Assert.AreSame(cached, test.ConfirmView.Root);
            test.UI.HandleBackPressed();
            yield return DialogTestContext.Until(() => second == 1 && cached.Q<Label>("confirm-title").text == "Third", "Third queued request");
            test.UI.HandleBackPressed();
            yield return DialogTestContext.Until(() => third == 1 && !test.UI.Confirm.IsOpen, "Queue finishes");
        }
    }
}
