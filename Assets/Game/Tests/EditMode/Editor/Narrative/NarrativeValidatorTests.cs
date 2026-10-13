using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Logic.Nodes;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Narrative;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Editor.Narrative
{
    /// <summary>Exactly the seven SPEC-40 NAR-013 tests, including config/unwired/provider cases in CLI acceptance.</summary>
    public sealed class NarrativeValidatorTests
    {
        [TearDown] public void Cleanup() => DestroyAll();

        [Test] public void V001_ReadNeverWritten()
        {
            var fixture = new Fixture(Bool()); fixture.Source("$story_flag");
            var report = fixture.Validate(); Has(report, "NAR-V001", true);
            var error = report.Entries.Single(e => e.Code == "NAR-V001");
            Assert.AreEqual("content:narfixture_source", error.AssetPath); Assert.AreEqual("condition", error.Field);
            fixture.Source("not $story_flag"); Has(fixture.Validate(), "NAR-V001", false);
            fixture.Source("$story_flag == false"); Has(fixture.Validate(), "NAR-V001", false);
            fixture.Source("$story_flag == true"); Has(fixture.Validate(), "NAR-V001", true);
            fixture.Source("$story_flag", debug: true); Has(fixture.Validate(), "NAR-V001", false);
            fixture.Source("$story_flag", effects: new[] { "set_fact story_flag true" }); Has(fixture.Validate(), "NAR-V001", false);
            var code = new Fixture(Bool(FactFlags.WrittenByCode)); code.Source("$story_flag"); Has(code.Validate(), "NAR-V001", false);
            var debugFact = new Fixture(Bool(FactFlags.Debug)); debugFact.Source("$story_flag"); Has(debugFact.Validate(), "NAR-V001", false);
            var defaultTrue = new Fixture(Bool(defaultValue: 1)); defaultTrue.Source("$story_flag"); Has(defaultTrue.Validate(), "NAR-V001", false);
            defaultTrue.Source("not $story_flag"); Has(defaultTrue.Validate(), "NAR-V001", true);
            var integer = new Fixture(new FactDeclaration { Name = "story_count", Kind = FactKind.Int, Default = 5 });
            integer.Source("$story_count >= 5"); Has(integer.Validate(), "NAR-V001", false);
            integer.Source("$story_count < 5"); Has(integer.Validate(), "NAR-V001", true);
            integer.Source("true", progress: "since_accept(\"story_count\")"); Has(integer.Validate(), "NAR-V001", false);
            integer.Source("$story_count >= credits()"); Has(integer.Validate(), "NAR-V001", false);
            var independent = new Fixture(new FactDeclaration { Name = "story_count", Kind = FactKind.Int, Default = 5 },
                new FactDeclaration { Name = "story_other", Kind = FactKind.Int, Default = 5, Flags = FactFlags.WrittenByCode, Note = "Independent code writer." });
            independent.Source("$story_count != $story_other"); Has(independent.Validate(), "NAR-V001", false);
        }

        [Test] public void V002_WrittenNeverRead()
        {
            var fixture = new Fixture(Bool()); fixture.Source("true", new[] { "set_fact story_flag true" });
            var report = fixture.Validate(); Has(report, "NAR-V002", true);
            Assert.AreEqual(ValidationSeverity.Warning, report.Entries.Single(e => e.Code == "NAR-V002").Severity);
            fixture.Source("$story_flag", new[] { "set_fact story_flag true" }); Has(fixture.Validate(), "NAR-V002", false);
            foreach (var flag in new[] { FactFlags.ReadByCode, FactFlags.Journal })
            { var exempt = new Fixture(Bool(flag)); exempt.Source("true", new[] { "set_fact story_flag true" }); Has(exempt.Validate(), "NAR-V002", false); }
            var count = new Fixture(new FactDeclaration { Name = "story_count", Kind = FactKind.Int });
            count.Source("true", new[] { "add_fact story_count 1" }); Has(count.Validate(), "NAR-V002", true);
        }

        [Test] public void V003_Unused()
        {
            var fixture = new Fixture(Bool()); fixture.Source("true"); Has(fixture.Validate(), "NAR-V003", true);
            fixture.Source("not $story_flag"); Has(fixture.Validate(), "NAR-V003", false);
            foreach (var flag in new[] { FactFlags.Debug, FactFlags.WrittenByCode, FactFlags.ReadByCode, FactFlags.Journal })
            { var exempt = new Fixture(Bool(flag)); exempt.Source("true"); Has(exempt.Validate(), "NAR-V003", false); }
        }

        [Test] public void V004_ClueNeverRevealed()
        {
            var fixture = new Fixture(); fixture.Definitions.Add(Create<ClueDefinition>("clue_test", serialized =>
            { var aliases = serialized.FindProperty("formerIds"); aliases.arraySize = 1; aliases.GetArrayElementAtIndex(0).stringValue = "clue_old"; }));
            fixture.Source("clue_known(\"clue_test\")"); Has(fixture.Validate(), "NAR-V004", true);
            fixture.Source("clue_known(\"clue_test\")", new[] { "reveal_clue clue_test" }); Has(fixture.Validate(), "NAR-V004", false);
            // The default generated-family writer does not exempt clues; only an actual reveal does.
            fixture.Source("true"); Has(fixture.Validate(), "NAR-V004", true);
            fixture.Source("true", new[] { "reveal_clue clue_old" });
            var alias = fixture.Validate(); Has(alias, "NAR-V004", false); Assert.AreEqual(0, alias.ErrorCount, alias.ToText());
        }

        [Test] public void V007_Stale()
        {
            var fixture = new Fixture(Bool()); fixture.Source("not $story_flag"); Has(fixture.Validate(), "NAR-V007", false);
            using (var serialized = new SerializedObject(fixture.Owner))
            { serialized.FindProperty("condition.source").stringValue = "$story_flag"; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            string before = EditorJsonUtility.ToJson(fixture.Owner); bool dirty = EditorUtility.IsDirty(fixture.Owner);
            var report = fixture.Validate(); Has(report, "NAR-E017", true); Has(report, "NAR-V007", true);
            Assert.AreEqual(before, EditorJsonUtility.ToJson(fixture.Owner), "Scratch compilation must preserve stored source, graph and stamps.");
            Assert.AreEqual(dirty, EditorUtility.IsDirty(fixture.Owner)); Assert.IsTrue(fixture.Owner.Condition.IsStale);
            fixture.Source("not $story_flag");
            using (var serialized = new SerializedObject(fixture.Owner))
            { serialized.FindProperty("progress.compilerVersion").intValue = -1; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            report = fixture.Validate(); Assert.IsTrue(report.Entries.Any(e => e.Code == "NAR-V007" && e.Field == "progress"));
            // Preserve failed-owner coverage when QST-002 replaces the formerly throwing quest shell.
            fixture.Definitions.Add(Create<NarrativeValidatorFixtureDefinition>("narfixture_failure", so => Set(so, "failCompile", true)));
            report = fixture.Validate(); Assert.IsTrue(report.Entries.Any(e => e.Code == "NAR-V007" && e.Message.Contains("Fixture compile failure")), report.ToText());
        }

        [Test] public void V008_BoolComparedToInt()
        {
            var fixture = new Fixture(Bool(FactFlags.WrittenByCode));
            foreach (string expression in new[] { "$story_flag == 5", "5 != $story_flag", "$story_flag < -2", "$story_flag == (2 + 3)" })
            { fixture.Source(expression, permitCompileErrors: true); Has(fixture.Validate(), "NAR-V008", true); }
            foreach (string expression in new[] { "$story_flag == true", "$story_flag == 0", "1 == $story_flag" })
            { fixture.Source(expression, permitCompileErrors: true); Has(fixture.Validate(), "NAR-V008", false); }
            fixture.Source("$story_flag == true");
            Assert.AreEqual(0, fixture.Validate().ErrorCount, "A valid Bool comparison and its code writer pass the full validator.");
            fixture.Source("true");
            fixture.Owner.Condition.SetCompiled(new CompareIntCondition(CompareOp.Equal, new FactIntRead("story_flag"), new IntLiteral(5)), false);
            Has(fixture.Validate(), "NAR-V008", true); // Stored graph can disagree with the freshly rebuilt source graph.
            var integer = new Fixture(new FactDeclaration { Name = "story_count", Kind = FactKind.Int });
            integer.Source("$story_count == 5"); Has(integer.Validate(), "NAR-V008", false);
        }

        [Test] public void Cli_ExitCode()
        {
            var fixture = new Fixture(); fixture.Source("true");
            var database = ScriptableObject.CreateInstance<ContentDatabase>();
            string root = Path.Combine(Path.GetTempPath(), "NAR013_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "narrative-validation.txt");
            try
            {
                database.EditorSetDefinitions(fixture.Definitions);
                Assert.AreEqual(0, NarrativeValidatorCli.RunAndGetExitCode(database, path));
                StringAssert.Contains("0 errors", File.ReadAllText(path));
                fixture.Definitions.RemoveAll(d => d is NarrativeConfigDefinition);
                database.EditorSetDefinitions(fixture.Definitions);
                Assert.AreEqual(1, NarrativeValidatorCli.RunAndGetExitCode(database, path));
                Has(fixture.Validate(), "NAR-V005", true); Has(fixture.Validate(), "NAR-V010", true);
                fixture.Definitions.Add(Create<NarrativeConfigDefinition>("narrative_one"));
                fixture.Definitions.Add(Create<NarrativeConfigDefinition>("narrative_two")); Has(fixture.Validate(), "NAR-V005", true);
                fixture.Definitions.RemoveAt(fixture.Definitions.Count - 1); Has(fixture.Validate(), "NAR-V005", false);
                fixture.Source("true", new[] { "damage_ship 1" });
                var warning = fixture.Validate(unwired: new[] { "DamagePlayerShip" });
                Has(warning, "NAR-V006", true); Has(warning, "NAR-W020", true); Assert.AreEqual(0, warning.ErrorCount, warning.ToText());
                Has(fixture.Validate(unwired: Array.Empty<string>()), "NAR-V006", false);
                var order = new List<int>();
                var providers = new INarrativeRuleProvider[] { new Provider(30, order), new Provider(10, order) };
                fixture.Validate(providers: providers); CollectionAssert.AreEqual(new[] { 10, 30 }, order);
            }
            finally { UnityEngine.Object.DestroyImmediate(database); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        static FactDeclaration Bool(FactFlags flags = FactFlags.None, int defaultValue = 0) => new FactDeclaration {
            Name = "story_flag", Kind = FactKind.Bool, Default = defaultValue, Flags = flags, Note = "Fixture code/journal owner.", JournalText = "Fixture journal."
        };
        static void Has(ValidationReport report, string code, bool expected) => Assert.AreEqual(expected, report.Entries.Any(e => e.Code == code), code + "\n" + report.ToText());
        sealed class Provider : INarrativeRuleProvider
        {
            readonly List<int> calls;
            public int Order { get; }
            internal Provider(int order, List<int> calls) { Order = order; this.calls = calls; }
            public void Run(NarrativeValidationContext context, ValidationReport report)
            {
                Assert.IsNotNull(context.Compiler); Assert.IsNotNull(context.Refs); Assert.Greater(context.Graphs.Count, 0);
                foreach (var graph in context.Graphs)
                {
                    Assert.AreNotSame(graph.SourceOwner, graph.Owner, "Providers receive scratch owners matching their compiled wrappers.");
                    Assert.IsEmpty(AssetDatabase.GetAssetPath(graph.Owner));
                    if (graph.Field == "condition") Assert.AreSame(((NarrativeValidatorFixtureDefinition)graph.Owner).Condition, graph.Wrapper);
                }
                Assert.Greater(context.Usage.WorldUses.Count, 0); calls.Add(Order);
            }
        }
        sealed class Fixture
        {
            internal readonly List<ContentDefinition> Definitions = FactCatalogTests.BaseContent();
            internal readonly NarrativeValidatorFixtureDefinition Owner;
            internal Fixture(params FactDeclaration[] facts)
            {
                Definitions.Add(Create<NarrativeConfigDefinition>("narrative_test"));
                Definitions.Add(FactCatalogTests.Facts("facts_test", facts));
                Owner = Create<NarrativeValidatorFixtureDefinition>("narfixture_source"); Definitions.Add(Owner);
            }
            internal void Source(string condition, string[] effects = null, bool debug = false, string progress = "", bool permitCompileErrors = false)
            {
                using (var serialized = new SerializedObject(Owner))
                {
                    serialized.FindProperty("condition.source").stringValue = condition;
                    serialized.FindProperty("progress.source").stringValue = progress;
                    serialized.FindProperty("debug").boolValue = debug;
                    var source = serialized.FindProperty("effects.source"); source.arraySize = effects?.Length ?? 0;
                    for (int i = 0; i < source.arraySize; i++) source.GetArrayElementAtIndex(i).stringValue = effects[i];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var report = new ValidationReport("fixture compile"); var catalog = ContentCatalog.Build(Definitions, report);
                var refs = RefResolverRegistry.CreateDefault(catalog); var facts = FactCatalog.Build(catalog, refs, report);
                Owner.CompileLogic(new LogicCompiler(facts, refs, new LogicFunctionRegistry()), report.For("fixture"));
                if (!permitCompileErrors) Assert.AreEqual(0, report.ErrorCount, report.ToText());
            }
            internal ValidationReport Validate(IEnumerable<INarrativeRuleProvider> providers = null, IReadOnlyCollection<string> unwired = null)
            {
                var report = new ValidationReport("validator fixture"); var catalog = ContentCatalog.Build(Definitions, report);
                new NarrativeValidator().Validate(catalog, report, providers ?? Array.Empty<INarrativeRuleProvider>(), unwired ?? Array.Empty<string>());
                return report;
            }
        }
    }
}
