using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Services;
using Game.ShipEditor.Data;
using Unity.Entities;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 EDT-002: session content; owns footprints and borrows immutable content blobs.</summary>
    public sealed class EditorContext : IDisposable
    {
        public ShipEditorConfig Config { get; }
        public BlobAssetReference<BlockDefsBlob> Definitions { get; }
        public BlobAssetReference<OrientationTablesBlob> Orientations { get; }
        public FootprintCache Footprints { get; }
        public BlockItemMap Items { get; }
        public int ColorCount { get; }
        public IReadOnlyList<BlockCategory> Categories { get; }
        internal ContentCatalog Catalog { get; }
        public EditorContext(ShipEditorConfig config, ContentCatalog catalog,
            BlobAssetReference<BlockDefsBlob> definitions, BlobAssetReference<OrientationTablesBlob> orientations, int colorCount)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!definitions.IsCreated || definitions.Value.Blocks.Length != catalog.BlockCount + 1)
                throw new ArgumentException("Block definitions must match the content catalog.", nameof(definitions));
            if (colorCount < 1 || colorCount > 16) throw new ArgumentOutOfRangeException(nameof(colorCount));
            Config = config; Catalog = catalog; Definitions = definitions; Orientations = orientations; ColorCount = colorCount;
            Items = new BlockItemMap(catalog);
            Categories = Array.AsReadOnly((BlockCategory[])Enum.GetValues(typeof(BlockCategory)));
            Footprints = new FootprintCache(definitions, orientations);
        }
        public void Dispose() => Footprints.Dispose();
    }
}
