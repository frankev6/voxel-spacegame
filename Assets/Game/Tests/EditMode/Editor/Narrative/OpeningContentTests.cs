using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Migration.Narrative;
using Game.Editor.Narrative;
using Game.Editor.Narrative.Dialog;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Narrative
{
    /// <summary>SPEC-42 QST-009: read the actual imported database; never repair content inside acceptance tests.</summary>
    public sealed class OpeningContentTests
    {
        static readonly (string Template, string Tag)[] Tags = {
            ("body_station_ark", "ark_station"), ("body_station", "station"), ("body_station_abandoned", "abandoned_station"),
            ("body_derelict_ship", "derelict"), ("body_debris_field", "debris_field"), ("body_resource_cache", "resource_cache"),
            ("body_gate_dormant", "gate_dormant"), ("body_gate_active", "gate_active"), ("body_jump_point", "jump_point"),
            ("body_asteroid_belt", "asteroid_belt"), ("body_planet_barren", "barren_planet"), ("body_planet_gas_giant", "gas_giant"),
            ("body_planet_ice", "ice_planet"), ("body_planet_ice_giant", "ice_giant"), ("body_planet_rocky", "rocky_planet"),
            ("body_planet_terrestrial", "terrestrial_planet"), ("body_planet_water", "ocean_planet"), ("body_moon", "moon"),
            ("body_star", "star"), ("body_star_blackhole", "black_hole")
        };
        static ContentCatalog Read(out ContentDatabase database)
        {
            database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath);
            Assert.IsNotNull(database, "Import/rebuild the production database before running acceptance.");
            var actual = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentDatabaseBuilder.ContentRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<ContentDefinition>()).ToArray();
            CollectionAssert.AreEquivalent(actual, database.Definitions, "The database must contain the whole content tree.");
            var report = new ValidationReport("Opening catalog");
            var catalog = ContentCatalog.Build(database.Definitions, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            return catalog;
        }
        static T Get<T>(ContentCatalog catalog, string id) where T : ContentDefinition
        {
            Assert.IsTrue(catalog.TryGet(new ContentId(id), out var definition), "Missing production definition: " + id);
            Assert.IsInstanceOf<T>(definition, id);
            return (T)definition;
        }
        [Test] public void ValidatesClean()
        {
            var catalog = Read(out var database);
            var validators = TypeCache.GetTypesDerivedFrom<IContentDatabaseValidator>().ToArray();
            foreach (var type in new[] { typeof(NarrativeValidator), typeof(Game.Editor.Content.Validators.GalaxyContentValidator),
                typeof(Game.Editor.Content.Validators.EncounterContentValidator), typeof(Game.Editor.Content.Validators.EconomyContentValidator) })
                Assert.Contains(type, validators, "Whole-database validation must include " + type.Name);
            var providers = TypeCache.GetTypesDerivedFrom<INarrativeRuleProvider>().ToArray();
            foreach (var type in new[] { typeof(DialogValidatorRules), typeof(BeatValidatorRules), typeof(QuestValidatorRules), typeof(MysteryValidatorRules) })
                Assert.Contains(type, providers, "Narrative validation must include " + type.Name);
            var report = ContentValidatorRunner.Validate(database);
            var families = new[] { "NAR-", "DLG-", "QST-", "GAL-", "ENC-", "ECO-" };
            var errors = report.Entries.Where(entry => entry.Severity == ValidationSeverity.Error &&
                (families.Any(prefix => entry.Code.StartsWith(prefix, StringComparison.Ordinal)) || entry.Code == "DATA-E099")).ToArray();
            Assert.IsEmpty(errors, report.ToText(int.MaxValue));

            var opening = Get<FactCatalogDefinition>(catalog, "facts_opening");
            CollectionAssert.AreEquivalent(new[] { "opening_cache_found", "opening_chapter_done" }, opening.Facts.Select(fact => fact.Name));
            foreach (var fact in opening.Facts)
            {
                Assert.AreEqual(1, catalog.All<FactCatalogDefinition>().Sum(set => set.Facts.Count(row => row.Name == fact.Name)), fact.Name);
                Assert.AreEqual(FactKind.Bool, fact.Kind); Assert.AreEqual(FactScope.Save, fact.Scope); Assert.AreEqual(0, fact.Default);
                Assert.AreEqual(fact.Name == "opening_chapter_done" ? FactFlags.Journal : FactFlags.None, fact.Flags);
                Assert.AreEqual(fact.Name == "opening_chapter_done" ? "The way to Whisper is open." : "", fact.JournalText ?? "");
            }
            Assert.IsFalse(Get<FactCatalogDefinition>(catalog, "facts_legacy").Facts.Any(fact => fact.Name.StartsWith("opening_", StringComparison.Ordinal)));

            var cache = Get<StoryBeatDefinition>(catalog, "beat_drift_survey_cache");
            Assert.AreEqual(BeatTrigger.Interact, cache.Trigger); Assert.AreEqual(BeatRepeat.Once, cache.Repeat);
            Assert.AreSame(Get<AuthoredStarSystemDefinition>(catalog, "starsys_drift"), cache.Where.starSystem);
            CollectionAssert.AreEqual(new[] { "resource_cache" }, cache.Where.bodyTags);
            Assert.IsNull(cache.Where.hailEncounter); Assert.AreEqual(40, cache.Salience); Assert.AreEqual(1f, cache.Weight);
            Assert.AreEqual("quest_active(\"quest_survey_local_materials\")", cache.Condition.Source);
            Assert.AreEqual("opening_cache", cache.YarnNode); Assert.IsFalse(cache.FirstVisitOnly); Assert.IsFalse(cache.Debug);
            Assert.IsFalse(cache.Condition.IsStale); Assert.IsFalse(cache.Condition.HasErrors); Assert.IsFalse(cache.Condition.IsAlwaysTrue);
            Assert.IsEmpty(cache.OnStart.Source); Assert.IsEmpty(cache.OnComplete.Source);

            var gate = Get<StoryBeatDefinition>(catalog, "beat_gate_awakens");
            Assert.AreEqual(BeatTrigger.Condition, gate.Trigger); Assert.AreEqual(BeatRepeat.Once, gate.Repeat);
            Assert.AreEqual("$gate_ark_start_active and not $opening_chapter_done", gate.Condition.Source);
            Assert.IsNull(gate.Where.starSystem); Assert.IsNull(gate.Where.hailEncounter); Assert.IsEmpty(gate.Where.bodyTags);
            Assert.IsTrue(string.IsNullOrEmpty(gate.YarnNode)); Assert.IsEmpty(gate.OnStart.Source);
            CollectionAssert.AreEqual(new[] { "set_fact opening_chapter_done true", "notify \"The gate to Whisper hums awake.\"" }, gate.OnComplete.Source);
            Assert.IsFalse(gate.FirstVisitOnly); Assert.IsFalse(gate.Debug);
            Assert.IsFalse(gate.Condition.IsStale); Assert.IsFalse(gate.Condition.HasErrors); Assert.IsFalse(gate.Condition.IsAlwaysTrue);
            Assert.IsFalse(gate.OnComplete.IsStale); Assert.IsFalse(gate.OnComplete.HasErrors); Assert.IsFalse(gate.OnComplete.IsEmpty);

            const string yarn = "Assets/Game/Content/Narrative/Dialog/Opening/opening.yarn";
            var script = DialogScriptParser.Parse(yarn, File.ReadAllText(yarn));
            Assert.IsFalse(script.HasErrors, script.Diagnostics.ToText()); Assert.AreEqual(1, script.Nodes.Count);
            var node = script.Nodes.Single(); Assert.AreEqual(cache.YarnNode, node.Title); Assert.AreEqual(3, node.Body.Count);
            CollectionAssert.AreEqual(new[] { "A battered cargo pod, tagged with an ARK survey stencil.", "Inside: refined palladium, enough for NORA's repairs." },
                node.Body.OfType<LineStmt>().Select(line => line.Text));
            var option = node.Body.OfType<OptionGroupStmt>().Single().Options.Single(); Assert.AreEqual("Take it", option.Text);
            Assert.AreEqual(3, option.Body.Count); Assert.IsInstanceOf<StopStmt>(option.Body[2]);
            CollectionAssert.AreEqual(new[] { "give_item resource_palladium 3", "set_fact opening_cache_found true" }, option.Body.OfType<CommandStmt>().Select(command => command.Text));
            var table = Get<DialogCommandTableDefinition>(catalog, "dialog_command_table");
            foreach (var command in option.Body.OfType<CommandStmt>())
                Assert.AreEqual(1, table.Entries.Count(entry => entry.Text == command.Text && !entry.Effect.IsStale && !entry.Effect.HasErrors && !entry.Effect.IsEmpty), command.Text);

            // Read the owner's stored program, not a program assembled or repaired by this test.
            var program = Get<DialogProgramDefinition>(catalog, "dialog_program");
            var context = DialogCommandTableGenerator.Context(catalog, new ValidationReport("Opening stored hash"));
            var sources = DialogCommandTableGenerator.ReadSources(DialogCommandTableGenerator.Root);
            var plan = DialogCommandTableGenerator.Prepare(context, sources, DialogCommandTableGenerator.Root);
            Assert.AreEqual(plan.Hash, table.SourcesHash); Assert.AreEqual(plan.Hash, program.SourcesHash);
            CollectionAssert.AreEqual(DialogCommandTableGenerator.Parse(sources).SelectMany(source => source.Nodes).Select(value => value.Title).OrderBy(title => title, StringComparer.Ordinal),
                program.Nodes.Select(value => value.Title), "Every current source node belongs in the real stored graph.");
            Assert.IsTrue(program.Conditions.All(value => !value.IsStale && !value.HasErrors));
            Assert.IsTrue(program.IntExprs.All(value => !value.IsStale && !value.HasErrors));
            var compiledNode = program.Nodes.Single(value => value.Title == cache.YarnNode);
            Assert.GreaterOrEqual(compiledNode.Start, 0); Assert.Greater(compiledNode.Length, 0);
            Assert.LessOrEqual(compiledNode.Start + compiledNode.Length, program.Code.Count);
            var code = program.Code.Skip(compiledNode.Start).Take(compiledNode.Length).ToArray();
            CollectionAssert.AreEqual(node.Body.OfType<LineStmt>().Select(value => value.Text), code.Where(value => value.Op == DialogOp.Line).Select(value => program.Strings[value.A]));
            Assert.AreEqual(1, code.Count(value => value.Op == DialogOp.Option));
            Assert.AreEqual("Take it", program.Strings[code.Single(value => value.Op == DialogOp.Option).A]);
            CollectionAssert.AreEqual(option.Body.OfType<CommandStmt>().Select(value => value.Text), code.Where(value => value.Op == DialogOp.Command).Select(value => program.Strings[value.A]));
            Assert.AreEqual(1, code.Count(value => value.Op == DialogOp.Stop));

            // NAR-016 already authored §4.11. Check its real assets and links instead of creating duplicate identities.
            Assert.AreEqual(7, catalog.All<ClueDefinition>().Count(clue => clue is not RumorDefinition));
            Assert.AreEqual(6, catalog.All<RumorDefinition>().Count); Assert.AreEqual(3, catalog.All<MysteryDefinition>().Count);
            foreach (var row in LegacyClueWriter.Texts)
            {
                var clue = Get<ClueDefinition>(catalog, row.Id);
                Assert.AreEqual(row.Title, clue.Title, row.Id); Assert.AreEqual(row.Body, clue.Body, row.Id);
                Assert.AreEqual(row.Mystery, clue.Mystery?.IdString ?? "", row.Id); Assert.IsEmpty(clue.Links, row.Id);
                if (clue is RumorDefinition rumor)
                {
                    Assert.AreEqual(row.Target, rumor.Target?.IdString ?? "", row.Id);
                    Assert.AreEqual(row.Tag, rumor.TargetBodyTag ?? "", row.Id); Assert.AreEqual(row.Condition, rumor.ResolvedWhen.Source, row.Id);
                }
            }
            for (int i = 0; i < LegacyClueWriter.Mysteries.Length; i++)
            {
                var row = LegacyClueWriter.Mysteries[i]; var mystery = Get<MysteryDefinition>(catalog, row.Id);
                Assert.AreEqual(row.Title, mystery.Title); Assert.AreEqual(row.Solved, mystery.SolvedText); Assert.AreEqual(i, mystery.Order);
                CollectionAssert.AreEqual(row.Requires, mystery.Requires.Select(clue => clue.IdString));
                Assert.IsTrue(mystery.Requires.All(clue => clue.Mystery == mystery)); Assert.IsEmpty(mystery.OnSolved.Source);
            }
            Assert.AreEqual(InteractionKind.Inspect, Get<BodyTemplateDefinition>(catalog, "body_derelict_ship").Interaction,
                "SPEC-42's first clue needs an Inspect; Salvage bypasses the narrative bridge.");
            Topology(catalog);
        }
        static void Topology(ContentCatalog catalog)
        {
            var ark = Get<AuthoredStarSystemDefinition>(catalog, "starsys_ark_start");
            var drift = Get<AuthoredStarSystemDefinition>(catalog, "starsys_drift");
            var whisper = Get<AuthoredStarSystemDefinition>(catalog, "starsys_whisper");
            var galaxy = Get<GalaxyGenerationDefinition>(catalog, "galaxy_main");
            CollectionAssert.AreEquivalent(new[] { ark, drift, whisper }, galaxy.AuthoredStarSystems);
            Assert.AreSame(ark, galaxy.StartStarSystem); Assert.AreEqual("ark_station", ark.StartBodyKey);
            Assert.AreEqual(0, ark.Danger); Assert.AreEqual(2, drift.Danger); Assert.AreEqual(1, whisper.Danger);
            Assert.AreEqual(new UnityEngine.Vector2(0, 0), ark.PositionLy); Assert.AreEqual(new UnityEngine.Vector2(6, 2), drift.PositionLy);
            Assert.AreEqual(new UnityEngine.Vector2(-5, 4), whisper.PositionLy); Assert.AreEqual(2, galaxy.AuthoredLanes.Count);
            var open = galaxy.AuthoredLanes.Single(value => value.a == drift || value.b == drift);
            Assert.IsTrue(open.a == ark || open.b == ark); Assert.IsTrue(open.condition.IsAlwaysTrue); Assert.IsFalse(open.condition.IsStale);
            var gate = galaxy.AuthoredLanes.Single(value => value.a == whisper || value.b == whisper);
            Assert.IsTrue(gate.a == ark || gate.b == ark); Assert.AreEqual("$gate_ark_start_active", gate.condition.Source);
            Assert.IsFalse(gate.condition.IsStale); Assert.IsFalse(gate.condition.HasErrors); Assert.IsFalse(gate.condition.IsAlwaysTrue);
            Assert.AreEqual("The gate is dormant.", gate.lockedMessage);
            Assert.AreSame(Get<BodyTemplateDefinition>(catalog, "body_gate_dormant"), gate.gateTemplateA); Assert.AreSame(gate.gateTemplateA, gate.gateTemplateB);
            Assert.Contains(ark, Get<FactionDefinition>(catalog, "faction_ark").OwnedStarSystems.ToArray());
            Assert.Contains(whisper, Get<FactionDefinition>(catalog, "faction_vestari").OwnedStarSystems.ToArray());
            Assert.IsFalse(catalog.All<FactionDefinition>().Any(faction => faction.OwnedStarSystems.Contains(drift)));
            Assert.AreEqual("body_station", whisper.Bodies.Single(body => body.key == "whisper_outpost").template.IdString);
        }
        [Test] public void TagsCovered()
        {
            var catalog = Read(out _);
            foreach (var row in Tags) Assert.AreEqual(row.Tag, Get<BodyTemplateDefinition>(catalog, row.Template).StoryTag, row.Template);
            CollectionAssert.AreEquivalent(Tags.Select(row => row.Tag), Get<NarrativeConfigDefinition>(catalog, "narrative_config").KnownBodyTags);
            var templates = catalog.All<BodyTemplateDefinition>();
            var beats = catalog.All<StoryBeatDefinition>(); Assert.IsNotEmpty(beats);
            foreach (var beat in beats)
                foreach (string tag in beat.Where.bodyTags)
                    Assert.IsTrue(templates.Any(template => template.StoryTag == tag), beat.IdString + " needs an actual template for " + tag);
            var drift = Get<AuthoredStarSystemDefinition>(catalog, "starsys_drift");
            Assert.AreEqual("resource_cache", drift.Bodies.Single(body => body.key == "drift_cache").template.StoryTag);
            Assert.AreEqual("derelict", drift.Bodies.Single(body => body.key == "drift_derelict").template.StoryTag);
        }
    }
}
