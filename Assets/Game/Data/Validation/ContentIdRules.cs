namespace Game.Data.Validation
{
    /// <summary>Content ID format and prefix rules (SPEC-04 §3 decisions 1–2, §4.3).</summary>
    public static class ContentIdRules
    {
        public const int MinLength = 3, MaxLength = 64;
        public static bool IsValidFormat(string id) { throw new System.NotImplementedException("DATA-004"); }
        public static bool HasAllowedPrefix(System.Type defType, string id, out string[] allowed) { throw new System.NotImplementedException("DATA-004"); }
    }
}
