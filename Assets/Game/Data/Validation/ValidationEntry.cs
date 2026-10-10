namespace Game.Data.Validation
{
    /// <summary>One error, warning or info of a validation pass (SPEC-04 §4.8).</summary>
    public readonly struct ValidationEntry
    {
        public readonly ValidationSeverity Severity;
        public readonly string Code;       // stable, e.g. "DATA-E004", "MIG-W030", "PERS-W001"
        public readonly string AssetPath;  // asset path, file path or logical path ("save:p001/manual_001")
        public readonly string Field;      // field or JSON path, "" if none
        public readonly string Message;    // one sentence, English
    }
}
