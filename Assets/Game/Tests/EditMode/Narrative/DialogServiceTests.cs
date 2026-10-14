using System;
using System.Reflection;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Nodes;
using Game.Data.Validation;
using Game.Narrative.Dialog;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class DialogServiceTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        internal sealed class Context : ILogicContext, ILogicEffectHost
        {
            readonly FakeLogicContext inner;
            public Context(FactCatalog catalog) => inner = new FakeLogicContext(catalog);
            public ulong Seed = 40004;
            public int Depth;
            public Action BlockingCommandCompleted { get; set; }
            public void EffectRejected() { }
            public ILogicFacts Facts => inner.Facts;
            public ILogicEconomy Economy => inner.Economy;
            public ILogicNarrative Narrative => inner.Narrative;
            public ILogicWorld World => inner.World;
            public LogicScope Scope => inner.Scope;
            public ulong CampaignSeed => Seed;
            public void PushScope(in LogicScope scope) { inner.PushScope(scope); Depth++; }
            public void PopScope() { inner.PopScope(); Depth--; }
        }
        internal static DialogCommandTable Table(params (string text, IEffect effect)[] commands)
        {
            var definition = TestContentFactory.Create<DialogCommandTableDefinition>("dialog_test");
            var entries = new DialogCommandEntry[commands.Length];
            for (int i = 0; i < commands.Length; i++)
            {
                var list = new CompiledEffectList(); list.SetCompiled(new[] { commands[i].effect }, false);
                entries[i] = new DialogCommandEntry { Text = commands[i].text, Effect = list };
            }
            typeof(DialogCommandTableDefinition).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, entries);
            return new DialogCommandTable(definition);
        }
        static Context ContextForTest() => new Context(FactStoreTests.Create().Catalog);
        static DialogRequest Request => new DialogRequest(0, "start", default);
        [Test] public void Lines_PumpedNotReentrant()
        {
            var steps = new Action<IDialogRunnerHost>[20];
            for (int i = 0; i < steps.Length; i++) steps[i] = FakeDialogRunner.Line(i.ToString());
            var runner = new FakeDialogRunner(steps); var context = ContextForTest();
            var service = new DialogService(runner, Table(), context, new DialogMemory()); int lines = 0;
            service.LinePresented += _ => { lines++; service.Pump(); };
            Assert.IsTrue(service.TryStart(Request)); Assert.AreEqual(1, lines);
            service.Pump(); Assert.AreEqual(17, lines); Assert.AreEqual(16, runner.Continues);
            service.Pump(); Assert.AreEqual(20, lines); Assert.AreEqual(1, runner.MaxDepth);
            Assert.IsTrue(service.IsActive); service.AcknowledgeEnd(); Assert.IsFalse(service.IsActive); Assert.AreEqual(0, context.Depth);
        }
        [Test] public void Terminal_EndsThenReturnsEffect()
        {
            var effect = new Effect(EffectFlow.Terminal); FakeDialogRunner runner = null;
            runner = new FakeDialogRunner(host => { Assert.AreEqual(DialogCommandResult.Stop, host.OnCommand("terminal")); runner.Stop(); },
                _ => Assert.Fail("instruction after terminal ran"));
            var context = ContextForTest(); var service = new DialogService(runner, Table(("terminal", effect)), context, new DialogMemory());
            int ended = 0;
            service.Ended += result => { ended++; Assert.AreSame(effect, result.PendingTerminal); Assert.IsFalse(service.IsActive); Assert.AreEqual(0, context.Depth); };
            service.TryStart(Request); service.Pump(); Assert.AreEqual(1, ended); Assert.AreEqual(0, effect.Applied);
        }
        [Test] public void AbortAndInterrupt_Outcomes()
        {
            FakeDialogRunner runner = null;
            runner = new FakeDialogRunner(host => { Assert.AreEqual(DialogCommandResult.Continue, host.OnCommand("abort")); runner.Stop(); });
            var service = new DialogService(runner, Table(("abort", new AbortBeatEffect())), ContextForTest(), new DialogMemory());
            DialogEnded result = default; service.Ended += value => result = value;
            service.TryStart(Request); Assert.AreEqual(DialogOutcome.Aborted, result.Outcome);
            var lineRunner = new FakeDialogRunner(FakeDialogRunner.Line("waiting"));
            service = new DialogService(lineRunner, Table(), ContextForTest(), new DialogMemory()); service.Ended += value => result = value;
            service.TryStart(Request); service.Interrupt(); Assert.AreEqual(DialogOutcome.Interrupted, result.Outcome); Assert.IsNull(result.PendingTerminal);
            service.TryStart(Request); service.AbortByPlayer(); Assert.AreEqual(DialogOutcome.Aborted, result.Outcome); Assert.IsFalse(service.IsActive);
        }
        [Test] public void Blocking_ResumesAfterClose()
        {
            Action close = null; var context = ContextForTest(); Action prior = () => { }; context.BlockingCommandCompleted = prior;
            var effect = new Effect(EffectFlow.Blocking) { Action = ctx => close = ((ILogicEffectHost)ctx).BlockingCommandCompleted };
            var runner = new FakeDialogRunner(host => Assert.AreEqual(DialogCommandResult.Block, host.OnCommand("merchant")), host => Assert.AreEqual(DialogCommandResult.Block, host.OnCommand("merchant")), FakeDialogRunner.Line("closed"));
            var service = new DialogService(runner, Table(("merchant", effect)), context, new DialogMemory());
            service.TryStart(Request); service.Pump(); Assert.AreEqual(0, runner.Resumes);
            Action oldClose = close;
            close(); Assert.AreEqual(0, runner.Resumes); service.Pump(); Assert.AreEqual(1, runner.Resumes);
            oldClose(); service.Pump(); Assert.AreEqual(1, runner.Resumes, "Old merchant cannot unblock the next one");
            close(); service.Pump(); Assert.AreEqual(2, runner.Resumes);
            service.AcknowledgeEnd(); Assert.AreSame(prior, context.BlockingCommandCompleted);
            service.TryStart(Request); service.Interrupt(); close(); service.Pump(); Assert.AreEqual(2, runner.Resumes);
        }
        [Test] public void Roll_MemoizedAndDeterministic()
        {
            int[] Sequence(ulong seed)
            {
                var context = ContextForTest(); context.Seed = seed;
                var service = new DialogService(new FakeDialogRunner(FakeDialogRunner.Line("roll")), Table(), context, new DialogMemory());
                service.TryStart(Request); var values = new int[8];
                for (int i = 0; i < values.Length; i++)
                { service.OnNodeStarted("node" + i); values[i] = service.Roll(100); Assert.AreEqual(values[i], service.Roll(100)); }
                int caller = service.Roll(100);
                service.OnNodeStarted("detour"); service.Roll(100); service.OnNodeCompleted("detour");
                Assert.AreEqual(caller, service.Roll(100), "Returning to caller restores its roll memo");
                service.Interrupt(); return values;
            }
            CollectionAssert.AreEqual(Sequence(4), Sequence(4)); CollectionAssert.AreNotEqual(Sequence(4), Sequence(5));
        }
        [Test] public void UnknownCommand_Interrupts()
        {
            FakeDialogRunner runner = null;
            runner = new FakeDialogRunner(host => { Assert.AreEqual(DialogCommandResult.Stop, host.OnCommand("unknown")); runner.Stop(); });
            var service = new DialogService(runner, Table(), ContextForTest(), new DialogMemory()); DialogEnded result = default;
            service.Ended += value => result = value;
            LogAssert.Expect(LogType.Error, "[Dialog] command not in table: unknown");
            service.TryStart(Request); Assert.AreEqual(DialogOutcome.Interrupted, result.Outcome); Assert.IsFalse(service.IsActive);
        }
        [Test] public void SaveParticipant_RoundTrip()
        {
            var memory = new DialogMemory(); memory.IncrementVisit("z"); memory.IncrementVisit("a"); memory.SetRaw("$Yarn.Internal.x", -7);
            var participant = new DialogSaveParticipant(memory);
            byte[] Capture() { using var writer = new SaveWriter(); participant.Capture(writer); return writer.ToArray(); }
            byte[] saved = Capture(); memory.Clear(); participant.Restore(new SaveReader(saved), 1); CollectionAssert.AreEqual(saved, Capture());
            Assert.AreEqual(1, memory.GetVisitCount("z")); Assert.AreEqual(-7, memory.GetRaw("$Yarn.Internal.x"));
            participant.Restore(SaveReader.Empty, 0); Assert.AreEqual(0, memory.GetVisitCount("z")); Assert.AreEqual(4, Capture().Length);
        }
        sealed class Effect : IEffect
        {
            public Effect(EffectFlow flow) => Flow = flow;
            public int Applied; public Action<ILogicContext> Action;
            public EffectFlow Flow { get; }
            public void Apply(ILogicContext context) { Applied++; Action?.Invoke(context); }
            public bool CanApply(ILogicContext context) => true;
            public void Validate(ValidationReport report) { }
            public void Bind(LogicBinder binder) { }
            public void AddRequirements(EffectRequirements requirements) { }
        }
    }
}
