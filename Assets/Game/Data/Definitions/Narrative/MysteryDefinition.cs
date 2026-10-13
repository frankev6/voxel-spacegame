using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.4 mystery data, required clues and solved effects.</summary>
    [ContentIdPrefix("mystery_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Mystery", fileName = "mystery_new")]
    public sealed class MysteryDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private string title;                               // the question, e.g. "What happened to the Arks?"
        [SerializeField, UnityEngine.TextArea(2, 6)] private string solvedText;
        [SerializeField] private System.Collections.Generic.List<ClueDefinition> requires = new();  // ≥ 1
        [SerializeField] private Game.Data.Logic.CompiledEffectList onSolved = new();             // QuestEffect
        [SerializeField] private int order;                                  // ship-log ordering

        public string Title => title;
        public string SolvedText => solvedText;
        public System.Collections.Generic.IReadOnlyList<ClueDefinition> Requires => requires;
        public Game.Data.Logic.CompiledEffectList OnSolved => onSolved;
        public int Order => order;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            QuestDefinitionValidation.Mystery(this, scope);
            QuestDefinitionValidation.Logic(onSolved, scope, "onSolved");
            if (requires != null) for (int i = 0; i < requires.Count; i++) QuestDefinitionValidation.Reference(requires[i], "requires[" + i + "]", scope, context);
        }
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
        {
            onSolved ??= new();
            QuestDefinitionValidation.Compile(compiler, onSolved, Game.Data.Logic.LogicUsage.QuestEffect, scope, "onSolved");
        }
        public void BindLogic(Game.Data.Logic.LogicBinder binder)
        {
            if (binder == null) throw new System.ArgumentNullException(nameof(binder));
            onSolved?.Bind(binder);
        }
    }
}
