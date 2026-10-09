# 05 — Narrative, Quests, Dialog, Event Bus, Content Pipeline

Auditor scope: `Assets/Game/Core/Quests/*`, `Assets/Game/Core/Dialog/*`, `Assets/Game/Core/Managers/DialogManager.cs`, `Assets/Game/UI/Pages/NarrativeView.cs`, `Assets/Game/UI/Pages/IntelView.cs`, `Assets/Game/Editor/QuestJsonImporter.cs`, `Assets/Game/Editor/NarrativeJsonImporter.cs`, `Assets/Game/Data/Narrative/*.json`, `Assets/Game/Data/NarrativeEvents.csv`, `Assets/Resources/Quests/*`, `Assets/Resources/Encounters/*`, `Assets/Localization/*`, `Assets/Game/Visuals/Cutscenes/*`. Light touch on `EncounterManager.cs`, entity interaction code and `GameDatabase` where they feed narrative.

All paths are relative to the repo root. Every source file in scope was read in full.

**Evidence legend:** **[V]** = verified (read the code, grep, GUID search, or git history). **[I]** = inference or judgement; it's labelled when it isn't obvious.

---

## 0. Executive summary

* **About 8,900 lines across 23 files [V]**, made up of four action vocabularies, five condition dialects, two flow controllers (DialogManager and NarrativeView both track "current encounter/page"), two importers with different JSON libraries, and three data stores (EncounterManager world state, QuestManager history sets, GameDatabase faction relations). This is too much machinery for about 34 short encounters and 11 quests.
* **The open-world refactor left the quest system disconnected from the world [V].** Commit `1069164e` deleted the only publishers of `system.entered`, `territory.entered` and `entity.interacted`, and nothing replaced them. As a result, three of the seven objective types and both territory triggers can never fire.
* **The content references a removed item-ID scheme [V].** Commit `3c3216b2` ("item id removal") replaced numeric IDs with linear runtime IDs 0..73, but the content still uses `500` and `717185106`. The gate activation, the survey turn-in and every "collect" quest are impossible to complete.
* **Several softlocks are reachable from the ARK station hub [V]**: PLAY_FROM_POOL with no pools, START_COMBAT pointing at missing assets, a missing page, and stale dialog state after START_COMBAT or OPEN_SHIP_EDITOR.
* **Localization is effectively non-functional [V].** The importer never saves `SharedTableData`, so `GameText_en` holds 3,599 orphaned entries (each string duplicated about 25 times) against 5 real keys.
* **Quests complete retroactively [V].** Untargeted objectives, and targets you've already met (for example the probe you destroyed in the tutorial), complete instantly when a quest is accepted, because they are checked against lifetime history.
* **What's worth keeping:** the *writing*, which already contains a strong mystery skeleton (First Ark, the Silence, the Vanguard, the gates, "messages from yourself"). The writer already uses about 40 write-only `lore_*`/`map_*`/`rep_*` flags as proto-clues. Also worth keeping are the importer's path-qualified validation pattern and the "contextual quest options injected into dialog" idea.
* **Headline recommendation:** replace all of this with a single **Narrative Director** built on a **typed Fact Store (blackboard)** and an **Outer-Wilds-style Clue/Rumor graph**. Story beats become storylets selected by conditions over facts, and quests become declarative views over facts with accept-time baselines. Use **one shared Condition/Effect vocabulary** for dialog, quests, triggers, encounter selection and entity spawns. Author dialog in **Yarn Spinner 3**, with its variable storage backed by the Fact Store. Save state as *facts + quest stage indices + known clues*. Never save definition text.

---

## 1. Current architecture map

### 1.1 Content authoring pipeline [V]

```
Assets/Game/Data/NarrativeEvents.csv        (orphan: no reader, stale 27/34 subset)
Assets/Game/Data/Narrative/
  NarrativeEvents.json  (29 encounters)  ─┐   Tools/Import Narrative JSON
  StoryEvents.json      (5 encounters)   ─┼─► NarrativeJsonImporter (JsonUtility + regex action DSL)
                                          │      ├─► Assets/Resources/Encounters/Narrative/<id>.asset  (NarrativeEncounter SO ×34)
                                          │      └─► Assets/Localization/GameText_en.asset (en only; SharedData never saved)
  QuestDefinitions.json (11 quests)      ─┐   Tools/Import Quest JSON
  QuestTriggers.json    (4 triggers)     ─┼─► QuestJsonImporter (Newtonsoft + validation)
  MissionTemplates.json (3 templates)    ─┘      └─► Assets/Resources/Quests/{QuestCatalog,QuestTriggerCatalog,MissionTemplateCatalog}.asset
```

* Narrative options use a string DSL: `actions: "SET_VAR(k, v); CUSTOM_ACTION(id, k=v|k=v); GOTO(page)"` and `showReq/enableReq: "REQ_ITEM(500, 3)"`. These are parsed by regex at `Assets/Game/Editor/NarrativeJsonImporter.cs:398-529`, and requirement strings are kept *unparsed* until runtime.
* Quests use structured JSON (condition trees with `all/any/not`, enums parsed leniently). They are converted to `[Serializable]` classes with `[SerializeReference] ConditionNode` (`Assets/Game/Core/Quests/QuestModels.cs:69-235`).
* The generated assets are in sync with the JSON. I checked quest IDs and encounter IDs; there are 34 assets and 34 JSON entries, with no orphans [V].

### 1.2 Runtime flow [V]

```
Entity.OnInteract()  (OpenWorldSolarSystemEntityInstance.cs:133-153 / EcsSolarSystemEntityInstance.cs:85-103)
   └─ entityData.GetRandomEncounter() (SolarSystemEntity.cs:79-108, EncounterPool)
   └─ EncounterManager.StartEncounter (EncounterManager.cs:163-193)
        └─ UIManager.ToggleNarrativeView(true) (UIManager.cs:301-314)
             └─ NarrativeView.StartEncounter (NarrativeView.cs:104-129)
                  └─ DialogManager.StartDialog → ShowPage → OnPageChanged → NarrativeView.ShowPage/DrawPage
                        DrawPage pulls QuestManager.GetDynamicOptionsForContext (QuestManager.cs:668-749)
                        and DialogManager.CheckRequirement / CanAffordOption for each option
   click → NarrativeView.OnOptionSelected → DialogManager.OnOptionSelected → ProcessOutcomes (DialogManager.cs:158-230)
        flow outcomes (GOTO/PUSH/RETURN/RANDOM/NESTED/POOL/END/TRADE/COMBAT/REPAIR/EDITOR) handled in DialogManager
        everything else → NarrativeActionEngine.Execute (NarrativeActionRuntime.cs:193-288)
             CustomAction → EncounterManager.ActionRegistry (NarrativeActionRegistry, 16 string-ID handlers)
                  quest.* handlers (QuestNarrativeActionHandlers.cs) → QuestManager
   END → NarrativeView.EndEncounterFull + DialogManager.EndDialog + EncounterManager.EndEncounter (DialogManager.cs:319-349)
        EncounterManager.EndEncounter → GameEventBus.Publish("encounter.ended") + checkpoint save (EncounterManager.cs:302-316)

GameEventBus (static, string-typed)  ──► QuestManager.HandleGameEvent (QuestManager.cs:879-906)
                                          ├─ RecordEventHistory (lifetime sets)
                                          ├─ EvaluateQuestObjectives (event-count objectives + custom conditions)
                                          └─ QuestTriggerEngine.HandleEvent → QuestActionExecutor (15 enum actions)
                                     ──► EncounterManager.HandleSaveLoadEvent (save/load only)

Queued encounters: QuestActionExecutor.QueueEncounter / "flow.queue_encounter"
   → EncounterManager.QueueNarrativeEncounter → TryStartQueuedEncounter (Resources.Load by id) (EncounterManager.cs:605-652)
```

### 1.3 State and persistence [V]

