# ADR-0123 — Experimental 3D flight for the player ship

**Status:** Accepted (2026-11-04). Owner: "Accept as written", with open question 1 answered "Chase camera, flight-sim style". Amends ADR-0003 for an opt-in, dev-only mode; ADR-0003 stays the game's default.

> **Amended 2026-11-08 (FLT-017).** Owner: "give the other ships 3d controls also when experimental flight3d is toggled on? also make flight3d toggled to on by default". See *Amendment 1* at the end; it replaces the player-only parts of decisions 1, 3 and 7.

## Context
The owner asked (2026-11-04) for "an experimental mode, toggleable in console, which unlocks 3d travel": two buttons for up and down thrust, a toggle that makes the ship "freely rotate to look at the mouse" while it "maintain[s] the movement feel of the game, so perhaps there should be an adjustable lag metric", and a toggle that makes the ship face the selected target. The owner chose the player-only layer: only the player's ship leaves the plane; AI, hazards and radar stay planar.

Today two separate locks hold ships to the plane:
- the `PlanarConstraint` tag pulls Y position and Y velocity back to 0 every tick (`PlanarImpulseJob`), and impulses on tagged bodies drop linear Y and angular X/Z;
- the spawn flag `GridSpawnFlags.PlanarInertia` keeps only the Y inertia (`GridMassMath`).

The thruster allocator is 2D (local X force, local Z force, yaw torque). Thrusters pointing up or down are dropped when a ship is built. Ships track a turn rate only; no system turns a ship to face a direction. Origin rebasing, cruise, spawn and teleport rebuild yaw-only rotations.

## Decision
1. **The switch.**
   - **`Flight3D`:** a per-ship enableable component in `Game.Data.Components`, next to `PlanarConstraint`, because the voxel mass and impulse code that honours it cannot see Simulation types. `FlightSetup` adds it to player ships only. Its sole writer is `Flight3DModeSystem`.
   - **`Flight3DRequest { On }`:** the switch itself. The dev command `flight3d [on|off]` (SPEC-70) writes it on the main thread between world updates (a SPEC-70 §5.4 write).
   - **On** enables `Flight3D` at once. **Off** keeps it enabled until the ship has levelled (decision 2), then disables it.
   - The mode is dev-only: nothing outside `DevRuntime` writes the request, and it is not saved. Release builds have no way to enable it.
2. **The plane locks honour it:**
   - `PlanarImpulseJob` and both impulse-flattening jobs skip bodies whose `Flight3D` is enabled;
   - `GridMassMath` builds the full inertia tensor for them. `GridMassUpdateSystem`, the existing `PhysicsMass` writer, rebuilds the mass when the enabled state changes;
   - `SpinLimitSystem` caps the whole angular velocity for them, not only the yaw.
   - **Turning the mode off:** the ship first levels (target pitch and roll 0), then the inertia lock returns, and the plane pull takes the ship back to Y = 0 at the usual `PlaneCorrection` rate.
3. **Commands.** `ShipCommand` and `PlayerCommand` gain `float Lift` (−1..1, up positive). `ShipCommandFlags` gains `FaceAim` and `FaceTarget`, the player's two toggles. The co-op format (`CoopNet`) and the replay script (`PlayerCommandScript`) carry the new field and flags, with a version bump. AI writes `Lift = 0` and no facing flags.
4. **Input (SPEC-03).** New Flight actions:
   - `Lift`, a 1D axis: Y up, H down;
   - `FaceAimToggle`: B;
   - `FaceTargetToggle`: P.

   All are rebindable. Toggles are latched edges, as Cruise and AutofireToggle are. The two toggles are independent (owner, 2026-11-04: "toggling off P shouldn't also toggle off B"); with both on, FaceTarget wins while a target is selected. With the mode off, they do nothing.
