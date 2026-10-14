# ADR-0113 — Rebase pending grid poses

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

OriginRebaseSystem owns floating-origin conversion of root GridSpawnRequest.Pose as well as root LocalTransform. Request producers still author the initial pose and GridSpawnSystem still consumes the request. During a rebase, a pending root with UniversePosition derives its local position from that absolute value and the new origin. Without UniversePosition, apply the ordinary delta only on a same-system rebase. Exclude Parent entities. Preserve rotation, velocity and other request fields.

## Rationale and consequences

The real opening route placed the Drift cache anchor 81 metres from the player while its delayed physical grid spawned over 30 kilometres away. Pending requests have no LocalTransform, so the existing rebase query missed them. This extends the existing sole coordinate-frame owner; no new fields, save schema, assembly references or spawn ownership changes. Extend existing origin-rebase functional cases to cover pending requests and verify the actual chapter interaction. No new test case or performance pass.
