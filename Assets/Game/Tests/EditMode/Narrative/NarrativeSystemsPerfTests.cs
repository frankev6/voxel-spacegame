using System;
using System.Diagnostics;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.PerformanceTesting;
using static Game.Tests.EditMode.Data.Logic.LogicTestEnvironment;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class NarrativeSystemsPerfTests
    {
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        [Test, Performance, Category("Performance")] public void OneChange50Objectives()
        {
            var definitions = new QuestDefinition[5];
            for (int q = 0; q < definitions.Length; q++)
            {
                var stage = QuestServiceTests.Stage("one", "$story_count >= 100000");
                for (int i = 1; i < 10; i++) stage.objectives.Add(new QuestObjective {
                    id = "objective_" + i, doneWhen = Source(new CompiledCondition(), "$story_count >= 100000") });
                definitions[q] = QuestServiceTests.Quest("quest_perf_" + q, stage);
            }
            var f = new QuestServiceTests.Fixture(definitions);
            foreach (var q in definitions) f.Quests.Start(f.Index(q));
            for (int i = 1; i <= 10; i++) { f.Set("story_count", i); f.Tick(); }
            var samples = new double[10]; long allocations = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                f.Set("story_count", i + 11); long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                f.Tick(); samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                allocations += GC.GetAllocatedBytesForCurrentThread() - bytes;
            }
            foreach (var q in definitions) Assert.AreEqual(QuestStatus.Active, f.Quests.GetStatus(f.Index(q)));
            PerfRun.Gate("QuestOneChange50Objectives", samples, .1);
            PerfRun.Gate("QuestOneChange50ObjectivesGC", new[] { (double)allocations }, 0, "B");
        }
    }
}
