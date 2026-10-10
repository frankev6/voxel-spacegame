namespace Game.Data.Blocks
{
    /// <summary>Base of every module. Implements SPEC-04 §4.6 (ADR-0013).</summary>
    [System.Serializable]
    public abstract class BlockModule
    {
        public abstract BlockModuleId Id { get; }
        public abstract int RowSize { get; }
        /// <summary>Writes exactly RowSize bytes. Called by BlockDefsBlobBuilder only.</summary>
        internal abstract unsafe void BakeRow(in BlockModuleBakeContext ctx, void* destination);
        /// <summary>Range checks of this module's fields: report DATA-E041 with the field name. Default: nothing to check.</summary>
        public virtual void Validate(Game.Data.Definitions.BlockDefinition owner, Game.Data.Validation.ValidationReport report) { }
        /// <summary>Adds this module's editor stats for orientation `orient`. Default: nothing.</summary>
        public virtual void ContributeStats(ref BlockStatContribution stats, byte orient) { }
    }

    /// <summary>Typed base: TRow is the unmanaged row baked into BlockModulesBlob and read by Burst code with TryGet&lt;TRow&gt;.
    /// Implements SPEC-04 §4.6.</summary>
    [System.Serializable]
    public abstract class BlockModule<TRow> : BlockModule where TRow : unmanaged
    {
        public sealed override int RowSize => Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<TRow>();
        protected abstract TRow Bake(in BlockModuleBakeContext ctx);
        internal sealed override unsafe void BakeRow(in BlockModuleBakeContext ctx, void* destination)
            => Unity.Collections.LowLevel.Unsafe.UnsafeUtility.WriteArrayElement(destination, 0, Bake(in ctx));
    }
}
