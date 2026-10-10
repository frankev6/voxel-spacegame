# 06 — Performance budgets

**Status: BINDING.** These numbers are gates. A task that makes a gate fail is not done. If a budget proves impossible, raise it in the ledger *Open questions* with measurements; do not silently relax it.

## Reference machine and settings

- **Reference machine:** the owner's Apple Silicon Mac (M-series), 1920×1080 window, `Default` quality level, Burst on, safety checks **off** for perf measurements (on for normal tests), development build or Editor play mode as stated per gate.
- **Frame target:** 60 FPS = 16.6 ms. Simulation tick 60 Hz.
- Measure with the Unity Performance Testing package (`Game.Tests.PlayMode` → `Performance` category) and `tools/playtest/perf.sh` for live checks. Report median and 95th percentile over at least 300 frames after a 60-frame warm-up.

## Frame budget (whole game)

| Scenario | Main thread | Render thread / GPU | GC alloc |
|---|---|---|---|
| Idle flight in a star system (player + 5 ships + 1 station of 200k cells) | ≤ 6 ms | ≤ 10 ms | 0 B/frame |
| Heavy combat (see combat gates) | ≤ 10 ms median, ≤ 14 ms p95 | ≤ 12 ms | 0 B/frame |
| Menus and editor UI | ≤ 4 ms | ≤ 8 ms | ≤ 1 KB/frame |

## Voxel gates (SPEC-10/11/12)

| Gate | Target | Old prototype for comparison |
|---|---|---|
| 1M-cell static station, idle, in view | ≤ 2 ms main thread for all voxel systems combined | ~140 ms (extrapolated) |
| Memory for a 1M-cell hollow station (cells + masks + meshes + colliders) | ≤ 40 MB | ~300 MB CPU + 112 MB GPU |
| Triangles | ≤ 2 per exposed face at LOD0; LOD1 ≤ 25% of LOD0 | ~66 per voxel |
| Spawn a 1M-cell grid | No frame over 8 ms of voxel work; fully meshed and collidable within 30 frames | 3+ s freeze |
| Remesh one dirty brick (Burst job) | ≤ 60 µs | n/a |
| Rebuild one brick collider (Burst job) | ≤ 150 µs | full-ship rebuild 0.5–1.5 s |
| Ship moving: per-frame transform cost | O(bricks): ≤ 0.5 ms for 2,500 bricks | O(voxels) |

## Combat gates (SPEC-13/20/21/22/23)

| Gate | Target |
|---|---|
| Skirmish: 30 ships × 20k cells, all firing | 60 FPS (main thread ≤ 10 ms median) |
| Projectiles | 5,000 live projectiles: ≤ 1 ms total for step + hits |
| One explosive hit (radius 3 cells) on a 1M-cell grid | ≤ 0.2 ms main thread; ≤ 1 ms total job time including remesh and collider |
| Cut of 2,000 cells including split into a new hulk | ≤ 2 ms amortized; no frame over 8 ms |
| Debris | 50,000 GPU debris particles live at ≤ 1 ms GPU; ≤ 64 chunk bodies live |
| AI | 50 AI ships ≤ 1.5 ms total |
| Swarm (ADR-0017) | 30 ships + 100 small craft (≤ 1 brick each, groups of 10), all fighting: main thread ≤ 10 ms median; AI ≤ 1.5 ms including craft |
| Launch (ADR-0017) | Unstow 20 craft in one tick: no frame over 8 ms |
| Determinism | Same seed + same scripted input → identical cell state hash after 600 ticks, at 30, 60 and 144 FPS |
| Errors | Zero exceptions, zero ECB errors, zero native-container leaks in a 10-minute soak |

## World gates (SPEC-30/31/32)

| Gate | Target |
|---|---|
| Galaxy generation (full galaxy definition) | ≤ 50 ms, Burst |
| Star-system load during jump | Hidden behind ≤ 1.5 s transition; no frame over 33 ms |
| 50-jump soak | Entity count and native memory return to baseline ±1% |
| Origin rebase | ≤ 0.5 ms; no visible pop |

## Save gates (SPEC-05)

| Gate | Target |
|---|---|
| Save capture on main thread | ≤ 5 ms (serialization and compression on a worker) |
| 1M-cell ship in a save | ≤ 80 KB compressed |
| Load a typical save to playable | ≤ 2 s |
| Round trip | Byte-identical re-save of a loaded save (excluding timestamps) |

## Editor gates (SPEC-51)

| Gate | Target |
|---|---|
| 100k-block ship in Blueprint Lab | 60 FPS while editing |
| One place/remove including validation and remesh | ≤ 2 ms main thread |
| Undo/redo | 1,000-step stroke undone and redone exactly |

## Iteration gates (developer experience)

| Gate | Target |
|---|---|
| Enter Play Mode (domain reload off) | ≤ 1 s |
| `tools/compile.sh` (no changes) | ≤ 10 s with the Editor open |
| EditMode test suite | ≤ 60 s |
