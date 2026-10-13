using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.3 quest data; field validation and compiled logic (SPEC-42).</summary>
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

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            QuestDefinitionValidation.Quest(this, scope);
            QuestDefinitionValidation.Logic(failWhen, scope, "failWhen");
            QuestDefinitionValidation.Logic(onAccepted, scope, "onAccepted"); QuestDefinitionValidation.Logic(onCompleted, scope, "onCompleted"); QuestDefinitionValidation.Logic(onFailed, scope, "onFailed");
            if (stages != null) for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i]; if (stage == null) continue; string field = "stages[" + i + "]";
                QuestDefinitionValidation.Logic(stage.onComplete, scope, field + ".onComplete");
                if (stage.objectives == null) continue;
                for (int j = 0; j < stage.objectives.Count; j++)
                {
                    var objective = stage.objectives[j]; if (objective == null) continue; string path = field + ".objectives[" + j + "]";
                    QuestDefinitionValidation.Logic(objective.doneWhen, scope, path + ".doneWhen"); QuestDefinitionValidation.Logic(objective.progress, scope, path + ".progress");
                    QuestDefinitionValidation.Reference(objective.marker?.starSystem, path + ".marker.starSystem", scope, context);
                }
            }
        }
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
        {
            failWhen ??= new(); onAccepted ??= new(); onCompleted ??= new(); onFailed ??= new();
            QuestDefinitionValidation.Compile(compiler, failWhen, Game.Data.Logic.LogicUsage.QuestObjective, scope, "failWhen");
            QuestDefinitionValidation.Compile(compiler, onAccepted, Game.Data.Logic.LogicUsage.QuestEffect, scope, "onAccepted");
            QuestDefinitionValidation.Compile(compiler, onCompleted, Game.Data.Logic.LogicUsage.QuestEffect, scope, "onCompleted");
            QuestDefinitionValidation.Compile(compiler, onFailed, Game.Data.Logic.LogicUsage.QuestEffect, scope, "onFailed");
            if (stages != null) for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i]; if (stage == null) continue; string field = "stages[" + i + "]"; stage.onComplete ??= new();
                QuestDefinitionValidation.Compile(compiler, stage.onComplete, Game.Data.Logic.LogicUsage.QuestEffect, scope, field + ".onComplete");
                if (stage.objectives == null) continue;
                for (int j = 0; j < stage.objectives.Count; j++)
                {
                    var objective = stage.objectives[j]; if (objective == null) continue; string path = field + ".objectives[" + j + "]";
                    objective.doneWhen ??= new(); objective.progress ??= new();
                    QuestDefinitionValidation.Compile(compiler, objective.doneWhen, Game.Data.Logic.LogicUsage.QuestObjective, scope, path + ".doneWhen");
                    QuestDefinitionValidation.Compile(compiler, objective.progress, Game.Data.Logic.LogicUsage.QuestObjective, scope, path + ".progress");
                }
            }
        }
        public void BindLogic(Game.Data.Logic.LogicBinder binder)
        {
            if (binder == null) throw new System.ArgumentNullException(nameof(binder));
            failWhen?.Bind(binder); onAccepted?.Bind(binder); onCompleted?.Bind(binder); onFailed?.Bind(binder);
            if (stages != null) foreach (var stage in stages)
            {
                if (stage == null) continue; stage.onComplete?.Bind(binder);
                if (stage.objectives != null) foreach (var objective in stage.objectives)
                { objective?.doneWhen?.Bind(binder); objective?.progress?.Bind(binder); }
            }
        }
    }
}
