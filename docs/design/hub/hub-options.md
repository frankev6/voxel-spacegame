# The capital hub: layout options (DEV-023)

## What the owner asked for (2026-10-18)

> "station hub feels too small, each section should really feel alive and purposeful. you blindly threw random things into circular spaces and called it a day wihtout any planning whatsoever."
>
> "the station hub should feel like a bustling station with ships of different sizes docking, coming in, and leaving, with buildings and sectors, like a live city. in the markets different merchants sell different things."
>
> "you put too much text everywhere especially unnecessary text, no need to have a reset for every section, just a main central reset is fine. and i should have a simple command to reset everything."
>
> "more good diagetic UI throughtout the world to make it feel part of you and alive."
>
> "the "flight" section is completely unnecessary, people already learn to fly by getting through the hub."
>
> "the center of the hub is currently useless wheel of idk what. when i asked for a space hub, i asked for something that is a vertical slice of the game, and representative of what a real world spacehub would look like."
>
> "the ships going around are not obeying laws of physics, i can't get them off path and they just push forward if you get in front, not realistic, should be using normal ai movement."
>
> "the hazards and mechanics, weapons, ships (like i should have a place to test out different ships and their weapons in the weapons arena, somewhere in the hub should be a showcase of the unique blocks and its mechanics etc. no campaign systems necessary."
>
> "you're thinking too much that this is a minimal testing ground, but i was actually asking for a vertical slice spacehub to showcase what a finished bustling capital hub would look like. you should feel like one small player in a big economic bustling city in space."
>
> "before going straight to building design it with purpose like we did the racetrack, should feel alive and grand"
>
> "i want to spawn outside the hub and the first impression as one enters this space hub should be grand, a city with traffic and multiple buildings, sections etc."

After the first three options:

> "make a fourth option with the gravity well at the core being a black hole and that is the stations power, and also teaches you how that mechanic works, but its better integrated than your options. i live the option a meridian you made, use that as a baseline for option a and integrate this idea. i also want to say when i said "buildings" you're thinking very blocky rectangles of a human city, im thinking form follows function and also different shapes and sizes that make sense in SPACE, you're following a "tall skinny skyscrapper building" when theres no sky and no gravity downwards. the spacehub uses the black hole in the center as their source of power, and everything is build around that central idea"
>
> "what are all those balls and rectangles meant to be, buildings?? i would rather it feel more like a DENSE bustling hub than sparse and uniform and boring with a bunch of shapes you know?"
>
> "also buildings can be connected into large dense clusters, i want megastructures"
>
> "and large mirror structures around the black hole syphoning its energy"
>
> "the moon or satelite in the top right corner is unnecessary because the black hole itself should have a strong gravity well. regarding the weird big panels idk what you think those are meant to represent? i asked for contraptions syphoning the black hole's energy"

## What went wrong with the Proving Grounds

It was a test menu laid out on the floor: a 1 km plaza with nine circles round it, a sign on everything and a reset pad in every circle. It never asked what a real city would be, why anything is where it is, or who works there. A real capital is dense, built up between its streets, and everything in it has a reason: the reactor powers the city, the defences guard it, merchants trade where the ships dock. This design starts there.

## Option D: Meridian on the hole (recommended)

![Option D](img/hub_option_d.png)

| Arrival | The core |
|---|---|
| ![D arrival](img/hub_arrival_d.jpg) | ![D core](img/hub_entry_d.jpg) |

Option A's Meridian, rebuilt round a black hole. The hole is why the city exists, and everything in it follows from that:

