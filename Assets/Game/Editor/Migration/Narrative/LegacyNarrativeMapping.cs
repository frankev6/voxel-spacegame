using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Data.Validation;
using Game.Data.Definitions;
using Game.Data.Logic;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>SPEC-40 §5.8 and §4.13. All migration vocabulary lives here, not in runtime code.</summary>
    internal static class LegacyNarrativeMapping
    {
        public static readonly IReadOnlyDictionary<string, string> BeatOverrides = Table(
            "story_activate_gate", "beat_gate_dormant", "story_use_active_gate", "beat_gate_active",
            "demo_station_hub", "beat_station_hub", "bar_shady_man", "beat_shady_man");
        public static readonly IReadOnlyDictionary<string, string> QuestIds = Table(
            "quest_ark_material_turnin", "quest_survey_local_materials", "quest_trigger_fail_test", "quest_debug_fail_sandbox",
            "story_chain_signal_1", "quest_whisper_chain", "story_chain_signal_2", "quest_whisper_chain",
            "story_chain_signal_3", "quest_whisper_chain", "tmpl_bounty_probe", "contract_bounty_probe",
            "tmpl_signal_relay", "contract_signal_relay", "tmpl_material_sweep", "contract_material_sweep");
        public static readonly IReadOnlyDictionary<string, string> ItemIds = Table(
            "500", "resource_palladium", "717185106", "item_strange_matter_container_full",
            "resource_fuel", "resource_fuel", "resource_crew", "resource_crew");
        public static readonly IReadOnlyDictionary<string, string> EncounterIds = Table(
            "npc_probe_tutorial", "encounter_xeno_probe_tutorial", "npc_story_hunter", "encounter_whisper_hunter",
            "npc_enemy_1", "encounter_raiders", "npc_traveling_merchant", "encounter_traveling_merchant",
            "encounter_combat_01", "encounter_raiders", "encounter_glass_worm", "encounter_glass_worms",
            "encounter_doppelganger", "encounter_doppelganger", "encounter_pirate_boss", "encounter_scavenger_king",
            "encounter_jungle_beasts", "encounter_jungle_beasts", "encounter_solar_parasite", "encounter_solar_parasite",
            "encounter_extraction", "encounter_extraction_guardians");
        public static readonly IReadOnlyDictionary<string, string> SystemIds = Table(
            "ark_start", "starsys_ark_start", "basesolarsystemdata", "starsys_whisper");
        public static readonly IReadOnlyDictionary<string, string> AudioIds = Table(
            "UI_Success", "cue_ui_success", "UI_Fail", "cue_ui_fail", "UI_Button_Click", "cue_ui_click", "UI_View_Open", "cue_ui_view_open");
        public static readonly IReadOnlyDictionary<string, string> LootIds = Table(
            "BaseLootTable", "loot_base", "CreativeModeLootTable", "loot_creative");
        public static readonly IReadOnlyDictionary<string, string> MerchantIds = Table(
            "demo_station_hub", "merchant_station_market", "event_merchant_shady", "merchant_shady_trader");
        public static readonly IReadOnlyDictionary<string, string> BodyTags = Table(
            "body_station_ark", "ark_station", "body_station", "station", "body_station_abandoned", "abandoned_station",
            "body_derelict_ship", "derelict", "body_debris_field", "debris_field", "body_resource_cache", "resource_cache",
            "body_gate_dormant", "gate_dormant", "body_gate_active", "gate_active", "body_jump_point", "jump_point",
            "body_asteroid_belt", "asteroid_belt", "body_planet_barren", "barren_planet", "body_planet_gas_giant", "gas_giant",
            "body_planet_ice", "ice_planet", "body_planet_ice_giant", "ice_giant", "body_planet_rocky", "rocky_planet",
            "body_planet_terrestrial", "terrestrial_planet", "body_planet_water", "ocean_planet", "body_moon", "moon",
            "body_star", "star", "body_star_blackhole", "black_hole");
        public static readonly IReadOnlyDictionary<string, string> RequirementFunctions = Table(
            "REQ_CURRENCY", "credits", "REQ_ITEM", "has_item", "REQ_VAR", "flag",
            "REQ_QUEST_ACTIVE", "quest_active", "REQ_QUEST_STATE", "quest_state");
        public static readonly IReadOnlyDictionary<string, string> ActionCommands = Table(
            "GOTO", "jump", "PUSH_GOTO", "detour", "RETURN", "return", "END", "stop", "RANDOM_BRANCH", "random",
            "SET_VAR", "flag", "ADD_ITEM", "give_item", "REMOVE_ITEM", "take_item", "CHANGE_CURRENCY", "credits",
            "CHANGE_FUEL", "fuel", "CHANGE_CREW", "crew", "CHANGE_HULL", "hull", "CHANGE_RESEARCH", "add_fact",
            "GRANT_LOOT", "give_loot", "DESTROY_ENTITY", "remove_body", "START_COMBAT", "start_combat",
            "TRADE", "open_merchant", "REPAIR_ALL_SHIPS", "repair_ship", "OPEN_SHIP_EDITOR", "open_ship_editor",
            "PLAY_AUDIO", "play_sfx", "PLAY_NESTED", "nested", "PLAY_FROM_POOL", "unavailable", "CUSTOM_ACTION", "custom");
        public static readonly IReadOnlyDictionary<string, string> CustomCommands = Table(
            "state.set", "flag", "quest.accept", "start_quest", "quest.generate_template", "template",
            "quest.complete_objective", "objective", "quest.add_log", "log", "quest.complete", "complete_quest",
            "quest.fail", "fail_quest", "spawn.entity", "spawn_encounter", "flow.queue_encounter", "queue_beat",
            "world.replace_entity", "gate", "world.exit_system", "request_lane_jump");
        public static readonly IReadOnlyDictionary<string, string> CustomParameterKeys = Table(
            "state.set", "key,value", "quest.accept", "questId,track", "quest.generate_template", "templateId,track",
            "quest.complete_objective", "questId,objectiveId", "quest.add_log", "questId,text", "quest.complete", "questId",
            "quest.fail", "questId", "spawn.entity", "entityId,anchor,count", "flow.queue_encounter", "id,intro",
            "world.replace_entity", "system,from,to,inPlace", "world.exit_system", "");
        public static readonly IReadOnlyDictionary<string, string> SymbolicFlagValues = Table(
            "persist.story_waking_up", "complete", "run.intro_phase", "complete", "story_survey", "complete",
            "persist.story_gate_activated", "complete", "cache_location", "asteroid_belt", "station_shady_truth", "revealed",
            "quest.ark_material_turnin.turnedin", "1", "quest.station_data.submitted", "1",
            "quest.relation_watch.report", "submitted", "quest.hostile_blackbox.turnin", "done");
        public static readonly IReadOnlyDictionary<string, string> QuestCommands = Table(
            "AddLog", "log", "AcceptQuest", "start_quest", "TrackQuest", "track_quest", "SpawnEntity", "spawn_encounter",
            "AdjustFactionRelation", "adjust_relation", "GenerateMissionFromTemplate", "template", "SetWorldState", "flag",
            "QueueEncounter", "queue_beat", "CompleteObjective", "objective", "FailQuest", "fail_quest");
        public static readonly IReadOnlyDictionary<string, string> OutcomeActions = Table(
            "RemoveItem", "REMOVE_ITEM", "CustomAction", "CUSTOM_ACTION");
        public static readonly IReadOnlyDictionary<string, string> QuestStateFunctions = Table(
            "NotAccepted", "quest_not_started", "Active", "quest_active", "Completed", "quest_completed", "Failed", "quest_failed");
        // Trigger event fields are routing metadata, not LogicFunctionRegistry expressions (NAR-016).
        public static readonly IReadOnlyDictionary<string, string> EventPredicateRoles = Table(
            "firstVisit", "firstVisitOnly", "questId", "questCompletedMatch", "delta", "droppedDebugRelationTrigger");
        public static readonly IReadOnlyDictionary<string, string> DroppedFlags = Table(
            "story.whisper_chain.complete", "quest_whisper_chain", "quest.bounty.last_completed", "contract_bounty_probe",
            "quest.template.signal_relay.completed", "contract_signal_relay");

        internal sealed class FlagRow
        {
            public readonly string Condition;
            public readonly IReadOnlyList<string> Commands;
            public readonly FactDeclaration? Fact;
            public FlagRow(string condition, FactDeclaration? fact, params string[] commands)
            { Condition = condition; Fact = fact; Commands = Array.AsReadOnly(commands); }
        }
        public static readonly IReadOnlyDictionary<string, FlagRow> Flags = BuildFlags();

        public static string BeatId(string id)
        {
            Id(id);
            if (BeatOverrides.TryGetValue(id, out string mapped)) return mapped;
            if (id.StartsWith("demo_", StringComparison.Ordinal)) return "beat_debug_" + Id(id.Substring(5));
            if (id.StartsWith("event_", StringComparison.Ordinal)) id = id.Substring(6);
            else if (id.StartsWith("story_", StringComparison.Ordinal)) id = id.Substring(6);
            return "beat_" + Id(id);
        }
        public static string NodeTitle(string encounter, string page) => BeatId(encounter).Substring(5) + "_" + Id(page);
        public static string QuestId(string id) => id != null && QuestIds.TryGetValue(id, out string mapped) ? mapped : ContentId(id);
        public static string ItemId(string id) => id != null && ItemIds.TryGetValue(id, out string mapped) ? mapped : ContentId(id);
        public static string ObjectiveFact(string quest, string objective)
        {
            string id = QuestId(quest);
            return (id.StartsWith("quest_", StringComparison.Ordinal) ? id.Substring(6) : id) + "_" + Id(objective);
        }
        public static string BodyTag(string id)
        {
            if (id == "player") return id;
            if (LegacyIdMap.Default.TryMap(LegacyIdKind.Body, id, out string body) && body != null) return Map(BodyTags, body);
            foreach (string tag in BodyTags.Values) if (tag == id) return id;
            return Map(BodyTags, id);
        }
        public static string Map(IReadOnlyDictionary<string, string> table, string key) =>
            key != null && table.TryGetValue(key, out string value) ? value : throw new FormatException("Unmapped legacy vocabulary: " + key);
        // Legacy page IDs (start, hub, …) are Yarn identifiers, not ContentIds.
        public static string Id(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z') throw new FormatException("Invalid legacy identifier: " + value);
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_')) throw new FormatException("Invalid legacy identifier: " + value);
            return value;
        }
        static string ContentId(string value) => ContentIdRules.IsValidFormat(value) ? value : throw new FormatException("Unmapped content ID: " + value);
        public static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static IReadOnlyDictionary<string, string> Table(params string[] pairs)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2) table.Add(pairs[i], pairs[i + 1]);
            return new ReadOnlyDictionary<string, string>(table);
        }
        static IReadOnlyDictionary<string, FlagRow> BuildFlags()
        {
            var rows = new Dictionary<string, FlagRow>(StringComparer.Ordinal);
            Bool("persist.story_waking_up", "intro_awake"); Bool("run.intro_phase", "intro_ship_chosen");
            Bool("story_survey", "story_survey_complete"); Bool("persist.story_gate_activated", "gate_ark_start_active");
            Item("item_ancient_data_log", "item_ancient_data_log"); Clue("lore_nanite_storms", "clue_ark_mission_12");
            Item("blueprint_glass_beam", "item_blueprint_glass_beam", "reveal_clue clue_vanguard_spire");
            Clue("vanguard_charts", "rumor_silence_incursions");
            Bool("trait_existential_dread", "trait_existential_dread", FactFlags.Journal, "reveal_clue clue_seen_the_loop");
            Clue("poi_satellite_array", "rumor_satellite_array"); Item("item_seed_bank", "item_seed_bank");
            Item("item_genetic_stimulants", "item_genetic_stimulants"); Clue("map_reveal_2", "rumor_two_sectors_ahead");
            Item("item_revival_injector", "item_reality_anchor"); Integer("rep_ark"); Item("item_research_data", "item_research_data");
            Item("weapon_scrap_cannon", "item_scrap_cannon"); Bool("morale_boost", "morale_boost", FactFlags.Journal);
            Clue("lore_great_panic", "clue_ark_mutiny"); Item("item_terra_seed", "item_terraforming_seed");
            Item("blueprint_vanguard_plating", "item_blueprint_vanguard_plating", "reveal_clue clue_vanguard_forge");
            Item("weapon_mining_beam_mk2", "item_mining_beam_mk2"); Integer("rep_vanguard_ghosts");
            Bool("trait_solar_charged_engines", "trait_solar_charged_engines", FactFlags.Journal);
            Item("resource_psycho_gas", "item_psycho_gas"); Bool("trait_shaken", "trait_shaken", FactFlags.Journal);
            Clue("map_hidden_planet", "rumor_hidden_planet"); Item("item_vanguard_thruster", "item_vanguard_thruster");
            Item("upgrade_stealth", "item_stealth_patch", "reveal_clue clue_message_from_self");
            Clue("lore_fragmented_warning", "clue_fragmented_warning"); Clue("signal_found", "rumor_hidden_research_station");
            Item("ai_data", "item_ai_core_data"); Clue("cache_location", "rumor_hidden_cache");
            Bool("station_shady_truth", "station_boarding_trial_decode_tip"); Bool("pirate_bounty", "pirate_bounty_accepted", FactFlags.Journal);
            Bool("has_hacking", "has_hacking", FactFlags.Debug | FactFlags.WrittenByCode);
            // Code owns has_hacking; it is only read by this legacy export.
            var hacking = rows["has_hacking"]; rows["has_hacking"] = new FlagRow(hacking.Condition, hacking.Fact);
            rows.Add("quest.ark_material_turnin.turnedin", rows["story_survey"]);
            Bool("quest.station_data.submitted", "station_data_delivery_submitted");
            Bool("quest.relation_watch.report", "relation_watch_report_submitted");
            Bool("quest.hostile_blackbox.turnin", "hostile_blackbox_turned_in");
            var research = Declaration("research_points", FactKind.Int, FactFlags.Journal);
            rows.Add("research_points", new FlagRow("$research_points > 0", research));
            var debug = Declaration("debug_menus", FactKind.Bool, FactFlags.Debug | FactFlags.WrittenByCode, FactScope.Session);
            rows.Add("debug_menus", new FlagRow("$debug_menus", debug));
            return new ReadOnlyDictionary<string, FlagRow>(rows);

            void Bool(string key, string name, FactFlags flags = FactFlags.None, params string[] extra)
            {
                var commands = new List<string> { "set_fact " + name + " true" }; commands.AddRange(extra);
                rows.Add(key, new FlagRow("$" + name, Declaration(name, FactKind.Bool, flags), commands.ToArray()));
            }
            void Integer(string name) => rows.Add(name, new FlagRow("$" + name + " > 0", Declaration(name, FactKind.Int, FactFlags.Journal), "add_fact " + name + " 1"));
            void Item(string key, string id, params string[] extra)
            {
                var commands = new List<string> { "give_item " + id + " 1" }; commands.AddRange(extra);
                rows.Add(key, new FlagRow("has_item(" + Quote(id) + ", 1)", null, commands.ToArray()));
            }
            void Clue(string key, string id) => rows.Add(key, new FlagRow("clue_known(" + Quote(id) + ")", null, "reveal_clue " + id));
        }
        internal static FactDeclaration Declaration(string name, FactKind kind, FactFlags flags = FactFlags.None, FactScope scope = FactScope.Save) =>
            new FactDeclaration { Name = name, Kind = kind, Scope = scope, Flags = flags, Note = "Converted legacy narrative fact: " + name,
                JournalText = (flags & FactFlags.Journal) == 0 ? "" : name, FormerNames = Array.Empty<string>() };
    }
}
