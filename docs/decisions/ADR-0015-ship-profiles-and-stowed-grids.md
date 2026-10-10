# ADR-0015 — Ship composition by profile registration; stowed and transient grids

**Status:** Accepted (owner, 2026-10-10; extensibility review X3)

## Context
`ShipSpawnService.Spawn` (SPEC-01) built a ship by calling a hard-coded list of managed setup helpers: `CombatGridSetup`, `PowerGridSetup`, `AbilityGridSetup`, `FlightSetup`, `PlanarConstraintSetup` and `AiSetup`. That caused four problems:
- Every new ship-level feature had to edit that list.
- No Burst system could spawn a ship, so a hangar could not.
- SPEC-31 repeated part of the list for armed stations, a second copy that had to stay in sync.
- A `GridSpawnRequest` recorded from the fixed step is converted in `GameInitGroup` once per frame, 0–6 ticks later depending on frame rate. That breaks the 30/60/144 FPS determinism gate for anything launched mid-combat.

## Decision
1. **Profiles.** `GridSpawnRequest` gains `GridProfile Profile` (SPEC-10). The enum is append-only: `None, PlayerShip, NpcShip, Station, SmallCraft, Dummy`.
   - Each spec appends its root component types for the profiles it serves to the singleton buffer `RootComponentRegistration {GridProfile Profile; ComponentType Type}` from a `[CreateAfter(typeof(GridSpawnSystem))]` system. This is the root-level twin of `FunctionalComponentRegistration`.
   - `GridSpawnSystem.ConvertToRoot` adds the profile's types in its existing batch. Types the requester already added keep their values; the rest start zeroed.
2. **Values through components, initialised by owners.**
   - **Requester values.** A requester sets per-spawn values by adding the component itself before conversion (main-thread `EntityManager` or any ECB, Burst included): `Allegiance`, `AiBrain { ProfileIndex }`, `StarSystemMember`, `OwnedBy` (ADR-0016).
   - **Never in a profile.** `Allegiance` is in no profile, because its zero value is the player faction. A grid without it is `Allegiance.None`.
   - **Owner init.** Every owner fills its zeroed components on the first update in `GameInitGroup`, using the `Initialized`/`Bound`/`SourceRevision` flags that already exist. The per-spec `*Setup.Add` helpers are removed; their `RootTypes` sets remain as the registration lists.
3. **Stowed grids.** A grid that must enter the simulation at an exact tick (for example a launched craft) is spawned ahead of time and **stowed**: `GridStowApi.Stow` disables the root and its `LinkedEntityGroup`, so physics, rendering, hash, AI, damage, LOD and rebase all skip it.
   - **Unstow.** `GridStowApi.Unstow` records, in the fixed step's GameplayEnd ECB: re-enable, then set the pose, velocities and graphical-smoothing history. The grid is live at the end of tick N at any frame rate.
   - **Exemption.** Unstowing initialises a body, so it falls under the architecture §8 initialisation exemption for `LocalTransform`/`PhysicsVelocity`.
   - **Preconditions.** Only grids whose spawn and body init have completed may be stowed.
   - **Cleanup.** Destroy-by-query cleanups (SPEC-31 unload) include disabled entities.
4. **Transient grids.** `GridSpawnFlags.Transient` marks grids that are never saved: `PersistentId` may be 0 without a warning, and no save path captures them. The owning feature records what it needs to recreate them (for example a carrier's bay counts).

## Consequences
- A new ship-level feature registers its root types and initialises them; spawners never change.
- Hangars (and later reinforcement waves) spawn from Burst through an ECB and launch tick-exact by unstowing.
- Armed stations get combat, power and turret AI through `GridProfile.Station`, with no second copy of the helper sequence in SPEC-31.
- **Spec changes:**
  - SPEC-10 §4.4/§5.7 and new §5.13 (stow).
  - SPEC-01 §5.7 (`ShipSpawnService` chooses a profile and adds requester components).
  - SPEC-20/21/22/23/24: registration instead of helpers; first-init values (`ShieldControl.RequestedOn`, the `ShieldRipple` ring, the AI anchor).
  - SPEC-31: station profile; unload query includes disabled entities.
  - Architecture §7.1 and §8.
- Owners of transient or stowed grids must handle star-system changes themselves, by stowing or despawning on the travel commit (SPEC-32). This is a rule for those features, not a new mechanism.

## Alternatives considered
- **Prefab entities per ship type, instantiated with `Instantiate`.** Rejected: a voxel ship's archetype depends on its structure (bricks and functional entities), so a prefab cannot hold it. Profiles cover the root only.
- **A Burst-callable spawn service with a fixed parameter struct.** Rejected: the parameter struct becomes the next hard-coded list.
- **Launch through a normal `GridSpawnRequest` and accept frame timing.** Rejected: it breaks the determinism gate and makes a big launch cost spawn time in the middle of combat.
