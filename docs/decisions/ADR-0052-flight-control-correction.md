# ADR-0052 — Thruster-based flight controls and gentle coasting

**Status:** Accepted (Tech lead, 2026-10-12; implements Frank's explicit control request)

## Context

Frank requested usable reverse, Shift-to-strafe, slight neutral deceleration and handling determined by the Fighter's actual directional thrusters. He explicitly requires implementation in V2's architecture, without copying V1. Live inspection confirms a 24,620 kg Fighter with 180 kN forward thrust, 40 kN reverse and 40 kN per lateral direction. Existing S input reverses at about 1.625 m/s²; neutral forward speed is deliberately preserved by the old spec. Large unachievable assist requests can dominate the allocation objective and spend maneuvering thrust fighting other requested axes.

## Decision

- Add rebindable `Flight/StrafeModifier` (Shift). While held, the sampler adds Turn to Strafe, clamps to [-1,1], and writes Turn=0. W/S and Q/E remain. Move the default Boost binding to Left Ctrl, retaining its binding ID and user overrides. Suppress Boost while the modifier is held so a historical Shift boost override cannot activate both modes. No change to PlayerCommand or replay format.
- Add `CoastDeceleration` to FlightConfig and its definition/factory, default 0.08 per second, finite and nonnegative. Neutral forward target is the capped local speed times `max(0, 1 - CoastDeceleration * fixedDt / LinearAssistGain)`. Existing proportional assist then asks for a gentle proportional braking force. With enough opposing thrust the speed multiplier is 0.9986667 per tick (roughly 8.66 seconds to halve speed). Brake still targets zero. No passive force, invented engine, frame-rate damping or velocity assignment: ships lacking opposing thrusters cannot cancel momentum.
- Before allocation, clamp each requested generalized-force component to the sum of the available actuator bounds on that axis. These are independent outer bounds, not a claim of joint feasibility. Retain the six-sweep bounded solver, actual application points, mass/inertia, gyro and impulse ownership. This prevents impossible target magnitudes from arbitrarily dominating other axes, while asymmetric ships still experience torque.
- Keep grid +Z as the nose and transform every actual thruster force by the ship rotation. Turning does not teleport the velocity vector. Smaller maneuvering thrusters take longer to cancel previous momentum. Prove this using the real Fighter and live controls; no per-ship speed override or content/hull edits.
- Prioritize a stationary-target firing range after this correction, before the rest of M2: real firing, cell damage, updated meshes/colliders and measured sustained-fire frame times; structural breakup follows. AI and galaxy services are not prerequisites for the local demonstration.

## Consequences

Input and FlightConfig public additions are explicit. Existing binding overrides are preserved. Tests asserting indefinite forward coasting change to the owner-requested deceleration; tests isolating conservation can disable coasting explicitly. V1 code is neither copied nor consulted further. Performance targets remain advisory.
