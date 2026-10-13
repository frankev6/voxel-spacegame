namespace Game.Data.Logic
{
    /// <summary>Immutable vocabulary lookup contract (SPEC-40 §4.7, §6).</summary>
    public sealed class LogicFunctionRegistry
    {
        // TODO(NAR-003): populate the immutable static-readonly vocabulary table and implement lookup.
        public bool TryGet(string name, out LogicFunctionInfo info) => throw new System.NotImplementedException("NAR-003");
    }
}
