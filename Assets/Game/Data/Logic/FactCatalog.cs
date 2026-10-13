using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;

namespace Game.Data.Logic
{
    /// <summary>Immutable ordinal fact slots and generated-family lookup (SPEC-40 §5.2).</summary>
    public sealed class FactCatalog
    {
        sealed class Entry
        {
            public FactDeclaration Declaration;
            public FactId Id;
            public FactFamily Family;
            public ulong SourceHash;
            public string Asset;
        }
        readonly Entry[] entries;
        readonly Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<ulong, int> hashes = new Dictionary<ulong, int>();
        readonly Dictionary<FactFamily, Dictionary<ulong, int>> generated = new Dictionary<FactFamily, Dictionary<ulong, int>>();
        FactCatalog(Entry[] entries) { this.entries = entries; }
        public int Count => entries.Length;
        public int SlotOf(in FactId id) => hashes.TryGetValue(id.Hash, out int slot) ? slot : -1;
        public int SlotOf(string name) => name != null && names.TryGetValue(name, out int slot) ? slot : -1;
        public FactId IdAt(int slot) => entries[slot].Id;
        public FactKind KindAt(int slot) => entries[slot].Declaration.Kind;
        public FactScope ScopeAt(int slot) => entries[slot].Declaration.Scope;
        public FactFlags FlagsAt(int slot) => entries[slot].Declaration.Flags;
        public FactFamily FamilyAt(int slot) => entries[slot].Family;
        public int DefaultAt(int slot) => entries[slot].Declaration.Default;
        public int MinAt(int slot) => entries[slot].Declaration.Min;
        public int MaxAt(int slot) => entries[slot].Declaration.Max;
        public bool IsWritableByContent(int slot) => entries[slot].Family == FactFamily.Authored;
        public string JournalTextAt(int slot) => entries[slot].Declaration.JournalText ?? "";
        public ulong SourceHashAt(int slot) => entries[slot].SourceHash;
        public int GeneratedSlot(FactFamily family, ulong sourceHash) =>
            generated.TryGetValue(family, out var familySlots) && familySlots.TryGetValue(sourceHash, out int slot) ? slot : -1;

        public static FactCatalog Build(ContentCatalog content, RefResolverRegistry refs, ValidationReport report) =>
            Build(content, refs, report, null);

        // Instance-scoped hash seam for the required collision fixture; production always uses FactId.Hash.
        internal static FactCatalog Build(ContentCatalog content, RefResolverRegistry refs, ValidationReport report, Func<string, ulong> hashForTest)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (refs == null) throw new ArgumentNullException(nameof(refs));
            if (report == null) throw new ArgumentNullException(nameof(report));
            var rows = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var reserved = new[] { "beat_", "clue_", "rumor_", "mystery_", "visited_", "interacted_", "destroyed_", "defeated_", "acquired_", "sys_" };
            foreach (var asset in content.All<FactCatalogDefinition>())
                foreach (var declaration in asset.Facts)
                {
                    string name = declaration.Name, path = "content:" + asset.IdString;
                    if (!ContentIdRules.IsValidFormat(name))
                    { report.Error("NAR-E015", path, "facts", "'" + name + "' is not a valid fact name (content ID format)."); continue; }
                    bool forbidden = false;
                    foreach (string prefix in reserved)
                        if (name.StartsWith(prefix, StringComparison.Ordinal))
                        { report.Error("NAR-E020", path, "facts", "Authored fact '" + name + "' uses a reserved prefix '" + prefix + "'."); forbidden = true; break; }
                    if (forbidden) continue;
                    if (declaration.Kind == FactKind.Bool && declaration.Default != 0 && declaration.Default != 1)
                        report.Error("NAR-E021", path, "facts", "Bool fact default must be 0 or 1.");
                    if ((declaration.Flags & (FactFlags.ReadByCode | FactFlags.WrittenByCode)) != 0 && string.IsNullOrWhiteSpace(declaration.Note))
                        report.Error("NAR-E022", path, "facts", "Fact with flags " + declaration.Flags + " needs a Note.");
                    if ((declaration.Flags & FactFlags.Journal) != 0 && string.IsNullOrWhiteSpace(declaration.JournalText))
                        report.Error("NAR-E022", path, "facts", "Fact with flags " + declaration.Flags + " needs a JournalText.");
                    if (declaration.Min > declaration.Max)
                        report.Error("NAR-E012", path, "facts", "Fact minimum exceeds maximum.");
                    // Snapshot every mutable authored field used after Build.
                    var copy = declaration;
                    copy.FormerNames = declaration.FormerNames == null ? Array.Empty<string>() : (string[])declaration.FormerNames.Clone();
                    Add(new Entry { Declaration = copy, Id = new FactId(name), Asset = path });
                }

