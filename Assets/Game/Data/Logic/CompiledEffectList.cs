namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.5); behavior follows in the named implementation task.</summary>
    [System.Serializable]
    public sealed class CompiledEffectList
    {
        [UnityEngine.SerializeField] private string[] source = System.Array.Empty<string>();
        [UnityEngine.SerializeField] private ulong sourceHash;            // hash of lines joined with '\n'
        [UnityEngine.SerializeField] private int compilerVersion;
        [UnityEngine.SerializeField] private bool hasErrors;
        [UnityEngine.SerializeReference] private IEffect[] effects = System.Array.Empty<IEffect>();
        public System.Collections.Generic.IReadOnlyList<string> Source => source;
        public bool HasErrors => hasErrors;
        public bool IsEmpty => effects.Length == 0;
        public bool CanApplyAll(ILogicContext ctx, EffectRequirements scratch) => throw new System.NotImplementedException("NAR-005");   // §5.4 step 1–3
        public bool ApplyAll(ILogicContext ctx, EffectRequirements scratch) => throw new System.NotImplementedException("NAR-005");      // §5.4; false = nothing applied
        // TODO(NAR-003): bind compiled effects and maintain compilation metadata.
        // TODO(NAR-005): implement summed requirement checks and atomic effect application.
        public void Bind(LogicBinder binder) => throw new System.NotImplementedException("NAR-003");
    }
}
