using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core.Events;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using Game.Narrative;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.Narrative
{
    [Category("Performance")]
    public sealed class NarrativeServicePerfTests
    {
        [TearDown] public void Cleanup() => EventHub.Reset();
        [Test] public void Tick_Idle() => Measure(false);
        [Test] public void Tick_OneChange50Dependents() => Measure(true);
        static void Measure(bool change)
        {
            using var environment = new LogicTestEnvironment();
            var facts = new FactStore(environment.Facts, 1);
            var context = new FakeLogicContext(environment.Facts);
            int slot = environment.Facts.SlotOf("story_count");
            var service = new NarrativeService(facts, context);
            var system = new Conditions(environment, context);
            service.Register(system); service.BeginNewGame(); service.SetWorldReady(true); service.Tick();
            void Tick(int value)
            {
                if (change)
                {
                    facts.SetBySlot(slot, value, FactWriteSource.Debugger);
                    context.Values[slot] = value;
                }
                service.Tick();
                EventBus<FactChanged>.Drain();
            }
            for (int i = 0; i < 100; i++) Tick((i + 1) & 1);
            var samples = new double[5];
            for (int run = 0; run < samples.Length; run++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 100; i++) Tick((i + 1) & 1);
                samples[run] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / 100;
                Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - bytes);
            }
            Assert.AreEqual(change ? 50 * 601 : 50, system.Evaluations);
            PerfRun.Gate(change ? "Tick_OneChange50Dependents" : "Tick_Idle", samples, change ? .2 : .02);
        }
        sealed class Conditions : INarrativeSystem
        {
            readonly CompiledCondition[] conditions = new CompiledCondition[50];
            readonly DependencyIndex index;
            readonly HashSet<int> dirty = new(50);
            readonly ILogicContext context;
            public int Evaluations;
            public int Order => 0;
            public Conditions(LogicTestEnvironment environment, ILogicContext context)
            {
                this.context = context;
                var dependencies = new IReadOnlyList<LogicDependency>[conditions.Length];
                for (int i = 0; i < conditions.Length; i++)
                {
                    conditions[i] = LogicTestEnvironment.Source(new CompiledCondition(), "$story_count >= 0");
                    var report = new ValidationReport("tick fixture");
                    Assert.IsTrue(environment.Compiler.Compile(conditions[i],
                        new LogicCompileOptions(LogicUsage.BeatCondition, "tick_fixture", "condition"), report), report.ToText());
                    var list = new List<LogicDependency>(); conditions[i].CollectDependencies(list); dependencies[i] = list;
                }
                index = new DependencyIndex(dependencies);
            }
            public void OnNewGame() { }
            public void OnRestored() { }
            public void AfterEvaluation(bool playerFree) { }
            public void Evaluate(IReadOnlyList<LogicDependency> changed, bool fullReevaluate)
            {
                dirty.Clear(); index.Collect(changed, dirty);
                for (int i = 0; i < conditions.Length; i++)
                    if (fullReevaluate || dirty.Contains(i))
                    {
                        if (!conditions[i].Evaluate(context)) throw new InvalidOperationException("Fixture condition must remain true.");
                        Evaluations++;
                    }
            }
        }
    }
}
