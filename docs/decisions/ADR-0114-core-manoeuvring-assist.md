# ADR-0114 — Core manoeuvring assist and tighter flight assist

**Status:** Accepted (2026-10-14, QA-001, owner direction "fix everything mentioned in your findings")

## Context
Owner playtest, 2026-10-14: "the controls are complete dogshit, they should feel tight and responsive while still using the thrusters calculations, but like it takes me 3 years to go sideways which doesn't match turning the ship, also when i go forward or back I just keep drifting indefinitely".

FLT-013 measured why on the starter `ship_fighter`:
- 7.2 m/s² forward, but 1.55 m/s² reverse and strafe;
- reaching the 72 m/s strafe cap takes about 46 s;
- reversing out of 120 m/s takes about 78 s;
- coasting removes only 8% of speed per second (ADR-0052).

Ledger Q125, the flight balance sign-off, stayed pending while the work moved on. SPEC-20 decision 6 already gives every live ship a core reaction torque, so that small ships turn on the core alone. There was no linear counterpart.

## Decision
1. **Core manoeuvring assist.** Every live ship's core provides a planar force at the centre of mass along local X and Z, in both directions:
   - force per axis: `CoreAssistForce = min(mass × CoreLinearAccel, CoreMaxLinearForce)`, scaled by the Thrust stat multiplier;
   - in the allocator: two extra actuators after the gyro, in fixed order (strafe, then forward), with throttle −1..1, warm-started from `FlightState.CoreAssist`;
   - application: one linear impulse per tick.

   Thrusters still push from their blocks and still decide each ship's top acceleration, its turning off-centre, and how much it degrades when damaged. The assist is capped, so large ships still need real thrusters (pillar 2, as decision 6 does for yaw).
2. **Stats.** `PropulsionStats` forward, reverse and strafe acceleration include the assist force, so AI braking estimates stay truthful.
3. **Coast.** Releasing W/S decays the forward target at `CoastDeceleration = 1/s` (was 0.08/s); real thrusters and the assist still bound the deceleration. This supersedes ADR-0052's "gentle" default.
4. **Boost.** Boost throttle 1.6 (was 1.25) and forward cap ×1.6 (was ×1.5). Plume intensity may exceed 1 while boosting, so the overdrive is visible. The camera widens its FOV by 7° while boost is held (SPEC-62, reduce motion disables it).
5. **Production values** (`config_flight`): `coreLinearAccel` 18 m/s², `coreMaxLinearForce` 600 kN. `FlightConfig.Default` and the definition defaults keep the assist at 0, so isolated physics fixtures still measure thrusters alone.

## Consequences
- The Fighter now has about 25 m/s² forward and about 19.6 m/s² reverse and strafe. It stops from full speed in about 6 s and reaches strafe speed in about 3.5 s.
- Tests that measured thrusters alone against the production config (FLT-013) now include the assist.
- "Losing both reverse thrusters removes reverse capability" becomes "only the core assist remains".
- AI ships get the same assist through the shared propulsion job.
- Balance numbers in Q125 are re-measured by the owner's play, not by more benchmark loops.

## Alternatives considered
- **Rebalance thruster rows and blueprints only.** Q124 showed shared row scaling cannot reconcile the blueprints' mass ratios. It also needs lateral thrusters on every small ship before the owner can judge feel.
- **Velocity-setting arcade flight.** This breaks SPEC-20 decision 2 and the owner's "still using the thrusters calculations".
- **Lower the strafe cap instead.** This makes strafing slower, not tighter.
