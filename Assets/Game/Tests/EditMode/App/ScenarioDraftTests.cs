using System;
using System.Collections.Generic;
using System.IO;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    public sealed class ScenarioDraftTests
    {
        sealed class FutureCommand : IDevCommand
        {
            public string Name { get; }
            public string Usage => Name;
            public string Summary => "SPEC-70 parse-only fixture";
            internal FutureCommand(string name) { Name = name; }
            public DevCommandResult Execute(DevArgs args, DevContext ctx) => throw new NotSupportedException("Draft parsing only");
        }
        sealed class FutureProbe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount { get; }
            internal FutureProbe(string name, int args = 0) { Name = name; ArgCount = args; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args) => throw new NotSupportedException("Draft parsing only");
        }
        [Test]
        public void AllDrafts_Parse()
        {
            // Explicit future vocabulary from SPEC-70 sections 6.2/6.3, never production stubs.
            var commands = new List<IDevCommand>(DevCommandRegistry.CreateDefault().Commands);
            foreach (string name in new[] { "interact", "spawn", "teleport", "save", "jump" })
                if (!commands.Exists(c => c.Name == name)) commands.Add(new FutureCommand(name));
            var probes = ScenarioProbeRegistry.CreateDefault();
            foreach (string name in new[] { "interaction.inrange", "player.upos.x", "player.upos.z", "star_system", "travel.phase", "travel.arrival_gap", "travel.inside_ring" })
                if (!probes.TryGet(name, out _)) probes.Register(new FutureProbe(name));
            if (!probes.TryGet("inv", out _)) probes.Register(new FutureProbe("inv", 1));
            if (!probes.TryGet("grids.count", out _)) probes.Register(new FutureProbe("grids.count", 1));
            string[] names = { "r10_station_interaction", "r15_editor_no_dupe", "r16_r19_editor_round_trip", "r25_r26_ship_death_no_ecb_errors", "r28_pause_load", "r30_lane_jump_entry" };
            string[][] tags = { new[] { "R10" }, new[] { "R15" }, new[] { "R16", "R19" }, new[] { "R25", "R26" }, new[] { "R28" }, new[] { "R30" } };
            for (int i = 0; i < names.Length; i++)
            {
                bool activeTravel = names[i] == "r30_lane_jump_entry";
                var script = ScenarioParser.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "../TestData/Scenarios", names[i] + ".scenario")),
                    activeTravel ? DevCommandRegistry.CreateDefault() : new DevCommandRegistry(commands.ToArray()),
                    activeTravel ? ScenarioProbeRegistry.CreateDefault() : probes);
                Assert.AreEqual(names[i], script.Name);
                Assert.AreEqual(activeTravel ? "active" : "draft", script.Status);
                CollectionAssert.Contains(script.Tags, "regression");
                foreach (string tag in tags[i]) CollectionAssert.Contains(script.Tags, tag);
                Assert.Greater(script.Statements.Count, 0);
            }
        }
    }
}
