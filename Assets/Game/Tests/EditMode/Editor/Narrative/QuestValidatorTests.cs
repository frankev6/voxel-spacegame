using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Narrative;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Editor.Narrative
{
    /// <summary>Exactly SPEC-42's twelve fail/pass pairs and one usage case; in-memory authoring and real compiler/binder.</summary>
    public sealed class QuestValidatorTests
    {
        sealed class Fixture
        {
            internal readonly List<ContentDefinition> Definitions = FactCatalogTests.BaseContent();
            internal Fixture()
            {
                Definitions.Add(Create<NarrativeConfigDefinition>("narrative_qst", so =>
                { var tags = so.FindProperty("knownBodyTags"); tags.arraySize = 1; tags.GetArrayElementAtIndex(0).stringValue = "known_tag"; }));
                Definitions.Add(FactCatalogTests.Facts("facts_qst", new FactDeclaration { Name = "story_count", Kind = FactKind.Int }, new FactDeclaration { Name = "story_flag", Kind = FactKind.Bool }));
            }
            internal T Add<T>(string id) where T : ContentDefinition { var asset = Create<T>(id); Definitions.Add(asset); return asset; }
            internal StoryBeatDefinition Beat(string id = "beat_test", string node = "", BeatTrigger trigger = BeatTrigger.Manual)
            {
                var beat = Add<StoryBeatDefinition>(id); SetField(beat, "trigger", trigger); SetField(beat, "yarnNode", node);
                Effects(beat.OnStart, "give_credits 1"); return beat;
            }
            internal QuestDefinition Quest(string id = "quest_test", QuestKind kind = QuestKind.Debug)
            {
                var quest = Add<QuestDefinition>(id); SetField(quest, "kind", kind);
                ((List<QuestStage>)quest.Stages).Add(Stage("stage_one", "objective_one")); return quest;
            }
            internal AuthoredStarSystemDefinition System() => Add<AuthoredStarSystemDefinition>("starsys_test");
            internal DialogProgramDefinition Program(params string[] titles)
            {
                var program = Add<DialogProgramDefinition>("dialog_program");
                SetField(program, "nodes", titles.Select((title, index) => new DialogNodeRecord { Title = title, Start = index, Length = 1 }).ToArray());
                SetField(program, "code", titles.Select(_ => new DialogInstruction { Op = DialogOp.Stop }).ToArray()); return program;
            }
            internal DialogCommandTableDefinition Table(params string[] commands)
            {
                var table = Add<DialogCommandTableDefinition>("dialog_command_table");
                SetField(table, "entries", commands.Select(text => new DialogCommandEntry { Text = text, Effect = new CompiledEffectList() }).ToArray()); return table;
            }
            internal ValidationReport Validate()
            {
                var report = new ValidationReport("QST fixture"); var content = ContentCatalog.Build(Definitions, report);
                var refs = RefResolverRegistry.CreateDefault(content); var facts = FactCatalog.Build(content, refs, report);
                var compiler = new LogicCompiler(facts, refs, new LogicFunctionRegistry()); var ctx = new NarrativeValidationContext(content, facts, refs, compiler, Array.Empty<string>());
                var binder = new LogicBinder(facts, refs, report);
                foreach (var definition in Definitions)
                {
                    if (definition is not StoryBeatDefinition && definition is not QuestDefinition && definition is not RumorDefinition && definition is not MysteryDefinition) continue;
                    var owner = (ILogicSourceOwner)definition; string path = NarrativeValidationContext.PathOf(definition);
                    owner.CompileLogic(compiler, report.For(path)); owner.BindLogic(binder);
                    foreach (var field in NarrativeValidationContext.Wrappers(definition)) ctx.AddGraph(definition, path, field.Field, field.Wrapper, NarrativeValidationContext.IsDebug(definition));
                }
                new BeatValidatorRules().Run(ctx, report); new QuestValidatorRules().Run(ctx, report); new MysteryValidatorRules().Run(ctx, report);
                return report;
            }
        }
        [TearDown] public void Cleanup() => DestroyAll();
        static void SetField(object owner, string name, object value)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) { field.SetValue(owner, value); return; }
            }
            throw new ArgumentException(name);
        }
        static void Source(object wrapper, string text) => SetField(wrapper, "source", text);
        static void Effects(CompiledEffectList effects, params string[] lines) => SetField(effects, "source", lines);
        static QuestStage Stage(string id, string objectiveId)
        { var stage = new QuestStage { id = id }; var objective = new QuestObjective { id = objectiveId }; Source(objective.doneWhen, "true"); stage.objectives.Add(objective); return stage; }
        static void Check(Fixture fixture, string code, bool fail)
        {
            var report = fixture.Validate(); var entries = report.Entries.Where(entry => entry.Code == code).ToArray();
            Assert.AreEqual(fail, entries.Length > 0, report.ToText());
            Assert.IsFalse(report.Entries.Any(entry => entry.Code == "DATA-E099" || entry.Code.StartsWith("NAR-E", StringComparison.Ordinal)), report.ToText());
            if (fail) foreach (var entry in entries)
            { Assert.IsNotEmpty(entry.AssetPath); Assert.AreEqual(code == "QST-V005" || code == "QST-V010" || code == "QST-V012" ? ValidationSeverity.Warning : ValidationSeverity.Error, entry.Severity); }
        }
        [Test] public void V001_Fails()
        {
            var f = new Fixture(); var beat = f.Beat(node: "absent"); Check(f, "QST-V001", true);
            SetField(beat, "yarnNode", ""); Effects(beat.OnStart); Check(f, "QST-V001", true);
        }
        [Test] public void V001_Passes()
        { var f = new Fixture(); f.Program("intro"); f.Beat(node: "intro"); Check(f, "QST-V001", false); var effects = new Fixture(); effects.Beat(); Check(effects, "QST-V001", false); }
        [Test] public void V002_Fails()
        {
            foreach (var trigger in new[] { BeatTrigger.Interact, BeatTrigger.Hail }) { var f = new Fixture(); f.Beat(trigger: trigger); Check(f, "QST-V002", true); }
            foreach (var trigger in new[] { BeatTrigger.GameStart, BeatTrigger.Manual, BeatTrigger.Condition })
            { var f = new Fixture(); var beat = f.Beat(trigger: trigger); beat.Where.starSystem = f.System(); Check(f, "QST-V002", true); }
        }
        [Test] public void V002_Passes()
        {
            var f = new Fixture(); var beat = f.Beat(trigger: BeatTrigger.Interact); beat.Where.bodyTags = new[] { "known_tag" }; Check(f, "QST-V002", false);
            beat.Where.bodyTags = Array.Empty<string>(); beat.Where.starSystem = f.System(); Check(f, "QST-V002", false);
            var hail = new Fixture(); var hailBeat = hail.Beat(trigger: BeatTrigger.Hail); hailBeat.Where.hailEncounter = hail.Add<EncounterDefinition>("encounter_test"); Check(hail, "QST-V002", false);
            foreach (var trigger in new[] { BeatTrigger.GameStart, BeatTrigger.Manual, BeatTrigger.Condition, BeatTrigger.StarSystemEntered }) { var free = new Fixture(); free.Beat(trigger: trigger); Check(free, "QST-V002", false); }
        }
        [Test] public void V003_Fails()
        {
            var f = new Fixture(); var beat = f.Beat(trigger: BeatTrigger.Interact); beat.Where.bodyTags = new[] { "bad_tag" };
            var quest = f.Quest(); quest.Stages[0].objectives[0].marker = new QuestMarker { starSystem = f.System(), bodyTag = "bad_tag" };
            var rumor = f.Add<RumorDefinition>("rumor_test"); SetField(rumor, "targetBodyTag", "bad_tag"); Check(f, "QST-V003", true);
        }
        [Test] public void V003_Passes()
        {
            var f = new Fixture(); var body = f.Add<BodyTemplateDefinition>("body_tagged"); SetField(body, "storyTag", "body_tag");
            var beat = f.Beat(trigger: BeatTrigger.Interact); beat.Where.bodyTags = new[] { "known_tag", "body_tag" };
            var quest = f.Quest(); quest.Stages[0].objectives[0].marker = new QuestMarker { starSystem = f.System(), bodyTag = "body_tag" };
            var rumor = f.Add<RumorDefinition>("rumor_test"); SetField(rumor, "targetBodyTag", "known_tag"); Check(f, "QST-V003", false);
        }
        [Test] public void V004_Fails()
        {
            var empty = new Fixture(); empty.Add<QuestDefinition>("quest_empty"); Check(empty, "QST-V004", true);
            var f = new Fixture(); var quest = f.Quest(); quest.Stages[0].objectives.Clear(); Check(f, "QST-V004", true);
            quest.Stages[0].objectives.Add(new QuestObjective { id = "good_id" }); Check(f, "QST-V004", true);
            Source(quest.Stages[0].objectives[0].doneWhen, "true"); quest.Stages[0].objectives[0].id = "Bad Id"; Check(f, "QST-V004", true);
            var duplicate = new Fixture(); var q = duplicate.Quest(); ((List<QuestStage>)q.Stages).Add(Stage("stage_one", "objective_one")); Check(duplicate, "QST-V004", true);
            var many = new Fixture(); var big = many.Quest(); for (int i = 1; i <= 16; i++) ((List<QuestStage>)big.Stages).Add(Stage("stage_" + i, "objective_" + i)); Check(many, "QST-V004", true);
            var objectives = new Fixture(); var o = objectives.Quest(); for (int i = 1; i <= 16; i++) o.Stages[0].objectives.Add(Stage("unused", "objective_" + i).objectives[0]); Check(objectives, "QST-V004", true);
        }
        [Test] public void V004_Passes()
        {
            var f = new Fixture(); var q = f.Quest(); Check(f, "QST-V004", false);
            for (int i = 1; i < 16; i++) ((List<QuestStage>)q.Stages).Add(Stage("stage_" + i, "objective_" + i));
            for (int j = 1; j < 16; j++) q.Stages[0].objectives.Add(Stage("unused", "extra_objective_" + j).objectives[0]); Check(f, "QST-V004", false);
        }
        [Test] public void V005_Fails() { var f = new Fixture(); f.Quest(kind: QuestKind.Side); Check(f, "QST-V005", true); }
        [Test] public void V005_Passes()
        {
            var debug = new Fixture(); debug.Quest(); Check(debug, "QST-V005", false);
            var effects = new Fixture(); effects.Quest(kind: QuestKind.Story); Effects(effects.Beat().OnComplete, "start_quest quest_test"); Check(effects, "QST-V005", false);
            var table = new Fixture(); table.Quest(kind: QuestKind.Side); table.Table("start_quest quest_test"); Check(table, "QST-V005", false);
            var mystery = new Fixture(); mystery.Quest(kind: QuestKind.Side); var m = mystery.Add<MysteryDefinition>("mystery_test"); Effects(m.OnSolved, "start_quest quest_test"); Check(mystery, "QST-V005", false);
        }
        [Test] public void V006_Fails() { var f = new Fixture(); f.Quest(); f.Table("quest_log quest_test absent_entry"); Check(f, "QST-V006", true); }
        [Test] public void V006_Passes()
        { var f = new Fixture(); var q = f.Quest(); ((List<QuestLogEntry>)q.LogEntries).Add(new QuestLogEntry { id = "log_one", text = "Journal" }); Effects(f.Beat().OnStart, "quest_log quest_test log_one"); Check(f, "QST-V006", false); }
        [Test] public void V007_Fails() { var f = new Fixture(); f.Add<ClueDefinition>("clue_test"); f.Add<RumorDefinition>("rumor_test"); Check(f, "QST-V007", true); }
        [Test] public void V007_Passes()
        {
            var f = new Fixture(); f.Add<ClueDefinition>("clue_test"); f.Add<RumorDefinition>("rumor_test"); f.Table("reveal_clue clue_test", "reveal_clue rumor_test"); Check(f, "QST-V007", false);
            var solved = new Fixture(); solved.Add<ClueDefinition>("clue_test"); var mystery = solved.Add<MysteryDefinition>("mystery_test"); Effects(mystery.OnSolved, "reveal_clue clue_test"); Check(solved, "QST-V007", false);
        }
        [Test] public void V008_Fails()
        {
            var f = new Fixture(); var mystery = f.Add<MysteryDefinition>("mystery_test"); Check(f, "QST-V008", true);
            ((List<ClueDefinition>)mystery.Requires).Add(f.Add<ClueDefinition>("clue_other")); Check(f, "QST-V008", true);
        }
        [Test] public void V008_Passes()
        {
            var f = new Fixture(); var mystery = f.Add<MysteryDefinition>("mystery_test"); var clue = f.Add<ClueDefinition>("clue_test");
            SetField(clue, "mystery", mystery); ((List<ClueDefinition>)mystery.Requires).Add(clue); Check(f, "QST-V008", false);
        }
        [Test] public void V009_Fails()
        { var f = new Fixture(); f.Quest("contract_bad", QuestKind.Side); Check(f, "QST-V009", true); var inverse = new Fixture(); inverse.Quest(kind: QuestKind.Contract); Check(inverse, "QST-V009", true); }
        [Test] public void V009_Passes()
        { var f = new Fixture(); f.Quest("contract_good", QuestKind.Contract); Check(f, "QST-V009", false); var story = new Fixture(); story.Quest(kind: QuestKind.Story); Check(story, "QST-V009", false); }
        [Test] public void V010_Fails() { var f = new Fixture(); f.Program("intro", "unreachable"); f.Beat(node: "intro"); Check(f, "QST-V010", true); }
        [Test] public void V010_Passes()
        {
            foreach (var op in new[] { DialogOp.Jump, DialogOp.Detour })
            {
                var f = new Fixture(); var program = f.Program("intro", "next"); f.Beat(node: "intro"); SetField(program, "strings", new[] { "next", "intro" });
                SetField(program, "code", new[] { new DialogInstruction { Op = op, A = 0 }, new DialogInstruction { Op = DialogOp.Jump, A = 1 } }); Check(f, "QST-V010", false);
            }
        }
        [Test] public void V011_Fails()
        { var f = new Fixture(); f.Quest().Stages[0].objectives[0].marker.bodyTag = "known_tag"; SetField(f.Add<RumorDefinition>("rumor_test"), "targetBodyTag", "known_tag"); Check(f, "QST-V011", true); }
        [Test] public void V011_Passes()
        { var f = new Fixture(); var system = f.System(); var marker = f.Quest().Stages[0].objectives[0].marker; marker.starSystem = system; marker.bodyTag = "known_tag";
          var rumor = f.Add<RumorDefinition>("rumor_test"); SetField(rumor, "target", system); SetField(rumor, "targetBodyTag", "known_tag"); Check(f, "QST-V011", false); }
        [Test] public void V012_Fails()
        { var f = new Fixture(); var a = f.Beat("beat_a", trigger: BeatTrigger.Interact); var b = f.Beat("beat_b", trigger: BeatTrigger.Interact);
          a.Where.bodyTags = new[] { "known_tag", "body_tag" }; b.Where.bodyTags = new[] { "body_tag", "known_tag" }; Check(f, "QST-V012", true); }
        [Test] public void V012_Passes()
        {
            var f = new Fixture(); f.Beat("beat_a"); var b = f.Beat("beat_b"); SetField(b, "salience", 1); Check(f, "QST-V012", false);
            SetField(b, "salience", 0); SetField(b, "debug", true); Check(f, "QST-V012", false);
            SetField(b, "debug", false); Source(b.Condition, "true"); Check(f, "QST-V012", false);
        }
        [Test] public void Usage_Enforced()
        {
            var f = new Fixture(); var beat = f.Beat("beat_usage"); var quest = f.Quest(); var rumor = f.Add<RumorDefinition>("rumor_usage"); var mystery = f.Add<MysteryDefinition>("mystery_usage");
            var clue = f.Add<ClueDefinition>("clue_usage"); SetField(clue, "mystery", mystery); ((List<ClueDefinition>)mystery.Requires).Add(clue);
            Source(beat.Condition, "$story_flag"); Source(quest.FailWhen, "$story_flag"); Source(quest.Stages[0].objectives[0].doneWhen, "$story_flag");
            Source(quest.Stages[0].objectives[0].progress, "$story_count"); Source(rumor.ResolvedWhen, "$story_flag");
            foreach (var effects in new[] { beat.OnStart, beat.OnComplete, quest.OnAccepted, quest.OnCompleted, quest.OnFailed, quest.Stages[0].onComplete, mystery.OnSolved }) Effects(effects, "give_credits 1");
            Effects(mystery.OnSolved, "give_credits 1", "reveal_clue clue_usage", "reveal_clue rumor_usage");
            var valid = f.Validate(); Assert.IsFalse(valid.HasErrors, valid.ToText());
            // Verify every owned logic field was compiled and bound, and Validate no longer throws at runtime.
            foreach (var definition in new ContentDefinition[] { beat, quest, rumor, mystery, clue })
            {
                var local = new ValidationReport(definition.IdString); definition.Validate(local.For("test:" + definition.IdString), null); Assert.IsFalse(local.HasErrors, local.ToText());
                foreach (var field in NarrativeValidationContext.Wrappers(definition))
                {
                    bool stale = field.Wrapper switch { CompiledCondition c => c.IsStale, CompiledIntExpr i => i.IsStale, CompiledEffectList e => e.IsStale, _ => true }; Assert.IsFalse(stale, field.Field);
                    var dependencies = new List<LogicDependency>();
                    if (field.Wrapper is ICondition condition) { condition.CollectDependencies(dependencies); Assert.IsNotEmpty(dependencies, field.Field + " must bind its fact read."); }
                    else if (field.Wrapper is IIntExpr expression) { expression.CollectDependencies(dependencies); Assert.IsNotEmpty(dependencies, field.Field + " must bind its fact read."); }
                    else if (NarrativeValidationContext.Value(field.Wrapper, "effects") is IEffect[] effects)
                        foreach (var effect in effects) Assert.IsNotNull(NarrativeValidationContext.Value(NarrativeValidationContext.Value(effect, "arguments"), "Indices"), field.Field + " must bind each effect.");
                }
            }
            foreach (var definition in new ContentDefinition[] { beat, quest, rumor, mystery })
                foreach (var field in NarrativeValidationContext.Wrappers(definition))
                {
                    object old = NarrativeValidationContext.Value(field.Wrapper, "source");
                    if (field.Wrapper is CompiledEffectList list) Effects(list, "complete_beat beat_usage");
                    else Source(field.Wrapper, field.Wrapper is CompiledIntExpr ? "roll(6)" : "roll(6) == 1");
                    var report = f.Validate(); Assert.IsTrue(report.Entries.Any(entry => entry.Code == "NAR-E008" && entry.AssetPath == "content:" + definition.IdString && (entry.Field == field.Field || entry.Field.StartsWith(field.Field + " (", StringComparison.Ordinal) || entry.Field.StartsWith(field.Field + "[", StringComparison.Ordinal))), report.ToText());
                    SetField(field.Wrapper, "source", old);
                }
        }
    }
}
