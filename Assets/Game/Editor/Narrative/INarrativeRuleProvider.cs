using Game.Data.Validation;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 §5.7: editor rules contributed by dialog, quests and mysteries.</summary>
    public interface INarrativeRuleProvider
    {
        int Order { get; }
        void Run(NarrativeValidationContext ctx, ValidationReport report);
    }
}
