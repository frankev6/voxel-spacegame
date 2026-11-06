# Handoff for Astra: Voxel Spacegame (2026-11-06)

You led this repo until 2026-10-14. Claude (Opus) led it from 2026-10-14 to 2026-10-21. On 2026-11-04 and 11-05, Daniel, the second developer Frank races and plays co-op with, added control and flight tooling through Claude and pushed it (§4.0). This page covers what you need to pick it back up: the rules, what changed while you were away, where things stand, and what comes next.

`docs/HANDOFF.md` was brought up to date with this page on 2026-11-06. `docs/ORCHESTRATION.md` still stops at 2026-10-14, so trust this page, the ledger log (`docs/LEDGER.md` § Log, newest first) and the ADRs over it.

## 1. The project in one paragraph

**Spaceship Game V2**: a Unity 6.6 DOTS game. You build ships from functional voxel blocks, fly between star systems uncovering a mystery, and watch huge voxel ships and stations break apart. Frank (the owner) plays builds and steers by playtest feedback. The current direction is an **exploration slice**: one star system (the Hollow system) that should feel like *Sid Meier's Pirates!* or *Starcom*. You hear a rumour in port, cross open space to something unknown, and come home with a clue that opens the way on.

## 2. Where everything is

| What | Where |
|---|---|
| Main checkout | `/Users/frank/DEV/voxel-spacegame`, branch `main` |
| Remote | `origin` = public GitHub `frankev6/voxel-spacegame`. Daniel pushes his work there (last: `209e0658`, 2026-11-05). Local `main` has merged it and is ahead by this handoff; fetch and merge `origin/main` before starting, since he may have pushed since |
| Unity | 6000.6.5f1 at `/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app`. CLI at `~/.unity/bin/unity`, driving the open Editor through `com.unity.pipeline` |
| Co-op worktree | `/Users/frank/DEV/spaceship-game-v2-coop`, on `task/TRV-015-exploration-slice` (merged into main; holds only three stray `_Test *.meta` files) |
| Old Codex worker worktrees | `~/.codex/worktrees/{1369,5fab,6d4c,combat-hit-detection}`: DEV-021/EDT-022 branches from before 2026-10-14, all integrated. Stale; don't build on them |
| Old prototype (V1) | `/Users/frank/DEV/spaceship-game`: reference for feel and content only, never code |
| **Not this game** | `/Users/frank/DEV/voxel-pirates` is a separate fork: a pirate sailing slice (sea, ships, whirlpools) on branch `pirate-slice`, with no remote. Leave it alone unless Frank asks |

Uncommitted in the main checkout right now:
- `Assets/Game/Settings/Rendering/UniversalRenderPipelineGlobalSettings.asset`: Unity rewrote it when the Editor opened. Don't commit it unless you have a reason.

## 3. Frank's standing rules

Binding. Most are also in `AGENTS.md` §2 and `docs/HANDOFF.md` §1.

- **Push only on Frank's word.** Commit locally on task branches, merge to local `main` freely; every push is public and needs his go-ahead.
- **Decisions come as 2–4 options with trade-offs, recommended first.** Never a single proposal.
- **Show rendered results, not process.** He decides on screenshots, proof sheets and playable builds, never on spec prose. Look-dev and art come before spec polish.
- **Designs must be intricate and general.** Every mechanic should be a reusable exploration system, not a one-off minigame (hit force, zones and mines from racing are reused elsewhere, for example).
- **Record his words verbatim** in the ledger log and in the *Answer* column of open questions.
- **Fast validation.** Focused acceptance tests per change; broad regression only at checkpoints. Tune fixtures freely (weaker targets, higher damage, short distances); keep production balance. No repeated three-minute test loops.
- **Never block on a long run.** Scenarios, races and long suites run in the background, guarded so they abort on exceptions, stalls or a deadline.
- **Performance is advisory** (ADR-0042). At most two optimisation passes per system; record the gap and move on.
- **Art:** follow `docs/design/DESIGN-GUIDE.md` (NOCTURNE, approved). Human hulls are blocky, alien (Xeno) hulls smooth. No Starsector or other copyrighted art; fonts SIL OFL 1.1 only; AI-generated assets only with his approval, one at a time.
- **Sequencing** (owner, 2026-10-14): finish and connect M1–M3 before further M4 depth. Since then he has directed the work himself, through the racing track, the playground hub, the Hollow and the exploration slice. Follow his current direction, and ask with options when it's unclear.
- **Workers:** the three GPT-6.1-Sol worker threads in `ORCHESTRATION.md` are paused since 2026-10-14. Don't resume them without Frank.