- **Power: the Siphons.** Eight harvester arms, 450 m long, reach in from the Harbour Ring to the edge of the accretion disk. Each ends in a magnetic nozzle, a funnel of four coil rings that pulls a tongue of plasma off the disk and drives it up the boom into a converter on the ring, with coolant tanks and radiators beside it. The arms alternate a little above and below the plane, so each stream arcs up off the disk into its nozzle. Conduits carry the power out to every quarter; energy canisters (cells) float at their nodes.
- **The first lesson is the gravity well.** Its pull falls off as 1/r²: 0.7 m/s² at the wall, 2.4 m/s² on the Current, 11.4 m/s² (the WRAITH's full thrust) at the **Horizon Line**, a ring of red buoys 600 m out. Inside it a starter ship cannot climb out. You feel the pull from the outer markers.
- **The Current** is the circular orbit 1300 m out (about 56 m/s). It is the city's ring road: the inbound lane bends into it, and if you cut your engines on it you orbit for free.
- **Flares.** The Breakers feed scrap down **the Drain**, a decaying-orbit spiral; every wreck that reaches the disk makes it flare (the reactor-pulse mechanic at city scale). The converters' lee is the shelter.
- **The Slingshot.** The Circuit's warm-up trial dives round the hole and out again: a gravity assist you learn by doing. The hole is the only well: there is no moon.
- **Megastructures.** Each quarter is a few huge connected slabs, cut by narrow flight streets, stepped in tiers above and below the plane, with the city's deep decks hanging hundreds of metres beneath. Tanks, radiator fins, masts and docking arms cover them, and ships dock along every street. Open space is left only where something happens: the firing lanes, the arena, the drydocks, the slipways.
- **Space forms, not skyscrapers.** Nothing stands on a floor. Habitat rings spin for weight, drums point their axis at the hole, radiators stand edge-on to it and the Siphons reach into it. Every form is centred on its own level.

### At play zoom, and what to polish before building

![D at play zoom](img/hub_play_sheet_d.jpg)

The renders above are taken from 3 km up; the player sees about 200 m. At that zoom (left to right, top to bottom: a Siphon nozzle, the Current by Harbour Control, an Exchange street zoomed out and at the normal zoom, the gate from inside, the core):

1. **The city stands up into the camera.** The slabs rise hundreds of metres above the flight plane, so at a 55° camera they lean into the lens and hide the streets and your ship. Recommended: hang the city's mass below the flight plane and fly over its roofs, docks and machinery; only masts, coils and docked ships rise above. (Owner decision.)
2. **Walls are one glittering window grid.** Use hull plating, a few broad lit bands, docking lights and a material per quarter.
3. **Too few ships on screen.** A street needs five to ten in view; this waits on the civilian ship classes.
4. **Too many dotted light lines.** One subtle lane style; one strong ring for the Horizon Line.
5. **Radiators read as orange stickers.** Rows of thin fins glowing at the root, fewer of them.
6. **The black hole is a glow, not a disk.** A thin banded disk brighter on one side, a photon ring, lensing of the stars behind: a VFX budget item.
7. **Converters are too small to read as the power stations.** Bigger converters, flickering streams, the conduits visible in 3D.
8. **Streets are about 52 m wide.** Widen the main streets to 80–100 m.
9. **Traffic cost.** About 200 ships at full physics is heavy: every ship within about 600 m of a player flies with real physics, distant ones are simulated cheaply and switch over as you approach.
10. **The Siphon nozzles sit inside the Horizon Line.** They must read as a no-go, or a stunt for strong ships.

## The first three options

All three maps are at true scale; every building, lane, berth and ship on them comes from `ArtSource/Hub/hub_layouts.py` (coordinates in [hub_options.json](hub_options.json)). The arrival renders are block-outs from the game camera (55° pitch, 40° field of view), pulled back to the arrival zoom; buildings are plain extrusions with lit windows (`ArtSource/Hub/hub_arrival.py`).

### Option A: Harbour City (the base for D)

![Option A](img/hub_option_a.png)

| Arrival | Through the Harbour Mouth |
|---|---|
| ![A arrival](img/hub_arrival_a.jpg) | ![A entry](img/hub_entry_a.jpg) |

**Meridian**, the Triple Alliance's free port: a walled city 4.6 km across, a docking spire at its heart and nine quarters between the boulevard and the wall. You arrive in the inbound stream behind a liner, pass under the gatehouse towers and the customs scan, and fly up the Grand Avenue to the harbour. Every quarter is one flight from the harbour along its avenue.

### Option B: The Keel

![Option B](img/hub_option_b.png)

| Arrival | The bow |
|---|---|
| ![B arrival](img/hub_arrival_b.jpg) | ![B entry](img/hub_entry_b.jpg) |

A city grown along the 5.4 km keel of the colony ship that brought the settlers. The ribs became its blocks. Traffic runs up one flank and down the other. You arrive at the bow and see the whole city in one long perspective. The ship's reactor still pulses and its stern engines still fire.

### Option C: The Crown

![Option C](img/hub_option_c.png)

| Arrival | The gate |
|---|---|
| ![C arrival](img/hub_arrival_c.jpg) | ![C entry](img/hub_entry_c.jpg) |

Station islands round an Aksharan gate, a hoop 1.3 km across standing through the plane, that still opens onto other systems. Ships flash in between its feet every few seconds. It has the strongest landmark of the three, but it is an archipelago rather than a city, and it commits the mystery's Aksharans early.

### Why D, and why it builds on A

A's walled harbour city, which you liked, keeps its nine quarters and its arrival; D gives it one idea that explains the whole city.

- A is the only one that feels like a city everywhere you fly: buildings on both sides, traffic in every street. You are one small ship among liners and freighters.
- Each quarter is one avenue from the harbour, so a new player learns the city in one lap, without any signs.
- The hazards sit where a city would have them: the reactor in the power quarter, turrets on the wall, the old minefield outside it, the quarry moon's gravity.
- C's gate could still stand outside A's wall as the Circuit's start gate, if you want the landmark too.

## The quarters (every option has the same nine; D's names in brackets)

| Quarter | Why it is there | What you can do |
|---|---|---|
| **The Harbour** (the centre) | Port Authority: the spire, the docking ring and sixteen piers | Watch liners, freighters and barges dock, wait and leave. The **Harbour Master's** console is the one reset for the whole city. |
| **The Exchange** | The market quarter on the Exchange Canal | Four trading houses, each with its own trader docked at its berth and its own stock: **Tlaxcala** (bulk goods, fuel, ore; buys anything), **Khuchad** (weapon blocks), **Vestari** (shields, reactors, high-tech blocks; charges foreigners more), **the Swap** (used and salvaged blocks, cheap). |
| **Meridian Yards** | The shipyard | Every ship in the game sits in a drydock: fly in to take it. The Blueprint Lab is in the yard office. An engine test stand fires a cruiser engine across the yard on a cycle (engine wash). New ships launch through the north gate. |
| **The Proving Range** | Weapons testing, next to the yards | The test line holds every ship in every weapon fit: take one and fire on targets out to 800 m (static hulks, targets on rails, drones, a breakable station). Field walls bound the lanes (shoot the emitters to drop them). Turret Alley runs the gauntlet turrets. A scoreboard wall shows hits, damage and DPS. |
| **The Pit** | The Khuchad arena | Pick a bout at one of four gates (raider duel, raider wing, xeno swarm, cruiser bout) and fight it in a 600 m bowl ringed by a field wall, in front of a crowd whose lights flash on every kill. |
| **The Institute** | The engineering gallery | Every special block and mechanism, working, in its own bay along a flyway: prismatic mirror, salvage magnet, portal blocks, shield emitter, reactor, switch and blast door, sweeper arm, rails, repulsor, EMP, seeker, gravity and proximity mines, energy canisters, impact driver. |
| **Breakers' Yard** | Shipbreakers | Hulks on slipways being cut apart by rigs; the debris drifts free for anyone with a magnet. Tugs tow a fresh wreck in every few minutes. Sell what you salvage at the Swap. |
| **The Foundry** (the Forge) | Power and industry | The city's reactor pulses behind blast bulkheads (hide in their shadows). Ore barges come in from the quarry moon outside the wall (its gravity well, the refinery's slow field; D has no moon, the barges use the outbound lanes). The old defence minefield lies beyond: proximity and seeker mines. |
| **The Circuit** | The racing quarter | Racers line up in the pit lane under the grandstand; the start portal outside the wall opens onto Track 01. |

