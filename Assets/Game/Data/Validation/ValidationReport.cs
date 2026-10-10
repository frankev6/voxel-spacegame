namespace Game.Data.Validation
{
    /// <summary>Collects errors and warnings (architecture §6.7). Not thread-safe; one report per pass. Implements SPEC-04 §4.8.</summary>
    public sealed class ValidationReport
    {
        readonly System.Collections.Generic.List<ValidationEntry> entries = new();

        public ValidationReport(string context) { Context = context ?? string.Empty; }   // e.g. "ContentDatabase", "MIG", "Load p001/auto_0"
        public string Context { get; }
        public System.Collections.Generic.IReadOnlyList<ValidationEntry> Entries => entries;
        public int ErrorCount { get; private set; } public int WarningCount { get; private set; } public int InfoCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public void Add(ValidationSeverity severity, string code, string assetPath, string field, string message)
        {
            Append(new ValidationEntry(severity, code, assetPath, field, message));
        }

        public void Error(string code, string assetPath, string field, string message) { Add(ValidationSeverity.Error, code, assetPath, field, message); }
        public void Warning(string code, string assetPath, string field, string message) { Add(ValidationSeverity.Warning, code, assetPath, field, message); }
        public void Info(string code, string assetPath, string field, string message) { Add(ValidationSeverity.Info, code, assetPath, field, message); }
        public ValidationScope For(string assetPath) => new ValidationScope(this, assetPath);   // convenience: binds AssetPath

        public void Merge(ValidationReport other)
        {
            if (other == null || ReferenceEquals(other, this)) return;
            foreach (var entry in other.entries) Append(entry);
        }

        public System.Collections.Generic.IEnumerable<ValidationEntry> Sorted()   // by AssetPath, Field, Code, Message (ordinal)
        {
            var sorted = new ValidationEntry[entries.Count];
            entries.CopyTo(sorted);
            System.Array.Sort(sorted, Compare);   // the comparer is total over every field, so the unstable sort is still deterministic
            return sorted;
        }

        public string ToText(int maxEntries = 500)   // deterministic, uses Sorted()
        {
            var text = new System.Text.StringBuilder();
            int written = 0;
            foreach (var entry in Sorted())
            {
                if (written == maxEntries) break;
                text.Append(Line(entry)).Append('\n');
                written++;
            }
            if (entries.Count > written) text.Append("... ").Append(entries.Count - written).Append(" more\n");
            text.Append(Summary());
            return text.ToString();
        }

        public void LogTo(string gameLogCategory, int maxEntries = 50)   // errors→GameLog.Error, warnings→Warn; then one summary line
        {
            int written = 0;
            for (var severity = ValidationSeverity.Error; ; severity--)   // most severe first, so the cap never hides an error behind warnings
            {
                foreach (var entry in Sorted())
                {
                    if (entry.Severity != severity) continue;
                    if (written == maxEntries) break;
                    Log(gameLogCategory, severity, Line(entry));
                    written++;
                }
                if (severity == ValidationSeverity.Info) break;
            }
            string summary = Context + ": " + Summary() + (entries.Count > written ? " (" + (entries.Count - written) + " not shown)" : string.Empty);
            Log(gameLogCategory, HasErrors ? ValidationSeverity.Error : WarningCount > 0 ? ValidationSeverity.Warning : ValidationSeverity.Info, summary);
        }

        void Append(in ValidationEntry entry)
        {
            entries.Add(entry);
            switch (entry.Severity)
            {
                case ValidationSeverity.Error: ErrorCount++; break;
                case ValidationSeverity.Warning: WarningCount++; break;
                default: InfoCount++; break;
            }
        }

        string Summary() => ErrorCount + " errors, " + WarningCount + " warnings, " + InfoCount + " infos";

        static string Line(in ValidationEntry e)
        {
            char tag = e.Severity == ValidationSeverity.Error ? 'E' : e.Severity == ValidationSeverity.Warning ? 'W' : 'I';
            return "[" + tag + "] " + e.Code + " " + e.AssetPath + (e.Field.Length > 0 ? " " + e.Field : string.Empty) + ": " + e.Message;
        }

        static void Log(string category, ValidationSeverity severity, string message)
        {
            switch (severity)
            {
                case ValidationSeverity.Error: Game.Core.GameLog.Error(category, message); break;
                case ValidationSeverity.Warning: Game.Core.GameLog.Warn(category, message); break;
                default: Game.Core.GameLog.Info(category, message); break;
            }
        }

        static int Compare(ValidationEntry a, ValidationEntry b)
        {
            int c = string.CompareOrdinal(a.AssetPath, b.AssetPath);
            if (c == 0) c = string.CompareOrdinal(a.Field, b.Field);
            if (c == 0) c = string.CompareOrdinal(a.Code, b.Code);
            if (c == 0) c = string.CompareOrdinal(a.Message, b.Message);
            if (c == 0) c = a.Severity.CompareTo(b.Severity);
            return c;
        }
    }
}
