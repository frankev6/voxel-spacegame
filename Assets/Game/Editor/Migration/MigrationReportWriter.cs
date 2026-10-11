using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Data.Validation;

namespace Game.Editor.Migration
{
    internal struct MigrationShipRow
    {
        internal string Ship;
        internal int LegacyVoxels, EmptyKeys, Placed, DroppedOverlaps, Cells, Palette, Instances, Cores;
        internal MigrationShipRow(string ship, int legacyVoxels, int emptyKeys, int placed, int droppedOverlaps, int cells, int palette, int instances, int cores)
        {
            Ship = ship; LegacyVoxels = legacyVoxels; EmptyKeys = emptyKeys; Placed = placed; DroppedOverlaps = droppedOverlaps;
            Cells = cells; Palette = palette; Instances = instances; Cores = cores;
        }
    }
    /// <summary>SPEC-72 §5.6: ordinal, timestamp-free Markdown with actual outcomes from this run.</summary>
    internal static class MigrationReportWriter
    {
        internal static void Write(string path, IReadOnlyList<ILegacyConverter> converters, LegacyAssetWriter writer, ValidationReport report, IEnumerable<MigrationShipRow> ships)
        {
            var text = new StringBuilder("# Legacy migration report\n\nConverters: ");
            if (converters.Count == 0) text.Append("(none)");
            text.AppendJoin(", ", converters.OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.Ordinal).Select(c => $"{Escape(c.Name)} ({c.Order})"));
            text.Append("\n\n## Assets\n\n| Asset | Outcome |\n|---|---|\n");
            foreach (var o in writer.Outcomes.OrderBy(o => o.AssetPath, StringComparer.Ordinal).ThenBy(o => o.Outcome))
                text.Append('|').Append(Escape(o.AssetPath)).Append('|').Append(o.Outcome).Append("|\n");
            text.Append("\n## Ships\n\n| Ship | Legacy voxels | Empty keys | Placed | Dropped overlaps | Cells | Palette | Instances | Cores |\n|---|---|---|---|---|---|---|---|---|\n");
            foreach (var s in ships.OrderBy(s => s.Ship, StringComparer.Ordinal))
                text.Append('|').Append(Escape(s.Ship)).Append('|').Append(s.LegacyVoxels).Append('|').Append(s.EmptyKeys).Append('|').Append(s.Placed).Append('|').Append(s.DroppedOverlaps).Append('|').Append(s.Cells).Append('|').Append(s.Palette).Append('|').Append(s.Instances).Append('|').Append(s.Cores).Append("|\n");
            text.Append("\n## Entries\n\n```text\n").Append(report.ToText(2000).Replace("```", "'''" )).Append("\n```\n\n## Summary\n\n");
            text.Append(report.ErrorCount).Append(" errors, ").Append(report.WarningCount).Append(" warnings, ").Append(report.InfoCount).Append(" infos\n\n");
            foreach (WriteOutcome outcome in Enum.GetValues(typeof(WriteOutcome)))
                text.Append(outcome).Append(": ").Append(writer.Outcomes.Count(o => o.Outcome == outcome)).Append('\n');
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, text.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
        static string Escape(string value) => (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
