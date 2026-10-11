using System;
using System.Linq;
using System.Reflection;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Game.Tests.EditMode.Voxel.Damage
{
    /// <summary>DMG-001: exact shared enum, unmanaged components, zero-capacity buffers and table-backed defaults.</summary>
    public sealed class DamageContractTests
    {
        [Test]
        public void DamageType_HasBindingValues()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(DamageType)));
            CollectionAssert.AreEqual(new[] { "Kinetic", "Energy", "Explosive", "Ram" }, Enum.GetNames(typeof(DamageType)));
            for (int i = 0; i < 4; i++) Assert.AreEqual(i, (byte)(DamageType)Enum.Parse(typeof(DamageType), Enum.GetNames(typeof(DamageType))[i]));
        }
        [Test]
        public void Components_AreUnmanaged()
        {
            var types = typeof(DamageEvent).Assembly.GetTypes().Where(t => t.Namespace == "Game.Voxel.Damage" && t.IsPublic && t.IsValueType).ToArray();
            Assert.GreaterOrEqual(types.Length, 30);
            foreach (var type in types) Assert.IsTrue(UnsafeUtility.IsUnmanaged(type), type.Name);
            CollectionAssert.AreEquivalent(new[] { ComponentType.ReadWrite<GridDamageStats>(), ComponentType.ReadWrite<PendingImpulse>(),
                ComponentType.ReadWrite<GridIntegrityState>(), ComponentType.ReadWrite<IntegritySeed>() }, DamageSetup.GridRootComponents());
            CollectionAssert.AreEqual(new[] { ComponentType.ReadWrite<BrickIntegrity>() }, DamageSetup.BrickComponents());
        }
        [Test]
        public void Buffers_HaveZeroInternalCapacity()
        {
            foreach (var type in new[] { typeof(IntegritySeed), typeof(ChunkCell) })
            {
                Assert.IsTrue(typeof(IBufferElementData).IsAssignableFrom(type));
                Assert.AreEqual(0, type.GetCustomAttribute<InternalBufferCapacityAttribute>().Capacity, type.Name);
            }
        }
        [Test]
        public void Config_DefaultMultipliers()
        {
            var config = DamageConfig.Default;
            Assert.IsFalse(config.Table.IsCreated); Assert.AreEqual(1f, config.Multiplier(DamageType.Explosive, 3));
            Assert.AreEqual(256, config.MaxDdaCells); Assert.AreEqual(20000, config.MinRamEnergyJ);
            Assert.AreEqual(131072, config.IntegrityNodeBudgetPerGrid); Assert.AreEqual(64, config.MaxLiveChunks);
            Assert.AreEqual(16384, config.SplitCellBudgetPerTick); Assert.AreEqual(12000, config.DissolveParticleCap);
            Assert.IsTrue(config.CueRamImpact.IsNone && config.CueCellBreak.IsNone && config.CueSplit.IsNone && config.CueCoreLost.IsNone);
            var definition = ScriptableObject.CreateInstance<DamageConfigDefinition>();
            try
            {
                foreach (var field in typeof(DamageConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.FieldType != typeof(int) && field.FieldType != typeof(float)) continue;
                    Assert.AreEqual(field.GetValue(config), typeof(DamageConfigDefinition).GetProperty(field.Name).GetValue(definition), field.Name);
                }
                Assert.IsNull(definition.RamImpactCue); Assert.IsNull(definition.CellBreakCue); Assert.IsNull(definition.SplitCue); Assert.IsNull(definition.CoreLostCue);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
            float[] expected = { 1, 0.6f, 0.8f, 1.5f, 1, 0.8f, 0.6f, 1.25f, 1, 0.7f, 0.7f, 2, 1, 0.5f, 0.75f, 2 };
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var table = ref builder.ConstructRoot<DamageTableBlob>(); table.ArmorClassCount = 4;
            var types = builder.Allocate(ref table.Types, 4); var multipliers = builder.Allocate(ref table.ArmorMultipliers, 16);
            for (int i = 0; i < 4; i++) types[i] = new DamageTypeRow { ArmorOffset = i * 4 };
            for (int i = 0; i < 16; i++) multipliers[i] = expected[i];
            var blob = builder.CreateBlobAssetReference<DamageTableBlob>(Allocator.Persistent);
            try
            {
                config.Table = blob;
                for (int type = 0; type < 4; type++) for (byte armor = 0; armor < 4; armor++)
                    Assert.AreEqual(expected[type * 4 + armor], config.Multiplier((DamageType)type, armor));
                Assert.AreEqual(2f, config.Multiplier(DamageType.Explosive, 3));
                Assert.AreEqual(1f, config.Multiplier(DamageType.Explosive, 7));
                Assert.AreEqual(1f, config.Multiplier((DamageType)255, 3));
            }
            finally { blob.Dispose(); }
        }
    }
}
