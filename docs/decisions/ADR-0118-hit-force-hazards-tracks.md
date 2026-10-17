# ADR-0118 — Hit force, world hazards and race tracks

**Status:** Accepted (owner, 2026-10-16), with SPEC-91 and SPEC-92 and all their open-question defaults: "Approved with the defaults. Merge the specs to main, then start the track layout options and racer silhouettes (RACE-002, RACE-003) and HAZ-001."

## Context
The owner, 2026-10-16, after inviting a second developer (Daniel) to test co-op: "make a racing minigame where we can also shoot eachother, crash into boundaries, have boost sections, interesting looping track with multiple paths and shortcuts, traps like mines in space (all of these things will get repurposed for other parts of the game, this is not just for racing, so build the foundations as well, like for example mines will be a normal thing in the actual game, and more of these). and two New ships purpose built to be good racers". And: "shooting not only should havve a small recoil on the ship that shot it, but also a clear force against the ship that got shot, obvisouly based on mass vs impact force and explosion of the shot. this adds "umph" to the fight, but also for racing, you can redirect people, obstacles or yourself with well placed shots". He asked for the full specs before any code.

What the code does today:
- **Knockback exists but cannot be felt.** Every hit carries `WorldImpulse = direction × impulsePerHit` (SPEC-21), and blasts push by `damage × ExplosionImpulsePerDamage × falloff²`. `DamageResolveSystem` accumulates them with their torque into `PendingImpulse`, and `ImpulseApplySystem` applies them a tick later (SPEC-13 decision 13). Content uses about 2 N·s per damage point. Cells weigh 25 kg, so a mid-size ship is tens of tonnes, and a plasma hit (120 N·s) changes its speed by millimetres per second.
- **Flight assist erases any push.** The command is a target velocity and yaw rate (SPEC-20 decision 3). The core assist alone gives about 18 m/s² (ADR-0114), so even a large push is cancelled within a fraction of a second, and a spin is stopped at once.
- **There is no recoil.** `WeaponFireSystem` spawns shots and does nothing to the shooter's body.
- **There are no world hazards or volumes.** Nothing like mines, boost sections or force walls exists, and no trigger-volume concept exists. The only course is the flight pad's four rings, which are managed playground code.
- **Every machine already applies impulses.** `ImpulseApplySystem` runs on every role (ADR-0117 NET-014). Damage, and therefore damage knockback, is decided on the authority. A guest's own ship is a predicted dynamic body (ADR-0116 §13), and the guest's replicas are kinematic.

## Decision

### Hit force
1. **Shots carry momentum.** `WeaponDefinition.impulsePerHit` is replaced by:
   - `projectileMass` (kg) for projectiles and missiles: per-pellet momentum `p = projectileMass × projectileSpeed`;
   - `beamForce` (N) for beams, delivered per pulse as `beamForce × pulse seconds`.

   A hit pushes the target with `p × WeaponTuning.KnockbackScale` along the shot direction at the hit point. The existing `DamageEvent.WorldImpulse` path carries it, with its torque, so an off-centre hit spins the target. The target's response is `Δv = J / mass` and `Δω = I⁻¹ (r × J)`, so light ships fly and heavy ships barely move, as the owner asked. Blasts keep their falloff-weighted push and are retuned in the same pass.
