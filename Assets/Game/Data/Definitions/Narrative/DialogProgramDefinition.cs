namespace Game.Data.Definitions
{
    /// <summary>Fallback-path compiled dialog (only exists when the gate rejected Yarn). Exactly one asset: dialog_program. Implements SPEC-41 §4.8.</summary>
    [ContentIdPrefix("dialog_")]
    public sealed class DialogProgramDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [UnityEngine.SerializeField] private DialogNodeRecord[] nodes = System.Array.Empty<DialogNodeRecord>();   // ordinal by Title
        [UnityEngine.SerializeField] private DialogInstruction[] code = System.Array.Empty<DialogInstruction>();
        [UnityEngine.SerializeField] private string[] strings = System.Array.Empty<string>();      // texts, line IDs, node titles, tag values
        [UnityEngine.SerializeField] private Game.Data.Logic.CompiledCondition[] conditions = System.Array.Empty<Game.Data.Logic.CompiledCondition>();
        [UnityEngine.SerializeField] private Game.Data.Logic.CompiledIntExpr[] intExprs = System.Array.Empty<Game.Data.Logic.CompiledIntExpr>();
        [UnityEngine.SerializeField] private ulong sourcesHash;
        // read-only accessors with the same names (Nodes, Code, Strings, Conditions, IntExprs, SourcesHash)

        public System.Collections.Generic.IReadOnlyList<DialogNodeRecord> Nodes => nodes;
        public System.Collections.Generic.IReadOnlyList<DialogInstruction> Code => code;
        public System.Collections.Generic.IReadOnlyList<string> Strings => strings;
        public System.Collections.Generic.IReadOnlyList<Game.Data.Logic.CompiledCondition> Conditions => conditions;
        public System.Collections.Generic.IReadOnlyList<Game.Data.Logic.CompiledIntExpr> IntExprs => intExprs;
        public ulong SourcesHash => sourcesHash;

        public void CompileLogic(Game.Data.Logic.Compiler.LogicCompiler compiler, Game.Data.Validation.ValidationScope scope)
        {
            var report = new Game.Data.Validation.ValidationReport(scope.AssetPath);
            for (int i = 0; i < conditions.Length; i++)
                compiler.Compile(conditions[i], new Game.Data.Logic.Compiler.LogicCompileOptions(Game.Data.Logic.LogicUsage.DialogCondition, scope.AssetPath, "conditions[" + i + "]"), report);
            for (int i = 0; i < intExprs.Length; i++)
                compiler.Compile(intExprs[i], new Game.Data.Logic.Compiler.LogicCompileOptions(Game.Data.Logic.LogicUsage.DialogCondition, scope.AssetPath, "intExprs[" + i + "]"), report);
            foreach (var entry in report.Entries)
                switch (entry.Severity)
                {
                    case Game.Data.Validation.ValidationSeverity.Error: scope.Error(entry.Code, entry.Field, entry.Message); break;
                    case Game.Data.Validation.ValidationSeverity.Warning: scope.Warning(entry.Code, entry.Field, entry.Message); break;
                    default: scope.Info(entry.Code, entry.Field, entry.Message); break;
                }
        }
        public void BindLogic(Game.Data.Logic.LogicBinder binder)
        {
            foreach (var condition in conditions) condition.Bind(binder);
            foreach (var expression in intExprs) expression.Bind(binder);
        }
    }
}
