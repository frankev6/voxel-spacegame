using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-011: decoded blueprint ownership, caching and the strict large-structure eviction threshold.</summary>
    public sealed class BlueprintLibraryTests
    {
        VoxelTestContent fixture;
        ContentCatalog catalog;
        [SetUp]
        public void SetUp()
        {
            fixture = new VoxelTestContent();
            var definitions = new List<ContentDefinition>();
            foreach (var definition in fixture.Service.Catalog.All<ItemDefinition>()) definitions.Add(definition);
            foreach (var spec in new[] { ("ship_small", 1), ("ship_boundary", 16), ("ship_large", 17), ("ship_pinned", 17) })
            {
                using var source = new VoxelStructure();
                ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = fixture.Index("block_hull") });
                for (int i = 0; i < spec.Item2; i++) source.SetBrickUniform(new int3(i, 0, 0), Cell.Palette(palette));
                var definition = TestContentFactory.Create<ShipBlueprintDefinition>(spec.Item1);
                definition.EditorSetPayload(VoxelStructureCodec.Encode(source, VoxsBlockNames.FromCatalog(fixture.Service.Catalog), VoxsEncodeOptions.ForAsset),
                    source.SolidCellCount, Vector3Int.zero, new Vector3Int(spec.Item2 * 16, 16, 16));
                definitions.Add(definition);
            }
            definitions.Add(TestContentFactory.Create<ShipBlueprintDefinition>("ship_invalid"));
            var report = new ValidationReport("blueprint library test"); catalog = ContentCatalog.Build(definitions, report);
            Assert.AreEqual(0, report.ErrorCount);
        }
        [TearDown] public void TearDown() => fixture?.Dispose();
        static ContentHash Hash(string id) => new ContentId(id).Hash;
        [Test]
        public void TryGet_CachesAndIsReadOnly()
        {
            using var library = new BlueprintLibrary(catalog, fixture.Content);
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                Assert.IsTrue(registry.TryGetBlueprint(Hash("ship_small"), out var first));
                Assert.IsTrue(library.TryGet(Hash("ship_small"), out var second)); Assert.AreSame(first, second);
                Assert.AreEqual(1, library.DecodeCount); Assert.AreEqual(1, library.CachedCount);
                Assert.IsTrue(first.IsReadOnly); Assert.Throws<InvalidOperationException>(() => first.SetCell(default, default));
                Assert.AreEqual(PlaceResult.ReadOnly, first.TryPlaceBlock(default, fixture.Content));
            }
            finally { UnityEngine.Object.DestroyImmediate(registry); }
        }
        [Test]
        public void UnknownHash_ReturnsFalse()
        {
            using var library = new BlueprintLibrary(catalog, fixture.Content);
            Assert.IsFalse(library.TryGet(Hash("ship_unknown"), out var missing)); Assert.IsNull(missing);
            Assert.AreEqual(0, library.DecodeCount);
            LogAssert.Expect(LogType.Error, new Regex("blueprint ship_invalid decode failed:"));
            Assert.IsFalse(library.TryGet(Hash("ship_invalid"), out missing)); Assert.IsNull(missing);
            Assert.IsFalse(library.TryGet(Hash("ship_invalid"), out missing)); Assert.IsNull(missing);
            Assert.AreEqual(1, library.DecodeCount); Assert.AreEqual(0, library.CachedCount);
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void ReleaseUnpinnedLarge_KeepsPinned()
        {
            using var library = new BlueprintLibrary(catalog, fixture.Content);
            library.TryGet(Hash("ship_small"), out var small); library.TryGet(Hash("ship_boundary"), out var boundary);
            library.TryGet(Hash("ship_large"), out var large); library.TryGet(Hash("ship_pinned"), out var pinned);
            Assert.AreEqual(65536, boundary.SolidCellCount); pinned.Pin();
            try
            {
                library.ReleaseUnpinnedLarge(); Assert.AreEqual(3, library.CachedCount);
                Assert.IsFalse(large.Data.IsCreated); Assert.IsTrue(small.Data.IsCreated);
                Assert.IsTrue(boundary.Data.IsCreated); Assert.IsTrue(pinned.Data.IsCreated);
                Assert.IsTrue(library.TryGet(Hash("ship_large"), out var again)); Assert.AreNotSame(large, again);
                Assert.AreEqual(5, library.DecodeCount);
            }
            finally { pinned.Unpin(); }
            library.ReleaseUnpinnedLarge(); Assert.AreEqual(2, library.CachedCount); Assert.IsFalse(pinned.Data.IsCreated);
        }
        [Test]
        public void Dispose_NoLeaks()
        {
            void Exercise()
            {
                var library = new BlueprintLibrary(catalog, fixture.Content);
                library.TryGet(Hash("ship_small"), out var small); library.TryGet(Hash("ship_large"), out var large);
                library.Dispose(); library.Dispose();
                Assert.IsFalse(small.Data.IsCreated); Assert.IsFalse(large.Data.IsCreated); Assert.AreEqual(0, library.CachedCount);
            }
            var before = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try { Exercise(); UnsafeUtility.CheckForLeaks(); Exercise(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks()); }
            finally { NativeLeakDetection.Mode = before; }
        }
    }
}
