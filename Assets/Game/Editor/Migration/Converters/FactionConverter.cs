using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Editor.Migration.Dto;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §4.6: strict faction values, catalog doctrine references and protected writes.</summary>
    public sealed class FactionConverter : ILegacyConverter
    {
        public string Name => "Factions";
        public int Order => 400;

        // The lead's targeted generation job checks the entire input before touching any output.
        internal bool Preflight(LegacyMigrationContext context)
        { foreach (var ignored in Plans(context)) { } return !context.Report.HasErrors; }

        public void Convert(LegacyMigrationContext context)
        {
            foreach (var plan in Plans(context))
            {
                try
                {
                    var writer = context.Writer; var dto = plan.Dto;
                    writer.CreateOrUpdate<FactionDefinition>("Factions", plan.Id, so =>
                    {
                        // Later ENC content owns stances, AI and territory. An owned link fill must survive reruns.
                        if (plan.Existing != null) { EditorUtility.CopySerialized(plan.Existing, so.targetObject); so.Update(); }
                        writer.Set(so, "displayName", p => p.stringValue = dto.DisplayName);
                        writer.Set(so, "description", p => p.stringValue = dto.Description);
                        writer.Set(so, "isPlayerFaction", p => p.boolValue = false);
                        writer.Set(so, "shipNamePrefix", p => p.stringValue = dto.ShipNamePrefix);
                        writer.Set(so, "mapColorToken", p => p.stringValue = "--color-faction-" + plan.Id.Substring("faction_".Length).Replace('_', '-'));
                        writer.Set(so, "liveryColor", p => p.colorValue = new Color((float)dto.Color[0], (float)dto.Color[1], (float)dto.Color[2], (float)dto.Color[3]));
                        writer.Set(so, "basePlayerRelation", p => p.intValue = dto.BasePlayerRelation);
                        writer.Set(so, "doctrine", p =>
                        {
                            p.arraySize = plan.Doctrine.Count;
                            for (int i = 0; i < plan.Doctrine.Count; i++)
                            {
                                var row = p.GetArrayElementAtIndex(i); var entry = plan.Doctrine[i];
                                row.FindPropertyRelative("blueprint").objectReferenceValue = entry.blueprint;
                                row.FindPropertyRelative("weight").floatValue = entry.weight;
                                row.FindPropertyRelative("aiProfile").objectReferenceValue = entry.aiProfile;
                            }
                        });
                    }, out _);
                }
                catch (Exception e) { context.Report.Error("MIG-E099", plan.File.RelativePath, "", "Faction conversion failed: " + e.Message); }
            }
        }

        sealed class Plan
        {
            internal LegacyFile File;
            internal LegacyFactionDto Dto;
            internal string Id;
            internal FactionDefinition Existing;
            internal readonly List<DoctrineEntry> Doctrine = new List<DoctrineEntry>();
        }

        static IReadOnlyList<Plan> Plans(LegacyMigrationContext context)
        {
            var plans = new List<Plan>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in context.Index.OfType("FactionData"))
            {
                var dto = LegacyJson.Read<LegacyFactionDto>(file, context.Report);
                if (dto == null) continue;
                if (!context.IdMap.TryMap(LegacyIdKind.Faction, dto.Id, out string id))
                { context.Report.Error("MIG-E003", file.RelativePath, "id", "Legacy faction ID is not mapped: " + dto.Id); continue; }
                if (id == null)
                { context.Report.Info("MIG-I091", file.RelativePath, "id", "Faction intentionally not converted: " + dto.Id + " (duplicate of faction_tlaxcala)."); continue; }
                if (!seen.Add(id))
                { duplicateIds.Add(id); context.Report.Error("MIG-E003", file.RelativePath, "id", "More than one source maps to faction " + id + "; every source for this target is skipped."); }
                if (dto.Color == null || dto.Color.Length != 4 || dto.Doctrine == null)
                { context.Report.Error("MIG-E002", file.RelativePath, "color/doctrine", "Faction requires four RGBA components and an explicit doctrine array; faction skipped."); continue; }
                bool valid = true;
                for (int i = 0; i < dto.Color.Length; i++)
                    if (double.IsNaN(dto.Color[i]) || double.IsInfinity(dto.Color[i]) || dto.Color[i] < 0 || dto.Color[i] > 1)
                    { Invalid(context, file, "color[" + i + "]", "RGBA must be finite and in [0,1]."); valid = false; }
                if (dto.BasePlayerRelation < -100 || dto.BasePlayerRelation > 100)
                { Invalid(context, file, "basePlayerRelation", "Relation must be in [-100,100]."); valid = false; }
                var plan = new Plan { File = file, Dto = dto, Id = id };
                if (context.Catalog != null) context.Catalog.TryGet<FactionDefinition>(new ContentId(id), out plan.Existing);
                if (plan.Existing != null && plan.Existing.IdString != id)
                { context.Report.Error("MIG-E003", file.RelativePath, "id", "Mapped faction ID resolves only through a former ID; reconcile its canonical identity before migrating."); valid = false; }
                if (plan.Existing != null && plan.Existing.IsPlayerFaction)
                { Invalid(context, file, "isPlayerFaction", "An existing player faction cannot be replaced by legacy non-player content."); valid = false; }
                for (int i = 0; i < dto.Doctrine.Length; i++)
                {
                    var row = dto.Doctrine[i]; string field = "doctrine[" + i + "]";
                    if (row == null || double.IsNaN(row.Weight) || double.IsInfinity(row.Weight) || row.Weight <= 0 || !float.IsFinite((float)row.Weight) || (float)row.Weight <= 0)
                    { Invalid(context, file, field + ".weight", "Doctrine weight must be a finite, representable positive float."); valid = false; continue; }
                    if (!context.IdMap.TryMap(LegacyIdKind.Ship, row.ShipKey, out string shipId))
                    { context.Report.Error("MIG-E003", file.RelativePath, field + ".shipKey", "Legacy ship key is not mapped: " + row.ShipKey); valid = false; continue; }
                    if (shipId == null || context.Catalog == null || !context.Catalog.TryGet<ShipBlueprintDefinition>(new ContentId(shipId), out var blueprint))
                    { context.Report.Error("MIG-E004", file.RelativePath, field + ".shipKey", "Mapped doctrine blueprint is unavailable in the current catalog: " + shipId); valid = false; continue; }
                    var entry = new DoctrineEntry { blueprint = blueprint, weight = (float)row.Weight };
                    if (plan.Existing != null)
                        foreach (var prior in plan.Existing.Doctrine) if (prior.blueprint == blueprint) { entry.aiProfile = prior.aiProfile; break; }
                    plan.Doctrine.Add(entry);
                }
                if (!dto.Logo.IsNull) context.Report.Info("MIG-I090", file.RelativePath, "logo", "FactionDefinition has no legacy logo field; dropped.");
                if (!dto.Crest.IsNull) context.Report.Info("MIG-I090", file.RelativePath, "crest", "FactionDefinition has no legacy crest field; dropped.");
                if (valid) plans.Add(plan);
            }
            // Build all plans before any write so a later duplicate cannot overwrite an earlier source's identity.
            plans.RemoveAll(plan => duplicateIds.Contains(plan.Id)); return plans;
        }
        static void Invalid(LegacyMigrationContext context, LegacyFile file, string field, string message)
            => context.Report.Error("MIG-E073", file.RelativePath, field, message + " Faction skipped before writing.");
    }
}