2. **Recoil.** Each fired round posts one `ImpulseRequest` on the shooter's root at the muzzle point, `−p × WeaponTuning.RecoilScale` along the barrel, with the new `ProducerKind.Recoil`. It rides the channel `ImpactEventSystem`'s impulse half already owns (architecture §8, ADR-0016). It is applied in the same tick, because `WeaponFireSystem` runs before `ImpulseApplySystem`. A turret mounted off-centre therefore also twists its ship a little.
3. **Flight assist carries external pushes instead of erasing them.** The binding change to SPEC-20:
   - `ImpulseApplySystem` records the velocity change it applied to each root this tick in a new Voxel component `ExternalPush { float3 LinearDv; float YawDv; uint Tick }`. Only that system writes it. It covers every impulse, from damage knockback, requests, recoil and zones alike.
   - `PropulsionJob` folds last tick's `ExternalPush` into `FlightState.Carry` (a ship-local planar velocity) and `FlightState.CarryYaw`. It adds them to the commanded targets. Assist then holds the pushed motion instead of fighting it.
   - Carry decays toward zero at `FlightConfig.CarryDecel` (m/s²) and `CarryYawDecel` (rad/s²). Braking uses `CarryBrakeDecel`. Each tick carry is clamped to the ship's actual velocity in the same direction, so a ship that hits a wall is never driven back into it.

   A shot from behind becomes a short forward surge, a shot to the bow a spin, and a boost section a long surge that bleeds off. The thrusters, the allocator and the core assist are unchanged.
4. **Every pusher posts impulses.** Zones, mines, walls and recoil all use `DamageStreams.Impulses`. No new `PhysicsVelocity` writer is added.

### World hazards (SPEC-91, `Game.Simulation.Hazards`)
5. **Zones are analytic planar shapes, not physics sensors.** `WorldZone` is an entity with a shape (oriented box, circle, or a capsule chain along a polyline), a yaw and a list of `ZoneEffect` rows. It has five kinds:
   - `Accelerate`: a boost section. It adds velocity along the zone direction up to a target speed.
   - `Drag`: a slow field.
   - `Repel`: a force wall. It is a spring and damper on the penetration, plus contact damage.
   - `Gate`: a crossing line, used for race checkpoints and later for traffic lanes and gate approaches.
   - `Bounds`: inside or outside an area.

   Membership is tested against each grid root's centre of mass plus its bounding radius, through `SpatialHash` queries. A gate crossing is tested on the segment from last tick's centre of mass to this tick's, so fast ships never tunnel.

   Reason: tests stay deterministic and cheap, a zone needs no new collision layer, and the result is a pure function of poses, so ADR-0117 lets it run on every machine.
6. **Zone effects that push run on every machine; zone effects that decide run on the authority.**
   - Pushes run everywhere: `Accelerate`, `Drag` and `Repel`'s spring. A guest's own predicted ship therefore feels a boost section without waiting a round trip. Replicas are kinematic and ignore impulses.
   - Decisions run on the authority only: `Repel` contact damage, and gate and bounds consumers (race progress).
7. **Mines are small voxel grids with a fuse block.**
   - New `GridKind.Device`, appended. It marks small autonomous world objects: mines now, beacons and turret pods later. A device has no core and no AI, cannot become a hulk, and is despawned when its last functional block is gone.
   - New block module `ProximityFuse` (`BlockModuleId` value 10, appended) with arm delay, trigger radius, fuse delay, a trigger filter and a blast. The blast reuses the existing `Explosive` module's cook-off, so a mine that is shot can chain-react through its field.
   - A mine is real voxels: it can be shot apart, pushed by shots and rammed, and it replicates with the existing grid stream.
   - `MineSystem` (authority) decides arm, trigger and detonate, and writes `ExplosionRequest`s. Mine state is replicated as compact state. Blink and beep feedback is an applier on every machine.
   - Anchored mines are held by a capped spring impulse toward their anchor, so a field keeps its shape after a shove.
8. **Hazards are placed through one spawn API** (`HazardSpawn`: zones and mine fields from definitions with a pose and a seed). Race tracks, encounters, star-system locations and the playground all use it. Mine field scatter uses `RngStreams` with a new `RngPurpose.HazardScatter`.

