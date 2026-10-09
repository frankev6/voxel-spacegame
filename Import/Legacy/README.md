# Legacy content (read-only converter input)

Exported on 2026-10-09 from the old prototype (`spaceship-game`) so v2 keeps the owner's content without any old code.
Nothing here is used at runtime. The converters in `docs/specs/SPEC-72-legacy-migration.md` (and the narrative
converter in SPEC-40) turn it into v2 content assets under `Assets/Game/Content/`.

| Folder | What | Notes |
|---|---|---|
| `content/` | Every old ScriptableObject (161) as neutral JSON: blocks, ships with voxel lists, items, consumables, modules, paint, factions, galaxy and body templates, encounters, loot tables, abilities, audio library | `{"$ref": "Assets/..."}` values point into the OLD project and are hints only. Anything referencing "Starsector Visuals" is legally unusable art |
| `narrative/` | `NarrativeEvents.json`, `StoryEvents.json`, `QuestDefinitions.json`, `QuestTriggers.json`, `MissionTemplates.json` — all the owner's writing | The most valuable content; convert without losing a line |
| `ui/` | Old UXML/USS | Layout reference only; the look is placeholder |
| `input/` | Old Input System actions | Reference for familiar key bindings |

Do not edit these files. If a converter needs a fix in the data, fix the converter.
