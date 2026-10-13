using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.3 quest data; validation and logic follow in QST-002.</summary>
    [ContentIdPrefix("quest_", "contract_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Quest", fileName = "quest_new")]
    public sealed class QuestDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private string title;
        [SerializeField] private string shortDescription;
        [SerializeField, UnityEngine.TextArea(2, 8)] private string description;
        [SerializeField] private QuestKind kind = QuestKind.Side;
        [SerializeField] private bool autoTrack = true;
        [SerializeField] private System.Collections.Generic.List<QuestStage> stages = new();          // 1..16
        [SerializeField] private Game.Data.Logic.CompiledCondition failWhen = new();                  // QuestObjective; empty = never
        [SerializeField] private Game.Data.Logic.CompiledEffectList onAccepted = new();              // QuestEffect
        [SerializeField] private Game.Data.Logic.CompiledEffectList onCompleted = new();             // QuestEffect
        [SerializeField] private Game.Data.Logic.CompiledEffectList onFailed = new();                // QuestEffect
        [SerializeField] private System.Collections.Generic.List<QuestLogEntry> logEntries = new();
        public bool Repeatable => kind == QuestKind.Contract;

        public string Title => title;
        public string ShortDescription => shortDescription;
        public string Description => description;
        public QuestKind Kind => kind;
        public bool AutoTrack => autoTrack;
        public System.Collections.Generic.IReadOnlyList<QuestStage> Stages => stages;
        public Game.Data.Logic.CompiledCondition FailWhen => failWhen;
        public Game.Data.Logic.CompiledEffectList OnAccepted => onAccepted;
        public Game.Data.Logic.CompiledEffectList OnCompleted => onCompleted;
        public Game.Data.Logic.CompiledEffectList OnFailed => onFailed;
        public System.Collections.Generic.IReadOnlyList<QuestLogEntry> LogEntries => logEntries;

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
