# ADR-0119 — World mechanisms: moving structures, mechanisms, portals and the wider hazard set

**Status:** Proposed (2026-10-17), with the SPEC-91 and SPEC-92 amendments for the owner's review.

## Context
The owner reviewed the racing mechanics sheet (RACE-002) on 2026-10-17: "i love all the mechanics except the grapple pylon (lets scratch that), and the "places that change" is less of a mechanic and more a design consideration using the other mechanics". The day before: "these mechanics should general and for me to be able to use them in real world exploration too".

Nine mechanics stay:
1. gravity wells;
2. moving structures (rotating arms, rails, drifting wreck pieces);
3. blast doors and switches;
4. engine wash;
5. the mine family (proximity, repulsor, EMP, seeker, gravity);
6. gauntlet turrets;
7. solar flares and reactor pulses, with shadows;
8. lane-gate portals;
9. energy canisters.

ADR-0118 and SPEC-91 already cover zones (accelerate, drag, repel, gate, bounds), proximity mines and hit force. Three of the new mechanics change shared contracts:
- **Moving structures:** a new writer of `PhysicsVelocity` on kinematic bodies.
- **Mechanisms:** a new decider that drives other features.
- **Portals:** a new writer of a root's `LocalTransform`. Only `OriginRebaseSystem` writes it today (architecture §8).

## Decision
1. **Moving structures are kinematic grids with a `Mover`.**
   - A grid of any kind gets a `Mover` (station, device or wreck piece).
   - Its pose is a pure function of a path and a clock: `Rotate` about a hub at a rate; `Rail` along a polyline at a speed (loop or ping-pong); `Drift`, a linear and angular velocity; or `Toggle` between two poses over a duration (doors).
   - `MoverSystem` (`GameplayPrePhysicsGroup`, after `BodyOnRailsSystem`) sets each mover's kinematic `PhysicsVelocity` so the body reaches the path pose on the next tick. Unity Physics then moves it and pushes dynamic ships out of its way.
   - This makes `MoverSystem` a `PhysicsVelocity` writer **for kinematic movers only** (architecture §8 row amended). Dynamic bodies never get a `Mover`.
   - Collisions with movers raise ram damage through `ImpactEventSystem` as usual.
   - Movers are authority deciders. Guests receive their poses through the existing snapshots, as for any replicated grid.
2. **Mechanisms connect triggers to actions; they are data, not code per place.**
   - A `MechanismDefinition` (content) lists rules: *when* a trigger fires, *do* actions.
   - **Triggers:** a `Switch` block is damaged, a block instance is destroyed, a zone or gate is entered, a timer (period and offset), a race lap reached, or a narrative condition (`ICondition`).
   - **Actions:** play, reverse or set the speed of a mover; enable or disable a zone, or set its period; arm or disarm a group of mines; set or clear a turret group's target policy; jam a portal; spawn a hazard group; apply a narrative effect.
   - `MechanismSystem` (authority) evaluates triggers and writes action records. Each action is applied by the system that owns its state: `MoverSystem`, `ZoneSystem`, `MineSystem`, AI targeting and the portal applier.
   - The new block module `Switch` (`BlockModuleId` 11) marks shootable switch blocks.
   - Bulkheads need no mechanism: they are plain destructible voxels, and what is shot away stays away.
3. **Portals move roots through a transit request.**
   - A portal pair is two `Gate`-kind zones with a `Portal` link. A crossing on the authority (`PortalDecideSystem`) writes `PortalTransit { Body, FromPortal, ToPortal, Tick }` and jams the pair for its `JamSeconds`.
   - `PortalTransitSystem` runs on every machine, in `GameplayPrePhysicsGroup` before `PropulsionSystem`. It applies each transit: the root's `LocalTransform` is moved and rotated from the entry frame to the exit frame, its velocity is rotated the same way, and graphical smoothing is reset.
   - It is the second writer of root `LocalTransform`, for portal transits only (architecture §8 row amended). `OriginRebaseSystem` stays the only floating-origin shifter.
   - A guest applies transits of its own predicted ship from the replicated record, and its reconciliation records are moved by the same transform, so the jump is not "corrected" back.
