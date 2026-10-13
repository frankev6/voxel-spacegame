using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Editor.Migration.Dto;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 MIG-010: canonical SPEC-24 ability values and protected block grant arrays.</summary>
    public sealed class AbilityConverter : ILegacyConverter
    {
        public string Name => "Abilities";
        public int Order => 210;

        public void Convert(LegacyMigrationContext context)
        {
            var sources = AssetDatabase.LoadAssetAtPath<LegacyMigrationSources>(LegacyMigrationSources.AssetPath);
            if (sources == null)
            { context.Report.Error("MIG-E004", LegacyMigrationSources.AssetPath, "", "Migration source references are missing."); return; }
            var icons = sources.LoadIcons(context.Report);
            foreach (var file in context.Index.Files)
            {
                string key, description;
                LegacyRef iconRef;
                bool shield;
                var dropped = new List<string>();
                if (file.Type == "ShieldToggleAbilitySO")
                {
                    var dto = LegacyJson.Read<LegacyShieldToggleAbilityDto>(file, context.Report);
                    if (dto == null) continue;
                    key = dto.Id; iconRef = dto.Icon; shield = true; description = "";
                    if (!string.IsNullOrEmpty(dto.Description)) dropped.Add("legacy Description (placeholder)");
                    if ((float)dto.EnableThreshold != .3f) dropped.Add("legacy enableThreshold (SPEC-24 threshold 0.3)");
                }
                else if (file.Type == "BoostAbilitySO")
                {
                    var dto = LegacyJson.Read<LegacyBoostAbilityDto>(file, context.Report);
                    if (dto == null) continue;
                    key = dto.Id; iconRef = dto.Icon; shield = false; description = dto.Description;
                    if (dto.MaxCharges != 2) dropped.Add("legacy maxCharges (SPEC-24 charges 2)");
                    if (dto.CooldownPerUse != .5) dropped.Add("legacy cooldownPerUse (SPEC-24 cooldown 0.5 s)");
                    if (dto.ChargeRechargeTime != 3) dropped.Add("legacy chargeRechargeTime (SPEC-24 recharge 3 s)");
                    if (dto.BoostDuration != 1) dropped.Add("legacy boostDuration (SPEC-24 duration 1 s)");
                }
                else continue;

                if (!context.IdMap.TryMap(LegacyIdKind.Ability, key ?? "", out string id) ||
                    id != (shield ? "ability_shield_toggle" : "ability_speed_boost"))
                { context.Report.Error("MIG-E003", file.RelativePath, "<ID>k__BackingField", "Ability ID has no canonical mapping for its legacy type: " + key); continue; }
                if (iconRef.IsNull || string.IsNullOrEmpty(iconRef.Name) || !icons.TryGetValue(iconRef.Name, out var icon))
                { context.Report.Error("MIG-E004", file.RelativePath, "<Icon>k__BackingField", "Icon sprite not found in configured sheet: " + iconRef.Name); continue; }
                try
                {
                    var writer = context.Writer;
                    var ability = writer.CreateOrUpdate<AbilityDefinition>("Abilities", id, so =>
                    {
                        writer.Set(so, "displayName", p => p.stringValue = shield ? "Shield Toggle" : "Speed Boost");
                        writer.Set(so, "description", p => p.stringValue = description);
                        writer.Set(so, "icon", p => p.objectReferenceValue = icon);
                        writer.Set(so, "kind", p => p.intValue = (int)(shield ? AbilityKind.ShieldToggle : AbilityKind.StatBoost));
                        writer.Set(so, "energyCost", p => p.floatValue = shield ? 0f : 15f);
                        writer.Set(so, "cooldownSeconds", p => p.floatValue = shield ? 1f : .5f);
                        writer.Set(so, "maxCharges", p => p.intValue = shield ? 1 : 2);
                        writer.Set(so, "chargeRechargeSeconds", p => p.floatValue = shield ? 0f : 3f);
                        writer.Set(so, "durationSeconds", p => p.floatValue = shield ? 0f : 1f);
                        writer.Set(so, "shieldEnableThreshold", p => p.floatValue = shield ? .3f : 0f);
                        writer.Set(so, "statModifiers", p =>
                        {
                            p.arraySize = shield ? 0 : 2;
                            if (shield) return;
                            p.GetArrayElementAtIndex(0).FindPropertyRelative("Stat").intValue = (int)StatId.MaxSpeed;
                            p.GetArrayElementAtIndex(0).FindPropertyRelative("Multiplier").floatValue = 1.8f;
                            p.GetArrayElementAtIndex(1).FindPropertyRelative("Stat").intValue = (int)StatId.Thrust;
                            p.GetArrayElementAtIndex(1).FindPropertyRelative("Multiplier").floatValue = 2f;
                        });
                        writer.Set(so, "activationCue", p => p.objectReferenceValue = null);
                        writer.Set(so, "activationFx", p => p.objectReferenceValue = null);
                    }, out _);
                    if (ability == null) continue;
                    if (dropped.Count != 0)
                        context.Report.Info("MIG-I091", file.RelativePath, "", "Dropped: " + string.Join(", ", dropped) + ".");
                    if (shield) Grant(context, file, "block_shield_small", ability);
                    else
                    {
                        Grant(context, file, "block_thruster_small", ability);
                        Grant(context, file, "block_thruster_medium", ability);
                        Grant(context, file, "block_thruster_large", ability);
                    }
                }
                catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, "", "Ability conversion failed: " + e.Message); }
            }
        }

        static void Grant(LegacyMigrationContext context, LegacyFile file, string blockId, AbilityDefinition ability)
        {
            if (context.Catalog == null || !context.Catalog.TryGet<BlockDefinition>(new ContentId(blockId), out var block))
            { context.Report.Error("MIG-E004", file.RelativePath, "grantedAbilities", "Required block for ability grant is unavailable: " + blockId); return; }
            context.Writer.SetLinkArrayIfEmpty(block, "grantedAbilities", new UnityEngine.Object[] { ability });
        }
    }
}
