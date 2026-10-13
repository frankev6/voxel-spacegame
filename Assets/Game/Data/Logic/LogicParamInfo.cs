namespace Game.Data.Logic
{
    /// <summary>One function parameter's declared argument kind and optional default (SPEC-40 §4.7).</summary>
    public struct LogicParamInfo
    {
        public string Name;
        public LogicArgKind ArgKind;
        public RefKind RefKind;
        public bool Optional;
        public int DefaultInt;
    }
}
