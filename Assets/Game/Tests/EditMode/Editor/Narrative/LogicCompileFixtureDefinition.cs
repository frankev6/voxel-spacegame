using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Tests.EditMode.Editor.Narrative
{
    /// <summary>Temporary asset owner exercises all three serialized wrapper shapes without production content changes.</summary>
    [ContentIdPrefix("nartest_")]
    public sealed class LogicCompileFixtureDefinition : ContentDefinition, ILogicSourceOwner
    {
        [SerializeField] private CompiledCondition condition = new();
        [SerializeField] private CompiledIntExpr progress = new();
        [SerializeField] private CompiledEffectList effects = new();
        public CompiledCondition Condition => condition;
        public CompiledIntExpr Progress => progress;
        public CompiledEffectList Effects => effects;
        public void CompileLogic(LogicCompiler compiler, ValidationScope scope)
        {
            var report = new ValidationReport(scope.AssetPath);
            compiler.Compile(condition, new LogicCompileOptions(LogicUsage.BeatCondition, scope.AssetPath, "condition"), report);
            compiler.Compile(progress, new LogicCompileOptions(LogicUsage.QuestObjective, scope.AssetPath, "progress"), report);
            compiler.Compile(effects, new LogicCompileOptions(LogicUsage.BeatEffect, scope.AssetPath, "effects"), report);
            foreach (var entry in report.Entries)
                if (entry.Severity == ValidationSeverity.Error) scope.Error(entry.Code, entry.Field, entry.Message);
                else if (entry.Severity == ValidationSeverity.Warning) scope.Warning(entry.Code, entry.Field, entry.Message);
                else scope.Info(entry.Code, entry.Field, entry.Message);
        }
        public void BindLogic(LogicBinder binder) { condition.Bind(binder); progress.Bind(binder); effects.Bind(binder); }
    }
}
