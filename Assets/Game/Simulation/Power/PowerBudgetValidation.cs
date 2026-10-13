using System;
using System.Globalization;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Voxel.Grid;
using Game.Voxel.Model;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §5.6. Shared editor/boot validation through the canonical blueprint library (Q40).</summary>
    internal static class PowerBudgetValidation
    {
        /// <summary>Appends warnings only. The caller owns the library; boot uses logical content paths.</summary>
        internal static void Validate(ContentCatalog catalog, BlueprintLibrary blueprints, ValidationReport report,
            Func<ShipBlueprintDefinition, string> assetPath = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (blueprints == null) throw new ArgumentNullException(nameof(blueprints));
            if (report == null) throw new ArgumentNullException(nameof(report));
            foreach (var blueprint in catalog.All<ShipBlueprintDefinition>())
            {
                // A failed decode is already diagnosed by BlueprintLibrary, not a fabricated power budget.
                if (!blueprints.TryGet(blueprint.Id.Hash, out var structure)) continue;
                var budget = Measure(structure, catalog);
                string path = assetPath?.Invoke(blueprint);
                if (string.IsNullOrEmpty(path)) path = "content:" + blueprint.IdString;
                if (budget.SustainSeconds < 30)
                    report.Warning("PWR-W001", path, "payload",
                        $"{blueprint.IdString}: sustain {Seconds(budget.SustainSeconds)} s < 30 s (rule R1)");
                if (budget.RechargeSeconds > 10)
                    report.Warning("PWR-W002", path, "payload",
                        $"{blueprint.IdString}: recharge {Seconds(budget.RechargeSeconds)} s > 10 s (rule R2)");
            }
        }

        internal static PowerBudget Measure(VoxelStructure structure, ContentCatalog catalog)
        {
            double generation = 0, capacity = 0, weapons = 0, shields = 0, thrusters = 0;
            var instances = structure.Data.Instances;
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0) continue;
                var block = catalog.GetBlock(instance.Block);
                if (block == null || block.Kind != BlockKind.Functional) continue;
                // Each functional instance counts once, even when its footprint occupies several cells.
                // Modules are independent: a mixed block may contribute sources, storage and several consumers.
                if (block.TryGetModule<PowerSourceModule>(out var source)) generation += source.Params.Generation;
                if (block.TryGetModule<PowerStorageModule>(out var storage)) capacity += storage.Params.Storage;
                if (block.TryGetModule<WeaponMountModule>(out var mount) && mount.Weapon != null)
                    weapons += mount.Weapon.PowerDraw;
                if (block.TryGetModule<ShieldEmitterModule>(out var shield)) shields += shield.Params.PowerDraw;
                if (block.TryGetModule<ThrusterModule>(out var thruster)) thrusters += thruster.Params.PowerDraw;
            }
            return new PowerBudget(generation, capacity, weapons, shields, thrusters);
        }

        static string Seconds(double value) => double.IsPositiveInfinity(value)
            ? "infinite" : value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
