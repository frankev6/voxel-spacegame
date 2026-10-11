using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-011: registry handles borrow storage and spawn hooks retain registration order.</summary>
    public sealed class StructureRegistryTests
    {
        sealed class Hook : IGridSpawnHook
        {
            public void OnEntitiesCreated(EntityManager em, Entity root, NativeArray<Entity> created) { }
        }
        [Test]
        public void Handles_UniqueNeverReused()
        {
            using var fixture = new VoxelTestContent();
            using var library = new BlueprintLibrary(fixture.Service.Catalog, fixture.Content);
            using var first = new VoxelStructure(); using var second = new VoxelStructure();
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                Assert.AreEqual(HideFlags.DontSave, registry.hideFlags);
                int a = registry.Register(first), b = registry.Register(second);
                Assert.GreaterOrEqual(a, 0); Assert.Greater(b, a);
                Assert.AreSame(first, registry.Get(a)); Assert.AreSame(second, registry.Get(b));
                registry.Release(a); registry.Release(a);
                Assert.IsNull(registry.Get(a)); Assert.IsTrue(first.Data.IsCreated);
                int c = registry.Register(first); Assert.Greater(c, b); Assert.AreSame(first, registry.Get(c));
                Assert.IsNull(registry.Get(-1)); Assert.IsNull(registry.Get(int.MaxValue));
                registry.Release(-1); registry.Release(int.MaxValue);
            }
            finally { Object.DestroyImmediate(registry); }
            Assert.IsTrue(first.Data.IsCreated); Assert.IsTrue(second.Data.IsCreated);
        }
        [Test]
        public void Hooks_AddRemove()
        {
            using var fixture = new VoxelTestContent();
            using var library = new BlueprintLibrary(fixture.Service.Catalog, fixture.Content);
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                var first = new Hook(); var second = new Hook(); var view = registry.SpawnHooks;
                registry.AddSpawnHook(first); registry.AddSpawnHook(second); registry.AddSpawnHook(first);
                CollectionAssert.AreEqual(new[] { first, second }, view);
                registry.RemoveSpawnHook(first); registry.RemoveSpawnHook(first);
                CollectionAssert.AreEqual(new[] { second }, view);
                registry.RemoveSpawnHook(second); Assert.AreEqual(0, view.Count);
            }
            finally { Object.DestroyImmediate(registry); }
        }
    }
}
