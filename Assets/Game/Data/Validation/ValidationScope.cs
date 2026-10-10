namespace Game.Data.Validation
{
    /// <summary>A ValidationReport bound to one asset path (SPEC-04 §4.8).</summary>
    public readonly struct ValidationScope
    {
        public void Error(string code, string field, string message) { throw new System.NotImplementedException("DATA-002"); }
        public void Warning(string code, string field, string message) { throw new System.NotImplementedException("DATA-002"); }
        public void Info(string code, string field, string message) { throw new System.NotImplementedException("DATA-002"); }
        public string AssetPath { get; }
    }
}
