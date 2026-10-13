namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.5); behavior follows in the named implementation task.</summary>
    [System.Serializable]
    public sealed class CompiledCondition : ICondition
    {
        [UnityEngine.SerializeField, UnityEngine.TextArea(1, 6)] private string source = "";
        [UnityEngine.SerializeField] private ulong sourceHash;            // StableHash.Hash64(source) at last compile
        [UnityEngine.SerializeField] private int compilerVersion;         // LogicCompiler.Version at last compile
        [UnityEngine.SerializeField] private bool hasErrors;
        [UnityEngine.SerializeReference] private ICondition root;         // null when source is empty or hasErrors
        [System.NonSerialized] private bool warned;
        public string Source => source;
        public bool HasErrors => hasErrors;
        public bool IsAlwaysTrue => !hasErrors && root == null;
        public bool IsStale => compilerVersion != Compiler.LogicCompiler.Version || sourceHash != Game.Core.StableHash.Hash64(source ?? "");
        // ICondition: Evaluate → hasErrors ? false (WarnOnce) : root == null ? true : root.Evaluate(ctx)
        internal void SetCompiled(ICondition compiledRoot, bool failed)
        {
            hasErrors = failed;
            warned = false;
            root = failed ? null : compiledRoot;
            sourceHash = Game.Core.StableHash.Hash64(source ?? "");
            compilerVersion = Compiler.LogicCompiler.Version;
        }
        public bool Evaluate(ILogicContext ctx)
        {
            if (!hasErrors) return root == null || root.Evaluate(ctx);
            if (!warned) { warned = true; Game.Core.GameLog.WarnOnce("NAR.Condition." + sourceHash, "NAR", "Condition failed to compile; evaluates false."); }
            return false;
        }
        public void Validate(Game.Data.Validation.ValidationReport report)
        {
            if (IsStale) report.Error("NAR-E017", "", "", "Compiled logic is stale; recompile (Game/Narrative/Recompile All Logic).");
            root?.Validate(report);
        }
        public void Bind(LogicBinder binder) => root?.Bind(binder);
        public void CollectDependencies(System.Collections.Generic.List<LogicDependency> into) => root?.CollectDependencies(into);
    }
}
