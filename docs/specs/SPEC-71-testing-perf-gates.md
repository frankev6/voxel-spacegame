# SPEC-71 — Test kit, determinism, replay and performance gates

| | |
|---|---|
| **Status** | Draft |
| **Area code** | TST (task IDs are `TST-001`, `TST-002`, …) |
| **Module(s)** | `Game.Tests.Common` (new test-only assembly, folder `Assets/Game/Tests/Common/`); `Game.Core` (`PerfMarkers`, folder `Assets/Game/Core/Diagnostics/`); `Game.App` (replay system and test hooks, folder `Assets/Game/App/Testing/`); `Game.Tests.EditMode`, `Game.Tests.PlayMode` (gate suites); `tools/` (perf summary, iteration gates) |
| **Depends on specs** | SPEC-02 (`SimClock`, `GameplayActive`, custom groups, `IPauseService`); SPEC-01 (`GameRoot`, `SceneFlow`, `SimClockAdvanceSystem`, sandbox entry); SPEC-03/20 (`PlayerCommand`, `PlayerInputSampleSystem`); SPEC-05 (`SavePaths`); SPEC-10/11/12/13 (benchmark structures, stats, `CellStateHasher`); SPEC-21/22/23 (combat systems for the determinism gate); SPEC-31/61 (frame-budget scenarios) |
| **Depended on by** | Every spec's tests (`EcsTestWorld`, `GameTestBase`, `PerfRun`), SPEC-70 (`GameFlowFixture`, `PlayerCommandRecorder`, replay, `PerfMarkers`), every task with a perf gate |
| **Milestone** | M0 (test kit), M1 (replay, game-flow fixture, voxel gates), M2 (determinism and soak), M3 (whole-game frame budgets) |

> Read [05-testing.md](../05-testing.md), [06-performance-budgets.md](../06-performance-budgets.md) and [07-implementation-traps.md](../07-implementation-traps.md) before this spec. This spec may not contradict them.

> **Owner policy, 2026-10-12 ([ADR-0042](../decisions/ADR-0042-advisory-performance-targets.md)):** every numeric performance target here and in dependent specs is advisory. Performance-only FAIL/inconclusive/unavailable results are recorded, not progression blockers. Keep correctness/determinism/leak checks mandatory. At most two initial optimization passes per system/feature across workers and handoffs, including benchmark troubleshooting; then move on to integrated gameplay. Existing assertion/reporting APIs may keep reporting failure honestly; that does not make a performance-only miss a workflow gate.

## 1. Purpose

Every other spec says "test with `EcsTestWorld`", "measure with the perf harness", "prove determinism at 30, 60 and 144 FPS". This spec builds those tools and owns the game-wide gates that no single module can own.

- **`EcsTestWorld`** builds a world with only the systems under test and steps exactly one fixed tick at a time, or whole frames of any length.
- **`GameTestBase`** turns on native-leak detection and fails a test on any unexpected error log.
- **`PerfRun`** measures medians and 95th percentiles under the reference settings and writes results the tools print against the budgets.
- **`PlayerCommandRecorder`** and its replay system record and replay player input per tick.
- **`GameFlowFixture`** boots the real game in PlayMode with throwaway save and settings folders.
- **Gate suites:**
  - the integrated voxel gates (moved here from SPEC-10 to break a dependency cycle);
  - the combat determinism gate;
  - the 10-minute soak;
  - the whole-game frame budgets;
  - the iteration gates.

The old prototype had no automated tests, and its determinism, leaks and frame drops were found by hand.

## 2. Scope

**In scope:**
- The `Game.Tests.Common` assembly with `EcsTestWorld`, `GameTestBase`, `PerfSettings`, `PerfRun`, `TransformStateHasher`, `DeterminismRunner`, `GameFlowFixture` and `PlayerCommandScript`.
- `PerfMarkers` (marker discovery for SPEC-70 and perf tests).
- Record and replay of `PlayerCommand`: `PlayerCommandRecorder`, `PlayerCommandReplaySystem`, the `.cmdrec` format.
- Test hooks for boot: `GameRootTestHooks`.
- Tooling: `tools/perf_summary.py`, an extension to `tools/test.sh perf`, and `tools/iteration-gates.sh`.
- Gate suites: voxel (06 voxel gates 1, 2, 4), determinism (06 combat), soak (06 errors), frame budget (06 whole game), iteration (06 developer experience).

**Out of scope (and who owns it instead):**
- Each module's own unit, system and perf tests: their specs, using this kit.
- Per-system gates: their specs, for example SPEC-12 PHY-019, SPEC-13 DMG-014, SPEC-21 WPN-013, SPEC-23 AI-012, SPEC-31 SYS-014.
- The scenario language and harness commands: SPEC-70.
- Save fixtures: SPEC-05 `SaveFixtureHarness`.
- `CellStateHasher`: SPEC-13.

## 3. Decisions (binding)

