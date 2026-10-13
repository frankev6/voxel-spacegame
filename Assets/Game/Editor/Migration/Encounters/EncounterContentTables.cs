using System;
using System.Collections.Generic;

namespace Game.Editor.Migration.Encounters
{
    /// <summary>SPEC-33 ENC-010 authored final content; no legacy tuning or invented asset substitutes.</summary>
    internal static class EncounterContentTables
    {
        internal readonly struct Ship
        {
            internal readonly string Id, Ai;
            internal readonly int Min, Max;
            internal Ship(string id, int min, int max, string ai = null) { Id = id; Min = min; Max = max; Ai = ai; }
        }
        internal sealed class Row
        {
            internal readonly string Id, Name, Faction, Legacy;
            internal readonly Game.Data.Definitions.EncounterKind Kind;
            internal readonly IReadOnlyList<Ship> Ships;
            internal readonly float Weight, DistanceMin, DistanceMax;
            internal readonly int DangerMin, DangerMax;
            internal Row(string id, string name, string faction, Game.Data.Definitions.EncounterKind kind, string legacy,
                Ship[] ships, float weight = 0, int dangerMin = 0, int dangerMax = 3, float distanceMin = 1500, float distanceMax = 2500)
            { Id = id; Name = name; Faction = faction; Kind = kind; Legacy = legacy; Ships = Array.AsReadOnly(ships); Weight = weight;
                DangerMin = dangerMin; DangerMax = dangerMax; DistanceMin = distanceMin; DistanceMax = distanceMax; }
        }
        internal static readonly IReadOnlyList<Row> Rows = Array.AsReadOnly(new[]
        {
            new Row("encounter_raiders", "Raiders", "faction_raiders", Game.Data.Definitions.EncounterKind.Ambush, "npc_enemy_1",
                new[] { new Ship("ship_fighter", 2, 3) }, 1, 1, 3, 1600, 2200),
            new Row("encounter_xeno_probe_tutorial", "Xeno Probe Tutorial", "faction_xeno", Game.Data.Definitions.EncounterKind.Story, "npc_probe_tutorial",
                new[] { new Ship("ship_frigate", 3, 3) }),
            new Row("encounter_whisper_hunter", "Whisper Hunter", "faction_raiders", Game.Data.Definitions.EncounterKind.Story, "npc_story_hunter",
                new[] { new Ship("ship_red_elite", 1, 1) }),
            new Row("encounter_traveling_merchant", "Traveling Merchant", "faction_vestari", Game.Data.Definitions.EncounterKind.Merchant, "npc_traveling_merchant",
                new[] { new Ship("ship_freighter", 1, 1, "ai_civilian") }, .5f, 0, 2),
            new Row("encounter_glass_worms", "Glass Worms", "faction_xeno", Game.Data.Definitions.EncounterKind.Story, null,
                new[] { new Ship("ship_beetle", 3, 3) }),
            new Row("encounter_doppelganger", "Doppelganger", "faction_xeno", Game.Data.Definitions.EncounterKind.Story, null,
                new[] { new Ship("ship_crescent", 1, 1) }),
            new Row("encounter_scavenger_king", "Scavenger King", "faction_raiders", Game.Data.Definitions.EncounterKind.Story, null,
                new[] { new Ship("ship_bastion", 1, 1), new Ship("ship_fighter", 2, 2) }),
            new Row("encounter_jungle_beasts", "Jungle Beasts", "faction_xeno", Game.Data.Definitions.EncounterKind.Story, null,
                new[] { new Ship("ship_beetle", 4, 4) }),
            new Row("encounter_solar_parasite", "Solar Parasite", "faction_xeno", Game.Data.Definitions.EncounterKind.Story, null,
                new[] { new Ship("ship_butterfly", 2, 2) }),
            new Row("encounter_extraction_guardians", "Extraction Guardians", "faction_raiders", Game.Data.Definitions.EncounterKind.Story, null,
                new[] { new Ship("ship_striker", 2, 2), new Ship("ship_frigate", 1, 1) })
        });
    }
}
