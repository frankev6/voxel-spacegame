# 03 — Galaxy, Solar Systems, Open World, Encounters, Factions, Travel, Map/Camera

Auditor scope: `Assets/Game/Core/Galaxy Map/**`, `Core/Managers/EncounterManager.cs`, `Core/Factions/`, `Core/Camera/`, `UI/GalaxyView*.cs`, `Prefabs/Galaxy`, `Resources/{Galaxies,Encounters,Factions}`, `Scenes/GalaxyMapScene.unity`, plus the seams into persistence, quests, asteroids and the ECS sync system.

**Method.** I read every in-scope file in full. I resolved script GUIDs against every `.unity`, `.prefab` and `.asset` file, read the config and scene YAML, and went through the git history (`1069164e`, `442a4e7b`, `881da586`, `bfa6daaf`). **Nothing was run in the editor or a player.**
- **Verified** means read directly in code or asset YAML.
- **Inference** means reasoned about how Unity or Entities behaves at runtime.

All paths are relative to the repo root.

---

## 0. TL;DR

- **Two half-migrated solar-system generators run side by side today** (`OpenWorldGalaxyManager.cs:454-477`).
  - The legacy GameObject generator handles interaction and the radar.
  - The ECS generator handles physics colliders.
  - Both seed and consume the *global* `UnityEngine.Random` across coroutine `yield`s, so they produce **two different, overlapping solar systems**. Neither is reproducible across sessions.
- **"Systems not showing up": the most likely causes, in order.** All are verified in code.
  1. The player's position gets a **double offset** after every scene round-trip, for example to the ship editor. The player respawns displaced, often outside every system's 1500-unit load radius.
  2. **Loading a save from the pause menu** reloads the world scene without un-streaming the old systems, so they end up anchored to the wrong origin.
  3. **Load radius == unload radius == 1500**, but hyperlanes sit at about 1650 units. A system (and its hyperlane) is destroyed as you fly toward the hyperlane.
  4. **There is no galaxy map and no long-range representation of systems.** The radar clips everything beyond 1000 units.
  5. On a new game the player spawns **inside the start star**. During the intro the camera sits at the star's centre.
- **"Data reset": the most likely causes.**
  1. **SaveManager refuses any save** whose content fingerprint (all item, faction, system and entity IDs plus the app version) has changed. The game then silently creates a fresh campaign.
  2. **Destroyed entities are never persisted.** EncounterManager calls a legacy overload that drops the systemId, so the write is a no-op.
  3. **Entity IDs are derived from non-deterministic rolls**, so saved interacted state stops matching.
- **ECS leaks.** Unloading a system leaks every planet, star and hyperlane physics body (they then orbit the floating origin as invisible colliders), all asteroid-belt entities, and every collider blob. Each ship-editor round-trip also leaks the player's ECS ship.
- **Headline recommendation.** Treat the galaxy as **pure seeded data**: a graph of systems with hash-derived per-system seeds and generator-structural IDs. Save **only deltas**. Make **jump travel between instanced systems** the core travel model instead of seamless float space. Use **one ECS generation path** with analytic orbits, and **double-precision galaxy coordinates** with a single floating-origin owner. Cinemachine is installed but unused; adopt it.

---

## 1. Architecture map

### 1.1 Generations of galaxy code

| Gen | Code | Status | Evidence |
|---|---|---|---|
| 1. Node-graph galaxy | `GalaxyManager(.MapGeneration/.SolarSystem).cs`, `SolarSystemGenerator.cs`, `SolarSystemEntityInstance.cs`, `StarNode.cs`, `SolarSystemPlayerController.cs`, `GalaxyShapeGenerator.cs`, `GalaxyFactionBorderRenderer.cs`, `MapConnectionVisualizer.cs` | **Deleted** in `1069164e` and `442a4e7b`. Leftovers: `GalaxyData.cs`/`GalaxyData_01.asset`, `MapCameraController.cs`, the 1-line `MapConnectionVisualizer.cs`, the "Legacy API Bridge" in `OpenWorldGalaxyManager.cs:655-738`, `PersistentSingleton.cs:22-26` (special-cases "GalaxyManager"), and the `StarNode` layer in TagManager. | `git show --stat 1069164e` (−1486 GalaxyManager, −1343 MapGeneration, −1078 SolarSystem, −1303 SolarSystemGenerator) |
| 2. Open-world GameObjects | `OpenWorldGalaxyManager`, `OpenWorldSolarSystemGenerator`, `OpenWorldSolarSystemEntityInstance`, `HyperlaneEntityInstance`, `OpenWorldPlayerController` | **Live.** Drives interaction, encounters, the radar and hyperlane boosts. | Added in `409b86a6`/`8baa69c5` |
| 3. ECS | `EcsSolarSystemGenerator`, `EcsSolarSystemEntityInstance`, `OrbitSimulationSystem`, `SolarSystemCoreComponents`, `Asteroid/*` | **Live in parallel.** Provides physics colliders, the ECS orbit simulation and asteroid belts. Not interactable. | `bfa6daaf` ("--- ECS SIDE-BY-SIDE SPAWN ---", `OpenWorldGalaxyManager.cs:469`) |

### 1.2 Live runtime flow (verified)

```
StartScene / GalaxyMapScene / ShipEditorScene each contain GameInitializer.prefab
  GameInitializer (PersistentSingleton, DontDestroyOnLoad)          GameInitializer.cs:17-58
    GameDatabase.Initialize -> LoadOpenWorldConfig()                GameDatabase.cs:105,110-118
      Resources.Load<OpenWorldGalaxyConfig>("Galaxies/OpenWorldGalaxyConfig")
    Instantiate(GAME MANAGERS PREFAB)   root = UIManager, CombatManager, InputManager, PauseManager,
                                        EncounterManager(DontDestroyOnLoad on root), AudioManager,
                                        SettingsManager, DialogManager, QuestManager
                                        child GO = OpenWorldGalaxyManager    (prefab line 1669)
sceneLoaded("GalaxyMapScene") -> OpenWorldGalaxyManager.OnSceneLoaded    OpenWorldGalaxyManager.cs:58-72
  InitializeOpenWorld: absolute origin = saved pos, or config.solarSystems[0] coords   :74-116
  SpawnPlayerAtOrigin -> CombatManager.SpawnShip(flagship, _lastPlayerLocalPosition)  :331-413
Update (every frame, world scene only)                                   :160-186
  SavePlayerState (writes CampaignState.OpenWorldPlayerPosition every frame)
  UpdateFloatingOrigin (if |pos.xz| > 5000: shift GOs + all SolarSystemEntityTag + ShipMaster entities)
  ProcessSystemStreaming: for each SolarSystemData in config (O(N) per frame)    :427-495
     dist <= loadRadius  -> new GO "SystemGenerator_*"   (OpenWorldSolarSystemGenerator, coroutine)
                         -> new GO "EcsSystemGenerator_*" (EcsSolarSystemGenerator, coroutine)
     dist > unloadRadius -> DestroySystem() on both
OpenWorldPlayerController (on player ship prefab): OverlapSphere -> nearest *legacy* entity -> E -> OnInteract
  -> EncounterManager.StartEncounter (only NarrativeEncounter supported)          EncounterManager.cs:163-193
  -> hyperlane: HyperlaneEntityInstance.AccelerateShip (sets ECS boost)           HyperlaneEntityInstance.cs:76-121
GalaxyView.Map (radar) reads legacy generators only                              GalaxyView.Map.cs:101-105
ThirdPersonCamera on Main Camera (GalaxyMapScene.unity:760-784), target assigned by ReassignCameraTarget
```

