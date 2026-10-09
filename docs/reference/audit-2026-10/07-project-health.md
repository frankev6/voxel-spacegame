# 07 - Project Health Audit: packages, settings, third-party, assets, scenes, repo, rendering

Project: `/Users/frank/DEV/spaceship-game` - Unity 6000.6.5f1 (6.6). Audited 2026-10-09 (read-only).
Method: package manifest/lock, `.meta` GUID map (25,134 GUIDs across Assets, Packages, PackageCache), a reverse-reference index over every YAML asset (870 files), a **build-reachability graph** (seeds: the 3 enabled build scenes, every file under `Resources/`, assets referenced by ProjectSettings, Addressables groups; followed transitively through every GUID), `git` object inspection, and `Logs/Editor.log`.

> **The working tree changed while I was auditing, and I did not change it.** Between about 04:04 and 04:21, someone else deleted the embedded `Packages/com.unity.localization` copy and patched `GetInstanceID()` call sites in `Packages/com.lunawolfstudios.scriptablesheets/Editor/*`, `Assets/FastScriptReload/Scripts/Editor/FastScriptReloadManager.cs` and `Assets/Game/Core/Game Logic/GameDatabase.cs`. Findings below describe the state at audit start. Section 4 also records the latest compile state.

> **The Unity 6.3 to 6.6 upgrade is not committed.** `git diff ProjectSettings/ProjectVersion.txt` shows 6000.3.0f1 changing to 6000.6.5f1. The manifest diff shows Entities 1.4.3 changing to 6.6.0, URP 17.3 to 17.6, and so on. The upgrade can still be reverted.

---

## 0. Headline findings

1. **The project did not compile on Unity 6.6.** The three blockers were all vendor code: the embedded Localization 1.5.9 copy, the embedded ScriptableSheets package, and FastScriptReload. They failed with `CS0619 Object.GetInstanceID() is obsolete`. Because `Unity.Localization` failed, `Assembly-CSharp.dll` was never produced (it was missing from `Library/ScriptAssemblies`). Once the packages were swapped or patched, the first game-code error appeared: `Assets/Game/Core/Game Logic/GameSettings.cs(4,22)`, an unused `using Unity.Entities.UniversalDelegates;` that Entities 6.6 removed.
2. **Most third-party content is dead weight.**
   - YughuesFreeMetalMaterials: 2 of 228 files used (8.4 MB of 604 MB).
   - Space Skyboxes: 7 of 110 used.
   - JMO Cartoon FX: 20 of 484.
   - Starsector Visuals: 22 of 651.
   - scifi_ui: 5 of 603.
   - FastScriptReload, SerializedCollections, ParrelSync, EasyWallCollider: 0 (EasyWallCollider supplies one 6-line extension method).
3. **Legal risk: "Starsector Visuals" appears to be content extracted from the commercial game *Starsector*.** Examples are `illustrations/hegemony_bar.jpg`, `kazeron.jpg`, `cryosleeper_interior.jpg` and `icons/cryosleeper.png`. There is no license file. 22 of these files ship in builds today, including the default cursor set in PlayerSettings. Treat it as placeholder only and replace it before anything public.
4. **The git repo is 1.9 GB with no LFS.** The HEAD tree alone is 1,416 MB: 621 MB PNG and 567 MB TGA, mostly unused third-party textures. History also carries 672 MB of an already-deleted `Cartoon_Texture_Pack`, plus AllSkyFree, PolygonPirates and StylizedWater2. 14 log, scratch and snapshot files are tracked at the repo root, including a 30 MB MSBuild log.
5. **Dead or BiRP-only packages:**
   - `com.unity.postprocessing` (v2, BiRP-only): zero references anywhere. Its only effect is the `UNITY_POST_PROCESSING_STACK_V2` define on 16 platforms.
   - Visual Scripting, AI Navigation, Cinemachine, Collab-proxy: zero references.
   - `ide.vscode`: deprecated.
   - Timeline: used only by `ShipSelectionController`, which nothing references.
6. **Quality settings are broken.**
   - 4 of the 6 quality tiers point to URP assets deleted in commit `72366ce5`, so they silently render with the Ultra asset.
   - The "High" tier uses a URP asset with depth and opaque textures off. That breaks `HeatDistortionShader` (it samples Scene Color) on `Blackhole.prefab`.
   - Players can select any of these tiers from `SettingsView.cs:188`.
7. **Two build-list scenes no longer exist.** `CombatScene.unity` (still enabled) and `SpaceStationScene.unity` were deleted in commit `442a4e7b` but remain in `EditorBuildSettings`.
8. **Broken references shipped through Resources:**
   - `encounter_extraction.asset` and `encounter_survivor_1.asset` have missing scripts. `event_stable_point.asset` triggers `START_COMBAT(encounter_extraction)`, which therefore loads null.
   - Code loads `Resources` paths that do not exist: `Portraits`, `NarrativeImages/`, `Materials/OrbitLineMaterial`.
   - Three call sites use `Resources.LoadAll<T>("")`, which loads every Resources folder.
9. **Rendering visual bugs:**
   - `StarlightMat.mat` uses the built-in `Particles/Standard Unlit` shader (renders magenta in URP) on `StarSolarSystem.prefab/Plane`.
   - Main-light shadows are 512 px over 500 m with 2 cascades, roughly 1-2 m per texel, while voxels are 0.5 m (`ShipScale: 0.5`). Voxel self-shadowing cannot resolve.
   - The galaxy camera applies MSAA 2x and FXAA together.
   - Fog is on in both space scenes.
   - Realtime GI (Enlighten) is enabled in the lighting settings.
10. **Physics and ECS setup:**
    - `PhysicsStepAuthoring` sits in the main GalaxyMapScene, not in the SubScene, so it is never baked.
    - `PhysicsSetup.cs` builds `PhysicsStep` with a partial object initializer, which leaves `CollisionTolerance = 0` instead of 0.01.
    - The PhysX and Unity Physics worlds both run, at 50 Hz and 60 Hz.
    - The `VoxelPrefabAuthoring` baker calls `Resources.LoadAll` without `DependsOn`, so baked block data goes stale.
11. **One 39k-LOC assembly with dependency cycles** (VoxelShip and Combat reference each other; Managers and UI do too; Inventory, a core folder, references UI). Splitting into asmdefs requires breaking those cycles first. There are no tests.

---

## 1. Package audit

Versions are from `Packages/packages-lock.json`. "Refs" means references found in `Assets/Game` code (`using` or fully-qualified names) or in asset GUIDs.

