using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;

namespace Game.Data.Services
{
    /// <summary>Read-only runtime view of all content. Implements SPEC-04 §4.9, §5.4. Reads are thread-safe; Try/Resolve recording is main-thread only.
    /// Runtime indices are 1-based per family (decision 5): a family is a definition type T, sorted by ordinal ID.</summary>
    public sealed class ContentCatalog
    {
        /// <summary>Test hook: replaces the ID hash so tests can force a collision (DATA-E005). Null in production.</summary>
        internal static Func<string, ContentHash> HashOverrideForTests;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { HashOverrideForTests = null; }

        sealed class Family
        {
            public readonly List<ContentDefinition> List = new List<ContentDefinition>();
            public readonly Dictionary<ContentDefinition, ushort> Index = new Dictionary<ContentDefinition, ushort>(ReferenceComparer.Instance);
            public Array Typed;   // T[] for All<T>()
        }

        sealed class ReferenceComparer : IEqualityComparer<ContentDefinition>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(ContentDefinition a, ContentDefinition b) => ReferenceEquals(a, b);
            public int GetHashCode(ContentDefinition d) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(d);   // in-memory only
        }

        readonly Dictionary<string, ContentDefinition> byId = new Dictionary<string, ContentDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string, ContentDefinition> aliases = new Dictionary<string, ContentDefinition>(StringComparer.Ordinal);
        readonly Dictionary<ulong, ContentDefinition> byHash = new Dictionary<ulong, ContentDefinition>();
        readonly Dictionary<Type, Family> families = new Dictionary<Type, Family>();
        readonly List<UnresolvedContentId> unresolved = new List<UnresolvedContentId>();
        readonly HashSet<(string, string)> unresolvedKeys = new HashSet<(string, string)>();
        readonly HashSet<string> loggedAliases = new HashSet<string>(StringComparer.Ordinal);

        ContentCatalog() { }

        /// <summary>§5.4 steps 1–7. Errors are collected in the report, never thrown here.</summary>
        public static ContentCatalog Build(IReadOnlyList<ContentDefinition> definitions, ValidationReport report)
        {
            var c = new ContentCatalog();
            var live = new List<ContentDefinition>();
            for (int i = 0; i < (definitions?.Count ?? 0); i++)
            {
                var d = definitions[i];
                if (d == null) { report.Error("DATA-E007", "ContentDatabase", $"definitions[{i}]", "Null entry in the content database."); continue; }
                string id = d.IdString, path = PathOf(d);
                if (string.IsNullOrEmpty(id)) { report.Error("DATA-E001", path, "id", "ID is empty."); continue; }
                if (!ContentIdRules.IsValidFormat(id)) { report.Error("DATA-E002", path, "id", $"ID '{id}' is not lowercase snake_case of 3–64 characters."); continue; }
                if (!ContentIdRules.HasAllowedPrefix(d.GetType(), id, out var allowed))
                {
                    report.Error("DATA-E003", path, "id", allowed.Length == 0
                        ? $"Type {d.GetType().Name} declares no [ContentIdPrefix]."
                        : $"ID '{id}' does not start with an allowed prefix for {d.GetType().Name} ({string.Join(", ", allowed)}).");
                    continue;
                }
                if (c.byId.TryGetValue(id, out var first)) { report.Error("DATA-E004", path, "id", $"Duplicate ID '{id}': also used by {first.name}."); continue; }
                ulong hash = (HashOverrideForTests != null ? HashOverrideForTests(id) : d.Id.Hash).Value;
                if (c.byHash.TryGetValue(hash, out var other))
                { report.Error("DATA-E005", path, "id", $"ID '{id}' has the same 64-bit hash as '{other.IdString}'."); continue; }
                c.byId.Add(id, d);
                c.byHash.Add(hash, d);
                live.Add(d);
            }
            foreach (var d in live)
                for (int k = 0; k < d.FormerIds.Count; k++)
                {
                    string former = d.FormerIds[k];
                    if (!ContentIdRules.IsValidFormat(former)) report.Error("DATA-E006", PathOf(d), $"formerIds[{k}]", $"Former ID '{former}' is not a valid ID.");
                    else if (c.byId.ContainsKey(former)) report.Error("DATA-E006", PathOf(d), $"formerIds[{k}]", $"Former ID '{former}' equals a live ID.");
                    else if (c.aliases.TryGetValue(former, out var dup)) report.Error("DATA-E006", PathOf(d), $"formerIds[{k}]", $"Former ID '{former}' is also a former ID of '{dup.IdString}'.");
                    else c.aliases.Add(former, d);
                }
            live.Sort((a, b) => string.CompareOrdinal(a.IdString, b.IdString));
            foreach (var d in live)
                for (var t = d.GetType(); t != null && t != typeof(ContentDefinition); t = t.BaseType)
                {
                    if (!c.families.TryGetValue(t, out var f)) c.families[t] = f = new Family();
                    f.List.Add(d);                                   // already in ordinal order
                }
            foreach (var kv in c.families)
            {
                var f = kv.Value;
                if (f.List.Count > ushort.MaxValue) report.Error("DATA-E009", "ContentDatabase", kv.Key.Name, $"Family {kv.Key.Name} has {f.List.Count} definitions; at most 65,535 fit a ushort index.");
                f.Typed = Array.CreateInstance(kv.Key, f.List.Count);
                for (int i = 0; i < f.List.Count; i++) { f.Typed.SetValue(f.List[i], i); f.Index[f.List[i]] = (ushort)Math.Min(i + 1, ushort.MaxValue); }
            }
            c.MissingBlock = c.byId.TryGetValue("block_missing", out var mb) ? mb as BlockDefinition : null;
            c.MissingItem = c.byId.TryGetValue("item_missing", out var mi) && !(mi is BlockDefinition) ? mi as ItemDefinition : null;
            if (c.MissingBlock == null) report.Error("DATA-E008", "ContentDatabase", "block_missing", "block_missing is missing or is not a BlockDefinition.");
            if (c.MissingItem == null) report.Error("DATA-E008", "ContentDatabase", "item_missing", "item_missing is missing or is not a plain ItemDefinition.");
            c.Count = live.Count;
            c.BlockCount = (ushort)Math.Min(c.FamilyOf(typeof(BlockDefinition))?.List.Count ?? 0, ushort.MaxValue);
            c.IdentityHash = ComputeIdentityHash(live);
            return c;
        }

