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
1. **Open the race.** The host flies to pad **18 RACE · BATTLE GRAVEYARD** (east of FLIGHT, on the first row) and presses **F**, or types `race open` (`race open laps=1` for a quick one). Every pilot is put on the start grid, held in place.
2. **Start it.** The host presses **F** again, or types `race start`. The countdown runs 3, 2, 1, then GO.
3. **Laps.** Three by default. A lap counts only through the gates in order. The line through the next gate is drawn thicker.
4. **Routes.** All of these count as laps:
   - the outer line;
   - **the Break** between the dreadnought's halves;
   - the carrier's **flight deck**;
   - the **keel line** past the battleship;
   - the Break and the keel line together;
   - the outer line through the lane-gate (the portal itself comes later; today you fly the outer line).
5. **Boost strips.** The green chevrons push you to 200 m/s, and the speed carries off the strip.
6. **Field walls** (cyan) bounce you. Hitting one fast costs hull.
7. **Mines** (red rings) arm, then blow when a ship comes close or when shot. Shoot a field to clear a path, or shoot the mine beside a rival.
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
| `net status` | Peers, round-trip time, bandwidth |
| `net guest <slot> "<command>"` | Run a console command on a guest |

## Known limits (first playable)
- The race state reaches guests through the dev console channel, so co-op racing needs development builds.
- Mines show their state (arming, armed, triggered) on the host only. Guests see a steady red ring.
- A respawned ship has no shield. The playground fits shields only when a session starts.
- The drifting bow, blast door, bulkheads, reactor pulses, engine wash, turrets, portal and canisters are RACE-015 and the HAZ-013 to HAZ-021 set pieces. They are not in the track yet.