4. **The zone set grows by two kinds and a schedule** (SPEC-91 amendment):
   - **`Attract`** (gravity): acceleration `GM / max(r, rmin)²` toward the zone centre, up to its radius.
   - **`Pulse`**: periodic damage and shield drain from a point source (a reactor) or a direction (a star), skipped for roots in shadow. A root is in shadow when a physics ray from the source to its centre of mass hits another grid first. That is one raycast per root per pulse tick, on the authority only.
   - **`ZoneSchedule`** (period, on-time, phase, enabled) lets any zone switch on and off. Mechanisms set it.
5. **Engine wash is derived from thrusters, not authored.** `EngineWashSystem` (every machine) reads each firing thruster's `ThrusterState.Intensity` and `MaxForce`. It pushes roots inside a cone behind the thruster (never the ship that owns it), through `DamageStreams.Impulses` with `ProducerKind.Hazard`. Heat damage is decided on the authority only.
6. **Mines get payloads.** `ProximityFuseParams.Payload` is `Blast`, `Repulse` (impulse only), `Emp`, `Gravity` (an `Attract` zone for a few seconds) or `None`:
   - `Emp` posts an `InhibitRequest` on thrusters for its seconds, through SPEC-22's channel.
   - `SeekSpeed` > 0 makes an armed mine drift toward its nearest valid target by impulses (seekers).
7. **Canisters are devices with a `Pickup` module** (`BlockModuleId` 12). A ship root within reach is granted energy through a new `EnergyGrantRequest` channel, owned by SPEC-22's `PowerAllocationSystem`. The canister then despawns. Canisters carry an `Explosive` block, so shooting one sets it off.
8. **Gauntlet turrets use a target override.** `TargetOverride { Entity Target; byte Policy }` on a turret group's root is written only by its decider: `RaceSystem` (the leader), a mechanism action, or later faction logic. AI turret targeting (SPEC-23 AI-009) prefers the override when it is valid.
9. **Places change through these mechanisms, never through bespoke code.** This is the owner's design rule: each place lists what changes and when (a timer, a lap, a trigger) as mechanism rules.
10. **Capital wrecks are hollow shells at 2 m cells.** Per-grid `CellSize` already exists. Wreck blueprints use 2 m cells and hull plating two cells thick, about 600k cells at most each, to keep a 1.5 km dreadnought within SPEC-11/12 budgets. Racers and small devices stay at 0.5 m.
11. **The grapple tether is cut** (owner). The Salvage Tug racer keeps its crane as an armoured side arm.

## Consequences
- **Architecture §8:**
  - `PhysicsVelocity` gains `MoverSystem` (kinematic movers only);
  - root `LocalTransform` gains `PortalTransitSystem` (portal transits only);
  - new rows for `Mover`, mechanism state, `PortalTransit`, `TargetOverride` and `EnergyGrantRequest`.
- **New contracts:**
  - appended `BlockModuleId` values `Switch` = 11 and `Pickup` = 12;
  - appended `ZoneEffectKind` values `Attract` = 5 and `Pulse` = 6;
  - `ProducerKind` stays as it is (`Hazard` covers wash, pulse and mine payloads).
- **Co-op:** mechanism and portal state, schedules and target overrides travel as compact replicated state. SPEC-91 HAZ-010 grows to cover them.
- **Every one of these systems is general** and lives in `Game.Simulation.Hazards` or the new `Game.Simulation.Mechanisms`. Tracks only place and configure them.

## Alternatives rejected
- **Moving structures as dynamic bodies driven by thrust:** they could be knocked off their path, and they would be expensive to hold steady.
- **Doors as zones that block passage:** they are not voxels, so they cannot be shot or rammed.
- **Portals as two linked zones that push:** that cannot move a ship a kilometre in one tick.
- **Per-place scripts (C# per track):** this is exactly the bespoke code that ADR-0117 and the owner's "general" rule exclude.