### 1.3 Data and content actually in use (verified)

**`Resources/Galaxies/OpenWorldGalaxyConfig.asset`**
- Exactly **2 systems**: `ark_start` at (0,0) and `base_solar_system` at (3000,0). They are connected to each other by hyperlanes.
- `systemLoadRadius: 1500` and `systemUnloadRadius: 1500` (lines 19-20).
- `floatingOriginThreshold: 5000`.
- `solarSystemScaleMultiplier: 4` (changed from 1.5 in `bfa6daaf`) and `orbitDistanceMultiplier: 7.5`.

**World scale, derived from code plus config**
- Planets orbit at roughly 290–1370 world units.
- Hyperlanes sit at `(50 + 45/n)·7.5·4` ≈ **1620–1670** units (`OpenWorldSolarSystemGenerator.cs:189`, `EcsSolarSystemGenerator.cs:163,252`).
- The star visual radius is about 120–160: `star_entity.asset:39-40` scale 30–40 × 4, on a unit-radius `sphere_high_lod.obj`.

**Seed:** `CampaignState.Seed` is **never assigned**. It is only read (`OpenWorldGalaxyManager.cs:123`, `SaveManager.cs:762`). The per-system seed is therefore `systemId.GetHashCode()` (`OpenWorldGalaxyManager.cs:131`), and every campaign has the identical galaxy.

**Unreferenced content**
- Entity assets not in any pool: all 4 NPC fleets, `exitgate_active`, `exitgate_dormant`, `asteroidbelt_1`, `debris_field_entity`, `resrource_cache_entity`, `star_entity_blackhole`.
- `GalaxyData_01.asset` and `ExtractStablePoint.asset` are loaded by nothing.

**`GalaxyMapScene.unity` roots**
- EntitiesSubscene, with a `PhysicsStepAuthoring` GameObject parented to it *in the main scene*, so it is not baked. `PhysicsSetup.cs` creates the `PhysicsStep` at runtime instead.
- LIGHTING group. The Directional Light is **inactive** (line 631).
- "local volume" (inactive, line 343) and Global Volume.
- Main Camera with `ThirdPersonCamera` and URP data: far clip **5000** (line 861), FOV 40, at the origin.
- An **inactive** Blackhole prefab instance (line 562).
- ParticleSpawner.
- A GameInitializer prefab instance.
- RenderSettings: **exponential grey fog** (`m_Fog: 1`, colour 0.86 grey, density 1e-4, lines 17-20) and the "Starfield Skybox" material.

### 1.4 Third-party packs (usage only)

- **Planets of the Solar System 3D:** 2k/4k planet textures used by the `PlanetEntity.planetTextures` arrays, `Moon.prefab` (moon_1) and `Asteroids.fbx` (asteroidbelt_1, which is unreferenced).
- **Starfield Skybox:** the GalaxyMapScene skybox.
- **Space Skyboxes:** one material.
- **Starsector Visuals:** 19 files used as narrative illustrations and map icons (`hegemony_bar.jpg`, `kazeron.jpg`, `icons/station0.png`, …).
  - **Licensing risk (inference):** the names match the commercial game *Starsector*. Verify the license before shipping.

---

## 2. Bugs

Severity: Critical / High / Medium / Low. Confidence: in the logic (verified code) and, where it differs, in the runtime symptom.

### 2.1 Root-cause analysis: "systems not showing up"

**S1. Double-counted player offset after a scene round-trip.** Critical. Logic confidence: High.

How it happens:
- While flying, `_lastPlayerLocalPosition` = the world position P relative to the current floating origin (`OpenWorldGalaxyManager.cs:172-178`).
- Every frame the save gets the absolute position `origin + P` (`:193-196`).
- Leaving the world scene calls `ResetEntireState` (`:666-692`). This resets the origin but **not** `_lastPlayerLocalPosition`.
- On return, `InitializeOpenWorld` sets the origin to the saved absolute position `origin + P` (`:81`). It then spawns the ship at `_lastPlayerLocalPosition = P` (`:401`). The player is now at `origin + 2P`.

When it happens: every trip to the ship editor (`FleetView.cs:477`, `DialogManager.cs:637`, `NarrativeActionRuntime.cs:456-460`, return at `ShipEditorController.cs:94`). This is the core "build your ship" loop.

Effect:
- P can be up to 5000, the origin-shift threshold, while systems load at ≤1500. One round-trip can put you in empty space, and each trip adds another P.
- While docked (parented, `:168-174`) the same field holds a *parent-local* offset. That path is only accidentally close to correct.

**S2. Loading a save in-game misplaces all loaded systems.** High. Confidence: High.

How it happens:
- `PauseView.cs:105-114` loads the slot and reloads `GalaxyMapScene` in Single mode.
- `OnSceneLoaded` only resets for *non-world* scenes (`:60-71`).
- The generator GameObjects are children of the DontDestroyOnLoad manager (`:455,:471`), so they survive with positions relative to the **old** origin.
- `activeSystems` still marks them as loaded, so they are not regenerated.
- `InitializeOpenWorld` then sets a new origin from the save (`:81`).

Effect:
- Systems render at the wrong offset until the player leaves their unload radius.
- The old player GameObject was destroyed with the scene, but its ECS ship entity leaks (`CombatShip.cs:92-95` only deregisters sync). That leaves a ghost player ship and two `PlayerTag` entities. `AsteroidBeltCullingSystem.cs:36-41` picks "the first" one.

**S3. No streaming hysteresis, and hyperlanes outside the unload radius.** High. Confidence: High.

- Load and unload radii are both 1500 (config lines 19-20), with tests `<=` and `>` (`:448,:479`).
- The hyperlane sits at about 1650 units. As you approach it, the player crosses 1500 and **the whole system, including the hyperlane, is destroyed**.
- Hovering near 1500 thrashes load and unload. Each reload regenerates with non-deterministic RNG (S/D3) and leaks ECS entities (B7).
- This regressed when the scale changed from 1.5 to 4 in `bfa6daaf`. The old hyperlane radius was about 540.

**S4. Nothing shows where systems are.** High (design gap). Confidence: High.

