using System.Collections.Generic;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2; the rows transcribe §4.4. Aliases of another key carry a Note starting with "alias of".</summary>
    public sealed class LegacyIdMap
    {
        static readonly LegacyIdMapping[] Rows =
        {
            new LegacyIdMapping(LegacyIdKind.Block, "block_hull", "block_hull", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_hull_angled", "block_hull_angled", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_core_small", "block_core_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_core_cross", "block_core_cross", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_capacitor_small", "block_capacitor_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_shield_small", "block_shield_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_portal_small", "block_portal_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_prismatic_mirror_small", "block_prismatic_mirror_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_prismatic_mirror_fixed_small", "block_prismatic_mirror_fixed_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_thruster_small", "block_thruster_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_thruster_medium", "block_thruster_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "block_thruster_large", "block_thruster_large", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_plasma_turret_small", "weapon_plasma_turret_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_plasma_turret_small_fixed", "weapon_plasma_turret_small_fixed", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_plasma_turret_medium", "weapon_plasma_turret_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_small_beam_turret", "weapon_small_beam_turret", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_fixed_beam_turret_small", "weapon_fixed_beam_turret_small", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_railgun_medium", "weapon_railgun_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_ark_lance_medium", "weapon_ark_lance_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_flak_cannon_medium", "weapon_flak_cannon_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Block, "weapon_missile_launcher_small", "weapon_missile_launcher_small", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_plasma_turret_small", "wpn_plasma_turret_small", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_plasma_turret_small_fixed", "wpn_plasma_turret_small_fixed", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_plasma_turret_medium", "wpn_plasma_turret_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_small_beam_turret", "wpn_small_beam_turret", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_fixed_beam_turret_small", "wpn_fixed_beam_turret_small", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_railgun_medium", "wpn_railgun_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_ark_lance_medium", "wpn_ark_lance_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_flak_cannon_medium", "wpn_flak_cannon_medium", ""),
            new LegacyIdMapping(LegacyIdKind.Weapon, "weapon_missile_launcher_small", "wpn_missile_launcher_small", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "item_regeneration_serum", "item_regeneration_serum", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "item_research_blueprint", "item_research_blueprint", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "item_strange_matter", "item_strange_matter", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "resource_crew", "resource_crew", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "resource_fuel", "resource_fuel", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "module_armor_plating", "module_armor_plating", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "module_coolant_injector", "module_coolant_injector", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "module_scrap_collector", "module_scrap_collector", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "module_increase_weapon_rotation", "module_increase_weapon_rotation", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "module_micro_plating", "module_micro_plating", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "module_shield_efficiency", "module_shield_efficiency", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "module_trained_crew", "module_trained_crew", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "officer_august", "officer_august", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "officer_gunner_vax", "officer_gunner_vax", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "palladium_resource", "resource_palladium", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "slot_expansion_item", "item_slot_expansion", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "strange_matter_container_empty", "item_strange_matter_container_empty", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "strange_matter_container_full", "item_strange_matter_container_full", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_00_Default", "paint_00_default", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_01_DarkGray", "paint_01_dark_gray", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_02_Red", "paint_02_red", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_03_Green", "paint_03_green", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_04_Blue", "paint_04_blue", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_05_Yellow", "paint_05_yellow", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_06_Orange", "paint_06_orange", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_07_Purple", "paint_07_purple", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_08_Cyan", "paint_08_cyan", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_09_Pink", "paint_09_pink", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_10_Brown", "paint_10_brown", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_11_Black", "paint_11_black", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_12_MilitaryGreen", "paint_12_military_green", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_13_SteelBlue", "paint_13_steel_blue", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_14_Tan", "paint_14_tan", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_15_DarkRed", "paint_15_dark_red", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "PaintBucket_16_White", "paint_16_white", ""),
            new LegacyIdMapping(LegacyIdKind.Item, "717185106", "item_strange_matter_container_full", "alias of strange_matter_container_full: orphaned numeric Id (audit 05-B2), narrative only"),
            new LegacyIdMapping(LegacyIdKind.Item, "500", "resource_palladium", "alias of palladium_resource: legacy raw materials, narrative only (open question 3)"),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_base", "ship_base", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_bastion", "ship_bastion", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_beetle", "ship_beetle", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_butterfly", "ship_butterfly", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_crescent", "ship_crescent", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_fighter", "ship_fighter", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_freighter", "ship_freighter", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_frigate", "ship_frigate", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_icarus", "ship_icarus", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_red_elite", "ship_red_elite", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_shuttle", "ship_shuttle", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_striker", "ship_striker", ""),
            new LegacyIdMapping(LegacyIdKind.Ship, "ship_vestari", "ship_vestari", ""),
            new LegacyIdMapping(LegacyIdKind.Faction, "faction_khuchad_clan", "faction_khuchad_clan", ""),
            new LegacyIdMapping(LegacyIdKind.Faction, "faction_tlaxcala", "faction_tlaxcala", ""),
            new LegacyIdMapping(LegacyIdKind.Faction, "faction_vestari", "faction_vestari", ""),
            new LegacyIdMapping(LegacyIdKind.Faction, "faction_4", null, "duplicate of faction_tlaxcala, referenced nowhere"),
            new LegacyIdMapping(LegacyIdKind.Ability, "ability_shield_toggle", "ability_shield_toggle", ""),
            new LegacyIdMapping(LegacyIdKind.Ability, "1", "ability_shield_toggle", "alias of ability_shield_toggle: legacy <ID>"),
            new LegacyIdMapping(LegacyIdKind.Ability, "ability_speed_boost", "ability_speed_boost", ""),
            new LegacyIdMapping(LegacyIdKind.Ability, "2", "ability_speed_boost", "alias of ability_speed_boost: legacy <ID>"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_Button_Click", "cue_ui_click", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_Button_Hover", "cue_ui_hover", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_Slot_Click", "cue_ui_slot_click", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_Slot_Hover", "cue_ui_slot_hover", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_View_Open", "cue_ui_view_open", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_View_Close", "cue_ui_view_close", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_Fail", "cue_ui_fail", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "UI_Success", "cue_ui_success", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Weapon_Laser_Fire", "cue_weapon_laser_fire", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Weapon_Plasma_Fire", "cue_weapon_plasma_fire", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Weapon_Cannon_Fire", "cue_weapon_cannon_fire", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Impact_Hull", "cue_impact_hull", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Impact_Explosion_Small", "cue_explosion_small", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Impact_Explosion_Large", "cue_explosion_large", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Editor_Place_Voxel", "cue_editor_place", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Editor_Remove_Voxel", "cue_editor_remove", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Combat_Standard", "cue_music_combat", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "Exploration", "cue_music_exploration", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "MainMenu", "cue_music_menu", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "SpaceStation", "cue_music_station", "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.AudioCue, "None", null, "SPEC-64 §6.4"),
            new LegacyIdMapping(LegacyIdKind.LootTable, "BaseLootTable", "loot_base", ""),
            new LegacyIdMapping(LegacyIdKind.LootTable, "CreativeModeLootTable", "loot_creative", ""),
            new LegacyIdMapping(LegacyIdKind.LootTable, "StartShipsTable", null, "empty, dead"),
            new LegacyIdMapping(LegacyIdKind.Body, "star_entity", "body_star", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "star_entity_blackhole", "body_star_blackhole", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "planet_barren", "body_planet_barren", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "planet_gasgiant", "body_planet_gas_giant", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "planet_ice", "body_planet_ice", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "planet_icegiant", "body_planet_ice_giant", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "planet_rocky", "body_planet_rocky", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "planet_terrestrial", "body_planet_terrestrial", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "planet_water", "body_planet_water", "by $name only: its entityId duplicates planet_ice"),
            new LegacyIdMapping(LegacyIdKind.Body, "moon_1", "body_moon", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "satelite_1", "body_moon", "alias of moon_1"),
            new LegacyIdMapping(LegacyIdKind.Body, "station_1", "body_station", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "ark_station_1", "body_station_ark", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "ark_station", "body_station_ark", "alias of ark_station_1"),
            new LegacyIdMapping(LegacyIdKind.Body, "abandoned_station", "body_station_abandoned", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "asteroidbelt_1", "body_asteroid_belt", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "exitgate_active", "body_gate_active", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "exitgate_dormant", "body_gate_dormant", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "hyperlane_default", "body_jump_point", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "derelict_ship_entity", "body_derelict_ship", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "derelict_ship", "body_derelict_ship", "alias of derelict_ship_entity"),
            new LegacyIdMapping(LegacyIdKind.Body, "debris_field_entity", "body_debris_field", ""),
            new LegacyIdMapping(LegacyIdKind.Body, "derelict_ship_1", "body_debris_field", "alias of debris_field_entity"),
            new LegacyIdMapping(LegacyIdKind.Body, "resrource_cache_entity", "body_resource_cache", ""),
            new LegacyIdMapping(LegacyIdKind.Encounter, "npc_enemy_1", "encounter_raiders", ""),
            new LegacyIdMapping(LegacyIdKind.Encounter, "npc_probe_tutorial", "encounter_xeno_probe_tutorial", ""),
            new LegacyIdMapping(LegacyIdKind.Encounter, "npc_story_hunter", "encounter_whisper_hunter", ""),
            new LegacyIdMapping(LegacyIdKind.Encounter, "npc_traveling_merchant", "encounter_traveling_merchant", ""),
            new LegacyIdMapping(LegacyIdKind.Encounter, "encounter_combat_01", null, "formations folded into the fleets (§4.7)"),
            new LegacyIdMapping(LegacyIdKind.Encounter, "encounter_combat_probe_tutorial", null, "formations folded into the fleets (§4.7)"),
            new LegacyIdMapping(LegacyIdKind.Encounter, "encounter_boss", null, "formations folded into the fleets (§4.7)"),
            new LegacyIdMapping(LegacyIdKind.StarSystem, "ark_start", null, "authored star systems are SPEC-30's; values handed off"),
            new LegacyIdMapping(LegacyIdKind.StarSystem, "base_solar_system", null, "authored star systems are SPEC-30's; values handed off"),
        };

        public static LegacyIdMap Default { get; } = new LegacyIdMap(Rows);                   // §4.4, immutable

        readonly LegacyIdMapping[] all;
        readonly Dictionary<(LegacyIdKind, string), string> byKey = new Dictionary<(LegacyIdKind, string), string>();

        LegacyIdMap(LegacyIdMapping[] rows)
        {
            all = rows;
            foreach (var r in rows) byKey.Add((r.Kind, r.LegacyKey), r.V2Id);   // a duplicate legacy key is a transcription bug: throws
        }

        public IReadOnlyList<LegacyIdMapping> All => all;

        /// <summary>False if the key is unknown; true with null for "not converted".</summary>
        public bool TryMap(LegacyIdKind kind, string legacyKey, out string v2Id)
        {
            v2Id = null;
            return legacyKey != null && byKey.TryGetValue((kind, legacyKey), out v2Id);
        }

        /// <summary>For narrative DSL tokens: tries Item, Block, Ship in that order (whitespace trimmed); accepts numeric legacy IDs.</summary>
        public bool TryMapItemToken(string token, out string v2Id, out LegacyIdKind kind)
        {
            var key = token?.Trim();
            foreach (var k in new[] { LegacyIdKind.Item, LegacyIdKind.Block, LegacyIdKind.Ship })
                if (TryMap(k, key, out v2Id)) { kind = k; return true; }
            v2Id = null; kind = LegacyIdKind.Item;
            return false;
        }
    }
}
