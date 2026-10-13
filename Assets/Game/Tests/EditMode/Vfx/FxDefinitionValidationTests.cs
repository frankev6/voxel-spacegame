using System.Linq;
using Game.Data.Definitions;
using Game.Data.Validation;
using NUnit.Framework;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Vfx
{
    /// <summary>SPEC-63 §4.2 invalid content combinations and field-bound diagnostics.</summary>
    public sealed class FxDefinitionValidationTests
    {
        [TearDown] public void TearDown() => DestroyAll();
        static FxDefinition Fx(FxKind kind, FxMode mode) => Create<FxDefinition>("fx_test", so =>
        { Set(so, "kind", (int)kind); Set(so, "mode", (int)mode); });
        static void Has(ValidationReport report, string code, string field)
        {
            Assert.IsTrue(report.Entries.Any(e => e.Code == code && e.Field == field && e.AssetPath.StartsWith("test:")), report.ToText());
        }
        [Test] public void InvalidCombinations_ReportErrors()
        {
            Has(Validate(Fx(FxKind.None, FxMode.Batched)), "VFX-E001", "kind");
            Has(Validate(Fx(FxKind.HitKinetic, FxMode.Batched)), "VFX-E002", "graph");
            Has(Validate(Fx(FxKind.ExplosionLarge, FxMode.Pooled)), "VFX-E002", "graph");
            var missing = Validate(Fx(FxKind.Tracer, FxMode.Instanced));
            Has(missing, "VFX-E003", "mesh"); Has(missing, "VFX-E003", "material");
            foreach (var kind in new[] { FxKind.Beam, FxKind.Thruster, FxKind.Tracer, FxKind.ShieldShell })
            {
                Has(Validate(Fx(kind, FxMode.Batched)), "VFX-E004", "mode");
                Has(Validate(Fx(kind, FxMode.Pooled)), "VFX-E004", "mode");
            }
            Has(Validate(Fx(FxKind.HitEnergy, FxMode.Instanced)), "VFX-E004", "mode");
            var debris = Fx(FxKind.Debris, FxMode.Batched);
            Has(Validate(debris), "VFX-E005", "particlesPerEvent");
            Edit(debris, so => { Set(so, "particlesPerEvent", 1); Set(so, "mode", (int)FxMode.Pooled); });
            Has(Validate(debris), "VFX-E005", "particlesPerEvent");
            foreach (var kind in new[] { FxKind.ExplosionLarge, FxKind.Warp })
                Has(Validate(Fx(kind, FxMode.Batched)), "VFX-E006", "mode");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var tracer = Fx(FxKind.Tracer, FxMode.Instanced);
                Edit(tracer, so => { Set(so, "mesh", Mesh()); Set(so, "material", material); });
                Assert.AreEqual(0, Validate(tracer).ErrorCount, "Valid instanced definition");
            }
            finally { Object.DestroyImmediate(material); }

            var config = Create<VfxConfigDefinition>("config_vfx");
            Assert.AreEqual(7, Validate(config).Entries.Count(e => e.Code == "VFX-E011"));
            Assert.AreEqual(6, Validate(config).Entries.Count(e => e.Code == "VFX-W011"));
            Edit(config, so => so.FindProperty("defaults").arraySize = 12);
            Has(Validate(config), "VFX-E010", "defaults");
            var definitions = Enumerable.Range(1, 13).Select(k => Fx((FxKind)k, FxMode.Batched)).ToArray();
            Edit(config, so =>
            {
                var p = so.FindProperty("defaults"); p.arraySize = 14;
                for (int k = 1; k <= 13; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = definitions[k - 1];
            });
            Assert.AreEqual(0, Validate(config).ErrorCount, "Config validates references, definitions validate their own content");
            Edit(config, so =>
            {
                so.FindProperty("defaults").GetArrayElementAtIndex(1).objectReferenceValue = definitions[1];
                so.FindProperty("fallbackCellPalette").arraySize = 255;
                Set(so, "debrisLifetimeMin", 7f);
                so.FindProperty("tracerColorByDamageType").arraySize = 3;
            });
            var invalid = Validate(config);
            Has(invalid, "VFX-E012", "defaults[1]"); Has(invalid, "VFX-E013", "fallbackCellPalette");
            Has(invalid, "VFX-E014", "debrisLifetimeMin"); Has(invalid, "VFX-E015", "tracerColorByDamageType");
            Assert.AreEqual(4, invalid.ErrorCount);
        }
    }
}
