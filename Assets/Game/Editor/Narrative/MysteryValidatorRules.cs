using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Validation;
using static Game.Editor.Narrative.NarrativeValidationContext;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-42 QST-V007/008: explicit clue reveals and correct mystery membership.</summary>
    public sealed class MysteryValidatorRules : INarrativeRuleProvider
    {
        public int Order => 30;
        public void Run(NarrativeValidationContext ctx, ValidationReport report)
        {
            var revealed = new HashSet<ClueDefinition>();
            foreach (var command in QuestRuleTraversal.Commands(ctx, report))
                if (command.Name == "reveal_clue" && command.Args.Length > 0)
                {
                    var clue = QuestRuleTraversal.Resolve<ClueDefinition>(ctx, command.Args[0]); if (clue != null) revealed.Add(clue);
                }
            foreach (var clue in ctx.Content.All<ClueDefinition>())
                if (!revealed.Contains(clue)) report.Error("QST-V007", PathOf(clue), "", "Clue or rumor is never revealed by reveal_clue, including mystery onSolved effects.");
            foreach (var mystery in ctx.Content.All<MysteryDefinition>()) QuestDefinitionValidation.Mystery(mystery, report.For(PathOf(mystery)));
        }
    }
}
