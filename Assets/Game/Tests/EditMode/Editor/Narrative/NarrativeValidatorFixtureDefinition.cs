using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Tests.EditMode.Editor.Narrative
{
    /// <summary>In-memory source owner for the seven NAR-013 rule fixtures.</summary>
    [ContentIdPrefix("narfixture_")]
    public sealed class NarrativeValidatorFixtureDefinition : ContentDefinition, ILogicSourceOwner
    {
        [SerializeField] private CompiledCondition condition = new();
        [SerializeField] private CompiledIntExpr progress = new();
        [SerializeField] private CompiledEffectList effects = new();
        [SerializeField] private bool debug;
        [SerializeField] private bool failCompile;
        public CompiledCondition Condition => condition;
        public CompiledIntExpr Progress => progress;
        public CompiledEffectList Effects => effects;
        public bool Debug => debug;
        public void CompileLogic(LogicCompiler compiler, ValidationScope scope)
        {
            if (failCompile) throw new System.InvalidOperationException("Fixture compile failure");
            var report = new ValidationReport(scope.AssetPath);
            compiler.Compile(condition, new LogicCompileOptions(LogicUsage.DialogCondition, scope.AssetPath, "condition"), report);
            compiler.Compile(progress, new LogicCompileOptions(LogicUsage.QuestObjective, scope.AssetPath, "progress"), report);
            compiler.Compile(effects, new LogicCompileOptions(LogicUsage.DialogCommand, scope.AssetPath, "effects"), report);
            foreach (var entry in report.Entries)
                if (entry.Severity == ValidationSeverity.Error) scope.Error(entry.Code, entry.Field, entry.Message);
                else if (entry.Severity == ValidationSeverity.Warning) scope.Warning(entry.Code, entry.Field, entry.Message);
                else scope.Info(entry.Code, entry.Field, entry.Message);
        }
        public void BindLogic(LogicBinder binder) { condition.Bind(binder); progress.Bind(binder); effects.Bind(binder); }
    }
}