5. **Movement while `Flight3D` is enabled** (`PropulsionJob` branch):
   - **Horizontal flight:** forward, reverse, strafe and yaw keep the existing allocator and caps in the ship's own frame, so the planar feel is unchanged.
   - **Vertical flight:** a vertical assist force along the ship's up axis drives local vertical speed toward `Lift × MaxForwardSpeed × VerticalSpeedFraction`. It is `mass × VerticalAccel`, also capped by `CoreMaxLinearForce` when that is above 0 (its default 0 means no core assist, not "no vertical flight"). With no Lift input it brakes vertical speed at the coast rate. Real up/down thrusters stay out of the allocator in this mode (a later 6-DOF step).
   - **Thrusters:** they push through the centre of mass apart from their yaw torque. The attitude belongs to the controller, so an off-centre engine never tumbles the ship. Hits still pitch and roll it, and the controller recovers.
   - **Attitude:** a heading controller sets target yaw and pitch rates from the angle to a desired forward direction:

     `rate = clamp(angle × HeadingResponse, ±MaxYawRateDeg / ±MaxPitchRateDeg)`

     Those rates are reached through the existing turning assist (`AngularAssistGain`) and gyro limits (`CoreYawAccelDeg`, `CoreMaxYawTorque`), so turning keeps the game's weight. `HeadingResponse` is the "lag" the owner asked for: lower is lazier and swings wider, higher is snappier. Roll is always driven to level. Pitch is clamped to ±`MaxPitchDeg` (default 75°), so the ship never flips.
   - **What the ship faces:**
     - FaceTarget on: the selected `ShipTarget`;
     - else FaceAim on: the aim direction (decision 6);
     - else the current heading, steered by Turn as today, with pitch held level.
   - **New `FlightConfig` values:** `VerticalSpeedFraction` (0.5), `VerticalAccel` (20 m/s²), `HeadingResponse` (3 /s), `MaxPitchDeg` (75°), `MaxPitchRateDeg` (90°/s). They appear on the control sheet's Flight configs tab.
6. **Aim in 3D** (open question 1, answered):
   - **FaceAim on:** a chase camera, flight-sim style.
     - The cursor is locked. The mouse moves a free-aim direction (yaw and pitch, pitch clamped to ±`MaxPitchDeg`), scaled by the player's mouse sensitivity.
     - The camera sits behind and above that direction and looks along it, with a centre reticle.
     - `AimWorld` is the first collider hit along the centre ray, else a point 1,000 m along it.
     - The ship's nose chases the free-aim direction through the heading controller (decision 5), so `HeadingResponse` is the lag between where you look and where the ship points.
     - Menus and the console unlock the cursor and pause the free aim. Turning FaceAim off returns the normal camera.
   - **FaceAim off:** the existing camera. The mouse ray first hits colliders, as today; without a hit, the fallback plane is the horizontal plane at the ship's height, not Y = 0.
7. **Systems that assume the plane, and what they do in this mode:**
   - **Cruise:** refused while `Flight3D` is enabled. `CruiseSystem` reports the block reason "3D flight".
   - **Origin rebasing:** shifts positions only, so a 3D ship keeps its attitude. Placing the player on arrival in a star system still sets a level, yaw-only rotation, as do spawn and teleport. The mode stays on.
   - **Unchanged:** AI steering, hazards and `WorldZone` (infinite vertical prisms), the radar (XZ), and the Hollow dressing. An off-plane player is still found by the spatial hash (XZ columns, 3D tests) and can hit and be hit. Turret elevation limits may stop some shots until the ship pitches.

## Consequences
- **Easier:** trying vertical play without committing the game to it. The plane locks gain a clean per-ship opt-out that a full 6-DOF step can reuse.
- **Harder:**
  - `ShipCommand` and two binary formats change (co-op and replay) under a version bump;
  - `PropulsionJob` gains a second branch to keep correct;
  - AI cannot chase vertically, and the Hollow's roof and floor can be clipped.