Habitation towers fill the blocks between the quarters (Old Town round the harbour is the tallest): no gameplay, just the skyline and the people who live there.

**Cut:** the Flight district (you learn to fly getting to the harbour), the per-district reset pads, the taglines, directory boards and station labels, and every campaign system (galaxy map, story, survey, fleet). Those stay console shortcuts.

## Traffic: real ships, real physics

Every ship in the city is a real grid flying on its own thrusters under the normal AI pilot, not a mover on a rail.

- **A life cycle:** arrive from beyond the outer markers → follow the inbound lane → queue at the Harbour Mouth signals → take a berth → dock (clamps, lights) → wait 20–90 s → undock → leave by the outbound lane → despawn past the markers. District traffic runs the same loop between the Exchange, the yards and the breakers.
- **They share the road:** they brake and steer round you, give way at junctions, and keep their distance from each other. If you sit in their lane they slow, flash their lights and Harbour Control hails you. Ram one and it is knocked off its line by real hit force (SPEC-91), recovers and carries on, or limps to a berth if it lost its engines.
- **Sizes:** shuttles (14 m), couriers (22 m), tugs (32 m, some towing wrecks), haulers (64 m), freighters (140 m), ore barges (190 m), liners (240 m), plus NOCTURNE patrols and racers practising. About 40 real ships within 2 km of you; beyond that, traffic on the lower levels is lights only.
- **Ships needed:** the civilian line does not exist yet (the legacy freighter and shuttle are removed). It is the next design step: silhouettes for your approval first, per faction (Tlaxcala corporate haulers and liners, Khuchad armed traders, Scavenger tugs, Vestari couriers). The NOCTURNE ships stand in until then.

