using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;

namespace Game.Voxel.Grid
{
    /// <summary>Implements SPEC-10 §4.4. Owns immutable decoded blueprints for one boot session.</summary>
    public sealed class BlueprintLibrary : IDisposable
    {
        readonly ContentCatalog catalog;
        readonly Dictionary<ContentHash, ShipBlueprintDefinition> definitions = new Dictionary<ContentHash, ShipBlueprintDefinition>();
        readonly Dictionary<ContentHash, VoxelStructure> cached = new Dictionary<ContentHash, VoxelStructure>();
        readonly HashSet<ContentHash> failed = new HashSet<ContentHash>();
        readonly List<ContentHash> evictions = new List<ContentHash>();
        bool disposed;
        internal int DecodeCount { get; private set; }

        public BlueprintLibrary(ContentCatalog catalog, VoxelContent content)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            foreach (var definition in catalog.All<ShipBlueprintDefinition>()) definitions.Add(definition.Id.Hash, definition);
        }
        public bool TryGet(ContentHash blueprint, out VoxelStructure structure)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintLibrary));
            if (cached.TryGetValue(blueprint, out structure)) return true;
            if (failed.Contains(blueprint) || !definitions.TryGetValue(blueprint, out var definition)) return false;
            DecodeCount++;
            if (!VoxelStructureCodec.TryDecode(definition.Payload, catalog, out structure, out string error))
            {
                failed.Add(blueprint);
                GameLog.Error("Voxel", "blueprint " + definition.IdString + " decode failed: " + error);
                return false;
            }
            structure.SetReadOnly(); cached.Add(blueprint, structure); return true;
        }
        public void ReleaseUnpinnedLarge()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintLibrary));
            evictions.Clear();
            foreach (var pair in cached)
                if (!pair.Value.IsPinned && pair.Value.SolidCellCount > 65536) evictions.Add(pair.Key);
            foreach (var key in evictions) { cached[key].Dispose(); cached.Remove(key); }
            evictions.Clear();
        }
        public int CachedCount => cached.Count;
        public void Dispose()
        {
            if (disposed) return;
            // Fail before disposing anything: the owner must finish/cancel all pinned readers first.
            foreach (var structure in cached.Values)
                if (structure.IsPinned) throw new InvalidOperationException("Cannot dispose a blueprint library with pinned structures");
            foreach (var structure in cached.Values) structure.Dispose();
            cached.Clear(); definitions.Clear(); failed.Clear(); evictions.Clear(); disposed = true;
        }
    }
}
