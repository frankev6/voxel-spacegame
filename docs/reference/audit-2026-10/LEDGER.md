# Audit Ledger — Spaceship Game (started 2026-10-09)

Running log of the in-depth audit. Updated every few findings so nothing is lost
across context compaction. Newest entries at the bottom of each section.

## Status / where things are

**AUDIT COMPLETE (2026-10-09).** Deliverables: report page https://claude.ai/artifact/J1LF78yBJgFRQpsNtBrQ81 ;
docs/audit/00-synthesis.md (unified findings, architecture, roadmap, quick wins); 01..07 subsystem reports;
docs/audit/screenshots/; test harness Assets/_DevTools/Editor/GameTestHarness.cs + tools/playtest/ (README).
Test save slots moved out of the user's save folder to scratchpad/test-saves (user had no saves before).

- Detailed per-subsystem reports (written by parallel auditors) land in
  `/private/tmp/claude-501/-Users-frank-DEV-spaceship-game/4ac1ec9f-c410-479f-9d8f-1810c83e1b9d/scratchpad/audit/0N-*.md`
  and get copied into `docs/audit/` when done:
  01-voxel-core, 02-combat-destruction, 03-galaxy-world, 04-persistence-data,
  05-narrative-quests, 06-ui-shipeditor, 07-project-health.
- Final deliverable: an HTML report page (Artifact) + these markdown files.
- Unity CLI: `~/.unity/bin/unity` (not on default PATH in non-interactive shells;
  `export PATH="$HOME/.unity/bin:$PATH"`). Editor PID 65018, project open.

## Changes made to the project (all reversible, uncommitted)

1. Installed `com.unity.pipeline` 0.8.0-exp.1 via `unity pipeline install` (adds a
   line to `Packages/manifest.json`; original manifest backed up at
   scratchpad/manifest.backup.json). Needed for live Editor control.
