using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.2/6: catalog-indexed immutable ability data, created before the boot snapshot.</summary>
    public static class AbilityContent
    {
        /// <summary>Creates and records one singleton. The boot caller owns its persistent blob and must dispose it after jobs complete.
        /// An empty ability family yields the none-only blob until MIG-010 authors production assets (SPEC-24 Q4).</summary>
        public static void CreateSingletons(EntityManager em, ContentCatalog catalog, List<Entity> created)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (created == null) throw new ArgumentNullException(nameof(created));
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<AbilityDefsRef>());
            if (!query.IsEmpty) throw new InvalidOperationException("AbilityDefsRef already exists; create ability content once at boot.");
            var definitions = catalog.All<AbilityDefinition>();
            if (definitions.Count > ushort.MaxValue) throw new InvalidOperationException("Ability indices exceed ushort capacity.");
            var report = new ValidationReport("AbilityContent");
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                var scope = report.For("content:" + definition.IdString);
                definition.Validate(scope, null);
                if (definition.ActivationCue != null && catalog.IndexOf(definition.ActivationCue) == 0)
                    scope.Error("DATA-E010", "activationCue", "Activation cue is not in the catalog.");
                if (definition.ActivationFx != null && catalog.IndexOf(definition.ActivationFx) == 0)
                    scope.Error("DATA-E010", "activationFx", "Activation effect is not in the catalog.");
            }
            if (report.HasErrors) throw new ContentValidationException(report);
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AbilityDefsBlob>();
            var abilities = builder.Allocate(ref root.Abilities, definitions.Count + 1);
            abilities[0] = default; // None: zero hash, zero charges, no modifiers.
            builder.Allocate(ref abilities[0].Mods, 0);
            for (int i = 0; i < definitions.Count; i++)
            {
                var source = definitions[i];
                ref var target = ref abilities[i + 1];
                target.Kind = source.Kind; target.MaxCharges = (byte)source.MaxCharges;
                target.Reserved = 0; target.EnergyCost = source.EnergyCost;
                target.CooldownTicks = source.CooldownSeconds * SimTime.TicksPerSecond;
                target.ChargeRechargeTicks = source.ChargeRechargeSeconds * SimTime.TicksPerSecond;
                target.DurationTicks = SimTime.SecondsToTicks(source.DurationSeconds);
                target.ShieldEnableThreshold = source.ShieldEnableThreshold;
                target.IdHash = source.Id.Hash;
                target.ActivationCue = source.ActivationCue != null ? source.ActivationCue.Id.Hash : default;
                target.ActivationFx = source.ActivationFx != null ? source.ActivationFx.Id.Hash : default;
                int count = source.Kind == AbilityKind.StatBoost ? source.StatModifiers.Count : 0;
                var mods = builder.Allocate(ref target.Mods, count);
                for (int j = 0; j < count; j++)
                {
                    var mod = source.StatModifiers[j];
                    mods[j] = new AbilityStatModDef { Stat = mod.Stat, Multiplier = mod.Multiplier };
                }
            }
            var blob = builder.CreateBlobAssetReference<AbilityDefsBlob>(Allocator.Persistent);
            Entity entity = Entity.Null;
            try
            {
                entity = em.CreateEntity(typeof(AbilityDefsRef));
                em.SetComponentData(entity, new AbilityDefsRef { Value = blob });
#if UNITY_EDITOR
                em.SetName(entity, "Cfg.AbilityDefsRef");
#endif
                created.Add(entity);
            }
            catch
            {
                if (entity != Entity.Null && em.Exists(entity)) em.DestroyEntity(entity);
                blob.Dispose(); throw;
            }
        }
    }
}
