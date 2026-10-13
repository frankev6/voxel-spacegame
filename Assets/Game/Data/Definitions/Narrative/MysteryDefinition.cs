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

        // TODO(QST-002): implement field validation and the specified logic usages.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("QST-002");
        }
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
            => throw new System.NotImplementedException("QST-002");
        public void BindLogic(Game.Data.Logic.LogicBinder binder) => throw new System.NotImplementedException("QST-002");
    }
}
