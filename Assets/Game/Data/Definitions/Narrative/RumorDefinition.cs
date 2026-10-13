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
