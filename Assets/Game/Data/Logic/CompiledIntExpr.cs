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

        // TODO(NAR-003): implement wrapper validation, binding and dependency collection.
        // TODO(NAR-004): evaluate the compiled integer root, with zero for empty source.
        public int Evaluate(ILogicContext ctx) => throw new System.NotImplementedException("NAR-004");
        public void Validate(ValidationReport report) => throw new System.NotImplementedException("NAR-003");
        public void Bind(LogicBinder binder) => throw new System.NotImplementedException("NAR-003");
        public void CollectDependencies(List<LogicDependency> into) => throw new System.NotImplementedException("NAR-003");
    }
}