| State | Owner | Saved in | Notes |
|---|---|---|---|
| Run world state (`Dictionary<string,string>`, case-sensitive) | `EncounterManager.runWorldState` (`EncounterManager.cs:42`) | `CampaignState.WorldState` | Polluted with `interacted_<instanceId>`/`destroyed_<instanceId>` keys (`OpenWorldSolarSystemEntityInstance.cs:150,163`) |
| Persistent world state (`persist.*` prefix) | `EncounterManager.persistentWorldState` (`:45`, `:746-749`) | `CampaignState.PersistentWorldState` | Roguelike "meta" concept that survives `InitializeFreshRun` |
| Magic keys `run.intro_phase`, `run.first_spawn_anchor`, `run.skip_*` | special-cased in `SetVariable/GetVariable` (`EncounterManager.cs:667-733`) | IntroPhase field | Legacy bridge |
| Seen encounters | `EncounterManager.SeenEncounters` | `CampaignState.SeenEncounters` | Marked twice per start (`NarrativeView.cs:117-120` and `DialogManager.cs:91-94`) |
| Quest instances + event history sets + fired triggers + template sequence | `QuestManager` (`QuestManager.cs:30-45`) | `CampaignState.QuestState` (`QuestRunStateData`, `QuestModels.cs:359-464`) | Captured on `save.before`, restored on `save.data_loaded` (`QuestManager.cs:879-901`) |
| Faction relations | `GameDatabase` | `CampaignState.FactionRelations` | Publishes `faction.relation_changed` (`GameDatabase.cs:432-441`) |
| Consumed options | `DialogManager.consumedOptionKeys` | not saved (session) | Never read (see B6) |

### 1.4 Presentation [V]

* `NarrativeView` (UI Toolkit) renders pages, options, requirement/outcome icons, quest-styled options, and 1–9 keyboard shortcuts.
* `IntelView` is the quest log. It lists accepted quests, objectives and logs, and has Track and Delete buttons.
* `GalaxyView` shows tracked objective lines and minimap quest markers. It calls `QuestManager.IsStoryOrQuestEntity` for every entity every frame (`GalaxyView.cs:52-54`, `GalaxyView.Map.cs:133`).
* Quest notifications are hard-coded English strings (`QuestManager.cs:220,254,286,312,1377-1386`).

### 1.5 What uses what (GUID / reference check) [V]

| Script / asset | Referenced by |
|---|---|
| QuestManager, DialogManager, NarrativeView, IntelView | `Assets/Game/Prefabs/Managers/GAME MANAGERS PREFAB.prefab` (instantiated by `GameInitializer`) |
| NarrativeEncounter | 34 assets in `Assets/Resources/Encounters/Narrative` |
| QuestCatalog / QuestTriggerCatalog / MissionTemplateCatalog | 1 generated asset each, loaded via `Resources.Load` path (`QuestManager.cs:102-124`) |
| **NarrativePool** | **0 assets**; `Resources/Encounters/Pools` does not exist |
| CombatEncounter assets (5) | 3 referenced by NPC entities. However, `EncounterManager.StartEncounter` rejects non-narrative encounters (`EncounterManager.cs:180-191`), so they are effectively dead |
| `Visuals/Cutscenes/SpaceStationTimeline.playable` | **no references** |
| `Visuals/Cutscenes/Galaxy CutsceneTimeline.playable` | only `Assets/Game/Prefabs/Galaxy/Galaxy.prefab`, which no scene or prefab references (likely dead) |

---

## 2. Bugs

Severity: **Critical** (blocks progression or corrupts data), **High**, **Medium**, **Low**. Confidence is given per item.

### B1. Three objective types and both territory triggers can never fire: their events have no publisher. **Critical, confidence high [V]**
* `GameEventTypes.SystemEntered`, `FactionTerritoryEntered` and `EntityInteracted` are declared in `Assets/Game/Core/Quests/GameEventBus.cs:7-9`. A whole-tree grep finds **no publisher** for any of them. The only publishers are for `inventory.*` (`InventoryNotificationRelay.cs:34,55`), `faction.relation_changed` (`GameDatabase.cs:434`), `encounter.ended` (`DialogManager.cs:326`, `EncounterManager.cs:302`), `entity.destroyed` (`EncounterManager.cs:863`, `OpenWorldSolarSystemEntityInstance.cs:167`, `EcsSolarSystemEntityInstance.cs:117`), and the save events.
* Git history: commit `1069164e` ("removal of old solar system/galaxy…") deleted the publishers in `GalaxyManager.SolarSystem.cs` (SystemEntered and FactionTerritoryEntered, with a `firstVisit` payload) and `SolarSystemEntityInstance.cs` (EntityInteracted). The new open-world code only calls `OpenWorldGalaxyManager.MarkEntityInteracted` (`OpenWorldSolarSystemEntityInstance.cs:141-144`), which publishes nothing.
* Impact:
  * `ReachSystem`, `VisitFactionTerritory` and `InteractEntity` objectives never progress (`QuestManager.cs:987-1027`). Their history sets `visitedSystemKeys`, `visitedFactionIds` and `interactedEntityKeys` stay empty.
  * The dead objectives are: `story_chain_signal_1`, `story_chain_signal_3` (both of its objectives), `quest_territory_recon.report_back`, `quest_relation_watch.territory_seen`, `quest_trigger_fail_test`, and `tmpl_signal_relay.reach_any_system`.
  * `trigger_first_friendly_territory` and `trigger_first_hostile_territory` never fire (`QuestTriggers.json`), so `event_territory_friendly_entry` and `event_territory_hostile_entry` are unreachable.
  * The **Whisper Chain storyline cannot be completed.**

### B2. Content uses item IDs from a removed scheme: gate activation, survey turn-in and every collect quest are impossible. **Critical, confidence high [V]**
* Runtime item IDs are now linear array indices (`int runtimeId = _runtimeIdToItem.Count;`, `GameDatabase.cs:221`) over 74 items (`find Assets/Resources/Items -name '*.asset' | wc -l` = 74). The numeric `Id` field was removed in commit `3c3216b2` ("item id removal"). `717185106` still appears as orphaned YAML (`Id: 717185106`) in `Assets/Resources/Items/Modules/strange_matter_container_full.asset:15`, but `ResourceItem` no longer has that field (`Assets/Game/Core/Game Logic/ResourceItem.cs:7-37`).
* Content still uses `500` and `717185106`:
  * `story_activate_gate`: `enableReq REQ_ITEM(717185106, 1)` (StoryEvents.json). `GetItemCount(717185106)` is always 0, so the **gate can never be reactivated**.
  * `story_survey_system`: `REQ_ITEM(500, 3)` / `REMOVE_ITEM(500,3)` / `ADD_ITEM(717185106,1)`. `Inventory.TryAddItem(int)` logs "No ResourceItem found" (`Inventory.cs:451-461`).
  * `event_stable_point`: `REQ_ITEM(717185106, 1)`.
  * Quests `quest_ark_material_turnin`, `quest_station_data_delivery` and `tmpl_material_sweep` use `collect_item` with `"targetId": "500"`. Index 500 doesn't exist, so the count is always 0, the objective never completes, and the turn-in option (whose show-condition requires that objective) never appears.
  * `demo_conditional_options`: `REQ_ITEM(500, 1)`.
* Neither importer validates item references.

### B3. Retroactive and instant objective completion from lifetime history. **High, confidence high [V]**
* Every `AcceptQuest` and `GenerateMissionFromTemplate` calls `EvaluateQuestObjectives(null)` (`QuestManager.cs:221,255`). With no event, this falls into `TryAdvanceObjectiveFromHistory` (`QuestManager.cs:978-981, 1068-1158`):
  * Untargeted `CompleteEncounter` sets progress to **`completedEncounterIds.Count`** (`:1108`), the number of encounters completed in the whole save.
  * Untargeted `InteractEntity`/`DestroyEntity` sets progress to `Count > 0 ? 1 : 0` (`:1084`, `:1096`).
  * Targeted `DestroyEntity` completes if that entity ID was *ever* destroyed (`:1098-1101`).
* Concrete cases:
  * `tmpl_bounty_probe` targets `npc_probe_tutorial`, which the tutorial spawns and the player destroys. Every bounty generated afterwards **completes on generation**.
  * `quest_hostile_blackbox.survive_contact`, `quest_territory_recon.fight_once`, `tmpl_material_sweep.survive_contact` and `tmpl_signal_relay.resolve_any_encounter` all complete instantly once any encounter has ever succeeded.
  * Once B1 is fixed, `story_chain_signal_3` ("return to ARK", "debrief at ARK") will also complete instantly, because the player starts at ARK.
* `CollectItem` uses `Mathf.Max(progress, currentCount)` (`:1048-1050`). Progress never goes down, so selling or using the items doesn't regress the objective, and items you already owned count.

