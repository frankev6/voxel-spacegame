using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Validation;
using static Game.Editor.Narrative.NarrativeValidationContext;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-42 QST-V004/005/006/009/011: quest shape, start references, logs and marker targets.</summary>
    public sealed class QuestValidatorRules : INarrativeRuleProvider
    {
        public int Order => 20;
        public void Run(NarrativeValidationContext ctx, ValidationReport report)
        {
            var started = new HashSet<QuestDefinition>();
            foreach (var command in QuestRuleTraversal.Commands(ctx, report))
            {
                if (command.Args.Length == 0) continue;
                var quest = QuestRuleTraversal.Resolve<QuestDefinition>(ctx, command.Args[0]);
                if (quest == null) continue; // The compiler/binder owns unknown references.
                if (command.Name == "start_quest") started.Add(quest);
                else if (command.Name == "quest_log" && (command.Args.Length < 2 || quest.LogEntries == null || !quest.LogEntries.Any(entry => entry.id == command.Args[1])))
                    report.Error("QST-V006", command.Path, command.Field, "Quest '" + quest.IdString + "' has no log entry '" + (command.Args.Length > 1 ? command.Args[1] : "") + "'.");
            }
            foreach (var quest in ctx.Content.All<QuestDefinition>())
            {
                string path = PathOf(quest); QuestDefinitionValidation.Quest(quest, report.For(path));
                if (quest.Kind != QuestKind.Debug && !started.Contains(quest))
                    report.Warning("QST-V005", path, "", "Quest is never named by start_quest.");
            }
            foreach (var rumor in ctx.Content.All<RumorDefinition>()) QuestDefinitionValidation.Rumor(rumor, report.For(PathOf(rumor)));
        }
    }
}
