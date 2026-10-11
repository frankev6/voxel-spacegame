using Game.App.Boot;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Simulation.Flight;
using Game.Tests.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-003 config defaults, boot singleton and specified field failures (SPEC-20 §4.4, §7).</summary>
    public sealed class FlightConfigTests
    {
        const string AssetPath = "Assets/Game/Content/Config/config_flight.asset";

        [Test]
        public void DefaultAsset_EqualsDefault()
        {
            var definition = AssetDatabase.LoadAssetAtPath<FlightConfigDefinition>(AssetPath);
            Assert.IsNotNull(definition);
            Assert.AreEqual("config_flight", definition.IdString);
            Assert.AreEqual(FlightConfig.Default, FlightConfigFactory.Create(definition));
            var report = new ValidationReport("FLT-003");
            definition.Validate(report.For(AssetPath), null);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());

            using var content = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var world = EcsTestWorld.Create();
            content.CreateSingletons(world.EntityManager);
            BootSingletons.CreateAll(world.EntityManager, content.Catalog);
            Assert.AreEqual(FlightConfig.Default, world.GetSingleton<FlightConfig>());
            using var configs = world.EntityManager.CreateEntityQuery(typeof(FlightConfig), typeof(BootSingleton));
            Assert.AreEqual(1, configs.CalculateEntityCount());
        }

        [Test]
        public void Validate_E001()
        {
            Invalid("maxForwardSpeed", 0);
            Invalid("allocationSweeps", 0);
        }

        static void Invalid(string field, int value)
        {
            var definition = ScriptableObject.CreateInstance<FlightConfigDefinition>();
            try
            {
                using var serialized = new SerializedObject(definition);
                var property = serialized.FindProperty(field);
                if (property.propertyType == SerializedPropertyType.Integer) property.intValue = value;
                else property.floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var report = new ValidationReport("FLT-003");
                definition.Validate(report.For(AssetPath), null);
                Assert.AreEqual(1, report.ErrorCount, report.ToText());
                Assert.AreEqual("FLT-E001", report.Entries[0].Code);
                Assert.AreEqual(field, report.Entries[0].Field);
                Assert.AreEqual(AssetPath, report.Entries[0].AssetPath);
            }
            finally { Object.DestroyImmediate(definition); }
        }
    }
}
