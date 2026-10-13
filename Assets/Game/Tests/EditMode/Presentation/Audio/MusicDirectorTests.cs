using Game.Core;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation.Audio
{
    /// <summary>AUD-008's six required pure-director checks use only simulated time.</summary>
    public sealed class MusicDirectorTests
    {
        static MusicDirector Director() => new MusicDirector(new ContentHash(1), new ContentHash(2), new ContentHash(3), new ContentHash(4));
        static MusicCommand Step(MusicDirector director, MusicContext context = MusicContext.World, int signals = 0, float dt = 0,
            bool overridden = false, ulong cue = 99, float fade = .25f)
            => director.Update(context, signals, overridden, new ContentHash(cue), fade, dt);

        [Test]
        public void MenuScene_PlaysMenu()
        {
            var director = Director(); var command = Step(director, MusicContext.Menu);
            Assert.IsTrue(command.Change); Assert.AreEqual(MusicState.Menu, command.Target);
            Assert.AreEqual(new ContentHash(1), command.Cue); Assert.AreEqual(2, command.Fade);
            Assert.IsFalse(Step(director, MusicContext.Menu, dt: 1).Change);
            command = Step(director, MusicContext.Editor);
            Assert.IsTrue(command.Change); Assert.AreEqual(MusicState.Editor, command.Target); Assert.AreEqual(new ContentHash(4), command.Cue);
            Step(director, MusicContext.Silent); Assert.IsTrue(Step(director, MusicContext.Menu).Change, "Re-entry requests a fresh clip.");
        }
        [Test]
        public void ThreeSignalsInTwoSeconds_EntersCombat()
        {
            var director = Director(); Assert.AreEqual(MusicState.Exploration, Step(director).Target);
            Assert.IsFalse(Step(director, signals: 1).Change);
            Assert.IsFalse(Step(director, signals: 1, dt: 1).Change);
            var command = Step(director, signals: 1, dt: 1);
            Assert.IsTrue(command.Change); Assert.AreEqual(MusicState.Combat, command.Target);
            Assert.AreEqual(new ContentHash(3), command.Cue); Assert.AreEqual(1, command.Fade);
            Assert.IsFalse(Step(director, dt: .5f).Change);
            Assert.AreEqual(MusicState.Combat, Step(Director(), signals: 4096).Target, "A frame's coalesced count can exceed the bounded timestamp ring.");
        }
        [Test]
        public void TwoSignals_StaysExploration()
        {
            var director = Director(); Step(director, signals: 1);
            var command = Step(director, signals: 1, dt: 1);
            Assert.IsFalse(command.Change); Assert.AreEqual(MusicState.Exploration, command.Target);
            Assert.AreEqual(new ContentHash(2), command.Cue);
            Assert.AreEqual(MusicState.Exploration, Step(director, signals: 1, dt: 2.5f).Target, "Expired signals do not enter combat.");
            Step(director, MusicContext.Menu);
            Assert.AreEqual(MusicState.Exploration, Step(director, signals: 2).Target, "Leaving World clears its history.");
        }
        [Test]
        public void EightSecondsQuiet_LeavesCombat()
        {
            var director = Director(); Step(director, signals: 3);
            Assert.IsFalse(Step(director, dt: 7.5f).Change);
            var command = Step(director, dt: .5f);
            Assert.IsTrue(command.Change); Assert.AreEqual(MusicState.Exploration, command.Target); Assert.AreEqual(4, command.Fade);
            Step(director, signals: 3); Step(director, signals: 1, dt: 7.5f);
            Assert.AreEqual(MusicState.Combat, Step(director, dt: 7.5f).Target, "A lone later signal extends the hold without re-entering.");
            Assert.AreEqual(MusicState.Exploration, Step(director, dt: .5f).Target);
        }
        [Test]
        public void Override_BeatsCombat()
        {
            var director = Director(); Step(director, signals: 3);
            var command = Step(director, overridden: true);
            Assert.IsTrue(command.Change); Assert.AreEqual(MusicState.Override, command.Target);
            Assert.AreEqual(new ContentHash(99), command.Cue); Assert.AreEqual(.25f, command.Fade);
            command = Step(director, overridden: true, cue: 98);
            Assert.IsFalse(command.Change, "Same-state cue replacement is applied by the service, not a fabricated state transition.");
            Assert.AreEqual(new ContentHash(98), command.Cue);
            Assert.AreEqual(MusicState.Combat, Step(director).Target, "Clearing an override restores held combat.");
            Step(director, overridden: true, dt: 8);
            Assert.AreEqual(MusicState.Exploration, Step(director).Target, "Quiet time also ages during an override.");
        }
        [Test]
        public void SilentContext_BeatsOverride()
        {
            var director = Director(); Step(director, signals: 3, overridden: true);
            var command = Step(director, MusicContext.Silent, overridden: true, fade: .1f);
            Assert.IsTrue(command.Change); Assert.AreEqual(MusicState.Silent, command.Target);
            Assert.IsTrue(command.Cue.IsNone); Assert.AreEqual(2, command.Fade, "Silent uses fadeDefault.");
            Assert.IsFalse(Step(director, MusicContext.Silent, overridden: true).Change);
            Assert.AreEqual(MusicState.Override, Step(director, overridden: true).Target);
            Assert.AreEqual(MusicState.Exploration, Step(director).Target, "Old World signals do not survive Silent.");
            Step(director, signals: 3); director.Reset();
            Assert.AreEqual(MusicState.Exploration, Step(director).Target, "Reset starts fresh in the current supplied context.");
        }
    }
}