            Family(FactFamily.Beat, RefKind.Beat, "");
            Family(FactFamily.Clue, RefKind.Clue, "");
            Family(FactFamily.Mystery, RefKind.Mystery, "");
            Family(FactFamily.Visited, RefKind.StarSystem, "visited_");
            Family(FactFamily.Interacted, RefKind.Entity, "interacted_");
            Family(FactFamily.Destroyed, RefKind.Encounter, "destroyed_");
            Family(FactFamily.Destroyed, RefKind.Blueprint, "destroyed_");
            Family(FactFamily.Destroyed, RefKind.Entity, "destroyed_");
            Family(FactFamily.Defeated, RefKind.Encounter, "defeated_");
            Family(FactFamily.Acquired, RefKind.Item, "acquired_");
            foreach (string name in new[] { "sys_clue_counter", "sys_beats_completed", "sys_star_systems_entered", "sys_territories_entered" })
                Add(new Entry { Id = new FactId(name), Family = FactFamily.System, Asset = "generated:System",
                    Declaration = new FactDeclaration { Name = name, Kind = FactKind.Int, Scope = FactScope.Save } });

            var ordered = new List<Entry>(rows.Values);
            ordered.Sort((a, b) => string.CompareOrdinal(a.Id.Name, b.Id.Name));
            var result = new FactCatalog(ordered.ToArray());
            for (int i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                result.names.Add(row.Id.Name, i);
                ulong hash = hashForTest == null ? row.Id.Hash : hashForTest(row.Id.Name);
                if (result.hashes.TryGetValue(hash, out int other))
                    report.Error("NAR-E014", row.Asset, "facts", "Facts '" + ordered[other].Id.Name + "' and '" + row.Id.Name + "' share a 64-bit hash.");
                else result.hashes.Add(hash, i);
                if (row.Family != FactFamily.Authored && row.Family != FactFamily.System)
                {
                    if (!result.generated.TryGetValue(row.Family, out var slots))
                        result.generated.Add(row.Family, slots = new Dictionary<ulong, int>());
                    if (slots.TryGetValue(row.SourceHash, out int prior))
                        report.Error("NAR-E014", row.Asset, "facts", "Facts '" + ordered[prior].Id.Name + "' and '" + row.Id.Name + "' share a source hash.");
                    else slots.Add(row.SourceHash, i);
                }
            }
            for (int i = 0; i < ordered.Count; i++)
                foreach (string former in ordered[i].Declaration.FormerNames ?? Array.Empty<string>())
                {
                    if (!ContentIdRules.IsValidFormat(former))
                        report.Error("NAR-E015", ordered[i].Asset, "FormerNames", "'" + former + "' is not a valid fact name (content ID format).");
                    else if (result.names.TryGetValue(former, out int other))
                        report.Error("NAR-E018", ordered[i].Asset, "FormerNames", "Fact '" + former + "' is declared twice (" + ordered[other].Asset + ", " + ordered[i].Asset + ").");
                    else result.names.Add(former, i);
                }
            return result;

            void Add(Entry entry)
            {
                if (rows.TryGetValue(entry.Id.Name, out var other))
                    report.Error("NAR-E018", entry.Asset, "facts", "Fact '" + entry.Id.Name + "' is declared twice (" + other.Asset + ", " + entry.Asset + ").");
                else rows.Add(entry.Id.Name, entry);
            }
            void Family(FactFamily family, RefKind kind, string prefix)
            {
                if (!refs.TryGet(kind, out var resolver))
                { report.Warning("NAR-W001", "", family.ToString(), "No resolver for " + kind + "; generated facts were not added."); return; }
                foreach (string id in resolver.AllIds)
                {
                    string name = prefix + id;
                    Add(new Entry { Id = new FactId(name), Family = family, SourceHash = new ContentId(id).Hash.Value,
                        Asset = "generated:" + kind, Declaration = new FactDeclaration { Name = name, Kind = FactKind.Int, Scope = FactScope.Save } });
                }
            }
        }
    }
}
