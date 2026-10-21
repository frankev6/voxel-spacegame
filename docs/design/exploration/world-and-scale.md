# World, scale and planets (design note, 2026-10-21)

**Status:** proposal; it changes nothing binding yet. It answers Frank's open questions from 2026-10-21:
- how big the world is;
- what planets are when flight stays on a plane;
- whether the game should stay in space or go back to water.

The feel prototype on `task/feel-prototype` tests the parts that can be felt.

## What the reference games do with planets

None of Frank's four reference games lands on a planet in the same physics as combat:

| Game | What a planet or port is |
|---|---|
| **Starsector** | A place on the one campaign map. You fly to it, and an interaction screen opens: survey, salvage, colony, trade. |
| **Starcom: Unknown Space** | You fly over planets in the same top-down world, with no collision. Scanning and away teams run as illustrated text events. |
| **Sid Meier's Pirates!** | A town is a place on the coast. Sailing into it opens illustrated scenes: tavern, governor, merchant. |
| **RimWorld** | Depth comes from systems colliding: crew traits, needs, a storyteller pacing events. Content density matters more than map size. |

So the open world stays continuous, and a planet is a *place you arrive at*, not a sphere you bump into or a surface you walk on.

## Proposal: the flight plane is an orbit, and planets lie below it

- **Ships fly on the plane, as now.** A planet sits *below* the plane: a huge sphere whose top lies a few kilometres under it. You never touch it and never go inside it. Flying over it is flying in orbit.
- **The orbit is the coast.** Stations, docks, defence platforms, moons, wrecks and debris rings sit in the plane around and over the planet. Tethers and space elevators rise from the surface to the plane: these are the ports.
- **Landing is a scene, not a flight.** Over a planet you can:
  - send an away team or a lander, as an illustrated event (Starcom, Pirates!);
  - scan;
  - mine from orbit;
  - trade with the surface through the elevator.

  The ship stays in the same world, and the same mechanics keep running around it: pirates can arrive while your team is down.
- **The camera sells it.** A lower chase camera puts the planet's curve and the horizon in view. That is why the camera change matters for exploration, not only for combat.
- **Optional later: atmosphere layers.** A gas giant's cloud deck can be a second plane below the orbit plane, entered at a marked descent point. The mechanics are the same, over clouds instead of stars. One plane at a time keeps planar combat.

### What the prototype shows (2026-10-21)

VESHA is in the playground (`playground goto=orbit`).
- It is an 80 km planet whose surface lies 2.5 km below the plane, south of the Hollow (first built at 150 km; at 80 km the horizon dips 14°, a curve the chase camera frames, where 150 km read almost flat).
- The Clan's tether rises from its surface to an anchor in the plane, with five wrecks drifting round the anchor.
- The status plate reads "LOW ORBIT · VESHA" within 1.6 km of the anchor.

From the low cameras, the planet's curve and the horizon sit just under the ship. From the Hollow it reads as cloud far below.

## 3D movement

Planar movement stays. Free 3D makes the camera, aiming and reading destruction much harder, and none of the four references needs it. The world can still be 3D *to look at*: planets below, structures and nebulae above and below the plane, tall stations.

## Scale

| Thing | Proposed size | Why |
|---|---|---|
| Combat bubble | ~2 km | What the current weapons, AI and streaming are tuned for |
| Station / capital ship | 100 m – 1 km | Already true (the Hollow, the REQUIEM) |
| Planet radius (below the plane) | ~80 km, top ~2.5 km under the plane | Measured in the prototype: from a 15–20° camera the horizon must sit just under the view. 150 km read almost flat; 80 km dips the horizon 14° and shows the curve. A 15 km planet 2.5 km down drops its limb 45° below the horizon, out of view. A huge, shallow planet reads as a world below you, and its surface scrolls by as you fly: a sense of speed that space otherwise lacks. Smaller bodies (moons, rocks) can still sit in the plane as obstacles |
| Star system | About 640 km across (the approved Hollow system, [exploration-slice.md](exploration-slice.md)), not to scale | A leg between planets takes 45–90 s of cruise |
| Cruise speed | Builds to 8 km/s in open space and falls to flight speed 1 km from anything ([ADR-0122](../../decisions/ADR-0122-in-world-cruise.md)); 800 m/s in the belt | Dropped by hostiles within 5 km (mass lock), a hit, Brake or J |

**Density beats size.** Aim for something worth a detour every 20–30 s of cruise:
- a derelict, a signal or an anomaly;
- a hazard field;
- faction traffic;
- a planet's orbit.

The jump gates stay: a system is a sea, and lanes are the straits between seas. The gates themselves are places.

SPEC-32 decision 12 (supercruise as a covered hop) is superseded by in-world cruise: [ADR-0122](../../decisions/ADR-0122-in-world-cruise.md) (2026-10-21).

## Space or water

The combat simulation is planar either way. Naval games keep the gameplay plane flat and make the waves cosmetic, so a water version would not bring back the old wave-pitch problem.

What water gives, and its space equivalent:

| Water | Space |
|---|---|
| Island and port | Planet orbit, station, tether |
| Horizon and the unknown beyond it | Sensor range, fog of war, a low camera, nebula haze |
| Weather | Ion storms, nebulae, solar wind |
| Currents and shallows | Gravity wells, debris fields, mines |
| Landing party | Away team or lander scene |

What space keeps that water would lose:
- 360° drift;
- debris that floats instead of sinking;
- ramming and splitting in vacuum;
- the lore;
- everything already built.

**Recommendation:** stay in space and build the water game's *structure* into it. The test: does one planet with its orbit coast, seen from the low camera, feel like arriving at an island? If it does not, reopen the question with that evidence.
