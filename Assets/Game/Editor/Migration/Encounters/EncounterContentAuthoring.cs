using System;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Editor.Migration.Encounters
{
    /// <summary>ENC-010 authored factions/encounters; all references must already be real catalog entries.</summary>
    internal static class EncounterContentAuthoring
    {
        internal static T Require<T>(LegacyMigrationContext context, string id) where T : ContentDefinition
        {
            if (context.Catalog != null && context.Catalog.TryGet<T>(new ContentId(id), out var asset) && asset.IdString == id) return asset;
            context.Report.Error("MIG-E004", "ENC-010", id, "Required canonical " + typeof(T).Name + " is unavailable: " + id + ". No substitute is created.");
            return null;
        }
        internal static void CheckRow(LegacyMigrationContext context, EncounterContentTables.Row row, bool pendingFactions = false)
        {
            if (!(pendingFactions && (row.Faction == "faction_raiders" || row.Faction == "faction_xeno"))) Require<FactionDefinition>(context, row.Faction);
            foreach (var ship in row.Ships)
            {
                Require<ShipBlueprintDefinition>(context, ship.Id);
                if (ship.Ai != null)
                {
                    var ai = Require<AiProfileDefinition>(context, ship.Ai);
                    if (ai != null && ai.Role != AiRole.Civilian) context.Report.Error("ENC-E015", row.Id, "ships.aiProfile", "Merchant requires the real Civilian profile.");
                }
            }
        }
        internal static LogicCompiler Compiler(LegacyMigrationContext context)
        {
            var refs = RefResolverRegistry.CreateDefault(context.Catalog);
            return new LogicCompiler(FactCatalog.Build(context.Catalog, refs, context.Report), refs, new LogicFunctionRegistry());
        }
        internal static void WriteEncounter(LegacyMigrationContext context, EncounterContentTables.Row row, LogicCompiler compiler)
        {
            try
            {
                var faction = context.Catalog.Get<FactionDefinition>(new ContentId(row.Faction));
                context.Writer.CreateOrUpdateFields<EncounterDefinition>("Encounters", row.Id, so =>
                {
                    var writer = context.Writer;
                    writer.Set(so, "displayName", p => p.stringValue = row.Name);
                    writer.Set(so, "faction", p => p.objectReferenceValue = faction);
                    writer.Set(so, "kind", p => p.intValue = (int)row.Kind);
                    writer.Set(so, "useFactionDoctrine", p => p.boolValue = false);
                    writer.Set(so, "ships", p =>
                    {
                        p.arraySize = row.Ships.Count;
                        for (int i = 0; i < row.Ships.Count; i++)
                        {
                            var source = row.Ships[i]; var entry = p.GetArrayElementAtIndex(i);
                            entry.FindPropertyRelative("blueprint").objectReferenceValue = context.Catalog.Get<ShipBlueprintDefinition>(new ContentId(source.Id));
                            entry.FindPropertyRelative("countMin").intValue = source.Min;
                            entry.FindPropertyRelative("countMax").intValue = source.Max;
                            entry.FindPropertyRelative("aiProfile").objectReferenceValue = source.Ai == null ? null : context.Catalog.Get<AiProfileDefinition>(new ContentId(source.Ai));
                        }
                    });
                    writer.Set(so, "directorWeight", p => p.floatValue = row.Weight);
                    writer.Set(so, "minDanger", p => p.intValue = row.DangerMin);
                    writer.Set(so, "maxDanger", p => p.intValue = row.DangerMax);
                    writer.Set(so, "spawnDistanceMinM", p => p.floatValue = row.DistanceMin);
                    writer.Set(so, "spawnDistanceMaxM", p => p.floatValue = row.DistanceMax);
                    writer.Set(so, "uniquePerVisit", p => p.boolValue = row.Kind == EncounterKind.Merchant);
                    writer.Set(so, "hailable", p => p.boolValue = row.Kind == EncounterKind.Merchant);
                    writer.Set(so, "persistUntilDefeated", p => p.boolValue = row.Kind == EncounterKind.Story);
                    // Use the real compiler before serialization/ownership hashing; import callbacks may run later.
                    so.ApplyModifiedPropertiesWithoutUndo();
                    ((EncounterDefinition)so.targetObject).CompileLogic(compiler, context.Report.For("content:" + row.Id));
                    so.Update();
                }, out _);
            }
            catch (Exception e) { context.Report.Error("MIG-E099", row.Id, "", "Encounter authoring failed: " + e.Message); }
        }

        // Call before any writes. GAL-012 owns these two actual star-system definitions.
        internal static bool PreflightFactions(LegacyMigrationContext context)
        {
            if (context.Catalog == null) { context.Report.Error("MIG-E004", "ENC-010", "catalog", "A current catalog is required."); return false; }
            int errors = context.Report.ErrorCount;
            var ark = Require<FactionDefinition>(context, "faction_ark");
            var vestari = Require<FactionDefinition>(context, "faction_vestari");
            Require<FactionDefinition>(context, "faction_tlaxcala"); Require<FactionDefinition>(context, "faction_khuchad_clan");
            var start = Require<AuthoredStarSystemDefinition>(context, "starsys_ark_start");
            var whisper = Require<AuthoredStarSystemDefinition>(context, "starsys_whisper");
            Require<ShipBlueprintDefinition>(context, "ship_fighter"); Require<ShipBlueprintDefinition>(context, "ship_striker");
            Require<AiProfileDefinition>(context, "ai_default");
            if (ark != null && (!ark.IsPlayerFaction || !ark.RelationLocked || ark.DisplayName != "ARK Expedition"))
                context.Report.Error("ENC-E001", "faction_ark", "", "Preserve/reconcile the canonical locked ARK Expedition player faction first.");
            foreach (var faction in context.Catalog.All<FactionDefinition>())
            {
                foreach (var system in faction.OwnedStarSystems)
                    if (system != null && (system == start && faction != ark || system == whisper && faction != vestari))
                        context.Report.Error("ENC-E005", faction.IdString, "ownedStarSystems", "Required star system already has a different owner.");
                foreach (var stance in faction.Stances)
                    if (stance.other != null && stance.other.IdString == "faction_raiders" && stance.stance != FactionStanceValue.Hostile)
                        context.Report.Error("ENC-E003", faction.IdString, "stances", "Existing reverse raider stance conflicts with ENC-010; reconcile before authoring.");
            }
            CheckOwnership(ark, start); CheckOwnership(vestari, whisper);
            return context.Report.ErrorCount == errors;
            void CheckOwnership(FactionDefinition faction, AuthoredStarSystemDefinition system)
            {
                if (faction == null || system == null || faction.OwnedStarSystems.Count == 0 || faction.OwnedStarSystems.Contains(system)) return;
                context.Report.Error("ENC-E005", faction.IdString, "ownedStarSystems", "Existing nonempty ownership list lacks the required system; preserved for reconciliation.");
            }
        }
        internal static bool PreflightGeneration(LegacyMigrationContext context)
        {
            int errors = context.Report.ErrorCount;
            if (context.Catalog == null) { context.Report.Error("MIG-E004", "ENC-010", "catalog", "A current catalog is required."); return false; }
            PreflightFactions(context);
            new FleetEncounterConverter().Preflight(context);
            Require<EncounterConfigDefinition>(context, "config_encounters");
            var drift = Require<AuthoredStarSystemDefinition>(context, "starsys_drift");
            if (drift != null && drift.Danger != 2) context.Report.Error("ENC-E013", "starsys_drift", "danger", "ENC-010 requires GAL-012's real Drift system at danger 2.");
            foreach (var row in EncounterContentTables.Rows)
                foreach (var ship in row.Ships)
                {
                    Require<ShipBlueprintDefinition>(context, ship.Id);
                    if (ship.Ai != null)
                    {
                        var profile = Require<AiProfileDefinition>(context, ship.Ai);
                        if (profile != null && profile.Role != AiRole.Civilian) context.Report.Error("ENC-E015", row.Id, "aiProfile", "Merchant AI profile must be Civilian.");
                    }
                }
            return context.Report.ErrorCount == errors;
        }
        internal static void ValidateScoped(Game.Data.Services.ContentCatalog catalog, ValidationReport report)
        {
            bool Present(ContentDefinition definition) => definition != null && catalog.TryGet<ContentDefinition>(definition.Id, out var found) && ReferenceEquals(definition, found);
            foreach (var faction in catalog.All<FactionDefinition>())
                faction.Validate(report.For("content:" + faction.IdString), new ContentValidationContext(faction.IdString, true, Present, catalog));
            foreach (var encounter in catalog.All<EncounterDefinition>())
                encounter.Validate(report.For("content:" + encounter.IdString), new ContentValidationContext(encounter.IdString, true, Present, catalog));
            foreach (var config in catalog.All<EncounterConfigDefinition>())
                config.Validate(report.For("content:" + config.IdString), new ContentValidationContext(config.IdString, true, Present, catalog));
            new Game.Editor.Content.Validators.EncounterContentValidator().Validate(catalog, report);
        }
        internal static void AuthorFactions(LegacyMigrationContext context)
        {
            if (!PreflightFactions(context)) return;
            var xeno = context.Writer.CreateOrUpdateFields<FactionDefinition>("Factions", "faction_xeno", so =>
            {
                var writer = context.Writer;
                writer.Set(so, "displayName", p => p.stringValue = "Xeno");
                writer.Set(so, "isPlayerFaction", p => p.boolValue = false);
                writer.Set(so, "relationLocked", p => p.boolValue = true);
                writer.Set(so, "basePlayerRelation", p => p.intValue = -100);
                writer.Set(so, "mapColorToken", p => p.stringValue = "--color-faction-xeno");
            }, out _);
            if (xeno == null) return;
            var others = context.Catalog.All<FactionDefinition>().Where(f => !f.IsPlayerFaction && f.IdString != "faction_raiders" && f.IdString != "faction_xeno")
                .Concat(new[] { xeno }).OrderBy(f => f.IdString, StringComparer.Ordinal).ToArray();
            context.Writer.CreateOrUpdateFields<FactionDefinition>("Factions", "faction_raiders", so =>
            {
                var writer = context.Writer;
                writer.Set(so, "displayName", p => p.stringValue = "Raiders");
                writer.Set(so, "isPlayerFaction", p => p.boolValue = false);
                writer.Set(so, "relationLocked", p => p.boolValue = true);
                writer.Set(so, "basePlayerRelation", p => p.intValue = -100);
                writer.Set(so, "mapColorToken", p => p.stringValue = "--color-faction-raiders");
                writer.Set(so, "defaultAiProfile", p => p.objectReferenceValue = context.Catalog.Get<AiProfileDefinition>(new ContentId("ai_default")));
                writer.Set(so, "stances", p =>
                {
                    p.arraySize = others.Length;
                    for (int i = 0; i < others.Length; i++)
                    { var entry = p.GetArrayElementAtIndex(i); entry.FindPropertyRelative("other").objectReferenceValue = others[i]; entry.FindPropertyRelative("stance").intValue = (int)FactionStanceValue.Hostile; }
                });
                writer.Set(so, "doctrine", p =>
                {
                    p.arraySize = 2;
                    for (int i = 0; i < 2; i++)
                    {
                        var entry = p.GetArrayElementAtIndex(i); string id = i == 0 ? "ship_fighter" : "ship_striker";
                        entry.FindPropertyRelative("blueprint").objectReferenceValue = context.Catalog.Get<ShipBlueprintDefinition>(new ContentId(id));
                        entry.FindPropertyRelative("weight").floatValue = i == 0 ? 1 : .6f;
                        entry.FindPropertyRelative("aiProfile").objectReferenceValue = null;
                    }
                });
            }, out _);
            Link("faction_ark", "starsys_ark_start"); Link("faction_vestari", "starsys_whisper");
            void Link(string faction, string system)
            {
                var owner = context.Catalog.Get<FactionDefinition>(new ContentId(faction)); var target = context.Catalog.Get<AuthoredStarSystemDefinition>(new ContentId(system));
                if (!owner.OwnedStarSystems.Contains(target)) context.Writer.SetLinkArrayIfEmpty(owner, "ownedStarSystems", new UnityEngine.Object[] { target });
            }
        }
        internal static void AuthorRemainingEncounters(LegacyMigrationContext context)
        {
            int errors = context.Report.ErrorCount;
            foreach (var row in EncounterContentTables.Rows.Where(r => r.Legacy == null)) CheckRow(context, row);
            if (context.Report.ErrorCount != errors) return;
            var compiler = Compiler(context);
            if (context.Report.ErrorCount != errors) return;
            foreach (var row in EncounterContentTables.Rows.Where(r => r.Legacy == null)) WriteEncounter(context, row, compiler);
        }
    }
}