1. **`TST-001` is the contract and the implementation of the dependency-free kit.** The usual `-001` rule (types only, no behaviour) is waived for this spec: test helpers are useless as stubs, and every other spec's tests depend on them. `TST-001` depends only on SPEC-02's contract. Other specs depend on `TST-001`, never on `SPEC-71`, except SPEC-70's harness tasks, which need the M1 fixture and replay. Reason: removes the `SPEC-71` dependency cycles (item 16 and 52).
2. **A new test-only assembly, `Game.Tests.Common`** (folder `Assets/Game/Tests/Common/`):
   - `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, references every `Game.*` runtime assembly and the Unity Test Framework;
   - referenced by `Game.Tests.EditMode` and `Game.Tests.PlayMode`;
   - never by runtime assemblies.

   Architecture §3 gets the row (item 91). Reason: both test assemblies share the kit.
3. **`EcsTestWorld` controls time itself.**
   - The world is created with explicit system types only, so Unity's `UpdateWorldTimeSystem` is absent.
   - Time is set with `World.SetTime`.
   - `StepTicks` uses a simple fixed-rate manager: exactly one fixed tick per update.
   - `StepFrame` uses Unity's catch-up manager with `MaximumDeltaTime = 0.1`: at most 6 ticks per frame, as in the game.

   Reason: tests must never depend on wall time.
4. **Perf runs use the reference settings or are inconclusive.**
   - Settings: Burst on, safety checks off, job debugger off, leak detection off; at least 60 warm-up frames and 300 measured frames.
   - Metrics: median and p95.
   - `PerfSettings.AssertReference` calls `Assert.Inconclusive` (not `Fail`) when the settings are wrong.
   - Results go to `Logs/perf/<test>.json`.

   Reason: 06 reference settings; no false failures on a developer machine with safety checks on.
5. **Determinism is tested under fixed simulated frame rates.**
   - PlayMode runs set `Time.captureDeltaTime = 1/fps` (30, 60, 144) and `JobsUtility.JobWorkerCount` to 1 and to the maximum.
   - Everything is spawned, meshed and collider-installed while a pause token holds the simulation. The measured run starts at the same tick in every variant.
   - Scripted input changes only at even ticks.
   - Replays never contain spawns, star-system jumps (SPEC-32 decision 10), director rolls (SPEC-33 decision 6) or `Dormant` toggles (SPEC-11 Q3).

   Reason: spawns, jumps and dormancy are frame-granular by design. The gate measures the simulation itself.
6. **Replay is indexed by simulation tick at the frame input stage (ADR-0051).** `PlayerCommandReplaySystem` overwrites `PlayerCommand` after `PlayerInputSampleSystem` with the script value for the tick about to run (`SimClock.Tick`). Edge counters are absolute values from the script, so re-writing on frames without ticks is idempotent. Reason: SPEC-03/20 latch edges per frame and consume them per tick; the even-tick scripts used by the 30/60/144 FPS gate produce identical commands. Arbitrary odd-tick changes or other catch-up schedules are not covered by that frame-stage guarantee.
7. **Profiler markers are discovered, not registered.**
   - `PerfMarkers` lists recorders for every available marker in `ProfilerCategory.Scripts` whose name matches `^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$`, the architecture §11.11 naming.
   - Systems keep `static readonly ProfilerMarker` fields created with `new ProfilerMarker("<Type>.<Step>")`, which is Burst-compatible.

   Reason: no managed registry inside Burst code.
8. **PlayMode game tests use throwaway folders.**
   - `GameFlowFixture` sets `GameRootTestHooks.SaveRoot` and `.SettingsPath` to a fresh folder under `Temp/TestRuns/<guid>/` before loading the Boot scene, and deletes it in teardown.
   - The real profile is never touched.

   Reason: SPEC-70 Q4; tests must not corrupt player saves.
9. **Any unexpected error log fails a test.** `GameTestBase` and `GameFlowFixture` keep `LogAssert.ignoreFailingMessages = false` and enable `NativeLeakDetection.Mode = EnabledWithStackTrace` outside perf runs. Expected errors are declared with `LogAssert.Expect`. Reason: 06 "zero exceptions, zero leaks".
10. **Folders:**
    - `Assets/Game/Tests/Common/{Ecs,Perf,Determinism,Flow,Replay}/` → `Game.Tests.Common`;
    - `Assets/Game/Core/Diagnostics/PerfMarkers.cs` → `Game.Core.Diagnostics`;
    - `Assets/Game/App/Testing/` → `Game.App.Testing` (runtime pieces, compiled in `UNITY_EDITOR || DEVELOPMENT_BUILD`).

## 4. Data model

### 4.1 `EcsTestWorld` and `GameTestBase` (`Game.Tests.Common`)

```csharp
namespace Game.Tests.Common
{
    using Unity.Entities;

    /// <summary>World with only the systems under test. Implements SPEC-71 §5.1.</summary>
    public sealed class EcsTestWorld : System.IDisposable
    {
        public const float TickSeconds = 1f / 60f;
        public static EcsTestWorld Create(params System.Type[] systems);   // ISystem or ComponentSystemBase types; groups added from [UpdateInGroup]
        public World World { get; }
        public EntityManager EntityManager { get; }
        public uint TicksRun { get; }                    // fixed ticks executed since creation
        public double ElapsedSeconds { get; }

        public Entity AddGameplayActive();
        public Entity AddWorldSceneActive();
        public Entity AddSimClock(uint tick = 0, double universalTime = 0);   // auto-advanced by StepTicks unless SimClockAdvanceSystem is present
        public Entity AddSingleton<T>(T value) where T : unmanaged, IComponentData;
        public T GetSingleton<T>() where T : unmanaged, IComponentData;    // completes dependencies first

        public void StepTicks(int ticks);                // exactly `ticks` fixed ticks, one World.Update each
        public void StepFrame(float deltaSeconds);       // one frame; catch-up manager; requires SimClockAdvanceSystem if a SimClock exists
        public void UpdateSystem<T>() where T : unmanaged, ISystem;        // one direct update (unit tests of a single system)
        public void UpdateManaged<T>() where T : ComponentSystemBase;
        public void CompleteAll();                       // EntityManager.CompleteAllTrackedJobs
        public void Dispose();                           // CompleteAll; World.Dispose; restores World.DefaultGameObjectInjectionWorld
    }

    /// <summary>NUnit base for EditMode tests: leak detection, error-log guard. Implements SPEC-71 §5.2.</summary>
    public abstract class GameTestBase
    {
        [NUnit.Framework.SetUp] public void GameTestSetUp();      // NativeLeakDetection.Mode = EnabledWithStackTrace; LogAssert.ignoreFailingMessages = false
        [NUnit.Framework.TearDown] public void GameTestTearDown(); // disposes tracked worlds; LogAssert.NoUnexpectedReceived()
        protected EcsTestWorld CreateWorld(params System.Type[] systems);   // tracked; disposed in teardown even when the test throws
    }
}
```

### 4.2 Performance (`Game.Tests.Common.Perf`, `Game.Core.Diagnostics`)

```csharp
namespace Game.Core.Diagnostics
{
    /// <summary>Recorders for game profiler markers (decision 7). Dev builds and tests only.</summary>
    public static class PerfMarkers
    {
        public static void StartRecording(int capacityFrames = 1);   // idempotent; discovers markers (§5.3)
        public static void StopRecording();                          // disposes recorders
        public static int Count { get; }
        /// Fills `into` with (name, main-thread ms of the last frame), sorted descending; `max` entries.
        public static void SampleLastFrame(System.Collections.Generic.List<(string name, double ms)> into, int max);
        public static bool TryGetLastFrameMs(string markerName, out double ms);
    }
}

namespace Game.Tests.Common.Perf
{
    public static class PerfSettings
    {
        public static void AssertReference();      // decision 4; Assert.Inconclusive with the reason when not met
        public static bool IsReference(out string reason);
    }

    public readonly struct PerfStat { public readonly string Name; public readonly double Median, P95, Max, Mean; public readonly int Samples; public readonly string Unit;
        public PerfStat(string n, double med, double p95, double max, double mean, int s, string u) { Name = n; Median = med; P95 = p95; Max = max; Mean = mean; Samples = s; Unit = u; } }