## 4. What changed since you left (2026-10-14 → 2026-11-05)

Every item has a ledger log entry with the owner's words, proof images and tests. Newest first:

0. **Daniel's tooling and experimental 3D flight** (2026-11-04/05, all merged and pushed). In his ledger entries "Owner" quotes Daniel.
   - **Control sheet** (DEV-029): `controls` in the console opens a localhost page (`http://127.0.0.1:47810/`) for a second monitor. It shows every action, live input state and probes; the page lives in `tools/controlsheet/`.
   - **Flight configs panel** (DEV-030) on the same page: all 25 `FlightConfig` values, adjustable live (not saved).
   - **Experimental 3D flight** (FLT-016, ADR-0123), player ship only:
     - `flight3d on|off`, which DEV-031 added;
     - Lift on Y/H;
     - B makes the ship face where you aim, and P makes it face the selected target;
     - cruise is refused in 3D;
     - co-op `NetProtocol.Version` 9 carries Lift.
   - **Free-aim chase camera** (CAM-013, preset `freeaim`): it switches in automatically in 3D with B on, and `camera tilt=` tunes the crosshair offset.
   - **Enter playground from any scene** (DEV-032), except a Blueprint Lab outside the playground profile.
   - **Settings controls** (HUD-021):
     - sub-tabs by map, in two columns, one row per action;
     - a keyboard/controller switch;
     - Backspace/Delete unbinds;
     - a bigger opaque panel and legible hover colours throughout the UI.

1. **Exploration slice, phase 1** (TRV-015, DEV-028; ADR-0122). Plan: `docs/design/exploration/exploration-slice.md` (approved).
   - **In-world cruise replaces the old supercruise** (TRV-013 dropped). J toggles it. It spools 1 s, builds to 8 km/s, and slows by itself to arrive 1 km from anything. Mass lock from hostiles within 5 km, a 5 s lock after a hit, cruise zones cap it (the Scatter: 800 m/s). `CruiseSystem` is the sole writer of `Cruise` (ship edge 512).
   - **The Hollow system** in the playground (`HollowSystem`): a star, VESHA (ocean, 80 km), CINDER, ORRO (gas giant with a ring), PALE, the Scatter belt and the Haze nebula, about 640 km across, with the lane gate at the edge. Far bodies are drawn in scaled space. `playground goto=vesha|cinder|orro|pale|star|gate|hauler|colony|scatter|haze`.
   - **Cruise view:** the FOV opens with speed, plus speed lines, dust streaks, rumble and vignette (`CruiseEffects`). Bodies are placed at `beginContextRendering` (no jitter); far clip 1000 km; 4× MSAA.
2. **HUD redesign.** SPEC-61 §4.4 is superseded. No corner plates; marks on the world instead (`HudMarks`, `WorldOverlaySystem`):
   - arcs at the ship, and full bars never show;
   - edge chevrons instead of the radar;
   - a title card and an objective toast;
   - sensor ping on V.
3. **Feel prototype.**
   - **Auto-aim** is on by default (`camera assist=off` turns it off).
   - **Camera presets** (`FlightCameraPreset`; `camera <name>` in the console): `chase` is the default, `orbit`, `cursor` and `topdown`. C toggles free look between `chase` and `orbit`.
   - **VESHA** lies below the flight plane, with the Clan's tether rising to it (`goto=orbit`).
