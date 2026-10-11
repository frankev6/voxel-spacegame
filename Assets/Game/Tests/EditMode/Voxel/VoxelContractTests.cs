using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-001 contract checks (SPEC-10 §9).</summary>
    public class VoxelContractTests
    {
        static readonly Assembly VoxelAssembly = typeof(Cell).Assembly;

        // Module singletons allowed to hold native containers (SPEC-10 decision 9). SPEC-11/12 queue and cache singletons and SPEC-63's
        // FxEventQueue are added here by REN-001/PHY-001/VFX-001; SPEC-64's SfxEventQueue lives in Game.Voxel too (AUD-001).
        static readonly HashSet<Type> ContainerSingletons = new HashSet<Type>
        { typeof(DeferredDisposalQueue), typeof(GridPrototypes), typeof(Game.Voxel.PresentationStreams.SfxEventQueue),
          typeof(VoxelRenderConfig), typeof(VoxelRenderRuntime), typeof(BrickMeshQueue), typeof(BrickMeshBatch), typeof(ColliderDirtyQueue), typeof(ColliderBuildCache), typeof(ColliderBatchQueue) };

        [Test]
        public void StructSizes_MatchSpec()
        {
            Assert.AreEqual(4, UnsafeUtility.SizeOf<Cell>(), "Cell");
            Assert.AreEqual(4, UnsafeUtility.SizeOf<PaletteEntry>(), "PaletteEntry");
            Assert.AreEqual(32, UnsafeUtility.SizeOf<BlockInstance>(), "BlockInstance");
            Assert.AreEqual(28, UnsafeUtility.SizeOf<Brick>(), "Brick");
            Assert.AreEqual(512, UnsafeUtility.SizeOf<BrickMaskBits>(), "BrickMaskBits");
            Assert.AreEqual(28, UnsafeUtility.SizeOf<BrickRecord>(), "BrickRecord (SPEC-10 open question 13: the fields of §4.3 total 28 bytes)");
        }

        [Test]
        public void GridKind_HasBindingValues()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(GridKind)));
            CollectionAssert.AreEqual(new[] { GridKind.Ship, GridKind.Station, GridKind.Asteroid, GridKind.Hulk },
                                      Enum.GetValues(typeof(GridKind)).Cast<GridKind>().ToArray());
            Assert.AreEqual(0, (byte)GridKind.Ship);
            Assert.AreEqual(1, (byte)GridKind.Station);
            Assert.AreEqual(2, (byte)GridKind.Asteroid);
            Assert.AreEqual(3, (byte)GridKind.Hulk);
        }

        [Test]
        public void Buffers_HaveCapacityZero()
        {
            foreach (var t in new[] { typeof(BrickCells), typeof(BrickSolidMask), typeof(BlockInstance), typeof(BrickSlot), typeof(GridEditedBrick) })
            {
                var attr = t.GetCustomAttribute<InternalBufferCapacityAttribute>();
                Assert.IsNotNull(attr, $"{t.Name} has no [InternalBufferCapacity]");
                Assert.AreEqual(0, attr.Capacity, t.Name);
            }
        }

        [Test]
        public void Components_AreUnmanagedWithoutContainers()
        {
            var ecsTypes = VoxelAssembly.GetTypes()
                .Where(t => t.IsValueType && (typeof(IComponentData).IsAssignableFrom(t) || typeof(IBufferElementData).IsAssignableFrom(t)))
                .ToList();
            Assert.Greater(ecsTypes.Count, 20, "expected the SPEC-10 §4 components");
            foreach (var t in ecsTypes)
            {
                Assert.IsTrue(UnsafeUtility.IsUnmanaged(t), $"{t.Name} contains a managed type");
                bool hasContainer = ContainsNativeContainer(t, new HashSet<Type>());
                if (ContainerSingletons.Contains(t)) continue;
                Assert.IsFalse(hasContainer, $"{t.Name} holds a native container but is not an allowed module singleton");
            }
            foreach (var t in ContainerSingletons)
                Assert.IsTrue(ContainsNativeContainer(t, new HashSet<Type>()), $"{t.Name} is allow-listed but holds no container");
        }

        static bool ContainsNativeContainer(Type t, HashSet<Type> seen)
        {
            if (t.IsPrimitive || t.IsEnum || t.IsPointer || !seen.Add(t)) return false;
            if (t.GetCustomAttribute<NativeContainerAttribute>() != null) return true;
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (f.FieldType.IsValueType && ContainsNativeContainer(f.FieldType, seen)) return true;
            return false;
        }
    }
}
