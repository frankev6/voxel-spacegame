namespace Game.Data.Validation
{
    /// <summary>Collects errors and warnings (architecture §6.7). Not thread-safe; one report per pass. Implements SPEC-04 §4.8.</summary>
    public sealed class ValidationReport
    {
        public ValidationReport(string context) { throw new System.NotImplementedException("DATA-002"); }                 // e.g. "ContentDatabase", "MIG", "Load p001/auto_0"
        public string Context { get; }
        public System.Collections.Generic.IReadOnlyList<ValidationEntry> Entries { get; }
        public int ErrorCount { get; } public int WarningCount { get; } public int InfoCount { get; }
        public bool HasErrors => ErrorCount > 0;
        public void Add(ValidationSeverity severity, string code, string assetPath, string field, string message) { throw new System.NotImplementedException("DATA-002"); }
        public void Error(string code, string assetPath, string field, string message) { throw new System.NotImplementedException("DATA-002"); }
        public void Warning(string code, string assetPath, string field, string message) { throw new System.NotImplementedException("DATA-002"); }
        public void Info(string code, string assetPath, string field, string message) { throw new System.NotImplementedException("DATA-002"); }
        public ValidationScope For(string assetPath) { throw new System.NotImplementedException("DATA-002"); }           // convenience: binds AssetPath
        public void Merge(ValidationReport other) { throw new System.NotImplementedException("DATA-002"); }
        public System.Collections.Generic.IEnumerable<ValidationEntry> Sorted() { throw new System.NotImplementedException("DATA-002"); }  // by AssetPath, Field, Code, Message (ordinal)
        public string ToText(int maxEntries = 500) { throw new System.NotImplementedException("DATA-002"); }              // deterministic, uses Sorted()
        public void LogTo(string gameLogCategory, int maxEntries = 50) { throw new System.NotImplementedException("DATA-002"); } // errors→GameLog.Error, warnings→Warn; then one summary line
    }
}