| Package | Version | Used? | Evidence | Recommendation |
|---|---|---|---|---|
| com.unity.entities | 6.6.0 (core pkg since 6.4) | **Yes, heavily** | `using Unity.Entities` in 98 files; 27 `ISystem`, 14 `SystemBase`, 16 files with `IJobEntity` | Keep. Note that Entities 6.x removed `Unity.Entities.UniversalDelegates` (see GameSettings.cs:4). |
| com.unity.entities.graphics | 6.6.0 | Yes | `Unity.Rendering` in 9 files; MaterialMeshInfo/RenderMeshArray in 8 | Keep |
| com.unity.physics | 6.6.0 | Yes | 19 files; `PhysicsStepAuthoring` in GalaxyMapScene | Keep, and make it the only gameplay physics (see section 5) |
| burst / collections / mathematics | 2.0.0 / 6.6.0 / 1.4.0 (transitive) | Yes | Burst 27 files, Collections 40, Mathematics 64 | Keep. Consider listing them explicitly in the manifest. |
| render-pipelines.universal | 17.6.0 | Yes | 3 URP assets, Forward+ renderer | Keep. Consolidate the assets (section 5). |
| **postprocessing (PPv2)** | 3.5.4 | **No** | 0 code refs; 0 asset refs to any PPv2 script GUID; BiRP-only; only side effect is `UNITY_POST_PROCESSING_STACK_V2` in `scriptingDefineSymbols` for 16 platforms (ProjectSettings.asset:679-695) | **Remove**, then delete the define. URP Volumes already do bloom and tonemapping (`BaseCameraProfile 1(URP).asset`). |
| visualeffectgraph | 17.6.0 | Yes (small) | `GalaxyVFX.vfx` (Blackhole.prefab), `GalaxyVFXSlow.vfx` (GalaxyMapScene). Unused: `LaserBeamVFX.vfx`, root `NebulaDustVFX.vfx`, `SpaceDebrisVFX.vfx` | **Keep.** It is the right tool for GPU-scale voxel debris. |
| inputsystem | 1.20.0 | Yes (partial) | 5 files plus `InputManager` InputActions and `InputSystemUIInputModule`. **9 files still use legacy `UnityEngine.Input`**, including the ECS `ShipInputSystem.cs:25-27` and `WeaponInputSystem` | Keep. Finish the migration, then set Active Input Handling to "Input System Package" only. |
| ugui | 2.6.0 | Marginal | EventSystem in the managers prefab; Canvas in `ShipBlock.prefab`; Image in `ModuleSlotVisual.prefab`; MicroBar bars; QFSW console; TMP | Keep (TMP and QFSW need it). Use UI Toolkit for new game UI. |
| **localization** | manifest 1.5.13, but an **embedded 1.5.9 copy in `Packages/` overrode it** (lock: `file:com.unity.localization`, embedded) | Barely | 3 runtime files (SettingsView, NarrativeView, SettingsManager) plus the editor `NarrativeJsonImporter`. `GameText_en.asset` has **3,599 entries but `GameText Shared Data.asset` has only 5 keys**, so lookups by key fail and fall back to the inline text (`NarrativeView.cs:566-579`). fr and es tables are empty. The embedded copy did not compile on 6.6. | **Remove for now** (it also drags in Addressables and preloads at startup). Re-add when the narrative is stable and you really ship translations. Never embed Unity packages. |
| addressables | 2.11.2 (transitive) | Only via Localization | Groups are Localization-only; `Default Local Group` has `m_SerializeEntries: []` | Goes with Localization |
| **timeline** | 6.6.0 | **Effectively no** | Only `ShipSelectionController` (no scene, prefab or code references it) and `TimelineSpeed` use `PlayableDirector`. Both `.playable` assets are unreachable. | **Remove**, together with the dead `ShipSelection*` scripts |
| **cinemachine** | 6.6.0 | **No** | 0 code, 0 assets; cameras are custom (`ThirdPersonCamera`, `OrbitalCamera`) | Remove. Cinemachine 3 is worth reconsidering later for the many camera modes. |
| **ai.navigation** | 2.0.15 | **No** | 0 refs (no NavMesh anywhere) | Remove, plus `com.unity.modules.ai` |
| **visualscripting** | 1.9.12 | **No** | 0 graphs, 0 refs | Remove |
| **collab-proxy** | 2.13.6 | **No** | Unity Version Control client; the repo uses git | Remove |
| **ide.vscode** | 1.2.5 | Deprecated | Superseded by ide.visualstudio plus the VS Code Unity extension (`.vscode/extensions.json` already recommends `visualstudiotoolsforunity.vstuc`) | Remove |
| ide.rider / ide.visualstudio | 3.0.38 / 2.0.26 | Tooling | - | Keep the one(s) you use |
| feature.development | 1.0.2 | Tooling | Bundles test-framework, editorcoroutines, profile-analyzer, code coverage | Keep |
| test-framework | 1.8.0 | **No tests exist** | 0 `[Test]` in `Assets/Game` | Keep, and start using it (section 7) |
| 2d.sprite | 1.0.0 | Not referenced | Only needed for the Sprite Editor (slicing) | Optional. Keep if you slice sprite sheets. |
| com.wooshii.foldericons | git URL, **unpinned** | Editor cosmetics | `FolderIcons.asset` plus `Assets/Samples/Folder Icons`. The URL has no `#commit`, so it is not reproducible. Editor.log warns about a missing `package-lock.json.meta` in an immutable folder. | Pin to a commit hash, or remove |
| com.lunawolfstudios.scriptablesheets | 1.8.0 (**embedded** Asset Store package, 5.2 MB, 12 asmdefs) | Editor tool only | Failed on 6.6 (`ScriptableSheetsEditorWindow.cs:191,1692`, `ScriptableSheetsSettings.cs:206,216`, all CS0619); now being hand-patched | **Remove.** Re-import from the Asset Store when the vendor supports 6.6. Do not hand-patch vendor code in place. |
| com.unity.pipeline | 0.8.0-exp.1 | (owner's CLI tool) | - | Ignored per instructions |
| Engine modules | - | - | No code uses Terrain, Cloth, WheelCollider, VideoPlayer, XR, WindZone, Tilemap, Physics2D, Animator, UnityWebRequest or AssetBundle (grep over `Assets/Game`) | Remove `ai, cloth, terrain, terrainphysics, vehicles, wind, tilemap, physics2d, physicscore2d, umbra, video, xr, unityanalytics, adaptiveperformance` (also set URP `m_UseAdaptivePerformance: 0`). Keep `vectorgraphics` while you use SVGs (`Visuals/SVG/aim_white_1.svg`). |

ProjectSettings leftovers from the removed Netcode for Entities: `NetCodeClientAndServerSettings.asset`, `NetCodeClientSettings.asset`, `NetCodeServerSettings.asset`, `MultiplayerManager.asset`, `ProjectSettings/Packages/com.unity.dedicated-server/`. Delete them.

---

## 2. Third-party assets

"Shipped" means the files reachable from the build graph (scenes in the build plus Resources plus settings). Disk sizes come from `du -sh`.

| Asset | Path | Disk | Shipped / total files (bytes) | Referenced by game? | Recommendation |
|---|---|---|---|---|---|
| DOTween (Demigiant) | `Assets/Plugins/Demigiant` | 420K | DLL + `Resources/DOTweenSettings.asset` | 6 files `using DG.Tweening`. Only 3 are live: `ThirdPersonCamera.cs:207-237` (FOV and distance), `ShipEditorController.cs:233` (ghost DOMove), `PlacedBlock.cs:61` (shake). The other 3 (`ShipSelectionVisualizer`, `MapCameraController`, `OrbitalCamera`) have no scene or prefab reference. | **Keep short-term; replace eventually** with a ~50-line tween helper, or PrimeTween if you want a library. It does not work with ECS. Its modules compile into `Assembly-CSharp-firstpass`, which **asmdef assemblies cannot reference**. Enable DOTween's "Create ASMDEF" before splitting assemblies. |
| Quantum Console (QFSW) | `Assets/Plugins/QFSW` | 6.7M | 27/270 (1.6 MB) | Nested prefab in `GAME MANAGERS PREFAB.prefab`. 3 `[Command]`s (`Global.cs:10`, `PauseManager.cs:9`, `ShipEditorManager.cs:521`). Dead `using` in CombatManager and UIManager. **Its font `OfficeCodePro-Medium.ttf` is used by game UI**: `LootPickerStyles.uss`, `LootPickerUI.uxml`, `PauseUIPage.uxml`, `styles.uss`. 38 deprecation warnings on 6.6. | **Keep as a dev tool.** Delete `Source/Demo Scene` (744K). Strip the console from release builds. Copy the font into `Game/UI/Fonts`. |
| SerializedCollections (AYellowpaper) | `Assets/Plugins/SerializedCollections` | 700K | 0/53 | 0 code and 0 asset refs | **Remove** |
| EasyWallCollider | `Assets/Plugins/EasyWallCollider` | 584K | 0/11 | Only the `SetLayerRecursive` extension, in `OpenWorldSolarSystemEntityInstance.cs:191,203` and `EcsSolarSystemEntityInstance.cs:148,156`. It is a 2D polygon-collider tool and pulls in physics2d. | **Remove.** Inline a 6-line `SetLayerRecursive`. |
| ParrelSync | `Assets/ParrelSync` (176K) + `Assets/Plugins/ParrelSync` (settings SO) | 188K | 0 | Editor-only clone tool for multiplayer testing; Netcode is gone; Unity 6 has Multiplayer Play Mode | **Remove both folders** |
| FastScriptReload | `Assets/FastScriptReload` | **23M** | 0/90 | 0 refs. **Broke the 6.6 compile** (`FastScriptReloadManager.cs:459`, 2x CS0619). 80 Unity analyzer warnings (UAC0005/0007/0020: `AppDomain.GetAssemblies`, `Assembly.LoadFrom`, `Assembly.Location`), so its approach conflicts with where Unity 6.6's scripting runtime is going. Its `Samples/` (2 files) compile into `Assembly-CSharp`. | **Remove.** Get fast iteration from asmdefs plus Enter Play Mode Options (section 5). |
| Cartoon FX Remaster FREE (JMO) | `Assets/JMO Assets` | 39M | 20/484 (1.2 MB) | `CFXR Explosion 1` and `CFXR2 WW Explosion` prefabs in GalaxyMapScene; textures/materials reused by `energySphereMat`, `glassMaterial`, `laserMat`, `SolarSystemPortal.prefab`; copies `Visuals/Hit Misc A.prefab` (unused) and `Core/RotateObject.cs` (`namespace CartoonFX`) | **Keep for now** (URP-compatible ubershader). Delete `Demo Assets/` (including Kino Bloom, a BiRP `OnRenderImage` effect) and `CFXRF Demo.unity`. Long-term, ParticleSystem is GameObject-bound; mass voxel explosions belong in VFX Graph. |
| Microlight MicroBar | `Assets/Microlight` | 1.4M | 6/55 | `NEW COMBAT SHIP(.PLAYER).prefab` health bars (uGUI world canvases); `CombatShip.cs:1,28`. **No asmdef**, so its 24 scripts (5.3k LOC) compile into Assembly-CSharp(-Editor). | **Replace** with a UI Toolkit or shader-quad bar (or an entity-rendered quad for ECS ships), then remove |
| Planets of the Solar System 3D | `Assets/Planets of the Solar System 3D` | 67M | 18/78 (**30.9 MB**) | Planet textures for 9 Resources planet SOs; `Earth_Lod.fbx`, `Ring.fbx`, `Asteroids Belt`/`Moon` prefabs, `Planet.shadergraph`, `PlanetMat.mat`. Ships its **own URP asset and renderer** (`Settings/POTSS_UniversalRP*.asset`). | **Keep as placeholder**: move the used files into `Game/Art`, delete the rest including its URP assets. Long-term, use a procedural planet shader. `Earth_4k.png` (4096x2048) is used. |
| Space Skyboxes | `Assets/Space Skyboxes` | 203M | 7/110 (13 MB) | Only `SBS Space 1/Medium` (ShipEditorScene skybox). Imported **uncompressed** (`textureCompression: 0`): 6 x 2048 squared x RGBA32 + mips, roughly 134 MB VRAM (estimate). 5 `SBS Space N.asset` have a missing script (6f6156...). | Keep the one skybox (compress it, no mips), delete the rest; or replace with a procedural skybox |
| Starfield Skybox | `Assets/Starfield Skybox` | **302M** | 7/7 (**316.7 MB**) | GalaxyMapScene skybox. 6 x 4096 squared PNG (about 50 MB each; noise does not compress), imported 4096 BC7 with mips, roughly 134 MB VRAM (estimate). **299 MB in git history.** | **Replace** with a procedural star shader (better for a "galaxy to system" game anyway), or one 2K-4K cubemap |
| **Starsector Visuals** | `Assets/Starsector Visuals` | 51M | 22/651 (1 MB) | 13 map icons on Solar System Entity SOs; illustrations `hegemony_bar.jpg`, `kazeron.jpg`, `cryosleeper_interior.jpg`; `planets/star_yellow-orange.jpg` (SunMaterial), `rings_special0.png` (RingMaterial); **default cursor** `cursor_blue.png` in PlayerSettings; `icons/mission_marker.png` in the managers prefab | **Legal risk: replace before any public build, demo, trailer or stream.** These names are Starsector IP and there is no license. Keep them out of a fresh repo. |
| YughuesFreeMetalMaterials | `Assets/YughuesFreeMetalMaterials` | **577M** | 2/228 (8.4 MB) | `T_YFMeM_13_s.tga` (ShieldMat), `T_YFMeM_07_s.tga` (a CFXR material) | **Remove.** Copy or replace the 2 textures. This is the single biggest folder (567 MB TGA in git). |
| TextMesh Pro | `Assets/TextMesh Pro` | 4.1M | 14/37 | Essential resources for the QFSW console and TMP sprite assets. Game UI is UI Toolkit. | Keep while QFSW stays |
| scifi_ui white | `Assets/scifi_ui white` | 9.3M | 5/603 | `GalaxyViewUI.uxml`, `styles.uss`, `InventoryUI.uxml`, `PauseUIPage.uxml` | Keep 5 files (move to `Game/UI/Sprites`), delete the rest |
| HOLO UI | `Assets/Game/Visuals/Sprites/HOLO UI` | 2.1M | 6/265 | `Inventory.uss`, `FleetView.uxml`, `ModuleSlotVisual.prefab` | Keep the used files, delete the rest |
| Food pixel sprites | `Assets/Game/Visuals/Sprites/Food` | 512K | 0/64 | Only `WorldUI.uxml`, which is itself unreferenced. These, plus HOLO UI, are most of the 385 `.meta` lines in `git status` (6.6 reimport noise). | **Remove** |
| Folder Icons sample | `Assets/Samples` + `Assets/FolderIcons.asset` | 156K | editor | Used by the wooshii package | Optional |
| Kontrapunkt font | `Assets/Fonts` | 32K | 1/1 | `InteractView.uss`, `NarrativeView.uss`, `styles.uss` | Keep, move to `Game/UI/Fonts` |
| Input Sprites | `Assets/Input Sprites` | 372K | 1/3 | `all_input.png` (ShipEditorScene, `InteractView.uss`); TMP sprite assets unused | Keep the png |
| UI Toolkit settings | `Assets/UI Toolkit` | 20K | used | `PanelSettings.asset` (StartScene, managers prefab, EditorPlayer) | Keep, move under `Game/UI` |

---

## 3. Repo and Assets clutter

### Repo root

All of these are **tracked in git** (`git ls-files`):

| File | Size | What it is | Action |
|---|---|---|---|
| `build_log4.txt` | 30 MB (313,638 lines) | MSBuild diagnostic-verbosity log from `dotnet build` | git rm |
| `errors.log` | 1.2K now; **30.7 MB version in history** | dotnet build output (UTF-16) | git rm |
| `build.log`, `build2.txt`, `build_errors.log`, `build_errors.txt`, `build_log.txt` (340K), `build_log2.txt` (344K), `build_log3.txt` | - | dotnet build outputs | git rm |
| `old_ui.cs`, `old_ui.txt`, `prev_gv.cs`, `prev_gv.utf8.cs` | 21K/10K/2K/20K | Hand snapshots of old UIManager/GalaxyView (UTF-16) | Delete; history has them |
| `status.txt` | 3.6K | Scratch | Delete |
| `Spaceship Game.slnx` | tracked, stale (Windows-era name) | Generated solution; the untracked `spaceship-game.slnx` replaces it | git rm, add `*.slnx` to .gitignore |
| `UIElementsSchema/` (23 .xsd) | - | Auto-generated by UI Builder | git rm, add to .gitignore |
| `AudioSystem_Walkthrough.md`, `OpenWorldCombatRefactorGuide.md`, `Assets/Game/Core/Dialog/*_Guide.md` | - | Design notes | Move to `Docs/` |
| `.vscode/` | 3 files; settings.json modified | Editor prefs | Optional; personal |
| `*.csproj`, `*.sln` | 40+ files | Correctly ignored | OK |

History also contains `bin_cli/Assembly-CSharp.dll`, committed before `bin_cli/` was ignored.

Suggested `.gitignore` additions: `*.slnx`, `/UIElementsSchema/`, `*.log`, `/build*.txt`, `/Assets/SceneDependencyCache*`.

### Assets root

| Item | Status | Action |
|---|---|---|
| `NebulaDustVFX.vfx` (152K), `SpaceDebrisVFX.vfx` (220K) | Not referenced anywhere | Move to `Game/Art/VFX` if wanted (good ambient candidates), else delete |
| `StarMat.mat` | Unreferenced; uses built-in `Particles/Standard Unlit` (renders magenta in URP) | Delete |
| `cfxr magic star hdr ab.mat` | Unreferenced | Delete |
| `SceneDependencyCache/` | Six 48-byte `.sceneWithBuildSettings` files from the initial commit; generated cache | Delete |
| `AddressableAssetsData/` | Localization groups only | Delete together with Localization |
| `Localization/` | See section 1 | Delete or rebuild |
| `InputSystem_Actions.inputactions`, `InputSystem.inputsettings.asset` | Used (preloaded) | Move to `Game/Settings/Input` |
| `Settings/` | `High_PipelineAsset.asset`, the shared renderer, a Windows build profile | Consolidate all render settings here (section 5) |
| `Resources/` | See below | Restructure |

### Resources usage

`Assets/Resources` has 170 files (2.6 MB): 158 `.asset`, 10 png, 1 uss, 1 uxml. It is effectively the game's ScriptableObject database (Items, Encounters, Quests, Galaxies, Factions, Abilities, Loot Tables, AudioLibrary, GameSettings, DOTweenSettings). There are 27 `Resources.Load*` call sites in 15 files.

Problems:

- **Loads of the entire Resources tree.** `Resources.LoadAll<T>(string.Empty)` at `SolarSystemEntityCatalog.cs:25`, `SaveManager.cs:899` and `SaveManager.cs:919` also loads TMP's Resources.
- **Paths that do not exist (always null):**
  - `"Portraits"` (`StartView.cs:73`, `Player.cs:234`)
  - `"NarrativeImages/..."` (`NarrativeView.cs:184`)
  - `"Materials/OrbitLineMaterial"` (`OpenWorldSolarSystemGenerator.cs:346`, which falls back to `Shader.Find("Sprites/Default")` at line 353)
- **A baker reads Resources:** `VoxelPrefabAuthoring.cs:61` calls `Resources.LoadAll<BlockItem>("Items")` with no `DependsOn`. Editing a BlockItem does not re-bake the SubScene, so baked voxel data goes stale.
- **Stringly-typed paths** are scattered across `DialogManager`, `EncounterManager`, `QuestManager`, `NarrativeActionRuntime`, `SolarSystemEntity` and `ShipEditorManager`.

Recommendation: create one `GameDatabase` ScriptableObject that holds direct references (lists per type), referenced by the boot scene. ECS-side definitions should be baked from a SubScene authoring component that references that database and calls `DependsOn`. Keep the `Assets/Game/Data/*.json` plus importer pipeline: it is a good, portable source of truth.

### Git repository

- `git count-objects -vH`: size-pack **1.86 GiB**, 27,913 objects. 134 commits from 2025-10-25 to 2026-02-23. **No `.gitattributes`, no LFS.**
- HEAD tree is 1,416 MB: png 621 MB (2,008 files), tga 567 MB (163), wav 56 MB, dll 24.6 MB.
- Packed history by folder:

  | Folder | Size | Notes |
  |---|---|---|
  | Cartoon_Texture_Pack | 672 MB | already deleted |
  | Starfield Skybox | 299 MB | |
  | Yughues | 230 MB | |
  | Space Skyboxes | 202 MB | |
  | Game | 133 MB | |
  | Resources | 81 MB | |
  | AllSkyFree | 64 MB | deleted |
  | Planets | 63 MB | |
  | Starsector | 46 MB | |
  | PolygonPirates | 29 MB | deleted |
  | StylizedWater2 | 13 MB | deleted |

- Largest blobs: the 6 Starfield faces (46-55 MB each), `errors.log` (30.7 MB), `build_log4.txt` (28.7 MB), `Game/Meshes/maptest.obj` (18 MB, deleted).
- **Recommendation:** start a fresh repository for the rebuild and archive this one. Add `.gitattributes` with LFS from day one for `*.png *.tga *.psd *.jpg *.exr *.wav *.mp3 *.ogg *.fbx *.obj *.dll *.ttf *.otf`. If you keep this repo, run `git filter-repo` to strip deleted vendor packs and logs, then `git lfs migrate import`.

---

## 4. Build-log summary

The root logs are **not Unity logs**. They are `dotnet build` (MSBuild, .NET SDK 10.0.101) runs against the Unity-generated `.csproj` files on the old Windows checkout `c:\Users\Frank\Spaceship Game`. All were last committed between 2026-02-21 and 2026-02-23.

| File | Content | Status today |
|---|---|---|
| `errors.log` | `EcsSolarSystemGenerator.cs(231,35)` CS0104 ambiguous `SphereCollider`; `(270,17)` ambiguous `Random` | **Stale.** The file now uses `Unity.Physics.SphereCollider.Create` (line 283). |
| `build.log`, `build2.txt` | `OpenWorldGalaxyManager.cs(7,7)` namespace `GalaxyMap` not found; `(23,32)` `EcsSolarSystemGenerator` not found | **Stale.** `namespace GalaxyMap.ECS` and `.Asteroids` exist now. |
| `build_errors.log` | `UIManager.shipEditorLoadUI` missing; `SpaceStationManager.cs`; `SolarSystemPlayerController.cs` | **Stale.** The field exists (`UIManager.cs:20`); both files are deleted. |
| `build_log.txt` | `NarrativeView.cs(500,43)` CS1503 | Stale (code changed) |
| `build_log2.txt` | `EncounterManager.cs(265-288)` `starSeed` undefined | Stale (`starSeed` declared at :265) |
| `build_log3.txt` | MSB4006 circular dependency in `LunaWolfStudiosEditor.ScriptableSheets.Comparables.csproj` | A dotnet-CLI artifact of building Unity csproj files; irrelevant inside Unity |
| `build_log4.txt` | 30 MB detailed log; one error, `DialogManager.cs(341,13)` `SetEncounterPresentationMode` | Stale (method lives in NarrativeView) |
| `build_errors.txt` | Warnings only | **Still present**: `GalaxyView.Map.cs:197` obsolete `unityBackgroundScaleMode`; `SaveManager.cs:126` `ItemId` never assigned; unused `isGenerating` (`OpenWorldSolarSystemGenerator.cs:16`, `EcsSolarSystemGenerator.cs:25`); unused `CombatManager.defaultSpawnSpacing`/`maxShipsPerRow` (:19, :21). |

### Current Unity 6.6 compile state (`Logs/Editor.log`)

At audit start there were 80 `error CS` lines, all CS0619 obsolete-as-error for the `GetInstanceID` to `EntityId` migration:

- embedded `com.unity.localization` 1.5.9: `Locale.cs:458`, `JsonSerializerTrackedObject.cs:104,183,231,239`
- `com.lunawolfstudios.scriptablesheets` (4 sites)
- `FastScriptReloadManager.cs:459`

`Assembly-CSharp.dll` was absent, so game code had not compiled at all.

After the external changes (Localization resolved to registry 1.5.13 at Editor.log:52513), the only remaining error is in **game code**: `Assets/Game/Core/Game Logic/GameSettings.cs(4,22)` CS0234, `using Unity.Entities.UniversalDelegates;`, an unused using. Delete it.

Also note:

- `GameDatabase.cs:164` was changed externally to `gameObject.GetEntityId().GetHashCode()` as a dictionary key. Prefer `Dictionary<EntityId, GameObject>`, because hash codes are not guaranteed unique.
- The embedded Localization's Editor assembly also failed (UxmlFactory/UxmlTraits removed: `ProjectLocalePopupField.cs:24,29`, `XliffVersionPopup.cs:20`).
- Expect more Entities 6.x and 6.6 API fallout once Assembly-CSharp compiles past this line. For example, `EncounterManager.cs:391` uses the deprecated `FindObjectsSortMode`, and there are 7x `FindFirstObjectByType` (deprecated in 6.6; use `FindAnyObjectByType`).

---

## 5. ProjectSettings findings

| Area | Current (file:line) | Risk | Recommended |
|---|---|---|---|
| **Enter Play Mode** | `EditorSettings.asset`: `m_EnterPlayModeOptionsEnabled: 0` (full domain and scene reload every Play; "Domain Reload Profiling: 1505ms" in Editor.log) | Slow iteration, but it currently masks static-state bugs. About 40 mutable statics, for example `GameInitializer.isGameInitialized` (GameInitializer.cs:15), 13 statics in `GameDatabase.cs:12-54`, `PauseManager.isGamePaused`, `Global.DEV_MODE`, `SimplePool.pools`, `SceneLoader.asyncLoad`, 9 `Instance` singletons, static events `GameEventBus.OnEventPublished` and `OpenWorldSolarSystemGenerator.OnSystemGenerated`. Turning domain reload off today would break the second Play: `isGameInitialized` stays true, so the managers prefab is never instantiated. | In the rebuild, add `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` static resets (or avoid statics), then enable "Do not reload Domain". |
| Scripting backend | Standalone unset, so Mono | - | Mono for dev; IL2CPP for release builds |
| API compatibility | `apiCompatibilityLevel: 6` (.NET Standard 2.1) | - | OK |
| Color space | `m_ActiveColorSpace: 1` (Linear) | - | OK |
| Graphics APIs | Windows `0200000012000000` = D3D11 then D3D12, `m_Automatic: 0`; Mac automatic (Metal) | DX11-first is legacy for a compute/BRG-heavy voxel renderer | D3D12 first (or Automatic); Vulkan on Linux |
| Graphics jobs | Win/Linux 1, Mac 0 | - | OK |
| Incremental GC | `gcIncremental: 1` | - | OK |
| Unsafe code | `allowUnsafeCode: 1` project-wide | Applies to the whole assembly | Scope it to the Voxel asmdef |
| Defines | `Standalone: USE_NEW_INPUT;UNITY_POST_PROCESSING_STACK_V2` (ProjectSettings.asset:691) plus the PPv2 define on 15 other platforms | `USE_NEW_INPUT` is unused in code; the PPv2 define is stale | Remove both |
| **Active Input Handling** | `activeInputHandler: 2` (Both) | 9 files on legacy Input, including an ECS system reading `Input.GetAxis` (`ShipInputSystem.cs:25`) | Migrate, then set it to 1 (Input System only) |
| Default cursor | `cursor_blue.png` from Starsector Visuals | Legal | Replace |
| Preloaded assets | InputSystem settings, actions, Localization Settings | Localization plus Addressables init at boot | Drop Localization |
| **Quality tiers** | 6 tiers (QualitySettings.asset). Very Low, Low, Medium and Very High reference URP assets **deleted in `72366ce5`** (GUIDs f191..., 5755..., 8e03..., 4564...), so they fall back to the GraphicsSettings default (Ultra). High points to `Assets/Settings/High_PipelineAsset.asset`. Ultra points to `Assets/Game/Visuals/URP Asset.asset`. `m_CurrentQuality: 5`. | Low renders identical to Ultra. **High has `m_RequireOpaqueTexture: 0` / `m_RequireDepthTexture: 0`**, which breaks `HeatDistortionShader.shadergraph` (uses `SceneColorNode`) on `Blackhole.prefab` and the `DepthFade.shadersubgraph`. Shadow distance is 40 on High and 500 on Ultra. All tiers are user-selectable (`SettingsView.cs:188-193`). | 3 tiers (Low/Med/High), each with its own URP asset sharing one renderer, identical feature requirements (depth/opaque) and different only in cost knobs |
| URP assets scattered | `Assets/Game/Visuals/URP Asset.asset`, `Assets/Game/Visuals/URP.asset` (Global Settings), `Assets/Settings/High_PipelineAsset.asset`, `Assets/Settings/BASE ..._Renderer.asset`, `Assets/Planets.../Settings/POTSS_UniversalRP*.asset` | Confusion | One `Game/Settings/Rendering` folder |
| **URP default asset** | Forward+ (`m_RenderingMode: 2`), HDR, MSAA 2x, depth and opaque textures on, SRP Batcher on, dynamic batching off, **GPU Resident Drawer off** (`m_GPUResidentDrawerMode: 0`), **main light shadowmap 512, distance 500, 2 cascades**, soft shadows on, `m_UseAdaptivePerformance: 1`, LOD cross-fade on | 512 px over about 250 m per cascade is roughly 1-2 m per texel against 0.5 m voxels, so self-shadowing on ships is blotchy or missing. Opaque copy costs bandwidth every frame for one shader. Adaptive Performance is a mobile feature. | Shadowmap 2048-4096, 4 cascades, distance about 150-300 m tuned to the gameplay camera; keep the opaque texture only if distortion stays (or make it per-camera); Adaptive Performance off; try GRD for the GameObject-based ship editor (one GameObject per `PlacedBlock`) |
| Renderer features | `BASE ..._Renderer.asset`: 4 RenderObjects (`selectoutline` layer 18, `OutlineEnemyShip` 16, `OutlinePlayerShip` 15+17, `ShipStencilWrite` 15-18) plus **SSAO full-res** (`Downsample: 0`, Intensity 2) | SSAO cost at full resolution | SSAO Downsample on, or defer it. Layer masks are correct for TagManager. |
| Camera AA | GalaxyMapScene camera `m_Antialiasing: 1` (FXAA) on top of pipeline MSAA 2x | Double AA (blur plus cost) | Pick one: MSAA for voxel edges, or SMAA/TAA |
| Large-world precision | `GraphicsSettings.m_CameraRelativeLightCulling: 0`, `m_CameraRelativeShadowCulling: 0`; camera far 5000 | Space scale means float jitter far from origin | Enable camera-relative culling. Plan a floating origin for "massive stations". |
| GraphicsSettings | `m_BrgStripping: 2` (Keep All) | Correct for Entities Graphics | OK |
| Lighting | `Visuals/Lighting Settings.lighting`: `m_EnableRealtimeLightmaps: 1` (Enlighten Realtime GI), baked 0; GalaxyMap and ShipEditor scenes have `m_Fog: 1` (`m_FogMode: 2`) and flat ambient | Realtime GI is useless for a fully dynamic space game; fog in space washes out silhouettes | Disable Realtime GI; disable fog (use a depth-fade or volumetric look instead) |
| **PhysX** | `DynamicsManager`: gravity 0, solver iterations 6, fixed step `2822399/141120000` = 0.02 s (50 Hz), Maximum Allowed Timestep 0.333 | Runs alongside Unity Physics (`FixedStepSimulationSystemGroup` defaults to 1/60): two physics worlds at different rates. PhysX is still used in 7 files (editor raycasts, `GhostBlockCollision`, `OpenWorldPlayerController`). A 0.333 max timestep allows a spiral-of-death. | Gameplay on Unity Physics only. Set both fixed rates explicitly. Max allowed timestep about 0.1. |
| **Unity Physics step** | `PhysicsStepAuthoring` is on the `EntitiesSubscene` host GameObject **in the main GalaxyMapScene** (GalaxyMapScene.unity:160-192), so it is never baked (bakers run only in SubScenes). `PhysicsSetup.cs:15-21` creates `new PhysicsStep{...}` with only 4 fields set, leaving `CollisionTolerance = 0` (default 0.01; used directly by `BuildPhysicsWorld.cs:194`) and the stabilization and direct-solver settings zeroed. | Subtle contact and tunneling behaviour; a dead authoring component | `var s = PhysicsStep.Default; s.Gravity = float3.zero;`. Delete the scene authoring, or move it into the SubScene. |
| TagManager | Layers 15-19 drive the outline features; legacy layers Water, Terrain, NPC, Tile, Unit, StarNode; tags such as `FxTemporaire` | Noise | Prune in the rebuild |
| **Build scene list** | `EditorBuildSettings.asset`: `CombatScene.unity` (**enabled**) and `SpaceStationScene.unity` (disabled) were deleted in `442a4e7b` | Build warnings or failures | Remove them. Also delete the dead `SceneLoader.BattleSceneName`, `ManagersSceneName = "PersistentScene"` and `SpaceStationScene` constants (SceneLoader.cs:9-13). |
| Build profile | `Assets/Settings/Build Profiles/New Windows Profile.asset` (global scene list) while development is now on macOS | - | Add a macOS dev profile |
| Burst | `BurstAotSettings_StandaloneWindows.json`: optimizations on, safety checks off | - | OK |
| VFX / ShaderGraph | VFX fixed step 1/60; SG variant limit 128 | - | OK |
| DOTween | `Resources/DOTweenSettings.asset`: safe mode on, all modules | - | OK while kept |
| Audio (adjacent) | Music `.wav` (11-13 MB each) imported `loadType: 0` (Decompress On Load), Vorbis q=1 | RAM | Use Streaming for music |

---

## 6. Scenes, ECS setup, missing scripts, duplicated managers

| Scene | In build | Roots | Notes |
|---|---|---|---|
| `StartScene.unity` | #0 | `AudioListener`, `StartMenu` (UIDocument + StartView), `GameInitializer` prefab | No Camera (UI Toolkit overlay only) |
| `GalaxyMapScene.unity` | #1 | `EntitiesSubscene` (SubScene, `AutoLoadScene: 1`, plus the unbaked `PhysicsStepAuthoring`), `LIGHTING---`, `Main Camera` (ThirdPersonCamera, URP data, FXAA), `ParticleSpawner`, `GameInitializer` prefab, `Blackhole` prefab | 1 local Volume (priority 10, blend 30) and 1 global Volume using 2 different profiles that both override Bloom, Tonemapping and ColorAdjustments. Starfield skybox. |
| `ShipEditorScene.unity` | #3 | `ShipEditorManager`, `ThumbnailCamera`, `LEVEL---`, `GameInitializer` prefab, `EditorPlayer` prefab | **No SubScene.** The editor builds ships from GameObjects (one `PlacedBlock` per voxel), while gameplay uses ECS voxels, so the project keeps two separate ship representations. |
| `EntitiesSubscene.unity` | not in list (correct) | `ThrusterPrefabAuthoring`, `GlobalShipScale` (`ShipScale: 0.5`, `ProjectileScale: 4`), prefab instances `ShipVoxelEntityPrefab`, `ShieldEntityPrefab`, `ProjectileDatabaseAuthoring` | A proper SubScene. All baked materials are DOTS-instancing compatible: MetallicShipShader.shadergraph, ShieldShader.shader (`DOTS_INSTANCING_ON`), URP Lit/Unlit. |
| `CombatScene`, `SpaceStationScene` | listed | **missing** | See section 5 |

### Managers

`GameInitializer.prefab` (a `PersistentSingleton`) is placed in all 3 build scenes. It instantiates `GAME MANAGERS PREFAB` once, guarded by the static `isGameInitialized`. That prefab is one root holding:

- 9 managers on the same GameObject: UIManager, CombatManager, InputManager, PauseManager, EncounterManager, AudioManager, SettingsManager, DialogManager, QuestManager
- 15 child `UIDocument` views
- an EventSystem with InputSystemUIInputModule
- `OpenWorldGalaxyManager`
- the nested Quantum Console prefab

6 of these managers each call `DontDestroyOnLoad(gameObject)` on the same root (`QuestManager.cs:82`, `InputManager.cs:31`, `EncounterManager.cs:60`, `SettingsManager.cs:38`, `DialogManager.cs:45`, `AudioManager.cs:33`), which is redundant. At runtime there are no duplicates, but the "bootstrap prefab in every scene plus static flag" pattern is fragile. Use a dedicated Boot scene (index 0) and load gameplay scenes additively. `OpenWorldGalaxyManager` and `CombatManager` also live in the ship editor and menu.

### Missing script references

Scanned all YAML in `Assets` with built-in GUIDs excluded:

| Asset | Missing script | Shipped? |
|---|---|---|
| `Resources/Encounters/Combat/encounter_extraction.asset` | `ExtractionCombatEncounter.cs` (deleted) | **Yes.** `Encounters/Narrative/event_stable_point.asset:44` outcome uses it, so combat loads null. |
| `Resources/Encounters/Combat/encounter_survivor_1.asset` | `SurvivorCombatEncounter.cs` (deleted) | **Yes** |
| `Prefabs/Galaxy/Galaxy.prefab` | `GalaxyManager.cs` (deleted) | No (unreferenced) |
| `Prefabs/MultiplayerShip.prefab` | `Network/NetworkShipAuthoring.cs` plus one Netcode script | No |
| `Space Skyboxes/SBS Space 1-5/*.asset` | third-party (6f6156...) | No |

### Built-in (non-URP) shaders

| Material | Shader | Effect | Shipped? |
|---|---|---|---|
| `Visuals/Materials/StarlightMat.mat` | `Particles/Standard Unlit` (built-in fileID 211) | **Magenta** on `StarSolarSystem.prefab/Plane` MeshRenderer | **Yes**, via a Resources star entity |
| `Assets/StarMat.mat` | same | magenta | No |
| `Visuals/Materials/BaseHeightMat.mat` | `HeightColorGradientReflective.shader` (`#pragma surface`, BiRP only) | magenta | No |
| `WireframeMat.mat`, `DarkWireframeMat.mat` | `Core/ShipEditor/Wireframe.shader` (CGPROGRAM + UnityCG.cginc) | Renders, but not SRP Batcher compatible | Yes (ship editor) |
| Skybox materials | `Skybox/6 Sided`, `Skybox/Cubemap` | Fine in URP | - |

Also unused: `HeightColorBlendRelative.shader` (surface shader), `MetallicShipShader.shader` (an HLSL duplicate of the .shadergraph), `HeightColorShader.shadergraph`, `Water.shadergraph`, `UI/UIShaders/TestShader.shadergraph`.

### Shaders and VFX in `Assets/Game`

- 6 `.shader`: Wireframe, MetallicShipShader, WeaponArcShader, ShieldShader, HeightColorGradientReflective, HeightColorBlendRelative
- 9 `.shadergraph`: Fresnel, GhostBlock, HeatDistortion, HeightColor, MetallicShip, Sun, VoxelOutline, Water, UI TestShader
- 2 subgraphs: Cutout, DepthFade
- 3 `.vfx`: GalaxyVFX, GalaxyVFXSlow, LaserBeamVFX
- Shipped content has 12 ParticleSystems and 1 VisualEffect.

### Unreachable game content

- 74 non-code files (20.1 MB) under `Assets/Game` are not reachable from build scenes, Resources or settings. Examples:
  - `SFX/Music/ComfyUI_00018_.mp3` (4.3 MB), `upbeat.mp3` (4.2 MB), 3 WAV SFX of about 2 MB each
  - `Visuals/sun_noise.png` (1.7 MB), `Materials/*.psd`/`.jpg`
  - `Visuals/Fonts/TT Ramillas Bold SDF.asset`, `Pixel_Anxel.ttf`
  - Prefabs `CombatExitPortal`, `Galaxy`, `MultiplayerShip`, `ShipBlockVisualOnly`, 2 projectile prefabs
  - `UI/TradeUI.uxml`, `TradePageUI.uxml` (0 bytes), `WorldUI.uxml`, `event_dialogue.uss`
  - Cutscene `.playable`s, `SWS_PostProcessingProfile_URP.asset`
  - `Data/*.json`: source data for the editor importers, so keep it
- 25 MonoBehaviour/SO classes have no asset reference. Some are added via `AddComponent` or are abstract, so code auditors should confirm. Clearly dead: `ShipSelectionController`/`ShipSelectionVisualizer`/`TimelineSpeed`, `AsteroidMiner`, `BoostCameraController`, `MapCameraController`, `CombatAudioListener`, `BrightnessController`.

---

## 7. Proposed asmdef layout and fresh-project structure

### Why

All game code is one assembly: 247 files / 39.4k LOC, plus 14 MicroBar scripts and 2 FastScriptReload samples in `Assembly-CSharp`. Every edit re-runs csc, the Entities source generators and IL post-processing over all of it, then a full domain reload. There are no tests.

### Blocker: dependency cycles

From a type-reference scan of `Assets/Game`:

| From | To | Example |
|---|---|---|
| Core/VoxelShip | Core/Combat (25 refs) | `ShipBuildRequest.cs` uses `CombatShip`, `Allegiance`; `ShipConstructionJobSystem.cs` uses `AIPersonality` |
| Core/Combat | Core/VoxelShip (76 refs) | `AIShipNavigationSystem` uses `ShipHealth`, `ShipInputComponent` |
| Core/VoxelShip | Core/Managers | `Global.DEV_MODE` in `GlobalShipScaleAuthoring`, `ShipStatCalculator` |
| Core/Managers | UI (17) | - |
| UI | Core/Managers (19) | - |
| Core/Game Logic | UI | `GameDatabase.cs` uses `InventoryNotificationRelay` |
| Core/Inventory | UI | `Inventory.cs` uses `InventorySlot` |

Only 29 of 247 files declare a namespace. Namespaces in use are inconsistent: `Game.Core.VoxelShip`, `GalaxyMap.ECS`, `Systems.Inventory`, `Assets.Game.Core.Galaxy_Map`, and `CartoonFX` in `Core/RotateObject.cs`.

### Target layout

Dependencies point downward only. Set every asmdef to `autoReferenced: false`.

```
Game.Core            (pure: ids, math helpers, event bus, data DTOs, interfaces)  -> Unity.Mathematics, Unity.Collections
Game.Data            (ScriptableObject definitions: Block/Item/Ship/Faction defs, GameDatabase registry) -> Core
Game.Voxel           (ECS: voxel grid/chunk components, meshing jobs, damage/destruction, collider rebuild;
                      allowUnsafeCode=true)  -> Core, Data, Entities, Entities.Graphics, Physics, Burst, Collections
Game.Voxel.Authoring (MonoBehaviours + Bakers; DependsOn the registry) -> Voxel, Data
Game.Simulation      (ship flight, weapons, AI, combat systems) -> Voxel, Core, Data
Game.Galaxy          (galaxy/system generation, travel, asteroid fields) -> Simulation, Core, Data
Game.Narrative       (dialog, quests, encounters, mystery state) -> Core, Data   (talks to Galaxy/Simulation via Core events)
Game.Persistence     (save/load; modules register ISaveable via Core interfaces) -> Core, Data
Game.Presentation    (cameras, VFX Graph bridges, audio, ECS<->GameObject sync) -> Simulation, Galaxy
Game.UI              (UI Toolkit views; nothing references UI) -> Core, Data, read-only facades of the above
Game.App             (Boot scene, scene flow, composition root, dev console commands) -> everything
Game.Editor          (Editor-only: JSON importers, ship tools) -> everything
Game.Tests.EditMode  (NUnit: voxel meshing, damage propagation, connectivity/split, save round-trip) -> Core, Data, Voxel, Persistence
Game.Tests.PlayMode  (bootstrap smoke test, scene loads, ECS world spins up) -> App
```

Fixes needed first:

- Move `Allegiance` and `AIPersonality` into Core/Data.
- Have Combat consume voxel damage through components owned by Voxel.
- Replace `Global.DEV_MODE` with a Core config.
- Invert Inventory to UI (UI observes inventory events).
- Have managers raise events instead of calling views.

Third-party gotchas:

- DOTween: enable "Create ASMDEF" (its modules currently live in `Assembly-CSharp-firstpass`).
- MicroBar has no asmdef: remove it or add one.
- QFSW already has asmdefs.

### Fresh project (recommended, given "existing code is only a starting idea")

1. **New Unity 6.6 URP 3D project and a new git repo with LFS.** Packages:
   - Entities, Entities Graphics, Unity Physics, Burst, Collections, Mathematics
   - Input System, VFX Graph, Shader Graph, Test Framework, IDE package
   - Optional: QFSW Quantum Console
   - Not: PPv2, Visual Scripting, AI Nav, Cinemachine (until needed), Timeline (until cutscenes), Localization (until translation), Collab, ide.vscode, FastScriptReload, ScriptableSheets, ParrelSync, SerializedCollections, EasyWallCollider.
2. **Folder layout:**
   ```
   Assets/
     _Game/
       Art/        Materials, Meshes, Shaders (URP/DOTS only), Textures, VFX
       Audio/      Music (streaming), SFX
       Code/       one folder per asmdef above
       Data/       Definitions (SOs) + Database.asset; Source/ (the JSON files + importers)
       Prefabs/
       Scenes/     Boot.unity, MainMenu.unity, Galaxy.unity, ShipEditor.unity, SubScenes/
       Settings/   Rendering (URP assets, renderer, volume profiles, lighting), Input, UI (PanelSettings, themes)
       UI/         UXML, USS, Fonts, Sprites (only used ones)
     ThirdParty/   vendor assets you keep (CFXR trimmed, QFSW), never edited in place
   Docs/           design notes (currently at repo root and in Core/Dialog)
   ```
3. **Port assets, not code, first.** Bring over:
   - the DOTS-ready shaders (MetallicShipShader.shadergraph, ShieldShader, WeaponArcShader, GhostBlock, VoxelOutline, HeatDistortion, Sun, Fresnel)
   - the VFX graphs (GalaxyVFX, NebulaDust, SpaceDebris)
   - meshes from `Game/Meshes` (check `ariadne ship.obj`, 13 MB)
   - licensed music and SFX, UI UXML/USS as a reference
   - `Game/Data/*.json` plus the two importers
   - only the 5-6 used sprites per UI pack
   - an original replacement for every Starsector file
4. **Port code module by module**, starting with Game.Voxel (the heart of the vision). Write EditMode tests for meshing, damage and connectivity before porting Simulation, then Galaxy, then Narrative and UI. Treat the old systems as reference, not as code to copy.
5. **From day one:** a Boot scene, no `Resources/`, statics reset for no-domain-reload, Input System only, and one gameplay physics engine (Unity Physics).

---

## 8. Quick wins (ordered, each under an hour)

1. **Finish unblocking compilation on 6.6.**
   - Delete the unused `using Unity.Entities.UniversalDelegates;` (`GameSettings.cs:4`).
   - Keep the registry Localization (or remove it entirely).
   - Delete `Assets/FastScriptReload` and `Packages/com.lunawolfstudios.scriptablesheets` rather than patching them.
   - Commit the 6.6 upgrade only once it compiles.
2. **Remove dead packages:** postprocessing, visualscripting, ai.navigation, cinemachine, collab-proxy, ide.vscode. Remove timeline together with `ShipSelection*` and `TimelineSpeed`. Remove the `UNITY_POST_PROCESSING_STACK_V2` and `USE_NEW_INPUT` defines.
3. **EditorBuildSettings:** remove `CombatScene` and `SpaceStationScene`.
4. **QualitySettings:** repoint or delete the 4 tiers with missing URP assets, and make High match Ultra's depth/opaque requirements.
5. **Fix the magenta material:** switch `StarlightMat.mat` to `Universal Render Pipeline/Particles/Unlit`. Delete `StarMat.mat`, `BaseHeightMat.mat` and the 2 surface shaders.
6. **Fix PhysicsSetup** to start from `PhysicsStep.Default` with zero gravity. Remove the unbaked `PhysicsStepAuthoring` from GalaxyMapScene.
7. **Delete unused vendor packs:** YughuesFreeMetalMaterials (keep 2 textures, saves 577 MB), Space Skyboxes (keep 1, saves about 190 MB), SerializedCollections, EasyWallCollider (inline `SetLayerRecursive`), both ParrelSync folders, the Food sprites, demo scenes (CFXR, QFSW, EasyWallCollider, SerializedCollections, FastScriptReload, Yughues Preview), and the Netcode ProjectSettings leftovers.
8. **Fix broken Resources content:**
   - Delete or recreate `encounter_extraction`/`encounter_survivor_1`, and fix `event_stable_point` to stop referencing them.
   - Remove or fix the 3 nonexistent Resources paths.
   - Replace the three `Resources.LoadAll<T>("")` calls with typed subfolders.
9. **Repo hygiene:**
   - `git rm` the 14 root log, scratch and slnx files and `UIElementsSchema/`; update `.gitignore`.
   - Delete `Assets/SceneDependencyCache`, `Assets/StarMat.mat` and `Assets/cfxr magic star hdr ab.mat`.
   - Move the root `.vfx` files into Game.
   - Add `.gitattributes` with LFS for new binaries.
10. **Rendering:**
    - Main-light shadowmap to 2048+ with 4 cascades and a distance tuned to camera use.
    - Drop FXAA on the galaxy camera (keep MSAA) or vice versa.
    - SSAO Downsample on.
    - Disable Realtime GI and fog.
    - Enable camera-relative culling.
    - Set the Starfield faces to 2048 max, or swap in a procedural sky.
11. **Music import:** switch the three music WAVs to Streaming.
12. **Start the Starsector replacement list** (22 shipped files, listed in section 2) and set a new default cursor.
