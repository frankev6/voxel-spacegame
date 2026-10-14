using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Simulation.Factions
{
    /// <summary>Immutable player-first, ordinal faction ordering (SPEC-33 §5.1).</summary>
    public sealed class FactionIndexMap
    {
        readonly FactionDefinition[] definitions;
        readonly Dictionary<ContentHash, byte> indices = new();
        FactionIndexMap(FactionDefinition[] definitions)
        {
            this.definitions = definitions;
            for (byte i = 0; i < definitions.Length; i++)
                if (definitions[i] != null) indices.Add(definitions[i].Id.Hash, i);
        }
        public static FactionIndexMap Build(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            FactionDefinition player = null;
            var rest = new List<FactionDefinition>();
            foreach (var faction in catalog.All<FactionDefinition>())
                if (faction.IsPlayerFaction && player == null) player = faction; else rest.Add(faction);
            if (player == null) GameLog.Error("Factions", "Player faction missing; index zero is reserved.");
            rest.Sort((a, b) => string.CompareOrdinal(a.IdString, b.IdString));
            if (rest.Count > 63) GameLog.Error("Factions", "More than 64 factions; excess definitions have no runtime index.");
            var ordered = new FactionDefinition[1 + Math.Min(63, rest.Count)];
            ordered[0] = player;
            for (int i = 1; i < ordered.Length; i++) ordered[i] = rest[i - 1];
            return new FactionIndexMap(ordered);
        }
        public int Count => definitions.Length;
        public byte IndexOf(FactionDefinition def) => def == null ? (byte)255 : IndexOf(def.Id.Hash);
        public byte IndexOf(in ContentHash faction) => indices.TryGetValue(faction, out byte index) ? index : (byte)255;
        public FactionDefinition ByIndex(byte index) => index < definitions.Length ? definitions[index] : null;
    }
}