        static string PathOf(ContentDefinition d) => "content:" + (string.IsNullOrEmpty(d.IdString) ? d.name : d.IdString);

        /// <summary>§5.4 step 7: StableHash of (UTF-8 ID, 0x00, UTF-8 type full name, 0x00) for every definition in ordinal ID order.</summary>
        static ulong ComputeIdentityHash(List<ContentDefinition> sorted)
        {
            using var ms = new MemoryStream();
            foreach (var d in sorted)
            {
                var id = Encoding.UTF8.GetBytes(d.IdString); ms.Write(id, 0, id.Length); ms.WriteByte(0);
                var type = Encoding.UTF8.GetBytes(d.GetType().FullName); ms.Write(type, 0, type.Length); ms.WriteByte(0);
            }
            return StableHash.Hash64(new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length));
        }

        Family FamilyOf(Type t) => families.TryGetValue(t, out var f) ? f : null;

        public int Count { get; private set; }
        public ulong IdentityHash { get; private set; }                    // fingerprint of all (ID, type) pairs, §5.4 step 7
        public BlockDefinition MissingBlock { get; private set; }
        public ItemDefinition MissingItem { get; private set; }

        /// <summary>Live IDs, then former IDs (alias hits log once per alias).</summary>
        public bool TryGet(in ContentId id, out ContentDefinition def)
        {
            def = null;
            if (id.Value == null) return false;
            if (byId.TryGetValue(id.Value, out def)) return true;
            if (!aliases.TryGetValue(id.Value, out def)) return false;
            if (loggedAliases.Add(id.Value)) GameLog.Info(LogCategories.Content, $"'{id.Value}' is a former ID of '{def.IdString}'");
            return true;
        }

        public bool TryGet<T>(in ContentId id, out T def) where T : ContentDefinition
        {
            def = TryGet(id, out var d) ? d as T : null;
            return def != null;
        }

        /// <summary>For hashes coming from ECS data; live IDs only.</summary>
        public bool TryGet(in ContentHash hash, out ContentDefinition def) => byHash.TryGetValue(hash.Value, out def);

        public bool TryGet<T>(in ContentHash hash, out T def) where T : ContentDefinition
        {
            def = byHash.TryGetValue(hash.Value, out var d) ? d as T : null;
            return def != null;
        }

        /// <summary>KeyNotFoundException if absent or of another type.</summary>
        public T Get<T>(in ContentId id) where T : ContentDefinition
            => TryGet<T>(id, out var def) ? def : throw new KeyNotFoundException($"No {typeof(T).Name} with ID '{id.Value}'");

        /// <summary>Records an unresolved entry on failure (once per (ID, expected type)); a definition of the wrong type also counts.</summary>
        public bool TryResolve<T>(in ContentId id, string context, out T def) where T : ContentDefinition
        {
            if (TryGet(id, out def)) return true;
            if (unresolvedKeys.Add((id.Value, typeof(T).Name))) unresolved.Add(new UnresolvedContentId(id.Value, typeof(T).Name, context));
            return false;
        }

        public BlockDefinition ResolveBlockOrMissing(in ContentId id, string context) => TryResolve<BlockDefinition>(id, context, out var b) ? b : MissingBlock;
        public ItemDefinition ResolveItemOrMissing(in ContentId id, string context) => TryResolve<ItemDefinition>(id, context, out var i) ? i : MissingItem;

        /// <summary>Every definition assignable to T, sorted by ordinal ID.</summary>
        public IReadOnlyList<T> All<T>() where T : ContentDefinition => FamilyOf(typeof(T))?.Typed as T[] ?? Array.Empty<T>();

        /// <summary>1-based; 0 if not in the family.</summary>
        public ushort IndexOf<T>(T def) where T : ContentDefinition
            => def != null && FamilyOf(typeof(T)) is Family f && f.Index.TryGetValue(def, out var i) ? i : (ushort)0;

        /// <summary>Null for 0 or out of range.</summary>
        public T GetByIndex<T>(ushort index) where T : ContentDefinition
        {
            var f = FamilyOf(typeof(T));
            return f == null || index == 0 || index > f.List.Count ? null : (T)f.List[index - 1];
        }

        public ushort BlockCount { get; private set; }
        public ushort GetBlockIndex(BlockDefinition block) => IndexOf(block);        // == IndexOf<BlockDefinition>
        public BlockDefinition GetBlock(ushort blockIndex) => GetByIndex<BlockDefinition>(blockIndex);   // null for Air
        public bool TryGetBlockIndex(in ContentId id, out ushort blockIndex)
        {
            blockIndex = TryGet<BlockDefinition>(id, out var b) ? IndexOf(b) : (ushort)0;
            return blockIndex != 0;
        }

        public bool IsPlaceholder(ContentDefinition def) => def != null && (ReferenceEquals(def, MissingBlock) || ReferenceEquals(def, MissingItem));   // block_missing, item_missing

        public IReadOnlyList<UnresolvedContentId> Unresolved => unresolved;
        public void ClearUnresolved() { unresolved.Clear(); unresolvedKeys.Clear(); }
    }
}