- No galaxy map exists. `MapCameraController` is unused and the old GalaxyView map was removed in `1069164e`.
- Other systems are not rendered at all. They are streamed out, beyond the 5000 far clip, and there are no impostors.
- The radar uses `RadarWorldRadius = 1000` (`GalaxyView.Map.cs:15`) against system extents of about 1670, with `overflow: hidden` (`GalaxyViewUI.uxml:25`) and no edge clamping. Most of the current system's entities never appear.
- The neighbouring system 3000 units away is invisible until you're within 1500 of it.

**S5. New-game spawn inside the start star; intro camera at the star's centre.** Medium-High. Logic confidence: High. Visual symptom: Medium.

- The spawn offset is (50,0,0) (`:103`).
- The star's visual radius is about 120–160 and its ECS collider radius is 60–80 (`EcsSolarSystemGenerator.cs:281`), so the ship spawns inside both. Kinematic depenetration pushes the ship.
- Before the player owns a ship (intro and ship-choice encounters), `ThirdPersonCamera` has no target and returns early (`ThirdPersonCamera.cs:71-73`). The camera stays at the scene origin (0,0,0), which is the `ark_start` star centre.
- Inference: back-face-culled sphere, so from inside the star is effectively invisible.

**S6. NPC fleets can never appear.** High. Confidence: High.

- `NPCFleetEntity` prefab GUID `3266d903…` (`npc_enemy_1.asset:16`, and the same in all 4 fleets) is `2DFleet.prefab`, deleted in `442a4e7b`.
- `SpawnEntity` returns null when the prefab is missing (`OpenWorldSolarSystemGenerator.cs:237`, `EcsSolarSystemGenerator.cs:235`).
- The legacy fleet initialisation is a TODO (`OpenWorldSolarSystemGenerator.cs:308-315`).
- `bfa6daaf` also removed the fleets from both system pools.
- Similarly, `StarEntity.galaxyMapPrefab` points at the deleted `Pulsar.prefab` (`star_entity.asset:45`).

**S7. Floating-origin shifts break belts and the camera.** Medium today, High at scale. Confidence: High.

- The shift does not trigger in current content (systems 3000 apart, threshold 5000), but it will as soon as the galaxy grows. Details are in B9.
- Asteroid belt centres are never shifted, so belts spawn displaced by the shift amount.

**S8. Fog plus two-tier visual mismatch.** Low-Medium. Inference.

- Exponential grey fog (scene lines 17-20) greys Lit objects by about 14% at 1500 units and about 39% at 5000.
- Two overlapping planet sets (B1) make "which one is real" ambiguous.

**Historical note:** the deleted Gen-1 code had `galaxyData.seed != -1 ? seed : DateTime.Now.Ticks` (`git show 1069164e^:Assets/Game/Core/Managers/GalaxyManager.MapGeneration.cs:62`). Any `seed = -1` asset regenerated a new galaxy on every load. The same landmine is copied into `OpenWorldGalaxyManager.cs:126-129`, which is dormant only because `Seed` stays 0.

### 2.2 Root-cause analysis: "system data getting reset"

**D1. The save compatibility gate discards saves on any content change.** Critical. Logic confidence: High.

- `LoadSlot` rejects a save (`SaveManager.cs:521-526`) unless `GameContentFingerprint` matches. That fingerprint is a SHA of every item key, faction ID, `SolarSystemData` ID and `SolarSystemEntity` ID (`:855-938`).
- `EnabledContentFingerprint` includes `Application.version` (`:944`), and the compatibility fingerprint includes `BuildVersion` (`:850`).
- Adding one item or entity asset, or bumping the version, makes **every** save fail to load.
- In the editor dev path (`GameInitializer.cs:52-55` → `AttemptAutoLoadMostRecentSlot`), the failure is silent. On world load, `EncounterManager.EnsureActiveSaveData` (`:195-206`) creates a blank `GameSaveData`, and `OnSceneLoaded` hydrates it as the campaign (`:123-135`). From the StartScene, the UI gets `null` from `LoadSlot` (`StartView.cs:140`, `PauseView.cs:105`).
- **The failure makes things worse.** `StartView.cs:138-139` and `PauseView.cs:103-104` call `SetActiveSlot(slot)` *before* `LoadSlot`. On failure they only `return`, with a log line and no UI message. From the pause menu, the running game keeps playing with `ActiveSlotId` now pointing at the rejected slot. The next checkpoint (`EncounterManager.cs:309-311`) **overwrites that old save with the current, unrelated campaign**, so the data is permanently lost. Logic confidence: High.
- For a developer who keeps adding content this looks exactly like "my galaxy/system data reset". It is probably the #1 cause during development. It overlaps the persistence auditor's area.

**D2. Destroyed entities are never persisted.** Critical. Confidence: High.

- `EncounterManager.EndEncounter`/`DestroyCurrentEntity` call `MarkEntityDestroyed(starSeed, instanceId)` (`EncounterManager.cs:278, 378, 386`).
- That overload calls `MarkEntityDestroyed(string.Empty, entityId)` (`OpenWorldGalaxyManager.cs:587-592`).
- `GetSolarSystemSaveData("")` returns null (`:539`), and `UpsertEntityPersistenceState(null, …)` returns without writing (`:608`).
- `EcsSolarSystemEntityInstance.MarkDestroyed`/`OpenWorldSolarSystemEntityInstance.MarkDestroyed` only set a run variable and publish an event. The generators check `SolarSystemSaveData`, not the variable (`OpenWorldSolarSystemGenerator.cs:239-250`).
- `currentInteractingSystemId` is available (`EncounterManager.cs:342`) but unused for this.
- Result: destroyed derelicts, quest targets and fleets reappear whenever the system re-streams or the game reloads.

**D3. Generation is non-deterministic, so persisted IDs drift.** Critical. Logic confidence: High. Visible frequency: Medium.

- Instance IDs are `"{entityId}_{systemSeed}_{i}"` (`OpenWorldSolarSystemGenerator.cs:79`, `EcsSolarSystemGenerator.cs:89`). The *type* at slot `i` is a weighted roll on the **global** `UnityEngine.Random`, made after a `yield` (`:51`, `:66`).
- Between `InitState` and the resume, other code consumes the same global RNG:
  - the other generator, started in the same frame (`OpenWorldGalaxyManager.cs:467,477`);
  - `AudioManager.cs:149-150` (hit sounds);
  - `CameraShake.cs:75-76`;
  - `DialogManager.cs:270`, `NarrativePool.cs:31,81`, `LootSystem.cs:27-86`, `EncounterPool.cs:35`.
- The ECS generator additionally consumes draws outside any save/restore: `pe.GetRandomRadius()` and `se.GetRandomScale()` (`EcsSolarSystemGenerator.cs:280-281`). These are skipped for persisted-destroyed entities (`:244`), so **destroying one entity shifts every later roll**.
- Result: after a reload the same ID can denote a different body, or not exist. Saved `Interacted` and `Destroyed` states silently stop applying ("reset"), and layouts change between visits.

**D4. Streaming thrash regenerates systems** (see S3). Each regeneration re-rolls with D3's non-determinism.

