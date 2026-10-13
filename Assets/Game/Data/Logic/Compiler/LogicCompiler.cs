namespace Game.Data.Logic.Compiler
{
    /// <summary>Public narrative contract (SPEC-40 §4.5); behavior follows in the named implementation task.</summary>
    public sealed class LogicCompiler
    {
        // TODO(NAR-003): implement compiler/binder behavior and diagnostics.
        public const int Version = 1;                     // bump when node shapes change → every wrapper becomes stale
        public LogicCompiler(FactCatalog facts, RefResolverRegistry refs, LogicFunctionRegistry functions) => throw new System.NotImplementedException("NAR-003");
        public bool Compile(CompiledCondition target, in LogicCompileOptions options, Game.Data.Validation.ValidationReport report) => throw new System.NotImplementedException("NAR-003");
        public bool Compile(CompiledIntExpr target, in LogicCompileOptions options, Game.Data.Validation.ValidationReport report) => throw new System.NotImplementedException("NAR-003");
        public bool Compile(CompiledEffectList target, in LogicCompileOptions options, Game.Data.Validation.ValidationReport report) => throw new System.NotImplementedException("NAR-003");
        public bool TryCompileCommand(string commandText, in LogicCompileOptions options, Game.Data.Validation.ValidationReport report, out IEffect effect) => throw new System.NotImplementedException("NAR-003");  // SPEC-41 command table
        public bool TryCompileExpression(string expressionText, in LogicCompileOptions options, Game.Data.Validation.ValidationReport report, out ICondition condition) => throw new System.NotImplementedException("NAR-003"); // SPEC-41 <<if>> validation/fallback
    }
}