    /// <summary>One measured run. Implements SPEC-71 §5.3.</summary>
    public sealed class PerfRun
    {
        public PerfRun(string testName, int warmupFrames = 60, int measuredFrames = 300);
        public PerfRun Marker(string markerName);           // main-thread ms per frame from PerfMarkers
        public PerfRun MarkerPrefix(string prefix);         // sum of every marker starting with prefix (e.g. "Brick")
        public PerfRun FrameTime();                         // Time.unscaledDeltaTime × 1000
        public PerfRun Custom(string name, System.Func<double> sample, string unit = "ms");
        public System.Collections.IEnumerator RunFrames(System.Action perFrame = null);   // PlayMode: yields frames
        public void RunEcs(EcsTestWorld world, System.Action perTick = null);             // EditMode: one StepTicks(1) per sample
        public PerfStat Get(string name);
        public void AssertMedian(string name, double maxMedian);
        public void AssertP95(string name, double maxP95);
        public void AssertMax(string name, double maxValue);
        public void Write(string gate = null, double budgetMedian = double.NaN, double budgetP95 = double.NaN);   // Logs/perf/<testName>.json + Performance Testing sample groups
    }
}
```

### 4.3 Determinism (`Game.Tests.Common.Determinism`)

```csharp
namespace Game.Tests.Common.Determinism
{
    public static class TransformStateHasher
    {
        /// Grid roots ordered by PersistentId.Value: Combine(id, bits of LocalTransform.Position/Rotation, PhysicsVelocity.Linear/Angular).
        public static ulong ComputeWorldHash(Unity.Entities.EntityManager em);
    }

    public readonly struct DeterminismVariant { public readonly int Fps; public readonly int Workers;   // Workers 0 = JobWorkerMaximumCount
        public DeterminismVariant(int fps, int workers) { Fps = fps; Workers = workers; } }

    public readonly struct DeterminismResult { public readonly DeterminismVariant Variant; public readonly ulong CellHash, TransformHash; public readonly uint EndTick;
        public DeterminismResult(DeterminismVariant v, ulong c, ulong t, uint end) { Variant = v; CellHash = c; TransformHash = t; EndTick = end; } }

    /// <summary>Runs one scenario under several variants (PlayMode). Implements SPEC-71 §5.5.</summary>
    public sealed class DeterminismRunner
    {
        public static readonly DeterminismVariant[] Standard = { new(60, 0), new(30, 0), new(144, 0), new(60, 1) };
        /// setup: spawns everything (runs while paused); script: input; ticks: measured ticks.
        public System.Collections.IEnumerator Run(GameFlowFixture fixture, System.Func<System.Collections.IEnumerator> setup,
                                                  Game.App.Testing.PlayerCommandScript script, int ticks,
                                                  System.Collections.Generic.IReadOnlyList<DeterminismVariant> variants,
                                                  System.Collections.Generic.List<DeterminismResult> results);
        public static void AssertAllEqual(System.Collections.Generic.IReadOnlyList<DeterminismResult> results);   // message lists every differing variant
    }
}
```

### 4.4 Record and replay (`Assets/Game/App/Testing/`, namespace `Game.App.Testing`)

```csharp
namespace Game.App.Testing
{
    /// <summary>One PlayerCommand per tick, stored as changes. Implements the .cmdrec format (§5.4).</summary>
    public sealed class PlayerCommandScript
    {
        public uint StartTick { get; }
        public uint EndTick { get; }                                   // exclusive
        public void Set(uint tick, in Game.Simulation.Commands.PlayerCommand command);   // tick ≥ previous tick; command holds absolute edge counters
        public Game.Simulation.Commands.PlayerCommand At(uint tick);    // last command set at or before tick (default before StartTick)
        public bool ChangesOnlyAtEvenTicks { get; }                     // decision 5 check
        public string ToText();                                         // §5.4
        public static PlayerCommandScript Parse(string text);           // FormatException with line number on error
        public static PlayerCommandScript Load(string path); public void Save(string path);
    }

    /// <summary>Records PlayerCommand per tick while active (dev builds). Used by SPEC-70 `record`.</summary>
    public sealed class PlayerCommandRecorder
    {
        public bool IsRecording { get; }
        public void Start(Unity.Entities.World world);
        public PlayerCommandScript Stop();
    }

    /// <summary>GameInitGroup, after PlayerInputSampleSystem. While a script is set, overwrites PlayerCommand with script.At(SimClock.Tick).</summary>
    public partial class PlayerCommandReplaySystem : Unity.Entities.SystemBase
    {
        public void Play(PlayerCommandScript script);   // starts at the next frame
        public void Stop();
        public bool IsPlaying { get; }
        public bool Finished { get; }                   // SimClock.Tick ≥ script.EndTick
    }

