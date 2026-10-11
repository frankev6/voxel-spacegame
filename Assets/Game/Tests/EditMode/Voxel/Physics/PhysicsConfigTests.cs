using Game.App.Boot;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Tests.Common;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-003 default content, boot values and the two specified validation failures.</summary>
    public sealed class PhysicsConfigTests
    {
        const string AssetPath = "Assets/Game/Content/Config/config_physics.asset";
        [Test]
        public void DefaultAsset_EqualsDefault()
        {
            var definition = AssetDatabase.LoadAssetAtPath<PhysicsVoxelConfigDefinition>(AssetPath);
            Assert.IsNotNull(definition);
            Assert.AreEqual("config_physics", definition.IdString);
            Assert.AreEqual(PhysicsVoxelConfig.Default, PhysicsVoxelConfigFactory.Create(definition));
            var report = new ValidationReport("PHY-003");
            definition.Validate(report.For(AssetPath), null);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }
        [Test]
        public void PhysicsStep_ZeroGravity_SynchronizedWorld()
        {
            using var content = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var world = EcsTestWorld.Create();
            content.CreateSingletons(world.EntityManager);
            BootSingletons.CreateAll(world.EntityManager, content.Catalog);
            var definition = content.Catalog.Get<PhysicsVoxelConfigDefinition>(new ContentId("config_physics"));
            var step = world.GetSingleton<PhysicsStep>();
            Assert.AreEqual(PhysicsVoxelConfigFactory.CreatePhysicsStep(definition), step);
            Assert.AreEqual(PhysicsVoxelConfig.Default, world.GetSingleton<PhysicsVoxelConfig>());
            Assert.AreEqual(SimulationType.UnityPhysics, step.SimulationType);
            Assert.AreEqual(float3.zero, step.Gravity); Assert.AreEqual(1, step.SynchronizeCollisionWorld);
            Assert.AreEqual(4, step.SolverIterationCount); Assert.AreEqual(1, step.MultiThreaded);
            Assert.AreEqual(0.1f, step.CollisionTolerance);
            Assert.IsFalse(step.SolverStabilizationHeuristicSettings.EnableSolverStabilization);
            Assert.IsFalse(step.IncrementalDynamicBroadphase); Assert.IsTrue(step.IncrementalStaticBroadphase);
            using var configs = world.EntityManager.CreateEntityQuery(typeof(PhysicsVoxelConfig), typeof(BootSingleton));
            using var steps = world.EntityManager.CreateEntityQuery(typeof(PhysicsStep), typeof(BootSingleton));
            Assert.AreEqual(1, configs.CalculateEntityCount()); Assert.AreEqual(1, steps.CalculateEntityCount());
        }
        static void Invalid(string field, float value, string code)
        {
            var definition = ScriptableObject.CreateInstance<PhysicsVoxelConfigDefinition>();
            try
            {
                using var serialized = new SerializedObject(definition);
                var property = serialized.FindProperty(field);
                if (property.propertyType == SerializedPropertyType.Integer) property.intValue = (int)value;
                else property.floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var report = new ValidationReport("PHY-003");
                definition.Validate(report.For(AssetPath), null);
                Assert.AreEqual(1, report.ErrorCount, report.ToText());
                Assert.AreEqual(code, report.Entries[0].Code);
                Assert.AreEqual(field, report.Entries[0].Field);
                Assert.AreEqual(AssetPath, report.Entries[0].AssetPath);
            }
            finally { Object.DestroyImmediate(definition); }
        }
        [Test] public void Validate_DelayTooShort_E001() => Invalid("installDelayTicks", 6, "PHY-E001");
        [Test] public void Validate_BevelTooLarge_E002() => Invalid("bevelFraction", 0.26f, "PHY-E002");
    }
}
