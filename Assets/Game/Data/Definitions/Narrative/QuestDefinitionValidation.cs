using System;
using System.Collections.Generic;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 field-local checks shared by runtime definitions and editor rule providers.</summary>
    internal static class QuestDefinitionValidation
    {
        internal static void Beat(StoryBeatDefinition beat, ValidationScope scope)
        {
            if (string.IsNullOrWhiteSpace(beat.YarnNode) && !HasEffects(beat.OnStart) && !HasEffects(beat.OnComplete))
                scope.Error("QST-V001", "yarnNode", "A beat needs a dialog node or start/completion effects.");
            var where = beat.Where;
            bool tags = where?.bodyTags != null && where.bodyTags.Length > 0;
            bool system = where?.starSystem != null, hail = where?.hailEncounter != null;
            if (beat.Trigger == BeatTrigger.Interact && !tags && !system)
                scope.Error("QST-V002", "where", "An Interact beat needs body tags or a star system.");
            if (beat.Trigger == BeatTrigger.Hail && !hail)
                scope.Error("QST-V002", "where.hailEncounter", "A Hail beat needs an encounter.");
            if ((beat.Trigger == BeatTrigger.GameStart || beat.Trigger == BeatTrigger.Manual || beat.Trigger == BeatTrigger.Condition) && (tags || system || hail))
                scope.Error("QST-V002", "where", "This beat trigger requires an empty location.");
        }
        internal static bool HasEffects(CompiledEffectList effects)
        { if (effects?.Source != null) foreach (string line in effects.Source) if (!string.IsNullOrWhiteSpace(line)) return true; return false; }
        internal static void Quest(QuestDefinition quest, ValidationScope scope)
        {
            var stages = quest.Stages;
            if (stages == null || stages.Count < 1 || stages.Count > 16)
                scope.Error("QST-V004", "stages", "A quest needs 1..16 stages.");
            var stageIds = new HashSet<string>(StringComparer.Ordinal); var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            if (stages != null) for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i]; string field = "stages[" + i + "]";
                if (stage == null) { scope.Error("QST-V004", field, "Stage must be set."); continue; }
                if (!Snake(stage.id) || !stageIds.Add(stage.id)) scope.Error("QST-V004", field + ".id", "Stage ID must be unique snake_case.");
                if (stage.objectives == null || stage.objectives.Count < 1 || stage.objectives.Count > 16)
                    scope.Error("QST-V004", field + ".objectives", "A stage needs 1..16 objectives.");
                if (stage.objectives == null) continue;
                for (int j = 0; j < stage.objectives.Count; j++)
                {
                    var objective = stage.objectives[j]; string path = field + ".objectives[" + j + "]";
                    if (objective == null) { scope.Error("QST-V004", path, "Objective must be set."); continue; }
                    if (!Snake(objective.id) || !objectiveIds.Add(objective.id)) scope.Error("QST-V004", path + ".id", "Objective ID must be unique snake_case across the quest.");
                    if (string.IsNullOrWhiteSpace(objective.doneWhen?.Source)) scope.Error("QST-V004", path + ".doneWhen", "Objective completion condition must be nonempty.");
                    Marker(objective.marker, scope, path + ".marker");
                }
            }
            bool contractId = quest.IdString?.StartsWith("contract_", StringComparison.Ordinal) == true;
            if (contractId != (quest.Kind == QuestKind.Contract) || quest.Repeatable != (quest.Kind == QuestKind.Contract))
                scope.Error("QST-V009", "kind", "Contract kind and contract_ ID must agree; only contracts are repeatable.");
        }
        internal static void Marker(QuestMarker marker, ValidationScope scope, string field)
        {
            if (!string.IsNullOrEmpty(marker?.bodyTag) && marker.starSystem == null)
                scope.Error("QST-V011", field + ".starSystem", "A body marker needs a star system.");
        }
        internal static void Rumor(RumorDefinition rumor, ValidationScope scope)
        {
            if (!string.IsNullOrEmpty(rumor.TargetBodyTag) && rumor.Target == null)
                scope.Error("QST-V011", "target", "A rumor body marker needs a star system.");
        }
        internal static void Mystery(MysteryDefinition mystery, ValidationScope scope)
        {
            if (mystery.Requires == null || mystery.Requires.Count == 0) scope.Error("QST-V008", "requires", "A mystery needs at least one required clue.");
            if (mystery.Requires == null) return;
            for (int i = 0; i < mystery.Requires.Count; i++)
                if (mystery.Requires[i] == null || mystery.Requires[i].Mystery != mystery)
                    scope.Error("QST-V008", "requires[" + i + "]", "Required clue must belong to this mystery.");
        }
        internal static bool Snake(string id)
        {
            if (string.IsNullOrEmpty(id) || id[0] < 'a' || id[0] > 'z') return false;
            bool underscore = false;
            foreach (char c in id)
            {
                if (c == '_') { if (underscore) return false; underscore = true; }
                else { if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9')) return false; underscore = false; }
            }
            return !underscore;
        }
        internal static void Reference(ContentDefinition target, string field, ValidationScope scope, ContentValidationContext context)
        {
            if (target != null && context != null && !context.IsInDatabase(target))
                scope.Error("DATA-E010", field, "Referenced definition must belong to the content database.");
        }
        internal static void Logic(object wrapper, ValidationScope scope, string field)
        {
            bool stale = wrapper switch { CompiledCondition c => c.IsStale, CompiledIntExpr i => i.IsStale, CompiledEffectList e => e.IsStale, _ => true };
            bool failed = wrapper switch { CompiledCondition c => c.HasErrors, CompiledIntExpr i => i.HasErrors, CompiledEffectList e => e.HasErrors, _ => false };
            if (stale || failed) { scope.Error("NAR-E017", field, "Compiled logic is missing, stale or failed; recompile (Game/Narrative/Recompile All Logic)."); return; }
            var report = new ValidationReport(scope.AssetPath);
            if (wrapper is ICondition condition) condition.Validate(report);
            else if (wrapper is IIntExpr expression) expression.Validate(report);
            foreach (var entry in report.Entries)
            {
                string path = string.IsNullOrEmpty(entry.Field) ? field : field + "." + entry.Field;
                if (entry.Severity == ValidationSeverity.Error) scope.Error(entry.Code, path, entry.Message);
                else if (entry.Severity == ValidationSeverity.Warning) scope.Warning(entry.Code, path, entry.Message);
                else scope.Info(entry.Code, path, entry.Message);
            }
        }
        internal static void Compile(LogicCompiler compiler, object wrapper, LogicUsage usage, ValidationScope scope, string field)
        {
            if (compiler == null) throw new ArgumentNullException(nameof(compiler));
            var report = new ValidationReport(scope.AssetPath);
            var options = new LogicCompileOptions(usage, scope.AssetPath, field);
            switch (wrapper)
            {
                case CompiledCondition condition: compiler.Compile(condition, options, report); break;
                case CompiledIntExpr expression: compiler.Compile(expression, options, report); break;
                case CompiledEffectList effects: compiler.Compile(effects, options, report); break;
                default: scope.Error("NAR-E017", field, "Logic field must be set."); return;
            }
            foreach (var entry in report.Entries)
                switch (entry.Severity)
                { case ValidationSeverity.Error: scope.Error(entry.Code, entry.Field, entry.Message); break;
                  case ValidationSeverity.Warning: scope.Warning(entry.Code, entry.Field, entry.Message); break;
                  default: scope.Info(entry.Code, entry.Field, entry.Message); break; }
        }
    }
}
