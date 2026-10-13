using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using UnityEditor;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>Exact SPEC-42 §4.10 legacy placements; no inferred locations from prototype behavior.</summary>
    internal static class LegacyBeatWriter
    {
        internal readonly struct Placement
        {
            internal readonly string BeatId, Condition;
            internal readonly BeatTrigger Trigger;
            internal readonly int Salience;
            internal readonly float Weight;
            internal readonly BeatRepeat Repeat;
            internal readonly bool FirstVisitOnly;
            internal readonly string[] Where;
            internal Placement(string id, BeatTrigger trigger, int salience, float weight, BeatRepeat repeat,
                string condition, bool firstVisit, string[] where)
            { BeatId = id; Trigger = trigger; Salience = salience; Weight = weight; Repeat = repeat;
              Condition = condition; FirstVisitOnly = firstVisit; Where = where; }
        }
        static readonly Dictionary<string, Placement> Placements = new(StringComparer.Ordinal) {
            { "story_waking_up", new Placement("beat_waking_up", BeatTrigger.GameStart, 100, 1f, BeatRepeat.Once, "", false, Array.Empty<string>()) },
            { "story_choose_start_ship", new Placement("beat_choose_start_ship", BeatTrigger.Manual, 0, 1f, BeatRepeat.Always, "", false, Array.Empty<string>()) },
            { "story_survey_system", new Placement("beat_survey_system", BeatTrigger.Manual, 0, 1f, BeatRepeat.Once, "", false, Array.Empty<string>()) },
            { "story_activate_gate", new Placement("beat_gate_dormant", BeatTrigger.Interact, 50, 1f, BeatRepeat.Always, "not $gate_ark_start_active", false, new[] { "gate_dormant" }) },
            { "story_use_active_gate", new Placement("beat_gate_active", BeatTrigger.Manual, 0, 1f, BeatRepeat.Always, "", false, Array.Empty<string>()) },
            { "demo_station_hub", new Placement("beat_station_hub", BeatTrigger.Interact, 10, 1f, BeatRepeat.Always, "", false, new[] { "ark_station" }) },
            { "bar_shady_man", new Placement("beat_shady_man", BeatTrigger.Manual, 0, 1f, BeatRepeat.Always, "", false, Array.Empty<string>()) },
            { "event_merchant_shady", new Placement("beat_merchant_shady", BeatTrigger.Hail, 10, 1f, BeatRepeat.Always, "", false, new[] { "encounter_traveling_merchant" }) },
            { "event_derelict_salvage", new Placement("beat_derelict_salvage", BeatTrigger.Interact, 10, 1f, BeatRepeat.OncePerBody, "", false, new[] { "derelict", "debris_field", "resource_cache" }) },
            { "event_mutiny_end", new Placement("beat_mutiny_end", BeatTrigger.Interact, 20, 1f, BeatRepeat.Once, "", false, new[] { "derelict", "debris_field" }) },
            { "event_distress_trap", new Placement("beat_distress_trap", BeatTrigger.Interact, 10, 0.3f, BeatRepeat.OncePerBody, "", false, new[] { "derelict", "debris_field" }) },
            { "event_empty_cargo", new Placement("beat_empty_cargo", BeatTrigger.Interact, 10, 0.3f, BeatRepeat.OncePerBody, "", false, new[] { "derelict", "resource_cache" }) },
            { "event_crew_stranded", new Placement("beat_crew_stranded", BeatTrigger.Interact, 10, 0.3f, BeatRepeat.OncePerBody, "", false, new[] { "derelict" }) },
            { "event_stable_point", new Placement("beat_stable_point", BeatTrigger.Interact, 10, 0.3f, BeatRepeat.OncePerBody, "", false, new[] { "resource_cache" }) },
            { "event_fuel_station", new Placement("beat_fuel_station", BeatTrigger.Interact, 5, 1f, BeatRepeat.Always, "", false, new[] { "station" }) },
            { "event_singing_storm", new Placement("beat_singing_storm", BeatTrigger.Interact, 10, 1f, BeatRepeat.Once, "", false, new[] { "gas_giant", "ice_planet", "rocky_planet", "ocean_planet" }) },
            { "event_glass_desert", new Placement("beat_glass_desert", BeatTrigger.Interact, 10, 1f, BeatRepeat.Once, "", false, new[] { "barren_planet" }) },
            { "event_frozen_sentinel", new Placement("beat_frozen_sentinel", BeatTrigger.Interact, 15, 1f, BeatRepeat.Once, "", false, new[] { "ice_planet" }) },
            { "event_silent_city", new Placement("beat_silent_city", BeatTrigger.Interact, 10, 0.5f, BeatRepeat.Once, "", false, new[] { "terrestrial_planet" }) },
            { "event_overgrown_forge", new Placement("beat_overgrown_forge", BeatTrigger.Interact, 10, 0.5f, BeatRepeat.Once, "", false, new[] { "terrestrial_planet" }) },
            { "event_cryo_vault", new Placement("beat_cryo_vault", BeatTrigger.Interact, 10, 1f, BeatRepeat.Once, "", false, new[] { "ice_giant" }) },
            { "event_time_echo", new Placement("beat_time_echo", BeatTrigger.Interact, 5, 1f, BeatRepeat.Once, "", false, new[] { "jump_point" }) },
            { "event_quarantine_lab", new Placement("beat_quarantine_lab", BeatTrigger.Interact, 10, 0.5f, BeatRepeat.Once, "", false, new[] { "abandoned_station" }) },
            { "event_miner_lament", new Placement("beat_miner_lament", BeatTrigger.Interact, 10, 0.5f, BeatRepeat.Once, "", false, new[] { "abandoned_station" }) },
            { "event_scavenger_king", new Placement("beat_scavenger_king", BeatTrigger.Interact, 10, 1f, BeatRepeat.Once, "", false, new[] { "asteroid_belt" }) },
            { "event_solar_parasite", new Placement("beat_solar_parasite", BeatTrigger.Interact, 10, 1f, BeatRepeat.Once, "", false, new[] { "star" }) },
            { "event_sensor_ghost", new Placement("beat_sensor_ghost", BeatTrigger.StarSystemEntered, 5, 1f, BeatRepeat.Once, "$sys_star_systems_entered >= 4", false, Array.Empty<string>()) },
            { "event_hollow_moon", new Placement("beat_hollow_moon", BeatTrigger.Interact, 10, 1f, BeatRepeat.Once, "", false, new[] { "moon" }) },
            { "event_horizon_message", new Placement("beat_horizon_message", BeatTrigger.Interact, 10, 1f, BeatRepeat.Once, "", false, new[] { "black_hole" }) },
            { "event_mystery_signal", new Placement("beat_mystery_signal", BeatTrigger.StarSystemEntered, 50, 1f, BeatRepeat.Once, "", true, new[] { "starsys_whisper" }) },
            { "event_territory_friendly_entry", new Placement("beat_territory_friendly_entry", BeatTrigger.StarSystemEntered, 20, 1f, BeatRepeat.Always, "star_system_owned() and relation(\"owner\") >= 0", true, Array.Empty<string>()) },
            { "event_territory_hostile_entry", new Placement("beat_territory_hostile_entry", BeatTrigger.StarSystemEntered, 20, 1f, BeatRepeat.Always, "star_system_owned() and relation(\"owner\") < 0", true, Array.Empty<string>()) },
            { "demo_conditional_options", new Placement("beat_debug_conditional_options", BeatTrigger.Manual, 0, 1f, BeatRepeat.Always, "", false, Array.Empty<string>()) },
            { "demo_audio_events", new Placement("beat_debug_audio_events", BeatTrigger.Manual, 0, 1f, BeatRepeat.Always, "", false, Array.Empty<string>()) },
        };
        internal static Placement GetPlacement(string legacyId)
            => Placements.TryGetValue(legacyId, out var value) ? value : throw new FormatException("Missing SPEC-42 placement: " + legacyId);

        internal static StoryBeatDefinition Write(LegacyAssetWriter writer, ConvertedEncounter encounter,
            bool nested, Func<string, ContentDefinition> resolve, IReadOnlyList<string> onStart = null)
        {
            var placement = GetPlacement(encounter.Source.Id);
            if (placement.BeatId != encounter.BeatId || encounter.Nodes.Count == 0)
                throw new FormatException("Converted beat identity/start node disagrees with placement: " + encounter.Source.Id);
            ContentDefinition location = null;
            if (placement.Trigger == BeatTrigger.Hail || placement.Where.Length > 0 && placement.Where[0].StartsWith("starsys_", StringComparison.Ordinal))
            {
                location = resolve(placement.Where[0]);
                if (location == null || placement.Trigger == BeatTrigger.Hail && !(location is EncounterDefinition)
                    || placement.Trigger != BeatTrigger.Hail && !(location is AuthoredStarSystemDefinition))
                    throw new FormatException("Missing typed placement reference: " + placement.Where[0]);
            }
            return writer.CreateOrUpdateFields<StoryBeatDefinition>("Beats", encounter.BeatId, so => {
                so.FindProperty("trigger").intValue = (int)(nested ? BeatTrigger.Manual : placement.Trigger);
                so.FindProperty("where.starSystem").objectReferenceValue = location as AuthoredStarSystemDefinition;
                so.FindProperty("where.hailEncounter").objectReferenceValue = location as EncounterDefinition;
                Strings(so.FindProperty("where.bodyTags"), location == null ? placement.Where : Array.Empty<string>());
                so.FindProperty("condition.source").stringValue = placement.Condition;
                so.FindProperty("salience").intValue = placement.Salience;
                so.FindProperty("weight").floatValue = placement.Weight;
                so.FindProperty("repeat").intValue = (int)placement.Repeat;
                so.FindProperty("firstVisitOnly").boolValue = placement.FirstVisitOnly;
                so.FindProperty("yarnNode").stringValue = encounter.Nodes[0].Title;
                Strings(so.FindProperty("onStart.source"), onStart ?? Array.Empty<string>());
                Strings(so.FindProperty("onComplete.source"), Array.Empty<string>());
                so.FindProperty("debug").boolValue = encounter.BeatId.StartsWith("beat_debug_", StringComparison.Ordinal);
                // Imports compile the sources; unchanged writes retain the compiler-owned graphs.
            }, out _);
        }
        internal static FactCatalogDefinition WriteFacts(LegacyAssetWriter writer, IReadOnlyCollection<FactDeclaration> facts)
        {
            var sorted = new List<FactDeclaration>(facts);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return writer.CreateOrUpdate<FactCatalogDefinition>("Facts", "facts_legacy", so => {
                var array = so.FindProperty("facts"); array.arraySize = sorted.Count;
                for (int i = 0; i < sorted.Count; i++)
                {
                    var value = sorted[i]; var row = array.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative("Name").stringValue = value.Name;
                    row.FindPropertyRelative("Kind").intValue = (int)value.Kind;
                    row.FindPropertyRelative("Scope").intValue = (int)value.Scope;
                    row.FindPropertyRelative("Flags").intValue = (int)value.Flags;
                    row.FindPropertyRelative("Default").intValue = value.Default;
                    row.FindPropertyRelative("Min").intValue = value.Min;
                    row.FindPropertyRelative("Max").intValue = value.Max;
                    row.FindPropertyRelative("Note").stringValue = value.Note ?? "";
                    row.FindPropertyRelative("JournalText").stringValue = value.JournalText ?? "";
                    Strings(row.FindPropertyRelative("FormerNames"), value.FormerNames ?? Array.Empty<string>());
                }
            }, out _);
        }
        static void Strings(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
