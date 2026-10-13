using System;
using Game.Data.Services;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.5. Managed baker called at boot after ContentService.CreateSingletons.</summary>
    public sealed class PowerContent
    {
        /// <summary>Reads exactly one PowerTuningDefinition; otherwise throws ContentValidationException (PWR-E001).</summary>
        public PowerContent(ContentCatalog catalog) => throw new NotImplementedException("PWR-003");

        public PowerTuning Tuning => throw new NotImplementedException("PWR-003");

        /// <summary>Idempotently creates one PowerSingletons entity with PowerTuning; ShieldSystem owns ShieldHitStreams.</summary>
        public void CreateSingletons(EntityManager em) => throw new NotImplementedException("PWR-003");

        public void DestroySingletons(EntityManager em) => throw new NotImplementedException("PWR-003");
    }
}
