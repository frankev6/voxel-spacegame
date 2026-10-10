namespace Game.Data.Validation
{
    /// <summary>A ValidationReport bound to one asset path (SPEC-04 §4.8).</summary>
    public readonly struct ValidationScope
    {
        readonly ValidationReport report;

        internal ValidationScope(ValidationReport report, string assetPath)
        {
            this.report = report;
            AssetPath = assetPath ?? string.Empty;
        }

        public void Error(string code, string field, string message) { report.Error(code, AssetPath, field, message); }
        public void Warning(string code, string field, string message) { report.Warning(code, AssetPath, field, message); }
        public void Info(string code, string field, string message) { report.Info(code, AssetPath, field, message); }
        public string AssetPath { get; }
    }
}