2. Compile fixes for Unity 6.6 (`Object.GetInstanceID()` is now an error, CS0619):
   - `Packages/com.unity.localization/Runtime/Locale.cs:458`
   - `Packages/com.unity.localization/Runtime/Property Variants/Tracked Objects/JsonSerializerTrackedObject.cs:104,183,231,239`
   - `Packages/com.lunawolfstudios.scriptablesheets/Editor/ScriptableSheetsEditorWindow.cs:191,1692`
   - `Packages/com.lunawolfstudios.scriptablesheets/Editor/Settings/ScriptableSheetsSettings.cs:206,216`
   - `Assets/FastScriptReload/Scripts/Editor/FastScriptReloadManager.cs:459` (GetAssetPath(obj))
   - `Assets/Game/Core/Game Logic/GameDatabase.cs:164`
   All replaced with `GetEntityId().GetHashCode()` (same pattern Unity's own 6.6 packages use).
3. Embedded Localization 1.5.9 was too old for 6.6 (also uses removed UxmlFactory/UxmlTraits and
   ~12 other removed InstanceID APIs in Editor code). MOVED the embedded copy out to
   scratchpad/embedded-backup/com.unity.localization (still recoverable from git) so UPM resolves
   registry 1.5.13 (what manifest already requests). packages-lock now says source: registry.
   (My earlier patches to the embedded copy left with it.)
4. `Assets/Game/Core/Game Logic/GameSettings.cs:4` blanked stale `using Unity.Entities.UniversalDelegates;`
   (namespace gone in Entities 6.6; unused).
5. RESULT: `*** Tundra build success` (Editor.log line ~54153). `unity status` -> state "ready", port 7800.

## Environment facts

- Project was last committed on Unity 6000.3.0f1; opened today in 6000.6.5f1. The
  upgrade bumped Entities/Physics/Entities Graphics 1.4.x -> 6.6.0, Cinemachine 3.1.3 -> 6.6.0,
  Input System 1.16 -> 1.20, URP 17.3 -> 17.6 (uncommitted manifest change).
- Before patching, `Assembly-CSharp.dll` had never been built under 6.6, so the game
  code was not loaded at all in the open Editor.
- Localization is an *embedded* copy (Packages/com.unity.localization, v1.5.9) even
  though manifest says 1.5.13 — embedded wins. Game code uses Localization in
  SettingsView, NarrativeView, SettingsManager, NarrativeJsonImporter.
- `Assets/Game/Data/GameDescription.txt` describes the OLD roguelike vision (runs,
  permadeath, node map); current vision is open-world galaxy + mystery.
- No asmdefs for game code: everything compiles into Assembly-CSharp (~39.4k LOC in Assets/Game).
- Old Input Manager APIs (`Input.GetKey/GetAxis/mousePosition`) used in: AsteroidMiner,
  EntityTooltipView, ShipInputSystem, FreeFlyCamera, ShipEditorController, WeaponInputSystem,
  ThirdPersonCamera, OrbitalCamera, OpenWorldPlayerController — mixed with Input System package.

## Findings (cross-cutting, verified by me)

- F1 [critical/blocker] Project does not compile on Unity 6.6 (see above). Fixed minimally.
- F2 [medium] `ObjectTransformSync` / `GameObjectSyncSystem` + `GameDatabase.SyncTransformDictionary`:
  nothing ever adds `ObjectTransformSync` to an entity (only a comment in ShipBuildRequest.cs:20-24),
  so the system never runs (RequireForUpdate) -> dead code.

## Compile-time signals after the 6.6 upgrade (from first successful build)

- Entities 6.6 DEPRECATES managed components ("will be removed"): class-based IComponentData,
  AddComponentObject/GetComponentObject, managed ISharedComponentData. Game uses them in
  CombatManager.cs:98, ShipConstructionLinkSystem.cs:28,35, ShipUISyncSystem.cs:22, CombatHudSystem.cs:47,
  VoxelPrefabAuthoring.cs:106 (AddSharedComponentManaged), AsteroidConstructionJobSystem.cs:161.
  -> Any rewrite must use unmanaged components + UnityObjectRef<T>.
- FindFirstObjectByType deprecated (PersistentSingleton.cs:19, QuestManager.cs:62, SaveManager.cs:785,
  EncounterManager.cs:758/390, DialogManager.cs:338, OpenWorldGalaxyManager.cs:333, GalaxyView.Map.cs:313).
- Unused fields: OpenWorldSolarSystemGenerator.isGenerating, EcsSolarSystemGenerator.isGenerating,
  CombatManager.maxShipsPerRow/defaultSpawnSpacing.
- FastScriptReload broken on this Mac: HarmonySharedState TypeInitializationException "mprotect returned EACCES"
  (Apple Silicon W^X) + its welcome screen throws "Text on buttons must be less than 64 characters". -> remove FSR.
- DOTween .dll.meta PluginImporter v1 below supported minimum (needs re-save).
- Packages/com.wooshii.foldericons stale package-lock.json.meta warning spam every refresh.

## Runtime testing log

Tooling: scratchpad/u.sh <cmd> (condensed CLI), scratchpad/p.sh <ProbeMethod> '[args]' (runs
Temp/AgentScripts/Probe.cs via run_script: Click(btn), Buttons, Scenes, Entities, Find(type), Get(type,path),
Invoke(type,method,arg), Cameras), scratchpad/shot.sh <name> (inline game-view capture -> scratchpad/shots/).
NOTE capture_game_view --save_path always writes under Assets/ -> don't use it (deleted stray Assets/Temp).
No pre-existing saves in persistentDataPath (~/Library/Application Support/Frank/Spaceship Game); prefs plist backed up.

- R1 StartScene plays: start menu renders fine (shots/01-start.png). Boot: GameInitializer -> GameDatabase
  "Registry frozen with 69 items" (agent said 74 indices; check), Player '' initialized (empty name before new game).
- R2 Console at boot: Quantum Console "DontDestroyOnLoad only works for root GameObjects" (GameInitializer.cs:48);
  NarrativeView stylesheet missing font asset PerfectDOSVGA437 (URP debug font path); DOTween meta warnings.
- R4 New Game screen: commander name TextField = white text on light-grey bg (unreadable) (shots/02-newgame.png);
  portrait picker (selected_portrait/portrait_grid) not visible.
- R5 After Start -> GalaxyMapScene: NO AudioListener in scene -> "There are no audio listeners" logged EVERY FRAME
  (2000+ lines in seconds; StartScene had a separate AudioListener GO; galaxy Main Camera lacks one).
  timeScale=0 because intro narrative dialog (UIDialog) is open (PauseManager.cs:13). Main Camera at (0,0,0), far=5000.
  GalaxyMapScene has 60 roots incl. 55 pre-placed disabled CFXR explosion GameObjects (manual pool in scene).
  Visual (shots/03-galaxy-start.png): dialog text box scrolled mid-sentence; stray translucent rect top-left overlapping
  toolbar slots; a thin horizontal line across the whole screen at y~360; radar panel bottom-right; no star visible.
- TOOLING: user asked for a fast feedback loop and to use the official unity CLI (not clicking the Editor).
  Added Assets/_DevTools/Editor/GameTestHarness.cs (Editor-only, reflection-based) with [CliCommand]s:
  gh_snapshot, gh_logs, gh_mark, gh_ui, gh_click, gh_get, gh_invoke, gh_find, gh_ecs. Recompile via `unity command recompile`.
- TOOLING v2: gh_key (Input System state-event key injection w/ timed release), gh_mouse, gh_unfocused_input
  (auto-ON in play mode: sets InputSettings editorInputBehaviorInPlayMode=AllDeviceInputAlwaysGoesToGameView +
  backgroundBehavior=IgnoreFocus, restored on exit). Official `simulate_key` did NOT reach Keyboard.current
  (isPressed stayed false). Scripts: scratchpad/rc.sh (recompile+wait+errors), newgame.sh (fresh play -> New Game ->
  galaxy, ~13 s), dialog.sh [max] [opt] (auto-advance dialog), g.sh <gh cmd>, shot.sh <name>.
  FINDING: project InputSettings drop keyboard when Game view unfocused (EditorInputBehaviorInPlayMode=0,
  BackgroundBehavior=ResetAndDisableNonBackgroundDevices) -- fine for players, but note for tooling.
- R6 Intro dialog (6 pages) plays fine via keys 1-9: cryo pod -> ARK -> "hull breached by unknown entity" ->
  choose chassis (Fighter/Frigate/Vestari). Text box scroll position starts mid-paragraph.
- R7 After intro (shots/04-after-intro.png): player ECS ship spawns (966 ShipVoxel entities incl asteroid voxels;
  player ship LinkedEntityGroup=994; MaterialMeshInfo=1196). Ship spawns hugging the edge of a giant voxel STAR that
  fills half the screen. HUD: "AUTOFIRE: OFF" overlaps "FUEL: 50" top-left; top bar bg only spans 0-640px;
  "SHIELDS OFFLINE" dark-grey on dark (unreadable); radar panel = bottom-right quarter, nearly empty.
  Camera is a fixed side view (pos z=-120 looking +Z), gameplay on the XY plane at z~0.
  5,772 "no audio listener" lines logged before the ship (which carries the listener) spawned.
  QueueEntitySpawn(npc_probe_tutorial, ark_station) "not implemented" (OpenWorldGalaxyManager.cs:701) -> tutorial probe
  never spawns. Ship thumbnail persist fails: Texture 'ship_fighter' not readable (Player.cs:386).
  2x "referenced script (Unknown) missing" during SaveManager fingerprint (SaveManager.cs:899).
  Player.Name null after entering "Commander". SAVES: test runs create slots under
  ~/Library/Application Support/Frank/Spaceship Game/saves/profiles/default/slots/commander_N -> CLEAN UP at end
  (user had no saves before).
- FIX (playthrough enabler, line counts preserved): ported legacy Input reads to Input System in
  ShipInputSystem.cs:25-27 (InputManager.moveAction + Keyboard shift), ThirdPersonCamera.cs:88-89 (Mouse.delta*0.1),
  EntityTooltipView.cs:107 (Mouse.position). FIX ThirdPersonCamera.OnEnable: currentY=angles.x overwrote the 20deg default
  pitch with 0 -> camera started EDGE-ON to the XZ play plane (belts/orbits drawn as flat lines). Now keeps 20deg when
  transform is flat. Verified: chase cam pitch 20, W flies ~70 u/s (shots/05-flying.png).
- R8 Radar shows only the player even with asteroids/planets nearby. "SECTOR: x , z" shows raw world units, not sectors.
- R9 RUNTIME-CONFIRMED double generation in ark_start_0_0: GO generator ark_station @(930,0,447) vs ECS generator
  ark_station @(-688,0,-99); GO planets rocky(-609,-334) gasgiant(-133,-331) vs ECS rocky(-160,-319) barren(-1303,-421);
  2 StarInstance at origin; 6 PlanetInstance visuals from both generators overlapping (gas giant -132,-332 vs rocky
  -159,-319). Interactable copy (GO) and collidable copy (ECS) are different objects at different places. All system
  objects live in DontDestroyOnLoad. Hyperlane GO @1650 vs ECS @1668.75.
- TOOLING v3: gh_teleport (--x/--z or --near name; zeroes PhysicsVelocity AND ShipMovementComponent.CurrentVelocity,
  which overrides physics every frame), gh_near (ECS roots + GOs near player), gh_interact --name (calls OnInteract,
  mirrors LockPlayerForDialog), scratchpad/opt.sh <digit> (choose dialog option + print UI/logs), toworld.sh (~20 s).
- R10 BLOCKER: station interaction effectively impossible. OpenWorldPlayerController.FindNearestEntity
  (OpenWorldPlayerController.cs:55-80) requires distance to entity CENTER <= interactionRadius 7.2 (and the GO only has a
  16-unit trigger sphere), but large entities (ARK station ~300u voxel wheel) have solid ECS hulls; physics pushed the ship
  ~157u out from the centre. Radius predates the 1.5->4 scale change. Fix: interaction distance to collider surface /
  per-entity radius from bounds. Also: system objects ORBIT continuously (station moved ~260u between calls).
- R11 ARK station dialog (via gh_interact): text "Welcome to Nexus Station! The bustling marketplace..." -> CONTENT
  MISMATCH with intro (ARK is the player's home/cryo ship). Options: Marketplace, Station Bar, Research Lab,
  Mission Control (debug menu), All Ships Intact, Hangar Bay, Undock. Layout (shots/07-ark-dialog.png): portrait image
  covers the intro text; options 4-7 below the screen edge; HUD toolbar + stray rect still drawn over the dialog.
- R12 Merchant (shots/09-trade.png): category tab row overflows both screen edges (All/Misc clipped); grid only uses
  4 columns; "Research Blueprint 0 Scrap"; merchant sells the whole catalog (~65 items). Buying works (5000->4900);
  bought card disappears. Opening trade logs "Inventory expanded to 30 slots" (side effect on open).
  Player.Name is null even after entering a commander name (StartNewGame name not stored on Player).
- R13 Ship editor via ARK "Visit Hangar Bay" (shots/10-hangar.png): GALAXY HUD (fuel/scrap/health/energy/shields/radar/
  sector) stays drawn over the editor and hides the editor's "SHIP STATS" title; "Voxels: 0 / 0" until first edit;
  TRACKED MISSIONS text dark-on-dark; item tooltip half-transparent over radar; ghost placement block renders MAGENTA
  (shader unsupported on Metal). Editor spawns 965 PlacedBlock GameObjects (1 per voxel).
- FIX (playthrough enabler): new Assets/Game/Core/Utils/InputCompat.cs (Input System-backed drop-in for legacy
  Input.GetKey/GetKeyDown/GetMouseButton*/GetAxis/mousePosition); ShipEditorController.cs + FreeFlyCamera.cs switched
  Input.* -> InputCompat.* (line counts unchanged).
- TOOLING v4: virtual "AgentMouse" device (native mouse kept clobbering injected state with pos 0,0 when unfocused;
  harness disables native mice during play and restores on exit + safety net on reload), gh_mouse --dx/--dy, gh_inv,
  scratchpad/hangar.sh (~25 s to editor), to.sh <secs> <cmd> (perl alarm timeout).
- R14 Editor UX bug: when the selected hotbar slot is empty/non-block, curEditType=None and ShipEditorController.Update
  returns (ShipEditorController.cs:145-148) -> you CANNOT REMOVE blocks unless holding a block item.
  Placement raycasts from screen centre (camera forward), not mouse. RemoveBlock uses hit.transform.GetComponent
  (would silently fail if collider on a child).
- R15 RUNTIME-CONFIRMED ITEM DUPLICATION: in editor removed 11 blocks (965->954, inventory 85->96), pressed Tab (Continue)
  WITHOUT saving -> back in galaxy ship unchanged (linked=994) but inventory kept 96. Infinite dupe.
- R16 After editor round trip: editor "SHIP STATS Voxels 954/1000" HUD remains visible in galaxy; ASTEROID BELTS DOUBLED
  (Asteroid* 962->1924, PhysicsCollider 192->383, entities 1229->1432) -> belt entities never destroyed on scene change;
  player returned at (-6.7,0,244.8) vs pre-editor spawn ~(60,0,0) (consistent with drift bug, measure more).
- R17 Tab is bound to Player/Continue + UI/IntelPage + Combat/ChangeCamera simultaneously (InputSystem_Actions).
  F12=ShowShipEditor, F5=SaveShipData, X=ToggleSymmetry, Z=AutofireToggle, Space=Fire, E=Interact, I=Inventory, F=Fleet.
- R18 ParrelSync "Do you like ParrelSync?" modal (AskFeedbackDialog.cs) appeared on reload and BLOCKED the Editor main
  thread (CLI unreachable) -> dismissed via Close (sets StopShowingKey). Another reason to remove ParrelSync.
- R19 RUNTIME-CONFIRMED SOFTLOCK CHAIN after "Visit Hangar Bay" -> editor -> Tab back:
  DialogManager.IsDialogActive stays TRUE with no dialog visible (EndDialog never called on OPEN_SHIP_EDITOR);
  OpenWorldPlayerController.dialogEntity stays set -> its Update() returns early (OpenWorldPlayerController.cs:43) ->
  player can NEVER interact with anything again; player GO stays PARENTED to the orbiting ark_station
  (LockPlayerForDialog SetParent) -> galaxy manager lastLocal drifts with the station orbit ((100,100) real ship vs
  lastLocal (-31,222) then (-41,234)) -> saved position drifts -> after reload player lands away from systems
  (= the "systems not showing up" mechanism). Re-entering the station via the stale dialog no longer opens the hangar.
  Tab press also toggled the INTEL page open (multi-binding). F12 (ShowShipEditor) does nothing in the galaxy.
- TOOLING v5: gh_entities --component X (lists entities w/ component values via reflection), gh_spawn_ship (uses game's
  CombatManager.SpawnShip w/ Resources/Items/Ships/<name>), gh_aim (virtual mouse -> nearest enemy screen pos).
- R20 Weapons: Space fires (beams+plasma), asteroids die (192->189), explosion VFX OK (shots/11-firing.png). Asteroids
  have 1/1 HP and some 0/0 HP (alive with zero max health). Player ship drifts +X ~0.3 u/s with no input.
- R21 AI COMBAT (enemy only via harness spawn; game never spawns one): frigate (119 HP, 124 voxels) orbits at ~84u and
  fires; player (fighter: 965 HP, 994 linked voxels -- bigger than the frigate) lost ~1 HP/s, voxels 1085->1075.
  Autofire (Z) = continuous fire at MOUSE cursor, not at targets. With aim: beams visibly connect but enemy 119->73 over
  ~30s, its voxel count unchanged, energy 200->1 in ~10 s of autofire (regen ~40/s when idle). TTK > 1 min for the
  smallest frigate -> combat damage model broken/underpowered (consistent with static finding: splash centred on
  post-step pos / wrong-voxel resolution). Enemy NOT shown on radar. Debris: ~6 ScrapTag entities live at a time.
- R22 Perf baseline (galaxy, ~1,350 entities, 2 ships): cpu 4.4 ms, gpu 4.4 ms, 240 draw calls, 64 setpass,
  1.77M triangles / 2.0M verts (very high for the scene), mono heap 185 MB, total alloc 1.06 GB.
- R23 Ship sizes (ShipHealth/linked voxels): base 5, shuttle?/~54, 71, frigate 124, 278 x2, 352 x2, 766, 791, vestari 955,
  starting FIGHTER 994 (largest!). Several distinct ship types share identical voxel counts (possible blob-cache collision).
  Voxels are 0.5 world units (GridPosition 1 -> 0.5 u) so a 955-voxel ship is ~8 u across.
- R24 SCALING (editor, M-series Mac, play mode): baseline 1 ship/966 voxels: cpu 3.7 ms, main 2.8, gpu 2.7, 184 draws,
  0.22M tris. 44 vestari in view (42,590 ShipVoxel entities, IDLE): cpu 10.5 ms, main 8.5, gpu 5.8, 1,172 draws,
  306 setpass, 2.81M tris (~66 tris/voxel -> no hidden-face culling, detailed per-voxel meshes).
  ~0.14 us main-thread per idle voxel -> 1M-voxel station ~140 ms/frame idle. (shots/17-44-ships.png)
  Radar shows none of the 44 ships.
- R25 Overlapping spawns (63 ships, ~24k voxels) -> mass ramming destruction: 40 ships destroyed, cpu spike 16.4 ms,
  15x ArgumentException in ECB playback (EntityComponentStore.AppendRemovedComponentRecordError via
  EntityCommandBuffer.Playback -> SetComponentData on an entity missing the component / destroyed) = runtime
  confirmation of the ship-death/ECB ordering bug.
- R26 12 HOSTILE vestari (12.3k voxels) vs player: main thread 38-44 ms (~22 FPS) during combat vs 8.5 ms for 42k idle
  voxels -> combat systems (AI/weapons/projectiles/destruction/collider rebuild) dominate, not rendering. Ship death again
  threw the ECB AppendRemovedComponentRecordError exception. Visual (shots/18-12-enemies.png): voxel debris spray looks
  good (vision works!), but BEAMS render full length THROUGH their targets (not clipped at the hit point).
- R27 Pause menu: Continue/Save/Load/Settings/Quit + key hints. SAVE works (slot commander_19) but NO on-screen
  confirmation. Each New Game creates a new slot (19 from my runs). Save = state.json 132 KB + state.bak.json +
  descriptor.json + audit_save_state.json + audit_history.log per slot. Fleet (1 ship, 994 voxels) = 148 KB of 150 KB
  (~150 B/voxel JSON -> 1M-voxel station ~150 MB). MapSeed=0, Seed=0 (never assigned). HasExactPlayerTransform=false.
  Load list: every slot labelled identically "Commander 2026-10-09" (no time/location/slot id).
- R28 *** REPRODUCED "SYSTEMS NOT SHOWING UP" *** Save at (250,-300), move to (600,600), Load from PAUSE menu:
  log "Loading system ark_start at (0,0) (Distance: 848.53)" then "Unloading system ark_start out of range
  (Distance: 1697.06)" = floating-origin offset (600,600) applied TWICE -> home system unloads -> empty space.
  Also: galaxy absPos stays (600,600) (current pos overwrote saved (250,-300)); player GO destroyed (null);
  ECS player ship root survives with voxels gone (ShipVoxel 966->1) at (14419,0,13736); world 1229->69 entities;
  load list UI stays open over the running game; ECB AppendRemovedComponentRecordError during
  OpenWorldGalaxyManager.OnSceneLoaded -> InitializeOpenWorld -> SpawnPlayerAtOrigin (OpenWorldGalaxyManager.cs:62/115/344/411).
  (harness log collector missed this load's logs; read Editor.log directly)
- R29 GALAXY CONTENT: OpenWorldConfig has only 2 systems: ark_start @(0,0) and base_solar_system @(3000,0);
  load radius == unload radius == 1500; solarSystemScaleMultiplier 4.
- R30 HYPERLANE: not auto-trigger; E within 7.2u of lane centre -> AccelerateShip (BoostSpeed=routeStrength 300,
  6 s along launch dir). After using it: floating origin absPos jumped to (7087,-13591), player local (-3029,-989)
  -> ~15k units from BOTH systems, activeSystems empty (nothing loads = "systems not showing up" again);
  hyperlane GO reported destroyed; NullReferenceException spam x1635 in AsteroidConstructionJobSystem.cs:122
  (RenderMeshUtility.AddComponents with lost render mesh desc/array after rebase/scene change); Asteroid* entities 2308
  (leaked belts). [note: this run followed the broken pause-load run in the same Editor session; ran editor_stop +
  fresh StartScene first]
- R31 Inventory (I) (shots/22-inventory.png): top row clipped under the frame; Sort/Stack All buttons float outside the
  panel over the HUD; many identical "broken block" scrap icons don't stack. Fleet (F) (shots/23-fleet.png): STATS and
  GLOBAL MODIFIERS headers overlap the thumbnail/stat rows; "ThrusterForce +150" overlaps "Thrusters 0"; Weapons/Energy/
  Regen/Thrusters/Shields all 0 (ship has 14 weapons); raw id "ship_fighter" shown as name; "[FLAGSHIP]" overflows card.
- CLEANUP: test save slots moved (not deleted) to scratchpad/test-saves; play mode stopped.
- R3 DontDestroyOnLoad at boot: GameInitializer, GAME MANAGERS PREFAB(Clone), "New Game Object", SaveManagerAutoCreated
  (SaveManager auto-created separately from managers prefab -> singleton fragility). 17 UIDocuments all alive in DDOL from
  the start menu (UIRunEnd, UILootPicker etc. included). Editor fps ~260 on start menu.

## Subagent report summaries (full reports copied into docs/audit/)

### 05 narrative/quests (done)
- Commit 1069164e deleted the only publishers of `system.entered`, `territory.entered`, `entity.interacted`
  -> ReachSystem / VisitFactionTerritory / InteractEntity objectives can never progress; Whisper Chain storyline uncompletable.
- Content references removed item IDs (500, 717185106) while runtime IDs are array indices 0..73
  (GameDatabase.cs:221, commit 3c3216b2) -> gate activation / survey turn-in / all collect quests impossible.
- Dialog softlocks from ARK station hub: PLAY_FROM_POOL with no pool assets, 5 START_COMBAT refs to missing
  combat assets, a missing page, stale dialog state (EndDialog never called after START_COMBAT/OPEN_SHIP_EDITOR).
- Quests complete instantly on accept (objectives check lifetime history). consumeOnUse dead (IsOptionConsumed 0 callers).
  Abandoned quests re-acceptable (incl. story). Renamed quest auto-completes on load + runs side effects.
- Localization effectively broken: importer never saves SharedTableData -> GameText_en has 3,599 orphaned entries vs 5 keys.
- Event bus: one throwing listener starves others (incl. save capture). No handler leaks found.
- Duplication: 4 action vocabularies, 5 condition dialects, DialogManager vs NarrativeActionEngine duplicate side effects.
- Recommendation: Narrative Director over typed fact store + Outer Wilds-style clue/rumor graph; one condition/effect
  vocabulary compiled at import; storylets; Yarn Spinner 3 for dialog (verify 6.6 compat). Keep the writing/lore.

### 04 persistence/data (done)
- Saves silently rejected after almost any content change (SaveManager.cs:513-526, 788-961): exact schema v3 +
  fingerprint over all Resources items/factions/systems/entities + Application.version + loot table size;
  StartView.cs:140-144 returns with no message.  <-- major "data reset" cause
- Load replaces Inventory object (Player.cs:196) but notification relay / ShipItem->fleet conversion / GalaxyView HUD
  stay bound to the old one.
- Ship damage never saved (CombatManager.CaptureShipDamage 0 callers). Inventory slot positions/toolbar/capacity not saved;
  stacks past slot 60 dropped. No autosave (QueueAutosave never called), no save on quit.
- Load from pause menu in galaxy: galaxy manager keeps writing current position into save data during async reload.
- Roguelike leftovers wipe progress (defeat trims fleet; continue after run end wipes inventory/fleet/quests);
  new games get CreativeModeLootTable + 5000 credits.
- SO runtime mutation: GameDatabase.cs:204-213 item keys/names; ship data in DEV mode. Item ID 0 = real item AND "unresolved".
  ShipData.PrebakeStats resolves IDs on a loop copy -> wrong prices/abilities (editor tools bake into assets).
- Galaxy seed never assigned -> every campaign seed 0.
- Settings: music volume compounds toward 0; AudioManager never subscribes (wakes first); brightness/sensitivity/invert/
  resolution/rebinds/locale stored but unused or not persisted. All input maps always on; Tab triggers 3 actions;
  36 legacy Input.* calls bypass cutscene lock.
- Domain reload enabled (EditorSettings.asset:26).
- Dead: InventoryData, BindableProperty, SerializableGuid/GuidExtensions, LootObject, StorageObject, CombatAudioListener,
  BrightnessController, TradeView, InstanceCombiner, TimelineSpeed, exported GameData/ItemsData/CombatEncountersData.json,
  NarrativeEvents.csv, ~560 lines SaveManager audit code, CombatScene in Build Settings but missing, SerializedCollections,
  duplicate ParrelSync, 48 assets with old `Id:` field.
- Recommendation: Boot scene/GameRoot + Services; ContentDef SOs w/ stable keys, runtime idx 1..N (0 reserved), baked to blobs;
  GameSession; chunked save (JSON chunks + binary voxel chunks, per-chunk versions/migrations, ISaveParticipant,
  worker-thread write, File.Replace); palette+RLE+Deflate 16^3 voxel chunks; galaxy = seed + deltas; input contexts; AudioMixer.

### 06 UI/ship editor (done)
- CRITICAL item duplication in editor: inventory changes on place/remove but ship only on save; Tab exits w/o save ->
  infinite dupes; template load gives free blocks; removing combat-destroyed blocks refunds full block; F5 on invalid
  ship still writes inventory (ShipEditorManager.cs:377-397,716-748; ShipEditorController.cs:75-95,178-189).
- CRITICAL load time O(N^2): validation per voxel during ship load.
- Fleet close leaves game paused (FleetView.cs:75); Esc closes 5/17 views then unpauses (UIManager.cs:85-94);
  pause is a single bool; hotkeys fire while typing (f/i/x toggle screens, Tab exits editor).
- Editor selected-block index resets while toolbar keeps selection -> ghost shows one block, places another, throws on non-block.
- Symmetry wrong for asymmetric multi-cell blocks; mirrored removal deletes whatever covers mirror cell; plane fixed X=0.
- Key rebinding throws (actions enabled); merchant buy-one removes whole stack; inventory shows 59/60 slots.
- Visual: editor stats "Voxels: 0 / 0" on entry and stays visible in galaxy; toasts hidden behind Merchant/Inventory;
  5 Fleet stats always 0; ghost footprint uses mid-rotation; wireframe shader needs geometry shaders (unsupported on Metal).
- Dead: TradeView(480), StorageViewPage, LootPickerView, RunEndView + UXML/USS; all ShipSelection/, GhostBlockCollision,
  UIShaders/*, WorldUI.uxml, event_dialogue.uss, ShipBlockVisualOnly.prefab; MicroBar unreferenced.
  Duplicates: repair cost x3, thumbnail loader x2, open-editor x2, cell rotation math x5.
- Recommendation: one root UIDocument w/ layers; navigation stack drives pause/input contexts; MVVM w/ Unity 6 runtime
  binding; editor on shared chunked 16^3 voxel grid (same as runtime + saves), 24 orientations + mirror tables,
  chunk meshing, DDA picking, command-based undo/redo, draft/commit BOM session; in-world editing.

### 01 voxel core (done)
- Multi-cell blocks ignored at runtime (root cell only for connectivity + one sphere collider) -> detaches voxels editor
  considered attached; holes in big block colliders.
- VoxelPhysicsUtils.ResolveVoxel bit count wrong (lzcnt(count-1) vs Unity Physics lzcnt(NumChildren)) -> power-of-two
  voxel counts make hits damage the wrong voxel.
- Compound collider blobs leak (never disposed on rebuild/death).
- Shields can never work: shield prefab ref never baked; tag on Prefab entity that singleton queries skip.
- Debris vanishes when ship dies (destroyed voxels stay in LinkedEntityGroup; ship dies at 40% voxel loss).
- Voxels tied to subscene but ships not: leaving GalaxyMapScene destroys voxels, ships survive as invisible colliders.
- Collider rebuild postponed indefinitely under sustained fire; ship blob cache key ignores rotation/colour/modules;
  baked mesh map uses string.GetHashCode, no baker deps; asteroid belts not shifted by floating origin + never destroyed;
  negative seeds -> ~half asteroids identical; ship-asteroid ramming no damage; movement overwrites physics velocity
  (rams have no physical response).
- Dead: ShipVoxelBuffer, ShipDefinitionAuthoring, PlayerShipCameraFollowSystem (commented out), GameObjectSyncSystem/
  ObjectTransformSync/SyncTransformDictionary, ShipBlobReference, VoxelPrefabReference, AllModuleSlots, ShipVoxel.Size/
  IsExposed, ShipData blob+finalizer, 2 unused .obj meshes.
- Estimate ~300 B CPU + 112 B GPU per voxel entity; caps ~20-50k live voxels.
- Recommendation: sparse grid of 16^3 bricks (2-4 B/cell), Burst culled-face mesh per brick, box-merged collider per
  brick in one compound, DDA hit resolution, islands -> new ships, pooled particle debris; only functional blocks as entities.
  1M voxels: ~300MB -> 15-40MB.

### 03 galaxy/world (done)
- First galaxy gen (GalaxyManager/SolarSystemGenerator/StarNode) deleted in 1069164e/442a4e7b. NOW TWO generators run
  per system: old GameObject one (interaction, radar, hyperlane boost) + new ECS one (colliders, orbits, belts)
  (OpenWorldGalaxyManager.cs:454-477). Both use global UnityEngine.Random and yield mid-gen -> two different
  overlapping solar systems, different every session.
- "Systems not showing up": (1) player position drifts after every ship-editor round trip (_lastPlayerLocalPosition never
  reset; saved pos already includes it; OpenWorldGalaxyManager.cs:172-196,401,666-692) -> can land outside every 1500u
  load radius; (2) pause-menu load: already-loaded systems survive under persistent manager anchored to old origin;
  (3) load radius == unload radius == 1500 (no hysteresis) while hyperlanes sit ~1650 after scale 1.5->4 (bfa6daaf)
  -> systems unload as you approach lanes/flicker; (4) no galaxy map, radar 1000u, offscreen icons hidden;
  (5) new game spawns inside the star (50u offset < star radius), intro camera at star centre;
  (6) NPC fleets never spawn (prefab 2DFleet deleted).
- "Data reset": SaveManager fingerprint rejection -> blank campaign silently; from pause menu next checkpoint
  overwrites rejected slot (permanent loss). Destroyed entities never saved (EncounterManager.cs:278/378/386 old overload
  passes empty systemId -> OpenWorldGalaxyManager.cs:587-608 no-op). Entity IDs depend on random rolls consumed by
  other code (audio/dialog/other generator) -> saved "interacted" state drifts.
- ECS leaks on system unload: planet/star/hyperlane physics bodies (orbit floating origin as invisible colliders),
  asteroid belt entities, collider blobs. Every scene change leaks the player's ECS ship.
- Dead: MapConnectionVisualizer.cs (1 line), MapCameraController.cs, PlayerShipCameraFollowSystem.cs, OrbitalCamera.cs
  (on no GO), EntitySpawnCondition.cs, SolarSystemEntityCatalog.cs, GalaxyData.cs+asset, CombatEncounter+assets,
  stub world hooks (QueueEntitySpawn, RegisterPersistentEntityReplacement, RequestExitCurrentSolarSystem) which break
  story/quest spawns. Cinemachine installed, unused.
- Recommendation: galaxy = pure seeded data (per-system hash seeds, slot-based IDs), save deltas only; hyperlane jumps
  between systems + free flight within; one ECS generation path, orbits as f(time); double-precision galaxy coords
  w/ single floating-origin owner; faction territory sim; data-driven encounter director; mystery state gating lanes;
  Cinemachine rig.

### 07 project health (done)
- Dead packages: Post Processing v2 (0 refs, stale define on 16 platforms), Visual Scripting, AI Navigation,
  Cinemachine, collab-proxy, ide.vscode (0 refs). Timeline only used by ShipSelectionController (itself unreferenced).
  Localization effectively unused (3,599 entries / 5 keys; fr/es empty).
- Third-party mostly unshipped: Yughues 2/228 files used (577 MB!), Space Skyboxes 7/110, Cartoon FX 20/484.
  FastScriptReload, SerializedCollections, ParrelSync, EasyWallCollider unused (latter for a 6-line helper).
- LEGAL RISK: "Starsector Visuals" appears extracted from commercial game Starsector (Hegemony, Kazeron, Cryosleeper art);
  22 files ship incl. default cursor.
- Repo: .git 1.9 GB, no LFS; history has a deleted 672 MB texture pack; 14 tracked log/scratch files at root incl 30 MB
  MSBuild log; all root build logs are stale dotnet-build errors.
- Settings: 4/6 quality tiers point to deleted URP assets; "High" tier disables opaque texture (breaks Blackhole heat
  distortion); build list has deleted CombatScene (enabled) + SpaceStationScene; Active Input Handling = Both (9 legacy
  files); domain reload ON hides ~40 mutable statics.
- Visual: StarlightMat uses built-in particle shader -> magenta; shadows 512px over 500m (~1-2 m/texel vs 0.5 m voxels);
  galaxy camera MSAA + FXAA together; fog + realtime GI on in space scenes.
- ECS/physics: PhysicsStepAuthoring in main scene (never baked); PhysicsSetup.cs builds partial PhysicsStep
  (collision tolerance 0); PhysX AND Unity Physics both run (50 Hz vs 60 Hz); voxel baker reads Resources with no
  DependsOn (stale bakes); 2 shipped encounter assets with missing scripts (one used by a narrative event to start combat);
  3 Resources paths in code don't exist.
- Architecture: one 39k-line assembly with dependency cycles (VoxelShip<->Combat, Managers<->UI, Inventory->UI);
  proposes 12-asmdef layered layout + fresh-project structure with LFS.

### 02 combat/destruction (done)
- COMBAT CANNOT HAPPEN: only SpawnShip call spawns the player (OpenWorldGalaxyManager.cs:399); EncounterManager.StartEncounter
  rejects every combat encounter (EncounterManager.cs:177-192) -> no AI ship ever exists; AI/ramming/shields/abilities dormant.
- When re-enabled: wrong voxel hit on power-of-two child counts (VoxelPhysicsUtils.cs:63-72 vs Physics CompoundCollider.cs:1294);
  collision/trigger events read outside fixed step + event buffer never cleared -> ram damage FPS-dependent, projectile hits
  lost <60 FPS; splash centred on post-step position with radius < one step travel -> zero damage; ship death deletes ship +
  all debris same frame (possible 'entity does not exist'); collider leak per rebuild + rebuild starvation; parallel data
  races on health/energy; movement overwrites velocity; ram damage uses post-resolution speed; ship-asteroid no damage;
  flak never fires (no prefab mapping); missile/flak base damage 0; homing locks allies; fire rate frame-dependent.
- Dead: shields (never created/enabled), abilities never granted/activated, ScrapMagnetismSystem (empty),
  EnsurePhysicsSimulateSystem (query never matches), ShieldComponent, ShieldHitEvent, ProjectileHit, CombatResult,
  OverclockAbilitySO (copy of Boost), BoostCameraController, AsteroidMiner, CameraShake* (no scene), PortalTrigger.cs
  (class/file mismatch), extraction/portal stubs, sink-on-death, NetCode remnants, 2 unreferenced projectile prefabs.
  OpenWorldCombatRefactorGuide.md is stale.
- Recommendation: explicit fixed-step combat pipeline; raycast projectiles -> damage events -> one race-free resolver per ship
  removing cells in grid space; incremental connectivity; small islands -> GPU debris (VFX Graph buffer), mid -> pooled debris
  bodies, large -> hulk ships; impulse/energy ramming with force-driven ships; explosions on voxel grid; spatial hash +
  utility AI + steering + fleets; data-driven abilities.
