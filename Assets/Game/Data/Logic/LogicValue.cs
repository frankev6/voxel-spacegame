namespace Game.Data.Logic
{
    /// <summary>Runtime invocation value; node-construction arguments retain their node/reference types instead (SPEC-40 §4.7, Q12).</summary>
    public readonly struct LogicValue
    {
        public readonly LogicType Type;
        public readonly int Int;
        public readonly string Text;
        public LogicValue(LogicType type, int value, string text = null)
        { Type = type; Int = value; Text = text; }
    }
}
