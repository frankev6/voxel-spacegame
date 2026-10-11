using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-007: ContentService lifecycle and singletons (SPEC-04 §5.1–§5.2).</summary>
    public sealed class ContentServiceTests
    {
        NativeLeakDetectionMode previousMode;
        [SetUp] public void SetUp() { previousMode = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace; }
        [TearDown] public void TearDown() { NativeLeakDetection.Mode = previousMode; DestroyAll(); }

        static List<ContentDefinition> Valid() => BlockDefsBlobBuilderTests.Defs(out _, out _);

        [Test]
        public void InvalidContent_Throws_AndDoesNotLeak()
        {
            var defs = Valid();
            defs.Add(Item("item_broken", so => Set(so, "maxStack", 0)));   // DATA-E021
            LogAssert.Expect(LogType.Error, new Regex("DATA-E021"));
            LogAssert.Expect(LogType.Error, new Regex("error"));          // ValidationReport.LogTo summary line
            var ex = Assert.Throws<ContentValidationException>(() => ContentService.CreateForTests(defs));
            Assert.IsTrue(ex.Report.HasErrors);
            CollectionAssert.Contains(new List<string>(System.Linq.Enumerable.Select(ex.Report.Entries, e => e.Code)), "DATA-E021");
        }

        [Test]
        public void CreateSingletons_IsIdempotent()
        {
            using var service = ContentService.CreateForTests(Valid());
            using var world = new World("content");
            var em = world.EntityManager;
            service.CreateSingletons(em); service.CreateSingletons(em);
            using var q = em.CreateEntityQuery(typeof(BlockDefsRef), typeof(BlockModulesRef), typeof(OrientationTablesRef));
            Assert.AreEqual(1, q.CalculateEntityCount());
            Assert.AreEqual(service.Catalog.BlockCount + 1, q.GetSingleton<BlockDefsRef>().Value.Value.Blocks.Length);
            Assert.AreEqual(24, q.GetSingleton<OrientationTablesRef>().Value.Value.Matrices.Length);
            var thruster = service.Catalog.GetBlockIndex(service.Catalog.Get<BlockDefinition>(new ContentId("block_thruster_medium")));
            Assert.IsTrue(q.GetSingleton<BlockModulesRef>().Value.Value.TryGet<ThrusterParams>(BlockModuleId.Thruster, thruster, out var row));
            Assert.AreEqual(40000f, row.ThrustNewtons);
            service.DestroySingletons(em);
            Assert.AreEqual(0, q.CalculateEntityCount());
            service.DestroySingletons(em);   // nothing to destroy
        }

        [Test]
        public void Lifecycle_NoLeaks()
        {
            var service = ContentService.CreateForTests(Valid());
            using (var world = new World("content"))
            {
                service.CreateSingletons(world.EntityManager);
                service.DestroySingletons(world.EntityManager);
            }
            Assert.IsTrue(service.BlockDefs.IsCreated && service.Orientations.IsCreated);
            service.Dispose();
            Assert.IsFalse(service.BlockDefs.IsCreated); Assert.IsFalse(service.Orientations.IsCreated);
            service.Dispose();   // safe twice
            Assert.Throws<System.ObjectDisposedException>(() => service.CreateSingletons(default));
        }

        [Test]
        public void DatabaseHash_CombinesBlobAndCatalog()
        {
            var defs = Valid();
            using var a = ContentService.CreateForTests(defs);
            using var b = ContentService.CreateForTests(defs);
            Assert.AreEqual(StableHash.Combine(a.BlockDefs.Value.BlockDefsHash, a.Catalog.IdentityHash), a.DatabaseHash);
            Assert.AreEqual(a.DatabaseHash, b.DatabaseHash);
            Assert.AreEqual(0, a.BootReport.ErrorCount);
            Assert.Greater(a.PropMeshes.Count, 0, "prop meshes are exposed");
        }
    }
}
