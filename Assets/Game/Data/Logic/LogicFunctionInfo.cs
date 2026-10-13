namespace Game.Data.Logic
{
    /// <summary>Function metadata and the distinct node-construction/runtime invocation boundaries (SPEC-40 §4.7, Q12).</summary>
    public sealed class LogicFunctionInfo
    {
        public string Name;
        public bool IsEffect;
        public LogicType ReturnType;
        public LogicParamInfo[] Params = System.Array.Empty<LogicParamInfo>();
        public LogicUsage AllowedUsage;
        public EffectFlow Flow;
        public int Milestone;

        // TODO(NAR-003): construct the selected ICondition/IIntExpr/IEffect while retaining bound nodes, refs and literals.
        public object CreateNode(System.ReadOnlySpan<object> boundArgs) => throw new System.NotImplementedException("NAR-003");
        // TODO(NAR-003): invoke the vocabulary entry with runtime values (Yarn boundary).
        public LogicValue Invoke(ILogicContext context, System.ReadOnlySpan<LogicValue> args) => throw new System.NotImplementedException("NAR-003");
    }
}
