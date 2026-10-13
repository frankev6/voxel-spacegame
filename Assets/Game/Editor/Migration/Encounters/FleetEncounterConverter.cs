using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;

namespace Game.Editor.Migration.Encounters
{
    /// <summary>ENC-010: strict fleet import followed by the spec's authored final four-row overlay (Q24).</summary>
    public sealed class FleetEncounterConverter : ILegacyConverter
    {
        public string Name => "Encounters";
        public int Order => 600;
        internal readonly struct ImportedShip
        {
            internal readonly string Id;
            internal readonly int Count;
            internal ImportedShip(string id, int count) { Id = id; Count = count; }
        }
        public void Convert(LegacyMigrationContext context)
        {
            int errors = context.Report.ErrorCount;
            var plans = Plans(context, false);
            if (context.Report.ErrorCount != errors) return;
            var compiler = EncounterContentAuthoring.Compiler(context);
            if (context.Report.ErrorCount != errors) return;
            foreach (var row in plans) EncounterContentAuthoring.WriteEncounter(context, row, compiler);
        }
        internal bool Preflight(LegacyMigrationContext context)
        {
            int errors = context.Report.ErrorCount;
            var plans = Plans(context, true);
            return context.Report.ErrorCount == errors && plans.Count == 4;
        }
        static List<EncounterContentTables.Row> Plans(LegacyMigrationContext context, bool pendingFactions)
        {
            context.HandOff ??= LegacyHandOff.Build(context.Index, context.IdMap, context.Report);
            var plans = new List<EncounterContentTables.Row>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fleet in context.HandOff.Fleets)
            {
                var row = EncounterContentTables.Rows.FirstOrDefault(r => r.Legacy == fleet.LegacyEntityId);
                if (row == null || !context.IdMap.TryMap(LegacyIdKind.Encounter, fleet.LegacyEntityId, out string mapped) || mapped != row.Id || fleet.SuggestedId != row.Id)
                { context.Report.Error("MIG-E003", "Import/Legacy", fleet.LegacyEntityId, "Missing or conflicting canonical encounter mapping."); continue; }
                if (!seen.Add(row.Id)) { context.Report.Error("MIG-E003", "Import/Legacy", fleet.LegacyEntityId, "Duplicate fleet encounter target."); continue; }
                bool neutral = fleet.Allegiance == "Neutral", enemy = fleet.Allegiance == "Enemy";
                if (!(neutral && row.Faction == "faction_vestari" || enemy && row.Faction != "faction_vestari"))
                { context.Report.Error("MIG-E021", "Import/Legacy", fleet.LegacyEntityId + ".Allegiance", "Allegiance does not match the authored faction map."); continue; }
                if (fleet.Formations == null) { context.Report.Error("MIG-E002", "Import/Legacy", fleet.LegacyEntityId + ".Formations", "Missing formation list."); continue; }
                var imported = FirstFormation(fleet);
                if (imported.Count == 0 && !(row.Kind == EncounterKind.Merchant && fleet.LegacyNarrativeEncounterId == "event_merchant_shady"))
                { context.Report.Error("MIG-E002", "Import/Legacy", fleet.LegacyEntityId + ".Formations", "Combat fleet requires a first formation."); continue; }
                foreach (var ship in imported) EncounterContentAuthoring.Require<ShipBlueprintDefinition>(context, ship.Id);
                EncounterContentAuthoring.CheckRow(context, row, pendingFactions);
                for (int i = 1; i < fleet.Formations.Count; i++)
                    context.Report.Info("ENC-I001", "Import/Legacy", fleet.LegacyEntityId + ".Formations[" + i + "]", "Dropped additional formation: " + string.Join(", ", fleet.Formations[i]) + ".");
                bool same = imported.Count == row.Ships.Count;
                for (int i = 0; same && i < imported.Count; i++)
                    same = imported[i].Id == row.Ships[i].Id && imported[i].Count == row.Ships[i].Min && imported[i].Count == row.Ships[i].Max;
                if (!same) context.Report.Info("ENC-I002", "Import/Legacy", fleet.LegacyEntityId + ".Formations[0]",
                    "First-formation groups [" + string.Join(", ", imported.Select(s => s.Id + " x" + s.Count)) + "] replaced by ENC-010's authored final ships/counts; source retained in the hand-off.");
                context.Report.Info("MIG-I091", "Import/Legacy", fleet.LegacyEntityId,
                    "Dropped legacy detection/patrol radius, movement speed, chase, automatic contact, faction-doctrine toggle/range and pool tuning; ENC-010 authors explicit fleets.");
                plans.Add(row);
            }
            if (plans.Count != 4) context.Report.Error("MIG-E002", "Import/Legacy", "Fleets", "Expected the exact four ENC-010 fleets.");
            return plans;
        }
        internal static IReadOnlyList<ImportedShip> FirstFormation(LegacyFleetTemplate fleet)
        {
            var result = new List<ImportedShip>();
            if (fleet.Formations == null || fleet.Formations.Count == 0) return result;
            foreach (string id in fleet.Formations[0])
            {
                int found = result.FindIndex(s => s.Id == id);
                if (found < 0) result.Add(new ImportedShip(id, 1));
                else result[found] = new ImportedShip(id, checked(result[found].Count + 1));
            }
            return result;
        }
    }
}
