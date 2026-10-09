# ADR-0012 — Working title, company and save location

**Status:** Accepted (working values)

## Context
Player settings decide the save path (`Application.persistentDataPath`). The prototype used company "Frank", product "Spaceship Game"; v2 must not collide with its saves.

## Decision
Company "Koala Studios", product "Spaceship Game V2", bundle ID `com.koalastudios.spaceshipgame.v2` until the owner picks a name. `Game.Editor.ProjectSetup` holds these values and `ProjectSettingsGuardTests` checks them.

## Consequences
Renaming later changes the save folder; SPEC-05 must offer a one-time migration if saves exist by then.

## Alternatives considered
Reusing the prototype's identity (rejected: save collisions).