4. **ABL-006 emergency repairs and DMG-020 core floor.** Below 20% of core capacity a ship wrecks. Repairs rebuild engines first, up to 35% of what's missing, with a wrench icon, channel ring, weld sparks and sound.
5. **The Hollow** (DEV-023 option E): a pirate haven inside an asteroid, which is now **the playground** (you spawn at its entrance, the Maw).
   - **Size:** 92 blueprints, 2.58 M cells, mostly 2×2×2 and 4×4×4 large blocks (ADR-0121).
   - **Inside:** rock shading, shelter lighting under the rock, about 600 lamps, holograms and wayfinding, plus stations: market, quartermaster, gunsmith, parts, salvage, smuggler and docks.
   - **Defences:** forts and wall guns. Turrets hold fire when rock blocks the shot (AI-006).
   - **Build tools:** `Game/Content/Build Hollow` (from `ArtSource/Hub/hollow_*.py`) and `tools/hollow-map.sh`.
   - **History:** the earlier ARK Proving Grounds hub (DEV-022, districts behind portals) came first; the Hollow replaced it as the spawn.
6. **Xeno swarm.** `TestData/Scenarios/xeno_swarm.scenario` runs three waves of HUSKs and BROODMOTHERs against the Hollow while Frank defends it. `faction_xeno` is hostile to the Clan, Tlaxcala and Vestari.
7. **Batches 2–6.** Mechanisms (ADR-0119): gravity wells, the mine family, moving structures, lane-gate portals, pulses, energy canisters, gauntlet turrets, engine wash. Also the engine line (ADR-0120) and large hull blocks (ADR-0121). The legacy ships were removed; the WRAITH is the start ship.
8. **Racing with friends** (SPEC-91, SPEC-92, ADR-0118, all approved). Track 01 is a battle-graveyard layout with six racer hulls and racer AI with autopilot.
   - **Mechanics:** hit force and recoil, carried velocity, boost strips, field walls, gates, mines, respawn.
   - **Console:** `race open [laps=] [ai=]`, then `race start`. Guide: `docs/RACING-WITH-FRIENDS.md`; `tools/playtest/race-ai.sh`.
9. **Co-op, merged to main** (NET-001…019, ADR-0117 "one code path"). Unity Transport, host plus guests, prediction, and the same simulation path for every pilot. Frank's verdict: feasible with this many voxels. Played over Tailscale. A Windows build was set up.
10. **QA-001, merged.**
    - **Fixes:** the owner findings (flight feel, ADR-0114) and the review findings.
    - **Visuals:** the NOCTURNE visual pass with baked plating.
    - **Xeno grown hulls** (ADR-0115): HUSK and BROODMOTHER, signed-distance cells meshed by surface nets.

## 5. Where things stand

- **Ledger summary:** M0 and M1 done. M2: 8 todo, 1 in progress. M3: 12 todo, 11 in progress. M4: 13 todo, 12 in progress. A1 asset production: 29 todo.
- **Tests already failing on `main`** (each noted by the task that found them, none fixed):
  - `ConventionsTests`: `Mouse.current` in `CameraTargetSyncSystem.cs` and `HudSyncSystem.cs`, and `Debug.LogWarning` in `HollowSystem.cs`;
  - `GamepadTests.AllFlightActionsBound`: `Flight/SensorPing` has no gamepad binding;
  - `SettingsViewTests.Slider_PersistsImmediately`;
  - `ThemeFilesTests.ThemeUss_NoLiteralColorsOrFonts` on `toast.uss`;
  - `StaticStateAuditTests` lists seven older statics.
