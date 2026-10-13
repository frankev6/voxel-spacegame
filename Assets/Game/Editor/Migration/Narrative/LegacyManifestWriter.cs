using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>Preservation evidence assembled from the same emissions that become output files.</summary>
    internal sealed class LegacyManifestWriter
    {
        internal readonly struct Entry
        {
            public readonly string SourcePath, Text, OutputFile, Destination;
            public readonly bool Unavailable;
            internal Entry(string sourcePath, string text, string outputFile, string destination, bool unavailable)
            { SourcePath = sourcePath; Text = text; OutputFile = outputFile; Destination = destination; Unavailable = unavailable; }
        }
        readonly List<Entry> entries = new();
        readonly HashSet<string> sources = new(StringComparer.Ordinal);
        readonly HashSet<string> lineIds = new(StringComparer.Ordinal);
        internal IReadOnlyList<Entry> Entries => entries;

        internal void AddDialogue(string outputFile, YarnEmitter.Result emission)
        {
            if (emission == null) throw new ArgumentNullException(nameof(emission));
            // Check the entire addition before mutating the manifest; a duplicate invalidates the conversion.
            var incomingIds = new HashSet<string>(StringComparer.Ordinal);
            var incomingSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in emission.Lines)
            {
                Validate(line.SourcePath, line.Text, outputFile, line.LineId);
                if (lineIds.Contains(line.LineId) || !incomingIds.Add(line.LineId))
                    throw new FormatException("Duplicate converted line ID: " + line.LineId);
                if (sources.Contains(line.SourcePath) || !incomingSources.Add(line.SourcePath))
                    throw new FormatException("Duplicate preservation source: " + line.SourcePath);
            }
            foreach (var line in emission.Lines)
            {
                lineIds.Add(line.LineId); sources.Add(line.SourcePath);
                entries.Add(new Entry(line.SourcePath, line.Text, outputFile, line.LineId, line.Unavailable));
            }
        }

        // NAR-016 appends quest/template/log fields using the same representation.
        internal void AddField(string sourcePath, string text, string outputFile, string field)
        {
            Validate(sourcePath, text, outputFile, field);
            if (!sources.Add(sourcePath)) throw new FormatException("Duplicate preservation source: " + sourcePath);
            entries.Add(new Entry(sourcePath, text, outputFile, field, false));
        }
        static void Validate(string source, string text, string file, string destination)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(destination) || text == null)
                throw new ArgumentException("Every preservation row requires source, text, output and destination.");
        }

        internal string Render()
        {
            var output = new StringBuilder("# Legacy narrative preservation manifest\n\n");
            output.Append("Generated from the legacy source and emitted destinations. No timestamps or machine-specific paths.\n\n");
            output.Append("Unavailable pool choices are retained as comments, not executable choices. Text cells use HTML entities to preserve Markdown delimiters.\n\n");
            output.Append("| Source JSON path | Text | Output file | Line ID or quest field | Form |\n");
            output.Append("|---|---|---|---|---|\n");
            // Retain source traversal/emission order, rather than filesystem or dictionary enumeration.
            foreach (var row in entries)
                output.Append("| ").Append(Cell(row.SourcePath)).Append(" | ").Append(Cell(row.Text))
                    .Append(" | ").Append(Cell(row.OutputFile)).Append(" | ").Append(Cell(row.Destination))
                    .Append(" | ").Append(row.Unavailable ? "LEGACY-UNAVAILABLE comment" : "Preserved").Append(" |\n");
            return output.ToString();
        }
        static string Cell(string value)
        {
            var result = new StringBuilder();
            foreach (char character in value)
            {
                switch (character)
                {
                    case '&': result.Append("&amp;"); break;
                    case '<': result.Append("&lt;"); break;
                    case '>': result.Append("&gt;"); break;
                    case '|': result.Append("&#124;"); break;
                    case '\\': result.Append("&#92;"); break;
                    case '`': result.Append("&#96;"); break;
                    case '*': result.Append("&#42;"); break;
                    case '_': result.Append("&#95;"); break;
                    case '[': result.Append("&#91;"); break;
                    case ']': result.Append("&#93;"); break;
                    case '\n': result.Append("&#10;"); break;
                    case '\r': result.Append("&#13;"); break;
                    default: result.Append(character); break;
                }
            }
            return result.ToString();
        }
    }
}
