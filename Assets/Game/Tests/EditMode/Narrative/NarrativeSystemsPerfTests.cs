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
        [Test, Performance, Category("Performance")] public void Idle()
        {
            using var f = new BeatServiceTests.Fixture(new[] { BeatServiceTests.Beat("beat_idle", BeatTrigger.Manual) });
            var runtime = new Game.Narrative.NarrativeService(f.Facts, f.Context);
            var quests = new Game.Narrative.Quests.QuestService(f.Content, f.Facts, f.Context, runtime);
            var clues = new Game.Narrative.Clues.ClueService(f.Content, f.Facts, f.Context);
            runtime.Register(quests); runtime.Register(clues); runtime.Register(f.Beats);
            runtime.BeginNewGame(); runtime.SetWorldReady(true); runtime.SetPlayerFree(true);
            for (int i = 0; i < 20; i++) runtime.Tick();
            var samples = new double[10]; long allocations = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                runtime.Tick(); samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                allocations += GC.GetAllocatedBytesForCurrentThread() - bytes;
            }
            Assert.AreEqual(0, f.Beats.CurrentBeatIndex);
            PerfRun.Gate("NarrativeSystemsIdle", samples, .02);
            PerfRun.Gate("NarrativeSystemsIdleGC", new[] { (double)allocations }, 0, "B");
        }
        [Test, Performance, Category("Performance")] public void Select40_Time()
        {
            var beats = new StoryBeatDefinition[40];
            for (int i = 0; i < beats.Length; i++) beats[i] = BeatServiceTests.Beat("beat_select_" + i, repeat: BeatRepeat.Always, node: "start");
            using var f = new BeatServiceTests.Fixture(beats);
            var body = new LogicBodyHandle(4, 1, 1, 0, default); var samples = new double[10];
            for (int i = 0; i < 10; i++) { f.Beats.OnBodyInteracted(body, true); f.Dialog.Interrupt(); }
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); bool selected = f.Beats.OnBodyInteracted(body, true);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                Assert.IsTrue(selected); f.Dialog.Interrupt();
            }
            PerfRun.Gate("BeatSelect40", samples, .1);
        }
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
