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
        public bool CanApplyAll(ILogicContext ctx, EffectRequirements scratch)
        {
            scratch.Push();
            try { return Preflight(ctx, scratch); }
            finally { scratch.Pop(); }
        }
        public bool ApplyAll(ILogicContext ctx, EffectRequirements scratch)
        {
            scratch.Push();
            try
            {
                if (!Preflight(ctx, scratch)) return false;
                foreach (var effect in effects)
                {
                    if (effect is Nodes.TakeItemEffect item) item.ApplyPrepared(ctx, scratch);
                    else if (effect is Nodes.TakeCreditsEffect credits) credits.ApplyPrepared(ctx, scratch);
                    else effect.Apply(ctx);
                }
                return true;
            }
            finally { scratch.Pop(); }
        }
        bool Preflight(ILogicContext ctx, EffectRequirements scratch)
        {
            if (hasErrors)
            {
                Game.Core.GameLog.WarnOnce("NAR.Effects." + sourceHash, "Narrative", "Effect list failed to compile; no effects applied.");
                return false;
            }
            foreach (var effect in effects)
            {
                effect.AddRequirements(scratch);
                if (!effect.CanApply(ctx)) return Rejected(ctx);
            }
            return scratch.IsSatisfied(ctx) || Rejected(ctx);
        }
        static bool Rejected(ILogicContext ctx)
        {
            if (ctx is ILogicEffectHost host) host.EffectRejected();
            return false;
        }
        public bool IsStale => compilerVersion != Compiler.LogicCompiler.Version ||
            sourceHash != Game.Core.StableHash.Hash64(string.Join("\n", source ?? System.Array.Empty<string>()));
        /// <summary>ADR-0084: borrow the one current compiled node without losing concrete command identity.</summary>
        public bool TryGetSingleEffect(out IEffect effect)
        {
            effect = null;
            if (hasErrors || IsStale || effects == null || effects.Length != 1 || effects[0] == null) return false;
            effect = effects[0]; return true;
        }
        internal void SetCompiled(IEffect[] compiledEffects, bool failed)
        {
            hasErrors = failed;
            effects = failed ? System.Array.Empty<IEffect>() : compiledEffects ?? System.Array.Empty<IEffect>();
            sourceHash = Game.Core.StableHash.Hash64(string.Join("\n", source ?? System.Array.Empty<string>()));
            compilerVersion = Compiler.LogicCompiler.Version;
        }
        public void Bind(LogicBinder binder)
        {
            foreach (var effect in effects) effect?.Bind(binder);
        }
    }
}