- **Known bug, unfixed:** loading a save can fail with `InvalidOperationException: No participant restore is active`. It's thrown from `SaveService.ReportRestoreWarning` via `GameSession.RestoreFrom` (`GameSession.cs:259`), then `SceneFlow.RecoverAsync`. Logged 2026-10-21 and seen again live on 2026-11-04. A good first fix.
- **Docs check fails:** `tools/check-docs.py` reports 1 error. SPEC-24 ABL-006 depends on DMG-020, which is in the ledger but defined in no spec. Add DMG-020 to SPEC-13.
- **Phase 1 leftovers:** the heat zone does nothing yet (it comes in phase 6), and bottom-edge marker labels can overlap.
- **Still waiting on Frank** (from QA-001): art approval of the magnet block, the UI sounds and the baked plating and debris textures (`docs/design/asset-manifest.csv`, status `review`); and whether small debris drops scrap.
- **Not done:** navigation strobes (need per-grid hull anchors); the GRAVID, LAMPREY and CRINOID aliens (the builder supports them); per-faction team colours in the pooled render path.

## 6. What's next

The approved exploration slice, in order. Each phase must be playable in the playground with a scenario that proves it before the next starts:

2. **"?" contacts and the loud ping:**
   - beyond 2 km (1 km in the Haze) a contact is an unnamed "?" marker;
   - the ping (V) names everything within 3 km, and is heard: hostile AI within 6 km turns toward you, and dormant sites may wake;
   - running quiet is the default.
3. **The sites:**
   - the lost hauler at the Haze, a real voxel derelict with Raider scavengers and a log clue naming CINDER;
   - the dead colony and its lift over CINDER;
   - Picket 9, with live turrets and a cache;
   - Skerry, an ice moonlet;
   - the fact-locked lane gate;
   - minor finds along the legs.
4. **Crew:** three named hires with one trait each, toasts commenting on finds, a crew panel.
5. **The away-team scene:** an illustrated dialog scene at the colony lift; traits gate the choices.
6. **Fuel, supplies and the star's heat zone.**
7. **The whole loop end to end**, then Frank's playtest.

Before starting phase 2, show Frank where things stand (screens or a short build) and confirm the order. His priorities may have moved since 2026-10-21.

## 7. How to work here

```bash
tools/compile.sh                                  # compile through the open Editor (seconds)
tools/test.sh editmode <ClassName>                # focused tests; one class per run
tools/test.sh playmode <ClassName>
tools/playtest/scenario.sh <name>                 # TestData/Scenarios/*.scenario; run in the background
tools/playtest/g.sh dev --cmd "<console command>" # drive the running game (spawn, playground goto=, race, probe, camera)
tools/playtest/shot.sh <name>                     # screenshot to Logs/playtest/
tools/check-docs.py                               # after editing any doc
```

- **The game:** Play from any scene boots through `Boot`. In game, F1 opens the dev console.
- **Useful console commands:**
  - `playground status`
  - `playground goto=<place>`
  - `spawn blueprint=<id> faction=<id> dz=<m> face=player ai=true`
  - `encounter spawn=<id>`
  - `camera <preset>`
  - `race …`
  - `probe x= z=`
- **Scenarios worth knowing:**
  - `hollow_system_cruise`: phase 1's proof;
  - `xeno_swarm`: playable, with `timeout=1800`;
  - `opening_chapter`;
  - the `r*` regression set.
- **Editor discipline:** don't edit C# while tests or scenarios are running in the Editor. Frank sometimes plays in the same Editor, so check before stopping Play mode, and say so when you do. Domain reload is off, so static state leaks between plays; tests that build a camera rig pin `topdown` for that reason.
- **Traps:** read `docs/07-implementation-traps.md` before DOTS, physics or rendering work. Shaders found with `Shader.Find` must be in Always Included Shaders for player builds.
- **Finishing a task:** acceptance checked, focused tests passing, `tools/compile.sh` clean, ledger row and log entry (with Frank's words and proof paths), commit `<ID>: <summary>` on `task/<ID>-<slug>`, merge to local `main`. Then update `docs/HANDOFF.md` §2–3 so the next worker starts from the truth.

## 8. Read next, in order

1. `AGENTS.md` (unchanged rules and commands)
2. `docs/LEDGER.md` § Log, the entries from 2026-10-14 to 2026-10-21
3. `docs/design/exploration/exploration-slice.md` and `world-and-scale.md`
4. ADR-0114 to ADR-0123
5. `docs/07-implementation-traps.md`, then the spec for whatever you pick up
