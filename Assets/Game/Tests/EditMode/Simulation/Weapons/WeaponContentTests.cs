using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation;
using Game.Simulation.Weapons;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>SPEC-21 WPN-002: shipped table, sentinel, diagnostic contract and boot content.</summary>
    public sealed class WeaponContentTests
    {
        readonly List<Object> copies = new();
        const string DatabasePath = "Assets/Game/Content/ContentDatabase.asset";

        [TearDown]
        public void TearDown()
        {
            foreach (var copy in copies) Object.DestroyImmediate(copy);
            copies.Clear(); TestContentFactory.DestroyAll();
        }

        [Test]
        public void Bake_Table49_Values()
        {
            var catalog = Catalog();
            var definitions = catalog.All<WeaponDefinition>();
            Assert.That(definitions.Count, Is.EqualTo(10));
            using (var blob = WeaponContent.BuildBlob(catalog))
            {
                Assert.That(blob.Value.Weapons.Length, Is.EqualTo(11));
                for (int i = 0; i < definitions.Count; i++)
                {
                    var d = definitions[i];
                    var report = new ValidationReport("table");
                    string path = AssetDatabase.GetAssetPath(d);
                    d.Validate(report.For(path), new ContentValidationContext(path, false, x => catalog.TryGet(x.Id, out var found) && ReferenceEquals(x, found), catalog));
                    Assert.That(report.HasErrors, Is.False, report.ToText());
                    var expected = TableRow(d.IdString);
                    expected.Index = (ushort)(i + 1); expected.IdHash = d.Id.Hash;
                    expected.FireCue = d.FireCue != null ? d.FireCue.Id.Hash : default;
                    expected.MuzzleFx = d.MuzzleFx != null ? d.MuzzleFx.Id.Hash : default;
                    expected.HitFx = d.HitFx != null ? d.HitFx.Id.Hash : default;
                    Color32 color = d.TracerColor;
                    expected.TracerColor = (uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24);
                    AssertRow(expected, blob.Value.Weapons[i + 1], d.IdString);
                    Assert.That(d.SustainedDps, Is.EqualTo(expected.SustainedDps).Within(.5f), d.IdString);
                    Assert.That(WeaponContent.ComputeSustainedDps(blob.Value.Weapons[i + 1]), Is.EqualTo(expected.SustainedDps).Within(.5f), d.IdString);
                }
            }
            // Authored fractional lifetime controls Range; life and pulse tick counts round independently.
            var fractional = Copy(catalog.Get<WeaponDefinition>(new ContentId("wpn_plasma_turret_small")));
            var muzzle = TestContentFactory.Create<FxDefinition>("fx_weapon_test_muzzle");
            var hit = TestContentFactory.Create<FxDefinition>("fx_weapon_test_hit");
            TestContentFactory.Edit(fractional, so =>
            {
                TestContentFactory.Set(so, "lifetimeMin", .025f); TestContentFactory.Set(so, "lifetimeMax", 1.509f);
                TestContentFactory.Set(so, "beamPulseSeconds", .025f);
                TestContentFactory.Set(so, "muzzleFx", muzzle); TestContentFactory.Set(so, "hitFx", hit);
                so.FindProperty("tracerColor").colorValue = new Color32(17, 34, 51, 68);
            });
            var replaced = DatabaseDefinitions().Where(x => x.IdString != fractional.IdString).ToList();
            replaced.Add(fractional); replaced.Add(muzzle); replaced.Add(hit);
            using (var blob = WeaponContent.BuildBlob(BuildCatalog(replaced)))
            {
                ref var row = ref blob.Value.Weapons[catalog.IndexOf<WeaponDefinition>(catalog.Get<WeaponDefinition>(fractional.Id))];
                Assert.That(row.Range, Is.EqualTo(400 * 1.509f).Within(.001f));
                Assert.That(row.LifetimeMinTicks, Is.EqualTo(2u)); Assert.That(row.LifetimeMaxTicks, Is.EqualTo(91u));
                Assert.That(row.BeamPulseTicks, Is.EqualTo(2u)); Assert.That(row.TracerColor, Is.EqualTo(0x44332211u));
                Assert.That(row.MuzzleFx, Is.EqualTo(muzzle.Id.Hash)); Assert.That(row.HitFx, Is.EqualTo(hit.Id.Hash));
            }
            AssertFactory(catalog, false);
            AssertFactory(catalog, true);
        }

        [Test]
        public unsafe void Entry0_IsNone()
        {
            var catalog = Catalog();
            foreach (var input in new[] { catalog, BuildCatalog(DatabaseDefinitions().Where(x => !(x is WeaponDefinition)).ToList()) })
            {
                using var blob = WeaponContent.BuildBlob(input);
                ref var none = ref blob.Value.Weapons[0];
                byte* bytes = (byte*)UnsafeUtility.AddressOf(ref none);
                for (int i = 0; i < UnsafeUtility.SizeOf<WeaponDef>(); i++) Assert.That(bytes[i], Is.Zero, "sentinel byte " + i);
                Assert.That(WeaponContent.ComputeSustainedDps(none), Is.Zero);
                Assert.That(blob.Value.Weapons.Length, Is.EqualTo(input.All<WeaponDefinition>().Count + 1));
            }
        }

        [Test]
        public void Validation_Codes()
        {
            var projectile = Catalog().Get<WeaponDefinition>(new ContentId("wpn_plasma_turret_small"));
            CheckInvalid(projectile, "fireRate", 0f, "WPN-E001");
            CheckInvalid(projectile, "fireRate", float.NaN, "WPN-E001");
            CheckInvalid(projectile, "burstCount", 0, "WPN-E001");
            CheckInvalid(projectile, "pelletsPerRound", 33, "WPN-E001");
            CheckInvalid(projectile, "defaultFireGroups", 8, "WPN-E001");
            CheckInvalid(projectile, "projectileSpeed", 0f, "WPN-E002");
            CheckInvalid(projectile, "projectileSpeed", float.PositiveInfinity, "WPN-E002");
            var beam = Catalog().Get<WeaponDefinition>(new ContentId("wpn_small_beam_turret"));
            CheckInvalid(beam, "beamRange", 0f, "WPN-E003");
            CheckInvalid(beam, "beamPulseSeconds", 0f, "WPN-E003");
            CheckInvalid(projectile, "explosionRadius", 6.01f, "WPN-E004");
            CheckInvalid(projectile, "explosionRadius", float.PositiveInfinity, "WPN-E004");
            CheckInvalid(projectile, "lifetimeMax", 1f, "WPN-E005");
            CheckInvalid(projectile, "lifetimeMin", float.NaN, "WPN-E005");
            CheckInvalid(projectile, "damage", 0f, "WPN-E006");
            CheckInvalid(projectile, "damage", float.NaN, "WPN-E006");
            CheckInvalid(projectile, "penetrationCells", 0f, "WPN-E006");
            var tuning = Catalog().Get<WeaponTuningDefinition>(new ContentId("config_weapons"));
            CheckInvalid(tuning, "maxProjectiles", 255, "WPN-E007");
            CheckInvalid(tuning, "maxCandidates", 3, "WPN-E007");
            CheckInvalid(tuning, "fixedConvergenceDeg", 31f, "WPN-E007");
            CheckInvalid(tuning, "explosionImpulsePerDamage", -1f, "WPN-E007");
            CheckInvalid(tuning, "maxTargetRange", 0f, "WPN-E007");
            CheckInvalid(tuning, "underCursorRadius", float.NaN, "WPN-E007");
            var edge = Copy(tuning);
            TestContentFactory.Edit(edge, so => { TestContentFactory.Set(so, "maxProjectiles", 256); TestContentFactory.Set(so, "maxCandidates", 4); });
            Assert.That(TestContentFactory.Validate(edge).HasErrors, Is.False);
            var optional = Copy(projectile);
            TestContentFactory.Edit(optional, so => { TestContentFactory.Set(so, "fireCue", null); TestContentFactory.Set(so, "defaultFireGroups", 0); });
            Assert.That(TestContentFactory.Validate(optional).HasErrors, Is.False);
            string path = AssetDatabase.GetAssetPath(projectile);
            var report = new ValidationReport("reference");
            projectile.Validate(report.For(path), new ContentValidationContext(path, false, _ => false));
            AssertDiagnostic(report, path, "fireCue", "DATA-E010");
        }

        [Test]
        public void RamDamageType_Rejected()
        {
            Assert.That(typeof(WeaponDefinition).GetProperty(nameof(WeaponDefinition.DamageType)).PropertyType, Is.EqualTo(typeof(DamageType)));
            var source = Catalog().Get<WeaponDefinition>(new ContentId("wpn_plasma_turret_small"));
            CheckInvalid(source, "damageType", (int)DamageType.Ram, "WPN-E008");
        }

        T Copy<T>(T definition) where T : ContentDefinition
        { var copy = Object.Instantiate(definition); copies.Add(copy); return copy; }

        void CheckInvalid<T>(T source, string field, object value, string code) where T : ContentDefinition
        {
            var copy = Copy(source);
            TestContentFactory.Edit(copy, so => TestContentFactory.Set(so, field, value));
            string path = AssetDatabase.GetAssetPath(source);
            var report = new ValidationReport("invalid");
            copy.Validate(report.For(path), new ContentValidationContext(path, true, _ => true));
            AssertDiagnostic(report, path, field, code);
        }

        static void AssertDiagnostic(ValidationReport report, string path, string field, string code)
        {
            Assert.That(report.Entries.Any(x => x.Code == code && x.AssetPath == path && x.Field == field && x.Severity == ValidationSeverity.Error), Is.True, report.ToText());
        }

        static IReadOnlyList<ContentDefinition> DatabaseDefinitions()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            Assert.That(database, Is.Not.Null);
            return database.Definitions;
        }

        // Deliberately reverse database order; the bake must use stable catalog indices.
        static ContentCatalog Catalog() => BuildCatalog(DatabaseDefinitions().Reverse().ToList());

        static ContentCatalog BuildCatalog(IReadOnlyList<ContentDefinition> definitions)
        {
            var report = new ValidationReport("catalog");
            var catalog = ContentCatalog.Build(definitions, report);
            Assert.That(report.HasErrors, Is.False, report.ToText()); return catalog;
        }

        static void AssertRow(in WeaponDef expected, in WeaponDef actual, string id)
        {
            foreach (var field in typeof(WeaponDef).GetFields())
            {
                var value = field.GetValue(actual); var wanted = field.GetValue(expected);
                if (value is float number) Assert.That(number, Is.EqualTo((float)wanted).Within(.001f), id + "." + field.Name);
                else Assert.That(value, Is.EqualTo(wanted), id + "." + field.Name);
            }
        }

        static void AssertFactory(ContentCatalog catalog, bool hasFactionOwner)
        {
            using var world = new World("WPN-002 content");
            var em = world.EntityManager; var created = new List<Entity>();
            FactionRelations external = default;
            if (hasFactionOwner)
            {
                external = FactionRelations.CreateDefault(Allocator.Persistent);
                external.Stance[1] = (byte)FactionStance.Friendly;
                em.CreateSingleton(external);
            }
            try
            {
                WeaponContent.CreateSingletons(em, catalog, created);
                Assert.That(created.Count, Is.EqualTo(hasFactionOwner ? 2 : 3));
                using var weapons = em.CreateEntityQuery(ComponentType.ReadOnly<WeaponDefsRef>());
                using var tuning = em.CreateEntityQuery(ComponentType.ReadOnly<WeaponTuning>());
                using var factions = em.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>());
                Assert.That(weapons.CalculateEntityCount(), Is.EqualTo(1)); Assert.That(tuning.CalculateEntityCount(), Is.EqualTo(1));
                Assert.That(factions.CalculateEntityCount(), Is.EqualTo(1));
                var value = tuning.GetSingleton<WeaponTuning>();
                Assert.That(value.MaxProjectiles, Is.EqualTo(8192)); Assert.That(value.MaxCandidates, Is.EqualTo(32));
                Assert.That(value.FixedConvergenceRad, Is.EqualTo(math.radians(3f)).Within(.0001f));
                Assert.That(value.ExplosionImpulsePerDamage, Is.EqualTo(2)); Assert.That(value.MaxTargetRange, Is.EqualTo(2500));
                Assert.That(value.UnderCursorRadius, Is.EqualTo(30));
                var matrix = factions.GetSingleton<FactionRelations>();
                Assert.That(matrix.Count, Is.EqualTo(3));
                for (byte a = 0; a < 3; a++)
                    for (byte b = 0; b < 3; b++)
                    {
                        var expected = a == b ? FactionStance.Friendly : a < 2 && b < 2 ? FactionStance.Hostile : FactionStance.Neutral;
                        if (hasFactionOwner && a == 0 && b == 1) expected = FactionStance.Friendly;
                        Assert.That(matrix.Get(a, b), Is.EqualTo(expected));
                    }
                Assert.That(created.Contains(factions.GetSingletonEntity()), Is.EqualTo(!hasFactionOwner));
                Assert.Throws<System.InvalidOperationException>(() => WeaponContent.CreateSingletons(em, catalog, created));
                Assert.That(created.Count, Is.EqualTo(hasFactionOwner ? 2 : 3));
            }
            finally
            {
                em.CompleteAllTrackedJobs();
                foreach (var entity in created)
                {
                    if (em.HasComponent<WeaponDefsRef>(entity)) em.GetComponentData<WeaponDefsRef>(entity).Value.Dispose();
                    if (em.HasComponent<FactionRelations>(entity)) em.GetComponentData<FactionRelations>(entity).Dispose();
                    em.DestroyEntity(entity);
                }
                external.Dispose();
            }
        }

        // Independent SPEC-21 §4.9 oracle. Metadata hashes/colors come from direct authored references above.
        static WeaponDef TableRow(string id)
        {
            var w = new WeaponDef { Delivery = WeaponDelivery.Projectile, Type = DamageType.Kinetic, Visual = ProjectileVisual.StandardBullet,
                DefaultFireGroups = 1, Damage = 60, PenetrationCells = 1, TriggerIntervalTicks = 30, BurstCount = 1, PelletsPerRound = 1,
                SpreadRad = math.radians(2f), ProjectileSpeed = 400, ProjectileRadius = .1f, LifetimeMinTicks = 90, LifetimeMaxTicks = 90,
                BeamPulseTicks = 30, BeamWidth = .3f, TraverseHalfArcRad = math.radians(5f), TraverseRateRad = math.radians(360f),
                MaxElevationRad = math.radians(20f), FireConeRad = math.radians(3f), PowerDraw = 1.5f, Range = 600, SustainedDps = 120 };
            switch (id)
            {
                case "wpn_plasma_turret_small": break;
                case "wpn_xeno_acid":   // ADR-0115 spitter
                    w.Damage = 45; w.TriggerIntervalTicks = 40; w.SpreadRad = math.radians(3f); w.ProjectileSpeed = 340; w.Range = 510; w.SustainedDps = 67.5f; break;
                case "wpn_plasma_turret_small_fixed":
                    w.IsFixed = 1; w.TriggerIntervalTicks = 24; w.SpreadRad = math.radians(.5f); w.TraverseHalfArcRad = w.TraverseRateRad = 0; w.SustainedDps = 150; break;
                case "wpn_plasma_turret_medium":
                    w.Damage = 70; w.PenetrationCells = 2; w.TriggerIntervalTicks = 40; w.BurstCount = 3; w.BurstIntervalTicks = 3;
                    w.SpreadRad = math.radians(.5f); w.ProjectileSpeed = 450; w.LifetimeMinTicks = w.LifetimeMaxTicks = 96;
                    w.TraverseHalfArcRad = math.radians(90f); w.TraverseRateRad = math.radians(180f); w.PowerDraw = 4; w.Range = 720; w.SustainedDps = 315; break;
                case "wpn_small_beam_turret":
                case "wpn_fixed_beam_turret_small":
                case "wpn_ark_lance_medium":
                    w.Delivery = WeaponDelivery.Beam; w.Type = DamageType.Energy; w.Visual = ProjectileVisual.LaserBeam; w.DefaultFireGroups = 2;
                    w.SpreadRad = w.ProjectileSpeed = 0; w.LifetimeMinTicks = w.LifetimeMaxTicks = 1;
                    w.Damage = 150; w.TriggerIntervalTicks = 100; w.BeamRange = w.Range = 450;
                    w.TraverseHalfArcRad = math.radians(90f); w.TraverseRateRad = math.radians(180f); w.PowerDraw = 4; w.SustainedDps = 90;
                    if (id == "wpn_fixed_beam_turret_small") { w.Damage = 180; w.IsFixed = 1; w.TraverseHalfArcRad = w.TraverseRateRad = 0; w.SustainedDps = 108; }
                    if (id == "wpn_ark_lance_medium") { w.Damage = 900; w.PenetrationCells = 2; w.TriggerIntervalTicks = 600; w.BeamRange = w.Range = 900;
                        w.BeamPulseTicks = 60; w.BeamWidth = .6f; w.TraverseHalfArcRad = math.radians(35f); w.TraverseRateRad = math.radians(60f); w.PowerDraw = 10; }
                    break;
                case "wpn_railgun_medium":
                    w.Visual = ProjectileVisual.RailgunSlug; w.Damage = 250; w.PenetrationCells = 6; w.TriggerIntervalTicks = 200;
                    w.BurstCount = 4; w.BurstIntervalTicks = 6; w.SpreadRad = math.radians(.5f); w.ProjectileSpeed = 1200; w.LifetimeMinTicks = w.LifetimeMaxTicks = 60;
                    w.TraverseHalfArcRad = math.radians(30f); w.TraverseRateRad = math.radians(90f); w.PowerDraw = 8; w.Range = 1200; w.SustainedDps = 300; break;
                case "wpn_flak_cannon_medium":
                    w.Type = DamageType.Explosive; w.Visual = ProjectileVisual.FlakShell; w.DefaultFireGroups = 4; w.ExplodeOnExpire = w.IsFixed = 1;
                    w.Damage = 0; w.ExplosionRadius = 2.5f; w.ExplosionDamage = 60; w.TriggerIntervalTicks = 60; w.PelletsPerRound = 8;
                    w.SpreadRad = math.radians(6f); w.ProjectileSpeed = 300; w.ProjectileRadius = .2f; w.LifetimeMinTicks = 30;
                    w.TraverseHalfArcRad = w.TraverseRateRad = 0; w.PowerDraw = 5; w.Range = 450; w.SustainedDps = 240; break;
                case "wpn_missile_launcher_small":
                    w.Delivery = WeaponDelivery.Missile; w.Type = DamageType.Explosive; w.Visual = ProjectileVisual.Missile; w.DefaultFireGroups = 4; w.ExplodeOnExpire = w.IsFixed = 1;
                    w.Damage = 0; w.ExplosionRadius = 3; w.ExplosionDamage = 200; w.TriggerIntervalTicks = 200; w.BurstCount = 6; w.BurstIntervalTicks = 3;
                    w.ProjectileSpeed = 180; w.ProjectileRadius = .3f; w.LifetimeMinTicks = w.LifetimeMaxTicks = 300; w.HomingTurnRateRad = math.radians(90f);
                    w.TraverseHalfArcRad = w.TraverseRateRad = 0; w.PowerDraw = 2; w.Range = 900; w.SustainedDps = 180; break;
                default: Assert.Fail("No §4.9 oracle for " + id); break;
            }
            w.ImpulsePerHit = w.Damage * 2; return w;
        }
    }
}