### B4. Softlocks in dialog: buttons are disabled before processing and never re-enabled on failure. **High, confidence high [V]**
`NarrativeView.OnOptionSelected` sets `_isProcessingOption = true` and disables every button (`NarrativeView.cs:472-479`). Only a new `ShowPage` clears this (`:156`). Each of the following paths leaves the view open with no usable buttons:
* **PLAY_FROM_POOL**: `Resources/Encounters/Pools` does not exist and there are no `NarrativePool` assets [V]. `PlayFromPool` only logs an error (`DialogManager.cs:296-317`), and because `HasFlowOutcome` is true (`:241`) there's no fallback end. This is **reachable from the ARK station: Station Bar → Random Encounter** (`ark_station_1.asset` → `demo_station_hub` → `PLAY_FROM_POOL(bar_encounters)`).
* **Missing page** (GOTO/RANDOM_BRANCH typo, or `ResolveRandomBranch` falling back to `"end"`, `DialogManager.cs:278`): `ShowPage` calls `EndDialog()` only (`DialogManager.cs:111-116`). NarrativeView doesn't subscribe to `OnDialogEnded`, so the view stays up and `EncounterManager.activeEncounter` is never cleared.
* **START_COMBAT with a missing asset** calls `EndDialog()` only (`DialogManager.cs:589-593`). Five encounters reference non-existent combat assets [V]: `encounter_glass_worm` (event_glass_desert), `encounter_doppelganger` (event_time_echo), `encounter_pirate_boss` (event_scavenger_king), `encounter_jungle_beasts` (event_overgrown_forge), and `encounter_solar_parasite` (event_solar_parasite). Three of these events are placed on real planets or stars.
* **PLAY_NESTED with a missing asset**, and **TRADE when `lootTable` is null** (`DialogManager.cs:558-577`), behave the same way. This is latent today because both TRADE encounters have `BaseLootTable`.

### B5. START_COMBAT and OPEN_SHIP_EDITOR leave stale dialog and encounter state. **High, confidence medium-high [V]**
* `ResolveStartCombat` hides the view (`UIManager.ToggleNarrativeView(false)`) without calling `EndDialog` (`DialogManager.cs:584-588`). `DialogManager.currentEncounter` stays set, so `IsDialogActive` is true (`DialogManager.cs:58`).
  * `OpenWorldPlayerController.Update` locks and parents the player whenever `IsDialogActive` is true after any interaction (`OpenWorldPlayerController.cs:143-146`). The unlock only happens on `OnDialogEnded` (`:209-217`), which never fires. **[I]** If the next interaction target has no encounter, this is a likely player softlock.
  * The combat encounter itself is rejected (`EncounterManager.cs:180-191`, "Encounter is not a NarrativeEncounter class!") and auto-resolved as **success**. "Fight" therefore means instant win: the NPC is destroyed and `destroy_entity` objectives complete.
* `ResolveOpenShipEditor` (`DialogManager.cs:626-640`) hides the view and loads a scene without ending the encounter. `EncounterManager.activeEncounter` stays non-null, so `TryStartQueuedEncounter` refuses to run (`EncounterManager.cs:628`) until some later encounter ends. Quest `QueueEncounter` actions silently stall in the meantime.
* `StartDialog` doesn't clear `dialogStack` (`DialogManager.cs:86-88`; it's only cleared in `EndDialog`, `:438`). A nested dialog that's abandoned via either path will later "pop" into a previous encounter's page. This is latent with current content.

### B6. `consumeOnUse` is completely non-functional. **Medium, confidence high [V]**
* `DialogManager.ConsumeOption` records keys (`DialogManager.cs:470-474`), but `IsOptionConsumed` (`:479-483`) has **zero callers**, and `NarrativeView.DrawPage` never filters consumed options (`NarrativeView.cs:210-445`).
* Quest entity options are explicitly excluded even from recording (`isDynamic` check, `DialogManager.cs:150`), even though all four quest options in content set `consumeOnUse: true`.
* The guide documents this feature as working (`Assets/Game/Core/Dialog/Narrative_System_Guide.md:190-197`).

### B7. Removing a quest from the log lets you accept it again, including story quests. **Medium, confidence high [V]**
`IntelView` Delete calls `DeleteQuestRecord`, which removes completed or failed instances (`QuestManager.cs:383-398`). Uniqueness in `AcceptQuest` relies only on finding an existing instance (`QuestManager.cs:182-199`). After deletion, `GetQuestStatus` returns `NotAccepted`, so `REQ_QUEST_STATE(x, NotAccepted)` options reappear (demo_station_hub Mission Control) and onAccepted/onCompleted rewards and spawns can be repeated.

### B8. A renamed or removed quest definition auto-completes on load, with side effects during load. **Medium, confidence high [V]**
* `RebuildInstanceFromSave` falls back to `new QuestInstance()` with no objectives when the definition is missing (`QuestManager.cs:1516-1519`). `AreRequiredObjectivesComplete()` returns true for an empty list (`QuestModels.cs:266-269`). `LoadFromQuestStateData` then runs `EvaluateQuestObjectives(null)` (`QuestManager.cs:784`), which calls `CompleteQuest`, which shows a "Mission completed" toast and publishes `quest.completed` (which can fire triggers) *during save load*.
* More generally, any objective that history now satisfies completes on load and runs `onCompletedActions` (QueueEncounter, SpawnEntity) before the world scene exists.

### B9. Repeatable quests can never complete their second instance (latent). **Medium (latent), confidence high [V]**
* `AcceptQuest` creates every instance with `runtimeId = definition.id` (`QuestManager.cs:201`). `FindQuestInstance` returns the *first* instance whose `runtimeId` matches, regardless of status (`QuestManager.cs:1407-1419`). For a repeatable quest that means the old Completed instance, so `MarkObjectiveCompleted` and `CompleteQuest` both return false (`:320`, `:269`).
* No current quest is repeatable (all are `isUnique: true`). The feature is broken in place.

### B10. World-state-driven objectives only re-evaluate on unrelated events. **Medium, confidence high [V]**
* `EncounterManager.SetVariable` publishes nothing (`EncounterManager.cs:667-701`). `custom_condition` objectives such as `world_state quest.relation_watch.report == submitted` are only checked on the next bus event.
* The content works around this by pairing every `state.set` with an explicit `quest.complete_objective` (QuestDefinitions.json entity options; bar_shady_man), which shows the design is redundant.

### B11. Page audio plays twice. **Low, confidence high [V]**
`DialogManager.ShowPage` plays `audioClipName` (`DialogManager.cs:121-124`), and so does the `OnPageChanged` handler `NarrativeView.ShowPage` (`NarrativeView.cs:150-153`).

### B12. Localization table corruption: about 3,594 orphaned entries; nothing narrative is actually localized. **High (for loc), confidence high [V]**
* `Assets/Localization/GameText Shared Data.asset` has **5 keys** (leave/continue/attack/undock/trade). `GameText_en.asset` has **3,599** entries with unique IDs, and each narrative string appears about **25 times** (for example "Salvage Scrap" ×25). `GameText_es`/`GameText_fr` have **0** entries.
* Cause: `NarrativeJsonImporter` adds entries via `table.AddEntry` (`NarrativeJsonImporter.cs:386`) but only dirties `collection` and `englishTable` (`:192-196`), never `collection.SharedData`. The new keys are lost on reload while the table rows persist, and each import re-adds them.
* At runtime, every `story_waking_up_start_text`-style key is missing, and `NarrativeView.GetLocalizedText` silently falls back to inline text (`NarrativeView.cs:565-579`).
* Separate aliasing bug: when the expected key is absent, `GetOrGenerateKey` reuses **any key whose value has the same text** (`NarrativeJsonImporter.cs:374-384`). Two different options reading "Leave" share one key, so editing one later rewrites the other.
* Option keys are positional (`_opt{index}`, `:288`), so reordering options silently mismatches translations.

### B13. GameEventBus: one throwing subscriber starves the others (including save capture). **Medium, confidence high [V]**
`OnEventPublished?.Invoke` sits inside a single try/catch (`GameEventBus.cs:124-131`). A multicast delegate stops at the first exception. If `QuestManager.HandleGameEvent` throws on `save.before`, `EncounterManager.CaptureState` never runs and world state is missing from the save. Only `ex.Message` is logged (no stack trace).

