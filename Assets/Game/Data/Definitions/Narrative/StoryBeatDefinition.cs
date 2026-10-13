using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.2 story beat data; field validation and compiled logic (SPEC-42).</summary>
    [ContentIdPrefix("beat_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Story Beat", fileName = "beat_new")]
    public sealed class StoryBeatDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private BeatTrigger trigger;
        [SerializeField] private BeatLocation where = new();
        [SerializeField] private Game.Data.Logic.CompiledCondition condition = new();   // BeatCondition; empty = always
        [SerializeField] private int salience;                  // higher wins
        [SerializeField, UnityEngine.Min(0.0001f)] private float weight = 1f;   // tie-break among equal salience
        [SerializeField] private BeatRepeat repeat = BeatRepeat.Once;
        [SerializeField] private bool firstVisitOnly;           // StarSystemEntered: only on the first visit
        [SerializeField] private string yarnNode;               // "" = effect-only beat
        [SerializeField] private Game.Data.Logic.CompiledEffectList onStart = new();    // BeatEffect; applied before the dialog starts
        [SerializeField] private Game.Data.Logic.CompiledEffectList onComplete = new(); // BeatEffect
        [SerializeField] private bool debug;                    // beat_debug_*: excluded from release validation and never auto-selected in release builds

        public BeatTrigger Trigger => trigger;
        public BeatLocation Where => where;
        public Game.Data.Logic.CompiledCondition Condition => condition;
        public int Salience => salience;
        public float Weight => weight;
        public BeatRepeat Repeat => repeat;
        public bool FirstVisitOnly => firstVisitOnly;
        public string YarnNode => yarnNode;
        public Game.Data.Logic.CompiledEffectList OnStart => onStart;
        public Game.Data.Logic.CompiledEffectList OnComplete => onComplete;
        public bool Debug => debug;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (debug && context != null && !context.IsEditorPass && !UnityEngine.Debug.isDebugBuild) return;
            QuestDefinitionValidation.Beat(this, scope);
            QuestDefinitionValidation.Logic(condition, scope, "condition");
            QuestDefinitionValidation.Logic(onStart, scope, "onStart"); QuestDefinitionValidation.Logic(onComplete, scope, "onComplete");
            QuestDefinitionValidation.Reference(where?.starSystem, "where.starSystem", scope, context);
            QuestDefinitionValidation.Reference(where?.hailEncounter, "where.hailEncounter", scope, context);
        }
        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
        {
            condition ??= new(); onStart ??= new(); onComplete ??= new();
            QuestDefinitionValidation.Compile(compiler, condition, Game.Data.Logic.LogicUsage.BeatCondition, scope, "condition");
            QuestDefinitionValidation.Compile(compiler, onStart, Game.Data.Logic.LogicUsage.BeatEffect, scope, "onStart");
            QuestDefinitionValidation.Compile(compiler, onComplete, Game.Data.Logic.LogicUsage.BeatEffect, scope, "onComplete");
        }
        public void BindLogic(Game.Data.Logic.LogicBinder binder)
        {
            if (binder == null) throw new System.ArgumentNullException(nameof(binder));
            condition?.Bind(binder); onStart?.Bind(binder); onComplete?.Bind(binder);
        }
    }
}
