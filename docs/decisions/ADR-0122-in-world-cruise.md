# ADR-0122 — In-world cruise: the ship really crosses the star system

**Status:** Accepted (owner, 2026-10-21: approved the Hollow system map, "Write up the exploration slice plan (cruise ADR, …) and start building it")

## Context
The owner wants one continuous world: "I wanted everything to happen in the same open world with all the same mechanics, as in you go from place to place with the same ship." The approved exploration slice ([plan](../design/exploration/exploration-slice.md), [map](../design/exploration/img/hollow_system_slice.png)) is a star system about 640 km across:
- four planets below the flight plane;
- an asteroid belt and a nebula;
- the locked gate at the system edge, 320 km out.

Two things were already in place:
- **SPEC-32 decision 12** made supercruise an in-system hop: a covered teleport with the same cover, pause and placement as a lane jump. It was never built (TRV-013, `todo`). A hop skips the space between places, and the slice needs that space: the belt you cross, the planet whose surface scrolls under you, the contact you spot on the way.
- **Flight speed** is capped at 120 m/s, or 180 m/s with boost. At that speed one leg of the slice takes about 45 minutes.

## Decision
1. **Cruise is flight.** The ship flies the whole way in the one world, under the same physics and floating origin. The camera never cuts away and nothing is teleported.
2. **J toggles cruise** (left-stick press on a gamepad). It uses the existing `Flight/Cruise` action and its `Cruise` counter. The counter becomes a ship edge, `ShipCommandEdges.Cruise` (512, added at the end), consumed one press per tick like the other nine. This changes SPEC-20 Q13 and SPEC-03's row: the variable-rate consumer it was kept for (TravelService) no longer exists.
3. **Phases:**
   - **Off** is normal flight.
   - **Spooling** takes 1 s (60 ticks).
   - **On** is cruising.

   A press while Off starts the spool when cruise is allowed. A press while Spooling or On returns to Off.
4. **When cruise is allowed.** All three of these must hold to engage, and losing any of them drops cruise:
   - **Clearance:** the gap between the ship's sphere and the nearest other spatial-hash proxy's sphere is at least `SlowRadius` (1,000 m). That proxy can be any grid (ships, stations, rocks, wrecks). Celestial bodies are not proxies, because planets lie below the plane.
   - **No mass lock:** no hostile ship that isn't a hulk is within `MassLockRange` (5,000 m).
   - **Not hit:** the ship hasn't taken damage in the last `DamageLockTicks` (300 ticks, 5 s).

   Firing, Brake or reverse thrust also returns the ship to Off.
5. **Speed.**
   - The cap is `min(MaxSpeed, forward cap + Ramp × (clearance − SlowRadius))`, with `MaxSpeed` 8,000 m/s and `Ramp` 0.3 per second. **Cruise zones** (a ring or a disc in the plane, such as the belt) can lower the cap further.
   - Cruise speed rises by `max(MinAccel, Ramp × speed)` per second, with `MinAccel` 60 m/s². It drops to the cap at once.
   - When the cap falls to the forward cap, the ship is arriving: cruise turns Off and the ship goes on at flight speed, 1 km from whatever it approached.
   - The ramp keeps the ship at least 1/`Ramp` (3.3 s) from the nearest proxy at its current speed, so cruise never reaches a thing faster than it can stop.
6. **Handling while On:**
   - The ship flies along its nose at cruise speed. Turning is limited to 35% of the yaw cap, and there is no strafe.
   - Weapons hold: fire groups are cleared.
   - Thrusters show full forward thrust.
   - A system after propulsion (`CruiseDriveSystem`) sets the velocity. Only cruising ships pay for it.
7. **State:**
   - A `Cruise` component on every ship root holds the phase, the speed, the cap, the clearance and the reason cruise is blocked. One system writes it.
   - The HUD reads it: the speed readout, "CRUISE", and "MASS LOCKED", "TOO CLOSE" or "HIT" when a press is refused.
   - AI ships carry the component too, and can cruise later through the same edge.
8. **Floating origin:** at 8 km/s the origin rebases about four times a second, at the 2 km threshold. That is accepted: a rebase is one job over the roots, measured at ≤ 0.5 ms.
9. **The view** (amended 2026-10-21). Owner: "the ship should stay near ... increase FOV and have a shader effect". Then: "i didn't mean just make them all dull, i meant lets make it feel like you're actually going really fast".
   - The camera follows without lag while cruising. The usual follow filter trails by speed × 0.087 s, which is 430 m at 5 km/s.
   - The FOV opens by up to 14° with √(speed / 5 km/s), and the camera moves in to match, so the ship keeps its size on screen. While the drive spools, the FOV narrows 4°. As it engages, a kick: +9° and a flare of the speed lines, easing out over about a second.
   - The view follows an eased speed (half-life 0.5 s). The drive drops 5 km/s to a belt's 800 m/s in one tick, and the FOV and framing snapped with it.
   - Speed lines race out from the middle of the view to its edges. They are an overlay on the whole view (`Game/Vfx/CruiseSpeedLines`), the middle is kept clear for the ship, and they run faster and brighter with speed.
   - Bright dust dashes run past the ship in the world, at a felt speed the eye can follow: min(speed, 200 m/s + 0.12 × speed), so 920 m/s at 6 km/s. Dust at the true speed crossed the view in a frame or two and read as a still tunnel.
   - A rumble of up to 0.15°, and a vignette. Lens distortion and chromatic aberration were tried and removed: they squashed the planets and fringed the ring. The FOV stops at +14° for the same reason: a wider view stretches the planets near the edges.
   - All of it is off under reduce motion (`CruiseEffects`, `CameraRigService.SetCruise`).

## Consequences
- **SPEC-32:** decision 12, §5.10 and TRV-013 are superseded. `TravelService.CanCruise` and `RequestCruise` stay unimplemented and are marked superseded. TRV-015 builds this ADR.
- **SPEC-20:** Q13 changes. `Cruise` is now a ship edge, and the no-player and control-lock paths drop it like the others. The test `PlayerCommandApplyTests.Cruise_NotAShipEdge` becomes `Cruise_IsAShipEdge`.
- **SPEC-03:** the `Cruise` row now feeds the ship edge.
- **SPEC-61:** the HUD shows cruise in the speed readout. The system map's "Cruise here" button is dropped; the waypoint and edge markers replace it.
- **Co-op:** guests cruise through the same conversion, since the edge byte is already on the wire. The host's floating origin follows the host's ship only, so a guest cruising far from the host loses precision. Co-op cruise stays a known limit until co-op origins are per player.
- **Performance:** one job per tick over the cruising ships, each scanning the proxy list (hundreds of entries). Nothing runs when no ship is spooling or cruising.

## Alternatives rejected
- **The hop (decision 12):** it removes the space between places, which is what the slice is about.
- **A fixed 600 m/s cruise:** the first slice map assumed it. At system scale a single leg would take over 10 minutes.
- **Raising flight speed:** combat, AI, weapons and hit force are all tuned to 120 m/s.
- **Cruise as a separate mode with its own physics:** it would be a second flight model to keep in step. A velocity override on the same body keeps collisions, the origin and the camera unchanged.
