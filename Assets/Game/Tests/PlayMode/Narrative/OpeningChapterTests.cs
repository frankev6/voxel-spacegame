using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.Core;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Narrative;
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Dialog;
using Game.Narrative.Quests;
using Game.Simulation.Power;
using Game.Tests.PlayMode.Galaxy;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Narrative
{
    /// <summary>QST-010: actual compiled opening content and production input/save owners.</summary>
    public sealed class OpeningChapterTests : EncounterScenarioFixture
    {
        const string DriftMarker = "# CHECKPOINT_DRIFT_LOAD_DONE";
        const ushort ModifierSource = 42010;
        static string ScenarioPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "Scenarios", "opening_chapter.scenario"));

        [UnityTest] public IEnumerator Playthrough() => Run(false);
        [UnityTest] public IEnumerator SaveLoad_NoReplays() => Run(true);

        IEnumerator Run(bool stopAtDrift)
        {
            // Dependencies are genuine production registrations/content. Missing QST-008 or
            // an unimported reward is a specific failure, never an Ignore or a fixture substitute.
            Assert.IsTrue(Services.TryGet<QuestService>(out _), "QST-008 must register production QuestService.");
            Assert.IsTrue(Services.TryGet<ClueService>(out _), "QST-008 must register production ClueService.");
            Assert.IsTrue(Services.TryGet<BeatService>(out _), "QST-008 must register production BeatService.");
            var program = Catalog.Get<DialogProgramDefinition>(new ContentId("dialog_program"));
            Assert.IsTrue(program.Nodes.Any(node => node.Title == "opening_cache"), "QST-009 real dialogue import must include opening_cache.");
            var survey = Catalog.Get<QuestDefinition>(new ContentId("quest_survey_local_materials"));
            Assert.IsFalse(survey.Repeatable, "The authored opening reward requires a non-repeatable quest.");
            CollectionAssert.AreEqual(new[] { "give_item item_strange_matter_container_full 1" }, survey.OnCompleted.Source,
                "The real quest owns the existing gate reward.");
            Assert.IsFalse(survey.OnCompleted.IsStale || survey.OnCompleted.HasErrors,
                "Lead must compile the Q19 reward change with the real content importer.");
            var host = Service<DevRuntimeHost>();
            var dialog = Service<DialogService>();
            var onceStarts = new Dictionary<int, int>();
            var nodesStarted = new List<string>();
            void Started(DialogRequest request)
            {
                nodesStarted.Add(request.StartNode);
                var definitions = Catalog.All<StoryBeatDefinition>();
                if (request.BeatIndex <= 0 || request.BeatIndex > definitions.Count || definitions[request.BeatIndex - 1].Repeat != BeatRepeat.Once) return;
                onceStarts.TryGetValue(request.BeatIndex, out int starts);
                onceStarts[request.BeatIndex] = starts + 1;
            }
            dialog.Started += Started;
            FixedTicks ticks = null;
            Entity modifiedPlayer = Entity.Null;
            try
            {
                string source = File.ReadAllText(ScenarioPath);
                int marker = source.IndexOf(DriftMarker, StringComparison.Ordinal);
                Assert.Greater(marker, 0, "The second case runs the same route through its actual save/load checkpoint.");
                if (stopAtDrift) source = source.Substring(0, marker);
                int combatBeginLine = Array.FindIndex(source.Split('\n'), line => line == "wait encounters.ships == 3 timeout=10") + 1;
                int combatEndLine = Array.FindIndex(source.Split('\n'), line => line == "interact body=ark_station") + 1;
                var script = ScenarioParser.Parse(source, host.Commands, host.Probes);
                using var runner = host.RunScenario(script, ScenarioPath, 1, Path.Combine(TestDirectory, "scenario-results"));
                double deadline = Time.realtimeSinceStartupAsDouble + script.TimeoutSeconds + 1;
                bool prepared = false;
                int fightTicks = 0;
                while (!runner.Finished && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    // Only compress the real opening fight. All commands still run once through
                    // the production host, input bridge, weapon jobs, physics and defeat owner.
                    if (!prepared && PlayerRoot != Entity.Null && runner.Run.CurrentLine >= combatBeginLine && runner.Run.CurrentLine < combatEndLine)
                    {
                        var probes = Members(new ContentId("encounter_xeno_probe_tutorial").Hash);
                        if (probes.Count == 3 && probes.All(Ready) && Ready(PlayerRoot))
                        {
                            PrepareProbeFight(probes); prepared = true; modifiedPlayer = PlayerRoot;
                            ticks = new FixedTicks(World.DefaultGameObjectInjectionWorld);
                        }
                    }
                    if (ticks != null)
                    {
                        if (runner.Run.CurrentLine >= combatEndLine)
                        { ticks.Dispose(); ticks = null; CancelModifiers(modifiedPlayer); modifiedPlayer = Entity.Null; }
                        else
                        {
                            // A finite tick budget covers the script's 15 + 3*60 tick waits
                            // and actual director defeat readback.
                            Assert.Less(fightTicks, 240, "Opening fight exceeded its bounded real fixed ticks.");
                            for (int i = 0; i < 2; i++) { ticks.Step(); fightTicks++; }
                        }
                    }
                    yield return null; // host.Update drives the runner; do not Step it again here.
                }
                Assert.IsTrue(prepared, "The intro must spawn its three actual xeno probes.");
                AssertPassed(runner);
                CollectionAssert.AreEqual(new[] { "waking_up_start", "choose_start_ship_start", "territory_friendly_entry_start" },
                    nodesStarted.Take(3), "The actual campaign introduction precedes its queued arrival story.");
                AssertOnceStarts(onceStarts);
                if (!stopAtDrift) yield break;

                // Exercise the same physical body again after loading, instead of only looking
                // at a restored counter. A different eligible story may start; the completed
                // opening Once beat must not start again or execute its reward again.
                int cache = Catalog.IndexOf(Catalog.Get<StoryBeatDefinition>(new ContentId("beat_drift_survey_cache")));
                int previousStarts = onceStarts.TryGetValue(cache, out int count) ? count : 0;
                Assert.AreEqual(1, previousStarts, "Cache dialogue ran exactly once before the Drift load.");
                var facts = Service<FactStore>();
                var completed = CompletedOnce(facts);
                string retry = "scenario opening_chapter_cache_revisit\nstatus active\ntimeout 15\ninteract body=drift_cache\nwait frames=3\nassert fact(\"beat_drift_survey_cache\") == 1\nassert logs.errors == 0 && logs.exceptions == 0\n";
                using var revisit = host.RunScenario(ScenarioParser.Parse(retry, host.Commands, host.Probes), ScenarioPath, 2,
                    Path.Combine(TestDirectory, "scenario-results"));
                yield return WaitUntil(() => revisit.Finished, 16, "Real post-load cache interaction settles");
                AssertPassed(revisit);
                Assert.AreEqual(previousStarts, onceStarts[cache], "The completed cache beat did not restart after load.");
                foreach (var pair in completed) Assert.AreEqual(pair.Value, facts.GetBySlot(pair.Key), "A completed Once counter changed after load/reinteraction.");
                AssertOnceStarts(onceStarts);
            }
            finally
            {
                ticks?.Dispose();
                if (modifiedPlayer != Entity.Null) CancelModifiers(modifiedPlayer);
                dialog.Started -= Started;
                host.Input.SetMouse(-1, -1, "left", false, 0, 0, 0);
                host.Input.SetKeys("Digit1,Digit2,Digit3,Digit4,Digit7", 0, true);
            }
        }

        void PrepareProbeFight(List<Entity> probes)
        {
            Em.CompleteAllTrackedJobs();
            float3 player = Em.GetComponentData<LocalTransform>(PlayerRoot).Position;
            Place(PlayerRoot, player, math.PI); // fire away from the Ark station
            for (int i = 0; i < probes.Count; i++)
            {
                Place(probes[i], player + new float3((i - 1) * 35, 0, -100 - i * 40), 0);
                Freeze(probes[i]); EmptyShield(probes[i]); // small, stable weak-target fixtures
            }
            Modify(PlayerRoot, StatId.FireRate, 8);
            Modify(PlayerRoot, StatId.WeaponDamage, 100);
        }
        void Modify(Entity root, StatId stat, float multiplier)
        {
            using var q = Em.CreateEntityQuery(typeof(StatusChannels));
            q.GetSingleton<StatusChannels>().StatModifiers.PostFromMainThread(new StatModifierRequest
            { Root = root, Stat = stat, Producer = ProducerKind.Test, SourceId = ModifierSource, Multiplier = multiplier, UntilTick = uint.MaxValue, Sequence = 1 });
        }
        void CancelModifiers(Entity root)
        {
            if (!Em.Exists(root)) return;
            Modify(root, StatId.FireRate, 1); Modify(root, StatId.WeaponDamage, 1);
        }
        Dictionary<int, int> CompletedOnce(FactStore facts)
        {
            var completed = new Dictionary<int, int>();
            foreach (var beat in Catalog.All<StoryBeatDefinition>())
                if (beat.Repeat == BeatRepeat.Once)
                {
                    int slot = facts.Catalog.GeneratedSlot(FactFamily.Beat, beat.Id.Hash.Value);
                    Assert.GreaterOrEqual(slot, 0, beat.IdString);
                    int value = facts.GetBySlot(slot);
                    if (value != 0) completed.Add(slot, value);
                }
            return completed;
        }
        static void AssertOnceStarts(Dictionary<int, int> starts)
        {
            foreach (var pair in starts) Assert.AreEqual(1, pair.Value, "A production Once dialogue started more than once: catalog index " + pair.Key);
        }
        static void AssertPassed(ScenarioRunner runner)
        {
            Assert.IsTrue(runner.Finished, "Scenario did not finish; " + runner.Run.Message);
            TestContext.WriteLine("Scenario result: " + runner.Run.ResultJsonPath);
            Assert.AreEqual(ScenarioOutcome.Passed, runner.Run.Outcome, runner.Run.Message + "; " + runner.Run.ResultJsonPath);
            Assert.IsTrue(File.Exists(runner.Run.ResultJsonPath), "Actual scenario result JSON must exist.");
        }
    }
}