### Race tracks and race mode (SPEC-92, `Game.Galaxy.Racing`)
9. **A track is content.** `TrackDefinition` holds:
   - a gate graph whose branches allow multiple paths and shortcuts, with required gates marking what every valid lap must pass;
   - pieces (blueprints with poses: walls, pylons, breakable barricades, drifting obstacles);
   - zones and mine fields;
   - a start grid;
   - an outer bounds polyline, built as a `Repel` wall.

   `TrackSpawnService` spawns it through `GridSpawnRequest` and `HazardSpawn`. Ships fly on a plane (SPEC-20 decision 9), so tracks are planar: a crossing is a level intersection, and there are no vertical loops.
10. **Race progress is a decider on the authority.** `RaceSystem` decides phase, countdown, laps, gates, finish order and respawn requests. `RaceState` is replicated as compact state. The race HUD and gate visuals are appliers on every machine.

    During the countdown, `RaceHold` is an enableable tag on each pilot's root, written only by `RaceSystem`. `PilotCommandConversion` zeroes thrust, turn and fire while it is set. It is a per-pilot hold, separate from `PlayerControlLock`, which keeps its single writer (`TravelService`).
11. **Respawn replaces a pilot's ship.** It is triggered by ship death, by staying out of bounds for 5 s, or by a manual reset. The new ship spawns at the last passed gate's respawn point and carries the same `Pilot { Slot }`. The old hulk stays on the track as an obstacle until the race ends.

### Contract changes
12. These are the binding contract changes this ADR authorises:
    - architecture §6.3: `GridKind.Device` and the `ExternalPush` component;
    - architecture §6.2: `BlockModuleId.ProximityFuse` and `ProducerKind.Recoil`, both appended;
    - architecture §6.4: `WorldZone` and the zone effect kinds, `RaceHold`, and `FlightState.Carry`;
    - architecture §8: `ExternalPush` (writer `ImpulseApplySystem`), `MineState` (writer `MineSystem`), `RaceState` and `RaceHold` (writer `RaceSystem`);
    - SPEC-21: `projectileMass` and `beamForce` replace `impulsePerHit`.

    New pipeline placements:
    - `ZoneSystem` in `GameplayPostPhysicsGroup`, after `SpatialHashBuildSystem` and before `ImpulseApplySystem`;
    - `MineSystem`, after `ZoneSystem` and before `ExplosionSystem`;
    - `RaceSystem` in `GameplayPostPhysicsGroup`, after `ZoneSystem`.

## Consequences
- **Combat feel changes everywhere,** not only in races. AI steering, the turret lead and the ram damage tests must hold with carried velocity. SPEC-91 retunes all eleven weapons against stated feel targets on reference ships. The owner judges the feel in the playground; that judgement never blocks a task.
- **One more component on every grid root** (`ExternalPush`, 20 bytes) and two fields in `FlightState`.
- **Co-op knockback on a guest's own ship arrives one round trip late** until SPEC-91 HAZ-010 echoes impulses applied to pilot ships. A guest's own recoil and zone pushes are predicted at once, because `WeaponFireSystem` and `ZoneSystem` run on every machine.
- **This work is new M4-depth work.** The owner's 2026-10-14 sequencing note in ROADMAP put new M4 features after M1–M3 verification. The owner's request on 2026-10-16 directs this track explicitly. It runs beside the open M1–M3 tasks and does not replace them.

## Alternatives rejected
- **Scale `impulsePerHit` up and leave assist as it is.** Assist cancels any push within a few ticks, so the owner's "umph" would be a jitter, or would need pushes so large they break rams and wrecks.
- **Disable assist for a moment after a hit (a "stagger" timer).** Simpler, but the ship loses control entirely and the push direction does not matter. Carry keeps steering while the push plays out.
- **Physics trigger colliders for zones.** These need a new collision layer and collision-event plumbing. Large thin walls trigger unreliably at high speed, and an extra event stream would need ordering.
- **Mines as non-voxel entities with a sphere collider.** Cheaper per mine, but they could not be shot apart, pushed by the same rules or chain-reacted, and they would need their own replication.
- **Tracks as a separate scene.** Co-op only runs in the World sandbox (SPEC-90), and the owner wants the same pieces reused in the game world.
