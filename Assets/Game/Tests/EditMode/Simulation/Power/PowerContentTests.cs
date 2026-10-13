using System.Collections.Generic;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>PWR-003: tuning validation, immutable baking, singleton lifecycle and combat profiles (SPEC-22 §4.5–4.6).</summary>
    public sealed class PowerContentTests
    {
        NativeLeakDetectionMode previousMode;
        [SetUp] public void SetUp()
        { previousMode = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace; }
        [TearDown] public void TearDown()
        { NativeLeakDetection.Mode = previousMode; DestroyAll(); }

        [Test]
        public void NoTuning_Throws()
        {
            var error = Assert.Throws<ContentValidationException>(() => new PowerContent(Catalog()));
            StringAssert.Contains("PWR-E001", error.Message);
            Assert.AreEqual(1, error.Report.ErrorCount);
            Assert.AreEqual("PWR-E001", error.Report.Entries[0].Code);
        }

        [Test]
        public void TwoTunings_Throws()
        {
            var a = Create<PowerTuningDefinition>("tuning_a");
            var b = Create<PowerTuningDefinition>("tuning_b");
            var error = Assert.Throws<ContentValidationException>(() => new PowerContent(Catalog(a, b)));
            StringAssert.Contains("PWR-E001", error.Message);
            Assert.AreEqual(1, error.Report.ErrorCount);
            Assert.AreEqual("PWR-E001", error.Report.Entries[0].Code);
        }

        [Test]
        public void CreateSingletons_Idempotent()
        {
            var definition = Create<PowerTuningDefinition>("tuning_test", so =>
            {
                Set(so, "priorityShieldEmitter", 10); Set(so, "priorityThruster", 20);
                Set(so, "priorityAbility", 30); Set(so, "priorityWeapon", 40);
                Set(so, "brownoutHoldTicks", 19); Set(so, "shieldPaddingScale", 1.5f);
                Set(so, "shieldPaddingMetres", 4f); Set(so, "shieldMinRadiusMetres", 2.25f);
                Set(so, "shieldRestartFraction", .6f); Set(so, "sustainWarningSeconds", 45f);
            });
            var catalog = Catalog(definition);
            var content = new PowerContent(catalog);
            var expected = new PowerTuning
            {
                PriorityShieldEmitter = 10, PriorityThruster = 20, PriorityAbility = 30, PriorityWeapon = 40,
                BrownoutHoldTicks = 19, ShieldPaddingScale = 1.5f, ShieldPaddingMetres = 4f,
                ShieldMinRadiusMetres = 2.25f, ShieldRestartFraction = .6f, SustainWarningSeconds = 45f,
            };
            Assert.AreEqual(expected, content.Tuning);
            using var world = new World("PowerContent");
            using var otherWorld = new World("PowerContentOther");
            var em = world.EntityManager;
            var otherEm = otherWorld.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PowerTuning>());
            using var otherQuery = otherEm.CreateEntityQuery(ComponentType.ReadOnly<PowerTuning>());
            content.CreateSingletons(em); content.CreateSingletons(em);
            Assert.AreEqual(1, query.CalculateEntityCount());
            Assert.AreEqual("PowerSingletons", em.GetName(query.GetSingletonEntity()).ToString());
            Assert.AreEqual(expected, query.GetSingleton<PowerTuning>());
            Assert.IsFalse(em.HasComponent<ShieldHitStreams>(query.GetSingletonEntity()), "ShieldSystem owns its channel");
            content.CreateSingletons(otherEm);
            Assert.AreEqual(1, otherQuery.CalculateEntityCount());
            content.DestroySingletons(em); content.DestroySingletons(em);
            Assert.AreEqual(0, query.CalculateEntityCount());
            Assert.AreEqual(1, otherQuery.CalculateEntityCount(), "singleton lifetime is scoped to the supplied world");
            content.CreateSingletons(em);
            Assert.AreEqual(1, query.CalculateEntityCount());
            Assert.AreEqual(expected, query.GetSingleton<PowerTuning>());

            (string Field, object Value)[] invalid =
            {
                ("brownoutHoldTicks", 0), ("shieldPaddingScale", .99f), ("shieldPaddingMetres", -.01f),
                ("shieldMinRadiusMetres", .49f), ("shieldRestartFraction", -.01f), ("shieldRestartFraction", 1.01f),
                ("sustainWarningSeconds", .99f),
            };
            foreach (var trial in invalid) AssertInvalid(definition, catalog, trial.Field, trial.Value);
            string[] floats = { "shieldPaddingScale", "shieldPaddingMetres", "shieldMinRadiusMetres", "shieldRestartFraction", "sustainWarningSeconds" };
            foreach (string field in floats)
            {
                AssertInvalid(definition, catalog, field, float.NaN);
                AssertInvalid(definition, catalog, field, float.PositiveInfinity);
            }
            Edit(definition, so =>
            {
                Set(so, "brownoutHoldTicks", 1); Set(so, "shieldPaddingScale", 1f); Set(so, "shieldPaddingMetres", 0f);
                Set(so, "shieldMinRadiusMetres", .5f); Set(so, "shieldRestartFraction", 0f); Set(so, "sustainWarningSeconds", 1f);
            });
            Assert.IsFalse(Validate(definition).HasErrors, "attribute lower bounds are inclusive");
            Edit(definition, so => Set(so, "shieldRestartFraction", 1f));
            Assert.IsFalse(Validate(definition).HasErrors, "restart upper bound is inclusive");
            Assert.AreEqual(expected, content.Tuning, "the baked tuning does not follow later authoring edits");
            Assert.AreEqual(expected, query.GetSingleton<PowerTuning>());
        }

        [Test]
        public void GridSetup_RegistersForCombatProfiles()
        {
            ComponentType[] expected =
            {
                ComponentType.ReadWrite<ShipPower>(), ComponentType.ReadWrite<PowerConsumer>(),
                ComponentType.ReadWrite<ShipStats>(), ComponentType.ReadWrite<ShipStatMultipliers>(),
                ComponentType.ReadWrite<PowerStats>(), ComponentType.ReadWrite<ShieldStats>(),
                ComponentType.ReadWrite<ShipStatModifier>(), ComponentType.ReadWrite<ShieldState>(),
                ComponentType.ReadWrite<ShieldRipple>(), ComponentType.ReadWrite<ShieldControl>(),
            };
            var rootTypes = PowerGridSetup.RootTypes;
            Assert.AreEqual(10, rootTypes.Length);
            var actual = new List<ComponentType>();
            for (int i = 0; i < rootTypes.Length; i++) actual.Add(rootTypes.GetComponentType(i));
            CollectionAssert.AreEquivalent(expected, actual);

            using var world = new World("PowerGridSetup");
            var em = world.EntityManager;
            var registrations = em.AddBuffer<RootComponentRegistration>(em.CreateEntity());
            var existing = new RootComponentRegistration { Profile = GridProfile.PlayerShip, Type = ComponentType.ReadWrite<ShipCommand>() };
            registrations.Add(existing);
            PowerGridSetup.Register(registrations);
            Assert.AreEqual(51, registrations.Length);
            Assert.AreEqual(existing.Profile, registrations[0].Profile);
            Assert.AreEqual(existing.Type, registrations[0].Type);
            var added = new List<RootComponentRegistration>();
            for (int i = 1; i < registrations.Length; i++) added.Add(registrations[i]);
            Assert.IsFalse(added.Any(r => r.Profile == GridProfile.None));
            GridProfile[] profiles = { GridProfile.PlayerShip, GridProfile.NpcShip, GridProfile.Station, GridProfile.SmallCraft, GridProfile.Dummy };
            foreach (var profile in profiles)
                CollectionAssert.AreEquivalent(expected, added.Where(r => r.Profile == profile).Select(r => r.Type).ToArray(), profile.ToString());
        }

        static ContentCatalog Catalog(params ContentDefinition[] definitions)
        {
            var all = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing") };
            all.AddRange(definitions);
            var report = new ValidationReport("PowerContentTests");
            var catalog = ContentCatalog.Build(all, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            return catalog;
        }

        static void AssertInvalid(PowerTuningDefinition definition, ContentCatalog catalog, string field, object value)
        {
            string property = char.ToUpperInvariant(field[0]) + field.Substring(1);
            object original = typeof(PowerTuningDefinition).GetProperty(property).GetValue(definition);
            Edit(definition, so => Set(so, field, value));
            try
            {
                var report = Validate(definition);
                Assert.AreEqual(1, report.ErrorCount, report.ToText());
                Assert.AreEqual("PWR-E002", report.Entries[0].Code);
                Assert.AreEqual(field, report.Entries[0].Field);
                Assert.AreEqual("test:tuning_test", report.Entries[0].AssetPath);
                var error = Assert.Throws<ContentValidationException>(() => new PowerContent(catalog));
                StringAssert.Contains("PWR-E002", error.Message);
            }
            finally { Edit(definition, so => Set(so, field, original)); }
        }
    }
}