**D5. A fresh run keeps the stale world, and a respawn lands at (0,0).** Medium.

- `InitializeFreshRun` zeroes `OpenWorldPlayerPosition` but keeps `IsInOpenWorld` and `SolarSystemSaveDataList` (`EncounterManager.cs:530-555`).
- The next world load takes the "returning player" branch, so the player spawns at absolute (0,0) plus a stale local offset (the start star). The previous run's deltas are also kept.
- `HandlePlayerDefeat` does the same (`:505`), but it has no callers.

**D6. The fingerprint check scans the whole of Resources on every save.** Perf-adjacent. `Resources.LoadAll<SolarSystemData>("")` and `LoadAll<SolarSystemEntity>("")` run on every save (`SaveManager.cs:899,919`), via `BuildCurrentBaseline` from `SaveActiveSlot` (`:411`).

### 2.3 Other bugs

| ID | Sev | Conf | Bug | Evidence |
|---|---|---|---|---|
| B1 | Critical | High | Two divergent solar systems per system. Legacy (interactable, radar, PhysX triggers) and ECS (kinematic colliders, no interaction) overlap at different orbits. Every prefab, including the star's **soft-shadowed point light** (range 1500, `StarSolarSystem.prefab:5128-5136`), is instantiated twice. Players collide with planets they can't interact with and can interact with planets they pass through. | `OpenWorldGalaxyManager.cs:454-477`; `OpenWorldPlayerController.cs:14,63` (legacy type only); `GalaxyView.Map.cs:101-105`; `EcsSolarSystemEntityInstance.cs:55` |
| B7 | High | High | **ECS leaks on unload or reset.** `DestroySystem` destroys only GameObjects and the centre entity (`EcsSolarSystemGenerator.cs:355-376`). Physics bodies (`:248`), belt entities (`:196`) and collider blobs (`SphereCollider.Create` `:283`, never disposed) leak. Leaked bodies lose their `CenterEntity`, so `OrbitSimulationSystem.cs:66-70` falls back to `float3.zero` and they orbit the floating origin (near the player) as **invisible colliders**. Leaked belts keep spawning asteroids around a stale centre (ghost or duplicate belts). | as cited |
| B7b | High | High | The **player ECS ship leaks on every scene change.** Single-mode unload destroys the CombatShip GameObject before `sceneLoaded`, so `ResetEntireState`'s `if (playerInstance != null)` (`OpenWorldGalaxyManager.cs:681`) is false and `DespawnShip` never runs. The ECS ship (with `PlayerTag`, rendered by Entities Graphics) persists. | `CombatShip.cs:92-95`, `CombatManager.cs:233-255` |
| B9a | High at scale | High | `AsteroidBeltData.CenterWorldPosition` is a snapshot (`EcsSolarSystemGenerator.cs:210`). The belt entity has no `LocalTransform`, so `ShiftAllEcsEntities` (`OpenWorldGalaxyManager.cs:273-276`) never moves it. Culling and layout use the stale centre (`AsteroidBeltCullingSystem.cs:63`, `AsteroidLayoutJobSystem.cs:74`). | |
| B9b | Medium | Med (runtime) | On the shift frame, orbiting bodies read the centre's **previous-frame** `LocalToWorld` (`OrbitSimulationSystem.cs:30-33,67-70`, before `LocalToWorldSystem`). They are placed at the unshifted position for one frame, with `velocity = Δ/dt` of about 300 km/s (`:75-81`). That gives a one-frame teleport and possible huge speculative contacts on the next physics step. | |
| B9c | Medium | High | The camera is not shifted. `ThirdPersonCamera` lerps `smoothedTargetPosition` (`:114-127`, `positionSmoothing: 20` in the scene), so it swoops through 5000 units over several frames, with the ship beyond the far clip of 5000. | |
| B9d | Medium | High | Only `ShipMaster` and `SolarSystemEntityTag` entities are shifted (`:264-316`). Projectiles, debris and scrap (`EntityLifetimeSystem`), beams, loot, world-space particles and trails jump. | |
| B10 | Medium | High | Collider/visual mismatch. The ECS planet radius uses a *different* random draw than `PlanetInstance` (`EcsSolarSystemGenerator.cs:280` vs `PlanetInstance.cs:47-50`). The star collider is half the visual radius. The ECS interaction trigger radius `physicsRadius+2` is in local space under the ×4 parent (`EcsSolarSystemEntityInstance.cs:63-66`), so it is 4× too large. | |
| B11 | High | High | Story and quest world hooks are **stubs**: `QueueEntitySpawn`, `RegisterPersistentEntityReplacement` and `RequestExitCurrentSolarSystem` (`OpenWorldGalaxyManager.cs:699-715`). They are called by `NarrativeActionRuntime.cs:269,563,590,608` and `QuestActionExecutor.cs:120,134,143`. The exit-gate fallback encounter uses `world.exit_system` (`SolarSystemEntity.cs:138`). Story gates and quest spawns do nothing. | |
| B12 | High | High | `GameEventTypes.SystemEntered`, `FactionTerritoryEntered` and `EntityInteracted` (`GameEventBus.cs:7-9`) are **never published** anywhere, so `ReachSystem` quest objectives (`QuestManager.cs:1010-1011`) can't complete. The `event_territory_*` narrative assets are unreachable. | grep: no `Publish` of these |
| B13 | Medium | High | Factions are disconnected from the world. `CurrentSystemFactionId` is always `""` (`OpenWorldGalaxyManager.cs:659`) and every entity has an empty `ownerFactionIdOverride`. Relation changes from trade or fleet kills (`EncounterManager.cs:292-296,347-359`) never fire. `GameDatabase.GenerateFactionDoctrineFleet` has no callers. There are two "Tlaxcala" factions (`faction_4`, `faction_tlaxcala`). | |
| B14 | Medium | High | `GetCurrentGalaxy()` returns null (`:694-697`), so `GenerateEncounterLoot` always returns an empty array (`EncounterManager.cs:416-427`, used by `LootPickerView.cs:117`). | |
| B15 | Medium | High | PhysX `OnTriggerEnter` never fires. Neither the player prefab (trigger sphere r=10, **no Rigidbody**, `NEW COMBAT SHIP PLAYER.prefab:102-121`) nor any entity GameObject has a Rigidbody, so `HyperlaneEntityInstance.cs:54` and `OpenWorldPlayerController.cs:165` are dead paths. | |
| B16 | Medium | High | Global RNG pollution. Generators `InitState(systemSeed)` the global RNG (`OpenWorldSolarSystemGenerator.cs:41`, `EcsSolarSystemGenerator.cs:56`; `PlanetInstance.cs:47` does it without restore), so gameplay rolls after a system load (loot, encounter pools, dialog) become seed-correlated and repeatable. | |
| B17 | Medium | Med | `string.GetHashCode()` is used for persisted seeds and texture picks (`OpenWorldGalaxyManager.cs:131`; generators `:280,:304,:313,:315,:334`). It is stable on Mono today, but .NET documents it as unstable across processes, and it is randomised on CoreCLR, which Unity is migrating to. | |
| B18 | Medium | High | The hyperlane fling aims through the destination star. `routeStrength 300 × duration 6` = 1800 units from about 1650 (`hyperlane_default.asset:35-36`, `HyperlaneEntityInstance.cs:93-101`). The direction points exactly at the other system's centre, 1350 away, so the ship rams the star. (Masked today by S3.) | |
| B19 | Low | High | Hyperlane orientation differs between paths. The asset `orbitType: 0` (Static, not Stationary) means the legacy path skips the explicit rotation (`OpenWorldSolarSystemGenerator.cs:322`) and tidal-locks to face *inward* (`OpenWorldSolarSystemEntityInstance.cs:121-129`). ECS faces *outward* (`OrbitSimulationSystem.cs:87-91`). Two portals overlap with opposite facing. | |
| B20 | Medium | High | Material leak per streaming cycle. `renderer.material` instantiates materials (`PlanetInstance.cs:73-75,122-125`, `StarInstance.cs:51-61`) that are never destroyed. Orbit lines create `new Material(Shader.Find(...))` as a fallback (`OpenWorldSolarSystemGenerator.cs:353`) and `Resources.Load` per line (`:346`). | Unity docs: `.material` instances are caller-owned |
| B21 | Low | High | `EcsSolarSystemEntityInstance.MarkDestroyed` nulls `PhysicsEntity` (`:139`), so `OnDestroy` never deregisters it from `EcsToGameObjectSyncSystem` (`:80-83`), leaving a stale entry. `DestroyCurrentEntity` calls `MarkEntityDestroyed` twice and only searches *legacy* instances (`EncounterManager.cs:378-399`), so the ECS twin survives. | |
| B22 | Low | High | `systemInstanceId`/streaming keys use culture-sensitive float formatting (`OpenWorldGalaxyManager.cs:443,730`). A non-integer coordinate on a comma-decimal locale gives different keys. | |
| B23 | Low | High | `EncounterManager.HandleSystemGenerated` is bound to the static `OnSystemGenerated` (`:64,:208-228`). Every streamed system resets narrative input mode. This couples streaming to UI state. | |
| B24 | Low | High | Content errors: `planet_water.entityId = "planet_ice"` (duplicate, `planet_water.asset:15`); `debris_field_entity.entityId = "derelict_ship_1"`; `encounter_extraction.asset`/`encounter_survivor_1.asset` reference **deleted scripts**; `GalaxyData_01` references a missing `SolarSystemData` (`95f497dd…`); `CombatScene.unity` is listed in Build Settings but deleted; `Encounter` is abstract but has `[CreateAssetMenu]` (`Encounter.cs:8`). | |
| B25 | Low | High | Asteroid belts: the radius is *not* multiplied by the system scale (`EcsSolarSystemGenerator.cs:188`, unlike planets at `:252`). `cullingAngleRad = 8000/r` exceeds π for r < 2546 (`:193`), so angular culling is effectively off. The belt `OrbitAngle` advances while asteroids don't orbit, so sectors churn as the belt "rotates". `BeltId = Entity.Index` is reused after destroy. | |
| B26 | Low | Med | `ThirdPersonCamera` owns cursor lock state (`:75-84`) and uses legacy `Input.GetAxis` (activeInputHandler = Both). Lerp smoothing is frame-rate dependent (`:121`). | |