- **Changes:**
  - SPEC-20: §4 `Flight3D`, `FlightConfig` fields, §5 the 3D branch and heading controller;
  - SPEC-12: inertia;
  - SPEC-62: the free-aim chase camera (CAM-013);
  - SPEC-03: three actions;
  - SPEC-70: the `flight3d` command and its §5.4 row;
  - SPEC-32: cruise refusal;
  - SPEC-90: the co-op format;
  - SPEC-71: the replay script.
- **Tests:**
  - the toggle round trip (off-plane, then back on the plane, level, with the inertia restored);
  - Lift reaches its speed cap;
  - FaceTarget converges within the `HeadingResponse` time with no overshoot past `MaxPitchDeg`;
  - the plane locks still hold every ship without `Flight3D`;
  - replay determinism with Lift.

## Alternatives considered
- **A dev-only kinematic hack** (set velocity and rotation directly): fastest, but breaks the impulse-only velocity rule (architecture §8) and loses the flight feel. Rejected by the owner.
- **Full 6-DOF:** allocator over six axes with real thrusters, and 3D AI, hazards and radar. It is the eventual real version, but it changes the vision's scope and costs weeks. Rejected for now by the owner.

## Amendment 1 (2026-11-08): every ship, on by default
Owner, 2026-11-08: "give the other ships 3d controls also when experimental flight3d is toggled on? also make flight3d toggled to on by default".

1. **One world switch.** `Flight3DMode { On }` (a singleton, `Game.Simulation.Flight`) replaces the per-ship `Flight3DRequest`.
   - `Flight3DModeSystem` creates it **on**; after that its only writer is the dev command `flight3d` (SPEC-70 §5.4).
   - It counts as off unless `SimRole` is Solo, so co-op stays planar (ADR-0003) whatever the switch says.
   - `Flight3DModeSystem` is still the sole writer of each ship's `Flight3D` bit: on at once, off once that ship has levelled.
   - Not saved. Release builds have no dev console, so there it stays on; returning ADR-0003's planar default to release builds is an owner call.
2. **Every flying ship.** `FlightSetup` adds `Flight3D` to every flying profile (player, NPC, small craft, dummy), not only the player's. Stations stay planar.
3. **AI uses Lift** (supersedes "AI writes `Lift = 0`"). `AiSteerJob` keeps its planar steering and keeps the nose level (no facing flags). With `Flight3D` enabled it sets Lift toward a height:
   - while engaging with a valid aim, the target's height (the aim point's Y);
   - otherwise its goal's height: 0 for patrol, return and flee (the brain's goals are on the plane), the leader's height plus 0 for formation members.
   - Lift closes at the vertical speed cap and brakes in time at half `VerticalAccel` (`AiSteerJob.Lift`), within `HeightTolerance` (0.5 m).
4. **Consequences.**
   - Cruise is refused while the mode is on (decision 7), so with the default every ship flies without cruise until `flight3d off`.
   - The radar, hazards and `WorldZone` stay planar (decision 7); AI ships now leave the plane to follow the player.
   - Racers (`RacerPilotSystem`) write `Lift = 0`; the vertical assist holds them at their height.
   - A hulk keeps the mode it died in; with the switch off, a tumbling hulk never levels and stays off the plane.
   - AI turrets aim in 3D already; fixed forward guns on AI ships are not pitched toward the target (a later step).

## Open questions
| # | Question | Default | Decider |
|---|---|---|---|
| 2 | Release builds have no console, so the on-by-default switch cannot be turned off there. Keep it on in release builds? | On everywhere while the mode is experimental | Owner |
| 1 | The camera looks down at the ship from at least 20°. How does FaceAim get pitch from the mouse? | Pitch from what the cursor is on (alternatives: a chase camera; pitch from the cursor's screen height) | Owner — **answered 2026-11-04: "Chase camera, flight-sim style"** (decision 6) |
