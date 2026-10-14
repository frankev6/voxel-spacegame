using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Logic;

namespace Game.Narrative.Dialog
{
    /// <summary>SPEC-41 §5.5 and ADR-0084: ordinal lookup of the generated, precompiled command nodes.</summary>
    public sealed class DialogCommandTable
    {
        readonly Dictionary<string, IEffect> entries = new(StringComparer.Ordinal);
        public DialogCommandTable(DialogCommandTableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Entries == null) throw new ArgumentException("Dialog command entries are missing", nameof(definition));
            foreach (var entry in definition.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Text) || entry.Effect == null || !entry.Effect.TryGetSingleEffect(out var effect))
                    throw new ArgumentException("Dialog command is missing, stale or not a single compiled effect: " + entry.Text, nameof(definition));
                if (!entries.TryAdd(entry.Text, effect)) throw new ArgumentException("Duplicate dialog command: " + entry.Text, nameof(definition));
            }
        }
        public bool TryGet(string normalizedText, out IEffect effect)
        { effect = null; return normalizedText != null && entries.TryGetValue(normalizedText, out effect); }
        public int Count => entries.Count;
    }
}