---

## 3. Dead, deprecated and duplicate code (with proof)

"No refs" means the script GUID is in no `.unity`, `.prefab` or `.asset`, and the type is unreferenced or only self-referenced in code.

| Item | Proof | Action |
|---|---|---|
| `Galaxy Map/MapConnectionVisualizer.cs` | 1 line: `// Deleted for Open World` | delete |
| `Galaxy Map/MapCameraController.cs` (236 lines) | no refs (GUID `4b85b02a…` unused; no code users) | delete |
| `Camera/PlayerShipCameraFollowSystem.cs` | 100% commented out; commented call sites in `OrbitalCamera.cs:77-82,218-223`, `ThirdPersonCamera.cs:157-162` | delete |
| `Camera/OrbitalCamera.cs` | not on any GameObject. Only `GetComponent` lookups (`OpenWorldGalaxyManager.cs:359`, `GalaxyView.Map.cs:313`) and an unused `ShipSelectionController`. `TryCaptureState`/`ApplySavedState` have no callers. `SolarSystemCameraStateData` and `OrbitalCameraStateData` are dead save types. | delete (Cinemachine replaces it) |
| `Core/ShipSelection/*` | GUIDs in no scene or prefab | delete or confirm with the UI auditor |
| `CameraShake.cs`, `CameraShakeController.cs` | controller never placed, so `ShipHealthSystem.cs:41` is always a null no-op | re-implement on Cinemachine Impulse |
| **Entire legacy generator path** (`OpenWorldSolarSystemGenerator`, `OpenWorldSolarSystemEntityInstance`, `HyperlaneEntityInstance`) **or** the ECS path | about 80% copy-paste duplicates (compare `OpenWorldSolarSystemGenerator.cs:32-231` with `EcsSolarSystemGenerator.cs:51-229`) | keep ONE (ECS, see §5) |
| `EntitySpawnCondition.cs` | `CanEntitySpawn`/`Evaluate` have no callers; `spawnRequirement`/`spawnBlocker` are empty in every asset | delete, or wire into the director |
| `SolarSystemEntityCatalog.cs` | only a fallback when `config.defaultHyperlaneEntity` is null (it isn't); `Resources.LoadAll("")` scans everything | delete |
| `GalaxyData.cs` + `GalaxyData_01.asset` | no runtime loader; `GetCurrentGalaxy()` returns null; `GetRandomSolarSystem`, `GalaxyShape`, `WeightedFaction` and the territory fields are Gen-1 | mine for ideas, then delete |
| `CombatEncounter`/`FleetFormation`, `encounter_boss/extraction/survivor` assets | `GetRandomFormation` has no callers; `StartEncounter` rejects non-narrative encounters (`EncounterManager.cs:188-192`); two assets have missing scripts | delete; rebuild as director data |
| `NPCFleetEntity` fields (detection, patrol, chase, speed, doctrine*) | each referenced only at its declaration; prefab missing | redesign |
| `StarEntity.minPlanetOrbitRadius`, `maxSystemRadius`, `galaxyMapPrefab`; `SolarSystemData.GetStarVisual`, `encounterOverride`, `asteroidBeltEntity`; `PlanetEntity.preferredOrbitDistance`; `PlanetInstance.GetPlanetType/GetRadius`; `StarInstance.GetTemperature/GetLuminosity/GetScale` | declaration-only (grep count = 1) | prune |
| `OpenWorldGalaxyManager` "Legacy API Bridge" (`:655-738`): `IsInSolarSystem`, `CaptureCurrentSystemCheckpoint`, `CurrentStarSeed` (always 0), the `MarkEntityDestroyed(int,…)` overload, three stubs, `GetCurrentGalaxy` | no callers, constants, or stubs (see B11) | replace with a real world-service API |
| Save fields: `SpawnedEntityRecords`, `ReplacementRecords`, `PlayerAnchorState`, `RuntimeSpawnCounter`, `PersistentWorldMutations`, `HasSpawnedInCampaign`, `EntityStateEntryData.HasTransform/Position/EulerRotation`, `GenerationBaselineData.MapSeed` (always 0) | written or declared but never consumed | fold into the new delta schema |
| `FloatingOriginOffset` tag; `OrbitComponent.PositionOffset`; `isGenerating` in both generators; `HyperlaneEntityInstance.isActive/activeParticles` | never queried or set | delete |
| `EncounterManager.EndRun`, `HandlePlayerDefeat`, `ExtractSuccess`, the `RunEndView` chain, `ResolveExistingGalaxyManager` (`:751`, also `SaveManager.cs:778`) | no callers | delete or re-home |
| No-op null patterns: `if (X != null) X.Foo(); else X?.Foo();` | `EncounterManager.cs:418-420,479-480,508-509,525-526`; `QuestActionExecutor.cs:119-122,133-137,142-145,195-197` | clean up |
| `SceneLoader` constants `BattleSceneName`, `ManagersSceneName`, `SpaceStationScene`; `CombatScene` in Build Settings | scenes deleted in `442a4e7b` | delete |
| Scene leftovers: inactive Blackhole instance (`GalaxyMapScene.unity:562`); unbaked `PhysicsStepAuthoring` (`:154-180`); disabled Directional Light; `Galaxy.prefab` (no refs); `StarNode`/`Tile`/`Unit` layers | as cited | delete |
| `PersistentSingleton.cs:22-26` "GalaxyManager" special case | Gen-1 type no longer exists | delete |
| `OpenWorldCombatRefactorGuide.md` | describes the pre-`bfa6daaf` plan; no longer accurate | archive |

---

## 4. Performance

Ordered by expected impact for the target of massive ships and stations in a big galaxy.

1. **Double generation (B1)**:
   - Every celestial prefab is instantiated twice, including the particle-heavy `StarSolarSystem.prefab`.
   - Two **soft-shadowed point lights** sit on every star (cube-map shadows, range 1500).
   - Legacy entities move PhysX colliders without Rigidbodies every frame (`OpenWorldSolarSystemEntityInstance.cs:102-132`; the planet prefab has a non-trigger `SphereCollider`). Moving static colliders forces broadphase updates.
2. **Leaks scale with play time (B7, B7b, B20):** physics bodies, belt entities, collider blobs, material instances, and ghost player ships accumulate per stream cycle or scene trip. Physics broadphase and the Entities Graphics batch count grow unbounded.
3. **`ProcessSystemStreaming` is O(N) per frame with allocation.** It iterates every system and builds an interpolated string key per system per frame (`OpenWorldGalaxyManager.cs:438-443`). That is fine for 2 systems but unusable for thousands. Use a spatial grid and precomputed IDs.
4. **The save path scans all of Resources** (`SaveManager.cs:899,919`) on every checkpoint, which is after every encounter (`EncounterManager.cs:309-311`), causing hitches.
5. **ECS generation does per-component structural changes.** There are about 11 `AddComponent` calls per body (`EcsSolarSystemGenerator.cs:248-299`), so each body moves through about 11 archetypes. Create from an `EntityArchetype` or a baked prefab entity with `Instantiate`. Share collider blobs per radius bucket.
6. **Floating-origin shift is a main-thread loop** with `Get`/`Set` per entity and a new `EntityQuery` per shift (`:264-316`). Use an `IJobEntity`. Better still, avoid global shifts (see §5).
7. **Radar runs every frame:** `new List` and per-entity `QuestManager` lookups plus style writes even when nothing changed (`GalaxyView.Map.cs:81-153`). It also runs while hidden.
8. **`OpenWorldPlayerController.FindNearestEntity`** allocates via `Physics.OverlapSphere` every frame (`:57`). Use `OverlapSphereNonAlloc`, or an ECS spatial query.
9. **Orbit `LineRenderer`s** use up to about 840 points each (`OpenWorldSolarSystemGenerator.cs:360`). Prefer a single ring shader or a mesh.
10. **Asteroid belts:** culling is effectively disabled (B25), so 24×8 dynamic asteroid bodies stay alive within 8000 units. `DespawnSector` scans all asteroids per sector (`AsteroidBeltCullingSystem.cs:167-187`).
11. **Logging and textures:** `Debug.Log` in hot paths (`StarInstance.cs:74`, streaming logs, `SceneLoader.cs:17,23` per frame while loading). Planet textures are 2k/4k (Earth_4k).

---

## 5. Recommended target architecture

### 5.1 Principles

1. **The galaxy is data, not GameObjects.** It is generated from `(GalaxySeed, GeneratorVersion)` and is fully reproducible. It is never saved, only the deltas are.
2. **Every random decision comes from a named, local, counter-based stream.** No `UnityEngine.Random` and no `string.GetHashCode`.
3. **IDs come from generation structure, not from what was rolled.**
4. **One owner per concern:** a coordinate-frame owner, a streaming owner, a world-event publisher, and a delta store.
5. **ECS for everything simulated or physical**, including celestial bodies. GameObjects only for UI and hero VFX.

### 5.2 Determinism and IDs

```csharp
// Stable 64-bit hashing (xxHash/SplitMix64), NOT string.GetHashCode.
static ulong H(ulong a, ulong b) => SplitMix64(a ^ (b * 0x9E3779B97F4A7C15UL));
static ulong StableHash(string s) => XxHash64.Hash(Encoding.UTF8.GetBytes(s)); // or FNV-1a 64

ulong systemSeed  = H(galaxySeed, (ulong)systemIndex);           // or H(galaxySeed, StableHash(authoredSystemId))
ulong layoutSeed  = H(systemSeed, Purpose.Layout);               // purpose-separated substreams:
ulong lootSeed    = H(systemSeed, Purpose.Loot);                 // adding a loot feature can't move planets
ulong visitSeed   = H(H(systemSeed, Purpose.Encounters), visitCount);
var rng = new Unity.Mathematics.Random((uint)(layoutSeed | 1));  // local, Burst-friendly
```

- **Body IDs are structural**: `BodyId = (SystemId, Role, Slot)`, for example `(sys42, Planet, 3)`, `(sys42, Moon, 3.1)`, `(sys42, Lane, toSys17)`. The template rolled for a slot is derived *from* that ID's sub-seed, so persisted state always points at the same slot.
- **Never consume RNG conditionally** on persisted state. Generate everything first, then apply deltas: hide destroyed bodies, apply replacements, add story spawns.

### 5.3 Data layout

```csharp
// ---- Immutable, regenerated on load (never saved) ----
public struct SystemId : IEquatable<SystemId> { public uint Value; }
public struct GalaxyDef {                       // NativeArrays, Burst-generated, ~100k systems feasible
    public ulong Seed; public int GeneratorVersion;
    public NativeArray<StarSystemDef> Systems;  // sorted by SpatialCell for streaming
    public NativeArray<LaneDef> Lanes;           // hyperlane graph (CSR adjacency)
    public NativeParallelMultiHashMap<int2, int> Cells; // galaxy spatial hash
}
public struct StarSystemDef {
    public SystemId Id; public double2 PosLy;   // galaxy frame in light-years (double)
    public ulong Seed; public byte StarClass; public ushort RegionId;
    public ushort TemplateIndex;                 // -> SolarSystemData archetype
    public ushort AuthoredOverride;              // ark_start, story systems (0 = procedural)
    public SystemFlags Flags;                    // Story, Hidden, Locked, Anomaly...
}
public struct LaneDef { public SystemId A, B; public float LengthLy; public LaneKind Kind; public ushort GateFlag; }

// ---- Mutable, saved (deltas only) ----
[Serializable] public class GalaxySaveState {
    public ulong GalaxySeed; public int GeneratorVersion;
    public double UniversalTime;                       // drives analytic orbits
    public List<SystemDelta> Systems;                  // only touched systems
    public FactionTerritoryState Territory;            // owner/influence diffs vs seeded baseline
    public MysteryState Mystery;                       // flags, revealed systems/lanes, clues
    public List<SystemId> Visited, Known;              // fog of war
}
[Serializable] public class SystemDelta {
    public uint SystemId;
    public List<ulong> DestroyedBodies, InteractedBodies;
    public List<SpawnRecord> StorySpawns;               // replaces QueueEntitySpawn stubs
    public List<ReplaceRecord> Replacements;            // dormant gate -> active gate
    public List<VoxelDiffRef> StructureDiffs;           // damaged stations/asteroids -> separate chunk files
    public uint VisitCount;
}
```

**Save versioning:**
- Validate the *schema* only. Resolve content by ID at load. A missing ID is dropped with a warning, not a refused save.
- `GeneratorVersion` bumps get a migration hook. Never refuse a save because content was added (fixes D1).

### 5.4 Coordinates and floating origin

Use three frames:

| Frame | Type and unit | Used for |
|---|---|---|
| Galaxy | `double2` light-years | map, territory, lanes |
| System | `double3` metres or AU, relative to the star | analytic orbits, stations, fleets "on rails" |
| Local bubble | `float3` relative to a per-system `OriginAnchor` | Unity Physics, rendering, voxel ships |

- **Recommended:** while in a system, keep the origin at the **system anchor**, re-centred on the player only past a threshold.
  - Store `UniversePosition` (double3) on every persistent entity.
  - Have one `OriginRebaseSystem` in `InitializationSystemGroup`, so the rebase happens *before* physics and transforms. It updates `LocalTransform` for everything with `UniversePosition`, and raises a `OriginShifted` event for the camera, VFX and trails.
  - That one owner replaces the ad-hoc shifting in `OpenWorldGalaxyManager.cs:204-329` and fixes B9a–d.
- **Bodies on rails** (planets, moons, stations in orbit, belts) compute position = `f(UniversalTime, orbitParams)` in double each frame. Nothing to simulate, nothing to save, nothing to shift.
- Only bodies within the physics bubble get `PhysicsCollider`/`PhysicsVelocity`. Kinematic planets get `PhysicsVelocity` derived analytically, never `Δ/dt` of a teleport.

### 5.5 Travel model

I recommend **hyperlane jumps between instanced systems, with free flight inside systems**.

- The current model streams systems into one continuous float space 3000 units apart. That gives empty dead space, precision limits, streaming thrash and no meaningful sense of distance.
- **Inside a system:** sublight plus a "cruise/supercruise" drive whose speed scales with distance from gravity wells. Systems can be large (AU-scale in double) with on-rails bodies.
- **Between systems:** a jump along a `LaneDef` (graph edge), fuel-gated (`Player.FuelCount` exists).
  - Show a short warp transition while the old system unloads and the new one generates via jobs. Arrive at the lane's entry point at the destination, not aimed at the star (fixes B18).
  - Lanes and wormholes give chokepoints for faction territory and gates for mystery progression (dormant gates, locked lanes).
- **Optional later:** a "deep-space jump" to off-lane coordinates for exploration, using the same instancing.

### 5.6 Streaming tiers

| Tier | Contents | Representation |
|---|---|---|
| T0 Galaxy | every `StarSystemDef` | galaxy map: GPU-instanced points or a VFX Graph plus a lane line mesh; impostor "stars" in the skybox layer for known neighbours |
| T1 System far | star, planets, belts | analytic positions, LOD impostor billboards; no colliders |
| T2 System near | stations, derelicts, fleets, POIs | ECS entities (Entities Graphics), interaction via an ECS `Interactable` plus a spatial query |
| T3 Bubble | voxel ships, stations, asteroids near the player | full Unity Physics plus voxel destruction |

- Hysteresis everywhere: unload ≈ 1.25 × load, computed from each system's extent, never a global constant (fixes S3).
- Each loaded system owns its entities through a `SystemRoot` entity plus `LinkedEntityGroup` or a shared-component tag `SystemMember{SystemId}`. Unloading becomes one `DestroyEntity(query)` (fixes the B7 leaks).

### 5.7 ECS vs GameObject

- **ECS:** celestial bodies (rendered with Entities Graphics and Shader Graph planet, star and atmosphere shaders), stations, asteroids, fleets, hyperlane gates, orbits, interaction proximity, encounters' spawned ships, and the streaming bookkeeping.
- **GameObject:** UI Toolkit views, the Cinemachine rig, a handful of hero VFX (star corona, gate effect) driven from ECS through a presentation companion. Keep `EcsToGameObjectSyncSystem`'s `TransformAccessArray` approach, which is good, only for those.
- Remove the per-entity MonoBehaviour proxies and PhysX trigger colliders entirely. A Burst `InteractionQuerySystem` writes the nearest interactable to a singleton that the HUD reads.

### 5.8 Mystery progression hooks

- Make a `MysteryState` (flags, clue graph, revealed systems and lanes) the single source for world gating:
  - **authored `SystemOverride` assets** (for example ark_start, the gate system) pinned to deterministic galaxy positions or selected by rule (for example "first system in region 3 with a black hole");
  - **gated lanes** (`LaneDef.GateFlag`), **hidden systems** revealed by scanning, and **signals** on the galaxy map;
  - story spawns and replacements recorded as `SystemDelta.StorySpawns/Replacements`. This makes `spawn.entity`, `world.replace_entity` and `world.exit_system` real (fixes B11).
- The world layer publishes typed events (`SystemEntered`, `SystemScanned`, `BodyInteracted`, `BodyDestroyed`, `LaneJumped`) to the quest system (fixes B12).

### 5.9 Faction territory simulation

- **Seeded baseline:** faction capitals placed by rule, territory grown by weighted flood-fill over the lane graph. This revives the Gen-1 idea from `GalaxyData.factionTerritory*`.
- **Runtime:** a low-frequency `TerritoryTick`, per jump or per in-game day:
  - per-system `Influence[faction]`, `Owner`, `Stability`;
  - contested edges and war state;
  - player actions apply deltas (missions, kills, trade).
- **Save only the diffs** from the seeded baseline.
- Territory feeds the encounter director (patrol density, doctrine fleets via `FactionData.doctrine`), prices, and the map overlay. This also fixes B13: entities inherit the owner from their system.

### 5.10 Encounter director (data-driven)

- `EncounterDirector` runs on system entry and on a timer. Inputs:
  - system context tags (faction, danger, region, star class, story flags);
  - player power (ship voxel or block budget);
  - recent-history "tension" budget.
- It picks from **weighted tables with typed conditions**. Reuse the quest system's condition evaluator instead of the `REQ_VAR(...)` regex in `EntitySpawnCondition.cs`.
- Rolls use `H(systemSeed, Purpose.Encounters, visitCount)`, so they are reproducible but vary per visit. Outcomes are recorded in `SystemDelta`.
- Fleets spawn as ECS ships from faction doctrine. "Encounter" assets become data: fleet composition, behaviour preset, narrative hook.

### 5.11 Camera

- Cinemachine is installed (`Packages/manifest.json`: `com.unity.cinemachine 6.6.0`) and **unused**.
- Build one rig with `CinemachineBrain` and three `CinemachineCamera`s, blended by priority:
  1. **Flight**: ThirdPersonFollow or OrbitalFollow plus `CinemachineInputAxisController` on the Input System.
  2. **System map**: top-down orbit around the star.
  3. **Galaxy map**: orthographic or perspective over the T0 data.
- On origin rebase, call `CinemachineCore.OnTargetObjectWarped(target, delta)`. This removes the swoop (B9c).
- Use Impulse for shake, replacing the dead `CameraShakeController`.
- Move cursor-lock policy to an InputManager or UI state owner.

### 5.12 Migration path

| Phase | Work | Outcome |
|---|---|---|
| 0 (days) | §6 quick wins | stops position drift, leaks, lost saves and lost destroyed state |
| 1 | Delete the legacy GameObject generator. Make ECS the only path, with local seeded RNG and structural IDs. Add an ECS `Interactable` plus a query-based interaction. Add a `SystemMember` tag for clean unload. Fix persistence keys. | one system per system, deterministic |
| 2 | Introduce `GalaxyDef` (seeded, Burst) and `GalaxySaveState` deltas. Turn `OpenWorldGalaxyConfig.solarSystems` into *authored overrides* plus templates. Build the galaxy map UI from data. Publish world events. | real galaxy, map, quests can target systems |
| 3 | Jump travel between instanced systems. Analytic orbits in double. Single `OriginRebaseSystem`. Cinemachine rig. | scale without precision issues |
| 4 | Territory sim, encounter director, `MysteryState` gating, story spawn and replace records. | gameplay systems on top of a stable world |

**Worth keeping:**
- the `SolarSystemEntity` SO taxonomy (prefab, map icon, colour, encounter pool, spawn type, satellites) as body templates;
- `SolarSystemData` as a *system archetype*;
- the delta-save idea (`SolarSystemSaveData`/`EntityStateEntryData`);
- `EcsToGameObjectSyncSystem`;
- `StarInstance.TemperatureToColor` and the spectral class mapping;
- the asteroid sector-streaming concept, with fixes;
- `FactionData` (ID, doctrine, relations);
- `GameEventBus` as the world-to-quests seam.

---

## 6. Quick wins

Each is low-risk and done in hours.

1. **Stop double generation.** Remove one of the two spawn blocks at `OpenWorldGalaxyManager.cs:454-477`. Short-term, keep the legacy path (it has interaction) and disable the ECS block, or move interaction to ECS and remove legacy.
2. **Fix the position drift (S1).** In `ResetEntireState` and when restoring a saved position, set `_lastPlayerLocalPosition = Vector3.zero` (and reset the rotation). Spawn at `savedAbsolute - origin`.
3. **Reset on world reload (S2).** Call `ResetEntireState()` at the start of `InitializeOpenWorld`. Explicitly despawn the player ECS entity by querying `PlayerTag`, not via `playerInstance`.
4. **Persist destroys (D2).** Pass `currentInteractingSystemId` to `MarkEntityDestroyed(string, string)` in `EncounterManager.cs:278,378,386`. Delete the `int` overload. Have `MarkDestroyed` persist too.
5. **Make generation deterministic (D3, B16).** Replace global `Random` in both generators with a local `System.Random`/`Unity.Mathematics.Random` seeded per system. Pre-roll *all* values, including radius and scale, before applying persisted state. Derive IDs from slot index plus role.
6. **Clean unload (B7).** In `EcsSolarSystemGenerator.DestroySystem`, destroy each instance's `PhysicsEntity` and the belt entities, and dispose collider blobs. Belts should derive their centre from `CenterEntity`'s `LocalToWorld`, which also fixes B9a.
7. **Fix config (S3).** Set unload above load (for example load 2200 / unload 2800), both above the hyperlane radius of about 1670. Better, compute both from the system extent.
8. **Stop rejecting saves (D1).** Downgrade the content and version fingerprint mismatch in `SaveManager.ValidateCompatibility` (`:807-838`) to a warning. Keep only the schema version check, and surface load failures in the UI.
9. **Spawn outside the star (S5).** Set the offset to star visual radius + margin. Give the camera a default framing target, such as the system root, when the player has no ship.
10. **Radar (S4).** Clamp off-radius icons to the rim as arrows. Set `RadarWorldRadius` from the system extent. Add neighbouring system markers from config coordinates.
11. **Publish `SystemEntered`** from `ProcessSystemStreaming` when the player crosses into a system's radius. This unblocks `ReachSystem` quests (B12).
12. **Camera warp on origin shift.** Add `OnOriginShift(Vector3 d)` to `ThirdPersonCamera` that offsets `smoothedTargetPosition` and `transform.position` (B9c).
13. **Content fixes.** NPC fleet prefab (missing `2DFleet`), `planet_water.entityId`, the duplicate Tlaxcala faction, `encounter_extraction`/`survivor` assets with missing scripts, the Build Settings `CombatScene` entry. Turn off or blacken the GalaxyMapScene fog.
14. **Destroy instanced materials** in `PlanetInstance`/`StarInstance.OnDestroy`. Cache the orbit line material once (B20).
15. **Delete the dead files** in §3: MapConnectionVisualizer, MapCameraController, PlayerShipCameraFollowSystem, EntitySpawnCondition, SolarSystemEntityCatalog, the CombatEncounter assets, the no-op null patterns, and the legacy-bridge stubs, after replacing their callers.
