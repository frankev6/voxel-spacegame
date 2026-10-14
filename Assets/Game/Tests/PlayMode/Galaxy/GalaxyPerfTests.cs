using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Generation;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>GAL-013: two opt-in fixtures; real generation/deltas, fixed samples and advisory PerfRun records.</summary>
    [Category("Performance")]
    public sealed class GalaxyPerfTests
    {
        const ulong Seed = 0xABCDUL;
        const int Warmup = 60, Samples = 300;

        sealed class Fixture : IDisposable
        {
            readonly List<ContentDefinition> owned = new();
            internal readonly ContentCatalog Catalog;
            internal readonly GalaxyGenerationDefinition Generation;
            internal Fixture()
            {
                try
                {
                    var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset");
                    Assert.IsNotNull(database, "Real content must be imported before running this opt-in fixture.");
                    var definitions = database.Definitions.ToList();
                    var source = definitions.OfType<GalaxyGenerationDefinition>().Single();
                    Generation = Clone(source, "galaxy_perf");
                    var template = Clone(source.TemplatePool[0].template, "systpl_perf");
                    Edit(template, so =>
                    {
                        foreach (string prefix in new[] { "Planets", "Belts", "Stations", "PointsOfInterest" })
                        {
                            int count = prefix == "Planets" ? 2 : 1;
                            so.FindProperty("min" + prefix).intValue = count;
                            so.FindProperty("max" + prefix).intValue = count;
                        }
                        so.FindProperty("maxMoonsPerPlanet").intValue = 2;
                        so.FindProperty("moonChance").floatValue = 1;
                        so.FindProperty("stationRadiusMinM").floatValue = 30000;
                        so.FindProperty("stationRadiusMaxM").floatValue = 40000;
                        so.FindProperty("pointOfInterestRadiusMinM").floatValue = 45000;
                        so.FindProperty("pointOfInterestRadiusMaxM").floatValue = 55000;
                    });
                    var authored = new AuthoredStarSystemDefinition[4];
                    for (int i = 0; i < authored.Length; i++)
                    {
                        int index = i;
                        authored[i] = Clone(source.AuthoredStarSystems[i % source.AuthoredStarSystems.Count], "starsys_perf_" + i.ToString("D2"));
                        Edit(authored[i], so =>
                        {
                            so.FindProperty("template").objectReferenceValue = template;
                            so.FindProperty("positionLy").vector2Value = new Vector2(index * 6, 0);
                            so.FindProperty("connectProcedurally").boolValue = true;
                        });
                    }
                    Edit(Generation, so =>
                    {
                        so.FindProperty("useFixedSeed").boolValue = false;
                        so.FindProperty("proceduralStarSystemCount").intValue = 1000;
                        so.FindProperty("galaxyRadiusLy").floatValue = 120;
                        var pool = so.FindProperty("templatePool"); pool.arraySize = 1;
                        pool.GetArrayElementAtIndex(0).FindPropertyRelative("template").objectReferenceValue = template;
                        pool.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1;
                        var systems = so.FindProperty("authoredStarSystems"); systems.arraySize = authored.Length;
                        for (int i = 0; i < authored.Length; i++) systems.GetArrayElementAtIndex(i).objectReferenceValue = authored[i];
                        so.FindProperty("startStarSystem").objectReferenceValue = authored[0];
                        so.FindProperty("authoredLanes").arraySize = 0;
                    });
                    definitions.Remove(source); definitions.AddRange(owned);
                    var report = new ValidationReport("GAL-013 fixture catalog");
                    Catalog = ContentCatalog.Build(definitions, report);
                    Assert.IsFalse(report.HasErrors, report.ToText());
                }
                catch { Dispose(); throw; }
            }
            T Clone<T>(T source, string id) where T : ContentDefinition
            {
                Assert.IsNotNull(source, id);
                var copy = Object.Instantiate(source); owned.Add(copy); copy.name = id;
                Edit(copy, so => { so.FindProperty("id").stringValue = id; so.FindProperty("formerIds").arraySize = 0; });
                return copy;
            }
            static void Edit(ContentDefinition definition, Action<SerializedObject> edit)
            { using var so = new SerializedObject(definition); edit(so); so.ApplyModifiedPropertiesWithoutUndo(); }
            internal GalaxyDef Build() => GalaxyDefBuilder.Build(Catalog, Seed, GalaxyConstants.CurrentGeneratorVersion);
            public void Dispose() { foreach (var definition in owned) Object.DestroyImmediate(definition); owned.Clear(); }
        }

        static void Record(string phase, SampleGroup group, double budgetMs)
        {
            Assert.AreEqual(Samples, group.Samples.Count, "The timing fixture must keep the specified sample count.");
            Assert.IsTrue(group.Samples.All(sample => double.IsFinite(sample) && sample >= 0));
            Assert.IsTrue(group.Samples.Any(sample => sample > 0), "Unavailable timing must not become a passing zero record.");
            // Separate phase filenames preserve both content and delta budgets instead of overwriting one record.
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName + "." + phase).AddSamples(phase, group.Samples);
            run.Write(phase, budgetMs); run.AssertMedian(phase, budgetMs);
        }

        static void CheckGalaxy(GalaxyDef galaxy)
        {
            Assert.AreEqual(Seed, galaxy.Seed); Assert.AreEqual(1004, galaxy.StarSystems.Length);
            Assert.AreEqual(0, galaxy.SkippedProceduralCount); Assert.AreEqual(4, galaxy.Authored.Count);
            Assert.AreNotEqual(0UL, galaxy.Hash); Assert.GreaterOrEqual(galaxy.Lanes.Length, 1003);
            Assert.AreEqual(galaxy.Lanes.Length * 2, galaxy.Adjacency.Length);
            int authored = 0, start = 0; uint previous = 0;
            foreach (var system in galaxy.StarSystems)
            {
                Assert.Greater(system.Id.Value, previous); previous = system.Id.Value;
                Assert.IsTrue(math.all(math.isfinite(system.PositionLy)));
                Assert.IsTrue(galaxy.TryGetIndex(system.Id, out int index)); Assert.AreEqual(system.Id, galaxy.StarSystems[index].Id);
                if (system.AuthoredIndex != 0) authored++;
                if ((system.Flags & StarSystemFlags.Start) != 0) { start++; Assert.AreEqual(galaxy.StartStarSystem, system.Id); }
                Assert.Greater(system.LaneCount, 0);
                for (int slot = 0; slot < system.LaneCount; slot++)
                {
                    int laneIndex = galaxy.LaneIndexOf(system.Id, slot);
                    Assert.That(laneIndex, Is.InRange(0, galaxy.Lanes.Length - 1));
                    var neighbour = GalaxyDef.Neighbour(galaxy.Lanes[laneIndex], system.Id);
                    Assert.IsTrue(galaxy.TryGetIndex(neighbour, out _));
                }
            }
            Assert.AreEqual(4, authored); Assert.AreEqual(1, start);
            var reached = new HashSet<StarSystemId> { galaxy.StartStarSystem }; var queue = new Queue<StarSystemId>();
            queue.Enqueue(galaxy.StartStarSystem);
            while (queue.Count != 0)
            {
                var id = queue.Dequeue();
                for (int slot = 0; slot < galaxy.LaneCountOf(id); slot++)
                {
                    var neighbour = GalaxyDef.Neighbour(galaxy.Lanes[galaxy.LaneIndexOf(id, slot)], id);
                    if (reached.Add(neighbour)) queue.Enqueue(neighbour);
                }
            }
            Assert.AreEqual(1004, reached.Count, "Every generated system must be reachable.");
        }

        [Test, Performance] public void Generate_1000()
        {
            using var fixture = new Fixture(); using var baseline = fixture.Build(); CheckGalaxy(baseline);
            PerfSettings.AssertReference();
            GalaxyDef generated = null;
            var timing = new SampleGroup("GalaxyGenerate1000", SampleUnit.Millisecond);
            try
            {
                Measure.Method(() => generated = fixture.Build()).SampleGroup(timing)
                    .WarmupCount(Warmup).MeasurementCount(Samples).IterationsPerMeasurement(1)
                    .CleanUp(() =>
                    {
                        try
                        {
                            Assert.AreEqual(1004, generated.StarSystems.Length); Assert.AreEqual(0, generated.SkippedProceduralCount);
                            Assert.AreEqual(baseline.Hash, generated.Hash); Assert.AreEqual(baseline.Lanes.Length, generated.Lanes.Length);
                        }
                        finally { generated?.Dispose(); generated = null; }
                    }).Run();
                // End-to-end Build includes packing, real Burst jobs/Complete, adjacency and hash; cleanup/assertions are excluded.
                Record("GalaxyGenerate1000", timing, 50);
            }
            finally { generated?.Dispose(); }
        }

        static StarSystemContent Generate(GalaxyDef galaxy, StarSystemId id)
        {
            var content = StarSystemContentBuilder.Build(galaxy, id, out bool warning, out bool error);
            if (warning || error) { content.Dispose(); Assert.Fail("The content workload must generate fully without pool/capacity errors."); }
            return content;
        }
        static void CheckBodies(StarSystemContent content, BodySpec[] expected, ulong hash)
        {
            Assert.AreEqual(hash, content.GeneratedHash); Assert.AreEqual(expected.Length, content.Bodies.Length);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], content.Bodies[i], "Body " + i);
        }
        static void CheckDelta(StarSystemContent content, BodySpec[] expected, ulong hash, BodySpec anchor, ushort spawnTemplate)
        {
            Assert.AreEqual(hash, content.GeneratedHash, "Saved changes must preserve the generated identity.");
            Assert.AreEqual(expected.Length + 1, content.Bodies.Length);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], content.Bodies[i], "Delta body " + i);
            var spawn = content.Bodies[expected.Length];
            Assert.AreEqual(new BodyId(content.Id, BodyRole.StorySpawn, 0, 0), spawn.Id);
            Assert.AreEqual(BodyStateFlags.StorySpawn, spawn.State); Assert.AreEqual(spawnTemplate, spawn.TemplateIndex);
            Assert.AreEqual(anchor.Id.LocalKey, spawn.ParentKey); Assert.AreEqual(anchor.Orbit, spawn.ParentOrbit);
            Assert.AreEqual(OrbitMode.Fixed, spawn.Orbit.Mode); Assert.AreEqual(50, spawn.Orbit.RadiusM);
            Assert.AreEqual(math.atan2(40d, 30d), spawn.Orbit.PhaseRad); Assert.AreEqual(.25f, spawn.YawRad);
            Assert.IsTrue(content.TryFind(spawn.Id.LocalKey, out int index)); Assert.AreEqual(expected.Length, index);
        }

        [Test, Performance] public void Content_Procedural()
        {
            using var fixture = new Fixture(); using var galaxy = fixture.Build();
            var system = galaxy.StarSystems.First(value => value.AuthoredIndex == 0);
            using var baseline = Generate(galaxy, system.Id);
            var expected = baseline.Bodies.AsArray().ToArray(); ulong hash = baseline.GeneratedHash;
            Assert.AreNotEqual(0UL, hash); Assert.AreEqual(1, expected.Count(body => body.Id.Role == BodyRole.Star));
            Assert.AreEqual(2, expected.Count(body => body.Id.Role == BodyRole.Planet));
            Assert.AreEqual(1, expected.Count(body => body.Id.Role == BodyRole.Belt));
            Assert.AreEqual(1, expected.Count(body => body.Id.Role == BodyRole.Station));
            Assert.AreEqual(1, expected.Count(body => body.Id.Role == BodyRole.PointOfInterest));
            Assert.AreEqual(system.LaneCount, expected.Count(body => body.Id.Role == BodyRole.LaneGate));
            Assert.Greater(baseline.ExtentM, baseline.LaneRingRadiusM);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(system.Id, expected[i].Id.System);
                if (i > 0) Assert.Greater(expected[i].Id.LocalKey, expected[i - 1].Id.LocalKey);
                Assert.That(expected[i].TemplateIndex, Is.LessThan(galaxy.BodyTemplates.Count));
            }
            var changed = (BodySpec[])expected.Clone();
            int replacement = Array.FindIndex(expected, body => body.Id.Role == BodyRole.Planet);
            int interacted = Array.FindLastIndex(expected, body => body.Id.Role == BodyRole.Planet);
            int destroyed = Array.FindIndex(expected, body => body.Id.Role == BodyRole.Station);
            var cache = fixture.Catalog.Get<BodyTemplateDefinition>(new ContentId("body_resource_cache")); Assert.IsNotNull(cache);
            ushort templateIndex = galaxy.BodyTemplateIndexOf(cache);
            using var delta = new StarSystemDelta(system.Id);
            delta.SetReplacement(expected[replacement].Id.LocalKey, cache.Id); delta.MarkInteracted(expected[interacted].Id.LocalKey);
            delta.MarkDestroyed(expected[destroyed].Id.LocalKey);
            delta.AddStorySpawn(new StorySpawnRecord { Template = cache.Id, AnchorBodyKey = expected[replacement].Id.LocalKey, OffsetM = new double3(30, 0, 40), YawRad = .25f });
            changed[replacement].TemplateIndex = templateIndex; changed[replacement].State |= BodyStateFlags.Replaced;
            changed[interacted].State |= BodyStateFlags.Interacted; changed[destroyed].State |= BodyStateFlags.Destroyed;
            using (var check = Generate(galaxy, system.Id))
            {
                CheckBodies(check, expected, hash); StarSystemDeltaApplier.Apply(check, delta, galaxy, fixture.Catalog);
                CheckDelta(check, changed, hash, expected[replacement], templateIndex);
            }
            PerfSettings.AssertReference();
            StarSystemContent generated = null;
            bool warning = false, error = false;
            try
            {
                var contentTime = new SampleGroup("StarSystemContent", SampleUnit.Millisecond);
                Measure.Method(() => generated = StarSystemContentBuilder.Build(galaxy, system.Id, out warning, out error)).SampleGroup(contentTime)
                    .WarmupCount(Warmup).MeasurementCount(Samples).IterationsPerMeasurement(1)
                    .CleanUp(() =>
                    {
                        try { Assert.IsFalse(warning || error); CheckBodies(generated, expected, hash); }
                        finally { generated?.Dispose(); generated = null; }
                    }).Run();
                Record("StarSystemContent", contentTime, 2);
                var deltaTime = new SampleGroup("StarSystemDeltaApply", SampleUnit.Millisecond);
                Measure.Method(() => StarSystemDeltaApplier.Apply(generated, delta, galaxy, fixture.Catalog)).SampleGroup(deltaTime)
                    .WarmupCount(Warmup).MeasurementCount(Samples).IterationsPerMeasurement(1)
                    .SetUp(() => generated = Generate(galaxy, system.Id))
                    .CleanUp(() => { try { CheckDelta(generated, changed, hash, expected[replacement], templateIndex); } finally { generated?.Dispose(); generated = null; } }).Run();
                Record("StarSystemDeltaApply", deltaTime, .5);
                Assert.IsEmpty(fixture.Catalog.Unresolved, "All saved template references must resolve.");
            }
            finally { generated?.Dispose(); }
        }
    }
}