### B14. Trigger re-entrancy: the "fired" flag is set after actions run. **Low-Medium (latent), confidence high [V]**
`QuestTriggerEngine.HandleEvent` runs `ExecuteMany(trigger.actions)` *before* `firedTriggerIds.Add` (`QuestTriggerEngine.cs:98-103`). Actions publish events synchronously (`AcceptQuest` → `quest.accepted`, `AdjustFactionRelation` → `faction.relation_changed`). A trigger whose actions re-raise its own event type fires twice even when `oncePerRun`, or recurses without bound when `oncePerRun:false`.

### B15. `trigger_station_boarding_followup` re-opens the station hub immediately. **Low, confidence medium [V+I]**
When `quest_station_boarding_trial` completes (inside `bar_shady_man`, nested in `demo_station_hub`), the trigger does `QueueEncounter demo_station_hub` (`QuestTriggers.json`). The queue starts it as soon as the current encounter ends (`EncounterManager.cs:323`), so undocking re-docks you. The same trigger also duplicates the quest's own `onCompletedActions` AcceptQuest.

### B16. Per-frame allocations and `Resources.Load` in quest marker checks. **Low-Medium (perf), confidence high [V]**
`GalaxyView.Update` → `UpdateSolarSystemMap` → for each entity `IsStoryOrQuestEntity` → `IsStoryNarrativeEntity` allocates a `List` (`QuestManager.cs:570`) and calls `Resources.Load` for `story_*` overrides (`:593`) every frame.

### B17. Smaller issues [V]
* `NarrativeView.StartEncounter` dereferences `EncounterManager.Instance` without a null check (`NarrativeView.cs:119`).
* Unknown requirement strings **fail open**: `DialogManager.CheckRequirement` returns true (`DialogManager.cs:838`), and so do unknown condition primitives (`QuestConditionEvaluator.cs:105-106`) and unknown spawn conditions (`EntitySpawnCondition.cs:64-66`). A typo makes an option always visible.
* Case sensitivity is inconsistent: `REQ_VAR` compares ordinally (`DialogManager.cs:731`), quest `world_state` compares ignoring case (`QuestConditionEvaluator.cs:271`), and the world-state dictionary keys are case-sensitive (`EncounterManager.cs:42`).
* An empty `Any` evaluates to true (`QuestConditionEvaluator.cs:42-45`).
* `RemoveItem` supports only integer IDs while `AddItem` supports keys too (`NarrativeActionRuntime.cs:290-314`; `CanAffordOption` likewise, `DialogManager.cs:514-521`).
* `ChangeHull`, `ChangeCrew` and `ChangeResearch` are **no-ops** (`NarrativeActionRuntime.cs:246-249`) yet are used in about 20 options across content. Players read "your hull is damaged" and nothing happens.
* `OutcomeType.SpawnEntity` drops count and anchor (`NarrativeActionRuntime.cs:267-270`), unlike `spawn.entity`.
* "No handler executed action" is logged when a handler *returned false* (for example completing an objective of an inactive quest), which is misleading (`NarrativeActionRuntime.cs:473-477`). `bar_shady_man` triggers it on every option.
* Template `string.Format(titleFormat, seq)` throws `FormatException` on any stray `{` in content (`MissionTemplateGenerator.cs:66-74`), and nothing validates it.
* `IntelView.HandleTrackToggle` toggles by `questId` while the rest of the view uses `runtimeId` (`IntelView.cs:346`).
* Quest log timestamps are wall-clock `DateTime.UtcNow` (`QuestModels.cs:353`, `IntelView.cs:304-309`) rather than game time.
* `IntelView.Awake` is a `private new void Awake()` that hides `ViewUI.Awake`, so `startOpen` is ignored (`IntelView.cs:27`).
* Saved quests store denormalized `title/shortDescription/detailedDescription` and markers (`QuestManager.cs:803-820`) and restore them over the definition (`:1529-1531`). Text fixes and localization never reach existing saves.
* `InventoryNotificationRelay.Initialize` unsubscribes from the *new* inventory rather than the old one (`InventoryNotificationRelay.cs:16-24`). It also dereferences `item.Name` after a branch that allowed `item == null` (`:49`).
* `entity.OnInteract` returns early after the first interaction and persists that state (`OpenWorldSolarSystemEntityInstance.cs:135-139, 72`), so **repeatable encounters on entities are one-shot**. The ARK station hub and its quest turn-ins work only once per save. Ownership is with the encounter/galaxy auditor; it's flagged here because it breaks every hub and turn-in design.

### Event-bus leak check [V]
No leaks found in the current code. QuestManager and EncounterManager unsubscribe in `OnDisable` (`QuestManager.cs:97-100`, `EncounterManager.cs:82-85`). IntelView and GalaxyView unsubscribe from `OnQuestDataChanged` (`IntelView.cs:381-398`, `GalaxyView.cs:114-140`). NarrativeView uses the `-=`/`+=` pattern on `OnPageChanged` (`NarrativeView.cs:125-126`). Domain reload is enabled (`ProjectSettings/EditorSettings.asset:26`), so static state resets in the editor.

Risks remain:
* The static `GameEventBus.OnEventPublished` has no reset hook for fast-enter-play-mode, and FastScriptReload is installed.
* Duplicate singletons subscribe in `OnEnable` before their deferred `Destroy`. With null `triggerEngine` that could throw inside the bus and, per B13, starve other subscribers.
* `OpenWorldPlayerController` only subscribes to `OnDialogEnded` if `DialogManager.Instance` exists at `OnEnable` (`OpenWorldPlayerController.cs:24-30`), which is an ordering hazard.

---

## 3. Dead, deprecated and duplicate code

### 3.1 Duplicate systems doing the same job [V]

**Four action vocabularies:**

| Capability | `OutcomeType` (dialog enum) | `CustomAction` string handler | `QuestActionType` (quest enum) | other |
|---|---|---|---|---|
| Set world var | `SetVariable` (`NarrativeActionRuntime.cs:251-253`) | `state.set` (`:613-634`) | `SetWorldState` (`QuestActionExecutor.cs:106-112`) | — |
| Spawn entity | `SpawnEntity` (no count) | `spawn.entity` (`:538-566`) | `SpawnEntity` (`:114-125`) | — |
| Queue encounter | (PLAY_NESTED differs) | `flow.queue_encounter` (`:519-536`) | `QueueEncounter` (`:96-104`) | — |
| Replace entity | — | `world.replace_entity` (`:568-594`) | `ReplaceEntity` (`:127-139`) | — |
| Exit system | — | `world.exit_system` (`:596-611`) | `ExitCurrentSystem` (`:141-146`) | hardcoded fallback encounter (`SolarSystemEntity.cs:116-152`) |
| Faction adjust/set | — | `faction.adjust_relation` (default ±5) / `faction.set_relation` | `AdjustFactionRelation` / `SetFactionRelation` (intValue) | — |
| Quest accept/complete/fail/objective/track/log/template | — | `quest.*` ×8 (`QuestNarrativeActionHandlers.cs`) | same ×8 | — |
| Trade / Repair / Ship editor / Combat / Audio | `DialogManager.Resolve*` (`DialogManager.cs:558-640, 697-709`) | — | — | **second copy** in `NarrativeActionEngine` (`NarrativeActionRuntime.cs:316-491`) |

**Five condition dialects:**
1. `ConditionNode` trees (`QuestConditionEvaluator.cs:16-245`).
2. `REQ_*` strings (`DialogManager.CheckRequirement`, `DialogManager.cs:718-839`).
3. A `REQ_VAR`/`REQ_ITEM` subset re-implemented as a fallback (`QuestConditionEvaluator.cs:298-334`).
4. `REQ_VAR`/`REQ_ENTITY_*` for spawns (`EntitySpawnCondition.cs:14-66`). Note that it reads `interacted_{entityId}` while the writer sets `interacted_{instanceId}` (`OpenWorldSolarSystemEntityInstance.cs:150`).
5. Implicit affordability scanning of outcomes (`DialogManager.CanAffordOption`, `:494-544`).

