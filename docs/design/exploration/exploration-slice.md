# The Hollow system: exploration slice plan (2026-10-21)

**Status:** approved by the owner 2026-10-21 ("Approved. Write up the exploration slice plan … and start building it"). The map is [img/hollow_system_slice.png](img/hollow_system_slice.png), drawn by [img/hollow_system_map.py](img/hollow_system_map.py). Background: [world-and-scale.md](world-and-scale.md).

## What the slice must prove
One 20-minute loop in one star system that feels like Pirates! or Starcom. You hear a rumour in port, cross open space to something unknown, and come home with a clue that opens the way on. Everything happens in the one world, with the same ship and the same mechanics.

**The test:** does arriving at a planet's coast feel like arriving at an island? If not, reopen the space-or-water question with that evidence.

## The answer it rests on
- **A site is the island.** It is a physical place in the plane that you fly into: a derelict, a station, a tether, a ruin.
- **A planet is a region.** It lies below the flight plane, each at its own depth. Its disk is a region you fly over, and its coast is the sites in the plane above it. You never touch it and never go inside it.
- **The surface is reached only through scenes:** away teams down a lift or tether.
- **Exploring is scary because of three things:**
  - you do not know what a contact is until you commit;
  - distance costs fuel, supplies and repairs;
  - things are far bigger than you.

## The loop

| Step | Where | What happens | Built from |
|---|---|---|---|
| 1 Rumour | The Hollow (VESHA's orbit) | The salvage broker: a Clan hauler went dark | Dialog (SPEC-41), rumours (SPEC-42) |
| 2 The tether | VESHA's coast | The dockmaster has its last beacon: past the belt, at the Haze | Dialog, waypoint marker |
| 3 The crossing | The Scatter (belt) | Cruise; the belt slows you. Ping to see, and be seen, or run quiet. A "?" appears | **New:** cruise, "?" contacts, the loud ping |
| 4 The hauler | The Haze's edge | Raiders are stripping it. Fight, or cut in quietly. Its log names CINDER | **New:** site; existing encounters, salvage, clues |
| 5 Away team | CINDER, the dead colony's lift | One illustrated scene. A crew member's trait decides it | **New:** crew, away-team scene; dialog |
| 6 Home | Skirt the star's heat back to the Hollow | Repair, sell, hire | Economy (SPEC-50); **new:** supplies, heat zone |
| 7 The gate | The system edge, 320 km out | The clue from CINDER opens it. The slice ends there | Lane access by fact (SPEC-32) |

## Phases
Each phase is playable on its own in the playground, with a scenario that proves it, before the next starts.

### Phase 1: the system you can cross (TRV-015, DEV-028)
- **In-world cruise:** [ADR-0122](../../decisions/ADR-0122-in-world-cruise.md).
  - J toggles it, with a 1 s spool.
  - Speed builds to 8 km/s in open space and falls to flight speed 1 km from anything.
  - Mass lock from hostiles within 5 km, and a 5 s lock after a hit.
  - Cruise zones lower the cap: the Scatter caps it at 800 m/s.
- **The system in the playground**, placed so that VESHA's tether stays where it is:
  - the star far below the plane, with a heat zone;
  - VESHA (80 km), CINDER (18 km), ORRO (110 km, a gas giant with a ring) and PALE (26 km), each at its own depth;
  - the Scatter;
  - the Haze;
  - markers for the sites still to come, which the edge markers point at.
- **HUD:** the speed readout shows cruise speed, and refusals show as a short line ("MASS LOCKED · hostile 3.2 km").
- **Proof:** a scenario that cruises from the Hollow to CINDER and back, with shots over VESHA, crossing the belt, at CINDER, and with ORRO on the horizon.

### Phase 2: "?" contacts and the loud ping
- Beyond identification range (2 km; 1 km in the Haze), a contact is a "?" with a rough distance: an edge marker without a name.
- The ping (V) identifies everything within 3 km (1.5 km in the Haze). It is also heard: AI ships within 6 km that are hostile to you turn toward you, and a dormant site may wake.
- Running quiet is the default.
- **Proof:** a scenario in which a Raider pair is spotted as "?", a ping names it, and the pair turns.

### Phase 3: the sites
- **The lost hauler:**
  - a Clan derelict, a real voxel grid that can be cut open, at the Haze's edge;
  - a Raider scavenger encounter on it (SPEC-33);
  - its log is a clue item (SPEC-42) that names CINDER.
- **The dead colony** over CINDER: a small station with the lift, an interaction target.
- **Picket 9:** live turrets, with a block cache inside.
- **Skerry:** an ice moonlet with mineable ice.
- **The lane gate**, locked by fact.
- **Minor finds on the legs:**
  - a cargo pod;
  - a nav beacon;
  - a dead lifeboat;
  - a debris stream;
  - a distress call;
  - a roaming Raider pair.

### Phase 4: crew
- Three named crew members to start with, hired at the Hollow. Each has one trait, such as steady, reckless, engineer or devout.
- They comment in toasts on what you find.
- A crew panel in the ship log.
- No pay or morale yet.

### Phase 5: the away-team scene
- At the colony lift, the dialog screen (SPEC-41) opens as an illustrated scene. You choose who goes, and the traits gate the choices.
- Outcomes:
  - the clue;
  - an injury, so a crew member is out for a while;
  - loot.
- Your ship stays in the world while the team is down. A Raider pair can arrive, and the scene can be cut short by it.

### Phase 6: fuel and supplies
- **Fuel** burns per km of cruise. Skerry's ice refines to fuel at the Hollow, and lane jumps keep their fuel cost.
- **Supplies** are spent on field repairs. You buy them at the Hollow, and running out means repairs stop.
- **The heat zone** burns hulls near the star.

### Phase 7: the whole loop
- The gate opens on the clue.
- An end-to-end scenario plays the loop with scripted input.
- The owner playtests it.

## Open questions (defaults in use)

| # | Question | Default | Decider |
|---|---|---|---|
| 1 | Names other than VESHA, the Hollow and the Clan (CINDER, ORRO, PALE, the Scatter, the Haze, Picket 9, Skerry) | Placeholders until the owner names them | Owner |
| 2 | Where the system lives: the playground or a real star system (SPEC-30 content) | The playground first, for fast iteration; it moves into a real star-system definition once the loop plays | Lead |
| 3 | Gas giant cloud decks (a second plane) | Later; ORRO shows a "later site" | Owner |
