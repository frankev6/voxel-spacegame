namespace Game.Data.Logic.Compiler
{
    /// <summary>Public narrative contract (SPEC-40 §4.5); behavior follows in the named implementation task.</summary>
    public readonly struct LogicCompileOptions
    {
        public readonly LogicUsage Usage; public readonly string AssetPath; public readonly string Field;
        public LogicCompileOptions(LogicUsage usage, string assetPath, string field) { Usage = usage; AssetPath = assetPath; Field = field; }
    }
}
