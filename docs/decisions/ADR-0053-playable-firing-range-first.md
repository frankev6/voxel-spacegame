# ADR-0053 — Deliver the playable firing and breakup slice before the complete combat milestone

**Status:** Accepted (Tech lead, 2026-10-12; implementation sequencing under Frank's explicit priority)

## Context

Frank asks: “ok continue with the shooting and destruction, I want to be able to fire at a bigger stationary ship (different model) and pieces breaking off. Get that playable and show me it working.” The complete SPEC-13/21 dependency graph also includes shields, AI spatial queries, every weapon delivery, chunk pooling, large-station integrity budgets and later gameplay services. Requiring that whole graph before the first firing demonstration conflicts with the owner's integrated-slice priority and ADR-0042. V1 code must not be copied.

## Decision

FND-020 owns the first integrated combat slice. It uses the existing imported Fighter and an owner-requested procedural test station. Frank corrected the target during implementation: “dont use vestari, make a test station we can shoot, that has various possible combination of blocks and structures so we can test everything like breaking off, etc”. Build the station from existing block definitions: layered hull, hollow pod, single and redundant bridges, slopes and multi-cell shapes, core, cargo, thruster and clustered reactors. This is a destruction test fixture, not a new production ship design. Content references, positioning and presentation tuning are authored data. The approved Nocturne environment remains unchanged.

Implement the first usable portions of the named production systems and contracts in their existing assemblies: input through ShipCommand; weapons through mounts and fixed-tick projectile rows; hit testing against current voxel cells; DamageEvent streams; DamageResolveSystem as sole cell writer; IntegritySystem; SplitSystem; ShipDeathSystem as the final destroy recorder; normal brick rendering and collider rebuilds. Never add a parallel sandbox damage writer, use physics colliders as bullet hit truth, or fake breakup in presentation.

For this bounded first slice, the following staging is explicit:

- Primary fire on Spacebar (Frank: “spacebar should be shooting”), with left-click retained; G is the separate tertiary group. Keyboard fire is not blocked merely by pointer overlap with HUD, while normal input contexts still gate it. Manual direct fire from installed weapon mounts, without AI/autofire, shields or power simulation. SPEC-21 already permits firing when Powered is absent. Existing contracts and configuration are used; unfinished delivery variants remain unfinished, not silently represented as working.
- The small firing range may use a deterministic ordered grid list as its broad phase until SPEC-23's spatial hash is implemented. Narrow phase still uses current cells, ignores the firing grid and selects the nearest hit. Projectile rows retain stable order and their normal owned lifetime.
- Initial weapon aiming/firing and projectile delivery may use serial scheduled jobs; preserve the common WeaponShots delivery boundary. Three plasma projectile definitions provide real installed Fighter fire; other delivery variants remain unarmed until implemented.
- Damage resolution may use a single scheduled Burst job over ordered grid ranges initially. No fixed-step Complete or main-thread cell mutation. SPEC-13's parallel-over-grids execution is deferred, with the same event ordering and single-writer boundary.
- Integrity may flood the occupied cells of changed range grids, with face adjacency and instance links. Keep the anchored component, or the largest component for a coreless grid with deterministic ties. SPEC-13's brick summaries, size-fair race and station-scale deferral are follow-up implementations, not prerequisites to demonstrating a small ship breaking apart.
- Detached multi-cell pieces may initially use the existing renderable hulk-grid representation, including inherited rigid velocity, separation, collision grace, real cells and normal collider ownership. The particle/chunk pool tiering remains future work. This intentionally favors an observable physical breakup over adding a second temporary mesh/physics path. The resolver remains the only remover of parent cells; split creation and delayed detach preserve the ownership rule.
- Content baking and presentation portions necessary for this slice may be implemented here in their canonical files. Their broader owning tasks remain todo until all of their own acceptance criteria pass. Later work extends or replaces the staged internals in place, never activates duplicate systems.

The range is a local playable checkpoint, not galaxy spawning, target persistence, finished HUD/art, final balance, or completion of M2. It must be repeatable from a documented command or game entry, respond to real mouse/keyboard input, and survive restart/scene exit without stale projectiles, jobs or native resources.

## Validation

FND-020 requires real input → projectile → hit → cell loss → disconnected physical piece tests, shooting through an existing hole, target initially stationary, and restart/cleanup checks. Compile and full correctness suites must pass. Capture the actual running game before firing and during/after breakup, and report measured sustained-fire frame times without treating advisory targets as blockers. No optimization pass is required merely to match a spec number.
