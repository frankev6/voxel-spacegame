using Game.Core.Events;
using Game.Narrative.Dialog;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation
{
    public sealed class DialogPresenterTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void Numbering_SkipsHidden()
        {
            using var f = new DialogPresenterFixture();
            var choices = new DialogOption[12];
            choices[0] = new DialogOption { Id = 5, Text = "Hidden", HideWhenUnavailable = true };
            choices[1] = new DialogOption { Id = 7, Text = "Grey" };
            for (int i = 2; i < choices.Length; i++) choices[i] = new DialogOption { Id = 7 + i, Text = "Shown", Available = true, IsQuest = i == 2 };
            f.Start(host => host.OnOptions(choices, choices.Length));
            Assert.AreEqual(11, f.Model.Options.Count); Assert.AreEqual("1", f.Model.Options[0].NumberLabel);
            Assert.IsFalse(f.Model.Options[0].IsAvailable); Assert.IsFalse(f.Model.Options[0].Enabled);
            Assert.AreEqual("2", f.Model.Options[1].NumberLabel); Assert.IsTrue(f.Model.Options[1].IsQuest);
            Assert.AreEqual("9", f.Model.Options[8].NumberLabel); Assert.AreEqual("", f.Model.Options[9].NumberLabel);
            Assert.AreEqual(0, f.Model.Options[9].Number);
            f.Presenter.ChooseByNumber(1); f.Presenter.ChooseByNumber(0); Assert.AreEqual(2, f.Sounds.Errors);
            // The runner's array is reusable; presenter owns its displayed copies.
            choices[2].Id = 999; choices[2].Text = "Changed";
            Assert.AreEqual(9, f.Model.Options[1].OptionId); Assert.AreEqual("Shown", f.Model.Options[1].Text);
            f.Presenter.Choose(2); Assert.AreEqual(0, f.Script.Selections);
            f.Presenter.Tick(); Assert.AreEqual(9, f.Script.Selected); Assert.AreEqual(1, f.Script.Selections);
        }
        [Test] public void AllUnavailable_AddsEndConversation()
        {
            using var f = new DialogPresenterFixture();
            var choices = new[] { new DialogOption { Text = "Grey" }, new DialogOption { HideWhenUnavailable = true } };
            DialogOutcome outcome = DialogOutcome.Completed;
            f.Service.Ended += ended => { outcome = ended.Outcome; Assert.IsFalse(f.Presenter.View.IsOpen); };
            f.Start(host => host.OnOptions(choices, choices.Length));
            Assert.AreEqual(2, f.Model.Options.Count); Assert.AreEqual(1, f.Model.Options[1].Synthetic);
            Assert.AreEqual("2", f.Model.Options[1].NumberLabel); Assert.IsTrue(f.Model.Options[1].IsAvailable);
            Assert.AreEqual("", f.Model.Options[1].Text, "The view owns the static synthetic label.");
            f.Presenter.ChooseByNumber(2); Assert.AreEqual(DialogOutcome.Aborted, outcome);
            Assert.IsFalse(f.Service.IsActive); Assert.AreEqual(0, f.Context.Depth);
        }
        [Test] public void Back_ChoosesLeave()
        {
            using (var f = new DialogPresenterFixture())
            {
                var choices = new[] { new DialogOption { Id = 10, IsLeave = true },
                    new DialogOption { Id = 11, IsLeave = true, Available = true }, new DialogOption { Id = 12, IsLeave = true, Available = true } };
                f.Start(host => host.OnOptions(choices, choices.Length));
                Assert.IsTrue(f.Presenter.View.OnBack()); Assert.AreEqual(0, f.Script.Selections);
                f.Presenter.Tick(); Assert.AreEqual(11, f.Script.Selected);
            }
            using (var f = new DialogPresenterFixture())
            {
                var choices = new[] { new DialogOption { Id = 1, Available = true } };
                f.Start(host => host.OnOptions(choices, choices.Length));
                Assert.IsTrue(f.Presenter.Back()); f.Presenter.Tick();
                Assert.AreEqual(0, f.Script.Selections); Assert.IsTrue(f.Presenter.View.IsOpen);
            }
        }
        [Test] public void EndAcknowledge_OnlyAfterLines()
        {
            using (var f = new DialogPresenterFixture())
            {
                f.Start(host => host.OnLine(DialogPresenterFixture.Line()));
                f.Presenter.Continue(); Assert.IsTrue(f.Service.IsActive); Assert.AreEqual(0, f.Model.Options.Count);
                f.Presenter.Tick(); Assert.IsTrue(f.Service.IsActive); Assert.AreEqual(2, f.Model.Options[0].Synthetic);
                Assert.IsTrue(f.Model.CanContinue); Assert.AreEqual(1, f.Model.Options.Count);
                f.Presenter.Continue(); Assert.IsFalse(f.Service.IsActive); Assert.IsFalse(f.Presenter.View.IsOpen);
            }
            using (var f = new DialogPresenterFixture())
            {
                f.Start(null); Assert.IsFalse(f.Service.IsActive); Assert.AreEqual(0, f.Model.Options.Count);
                Assert.IsFalse(f.Model.CanContinue); Assert.IsFalse(f.Presenter.View.IsOpen);
            }
        }
        [Test] public void PortraitPersistsUntilChanged()
        {
            using var f = new DialogPresenterFixture();
            f.Start(host => host.OnLine(DialogPresenterFixture.Line(portrait: "speaker_nora", illustration: "illustration_known")));
            Assert.AreSame(f.Portrait, f.Model.Portrait); Assert.AreSame(f.Illustration, f.Model.Illustration);
            Assert.AreEqual("NORA", f.Model.Lines[0].SpeakerName); Assert.AreEqual("speaker--nora", f.Model.Lines[0].SpeakerStyleClass);
            f.Service.OnLine(DialogPresenterFixture.Line("second", speaker: "speaker_captain", name: "Explicit"));
            Assert.AreSame(f.Portrait, f.Model.Portrait); Assert.AreSame(f.Illustration, f.Model.Illustration);
            Assert.AreEqual("Explicit", f.Model.Lines[1].SpeakerName);
            f.Service.OnLine(DialogPresenterFixture.Line("third", "speaker_captain", "illustration_empty"));
            Assert.IsNull(f.Model.Portrait); Assert.IsNull(f.Model.Illustration);
            Assert.IsFalse(f.Model.HasPortrait); Assert.IsFalse(f.Model.HasIllustration);
            var pooled = f.Model.Lines[2];
            var choices = new[] { new DialogOption { Id = 1, Available = true } };
            f.Service.OnOptions(choices, 1); f.Presenter.ChooseOption(0); f.Presenter.Tick();
            Assert.AreEqual(0, f.Model.Lines.Count); Assert.AreEqual(0, f.Model.ScrollToLine);
            f.Start(host => host.OnLine(DialogPresenterFixture.Line("new")));
            Assert.AreSame(pooled, f.Model.Lines[0]);
            for (int i = 0; i < 40; i++) f.Service.OnLine(DialogPresenterFixture.Line());
            Assert.AreEqual(32, f.Model.Lines.Count);
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Loaded)); EventHub.DrainAll();
            Assert.IsFalse(f.Service.IsActive); Assert.IsFalse(f.Presenter.View.IsOpen);
            f.Presenter.Dispose(); f.Start(host => host.OnLine(DialogPresenterFixture.Line()));
            Assert.AreEqual(0, f.Model.Lines.Count, "Disposed presenters unsubscribe from service callbacks.");
            f.Service.Interrupt();
        }
    }
}
