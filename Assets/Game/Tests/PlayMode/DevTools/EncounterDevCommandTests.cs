using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Factions;
using Game.Tests.Common;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.TestTools;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.Tests.PlayMode.DevTools
{
    /// <summary>ENC-012: registered runtime commands/probes, production boot, director and fixed-step faction writer.</summary>
    public sealed class EncounterDevCommandTests : GameFlowFixture
    {
        DevRuntimeHost Host => Service<DevRuntimeHost>();
        EncounterDirectorService Director => Service<EncounterDirectorService>();
        EncounterDefinition Raiders => Service<ContentService>().Catalog.Get<EncounterDefinition>(new ContentId("encounter_raiders"));
        DevCommandResult Execute(string command) => Host.Commands.Execute(command, Host.Context);
        DevCommandResult Ok(string command)
        { var result = Execute(command); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text); return result; }
        ScenarioValue Read(DevContext context, string name, params ScenarioValue[] args)
        { Assert.IsTrue(Host.Probes.TryGet(name, out var probe), name); return probe.Read(context, args); }
        void DefeatRaiders()
        {
            Em.CompleteAllTrackedJobs();
            using (var query = Em.CreateEntityQuery(typeof(EncounterTag)))
            using (var roots = query.ToEntityArray(Allocator.Temp))
                foreach (var root in roots)
                    if (Em.GetComponentData<EncounterTag>(root).Encounter == Raiders.Id.Hash && !Em.HasComponent<Hulk>(root)) Em.AddComponent<Hulk>(root);
            using var clock = Em.CreateEntityQuery(typeof(SimClock));
            Director.Tick(new GalaxyFrameContext(Em, clock.GetSingleton<SimClock>().UniversalTime, 0, 0, 0));
            EventHub.DrainAll();
        }
        [UnityTest] public IEnumerator Spawn_Probe()
        {
            // Real menu/sandbox readiness cannot be replaced by publishing a fake entry event.
            var unavailable = Execute("encounter spawn=encounter_raiders");
            Assert.AreEqual(DevResultKind.Failed, unavailable.Kind); StringAssert.Contains("created 0", unavailable.Text);
            StringAssert.Contains("no galaxy", unavailable.Text);
            yield return NewGame(42);
            StringAssert.Contains("anchor=player|<tag>", Ok("help encounter").Text);
            Ok("encounter director=off"); Assert.IsFalse(Director.Enabled);
            Ok("encounter director=on"); Assert.IsTrue(Director.Enabled); Ok("encounter director=off");
            Assert.AreEqual(0, Director.ActiveInstanceCount);
            foreach (string command in new[] { "encounter spawn=unknown", "encounter spawn=encounter_raiders count=0",
                "encounter spawn=encounter_raiders count=11", "encounter spawn=encounter_raiders count=abc",
                "encounter spawn=encounter_raiders anchor=\"\"", "encounter spawn=encounter_raiders combat=encounter_raiders",
                "encounter combat=encounter_raiders count=2", "encounter list spawn=encounter_raiders", "encounter director=maybe", "encounter typo=true" })
                Assert.AreEqual(DevResultKind.Failed, Execute(command).Kind, command);
            Assert.AreEqual(0, Director.ActiveInstanceCount, "Invalid requests have no side effects.");
            using var run = Host.Context.CreateScenarioContext();
            Assert.AreEqual(0, Read(run, "encounter_defeated", Text(Raiders.Id.Value)).Number);
            StringAssert.Contains("created 1 of 1", Ok("encounter spawn=encounter_raiders count=1 anchor=player").Text);
            Assert.AreEqual(Director.ActiveInstanceCount, Read(run, "encounters.active").Number);
            Assert.AreEqual(Director.AliveShipCount, Read(run, "encounters.ships").Number);
            Assert.GreaterOrEqual(Read(run, "encounters.ships").Number, 2);
            StringAssert.Contains("true", Ok("until cond=\"encounters.ships >= 2\"").Text);
            var instances = new List<EncounterInstanceInfo>(); Director.GetActiveInstances(instances);
            Assert.AreEqual(1, instances.Count); Assert.AreEqual(EncounterSource.Dev, instances[0].Source);
            using (var query = Em.CreateEntityQuery(typeof(EncounterTag)))
            using (var roots = query.ToEntityArray(Allocator.Temp))
            {
                Assert.AreEqual(Director.AliveShipCount, roots.Length);
                foreach (var root in roots) Assert.AreEqual(instances[0].Id, Em.GetComponentData<EncounterTag>(root).Instance);
            }
            string listing = Ok("encounter list").Text;
            StringAssert.Contains(Raiders.Id.Value, listing); StringAssert.Contains("source=Dev", listing);
            DefeatRaiders();
            Assert.AreEqual(0, Read(run, "encounters.active").Number); Assert.AreEqual(0, Read(run, "encounters.ships").Number);
            Assert.AreEqual(1, Read(run, "encounter_defeated", Text(Raiders.Id.Value)).Number);
            using var laterRun = Host.Context.CreateScenarioContext();
            Assert.AreEqual(0, Read(laterRun, "encounter_defeated", Text(Raiders.Id.Value)).Number);
            // Combat uses the owner's separate source/placement path and the same real defeat bridge.
            Ok("encounter combat=encounter_raiders"); Director.GetActiveInstances(instances);
            Assert.AreEqual(1, instances.Count); Assert.AreEqual(EncounterSource.Combat, instances[0].Source);
            DefeatRaiders(); DefeatRaiders();
            Assert.AreEqual(2, Read(run, "encounter_defeated", Text(Raiders.Id.Value)).Number);
            Assert.AreEqual(1, Read(laterRun, "encounter_defeated", Text(Raiders.Id.Value)).Number, "Counters are independent and do not count repeated sweeps.");
            Assert.AreEqual(2, run.EventCount(nameof(EncounterDefeated))); Assert.AreEqual(1, laterRun.EventCount(nameof(EncounterDefeated)));
            Assert.Throws<ArgumentException>(() => Read(run, "encounter_defeated", Text("unknown")));
        }
        [UnityTest] public IEnumerator Relation_SetAndStance()
        {
            yield return NewGame(42); Ok("encounter director=off");
            StringAssert.Contains("set=<Int32>", Ok("help relation").Text);
            var factions = Service<FactionService>(); var vestari = Service<ContentService>().Catalog.Get<FactionDefinition>(new ContentId("faction_vestari"));
            byte index = factions.Indices.IndexOf(vestari);
            Assert.AreEqual("Neutral", Read(Host.Context, "stance", Text(vestari.Id.Value)).String);
            using (var pause = HoldSimulation("inspect queued dev relation"))
            {
                StringAssert.Contains("queued for next fixed tick", Ok("relation faction=faction_vestari set=-50").Text);
                Assert.AreEqual(-50, Read(Host.Context, "relation", Text(vestari.Id.Value)).Number);
                Assert.AreEqual(FactionStance.Neutral, factions.GetStance(0, index), "The dev command must not write the matrix directly.");
            }
            yield return WaitTicks(1);
            Assert.AreEqual(-50, Read(Host.Context, "relation", Text(vestari.Id.Value)).Number);
            Assert.AreEqual("Hostile", Read(Host.Context, "stance", Text(vestari.Id.Value)).String);
            Assert.AreEqual(FactionStance.Hostile, factions.GetStance(0, index)); Assert.AreEqual(FactionStance.Hostile, factions.GetStance(index, 0));
            StringAssert.Contains("relation=-50", Ok("relation faction=faction_vestari").Text);
            Ok("relation faction=faction_vestari add=80"); yield return WaitTicks(1);
            Assert.AreEqual(30, Read(Host.Context, "relation", Text(vestari.Id.Value)).Number);
            Assert.AreEqual("Neutral", Read(Host.Context, "stance", Text(vestari.Id.Value)).String);
            Ok("relation faction=faction_vestari set=2147483647"); yield return WaitTicks(1);
            Assert.AreEqual(100, Read(Host.Context, "relation", Text(vestari.Id.Value)).Number);
            foreach (string command in new[] { "relation", "relation faction=unknown", "relation faction=faction_raiders set=0",
                "relation faction=faction_ark add=1", "relation faction=faction_vestari set=-50 add=1",
                "relation faction=faction_vestari set=abc", "relation faction=faction_vestari add=2147483648", "relation faction=faction_vestari typo=1" })
                Assert.AreEqual(DevResultKind.Failed, Execute(command).Kind, command);
            Assert.AreEqual(100, factions.GetRelation(vestari), "Rejected writes do not alter pending state.");
            Assert.Throws<ArgumentException>(() => Read(Host.Context, "relation", Text("unknown")));
            Assert.Throws<InvalidOperationException>(() => Read(Host.Context, "stance", Number(1)));
        }
    }
}