    /// <summary>Boot-time overrides for tests (decision 8). Read once by GameRoot.Awake. Editor and development builds only.</summary>
    [Game.Core.StaticStateOk("test hooks, reset by GameFlowFixture")]
    public static class GameRootTestHooks
    {
        public static string SaveRoot;          // null = default
        public static string SettingsPath;      // null = default
        public static bool SkipIntroScreens;    // MainMenu splash skipped
        public static void Reset();
    }
}
```

### 4.5 Game flow fixture (`Game.Tests.Common.Flow`)

```csharp
namespace Game.Tests.Common
{
    /// <summary>PlayMode base that boots the real game in throwaway folders. Implements SPEC-71 §5.6.</summary>
    public abstract class GameFlowFixture
    {
        [UnityEngine.TestTools.UnitySetUp] public System.Collections.IEnumerator GameFlowSetUp();
        [UnityEngine.TestTools.UnityTearDown] public System.Collections.IEnumerator GameFlowTearDown();
        protected Unity.Entities.EntityManager Em { get; }
        protected Unity.Entities.Entity PlayerRoot { get; }               // Entity.Null when none
        protected System.Collections.IEnumerator NewGame(ulong seed);      // SceneFlow new game; waits PlayerReady
        protected System.Collections.IEnumerator EnterSandbox();           // SPEC-01 sandbox (no galaxy)
        protected System.Collections.IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string what);   // fails with `what` on timeout
        protected System.Collections.IEnumerator WaitFrames(int frames);
        protected System.Collections.IEnumerator WaitTicks(int ticks);     // by SimClock.Tick
        protected bool PlayerReady { get; }                                // PlayerShip exists, GameplayActive, PhysicsCollider on the root, context Flight
        protected Game.Core.PauseToken HoldSimulation(string reason);      // IPauseService.Acquire
        protected T Service<T>() where T : class;                          // Services.Get
    }
}
```

## 5. Systems, services and flow

### 5.1 `EcsTestWorld`

**`Create(types)`:**
1. `World = new World("EcsTestWorld#" + n, WorldFlags.Game)`.
2. `DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, types)`. This creates `InitializationSystemGroup`, `SimulationSystemGroup`, `PresentationSystemGroup` and every intermediate group named by `[UpdateInGroup]`.
3. `fixedGroup = World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>()`; `fixedGroup.Timestep = TickSeconds`.
4. Store the original `World.DefaultGameObjectInjectionWorld` and set it to `World` (some APIs read it).

**`StepTicks(n)`**, repeated `n` times:
1. `fixedGroup.RateManager = new RateUtils.FixedRateSimpleManager(TickSeconds)`.
2. `ElapsedSeconds += TickSeconds`; `World.SetTime(new TimeData(ElapsedSeconds, TickSeconds))`; `World.Update()`.
3. `CompleteAll()`; `TicksRun++`.
4. If a `SimClock` exists and `SimClockAdvanceSystem` is not in the world: `Tick += 1`, `UniversalTime = SessionStartUniversalTime + Tick / 60.0` (SPEC-02 decision 6).

**`StepFrame(dt)`:**
1. If a `SimClock` exists and `SimClockAdvanceSystem` is absent → `InvalidOperationException` ("add SimClockAdvanceSystem for frame stepping").
2. `fixedGroup.RateManager = new RateUtils.FixedRateCatchUpManager(TickSeconds)`, with `World.MaximumDeltaTime = 0.1f`.
3. `ElapsedSeconds += dt`; `SetTime`; `Update`; `CompleteAll`.
4. `TicksRun += ticks executed`, read from `SimClock.Tick` before and after, or counted by a hook system.

**`Dispose`:** `CompleteAll`; `World.Dispose()`; restore `DefaultGameObjectInjectionWorld`. Disposing twice is safe.

### 5.2 `GameTestBase`

- **SetUp:**
  - remember the previous `NativeLeakDetection.Mode` and set `EnabledWithStackTrace`;
  - `LogAssert.ignoreFailingMessages = false`.
- **TearDown:**
  - dispose every world from `CreateWorld` (try/finally);
  - restore the leak mode;
  - `LogAssert.NoUnexpectedReceived()`.

Leaks are reported by Unity as error logs, so they fail the test through the guard.

### 5.3 Perf: markers, runs and tooling

**`PerfMarkers.StartRecording()`:**
1. `ProfilerRecorderHandle.GetAvailable(list)`.
2. Keep the handles whose `ProfilerRecorderHandle.GetDescription(h)` has category `Scripts` and a name matching the decision 7 pattern.
3. Create `ProfilerRecorder.StartNew(category, name, 1)` for each.

`SampleLastFrame`: reads `LastValue` (ns) → ms, sorts descending and takes `max`. Markers that appear later (systems created after start) are found by calling `StartRecording` again, which only adds new names.

**`PerfRun`:**
1. `PerfSettings.AssertReference()` (perf tests only).
2. `PerfMarkers.StartRecording()`.
3. Run the warm-up frames (or ticks for `RunEcs`).
4. For each measured frame, sample every configured source into its own `double[]`.
5. **Stats:**
   - median = the average of the two middle values when the count is even;
   - p95 = nearest rank `ceil(0.95 × n)`;
   - max; mean.
6. `Write`:
   - report each stat as a Unity Performance Testing `SampleGroup` (`Measure.Custom`);
   - write `Logs/perf/<testName>.json`: `{ "test", "gate", "utc", "machine", "unity", "stats": [ { "name", "unit", "median", "p95", "max", "mean", "samples" } ], "budget": { "median", "p95" }, "pass" }`.

**`PerfSettings.IsReference`** checks all of:
- `BurstCompiler.IsEnabled`;
- `!BurstCompiler.Options.EnableBurstSafetyChecks`;
- `!JobsUtility.JobDebuggerEnabled`;
- `NativeLeakDetection.Mode == Disabled`;
- in the Editor, the Game view at 1920×1080 for rendering gates only (`PerfRun` option).

**`tools/perf_summary.py`:** reads `Logs/perf/*.json` written during the run and prints a table: test, gate, median, p95, budget, PASS/FAIL. `tools/test.sh perf` calls it after `test_summary.py`, and exits non-zero if any `pass` is false.

### 5.4 `.cmdrec` format and replay

`.cmdrec` is UTF-8 text with one record per line; `#` starts a comment.
- **Header lines:** `cmdrec 1`, `start <tick>`, `end <tick>`, optionally `seed <u64 hex>` and `content <ContentCatalog.IdentityHash hex>`.
- **Records:** `<tick> <thrustX> <thrustY> <turn> <aimX> <aimY> <aimZ> <aimValid> <flags> <fireGroups> <e0> … <e9>`, one line per tick where the command changed.
  - Finite floats use `"R"` round-trip invariant culture, preserving negative zero (ADR-0051); non-finite values are rejected.
  - `e0…e9` are the 10 absolute `PlayerEdgeCounters` bytes in declaration order (SPEC-20 §4.1; `e9` = `Cruise`, always present).
  - Ticks are ascending.

`Parse` fails with the line number on a malformed or non-ascending line.

**Endpoints and metadata (ADR-0051):** first Set establishes StartTick; every Set extends EndTick to at least tick + 1, including an unchanged held value. Same-tick Set replaces that tick. FrameIndex is transient: excluded from text/change comparisons and supplied by replay. Optional seed/content headers survive text round trips. Recorder Start fixes StartTick to the current clock tick and samples immediately; Stop fixes the exclusive EndTick to the current clock tick and discards any sample at that endpoint.

**`PlayerCommandRecorder.Start(world)`:**
- add or enable a recording hook system in `GameInitGroup` after `PlayerCommandReplaySystem` (which follows `PlayerInputSampleSystem`);
- each frame, if `SimClock.Tick` differs from the last recorded tick or the command changed: `Set(SimClock.Tick, PlayerCommand)`.

`Stop` returns the script with `EndTick = Tick`.

**`PlayerCommandReplaySystem`** (`GameInitGroup`, `[UpdateAfter(typeof(PlayerInputSampleSystem))]`, managed, `RequireForUpdate<SimClock>`):
1. While playing: `cmd = script.At(SimClock.Tick)`; `cmd.FrameIndex = Time.frameCount`; write the `PlayerCommand` singleton.
2. When `Tick ≥ EndTick`: `Finished = true`; stop overwriting (live input resumes).
3. SPEC-70 `record` and `replay` call these (DEV-016).

### 5.5 `DeterminismRunner`

For each variant:
1. Reset the game: `fixture.EnterSandbox()`, with a fresh session each time.
2. `token = HoldSimulation("Determinism")`.
3. `Time.captureDeltaTime = 1f / fps`; `JobsUtility.JobWorkerCount = workers == 0 ? JobsUtility.JobWorkerMaximumCount : workers`.
4. `yield return setup()` (spawns while paused).
5. Wait until every grid has `GridSpawning` disabled and SPEC-11/12 report no pending meshes and colliders (checked by their stats singletons).
6. `startTick = SimClock.Tick`; set the replay script with its ticks offset by `startTick`.
7. Release the token.
8. Wait until `SimClock.Tick ≥ startTick + ticks`. Ticks are counted, not frames.
9. Hold again.
10. Results: `CellStateHasher.ComputeWorldHash(em)` (SPEC-13) and `TransformStateHasher.ComputeWorldHash(em)`.
11. Restore `captureDeltaTime = 0` and the worker count.

A frame can run past the target tick, so the runner holds the simulation through a hook that requests a pause when `SimClock.Tick == startTick + ticks`. This is a `DeterminismStopSystem` in `GameInitGroup` that acquires the token on the main thread before the fixed step runs. The hash is therefore taken at exactly that tick.

`AssertAllEqual`: every result equals the first; otherwise fail, listing the variants and hashes.

### 5.6 `GameFlowFixture`

**SetUp:**
1. Set leak detection to `EnabledWithStackTrace`, keep unexpected error logs fatal, and capture the last 20 log lines for timeout diagnostics.
2. Settle/quit an existing game session and destroy its root before replacing its world. Reset `GameRootTestHooks`.
3. `dir = Path.GetFullPath(Path.Combine("Temp/TestRuns", Guid))`; create it and set `SaveRoot = dir + "/saves"`, `SettingsPath = dir + "/settings.json"`, `SkipIntroScreens = true`.
4. Load `Boot`, then explicitly call the existing `Bootstrap.Boot` (RuntimeInitializeOnLoadMethod is not rerun by scene loads). The test-only Common assembly gets friend access to App for this existing lifecycle; no new runtime API or assembly reference.
5. On a null graphics device only, disable `VoxelConfig.RequireRenderer` so headless correctness tests can reach player readiness. GPU runs retain production readiness.
6. Wait for the production `SceneFlow` to settle in MainMenu (30 s), then create/activate a throwaway profile.

**TearDown:**
1. Wait for an active transition to settle. Quit an active session, or transition Lab/World to MainMenu.
2. Destroy the `GameRoot` object; wait 2 frames; reset test hooks.
3. Delete `dir` (IO/access errors warn), restore leak-detection mode and unsubscribe log capture. Idempotent cleanup lets a derived teardown inspect the result.
4. `LogAssert.NoUnexpectedReceived()`; unexpected errors also fail through Unity's normal per-test check.

`NewGame(seed)` calls the canonical `IGameFlow.NewGameAsync(new NewGameArgs("Test", seed, false, ContentId.None))`, then waits for `PlayerReady` (60 s). M1's production new-game path uses the imported starter without galaxy streaming; SPEC-30/31 will extend that same flow. `EnterSandbox()` calls `SceneFlow.EnterSandboxAsync(new SandboxArgs(true, 42))` and waits for the same readiness. The intro hook already exists from FND-005; there is no splash yet to skip (HUD-003 owns it).

### 5.7 Gate suites

| Gate (06) | Scenario | Owner task |
|---|---|---|
| Voxel: 1M idle ≤ 2 ms; memory ≤ 40 MB; spawn 1M no frame over 8 ms and ready ≤ 30 frames | VOX-018's original procedure, unchanged (moved, item 16) | TST-004 |
| Determinism: identical cell hash after 600 ticks at 30/60/144 FPS, workers 1 and max | Sandbox. Player ship from `ship_frigate` and 5 AI ships (2 allied, 3 hostile, profiles `ai_default`), spawned at fixed positions while paused. The script holds thrust and fire for 120-tick blocks, turns and rams one hostile at tick 300, and toggles autofire at tick 400. Runs 600 ticks | TST-005 |
| Errors: zero exceptions, ECB errors and leaks in a 10-minute soak | Sandbox. Waves of 6 hostile ships respawned when fewer than 3 remain (dev spawn API), the player on an autofire replay loop, for 36,000 ticks at 60 FPS. Assert: no error logs; entity count ≤ 1.5 × its count after the first wave; native allocations (`UnsafeUtility.GetAllocationCount`-style counters when available, else `Profiler.GetTotalAllocatedMemoryLong`) within 5 % of the value after wave 1 | TST-006 |
| Frame budget: idle flight ≤ 6 ms main; heavy combat ≤ 10 ms median / 14 ms p95; menus ≤ 4 ms; 0 B GC per frame in flight | **Idle:** new game; at `ark_station` (200k-cell blueprint per SPEC-42/GAL-012); 5 neutral ships patrolling; 300 frames. **Combat:** sandbox with 30 × 20k-cell ships firing (SPEC-21 benchmark composition). **Menus:** the pause menu and the inventory open, 300 frames each | TST-007 |
| Iteration: enter Play Mode ≤ 1 s; `tools/compile.sh` (no changes) ≤ 10 s; EditMode suite ≤ 60 s | `tools/iteration-gates.sh` measures each 3 times and reports the median | TST-008 |

## 6. Public API

| Call | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `EcsTestWorld.Create/StepTicks/StepFrame/Dispose` | Test thread | §5.1; `StepFrame` throws without `SimClockAdvanceSystem` when a `SimClock` exists |
| `GameTestBase` | NUnit EditMode | §5.2 |
| `PerfRun.*`, `PerfSettings.*` | Perf tests | §5.3; inconclusive outside reference settings |
| `PerfMarkers.*` | Dev builds or Editor | §5.3 |
| `PlayerCommandScript.Parse/ToText` | — | Round trip exact (floats with `"R"`) |
| `PlayerCommandReplaySystem.Play/Stop` | World scene with `SimClock` | §5.4 |
| `DeterminismRunner.Run/AssertAllEqual` | PlayMode, `GameFlowFixture` | §5.5 |
| `GameFlowFixture.*` | PlayMode | §5.6 |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| A test forgets to dispose an `EcsTestWorld` created through `CreateWorld` | Disposed in teardown; no leak reported |
| A test creates a world with `new World` directly and leaks it | Leak error log → test fails (decision 9) |
| A perf test runs with safety checks on | Inconclusive with the reason, no failure |
| Two perf tests write the same `Logs/perf` name | The later overwrites; names are test full names, which are unique |
| A replay script changes input at an odd tick | `ChangesOnlyAtEvenTicks == false` → `DeterminismRunner` fails fast with the tick |
| The Boot scene fails to reach the main menu within 30 s | Fixture fails with the SceneFlow state and the last 20 log lines |
| The temp folder cannot be deleted | Warning only |
| `StepTicks` with no fixed-step systems in the world | Time advances; nothing else happens |

## 8. Performance budget

The kit itself has no runtime budget in the shipped game: it is test-only, except `PerfMarkers` and the replay system, which are dev-only. Its own targets:
- `EcsTestWorld.Create` + `Dispose` ≤ 20 ms;
- `StepTicks(1)` overhead ≤ 0.1 ms beyond the systems;
- EditMode suite share ≤ 5 s;
- tested in TST-001.

The game gates it owns are those listed in §5.7.

## 9. Tasks

### TST-001 — Test kit core
- **Milestone:** M0
- **Tier:** frontier — F2: ECS world construction and fixed-step timing that every system test in the project relies on
- **Depends on:** `CORE-001`
- **Do:**
  1. Create the `Game.Tests.Common` assembly (decision 2) and add the architecture §3 row (item 91).
  2. Implement `EcsTestWorld` (§5.1), `GameTestBase` (§5.2), `PerfMarkers` and `PerfSettings`, `PerfRun` and `PerfStat` (§5.3).
  3. Add `tools/perf_summary.py` and the `tools/test.sh perf` hook.
- **Files:** `Assets/Game/Tests/Common/Game.Tests.Common.asmdef`; `Assets/Game/Tests/Common/Ecs/{EcsTestWorld,GameTestBase}.cs`; `Assets/Game/Tests/Common/Perf/{PerfSettings,PerfStat,PerfRun}.cs`; `Assets/Game/Core/Diagnostics/PerfMarkers.cs`; `tools/perf_summary.py`; `tools/test.sh`; `Assets/Game/Tests/EditMode/TestKit/{EcsTestWorldTests,PerfRunTests,PerfMarkersTests}.cs`
- **Acceptance criteria:**
  - [ ] A counting test system in the fixed step runs exactly 7 times for `StepTicks(7)`.
  - [ ] `StepFrame(0.05f)` runs exactly 3 ticks; `StepFrame(0.2f)` runs 6 (cap).
  - [ ] An auto-advanced `SimClock` reads `Tick == 7` and `UniversalTime == 7/60` after `StepTicks(7)`.
  - [ ] `StepFrame` without `SimClockAdvanceSystem` throws when a `SimClock` exists.
  - [ ] A world created through `CreateWorld` and not disposed by the test leaves no leak. A deliberately leaked `NativeArray` fails the guard test, which is marked `[Explicit]` and run by the acceptance step.
  - [ ] **Stats:** median of 1..300 = 150.5; p95 = 285.
  - [ ] **Output:** the JSON matches §5.3; `perf_summary.py` prints PASS/FAIL and exits 1 on a failing record.
  - [ ] A test `ProfilerMarker("Foo.Bar")` is discovered by `PerfMarkers`.
- **Tests:** `EcsTestWorldTests.StepTicks_Exact`, `.StepFrame_CatchUpAndCap`, `.SimClock_AutoAdvance`, `.StepFrame_RequiresAdvanceSystem`, `.CreateWorld_DisposedOnTeardown`; `PerfRunTests.Stats_MedianP95`, `.Write_Json`; `PerfMarkersTests.Discovers_NamedMarker`.

### TST-002 — Record and replay
- **Milestone:** M1
- **Tier:** standard — text format and per-tick overwrite rule fully given in §5.4
- **Depends on:** `TST-001`, `FLT-001`, `INP-001`
- **Do:** Implement `PlayerCommandScript` (format, `Parse`/`ToText`/`Load`/`Save`, `At`, the even-tick check), `PlayerCommandRecorder` with its hook system, and `PlayerCommandReplaySystem` (§5.4).
- **Files:** `Assets/Game/App/Testing/{PlayerCommandScript,PlayerCommandRecorder,PlayerCommandRecordSystem,PlayerCommandReplaySystem}.cs`; `Assets/Game/Simulation/Commands/PlayerCommand.cs` (ownership comment); `Assets/Game/Tests/EditMode/TestKit/ReplayTests.cs`
- **Acceptance criteria:**
  - [x] `Parse(ToText(s))` equals `s` for every serialized field, with finite floats bit-exact (FrameIndex is transient, ADR-0051).
  - [x] **Replay into an `EcsTestWorld`** (with `PlayerCommandApplySystem`): the `ShipCommand` sequence per tick is identical at frame steps of 1/30, 1/60 and 1/144 s.
  - [x] An edge recorded once fires exactly once in each variant.
  - [x] The replay stops at `EndTick`.
- **Tests:** `ReplayTests.Text_RoundTrip`, `.Replay_SameShipCommands_AllFrameRates`, `.Edge_FiresOnce`, `.StopsAtEnd`, `.Parse_ErrorLine`.

### TST-003 — Game flow fixture and boot test hooks
- **Milestone:** M1
- **Tier:** standard — fixture steps given in §5.6
- **Depends on:** `TST-001`, `SPEC-01`, `SPEC-05`
- **Do:**
  1. Implement `GameRootTestHooks` and the `GameRoot.Awake` reads of it, with a ledger note to SPEC-01/05 (item 92): save root via `new SavePaths(root)`, settings path, intro skip.
  2. Implement `GameFlowFixture` (§5.6).
- **Files:** `Assets/Game/App/Testing/GameRootTestHooks.cs`; `Assets/Game/App/Boot/GameRoot.cs` (existing hook reads); `Assets/Game/App/AssemblyInfo.cs` (test-only friend access); `Assets/Game/Tests/Common/Flow/GameFlowFixture.cs`; `Assets/Game/Tests/PlayMode/TestKit/GameFlowFixtureTests.cs`
- **Acceptance criteria:**
  - [x] Boot reaches the menu.
  - [x] `NewGame(42)` yields `PlayerReady` (M1: sandbox; the galaxy path once SPEC-30/31 exist).
  - [x] A save made in the test lands under the temp folder and nothing is written to the real profile (folder timestamps unchanged).
  - [x] Teardown deletes the folder.
- **Tests:** `GameFlowFixtureTests.Boot_ReachesMenu`, `.Sandbox_PlayerReady`, `.Saves_InTempRoot`, `.Teardown_Cleans`.

### TST-004 — Integrated voxel gate suite (moved from SPEC-10 VOX-018)
- **Milestone:** M1
- **Tier:** frontier — F4: owns three 06 voxel gates
- **Depends on:** `TST-001`, `SPEC-10`, `SPEC-11`, `SPEC-12`
- **Do:** VOX-018's procedure, unchanged: PlayMode performance tests with the full voxel pipeline, URP camera at 1920×1080, reference settings (`PerfSettings`), 60 warm-up frames and 300 measured frames.
  1. Idle `HollowStation1M` 200 m in front of the camera: the sum of `Game.Voxel` system markers plus `EntitiesGraphicsSystem` (`PerfRun.MarkerPrefix`).
  2. Memory per SPEC-10 §8.2 through `VoxelMemoryStats`.
  3. Spawn `HollowStation1M`: per-frame voxel work, and frames until every brick has a mesh and SPEC-12 reports all colliders built.

  Record the results in the ledger *Measurements*. Remove VOX-018 from SPEC-10 and its ledger row (done with this spec, item 16).
- **Files:** `Assets/Game/Tests/PlayMode/Voxel/{VoxelGatePerfTests,VoxelMemoryStats}.cs`
- **Acceptance criteria:**
  - [ ] Idle median ≤ 2.0 ms main thread for all voxel systems combined.
  - [ ] Memory ≤ 40 MB for `HollowStation1M`; `ThinShellStation1M` reported, not asserted.
  - [ ] Spawn: no frame over 8 ms of voxel work; fully meshed and collidable within 30 frames.
- **Tests:** `VoxelGatePerfTests.IdleStation1M_VoxelMainThreadUnder2ms`, `.HollowStation1M_MemoryUnder40MB`, `.ThinShell_Report`, `.SpawnStation1M_NoFrameOver8ms_ReadyWithin30Frames`.

### TST-005 — Determinism runner and the combat determinism gate
- **Milestone:** M2
- **Tier:** frontier — F4: owns the 06 determinism gate; F2: worker-count and frame-rate variance
- **Depends on:** `TST-002`, `TST-003`, `SPEC-13`, `SPEC-20`, `SPEC-21`, `SPEC-22`, `SPEC-23`
- **Do:**
  1. Implement `TransformStateHasher`, `DeterminismRunner` and `DeterminismStopSystem` (§5.5).
  2. Write `TestData/Replays/determinism_combat.cmdrec` (§5.7 row 2) and the gate test.
- **Files:** `Assets/Game/Tests/Common/Determinism/{TransformStateHasher,DeterminismRunner,DeterminismVariant,DeterminismResult}.cs`; `Assets/Game/App/Testing/DeterminismStopSystem.cs`; `TestData/Replays/determinism_combat.cmdrec`; `Assets/Game/Tests/PlayMode/Simulation/CombatDeterminismTests.cs`
- **Acceptance criteria:**
  - [ ] All four standard variants give identical cell and transform hashes at tick `start + 600`.
  - [ ] A deliberately non-deterministic test system (it uses `UnityEngine.Random` in a fixed-step job) makes the runner fail and name the differing variants. This is an `[Explicit]` self-test.
  - [ ] The hashes are taken at exactly `start + 600` in every variant.
- **Tests:** `CombatDeterminismTests.SixShips_600Ticks_AllVariantsEqual`, `.SelfTest_DetectsNondeterminism` (Explicit).

### TST-006 — Ten-minute combat soak
- **Milestone:** M2
- **Tier:** standard — scenario and assertions given in §5.7
- **Depends on:** `TST-005`, `SPEC-70`
- **Do:** Implement the soak test per §5.7 row 3, in category `Soak` (excluded from the default PlayMode run). Use `tools/test.sh playmode Soak` and record the result in *Measurements*.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/CombatSoakTests.cs`; `TestData/Replays/soak_autofire.cmdrec`
- **Acceptance criteria:**
  - [ ] 36,000 ticks with zero error logs.
  - [ ] Entity count and allocation bounds hold.
- **Tests:** `CombatSoakTests.TenMinutes_NoErrorsNoGrowth`.

### TST-007 — Whole-game frame budgets
- **Milestone:** M3
- **Tier:** frontier — F4: owns the 06 whole-game frame budget gates
- **Depends on:** `TST-003`, `SPEC-31`, `SPEC-33`, `SPEC-61`, `SPEC-21`
- **Do:** Write the three scenarios of §5.7 row 4 as PlayMode perf tests (`PerfRun.FrameTime`, main-thread `PlayerLoop` time, GC alloc per frame via `ProfilerRecorder("GC.Alloc")`). Record the results.
- **Files:** `Assets/Game/Tests/PlayMode/Game/FrameBudgetPerfTests.cs`
- **Acceptance criteria:**
  - [ ] Idle ≤ 6 ms main thread median and 0 B GC.
  - [ ] Combat ≤ 10 ms median and ≤ 14 ms p95.
  - [ ] Menus ≤ 4 ms and ≤ 1 KB/frame GC.
  - [ ] On failure, the record names the top 10 markers of the median frame.
- **Tests:** `FrameBudgetPerfTests.IdleFlight_Budget`, `.HeavyCombat_Budget`, `.Menus_Budget`.

### TST-008 — Iteration gates
- **Milestone:** M0
- **Tier:** standard — shell timing script
- **Depends on:** `TST-001`
- **Do:**
  1. Write `tools/iteration-gates.sh`. It runs 3 times each and prints the medians against 06:
     - `tools/compile.sh` with no changes, timed;
     - enter Play Mode, measured through the CLI (`editor_play` until `GameplayActive` or the menu, then `editor_stop`);
     - `tools/test.sh editmode`, timed.
  2. Assert in a ProjectSetup EditMode test that Enter Play Mode Options has domain and scene reload disabled.
- **Files:** `tools/iteration-gates.sh`; `Assets/Game/Tests/EditMode/TestKit/ProjectSettingsTests.cs`
- **Acceptance criteria:**
  - [ ] The script prints three rows with PASS/FAIL.
  - [ ] The settings test passes.
- **Tests:** `ProjectSettingsTests.EnterPlayMode_ReloadsDisabled`.

### TST-009 — Serialize Unity tool operations and validate live-test identity
- **Milestone:** M0
- **Tier:** frontier — F5: cross-process ownership of the Editor test/result channel
- **Depends on:** `TST-008`
- **Do:** Fix the M0 system-review finding that overlapping live suite requests overwrite the shared Pipeline result channel. Use an OS-released project lock for compile, test and iteration entry points (nested calls reuse ownership). Reject an already-running external test before starting. Enumerate the requested mode/filter test inventory and verify the completed result's names, counts and statuses against it before exporting XML. Preserve bounded request/result evidence. Direct Pipeline calls bypass the shell lock, so do not claim a server-issued run ID that the installed API does not provide.
- **Files:** `tools/unity_tool_lock.py`, `tools/live-tests.py`, `tools/{compile,test,iteration-gates}.sh`, `tools/playtest/rc.sh`, `tools/tests/test_unity_tools.py`, `docs/08-tools.md`
- **Acceptance criteria:**
  - [ ] A competing tool invocation fails before changing XML/logs or dispatching a Unity mutation.
  - [ ] OS process exit releases ownership; nested iteration calls do not deadlock.
  - [ ] Results from another mode/filter or inconsistent summary are rejected.
  - [ ] Full live EditMode, PlayMode and perf runs still pass; direct interrupted-run recovery refuses to clobber an active request.
- **Tests:** Python unittest cases for competing/released locks, nested ownership, inventory mismatch and inconsistent summaries; live overlap smoke plus full Unity suites.

## 10. Open questions

| # | Question | Default if unanswered | Decider | Answer |
|---|---|---|---|---|
| 1 | Should soak tests run nightly on a separate machine? | Manual before each milestone exit | Owner | — |
| 2 | Native memory measurement API for the soak | `Profiler.GetTotalAllocatedMemoryLong` plus entity counts until a better counter is available | Tech lead | — |
| 3 | Requested changes to other specs | `_INTEGRATION-TODO.md` items 16, 52, 91–93 | Tech lead | — |
| 4 | §5.1 steps the fixed group with Unity's `FixedRateSimpleManager`/`FixedRateCatchUpManager`, but a fresh catch-up manager runs a tick at t=0 and then catches up from 0, and float `1/60f` makes `StepFrame(0.05f)` run 2 ticks instead of 3 (found in TST-001, 2026-10-11) | **Resolved in TST-001:** `EcsTestWorld` drives the fixed group with its own counted rate manager, which pushes fixed time and the group allocator as Unity's managers do. `StepTicks(n)` runs exactly n; `StepFrame(dt)` runs the whole ticks of an accumulator (1e-6 tolerance), at most 6, dropping excess time like the catch-up manager. Reduced worlds also get the default command buffer systems and every group their attributes order against (empty anchors), so they log no ordering warnings | Tech lead | — |
| 5 | `PerfRun.RunEcs` samples markers per tick, but an EditMode loop never ends a profiler frame, so `ProfilerRecorder.LastValue` never changes (found in TST-001) | **Resolved in TST-001:** `RunEcs` samples the recorder's `CurrentValue` before and after each tick (internal `PerfMarkers.TryGetCurrentFrameMs`); `RunFrames` keeps `LastValue` | Tech lead | — |
| 6 | Decision 4 makes every perf run inconclusive outside the reference settings, but the existing main-thread micro-gates (StableHash, EventBus, SaveWriter, ContentService, orientation tables, SimEventChannel, the managed-event drain) would then never be enforced in the Editor; and `LogAssert.NoUnexpectedReceived()` in `[TearDown]` does not see the test body's plain logs (found in TST-001) | **Resolved in TST-001:** micro-gates that time their own loop go through `PerfRun.Gate(name, samples, budget)`, which writes the `Logs/perf` record and asserts the median without the settings check; `RunFrames`/`RunEcs` keep decision 4. `GameTestBase` keeps the teardown call; error logs fail tests through Unity's default check (`ignoreFailingMessages = false`), and plain logs and warnings do not fail | Tech lead | — |

| 7 | Applied settings disabled only domain reload; live wrappers used an obsolete CLI schema and ignored process failure | **Answer (resolved in TST-008):** apply and guard both reload flags (ADR-0020); use the installed Pipeline command schema and complete structured results, export XML, clear stale results and reject failed process/CLI execution even when XML reports passes. Iteration timings include CLI transport and script wall time with an idle open Editor. | Tech lead | — |

| 8 | Replay/record managed systems and PlayerCommand override absent from architecture; endpoints/FrameIndex unspecified | Follow ADR-0051 | Tech lead | Resolved: explicit dev-only hooks, frame-stage/even-tick bounds, finite float round trips, transient FrameIndex and defined script endpoints. |
| 9 | Fixture pseudo-API differs from implemented boot/session lifecycle | Use existing canonical services and test-only boot access | Tech lead | Resolved TST-003: explicit Bootstrap after Boot scene load, existing IGameFlow.NewGameAsync and SceneFlow sandbox; null-device renderer readiness bypass only, hooks already supplied by FND-005, splash deferred to HUD-003. |

| 10 | Whole-voxel memory ownership and integrated marker scope differ from the original model | Report measured owned capacities and explicitly identify unavailable overhead | Tech lead | Resolved TST-004: sum inclusive markers of every updating Game.Voxel system plus Entities Graphics, using the production spawn/mesh/collider pipeline. The two disabled presentation-queue owners do not execute updates. Memory records ECS chunks and external buffer capacities, mesh GPU/native pools and batches, queues, collider containers and unique live/retired blobs after source eviction, over 300 frames including four damage buckets. Independent jobs are completed before memory inspection, separately from timing runs. Shared content, targets, engine/driver heaps and private scratch not exposed by owners remain excluded; a below-budget accounted peak is inconclusive for total memory. Missing total allocation measurement and numeric misses are performance deferred under ADR-0042; no tuning or progression block. |

## 11. References

- 05-testing §2–§4, 06 (all gates), 07-implementation-traps (determinism at 30/60/144 FPS, worker counts).
- Unity Entities `DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups`, `RateUtils.FixedRateSimpleManager`/`FixedRateCatchUpManager`, `World.SetTime`; Unity Performance Testing package; `ProfilerRecorder`; `Time.captureDeltaTime`; `JobsUtility.JobWorkerCount`.
