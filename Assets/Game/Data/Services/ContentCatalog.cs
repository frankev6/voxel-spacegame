namespace Game.Data.Services
{
    /// <summary>Read-only runtime view of all content. Implements SPEC-04 §4.9, §5.4. Reads are thread-safe; Try/Resolve recording is main-thread only.</summary>
    public sealed class ContentCatalog
    {
        public static ContentCatalog Build(System.Collections.Generic.IReadOnlyList<Game.Data.Definitions.ContentDefinition> definitions, Game.Data.Validation.ValidationReport report) { throw new System.NotImplementedException("DATA-005"); }

        public int Count { get; }
        public ulong IdentityHash { get; }                    // fingerprint of all (ID, type) pairs, §5.4 step 7
        public Game.Data.Definitions.BlockDefinition MissingBlock { get; }
        public Game.Data.Definitions.ItemDefinition MissingItem { get; }

        public bool TryGet(in Game.Core.ContentId id, out Game.Data.Definitions.ContentDefinition def) { throw new System.NotImplementedException("DATA-005"); }   // live IDs, then former IDs
        public bool TryGet<T>(in Game.Core.ContentId id, out T def) where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("DATA-005"); }
        public bool TryGet(in Game.Core.ContentHash hash, out Game.Data.Definitions.ContentDefinition def) { throw new System.NotImplementedException("DATA-005"); }   // for hashes coming from ECS data; live IDs only
        public bool TryGet<T>(in Game.Core.ContentHash hash, out T def) where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("DATA-005"); }
        public T Get<T>(in Game.Core.ContentId id) where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("DATA-005"); }      // KeyNotFoundException if absent
        public bool TryResolve<T>(in Game.Core.ContentId id, string context, out T def) where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("DATA-005"); } // records unresolved on failure
        public Game.Data.Definitions.BlockDefinition ResolveBlockOrMissing(in Game.Core.ContentId id, string context) { throw new System.NotImplementedException("DATA-005"); }
        public Game.Data.Definitions.ItemDefinition ResolveItemOrMissing(in Game.Core.ContentId id, string context) { throw new System.NotImplementedException("DATA-005"); }

        public System.Collections.Generic.IReadOnlyList<T> All<T>() where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("DATA-005"); } // sorted by ID (ordinal)
        public ushort IndexOf<T>(T def) where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("DATA-005"); }     // 1-based; 0 if not in family
        public T GetByIndex<T>(ushort index) where T : Game.Data.Definitions.ContentDefinition { throw new System.NotImplementedException("DATA-005"); } // null for 0 / out of range

        public ushort BlockCount { get; }
        public ushort GetBlockIndex(Game.Data.Definitions.BlockDefinition block) { throw new System.NotImplementedException("DATA-005"); }        // == IndexOf<BlockDefinition>
        public Game.Data.Definitions.BlockDefinition GetBlock(ushort blockIndex) { throw new System.NotImplementedException("DATA-005"); }        // null for Air
        public bool TryGetBlockIndex(in Game.Core.ContentId id, out ushort blockIndex) { throw new System.NotImplementedException("DATA-005"); }
        public bool IsPlaceholder(Game.Data.Definitions.ContentDefinition def) { throw new System.NotImplementedException("DATA-005"); }          // block_missing, item_missing

        public System.Collections.Generic.IReadOnlyList<UnresolvedContentId> Unresolved { get; }
        public void ClearUnresolved() { throw new System.NotImplementedException("DATA-005"); }
    }
}
