# ADR-0071 — Station AI command composition

**Status:** Accepted for implementation under the Tech lead's AI-009 delegation (2026-10-13); integration and focused Unity acceptance pending.

## Problem

Station profiles receive AiBrain through AiSetup and combat/power components through their owners. FlightSetup deliberately skips Station, so they receive no ShipCommand. AI-007 steering requires ShipCommand even for its stationary/no-FlightState path. This prevents station command rewriting and delivery of the station profile's ability edges.

## Decision

AiSetup.Register appends ShipCommand for GridProfile.Station in addition to its existing AiBrain registration. Keep the existing once-per-world registration from AiAnchorShiftSystem.OnCreate and the batch conversion in GridSpawnSystem. Requester-supplied command values retain ADR-0015's preservation rule; AiSteeringSystem rewrites them on subsequent live fixed ticks.

Do not add FlightState, flight thruster buffers, propulsion stats or PlanarConstraint to Station. FlightSetup and its flying profiles stay unchanged. Station roots remain statically spawned physics bodies and turret AI produces zero thrust/turn. AiSteeringSystem remains the command's sole simulation writer; AiDecisionSystem retains ownership of targeting/autofire and weapons retain ownership of firing.

## Validation and consequences

The two AI-009 PlayMode scenarios spawn an actual Station-profile static voxel root using authored ai_turret and weapon content, without manually adding ShipCommand. They exercise production profile registration, AI thinking/steering, power, weapon aim/fire and projectile delivery with an approaching hostile dynamic root. Assert targeting within the first eligible think, firing in weapon range, and unchanged station pose throughout. No new component layout, assembly/package reference, flight configuration, asset, save format or movement controller is introduced.