**Helpers copied multiple times:**
* `ParseFuncParams` ×3: `DialogManager.cs:844-861`, `QuestConditionEvaluator.cs:336-353`, `NarrativeView.cs:449-460`.
* `PlayAudioByName` ×3: DialogManager, NarrativeView, NarrativeActionEngine.
* `CalculateShipRepairCost` ×2 (`DialogManager.cs:664-680`, `NarrativeActionRuntime.cs:419-439`), plus FleetView according to its comment.
* `ParseEndEncounterSuccess` ×2 (the NarrativeView copy is unused, `:537-548`).
* Faction resolution ×3: `QuestActionExecutor.cs:180-198`, `NarrativeActionRuntime.cs:673-695`, and inline at `:704-715`.
* Boolean parsing ×4 with *different* accepted tokens: `GameEventPayload.GetBool`, `NarrativePayloadParser.GetBool` (accepts yes/on), `QuestActionExecutor.GetParamBool`, `QuestConditionEvaluator.ParseBool`.

**Two "current encounter/page" owners:** `NarrativeView._currentEncounter/_currentPage` (`NarrativeView.cs:21-22`) and `DialogManager.currentEncounter/currentPage` (`DialogManager.cs:19-20`). They diverge during nested dialogs: NarrativeView passes the *parent* encounter to `GetDynamicOptionsForContext` (`NarrativeView.cs:226-229`).

**Two importers with different JSON libraries:** JsonUtility silently ignores unknown or typo'd fields (`NarrativeJsonImporter.cs:145`); Newtonsoft is used in `QuestJsonImporter.cs:93`.

**Two sources of the same story beat:** `story_survey_system` (encounter with REQ_ITEM turn-in) and `quest_ark_material_turnin` (quest entity option) both set `story_survey = complete`.

### 3.2 Dead code [V unless marked]

| Item | Evidence |
|---|---|
| `Assets/Game/Data/NarrativeEvents.csv` | No code references `.csv` (grep). It's a stale subset: 27 of the 34 JSON encounters. It's the pre-JSON authoring format. |
| `NarrativePool.cs` + `DialogManager.PlayFromPool` + `PLAY_FROM_POOL` | 0 assets; no `Resources/Encounters/Pools` folder; `GetRandomEncounters` unused |
| `DialogManager.IsOptionConsumed`, `ResolveOutcome`, `ResolveRepairAllShips` (private), `IsFleetDamaged`, `CurrentOptions` | 0 callers |
| `EncounterManager.ResolveOutcome`, `GetIntVariable` | 0 callers |
| `NarrativeView.ParseEndEncounterSuccess` | 0 callers |
| `NarrativeActionEngine` Trade/StartCombat/Repair/OpenShipEditor/PlayAudio branches | Only reachable from the NarrativeView fallback when `DialogManager.Instance == null` (`NarrativeView.cs:499-506`); DialogManager intercepts these first (`DialogManager.cs:195-216`) |
| `QuestManager.GetMissionMarkerIcon` + `missionMarkerIcon` field, `ShouldShowMarkerForSystem`, `TryGetSystemMarkerLabel`, `MarkerSystemMatches`, `GetActiveQuests` | 0 external callers. The tooltip claims StarNode uses it; it doesn't. |
| `QuestManager.EnsureStoryEntityObjectiveSystemMarker` | Only reachable when a ReachSystem target resolved *and* no explicit system marker exists; no current content meets both conditions |
| `QuestObjectiveDefinition.hiddenUntilActive` | Imported and cloned, never read |
| `legacy_requirement` primitive + `EvaluateLegacyRequirement` | No content uses it (grep 0 hits in the JSON) |
| History sets `visitedSystemKeys`, `visitedFactionIds`, `interactedEntityKeys` | Always empty (see B1) |
| Legacy-bridge tautologies `if (X.Instance != null) X.Instance.M(); else X.Instance?.M();` | `QuestActionExecutor.cs:119-122,133-137,142-145,195-197`; `NarrativeActionRuntime.cs:562-563,589-592,607-609,690-691`; `EncounterManager.cs:525-526`. Git shows these were `GalaxyManager.Instance?.…` fallbacks (commit `409b86a6`) that were later find/replaced. |
| `SolarSystemEntity.GetRuntimeExitGateFallbackEncounter` (content in code) | `exitgate_active` already references `story_use_active_gate` |
| `OutcomeType.ChangeHull/ChangeCrew/ChangeResearch` | No-ops (B17) |
| `CombatEncounter` assets and the `START_COMBAT` path | Rejected by `EncounterManager.StartEncounter`; the encounter/combat auditor owns this |
| `Visuals/Cutscenes/*.playable` | See 1.5 |
| `QuestJsonImporter : EditorWindow`, `NarrativeJsonImporter : EditorWindow` | No `OnGUI`/window; they are just menu items |
| Roguelike "run" concept | `ResetForNewRun`, `oncePerRun`, `QuestRunStateData`, `InitializeFreshRun`, `persist.*` meta keys, `EndRun`/`HandleRunEndContinue`: the node-map era carried into the open world |

### 3.3 Dead or unreachable content [V]

* **13 of 34 encounters are unreachable.** No entity pool, ID queue, nested call or trigger reaches them: `story_survey_system`, `event_mystery_signal` (so `quest_signal_trace` can only be accepted from inside it, which means never), `event_territory_*` (B1), `demo_audio_events`, `demo_conditional_options`, `event_crew_stranded`, `event_distress_trap`, `event_empty_cargo`, `event_frozen_sentinel`, `event_fuel_station`, `event_scavenger_king`, `event_sensor_ghost`, and `event_time_echo`. (Method: GUID search of each asset across all `.asset/.prefab/.unity` files plus a string search of the IDs in code and JSON.)
* **42 world-state variables are written but never read.** Examples: `lore_nanite_storms`, `lore_great_panic`, `lore_fragmented_warning`, `map_reveal_2`, `map_hidden_planet`, `poi_satellite_array`, `rep_ark`, `rep_vanguard_ghosts`, `trait_shaken`, `trait_existential_dread`, `item_seed_bank`, `weapon_scrap_cannon`, `blueprint_glass_beam`, `upgrade_stealth`, `story.whisper_chain.complete`, and `quest.*.complete`. The only ones read are `persist.story_waking_up` and `run.intro_phase` in code. The writing *promises* rewards, items, traits and lore that the game never grants.
* **Debug content ships on the ARK station:** demo_station_hub → "Mission Control" offers "Accept Fail-State Sandbox", "Force Fail Sandbox Quest", "Generate … Template Mission" and so on, plus `quest_trigger_fail_test`. `has_hacking` is read (demo_conditional_options) but never set.

---

## 4. Content pipeline problems

1. **String DSLs inside JSON strings.**
   * `"actions": "ADD_ITEM(ship_fighter, 1); CUSTOM_ACTION(spawn.entity,entityId=npc_probe_tutorial|anchor=ark_station|count=1); …"` is parsed by `Split(';')` and the regex `(\w+)\(([^)]*)\)` (`NarrativeJsonImporter.cs:404,412`). Any `;` or `)` inside a value, such as a log text or quest description, silently truncates it.
   * CUSTOM_ACTION payloads use a third syntax (`k=v|k=v`), so `|` and `=` can't appear in values.
   * Requirements stay as unparsed strings until runtime, so typos fail open at runtime (B17).
2. **Two JSON shapes and two libraries.** Narrative uses a flat page/option DSL; quests use structured trees. Authors learn both, and the two condition languages can't be mixed: a dialog option can't use `all/any`, and a quest can't use `REQ_SEEN_ENCOUNTER`.
3. **Validation gaps.**
   * The narrative importer checks only GOTO/PUSH targets (`NarrativeJsonImporter.cs:305-319`). It doesn't check RANDOM_BRANCH targets, PLAY_NESTED/START_COMBAT assets, CUSTOM_ACTION IDs or payload keys, item IDs, requirement syntax, dead-end pages (pages with no options), or unreachable pages. Unknown action names only produce a warning (`:495`, `:522`).
   * The quest importer is better: errors are path-qualified, `ImportContext` aggregates them, and it validates IDs and enums. It still doesn't validate entity-option outcomes, item and entity targets, condition `questId`/`objectiveId` references, trigger `eventType` against published events (B1 would have been caught), template format strings, `custom_condition` objectives with no condition (these auto-complete), or event payload keys used by `event_value`.
