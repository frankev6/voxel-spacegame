using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using UnityEditor;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>SPEC-42 §4.11 exact text; memberships point to real generated mystery assets.</summary>
    internal static class LegacyClueWriter
    {
        internal readonly struct Clue
        {
            internal readonly string Id, Title, Body, Mystery, Target, Tag, Condition;
            internal Clue(string id, string title, string body, string mystery = "", string target = "", string tag = "", string condition = "")
            { Id = id; Title = title; Body = body; Mystery = mystery; Target = target; Tag = tag; Condition = condition; }
        }
        internal readonly struct Mystery
        {
            internal readonly string Id, Title, Solved;
            internal readonly string[] Requires;
            internal Mystery(string id, string title, string solved, params string[] requires)
            { Id = id; Title = title; Solved = solved; Requires = requires; }
        }
        internal static readonly Clue[] Texts = {
            new("clue_ark_mission_12", "Ark Mission Log 12", "An encrypted Ark beacon still sings inside the storm: 'Mission 12 — do not follow the lanes past the Silence.'", "mystery_arks"),
            new("clue_ark_mutiny", "The Mutiny", "The derelict's logs end in panic. Half the crew wanted to turn back; the other half sealed the bulkheads.", "mystery_arks"),
            new("clue_vanguard_spire", "Glass Spire", "A spire of fused glass marks where a city stood. The Vanguard's beam did this, a thousand years ago.", "mystery_vanguard"),
            new("clue_vanguard_forge", "Overgrown Forge", "Vanguard plating, still warm under the vines. Someone kept these forges running long after the war.", "mystery_vanguard"),
            new("clue_seen_the_loop", "Seen the Loop", "The sentinel showed you a ship being frozen in place. It was yours.", "mystery_loop"),
            new("clue_message_from_self", "A Message From You", "A recording, three cycles old, in your own voice: 'Don't trust the quiet systems.'", "mystery_loop"),
            new("clue_fragmented_warning", "Fragmented Warning", "Static, then a fragment: '…the gate remembers every crossing…'", "mystery_loop"),
            new("rumor_hidden_cache", "Hidden Cache", "The man at the bar swears there is a supply cache drifting in the asteroid belt nearby.", target: "starsys_drift", tag: "resource_cache", condition: "$opening_cache_found"),
            new("rumor_silence_incursions", "Silence Incursions", "Vanguard charts mark systems that went quiet, one after another, along the outer lanes."),
            new("rumor_satellite_array", "Satellite Array", "Someone left an array listening to the empty city. It is still transmitting."),
            new("rumor_two_sectors_ahead", "Two Sectors Ahead", "The echo pointed two sectors coreward, where the lanes fold back on themselves."),
            new("rumor_hidden_planet", "Hidden Planet", "The hollow moon's maps show a planet no chart lists."),
            new("rumor_hidden_research_station", "Hidden Research Station", "The signal traces back to a research station that does not answer.", target: "starsys_whisper", condition: "quest_completed(\"quest_signal_trace\")")
        };
        internal static readonly Mystery[] Mysteries = {
            new("mystery_arks", "What happened to the Arks?", "The Arks did not fail. They were turned back — from the inside.", "clue_ark_mission_12", "clue_ark_mutiny"),
            new("mystery_vanguard", "Who were the Vanguard?", "The Vanguard built gates and burned worlds to keep something out.", "clue_vanguard_spire", "clue_vanguard_forge"),
            new("mystery_loop", "Why do I keep finding my own voice?", "You have made this journey before. You left yourself directions.", "clue_seen_the_loop", "clue_message_from_self", "clue_fragmented_warning")
        };
        internal sealed class Plan
        {
            internal readonly Dictionary<string, AuthoredStarSystemDefinition> Targets = new(StringComparer.Ordinal);
            internal IEnumerable<string> Ids => Texts.Select(c => c.Id).Concat(Mysteries.Select(m => m.Id));
            internal IEnumerable<string> AssetPaths(string root) => Ids.Select(id => root + "/Clues/" + id + ".asset");
            internal void CheckTypes(string root)
            {
                foreach (var row in Texts)
                    if (row.Id.StartsWith("rumor_", StringComparison.Ordinal)) LegacyNarrativeConverter.CheckType<RumorDefinition>(root + "/Clues/" + row.Id + ".asset");
                    else LegacyNarrativeConverter.CheckType<ClueDefinition>(root + "/Clues/" + row.Id + ".asset");
                foreach (var row in Mysteries) LegacyNarrativeConverter.CheckType<MysteryDefinition>(root + "/Clues/" + row.Id + ".asset");
            }
            internal void Write(LegacyAssetWriter writer)
            {
                var mysteries = new Dictionary<string, MysteryDefinition>(StringComparer.Ordinal);
                for (int i = 0; i < Mysteries.Length; i++)
                {
                    var row = Mysteries[i]; int order = i;
                    mysteries.Add(row.Id, writer.CreateOrUpdate<MysteryDefinition>("Clues", row.Id, so => {
                        so.FindProperty("title").stringValue = row.Title; so.FindProperty("solvedText").stringValue = row.Solved;
                        so.FindProperty("order").intValue = order; so.FindProperty("requires").arraySize = 0;
                    }, out _));
                }
                var clues = new Dictionary<string, ClueDefinition>(StringComparer.Ordinal);
                foreach (var row in Texts)
                {
                    void Fill(SerializedObject so)
                    {
                        so.FindProperty("title").stringValue = row.Title; so.FindProperty("body").stringValue = row.Body;
                        so.FindProperty("mystery").objectReferenceValue = row.Mystery.Length == 0 ? null : mysteries[row.Mystery];
                        if (row.Id.StartsWith("rumor_", StringComparison.Ordinal))
                        {
                            so.FindProperty("target").objectReferenceValue = row.Target.Length == 0 ? null : Targets[row.Target];
                            so.FindProperty("targetBodyTag").stringValue = row.Tag;
                            so.FindProperty("resolvedWhen.source").stringValue = row.Condition;
                            so.FindProperty("resolvedWhen.hasErrors").boolValue = row.Condition.Length != 0;
                        }
                    }
                    clues.Add(row.Id, row.Id.StartsWith("rumor_", StringComparison.Ordinal)
                        ? writer.CreateOrUpdate<RumorDefinition>("Clues", row.Id, Fill, out _)
                        : writer.CreateOrUpdate<ClueDefinition>("Clues", row.Id, Fill, out _));
                }
                foreach (var row in Mysteries)
                    writer.SetLinkArrayIfEmpty(mysteries[row.Id], "requires", row.Requires.Select(id => (UnityEngine.Object)clues[id]).ToArray());
            }
        }
        internal static Plan Prepare(Func<string, ContentDefinition> resolve)
        {
            var plan = new Plan();
            foreach (string target in Texts.Where(c => c.Target.Length != 0).Select(c => c.Target).Distinct(StringComparer.Ordinal))
                plan.Targets.Add(target, LegacyQuestConverter.Require<AuthoredStarSystemDefinition>(resolve, target));
            return plan;
        }
    }
}
