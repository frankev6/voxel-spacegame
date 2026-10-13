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

        public string Source => source;
        public bool HasErrors => hasErrors;
        public bool IsStale => compilerVersion != Compiler.LogicCompiler.Version || sourceHash != Game.Core.StableHash.Hash64(source ?? "");

        internal void SetCompiled(IIntExpr compiledRoot, bool failed)
        {
            hasErrors = failed;
            root = failed ? null : compiledRoot;
            sourceHash = Game.Core.StableHash.Hash64(source ?? "");
            compilerVersion = Compiler.LogicCompiler.Version;
        }
        // TODO(NAR-004): evaluate the compiled integer root, with zero for empty source.
        public int Evaluate(ILogicContext ctx) => throw new System.NotImplementedException("NAR-004");
        public void Validate(ValidationReport report)
        {
            if (IsStale) report.Error("NAR-E017", "", "", "Compiled logic is stale; recompile (Game/Narrative/Recompile All Logic).");
            root?.Validate(report);
        }
        public void Bind(LogicBinder binder) => root?.Bind(binder);
        public void CollectDependencies(List<LogicDependency> into) => root?.CollectDependencies(into);
    }
}
