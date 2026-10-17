# Racing with friends (RACE-010 guide)

How to race Track 01, the Battle Graveyard, with friends over the internet. One person hosts; up to three others join. The host runs the race: gates, laps, places, mines and respawns are decided on the host, and the guests mirror it.

## What you need
- **Tailscale** on every machine, all signed in to the same tailnet (free: https://tailscale.com). It gives each machine a stable `100.x.y.z` address, so nobody has to open router ports.
- **The host** runs the Unity Editor on this repository, or the macOS dev player (`tools/build-dev.sh`).
- **Each guest** runs a development player:
  - on Windows, the zip from `tools/build-windows.sh`;
  - on a Mac, `Builds/dev/SpaceshipGame.app`.
- **Everyone** runs a build from the same commit. A guest from another commit is turned away with "Version mismatch".

## Building the Windows player (host machine, once per commit)
1. In Unity Hub, open **Installs**, pick this project's Unity version (see `ProjectSettings/ProjectVersion.txt`), then choose **Add modules → Windows Build Support (Mono)**.
2. Close the Editor on this project, then run:
   ```bash
   tools/build-windows.sh
   ```
3. Send `Builds/SpaceshipGame-windows-dev.zip` to your friends. They unzip it anywhere.

## Hosting
1. Open the project and press **Play** in the `World` scene. You are dropped into the playground.
2. Choose your racer **before** hosting. The playground cannot reset during a co-op session. Open the console (bottom-right **Console**, or F1) and type one of:
   ```
   playground ship=ship_racer_needle
   playground ship=ship_racer_kite
   playground ship=ship_racer_gimbal
   playground ship=ship_racer_battering_ram
   playground ship=ship_racer_hammerhead
   ```
3. Host, choosing the racer your guests fly:
   ```
   net host ship=ship_racer_hammerhead
   ```
   Without `ship=`, guests fly a copy of your ship.
4. Tell your friends your Tailscale address (`tailscale ip -4` on the host machine).

## Joining (guests)
- **Windows:** in the unzipped folder, run in a terminal:
  ```
  SpaceshipGame.exe -coop-join 100.x.y.z -coop-name Daniel
  ```
  Or start the game, open the console and type `net join 100.x.y.z`.
- **Mac:** run:
  ```
  open -n Builds/dev/SpaceshipGame.app --args -coop-join 100.x.y.z -coop-name Frank2
  ```

The guest loads the host's world. That takes a few seconds; the track wrecks are large.

## Racing
1. **Open the race.** The host flies to pad **18 RACE · BATTLE GRAVEYARD** (east of FLIGHT, on the first row) and presses **F**, or types `race open` (`race open laps=1` for a quick one). Every pilot is put on the start grid, held in place. **AI racers** fill the grid slots nobody takes (VEGA, ORION, LYRA… each in a different racer); `race open ai=0` races without them, `ai=1` with one.
2. **Start it.** The host presses **F** again, or types `race start`. The countdown runs 3, 2, 1, then GO.
3. **Laps.** Three by default. A lap counts only through the gates in order. The line through the next gate is drawn thicker.
4. **Routes.** All of these count as laps:
   - the outer line;
   - **the Break** between the dreadnought's halves;
   - the carrier's **flight deck**;
   - the **keel line** past the battleship;
   - the Break and the keel line together;
   - the outer line through the lane-gate (the portal itself comes later; today you fly the outer line).
5. **Boost strips.** The green chevrons cover the inside half of the route before a bend and push you to 200 m/s; the speed carries off the strip. Taking one is a choice: the other half is clear, and a heavy racer that cannot brake for the bend after it should keep off. The AI racers decide the same way.
6. **Field walls** (blue) bounce you. Hitting one fast costs hull. Each wall is projected by voxel emitter posts about every 50 m; shoot a post's head off and the fence on both sides of it goes dark and stops holding anyone in. The gate pylons carry the fence beside them too. On the carrier's flight deck there is no field: the hull's trench walls are the barrier.
7. **Mines** (red rings) arm, then blow when a ship comes within about 14 m: 2500 damage and a shove that throws a racer. Shooting one knocks it loose and sends it drifting (into a rival, or out of your line); it bounces off the fences. About ten hits crack the casing and set it off.
8. **Guns push.** Every hit shoves the ship it hits, and every shot kicks yours back. Darts are light and get thrown about. Rams barely move, and their impact driver shoves rivals off the line.
9. **Respawn.** A destroyed racer flies again after 3 s at its last gate, in a fresh ship. Its wreck stays on the track.
10. **Finish.** The race ends when everyone finishes, or 60 s after the winner. The results board shows places and best laps. **F** races again; `race end` clears the track.

## Controls
| Key | Action |
|---|---|
| W / S | Forward / reverse |
| A / D | Turn |
| Shift + A / D, or Q / E | Strafe |
| Ctrl | Boost |
| X | Brake |
| Space or left mouse | Fire |
| Mouse wheel | Camera zoom |
| F | Interact (on the grid: start the race; host only) |

## Host console commands
| Command | Does |
|---|---|
| `race open [laps=<n>]` | Spawn the track and put everyone on the grid |
| `race start` | Countdown |
| `race info` | Phase, laps, places, last gates |
| `race end` | Clear the track |
| `race respawn` | Put yourself back at your last gate |
| `race gate=<id> [back=<m>]` | Practice: jump `back` metres behind gate `id` |
| `race blast` | Test: blow the emitter post nearest your ship (its fence spans drop) |
| `race mine [ahead=<m>]` | Test: drop a mine in front of you |
| `race autopilot [on\|off]` | The racer AI flies your ship (host or solo), to watch a lap hands-off |
| `race ai` | What each AI-flown racer is doing: speed, target speed, line, threat |
| `race trace [on\|off]` | Record every racer 10 times a second to `Logs/race-trace.csv`; plot it with `tools/playtest/race-plot.py Logs/race-trace.csv` |
| `timescale [<1..6>]` | Solo only: run the simulation up to 6× real time to watch AI laps sooner. Ticks stay 1/60 s, so a race plays out the same; if frames cannot keep up it runs less fast, never coarser |
| `net status` | Peers, round-trip time, bandwidth |
| `net guest <slot> "<command>"` | Run a console command on a guest |

## Known limits (first playable)
- The race state reaches guests through the dev console channel, so co-op racing needs development builds.
- Mines show their state (arming, armed, triggered) on the host only. Guests see a steady red ring.
- Fence spans dropping on a guest when a post is shot follows the same rule as the host, but has not been checked on two machines yet.
- AI racers run on the host only; guests see them as ordinary ships. They shoot mines and wrecks in their way, and rivals now and then.
- A ship with its shield up flies at 80% of its speed caps (all ships, not only in races): safer but slower.
- A respawned ship has no shield. The playground fits shields only when a session starts.
- The drifting bow, blast door, bulkheads, reactor pulses, engine wash, turrets, portal and canisters are RACE-015 and the HAZ-013 to HAZ-021 set pieces. They are not in the track yet.
