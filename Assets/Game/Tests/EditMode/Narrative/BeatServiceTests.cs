using System;
using System.Collections.Generic;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Narrative;
using Game.Narrative.Beats;
using Game.Narrative.Dialog;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class BeatServiceTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        internal static StoryBeatDefinition Beat(string id, BeatTrigger trigger = BeatTrigger.Interact, BeatRepeat repeat = BeatRepeat.Once, int salience = 0, string node = "") =>
            TestContentFactory.Create<StoryBeatDefinition>(id, so =>
            {
                so.FindProperty("trigger").intValue = (int)trigger; so.FindProperty("repeat").intValue = (int)repeat;
                so.FindProperty("salience").intValue = salience; so.FindProperty("yarnNode").stringValue = node;
                var tags = so.FindProperty("where.bodyTags"); tags.arraySize = 1; tags.GetArrayElementAtIndex(0).stringValue = "test";
            });
        internal sealed class Fixture : IDisposable
        {
            internal readonly ContentCatalog Content;
            internal readonly FactStore Facts;
            internal readonly LogicContext Context;
            internal readonly DialogService Dialog;
            internal readonly BeatService Beats;
            internal readonly FakeDialogRunner Runner;
            internal Fixture(StoryBeatDefinition[] beats, Action<DialogEnded> beforeEnded = null, FakeDialogRunner runner = null, DialogCommandTable commands = null)
            {
                var definitions = FactCatalogTests.BaseContent(); definitions.AddRange(beats);
                definitions.Add(FactCatalogTests.Facts("facts_beats", new FactDeclaration { Name = "story_ready", Kind = FactKind.Bool }));
                Content = FactCatalogTests.Content(definitions); var refs = RefResolverRegistry.CreateDefault(Content);
                var report = new ValidationReport("beat fixture"); var facts = FactCatalog.Build(Content, refs, report);
                var compiler = new LogicCompiler(facts, refs, new LogicFunctionRegistry()); var binder = new LogicBinder(facts, refs, report);
                foreach (var beat in beats) { beat.CompileLogic(compiler, report.For(beat.IdString)); beat.BindLogic(binder); }
                Assert.IsFalse(report.HasErrors, report.ToText()); Facts = new FactStore(facts);
                var owner = new FakeLogicContext(facts); Context = new LogicContext(Facts, owner, owner, owner, () => 7);
                Runner = runner ?? new FakeDialogRunner(FakeDialogRunner.Line("Page"));
                Dialog = new DialogService(Runner, commands ?? DialogServiceTests.Table(), Context, new DialogMemory());
                if (beforeEnded != null) Dialog.Ended += beforeEnded;
                Beats = new BeatService(Content, Facts, Context, Dialog, _ => 4, _ => "test", 7);
            }
            internal int Index(StoryBeatDefinition beat) => Content.IndexOf(beat);
            internal int Count(StoryBeatDefinition beat) => Facts.GetBySlot(Facts.Catalog.GeneratedSlot(FactFamily.Beat, beat.Id.Hash.Value));
            internal void Finish() { Dialog.Pump(); Dialog.AcknowledgeEnd(); }
            public void Dispose() { Dialog.Interrupt(); Beats.Dispose(); }
        }
        static LogicBodyHandle Body(ushort slot = 1) => new(4, 1, slot, 0, default);
        [Test] public void Salience_And_TieBreak()
        {
            StoryBeatDefinition low = Beat("beat_low", repeat: BeatRepeat.Always), a = Beat("beat_a", repeat: BeatRepeat.Always, salience: 5, node: "start"), b = Beat("beat_b", repeat: BeatRepeat.Always, salience: 5, node: "start");
            using var f = new Fixture(new[] { low, b, a }); var picked = new HashSet<int>(); int total = f.Facts.Catalog.SlotOf("sys_beats_completed");
            for (int state = 0; state < 24; state++)
            {
                f.Facts.SetBySlot(total, state, FactWriteSource.Beat); Assert.IsTrue(f.Beats.OnBodyInteracted(Body(), true));
                int chosen = f.Beats.CurrentBeatIndex; Assert.AreNotEqual(f.Index(low), chosen); picked.Add(chosen); f.Dialog.Interrupt();
                Assert.IsTrue(f.Beats.OnBodyInteracted(Body(), true)); Assert.AreEqual(chosen, f.Beats.CurrentBeatIndex); f.Dialog.Interrupt();
            }
            Assert.AreEqual(2, picked.Count);
        }
        [Test] public void Once_NeverReplays()
        {
            var beat = Beat("beat_once"); using var f = new Fixture(new[] { beat });
            Assert.IsTrue(f.Beats.OnBodyInteracted(Body(), true)); Assert.AreEqual(1, f.Count(beat));
            var saved = f.Facts.Capture(); f.Facts.Restore(saved); f.Beats.OnRestored();
            Assert.IsFalse(f.Beats.OnBodyInteracted(Body(), true)); Assert.AreEqual(1, f.Count(beat));
        }
        [Test] public void OncePerBody()
        {
            var beat = Beat("beat_body", repeat: BeatRepeat.OncePerBody); using var f = new Fixture(new[] { beat });
            Assert.IsTrue(f.Beats.OnBodyInteracted(Body(), true)); Assert.IsFalse(f.Beats.OnBodyInteracted(Body(), true));
            Assert.IsTrue(f.Beats.OnBodyInteracted(Body(2), true)); Assert.AreEqual(2, f.Count(beat));
        }
        [Test] public void GameStart_Order()
        {
            StoryBeatDefinition a = Beat("beat_a", BeatTrigger.GameStart, salience: 2), b = Beat("beat_b", BeatTrigger.GameStart, salience: 2), c = Beat("beat_c", BeatTrigger.GameStart, salience: 4);
            using var f = new Fixture(new[] { b, c, a }); f.Beats.OnNewGame(); f.Beats.AfterEvaluation(false); Assert.AreEqual(0, f.Count(c));
            f.Beats.AfterEvaluation(true); Assert.AreEqual(1, f.Count(c)); Assert.AreEqual(0, f.Count(a)); Assert.AreEqual(2, f.Beats.QueuedCount);
            f.Beats.AfterEvaluation(true); Assert.AreEqual(1, f.Count(a)); Assert.AreEqual(0, f.Count(b));
            f.Beats.AfterEvaluation(true); Assert.AreEqual(1, f.Count(b));

            // QST-010 live regression: streaming queues arrival before the first free
            // narrative frame. Intro and its terminal follow-up precede that FIFO.
            var intro = Beat("beat_intro", BeatTrigger.GameStart, node: "start");
            var followup = Beat("beat_starter", BeatTrigger.Manual);
            var arrival = Beat("beat_arrival", BeatTrigger.StarSystemEntered);
            var ambient = Beat("beat_ambient", BeatTrigger.Manual);
            var terminal = new Terminal(); FakeDialogRunner runner = null;
            runner = new FakeDialogRunner(host => { host.OnCommand("terminal"); runner.Stop(); });
            using var opening = new Fixture(new[] { intro, followup, arrival, ambient }, runner: runner,
                commands: DialogServiceTests.Table(("terminal", terminal)));
            terminal.Action = _ =>
            {
                Assert.IsFalse(opening.Dialog.IsActive); Assert.AreEqual(0, opening.Beats.CurrentBeatIndex);
                opening.Beats.Queue(opening.Index(followup));
            };
            opening.Beats.OnNewGame(); opening.Beats.OnStarSystemEntered(4, true);
            opening.Beats.Queue(opening.Index(ambient)); opening.Beats.AfterEvaluation(false);
            opening.Beats.AfterEvaluation(true);
            Assert.AreEqual(1, opening.Count(intro)); Assert.AreEqual(0, opening.Count(arrival));
            opening.Beats.AfterEvaluation(true);
            Assert.AreEqual(1, opening.Count(followup)); Assert.AreEqual(0, opening.Count(arrival));
            opening.Beats.AfterEvaluation(true);
            Assert.AreEqual(1, opening.Count(arrival)); Assert.AreEqual(0, opening.Count(ambient));
            opening.Beats.AfterEvaluation(true); Assert.AreEqual(1, opening.Count(ambient));
        }
        [Test] public void ConditionBeat_QueuesAndPlays()
        {
            var beat = Beat("beat_condition", BeatTrigger.Condition); LogicTestEnvironment.Source(beat.Condition, "$story_ready");
            using var f = new Fixture(new[] { beat }); f.Beats.Evaluate(Array.Empty<LogicDependency>(), true); Assert.AreEqual(0, f.Beats.QueuedCount);
            int slot = f.Facts.Catalog.SlotOf("story_ready"); f.Facts.SetBySlot(slot, 1, FactWriteSource.Dialog);
            f.Beats.Evaluate(new[] { new LogicDependency(LogicDependencyKind.Fact, slot) }, false);
            f.Beats.AfterEvaluation(false); Assert.AreEqual(1, f.Beats.QueuedCount); Assert.AreEqual(0, f.Count(beat));
            f.Beats.AfterEvaluation(true); Assert.AreEqual(1, f.Count(beat)); f.Beats.OnRestored(); f.Beats.Evaluate(Array.Empty<LogicDependency>(), true); Assert.AreEqual(0, f.Beats.QueuedCount);
        }
        [Test] public void Count_OnlyOnCompleted()
        {
            var beat = Beat("beat_count", repeat: BeatRepeat.Always, node: "start"); using var f = new Fixture(new[] { beat });
            f.Beats.DebugPlay(f.Index(beat)); f.Beats.AbortCurrent(); f.Finish(); Assert.AreEqual(0, f.Count(beat));
            f.Beats.DebugPlay(f.Index(beat)); f.Dialog.AbortByPlayer(); Assert.AreEqual(0, f.Count(beat));
            f.Beats.DebugPlay(f.Index(beat)); f.Finish(); Assert.AreEqual(1, f.Count(beat)); Assert.AreEqual(0, f.Beats.CurrentBeatIndex);
        }
        [Test] public void NestedComplete()
        {
            StoryBeatDefinition outer = Beat("beat_outer", node: "start"), nested = Beat("beat_nested"); using var f = new Fixture(new[] { outer, nested });
            f.Beats.DebugPlay(f.Index(outer)); f.Beats.CompleteBeat(f.Index(nested)); Assert.AreEqual(1, f.Count(nested)); Assert.AreEqual(0, f.Count(outer));
            f.Beats.CompleteBeat(f.Index(outer)); f.Finish(); Assert.AreEqual(1, f.Count(outer)); Assert.AreEqual(1, f.Count(nested));
        }
        sealed class Terminal : IEffect
        {
            internal Action<ILogicContext> Action;
            public EffectFlow Flow => EffectFlow.Terminal;
            public bool CanApply(ILogicContext context) => true;
            public void Apply(ILogicContext context) => Action(context);
            public void Validate(ValidationReport report) { }
            public void Bind(LogicBinder binder) { }
            public void AddRequirements(EffectRequirements requirements) { }
        }
        [Test] public void Terminal_AfterPop()
        {
            bool popped = false, applied = false; var effect = new Terminal(); FakeDialogRunner runner = null;
            runner = new FakeDialogRunner(host => { host.OnCommand("terminal"); runner.Stop(); });
            var beat = Beat("beat_terminal", node: "start");
            using var f = new Fixture(new[] { beat }, _ => popped = true, runner, DialogServiceTests.Table(("terminal", effect)));
            effect.Action = context => { Assert.IsTrue(popped); Assert.IsFalse(f.Dialog.IsActive); Assert.AreEqual(0, f.Beats.CurrentBeatIndex); Assert.AreEqual(LogicUsage.BeatEffect, context.Scope.Usage); Assert.IsTrue(context.Scope.HasBody); Assert.AreEqual(Body().Key, context.Scope.Body.Key); applied = true; };
            f.Beats.OnBodyInteracted(Body(), true); Assert.IsTrue(applied); Assert.AreEqual(1, f.Count(beat));
        }
        [Test] public void TryStartFails_Requeues()
        {
            var beat = Beat("beat_retry", node: "missing"); using var f = new Fixture(new[] { beat });
            LogAssert.Expect(LogType.Error, "[Dialog] unknown node: missing"); f.Beats.DebugPlay(f.Index(beat));
            Assert.AreEqual(1, f.Beats.QueuedCount); Assert.AreEqual(0, f.Beats.CurrentBeatIndex); Assert.AreEqual(0, f.Count(beat));
            TestContentFactory.Edit(beat, so => so.FindProperty("yarnNode").stringValue = "start"); f.Beats.AfterEvaluation(true); f.Finish();
            Assert.AreEqual(1, f.Count(beat)); Assert.AreEqual(0, f.Beats.QueuedCount);
        }
    }
}
