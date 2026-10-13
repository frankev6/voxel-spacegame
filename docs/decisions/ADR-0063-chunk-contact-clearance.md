# ADR-0063 — Rearm chunk parent contacts after clearance

**Status:** Accepted (Tech lead, 2026-10-13; owner-authorized correctness fixes and spec-gap resolution).

## Evidence

DMG-011 live station wall cut yields 264 cells. During 60-tick parent contact suppression the chunk tumbles into surviving geometry. At age 59 it moves at approximately 1.5 m/s; at age 73, after contact reactivation, it moves at approximately 102 m/s. The parent collider was Ready before expiry. `Logs/playtest/dmg011/contact-handoff.txt` records the reproduction.

## Decision

Keep SplitGrace's existing minimum interval and all ordinary grid/stow behavior. Add internal Voxel PhysicsBody `ChunkContactState { byte Armed; }` to pooled chunk entities. SplitSystem initializes it to zero on creation and every slot reuse. SplitGraceContactSystem is its sole subsequent writer.

After the minimum interval, a serial job checks the current physics-world chunk/parent collider distance. While touching/overlapping, retain suppression for that named pair only. Once geometrically clear, set Armed to one permanently for that pool incarnation; all later collisions, including return impacts against the parent, occur normally. A missing parent arms immediately; a chunk not yet in the physics world waits. Use current collider poses and scales. No velocity clamps, changed separation/tumble defaults, extra native owner, or new module dependency.

## Consequences

A piece can ghost through its parent until clear; it continues moving, tumbling and expiring normally and still contacts unrelated bodies. This avoids stored interpenetration being converted to a violent impulse. The existing PHY-014 grace regression also checks continued suppression, clearance and subsequent recontact. DMG-011 live proof must be repeated before completion. This is an integrated correctness repair, not an optimization pass or owner gate.
