using System;
using System.Collections.Generic;

namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5 and §4.8. Participant chunks in capture/restore order. META and IDPL belong to the container and are not registered.</summary>
    public sealed class SaveChunkRegistry
    {
        /// <summary>The §4.8 table; immutable. Adding a participant = adding a row here and in the spec table in the same commit.</summary>
        [field: Game.Core.StaticStateOk("immutable registry built once")] public static SaveChunkRegistry Default { get; } = new SaveChunkRegistry(new[]
        {
            new ChunkRegistration("SESS", 10, "SPEC-01", "galaxy seed, UniversalTime, playtime, the PersistentId counter, session flags (creative), new-game options"),
            new ChunkRegistration("FACT", 20, "SPEC-40", "FactStore"),
            new ChunkRegistration("FACN", 30, "SPEC-33", "faction relations"),
            new ChunkRegistration("QUST", 40, "SPEC-42", "quests, stage baselines, clues, mysteries"),
            new ChunkRegistration("DLGS", 50, "SPEC-41", "dialog state (once-only lines, active beat)"),
            new ChunkRegistration("INVT", 60, "SPEC-50", "wallet, cargo slots (overflow included) with slot indices and capacity, merchant stock (no hotbar)"),
            new ChunkRegistration("FLET", 70, "SPEC-01", "fleet records, flagship, ship structures as VOXS (damage written back first)"),
            new ChunkRegistration("GLXY", 80, "SPEC-30", "per-star-system deltas, edited structures as Partial VOXS"),
            new ChunkRegistration("PLOC", 90, "SPEC-31", "player location: StarSystemId, UniversePosition (double3), yaw, docked body"),
            new ChunkRegistration("ENCT", 100, "SPEC-33", "encounter director state"),
        });

        readonly ChunkRegistration[] entries;

        /// <summary>Keys are 4 characters [A-Z0-9], unique, not META/IDPL; orders are unique positive multiples of 10. Throws ArgumentException.</summary>
        public SaveChunkRegistry(IEnumerable<ChunkRegistration> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var list = new List<ChunkRegistration>(entries);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            foreach (var e in list)
            {
                if (!IsValidKey(e.Key)) throw new ArgumentException($"Chunk key '{e.Key}' must be 4 characters [A-Z0-9].", nameof(entries));
                if (e.Key == "META" || e.Key == "IDPL") throw new ArgumentException($"Chunk key '{e.Key}' is reserved for the container.", nameof(entries));
                if (!keys.Add(e.Key)) throw new ArgumentException($"Chunk key '{e.Key}' is registered twice.", nameof(entries));
                if (e.Order <= 0 || e.Order % 10 != 0) throw new ArgumentException($"Chunk '{e.Key}' order {e.Order} must be a positive multiple of 10.", nameof(entries));
                if (!orders.Add(e.Order)) throw new ArgumentException($"Chunk order {e.Order} is used twice.", nameof(entries));
            }
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
            this.entries = list.ToArray();
        }

        public IReadOnlyList<ChunkRegistration> Entries => entries;

        public bool TryGetOrder(string key, out int order)
        {
            foreach (var e in entries)
                if (string.Equals(e.Key, key, StringComparison.Ordinal)) { order = e.Order; return true; }
            order = 0;
            return false;
        }

        static bool IsValidKey(string key)
        {
            if (key == null || key.Length != 4) return false;
            foreach (var c in key)
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            return true;
        }
    }
}
