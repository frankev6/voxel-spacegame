using System.Collections.Generic;
using Game.Data.Validation;

namespace Game.Data.Logic
{
    /// <summary>Source text and compiled graph of one integer-expression field; empty source means zero (SPEC-40 §4.5).</summary>
    [System.Serializable]
    public sealed class CompiledIntExpr : IIntExpr
    {
        [UnityEngine.SerializeField, UnityEngine.TextArea(1, 6)] private string source = "";
        [UnityEngine.SerializeField] private ulong sourceHash;
        [UnityEngine.SerializeField] private int compilerVersion;
        [UnityEngine.SerializeField] private bool hasErrors;
        [UnityEngine.SerializeReference] private IIntExpr root;
        [System.NonSerialized] private bool warned;

        public string Source => source;
        public bool HasErrors => hasErrors;
        public bool IsStale => compilerVersion != Compiler.LogicCompiler.Version || sourceHash != Game.Core.StableHash.Hash64(source ?? "");

        internal void SetCompiled(IIntExpr compiledRoot, bool failed)
        {
            hasErrors = failed;
            warned = false;
            root = failed ? null : compiledRoot;
            sourceHash = Game.Core.StableHash.Hash64(source ?? "");
            compilerVersion = Compiler.LogicCompiler.Version;
        }
        public int Evaluate(ILogicContext ctx)
        {
            if (!hasErrors) return root != null ? root.Evaluate(ctx) : 0;
            if (!warned) { warned = true; Game.Core.GameLog.WarnOnce("NAR.IntExpr." + sourceHash, "NAR", "Integer expression failed to compile; evaluates zero."); }
            return 0;
        }
        public void Validate(ValidationReport report)
        {
            if (IsStale) report.Error("NAR-E017", "", "", "Compiled logic is stale; recompile (Game/Narrative/Recompile All Logic).");
            root?.Validate(report);
        }
        public void Bind(LogicBinder binder) => root?.Bind(binder);
        public void CollectDependencies(List<LogicDependency> into) => root?.CollectDependencies(into);
    }
}
