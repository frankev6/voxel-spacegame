# ADR-0120 — The engine line: spool, boost headroom, verniers and torches

**Status:** Accepted (2026-10-17, owner direction; the values are defaults to tune by play)

## Context
Owner, 2026-10-17, on the first racer build: "the real answer is you need to also make new thruster engines, maybe some go from 0-100% faster but have less max thrust so theres a give and take, but also it makes sense for thrusters meant to rotate rather than be the main thrust, and maybe larger engines too, for the bigger ships."

Before this decision every thruster row was the same engine at different sizes:
- about 10 kN per cell, except the Medium row at 4 kN per cell;
- every engine reached any throttle in one tick;
- every engine overdrove to the same `FlightConfig.BoostThrottle` (1.6).

Nothing traded responsiveness against power. Ships over about 33 t had the core assist (ADR-0114, 600 kN cap) as their main source of thrust, so engine choice hardly mattered. The Battering Ram reached 25 m/s after 4 s.

## Decision
1. **Spool.** `ThrusterParams` gains `SpoolUpSeconds` and `SpoolDownSeconds`: the time from idle to full throttle and back. Each tick the allocator bounds an engine's throttle to within `dt / SpoolUpSeconds` above, and `dt / SpoolDownSeconds` below, last tick's throttle. A slow engine therefore keeps pushing after release, and the core assist and faster engines make up the difference. A value of 0 means instant, so rows authored before this decision keep their behaviour until they are given a value.
2. **Boost headroom.** `ThrusterParams.BoostThrottle` is the engine's own throttle ceiling while boosting. 0 means `FlightConfig.BoostThrottle`.
3. **The line.** Thrust density, spool and boost headroom are traded against each other:

   | Family | Blocks | Thrust per cell | Spool up / down | Boost ceiling | For |
   |---|---|---|---|---|---|
   | Vernier | `block_thruster_vernier` (1 cell) | 10 kN | 0.04 s / 0.04 s | 1.0 (no overdrive) | Turning and strafing, mounted far from the centre of mass |
   | Sprint | `block_engine_sprint_small` (1×1×2), `block_engine_sprint_medium` (2×2×3) | 8 kN | 0.08–0.12 s / 0.06–0.08 s | 2.0 | Racers and light craft |
   | Standard | the existing `block_thruster_small/medium/large` and the Xeno fin | unchanged | 0.25 s / 0.2 s | 1.6 (config) | General use |
   | Torch | `block_engine_torch_large` (3×3×4), `block_engine_torch_capital` (5×5×7) | about 17–18 kN | 1.5 s / 1.0 s and 2.5 s / 1.6 s | 1.3 and 1.2 | Heavy ships and capital hulls |

   Torches are heavier per cell (40–45 kg against 16–30 kg) and need the longest exhaust clearance (6 and 10 cells).
4. **Generated content.** `EngineContentBuilder` (Editor) creates the five blocks and their lathed nozzle meshes from code (own work, procedural), and applies the Standard spool to the existing rows. Re-running it rebuilds them in place.
5. **No new power draw in this pass.** Every engine still draws 0. A torch drawing reactor power is the next trade to try once the power budget is played.

## Consequences
- `FlightThruster` grows from 44 to 56 bytes, adding `SpoolUp`, `SpoolDown` and `BoostMax`. It stays unmanaged and its only writer is still `PropulsionSystem`.
- Allocation is unchanged. Spool only narrows each actuator's [Min, Max] bounds for the tick.
- Existing ships respond a quarter-second slower on their engines. The core assist still responds at once, so handling changes little.
- `PropulsionStats` still reports full thrust. AI braking estimates ignore spool, and torch-driven AI may overshoot slightly. Revisit this if it shows in play.
- The racers use the line: Darts fly sprint engines and verniers; Rams fly torches with sprint engines for correction (SPEC-92 RACE-013).

## Alternatives considered
- **Spool as a ship-wide stat.** This is simpler, but it cannot mix a torch with a sprint engine on one hull, and that mix is the point.
- **Raise every thrust row.** This makes engine choice matter on big ships, but it removes the trade. ADR-0114's Q124 also showed that shared-row scaling cannot reconcile the legacy blueprints.
- **Multi-nozzle RCS blocks.** These need per-nozzle actuators and per-nozzle plumes. Single-cell verniers give the same capability with today's renderer.