## The arrival

- You spawn 1.2 km outside the wall in the inbound stream, behind a liner, with the city ahead.
- The camera starts at the arrival zoom (the renders above) and eases in to the normal 150 m as you reach the outer markers, or as soon as you steer away.
- Harbour Control hails you on the comms line; the signals at the mouth are green for you; the customs scan sweeps you as you pass; MERIDIAN hangs over the gate.

## Diegetic UI, and the text budget

The world tells you what things are; text is the last resort.

- **Light, not labels:** lane lights chase in your direction of travel; berth lights run toward a free berth when you slow near one and turn red when it is taken; traffic signals at the mouth and the junctions.
- **Signs only where a city would have them:** MERIDIAN over the gate, one word per quarter on its landmark (EXCHANGE, YARDS, RANGE, THE PIT, INSTITUTE, BREAKERS, FOUNDRY, CIRCUIT), the four trading houses' names over their storefronts, and berth numbers painted on the piers. Nothing else.
- **Holograms that show, not tell:** storefront holograms show what each house sells (weapon silhouettes for Khuchad, a shield glyph for Vestari); a ship in a drydock shows its stat card only when you are within 100 m; the range's scoreboard wall shows your last run; the Pit's crowd boards show the bout; the Circuit's grandstand shows the leaderboard.
- **Voices:** Harbour Control, the Range Officer and the Pit's announcer speak on the comms line, briefly.
- **Interaction:** the F prompt appears only when you are close to something you can use.

## Resetting

- One reset: the **Harbour Master's** console at the spire, and the console command `hub reset` (also `reset`).
- Exhibits re-arm themselves: anything you broke, picked up or set off restores once you have been away from it for 10 s.

## How it gets built

- **The city fabric** (the thousands of buildings) is static, instanced meshes with box colliders, under the city's hard-light shields (hits flash and bounce): a 1.6 M-cell voxel city would blow the budgets. Everything you play with is voxel and destructible: the merchant houses, drydocks, targets, exhibits, hulks, the arena stands, every ship.
- **The hole (D):** the existing gravity-well system with a 1/r² pull and a kill radius at the horizon (you respawn at Harbour Control); the flares are the pulse system with the converters as shadow casters; the new work is the black hole's look (a lensing shader, the disk, the flares).
- **Occlusion:** a building between the camera and your ship fades to a hologram outline.
- **Lower levels:** the city runs on down below the plane, lit, out of reach.
- **Streaming:** fabric and traffic within 2.5 km of you are live; the rest of the skyline is a few draw calls.

## Decisions

1. **Which city:** D (recommended), A, B or C, or a mix.
2. **The civilian ship line:** approve the class list above, then silhouettes come for approval before any 3D.
