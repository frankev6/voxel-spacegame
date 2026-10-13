using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>GAL-003: one failing and one passing test per SPEC-30 §5.9 diagnostic (48 required tests).</summary>
    public sealed class GalaxyValidatorTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();

        [Test]
        public void GAL_E001_Fails()
        {
            using var missing = TestGalaxyFixtures.AuthoredOnly;
            missing.Definitions.Remove(missing.Generation);
            Only(missing, "GAL-E001");
            using var multiple = TestGalaxyFixtures.AuthoredOnly;
            var second = multiple.Create<GalaxyGenerationDefinition>("galaxy_second");
            EditorUtility.CopySerialized(multiple.Generation, second);
            Edit(second, "id", "galaxy_second");
            Only(multiple, "GAL-E001");
        }

        [Test]
        public void GAL_E001_Passes()
        {
            using var small = TestGalaxyFixtures.Small; Clean(small);
            Assert.That(small.Generation.AuthoredStarSystems.Count, Is.EqualTo(4)); Assert.That(small.Generation.ProceduralStarSystemCount, Is.EqualTo(50));
            Assert.That(small.Generation.FixedSeed, Is.EqualTo(0x1234UL));
            using var large = TestGalaxyFixtures.Large; Clean(large);
            Assert.That(large.Generation.ProceduralStarSystemCount, Is.EqualTo(1000)); Assert.That(large.Generation.FixedSeed, Is.EqualTo(0xABCDUL));
            using var disconnected = TestGalaxyFixtures.Disconnected; Clean(disconnected);
            Assert.That(disconnected.Generation.AuthoredLanes.Count, Is.Zero);
            using var authored = TestGalaxyFixtures.AuthoredOnly; Clean(authored);
            Assert.That(authored.Generation.ProceduralStarSystemCount, Is.Zero);
        }

        [Test] public void GAL_E002_Fails() => Fails("GAL-E002", f => Edit(f.Generation, "generatorVersion", GalaxyConstants.CurrentGeneratorVersion + 1));
        [Test] public void GAL_E002_Passes() => Passes(f => Assert.That(f.Generation.GeneratorVersion, Is.EqualTo(GalaxyConstants.CurrentGeneratorVersion)));
        [Test] public void GAL_E003_Fails() => Fails("GAL-E003", f => Edit(f.Generation, "startStarSystem", null));
        [Test] public void GAL_E003_Passes() => Passes(f => Assert.That(f.Generation.AuthoredStarSystems.Contains(f.Start), Is.True));
        [Test] public void GAL_E004_Fails() => Fails("GAL-E004", f => TestContentFactory.Edit(f.Generation, so => TestGalaxyFixtures.SetSystems(so, f.Generation.AuthoredStarSystems.Concat(new[] { f.Start }).ToArray())));
        [Test] public void GAL_E004_Passes() => Passes(f => Assert.That(f.Generation.AuthoredStarSystems.Distinct().Count(), Is.EqualTo(4)));
        [Test] public void GAL_E005_Fails() => Fails("GAL-E005", f => f.Start.Bodies[2].key = f.Start.Bodies[1].key);
        [Test] public void GAL_E005_Passes() => Passes(f => Assert.That(f.Start.Bodies.Count(x => x.role == BodyRole.Moon && x.parentKey == "planet"), Is.EqualTo(2)));
        [Test] public void GAL_E006_Fails() => Fails("GAL-E006", f => f.Start.Bodies[1].parentKey = "missing_planet");
        [Test] public void GAL_E006_Passes() => Passes(f => Assert.That(f.Start.Bodies[0].parentKey, Is.Empty));
        [Test] public void GAL_E007_Fails() => Fails("GAL-E007", f => f.Start.Bodies[0].mode = OrbitMode.Fixed);
        [Test] public void GAL_E007_Passes() => Passes(f => f.Start.Bodies[0].mode = OrbitMode.Elliptic);
        [Test] public void GAL_E008_Fails() => Fails("GAL-E008", f => f.Start.Bodies[1].eccentricity = .31f);
        [Test] public void GAL_E008_Passes() => Passes(f =>
        {
            f.Start.Bodies[0].mode = OrbitMode.Elliptic; f.Start.Bodies[0].eccentricity = .3f;
            var body = f.Start.Bodies[2]; body.role = BodyRole.PointOfInterest; body.template = f.Poi; body.mode = OrbitMode.Fixed; body.orbitRadiusM = 0; body.parentKey = "";
        });
        [Test] public void GAL_E009_Fails() => Fails("GAL-E009", f => f.Generation.AuthoredLanes[0].b = f.Start);
        [Test] public void GAL_E009_Passes() => Passes(f =>
        {
            var lane = f.Generation.AuthoredLanes[0]; (lane.a, lane.b) = (lane.b, lane.a);
        });
        [Test] public void GAL_E010_Fails() => Fails("GAL-E010", f => TestContentFactory.Edit(f.Template, so => TestGalaxyFixtures.BodyPool(so, "planetPool", f.Moon)));
        [Test] public void GAL_E010_Passes() => Passes(f =>
        {
            f.Generation.AuthoredLanes[0].gateTemplateA = f.Gate; f.Generation.AuthoredLanes[0].gateTemplateB = f.Gate;
        });
        [Test] public void GAL_E011_Fails() => Fails("GAL-E011", f => TestContentFactory.Edit(f.Template, so => TestGalaxyFixtures.BodyPool(so, "planetPool", f.Planet, 0)));
        [Test] public void GAL_E011_Passes() => Passes(f => TestContentFactory.Edit(f.Template, so =>
        {
            TestGalaxyFixtures.BodyPool(so, "planetPool", f.Planet, .25f);
            TestContentFactory.Set(so, "minBelts", 0); TestContentFactory.Set(so, "maxBelts", 0); so.FindProperty("beltPool").arraySize = 0;
        }));
        [Test] public void GAL_E012_Fails() => Fails("GAL-E012", f => Edit(f.Template, "minPlanets", 5));
        [Test] public void GAL_E012_Passes() => Passes(f => TestContentFactory.Edit(f.Template, so =>
        {
            TestContentFactory.Set(so, "minPlanets", 0); TestContentFactory.Set(so, "maxPlanets", 0); TestContentFactory.Set(so, "maxMoonsPerPlanet", 0);
            TestContentFactory.Set(so, "minBelts", 0); TestContentFactory.Set(so, "maxBelts", 0);
            TestContentFactory.Set(so, "minStations", 0); TestContentFactory.Set(so, "maxStations", 0);
            TestContentFactory.Set(so, "minPointsOfInterest", 0); TestContentFactory.Set(so, "maxPointsOfInterest", 0);
        }));
        [Test] public void GAL_E013_Fails() => Fails("GAL-E013", f => Edit(f.Template, "stationRadiusMaxM", 2000f));
        [Test] public void GAL_E013_Passes() => Passes(f => Edit(f.Template, "stationRadiusMaxM", f.Template.StationRadiusMinM));
        [Test] public void GAL_E014_Fails() => Fails("GAL-E014", f => Edit(f.Template, "maxPlanets", 171));
        [Test] public void GAL_E014_Passes() => Passes(f => TestContentFactory.Edit(f.Template, so =>
        {
            // 1 + 499 + 1 belt + 1 station + 3 POIs + 4 degree + 3 authored lanes = 512.
            TestContentFactory.Set(so, "maxPlanets", 499); TestContentFactory.Set(so, "maxMoonsPerPlanet", 0);
        }));
        [Test] public void GAL_E015_Fails() => Fails("GAL-E015", f => Edit(f.Station, "structureSource", (int)StructureSource.Blueprint));
        [Test] public void GAL_E015_Passes() => Passes(f => TestContentFactory.Edit(f.Station, so =>
        {
            TestContentFactory.Set(so, "structureSource", (int)StructureSource.ProceduralShape);
            so.FindProperty("shape.sizeCells").vector3IntValue = new Vector3Int(4, 4, 4);
            TestContentFactory.Set(so, "shape.wallThicknessCells", 0);
            TestContentFactory.Set(so, "shape.hullBlock", f.Hull); TestContentFactory.Set(so, "shape.coreBlock", f.Core);
        }));
        [Test] public void GAL_E016_Fails() => Fails("GAL-E016", f => Edit(f.Belt, "asteroidBlock", null));
        [Test] public void GAL_E016_Passes() => Passes(f => TestContentFactory.Edit(f.Belt, so =>
        {
            TestContentFactory.Set(so, "sectorSizeM", 100f); TestContentFactory.Set(so, "asteroidsPerSectorMin", 1); TestContentFactory.Set(so, "asteroidsPerSectorMax", 8);
        }));
        [Test] public void GAL_E017_Fails() => Fails("GAL-E017", f => Edit(f.Generation, "proceduralStarSystemCount", GalaxyConstants.MaxProceduralStarSystems + 1));
        [Test] public void GAL_E017_Passes() => Passes(f => TestContentFactory.Edit(f.Generation, so =>
        {
            TestContentFactory.Set(so, "proceduralStarSystemCount", GalaxyConstants.MaxProceduralStarSystems);
            TestContentFactory.Set(so, "placementAttemptsPerStarSystem", 100); TestContentFactory.Set(so, "laneCandidateNeighbours", 16); TestContentFactory.Set(so, "maxLaneDegree", 16);
        }));
        [Test] public void GAL_E018_Fails() => Fails("GAL-E018", f => Edit(f.Start, "startBodyKey", "missing_body"));
        [Test] public void GAL_E018_Passes() => Passes(f => Edit(f.Start, "startBodyKey", "moon_a"));
        [Test] public void GAL_E019_Fails() => Fails("GAL-E019", f => Edit(f.Generation, "defaultAncientGate", null));
        [Test] public void GAL_E019_Passes() => Passes(f => Assert.That(f.Generation.DefaultHyperlaneGate.Role, Is.EqualTo(BodyRole.LaneGate)));
        [Test] public void GAL_E020_Fails() => Fails("GAL-E020", f => Edit(f.Visual, "meshRadius", 0f));
        [Test] public void GAL_E020_Passes() => Passes(f => Edit(f.Visual, "meshRadius", 1f));

        [Test]
        public void GAL_E021_Fails()
        {
            // Frozen real xxHash3 IDs: distinct 64-bit hashes share the authored 31-bit payload.
            // Assert through production hashing/identity code; no search, hash override or cached-ID mutation.
            const string a = "starsys_collision_24573", b = "starsys_collision_55448";
            Assert.That(new ContentId(a).Hash.Value, Is.EqualTo(0x0421AF2BC15BA402UL));
            Assert.That(new ContentId(b).Hash.Value, Is.EqualTo(0x449A4D8F415BA402UL));
            Assert.That(StarSystemId.FromAuthored(new ContentId(a).Hash), Is.EqualTo(StarSystemId.FromAuthored(new ContentId(b).Hash)));
            using var fixture = TestGalaxyFixtures.AuthoredOnly;
            var first = fixture.NewSystem(a, Vector2.zero); var second = fixture.NewSystem(b, new Vector2(6, 0));
            TestContentFactory.Edit(fixture.Generation, so =>
            {
                TestGalaxyFixtures.SetSystems(so, first, second); TestContentFactory.Set(so, "startStarSystem", first); so.FindProperty("authoredLanes").arraySize = 0;
            });
            Only(fixture, "GAL-E021");
        }

        [Test] public void GAL_E021_Passes() => Passes(f =>
        {
            var values = f.Generation.AuthoredStarSystems.Select(s => StarSystemId.FromAuthored(s.Id.Hash).Value).ToArray();
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Length));
        });
        [Test] public void GAL_W001_Fails() => Fails("GAL-W001", f => Position(f.Generation.AuthoredStarSystems[1], 1));
        [Test] public void GAL_W001_Passes() => Passes(f => Position(f.Generation.AuthoredStarSystems[1], 3));
        [Test] public void GAL_W002_Fails() => Fails("GAL-W002", f => f.Generation.AuthoredLanes[0].b = f.Generation.AuthoredStarSystems[3]);
        [Test] public void GAL_W002_Passes() => Passes(f => Edit(f.Generation, "maxLaneLengthLy", 6f));
        [Test] public void GAL_W003_Fails() => Fails("GAL-W003", f => { Position(f.Generation.AuthoredStarSystems[3], 61); Edit(f.Generation, "maxLaneLengthLy", 100f); });
        [Test] public void GAL_W003_Passes() => Passes(f => { Position(f.Generation.AuthoredStarSystems[3], 60); Edit(f.Generation, "maxLaneLengthLy", 100f); });

        static void Edit(ContentDefinition definition, string field, object value)
            => TestContentFactory.Edit(definition, so => TestContentFactory.Set(so, field, value));
        static void Position(AuthoredStarSystemDefinition definition, float x)
            => TestContentFactory.Edit(definition, so => so.FindProperty("positionLy").vector2Value = new Vector2(x, 0));
        static void Fails(string code, Action<TestGalaxyFixtures.Fixture> mutate)
        { using var fixture = TestGalaxyFixtures.AuthoredOnly; mutate(fixture); Only(fixture, code); }
        static void Passes(Action<TestGalaxyFixtures.Fixture> mutate)
        { using var fixture = TestGalaxyFixtures.AuthoredOnly; mutate(fixture); Clean(fixture); }
        static void Clean(TestGalaxyFixtures.Fixture fixture)
        { var report = fixture.Validate(); Assert.That(report.ErrorCount + report.WarningCount, Is.Zero, report.ToText()); }
        static void Only(TestGalaxyFixtures.Fixture fixture, string code)
        {
            var report = fixture.Validate();
            CollectionAssert.AreEqual(new[] { code }, report.Entries.Select(e => e.Code).Distinct().ToArray(), report.ToText());
            foreach (var entry in report.Entries)
            {
                Assert.That(entry.Severity, Is.EqualTo(code.StartsWith("GAL-W", StringComparison.Ordinal) ? ValidationSeverity.Warning : ValidationSeverity.Error));
                Assert.That(entry.AssetPath, Is.Not.Empty); Assert.That(entry.Field, Is.Not.Empty);
            }
        }
    }
}
