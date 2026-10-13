using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.4 clue with an authored target and resolution condition.</summary>
    [ContentIdPrefix("rumor_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Rumor", fileName = "rumor_new")]
    public sealed class RumorDefinition : ClueDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private AuthoredStarSystemDefinition target;       // null = no map marker
        [SerializeField] private string targetBodyTag;                       // "" = the star system
        [SerializeField] private Game.Data.Logic.CompiledCondition resolvedWhen = new();   // MarkerCondition; empty = never resolved

        public AuthoredStarSystemDefinition Target => target;
        public string TargetBodyTag => targetBodyTag;
        public Game.Data.Logic.CompiledCondition ResolvedWhen => resolvedWhen;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            QuestDefinitionValidation.Rumor(this, scope);
            QuestDefinitionValidation.Reference(target, "target", scope, context);
            QuestDefinitionValidation.Logic(resolvedWhen, scope, "resolvedWhen");
        }
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
        {
            resolvedWhen ??= new();
            QuestDefinitionValidation.Compile(compiler, resolvedWhen, Game.Data.Logic.LogicUsage.MarkerCondition, scope, "resolvedWhen");
        }
        public void BindLogic(Game.Data.Logic.LogicBinder binder)
        {
            if (binder == null) throw new System.ArgumentNullException(nameof(binder));
            resolvedWhen?.Bind(binder);
        }
    }
}
