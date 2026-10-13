using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content.Validators;
using Game.Tests.EditMode.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>In-memory SPEC-30 fixtures; callers dispose each fixture and call TestContentFactory.DestroyAll at teardown.</summary>
    public static class TestGalaxyFixtures
    {
        public static Fixture Small => new Fixture(4, 50, 0x1234UL, false);
        public static Fixture Large => new Fixture(1, 1000, 0xABCDUL, false);
        public static Fixture Disconnected => new Fixture(4, 0, 0x1234UL, true);
        public static Fixture AuthoredOnly => new Fixture(4, 0, 0x1234UL, false);

        public sealed class Fixture : IDisposable
        {
            readonly List<Object> resources = new();
            public readonly List<ContentDefinition> Definitions = new();
            public GalaxyGenerationDefinition Generation { get; }
            public StarSystemTemplateDefinition Template { get; }
            public BodyVisualDefinition Visual { get; }
            public BodyTemplateDefinition Star { get; }
            public BodyTemplateDefinition Planet { get; }
            public BodyTemplateDefinition Moon { get; }
            public BodyTemplateDefinition Belt { get; }
            public BodyTemplateDefinition Station { get; }
            public BodyTemplateDefinition Poi { get; }
            public BodyTemplateDefinition Gate { get; }
            public BlockDefinition Hull { get; }
            public BlockDefinition Core { get; }
            public AuthoredStarSystemDefinition Start => Generation.StartStarSystem;

            internal Fixture(int authoredCount, int proceduralCount, ulong seed, bool disconnected)
            {
                Hull = TestContentFactory.Block("block_missing", BlockKind.Structural); Definitions.Add(Hull);
                var missing = TestContentFactory.Item("item_missing"); Definitions.Add(missing);
                Core = TestContentFactory.Block("block_fixture_core", BlockKind.Functional, modules: new BlockModule[] { new CoreModule { Params = new CoreParams { MaxCells = 10000, MaxExtentCells = 128 } } }); Definitions.Add(Core);
                var mesh = new Mesh { name = "Galaxy test visual" }; resources.Add(mesh);
                var shader = Shader.Find("Hidden/InternalErrorShader");
                if (shader == null) throw new InvalidOperationException("Unity's built-in error shader is required by this in-memory fixture.");
                var material = new Material(shader); resources.Add(material);
                Visual = Create<BodyVisualDefinition>("bodyvis_fixture", so => { TestContentFactory.Set(so, "mesh", mesh); TestContentFactory.Set(so, "material", material); });
                Star = Body("body_fixture_star", BodyRole.Star);
                Planet = Body("body_fixture_planet", BodyRole.Planet);
                Moon = Body("body_fixture_moon", BodyRole.Moon);
                Belt = Body("body_fixture_belt", BodyRole.Belt);
                Station = Body("body_fixture_station", BodyRole.Station);
                Poi = Body("body_fixture_poi", BodyRole.PointOfInterest);
                Gate = Body("body_fixture_gate", BodyRole.LaneGate);
                Template = Create<StarSystemTemplateDefinition>("systpl_fixture", so =>
                {
                    BodyPool(so, "starPool", Star); BodyPool(so, "planetPool", Planet); BodyPool(so, "moonPool", Moon);
                    BodyPool(so, "beltPool", Belt); BodyPool(so, "stationPool", Station); BodyPool(so, "pointOfInterestPool", Poi);
                });
                var systems = new AuthoredStarSystemDefinition[authoredCount];
                for (int i = 0; i < authoredCount; i++) systems[i] = NewSystem("starsys_fixture_" + i, new Vector2(i * (disconnected ? 15 : 6), 0));
                Generation = Create<GalaxyGenerationDefinition>("galaxy_fixture", so =>
                {
                    so.FindProperty("fixedSeed").longValue = unchecked((long)seed);
                    TestContentFactory.Set(so, "proceduralStarSystemCount", proceduralCount);
                    if (proceduralCount == 1000) TestContentFactory.Set(so, "galaxyRadiusLy", 120f);
                    var pool = so.FindProperty("templatePool"); pool.arraySize = 1;
                    pool.GetArrayElementAtIndex(0).FindPropertyRelative("template").objectReferenceValue = Template;
                    pool.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1;
                    SetSystems(so, systems);
                    TestContentFactory.Set(so, "startStarSystem", systems[0]);
                    TestContentFactory.Set(so, "defaultHyperlaneGate", Gate); TestContentFactory.Set(so, "defaultAncientGate", Gate);
                    var lanes = so.FindProperty("authoredLanes"); lanes.arraySize = disconnected ? 0 : authoredCount - 1;
                    for (int i = 0; i < lanes.arraySize; i++)
                    {
                        var lane = lanes.GetArrayElementAtIndex(i);
                        lane.FindPropertyRelative("a").objectReferenceValue = systems[i]; lane.FindPropertyRelative("b").objectReferenceValue = systems[i + 1];
                    }
                });
            }

            public T Create<T>(string id, Action<SerializedObject> edit = null) where T : ContentDefinition
            { var definition = TestContentFactory.Create<T>(id, edit); Definitions.Add(definition); return definition; }

            BodyTemplateDefinition Body(string id, BodyRole role) => Create<BodyTemplateDefinition>(id, so =>
            {
                TestContentFactory.Set(so, "displayName", id); TestContentFactory.Set(so, "role", (int)role); TestContentFactory.Set(so, "visual", Visual);
                if (role == BodyRole.Planet) TestContentFactory.Set(so, "canHaveMoons", true);
                if (role == BodyRole.Belt) TestContentFactory.Set(so, "asteroidBlock", Hull);
            });

            public AuthoredStarSystemDefinition NewSystem(string id, Vector2 position) => Create<AuthoredStarSystemDefinition>(id, so =>
            {
                TestContentFactory.Set(so, "displayName", id); TestContentFactory.Set(so, "template", Template); TestContentFactory.Set(so, "star", Star);
                TestContentFactory.Set(so, "connectProcedurally", true); so.FindProperty("positionLy").vector2Value = position;
                TestContentFactory.Set(so, "startBodyKey", "planet");
                var bodies = so.FindProperty("bodies"); bodies.arraySize = 3;
                SetBody(bodies.GetArrayElementAtIndex(0), "planet", BodyRole.Planet, Planet, "", 0);
                SetBody(bodies.GetArrayElementAtIndex(1), "moon_a", BodyRole.Moon, Moon, "planet", 0);
                SetBody(bodies.GetArrayElementAtIndex(2), "moon_b", BodyRole.Moon, Moon, "planet", 0);
            });

            public ValidationReport Validate()
            {
                var report = new ValidationReport("Galaxy fixture");
                var catalog = ContentCatalog.Build(Definitions, report);
                var membership = new HashSet<ContentDefinition>(Definitions);
                foreach (var definition in Definitions)
                {
                    string path = "content:" + definition.IdString;
                    definition.Validate(report.For(path), new ContentValidationContext(path, true, membership.Contains, catalog));
                }
                new GalaxyContentValidator().Validate(catalog, report);
                return report;
            }

            public void Dispose()
            { foreach (var resource in resources) Object.DestroyImmediate(resource); resources.Clear(); }
        }

        public static void BodyPool(SerializedObject so, string field, BodyTemplateDefinition body, float weight = 1, int maximum = 0)
        {
            var pool = so.FindProperty(field); pool.arraySize = 1;
            var entry = pool.GetArrayElementAtIndex(0); entry.FindPropertyRelative("template").objectReferenceValue = body;
            entry.FindPropertyRelative("weight").floatValue = weight; entry.FindPropertyRelative("maxCount").intValue = maximum;
        }

        public static void SetSystems(SerializedObject so, params AuthoredStarSystemDefinition[] systems)
        {
            var list = so.FindProperty("authoredStarSystems"); list.arraySize = systems.Length;
            for (int i = 0; i < systems.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
        }

        static void SetBody(SerializedProperty body, string key, BodyRole role, BodyTemplateDefinition template, string parent, int slot)
        {
            body.FindPropertyRelative("key").stringValue = key; body.FindPropertyRelative("role").intValue = (int)role;
            body.FindPropertyRelative("template").objectReferenceValue = template; body.FindPropertyRelative("parentKey").stringValue = parent;
            body.FindPropertyRelative("slot").intValue = slot; body.FindPropertyRelative("mode").intValue = (int)OrbitMode.Circular;
            body.FindPropertyRelative("orbitRadiusM").floatValue = role == BodyRole.Planet ? 4000 : 1000;
            body.FindPropertyRelative("phaseDegrees").floatValue = key == "moon_b" ? 180 : 0;
        }
    }
}
