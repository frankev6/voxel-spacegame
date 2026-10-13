using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Encounters
{
    /// <summary>Pure, ordered fleet composition (SPEC-33 §5.5). The caller applies its live-ship cap.</summary>
    public static class EncounterComposer
    {
        public static void Compose(EncounterDefinition def, ulong instanceSeed, float defaultCellSize, List<PlannedShip> into)
        {
            into.Clear();
            var rng = RngStreams.For(instanceSeed, RngPurpose.EncounterFleet, 0);
            // Validated blueprints own their cell size; the shared surface's default is not a radius override.
            if (!def.UseFactionDoctrine)
            {
                for (int i = 0; i < def.Ships.Count; i++)
                {
                    var entry = def.Ships[i];
                    int count = rng.NextInt(entry.countMin, entry.countMax + 1);
                    var ship = Plan(entry.blueprint, entry.aiProfile ?? def.Faction.DefaultAiProfile);
                    for (int k = 0; k < count; k++) into.Add(ship);
                }
                return;
            }

            int shipCount = rng.NextInt(def.DoctrineMinShips, def.DoctrineMaxShips + 1);
            var doctrine = def.Faction.Doctrine;
            double totalWeight = 0;
            for (int i = 0; i < doctrine.Count; i++) totalWeight += doctrine[i].weight;
            for (int k = 0; k < def.DoctrineMaxShips; k++)
            {
                // Consume every maximum slot even when this fleet uses fewer ships.
                float u = rng.NextFloat();
                if (k >= shipCount) continue;
                double threshold = u * totalWeight;
                int selected = doctrine.Count - 1;
                for (int i = 0; i < doctrine.Count; i++)
                {
                    threshold -= doctrine[i].weight;
                    if (threshold < 0) { selected = i; break; }
                }
                var entry = doctrine[selected];
                into.Add(Plan(entry.blueprint, entry.aiProfile ?? def.Faction.DefaultAiProfile));
            }
        }

        static PlannedShip Plan(ShipBlueprintDefinition blueprint, AiProfileDefinition profile)
            => new PlannedShip { Blueprint = blueprint, Profile = profile, RadiusM = BlueprintRadiusM(blueprint) };

        public static float BlueprintRadiusM(ShipBlueprintDefinition bp)
        {
            var min = bp.BoundsMin;
            var max = bp.BoundsMax;
            var extent = math.max(math.abs(new float3(min.x, min.y, min.z)),
                math.abs(new float3(max.x, max.y, max.z) + 1f));
            return bp.CellSize * math.length(extent);
        }
    }
}
