using System;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.5. Managed baker called at boot after ContentService.CreateSingletons.</summary>
    public sealed class PowerContent
    {
        /// <summary>Reads exactly one PowerTuningDefinition; otherwise throws ContentValidationException (PWR-E001).</summary>
        public PowerContent(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var definitions = catalog.All<PowerTuningDefinition>();
            var report = new ValidationReport("PowerContent");
            if (definitions.Count != 1)
            {
                report.Error("PWR-E001", "ContentDatabase", nameof(PowerTuningDefinition),
                    $"Expected exactly one power tuning definition; found {definitions.Count}.");
                throw new ContentValidationException(report);
            }
            var d = definitions[0];
            d.Validate(report.For("content:" + d.IdString), null);
            if (report.HasErrors) throw new ContentValidationException(report);
            Tuning = new PowerTuning
            {
                PriorityShieldEmitter = d.PriorityShieldEmitter,
                PriorityThruster = d.PriorityThruster,
                PriorityAbility = d.PriorityAbility,
                PriorityWeapon = d.PriorityWeapon,
                BrownoutHoldTicks = d.BrownoutHoldTicks,
                ShieldPaddingScale = d.ShieldPaddingScale,
                ShieldPaddingMetres = d.ShieldPaddingMetres,
                ShieldMinRadiusMetres = d.ShieldMinRadiusMetres,
                ShieldRestartFraction = d.ShieldRestartFraction,
                SustainWarningSeconds = d.SustainWarningSeconds,
            };
        }

        public PowerTuning Tuning { get; }

        /// <summary>Idempotently creates one PowerSingletons entity with PowerTuning; ShieldSystem owns ShieldHitStreams.</summary>
        public void CreateSingletons(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PowerTuning>());
            if (!query.IsEmpty) return;
            var entity = em.CreateEntity(typeof(PowerTuning));
            em.SetName(entity, "PowerSingletons");
            em.SetComponentData(entity, Tuning);
        }

        public void DestroySingletons(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PowerTuning>());
            if (!query.IsEmpty) em.DestroyEntity(query);
        }
    }
}
