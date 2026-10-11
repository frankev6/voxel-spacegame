using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Validation;
using Unity.Collections;
using Unity.Entities;

namespace Game.Data.Services
{
    /// <summary>Boot-time owner of the catalog and blobs (architecture §4.2, service #2). Implements SPEC-04 §4.9, §5.1–§5.2.
    /// Dispose after the singletons are destroyed or the world is disposed (SPEC-01 shutdown order).</summary>
    public sealed class ContentService : IDisposable
    {
        BlobAssetReference<BlockModulesBlob> modules;
        readonly List<UnityEngine.Mesh> propMeshes = new List<UnityEngine.Mesh>();
        bool disposed;

        /// <summary>Throws ContentValidationException when the content has errors (nothing native is allocated before that check).</summary>
        public ContentService(ContentDatabase database) : this((database ?? throw new ArgumentNullException(nameof(database))).Definitions) { }

        internal static ContentService CreateForTests(IReadOnlyList<ContentDefinition> definitions) => new ContentService(definitions);

        ContentService(IReadOnlyList<ContentDefinition> definitions)
        {
            var report = new ValidationReport("ContentDatabase");                                      // §5.2 step 1
            var catalog = ContentCatalog.Build(definitions, report);                                    // step 2
            var live = new List<ContentDefinition>();
            if (definitions != null)
                foreach (var d in definitions)
                    if (d != null && !string.IsNullOrEmpty(d.IdString) && catalog.TryGet(d.Id, out var found) && ReferenceEquals(found, d)) live.Add(d);
            live.Sort((a, b) => string.CompareOrdinal(a.IdString, b.IdString));
            foreach (var d in live)                                                                     // step 3: runtime validation pass
            {
                var context = new ContentValidationContext("content:" + d.IdString, false, x => catalog.TryGet(x.Id, out var y) && ReferenceEquals(x, y));
                d.Validate(report.For("content:" + d.IdString), context);
            }
            if (report.HasErrors)                                                                       // step 4
            {
                report.LogTo(LogCategories.Content);
                throw new ContentValidationException(report);
            }
            Catalog = catalog;
            try
            {
                Orientations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);   // step 5
                BlockDefs = BlockDefsBlobBuilder.Build(catalog, report, propMeshes, Allocator.Persistent, out modules);            // step 6
                if (report.HasErrors) throw new ContentValidationException(report);
            }
            catch
            {
                DisposeBlobs();
                throw;
            }
            DatabaseHash = StableHash.Combine(BlockDefs.Value.BlockDefsHash, Catalog.IdentityHash);   // step 7
            BootReport = report;
            GameLog.Info(LogCategories.Content, $"Content: {Catalog.Count} definitions, {Catalog.BlockCount} blocks, hash 0x{DatabaseHash:X16}, {report.WarningCount} warnings");
        }

        public ContentCatalog Catalog { get; }
        public ValidationReport BootReport { get; }
        public BlobAssetReference<BlockDefsBlob> BlockDefs { get; private set; }
        public BlobAssetReference<OrientationTablesBlob> Orientations { get; private set; }
        public IReadOnlyList<UnityEngine.Mesh> PropMeshes => propMeshes;   // index = BlockDef.PropMeshIndex
        public ulong DatabaseHash { get; }                                 // Combine(BlockDefsHash, IdentityHash); saved as diagnostics by SPEC-05

        /// <summary>Creates one entity "ContentSingletons" with BlockDefsRef, BlockModulesRef and OrientationTablesRef; does nothing if the
        /// world already has a BlockDefsRef.</summary>
        public void CreateSingletons(EntityManager entityManager)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ContentService));
            using var q = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BlockDefsRef>());
            if (!q.IsEmpty) return;
            var e = entityManager.CreateEntity(typeof(BlockDefsRef), typeof(BlockModulesRef), typeof(OrientationTablesRef));
            entityManager.SetName(e, "ContentSingletons");
            entityManager.SetComponentData(e, new BlockDefsRef { Value = BlockDefs });
            entityManager.SetComponentData(e, new BlockModulesRef { Value = modules });
            entityManager.SetComponentData(e, new OrientationTablesRef { Value = Orientations });
        }

        public void DestroySingletons(EntityManager entityManager)
        {
            using var q = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BlockDefsRef>());
            if (!q.IsEmpty) entityManager.DestroyEntity(q);
        }

        /// <summary>Disposes the three blobs; safe to call twice.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DisposeBlobs();
        }

        void DisposeBlobs()
        {
            if (BlockDefs.IsCreated) BlockDefs.Dispose();
            if (modules.IsCreated) modules.Dispose();
            if (Orientations.IsCreated) Orientations.Dispose();
            BlockDefs = default; modules = default; Orientations = default;
        }
    }
}