4. **No referential integrity across the pipeline.** IDs are free strings: entity IDs, system IDs (`"basesolarsystemdata"`, `"ark_start"`), item IDs (numbers from a removed scheme), encounter IDs, `persist.` magic prefixes and `story_` naming conventions (which drive markers, `QuestManager.cs:590,613`).
5. **Manual menu re-import.** Nothing re-imports on save; it's easy to forget. The quest importer **deletes and recreates** the catalog assets every import (`QuestJsonImporter.cs:1239-1250`), which changes their GUIDs and adds churn. That's harmless only because they're loaded by `Resources` path. Removed narrative encounters leave orphan assets (no cleanup, `NarrativeJsonImporter.cs:205-214`). Duplicate IDs across files "overwrite" with a warning (`:167-171`).
6. **Localization:** see B12. Quest text (titles, objectives, logs, notifications) has no localization path at all.
7. **`Resources/` everywhere:** encounters, combat, pools, catalogs and images (`NarrativeImages/` doesn't exist, so all 10 page images referenced in content silently show nothing, `NarrativeView.cs:182-197`).
8. **Docs drift.** `Quest_System_Guide.md:216-229` lists trigger events that are never published. `Narrative_System_Guide.md` documents Pools and Consume-On-Use, which are both broken. The guides live inside `Assets/Game/Core/Dialog/` (they're imported as TextAssets).

---

## 5. Recommended target architecture

### 5.1 Design goals for a mystery game
* **Knowledge is the progression.** As in Outer Wilds, the player advances by *learning facts*, not by ticking checklists. Quests become optional scaffolding derived from knowledge: "You know the gate reacts to strangelets → objective: find strangelets".
* **One truth: the Fact Store.** Everything narrative reads from and writes to typed facts. Dialog, quests, triggers, encounter and spawn selection, map markers and the ship log are all *views* or *rules* over facts.
* **One vocabulary of Conditions and Effects** is shared by every system and authored the same way everywhere.
* **Content is data with referential integrity.** IDs are validated at import; nothing fails open.
* **Saves contain no definition text.** They hold facts, known clues, quest stage pointers and baselines, all versioned.

### 5.2 Component sketch

```
                 ┌───────────────────────── NarrativeDirector (plain C# service, one host MonoBehaviour) ─────────────────────────┐
 typed events ─► │  RuleEngine (triggers: event → condition → effects; deferred, re-entrancy-safe)                               │
 (EventBus<T>)   │  FactStore (blackboard: typed keys, scopes, change events) ◄── single source of truth                          │
                 │  ClueLog (Ship Log: clues, rumors, threads, mysteries)    ── facts "clue.<id>" / "mystery.<id>.solved"          │
                 │  QuestTracker (declarative quests: stages, objectives = conditions over facts, accept-time baselines)          │
                 │  BeatSelector (storylets: which dialogue/beat plays at entity X given facts; replaces encounter pools for story) │
                 │  DialogueAdapter (Yarn Spinner DialogueRunner; VariableStorage = FactStore; commands/functions = Effects/Conds) │
                 │  MarkerProvider (derives map markers from rumors + quest objectives; cached, invalidated on FactChanged)       │
                 └────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
                        ▲ Conditions / Effects registry (one vocabulary)        ▼ SaveData { facts, clues, quests, version }
```

### 5.3 Fact Store (blackboard)

```csharp
public enum FactScope { Save, System, Entity, Session }      // Session = not saved (e.g. consumed options)
[CreateAssetMenu] public sealed class FactDef : ScriptableObject {   // optional; most facts are generated from content IDs
    public string id;            // "ark.gate.activated", "count.destroyed.npc_probe", "clue.first_ark.known"
    public FactType type;        // Bool, Int, String(enum-ish)
    public FactScope scope;
}
public readonly struct FactChanged : IGameEvent { public readonly FactId Id; public readonly FactValue Old, New; }

public sealed class FactStore {
    readonly Dictionary<FactId, FactValue> _values = new();
    public bool  GetBool(FactId id);   public int GetInt(FactId id);
    public void  Set(FactId id, FactValue v);           // raises FactChanged (deferred)
    public void  Add(FactId id, int delta);             // counters: kills, visits, deliveries
    public FactSnapshot Capture();  public void Restore(FactSnapshot s);   // save = just this
}
```

The Fact Store replaces:
* `runWorldState` and `persistentWorldState`, including the `run.*` magic keys;
* `SeenEncounters` (as facts `seen.<encounter>`);
* the QuestManager history sets (as counters `count.destroyed.<entityId>` and `visited.<systemId>`);
* `interacted_<instanceId>` and `destroyed_<instanceId>`, which move to per-entity scope so the global namespace stops growing;
* the 42 write-only `lore_*`/`map_*`/`rep_*` flags, which become real clues and facts.

Faction relations can be facts too (`relation.<faction>`). Alternatively they can stay in a Faction service that implements the same condition interface.

### 5.4 One Condition/Effect vocabulary

The goal is that every system (dialog, quests, triggers, spawn rules, beats) authors and evaluates the same thing.

```csharp
public interface ICondition { bool Evaluate(in NarrativeContext ctx); void Validate(ValidationReport r); }
public interface IEffect    { void Apply(NarrativeContext ctx);       void Validate(ValidationReport r); }

// Conditions:  Fact(cmp), HasItem(ItemRef,n), QuestState(QuestRef,state), ObjectiveDone, ClueKnown(ClueRef),
//              Relation(FactionRef,cmp), Seen(BeatRef), InSystem(SystemRef), AllOf/AnyOf/Not
// Effects:     SetFact, AddFact, GiveItem, TakeItem, ChangeHull, AdjustRelation, RevealClue, StartQuest,
//              AdvanceStage, SpawnEntity(EntityRef, anchor, count), ReplaceEntity, QueueBeat, ExitSystem,
//              OpenShop, OpenShipEditor, StartCombat, PlaySfx, Notify
```

* **Typed references**, not strings: `ItemRef` (registry key, validated against `Resources/Items`), `EntityRef`, `SystemRef`, `QuestRef`, `ClueRef`, `BeatRef`. Each is a small serializable struct with an ID string plus a custom property drawer and validator.
* **One text syntax** for files: `has_item("strange_matter_container_full") and not fact("ark.gate.activated")`. It's parsed **at import time** into the object graph above, so runtime never parses strings. With Yarn Spinner, you register the same conditions as Yarn functions and the same effects as Yarn commands.
* **Fail closed.** An unknown function or effect is an import error, never "true".

### 5.5 Clue / Rumor graph (the mystery spine)

```yaml
# clues/arks.yaml
thread: arks              # Ship Log "card cluster"
clues:
  - id: ark.first_ark_vault
    title: loc:clue.ark.first_ark_vault.title
    found_by: [ beat.cryo_vault.open ]             # beats that reveal it (RevealClue effect)
    rumors:   [ { points_to: system.ice_ring_vault, text: loc:... } ]   # rumor → map marker, not a quest
    links:    [ ark.first_ark_fate ]                 # ship-log arrows
  - id: ark.mutiny
    found_by: [ beat.mutiny_end.mess_hall ]
mysteries:
  - id: mystery.what_happened_to_the_arks
    requires: [ ark.first_ark_vault, ark.mutiny, ark.encrypted_beacon ]
    on_solved: [ RevealClue(ark.final_destination), StartQuest(quest.follow_the_first_ark) ]
```

The current writing already contains threads worth formalizing. These are taken from content text and SET_VAR names; the grouping is **[I]**:
* **The Arks:** waking up on A.R.K, the First Ark vault (cryo_vault), the mutiny ship (mutiny_end), the outdated Ark encryption beacon (singing_storm), the phantom Ark fleet (sensor_ghost).
* **The Vanguard:** frozen_sentinel, overgrown_forge, hollow_moon, scavenger_king's Vanguard-plated throne, and miner_lament's "rep_vanguard_ghosts".
* **The Silence / erasure:** silent_city ("no people, no bodies") and quarantine_lab ("cure for the Silence's erasure effect").
* **Messages from yourself:** time_echo ("it's your ship… your voice") and horizon_message ("a message from You, 3 cycles ago").
* **Gates and strangelets:** activate_gate, glass_desert ("glassed 1,000 years ago"), and the xeno probes in the intro.
* **The Whisper signal:** story_chain_signal_*.

The **Ship Log UI** replaces `IntelView`. It has a rumor map (clue cards with arrows), a "Leads" tab (rumors pointing at places, which drive galaxy markers) and a small "Contracts" tab for procedural missions.

### 5.6 Story beats (storylets) instead of hard-wired encounter pools

```csharp
[CreateAssetMenu] public sealed class StoryBeat : ScriptableObject {
    public string id;
    public LocationSelector where;          // entity id / entity tag / system / "any planet of type X"
    [SerializeReference] public ICondition when;   // over facts
    public int salience;                    // higher wins; ties → random/weights
    public bool once = true;
    public string yarnNode;                 // the dialogue to run
    [SerializeReference] public List<IEffect> onComplete;
}
```

* When the player interacts with an entity, `BeatSelector.Pick(entity, facts)` chooses the highest-salience eligible beat. If none qualifies, it falls back to the entity's ambient random-event pool, and those pool entries are just low-salience beats.
* This fixes hubs: a station is "repeatable" because each visit re-selects a beat. Quest turn-ins become beats with `when: ObjectiveReady(...)`, which replaces `QuestEntityOptionDefinition` and `GetDynamicOptionsForContext`. With Yarn, a hub node can also contain `<<if>>`-gated options driven by the same facts.
* Note the entity-side bug: `OnInteract` currently returns after the first interaction (B17), and that needs to change too.

### 5.7 Quests as declarative views over facts

```yaml
id: quest.whisper_chain
title: loc:quest.whisper_chain.title
kind: story            # story | contract (procedural) | lead (auto-created from rumor)
stages:
  - id: triangulate
    objectives:
      - id: reach_signal
        text: loc:quest.whisper_chain.reach_signal
        done_when: fact("visited.system.whisper_source")
        marker: { system: system.whisper_source }
    on_complete: [ SpawnEntity(entity.npc_story_hunter, anchor: player), RevealClue(whisper.hunter_origin) ]
  - id: intercept
    objectives:
      - id: destroy_hunter
        done_when: since_accept(count("destroyed.npc_story_hunter")) >= 1     # baseline captured on stage enter
  - id: debrief
    objectives:
      - id: debrief_ark
        done_when: beat_completed(beat.ark.whisper_debrief)
```

* **Stages** give ordering, which the current system lacks: "debrief" can't complete before "return".
* **`since_accept(...)`** stores a baseline when the stage is entered (saved per quest). This removes the B3 class of bugs entirely.
* Objectives are **re-evaluated only when a fact they depend on changes**. Build a dependency index at load; no polling on every event.
* Quest state saved is `{questId, status, stageIndex, baselines{factId:int}, completedObjectiveIds}`. Text always comes from the definition and localization.
* Mission templates become **contract generators**: a template plus parameter bindings (`target = random entity with tag X in system Y`). They produce quests whose IDs and parameters are saved, never their text. Format with named arguments (`{target}`) through Unity Localization Smart Strings, not `string.Format` on raw content.

### 5.8 Typed, leak-proof event bus

```csharp
public interface IGameEvent {}
public readonly struct EntityInteracted : IGameEvent { public readonly EntityRef Entity; public readonly string InstanceId; public readonly SystemRef System; }
public readonly struct SystemEntered    : IGameEvent { public readonly SystemRef System; public readonly FactionRef Owner; public readonly bool FirstVisit; }
public readonly struct EntityDestroyed  : IGameEvent { /* … */ }
public readonly struct EncounterEnded   : IGameEvent { public readonly BeatRef Beat; public readonly bool Success; }

public static class EventBus<T> where T : struct, IGameEvent {
    static readonly List<Action<T>> s_handlers = new();
    static readonly Queue<T> s_deferred = new();
    public static IDisposable Subscribe(Action<T> h) { s_handlers.Add(h); return new Unsub(() => s_handlers.Remove(h)); }
    public static void Raise(in T e) {                       // immediate, isolated per handler
        for (int i = s_handlers.Count - 1; i >= 0; i--)
            try { s_handlers[i](e); } catch (Exception ex) { Debug.LogException(ex); }
    }
    public static void Enqueue(in T e) => s_deferred.Enqueue(e);       // drained once per frame by the Director
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { s_handlers.Clear(); s_deferred.Clear(); }    // safe with Enter Play Mode Options / hot reload
}
```

* Subscribers hold the returned `IDisposable` in a per-component `CompositeDisposable` that's disposed in `OnDisable`, so there's no manual `-=` bookkeeping.
* Narrative rules process **deferred** events, so effects that raise events never re-enter mid-iteration (fixes B14).
* **ECS bridge:** gameplay systems append to a `NativeQueue<NarrativeEventData>` or a singleton `DynamicBuffer`. One managed system drains it into `EventBus<T>.Enqueue` each frame. That keeps Burst jobs free of managed delegates. This matters as ships and stations become large destructible ECS structures, where "block destroyed" type events will be frequent and should be aggregated (for example "station X core destroyed") before reaching narrative.
* Payloads are typed fields, not `Dictionary<string,string>`.

### 5.9 Dialog: Yarn Spinner vs Ink vs custom

| Criterion | Current custom JSON | **Yarn Spinner 3** | Ink (inkle) |
|---|---|---|---|
| Writer ergonomics | JSON plus a DSL in strings; poor | Screenplay-like `.yarn`; VS Code extension with graph view **[I: verify current tooling]** | Excellent prose flow; Inky editor |
| Flow control (goto/return/nested) | Hand-rolled in DialogManager (buggy, B4/B5) | Built in (`<<jump>>`, `<<detour>>`/return in v3 **[I]**) | Built in (diverts, tunnels, threads) |
| Conditions and vars | Two dialects, fail-open | Expressions plus **custom functions** (map to ICondition) | Expressions plus external functions |
| Game calls | Three action systems | **Commands** `<<give_item ...>>` map 1:1 to IEffect | External functions / tags |
| **Variable storage** | EncounterManager dicts | **Pluggable `VariableStorageBehaviour`**, so it can be backed directly by FactStore | Story state is an opaque JSON blob; you mirror it or observe variables |
| Localization | Broken (B12) | **Line IDs (`#line:`) plus Unity Localization integration** **[I: verify for Unity 6.6]** | No native line IDs; needs custom tagging |
| Content selection (storylets) | Encounter pools | v3 **node groups with `when:` conditions and saliency** **[I: verify]** | None built in |
| Save robustness across content patches | Saves text (B17) | Only your facts are saved | `state.ToJson()` can break when the story changes |
| License | — | Core MIT; some add-ons and samples are paid **[I: verify]** | MIT |

**Recommendation:** use **Yarn Spinner 3** for all dialogue, with a custom `VariableStorageBehaviour` that reads and writes the FactStore, and custom functions and commands generated from the Condition/Effect registry. Keep quests, clues and beats in SO or YAML (they're structured data, not prose). Ink is the better pure-writing tool, but its opaque state model fights the "facts are truth" design and its localization story is weaker. Staying custom means re-building flow control, localization, editor tooling and validation, which is exactly what is broken now.

**Before committing:** confirm Yarn Spinner's Unity 6.6 and UI Toolkit compatibility and the feature names marked [I]. If you need UI Toolkit presentation, write your own presenter on top of `DialogueRunner`; the presenter API is small.

### 5.10 Save format

```csharp
[Serializable] public sealed class NarrativeSave {
    public int version;                         // + ID rename table for migrations
    public List<FactEntry> facts;               // Save-scope facts only
    public List<string> knownClues;             // (or facts "clue.<id>")
    public List<QuestSave> quests;              // id, status, stageIndex, baselines, doneObjectives, contractParams
    public List<string> firedRules;             // "once" rules
    public string activeBeatId; public string yarnNode; // optional: resume mid-dialog, or simply restart beat
}
```

* There is no title, description or log text in saves. The log is reconstructed from facts and clue timestamps, using **game time** rather than wall clock.
* Loading **never executes effects**. Rules and objectives are re-derived silently, then evaluation resumes on the next frame in the world scene (fixes B8).

### 5.11 Localization readiness
* Every player-facing string is either a Yarn line (with a line ID, synced to a String Table by tooling) or a `LocalizedString` on an SO with a **deterministic key derived from stable IDs** (`quest.<id>.title`, `clue.<id>.body`).
* Never key by position or by text equality. Use Smart Strings for parameters (contract templates, counts).
* Regenerate `GameText` from scratch once to drop the 3,594 orphan entries, and make the importer dirty `SharedTableData` (B12).

### 5.12 Authoring tools and validation
* Use `ScriptedImporter`s for `.yarn` (provided by Yarn Spinner) and for `.quest.yaml`/`.clues.yaml`, so assets regenerate on save. Remove the menu-driven re-import.
* Add a **NarrativeValidator** that runs on import, from a menu, and in CI batch mode (`-executeMethod`). It should check:
  * every typed ref resolves (items against the item registry, entities, systems, beats, Yarn nodes, clues, quests);
  * every condition and effect is known;
  * no fact is read but never written, or written but never read (that check would have flagged the 42 orphan variables);
  * no unreachable beats or Yarn nodes, and no dead-end nodes;
  * every rumor points somewhere reachable;
  * every mystery is solvable (a graph search over clue sources).
* Use **Newtonsoft with `MissingMemberHandling.Error`** (or YAML with strict schema) so typos fail import.
* Add a **Narrative Debugger window**: a live fact table (editable), active quests with stages and baselines, rule fire log, and "jump to beat". This replaces the shipped Mission Control debug page.
* Generate C# constants for the few IDs code needs (for example the intro beat), instead of `[SerializeField] string wakeupEncounterId`.

### 5.13 Migration path

Each phase is shippable on its own.

**Phase 0: stop the bleeding (1–3 days).** Only do this if you'll keep playing the current build while migrating; see the quick wins in section 6.

**Phase 1: Fact Store and typed events (about 1 week).**
1. Add `FactStore` and implement `EncounterManager.Set/GetVariable` on top of it (an adapter), keeping old key names.
2. Add typed events and publish `SystemEntered`, `EntityInteracted`, `EntityDestroyed` and `EncounterEnded` from the open-world code. Bridge them to the old string bus temporarily so QuestManager keeps working.
3. Move QuestManager history sets into counters and facts. Capture baselines on accept.

**Phase 2: one vocabulary (about 1 week).**
1. Implement `ICondition`/`IEffect` with typed refs and the text parser.
2. Write adapters that compile `REQ_*`, `ConditionNode`, `OutcomeType`, `QuestActionType` and CUSTOM_ACTION payloads into the new objects **at import time**. Old JSON keeps working.
3. Delete `DialogManager.CheckRequirement`, `CanAffordOption` (it becomes `IEffect.CanApply`), `NarrativeActionEngine`, `QuestActionExecutor`, `QuestNarrativeActionHandlers` and `EntitySpawnCondition` once the adapters cover them.

**Phase 3: Yarn Spinner (1–2 weeks).**
1. Write a one-shot converter from `NarrativeEvents.json`/`StoryEvents.json` to `.yarn`. Pages become nodes; options become `->` lines with `<<if>>`; actions become commands; RANDOM_BRANCH becomes a `dice()`/line-group construct. It's mechanical because the schema is regular.
2. Write a UI Toolkit presenter that reuses NarrativeView's visuals (outcome icons, shortcuts, animation).
3. Delete DialogManager page/flow code, `NarrativePool`, `NarrativeEncounter.pages`, and the narrative importer.

**Phase 4: mystery layer (ongoing, content-led).**
1. Clues, threads, mysteries and the Ship Log UI. Convert the 42 orphan flags into clues and real effects (items, relations, traits).
2. StoryBeats and BeatSelector; entity interaction calls `BeatSelector` instead of `EncounterPool` for authored content. Fix one-shot interaction.
3. Rewrite quests as staged, declarative YAML. Turn `MissionTemplates` into contract generators.

**Phase 5: delete legacy.** Remove the CSV, the "run" vocabulary (`oncePerRun` → `once`, `ResetForNewRun` → `NewGame`), `persist.` prefixes (replaced by FactScope), `legacy_requirement`, the system-marker API, the history sets, the tautological bridges, the cutscene timelines (if unused), the debug hub content, and `Resources/` loading (switch to Addressables or direct SO references).

### 5.14 What to keep, port or delete

| Keep (port) | Rework | Delete |
|---|---|---|
| **All writing** in StoryEvents/NarrativeEvents (intro, NORA, the 15 unique "mystery" events) | Quest definitions → staged YAML; Whisper Chain → first real story thread | NarrativeEvents.csv |
| The idea of contextual quest options inside dialog (→ beats / Yarn `<<if>>`) | MissionTemplates → contract generators with Smart Strings | DialogManager flow/requirement/affordability code |
| Quest importer's `ImportContext` path-qualified errors and strict validation (`QuestJsonImporter.cs:1480-1518`) | Trigger engine → RuleEngine (deferred, typed events) | NarrativeActionEngine + QuestActionExecutor + QuestNarrativeActionHandlers (after adapters) |
| Condition-tree concept (`all/any/not`) | NarrativeView visuals → Yarn presenter | NarrativePool, legacy_requirement, system-marker API, history sets |
| NarrativeView's outcome icons, keyboard shortcuts and entry animation | IntelView → Ship Log | `run`/`persist.` vocabulary, debug Mission Control content |
| Per-target dedupe keys for event-count objectives (→ per-entity facts) | GalaxyView markers → MarkerProvider (cached) | Cutscene timelines (if confirmed unused) |

---

## 6. Quick wins

These are ordered by value per effort. Do them only if you need the current build playable during migration; most are superseded by Phase 1–3.

1. **Publish the missing events.** Add `system.entered` and `territory.entered` (with `firstVisit`) where OpenWorldGalaxyManager finishes generating or entering a system, and `entity.interacted` in `OnInteract` (`OpenWorldSolarSystemEntityInstance.cs:133-153`, `EcsSolarSystemEntityInstance.cs:85-103`). This restores three objective types and two triggers (B1). Use the deleted code in `git show 1069164e` as reference.
2. **Fix item references.** Replace `500` and `717185106` in the JSON with registry keys (`strange_matter_container_full`, plus whatever "raw materials" should be). Make `REQ_ITEM`, `REMOVE_ITEM`, `CanAffordOption`, `collect_item` and `inventory_item` resolve registry keys, and make both importers reject unknown items (B2).
3. **Add a single `AbortEncounter(bool success)` path** that tears down NarrativeView, DialogManager (including `dialogStack`) and EncounterManager. Call it on missing page, pool, nested or combat asset, null loot table, START_COMBAT and OPEN_SHIP_EDITOR. Also add a guaranteed "[Leave]" option when a page renders zero enabled options (B4, B5).
4. **Remove or neutralize PLAY_FROM_POOL and the five dangling START_COMBAT references** in content until combat or pools exist (B4).
5. **Ignore history for objectives.** In `TryAdvanceObjectiveFromHistory`, skip untargeted objectives and destroy objectives, or snapshot counts on accept and compare deltas (B3).
6. **Make `consumeOnUse` work:** filter with `IsOptionConsumed` in `DrawPage`, key by a stable option ID, and include dynamic options (B6).
7. **Keep unique quests unique after deletion:** keep a `completedQuestIds` set, or forbid deleting unique/story records (B7).
8. **Fail closed** on unknown requirements and primitives, with an error log (B17). The narrative importer should validate `REQ_*` syntax.
9. **Localization importer:** call `EditorUtility.SetDirty(collection.SharedData)`, drop the text-dedupe branch, then regenerate `GameText` to purge the orphan entries (B12).
10. **GameEventBus:** iterate `GetInvocationList()` with a per-handler try/catch and `Debug.LogException` (B13). Set the trigger "fired" flag before running actions (B14).
11. **Delete page-audio duplication** (`NarrativeView.cs:150-153`) and the second `MarkEncounterSeen` (`NarrativeView.cs:116-120`, which also has the null-ref) (B11, B17).
12. **Cache `IsStoryOrQuestEntity`** per entity and invalidate on `OnQuestDataChanged` (B16).
13. **Remove dead code** listed in 3.2. That's roughly 500 lines with zero behaviour change, starting with the tautological legacy branches, the DialogManager duplicates, the NarrativeView duplicates, the system-marker API and the CSV.
14. **Gate the "Mission Control" debug page** behind `Debug.isDebugBuild`, or move it to an editor window.
15. **Implement or remove `CHANGE_HULL`/`CHANGE_CREW`/`CHANGE_RESEARCH`,** and have the importer warn on no-op outcomes so the writing stops promising consequences the game doesn't deliver.
