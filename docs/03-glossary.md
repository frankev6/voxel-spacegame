# 03 — Glossary

**Status: BINDING.** Use these words with exactly these meanings in code, specs, commits and UI text. If you need a new term, add it here in the same commit.

## Voxels and structures

| Term | Meaning |
|---|---|
| **Cell** | One 0.5 m voxel position inside a grid. Stored as a 4-byte `Cell`. Air is `Ref == 0`. |
| **Brick** | A 16×16×16 block of cells, stored as one entity with a 4,096-cell buffer. Only bricks that contain at least one solid cell exist. |
| **Grid** | One rigid voxel object: a ship, station, asteroid or hulk. In ECS it is the `VoxelGrid` root entity plus its bricks and functional blocks. |
| **VoxelStructure** | The managed, non-ECS copy of a grid's data (palette, instances, bricks). Used by the editor, saves, tests and as the spawn source. |
| **Blueprint** | A saved `VoxelStructure` that describes a ship design (content asset or player-made). |
| **Block** | The unit the player places. Defined by a `BlockDefinition`. A block occupies one cell (single-cell block) or several (multi-cell block). |
| **Block definition** | The content asset (`BlockDefinition` ScriptableObject) and its baked `BlockDef` blob entry. |
| **Block index** | Runtime `ushort` index of a block definition (0 = air). Changes between builds; never saved. |
| **Block instance** | A record in the grid's `BlockInstance` buffer for a multi-cell or functional block. Its cells point at it. |
| **Palette** | Per-grid table mapping a small index to (block, orientation, colour) for single-cell blocks. |
| **Orientation** | One of the 24 rotations of a cube (`byte` 0–23). Never Euler angles. |
| **Footprint** | The cells a block occupies, in its unrotated frame. |
| **Clearance** | Cells that must stay empty for a block to work (e.g. behind a thruster). |
| **Structural block** | Plain geometry (hull, armour, slopes). Drawn by brick meshes. |
| **Prop block** | A block drawn with its own instanced mesh but no entity (antenna, detail). |
| **Functional block** | A block with behaviour (thruster, weapon, reactor, shield emitter, core). Has an entity. |
| **Core** | The functional block that anchors a ship. Losing it turns the ship into a hulk. |
| **Anchor** | The island that keeps the grid's identity after a split: the one containing the core, or the largest island if there is no core. |
| **Island** | A set of solid cells connected face-to-face. |
| **Integrity** | The check that finds islands after cells are removed. |
| **Split** | Turning non-anchor islands into debris particles, chunk bodies or new hulk grids. |
| **Debris particle** | 1–8 removed cells rendered as GPU particles. No entity, no physics. |
| **Chunk body** | A small broken-off piece (about 9–500 cells) simulated as a pooled rigid body with a simple collider. Not a grid; can't be damaged further. Dissolves after 10–30 s. |
| **Hulk** | A grid that has lost its core, or a large broken-off island. Physical and damageable, no AI or power. Salvageable. |
| **Dirty brick** | A brick whose mesh and/or collider must be rebuilt (`BrickDirty`). |
| **Remesh** | Rebuilding a brick's render mesh. |

## Simulation

| Term | Meaning |
|---|---|
| **Tick** | One fixed simulation step (1/60 s). |
| **Frame** | One rendered frame. A frame runs 0..N ticks. |
| **Ship command** | The per-ship control input (thrust, turn, aim, fire groups) produced by the player or AI. |
| **Fire group** | A bit in `ShipCommand.FireGroups`; weapons are assigned to groups in the editor. |
| **Damage event** | A request to damage cells of one grid, in grid space. Only the resolver applies it. |
| **Resolver** | `DamageResolveSystem`, the only code that writes cell HP at runtime. |
| **Ram** | Damage caused by collision impulse between grids. |
| **Shield** | An analytic ellipsoid around a ship that absorbs hits before cells are damaged. |
| **Allegiance** | Which faction and team an entity belongs to. |
| **Dormant** | A far-away grid simulated cheaply (simple collider, slow AI) or not at all. |

## World

| Term | Meaning |
|---|---|
| **Galaxy** | The whole procedurally generated map: star systems connected by lanes. Regenerated from the seed. |
| **Star system** | One star and its bodies. Loaded one at a time for full simulation. "System" alone means an ECS system. |
| **Body** | Anything in a star system with a stable `BodyId`: star, planet, moon, station, belt, gate, point of interest. |
| **On rails** | A body whose position is computed from time and orbit parameters, not simulated. |
| **Lane** | A connection between two star systems used for jumps. Can be gated by facts. |
| **Jump** | Travel along a lane: the current star system unloads and the target loads. |
| **Supercruise** | Fast in-system travel mode (non-combat). |
| **Bubble** | The region around the player with full physics and destruction (streaming tier T3). |
| **Frame origin** | The point in the star system that local coordinate (0,0,0) represents. Moved only by `OriginRebaseSystem`. |
| **Delta** | Saved changes to a generated star system (destroyed bodies, edited structures, story spawns). |
| **Encounter** | Content describing ships or events that the director can spawn when conditions match. |

## Narrative

| Term | Meaning |
|---|---|
| **Fact** | A named integer in the `FactStore`. The single source of narrative truth. |
| **Condition** | A compiled boolean expression over facts, items and world state. |
| **Effect** | A compiled action that changes facts, items or the world. The only way narrative changes state. |
| **Story beat** | A storylet: a piece of content (usually a dialog node) eligible when its condition holds; the highest-salience eligible beat plays. |
| **Quest** | A declarative view over facts with stages and objectives. |
| **Clue** | A discovered piece of the mystery. Clues form the clue graph. |
| **Rumor** | A clue that points at a place; it shows a map marker. |
| **Mystery** | A question resolved when its required clues are known. |

## Player and editor

| Term | Meaning |
|---|---|
| **Session** | `GameSession`: all runtime state of one playthrough. Restored in place on load. |
| **Slot** | One save of a profile. |
| **Fleet** | The ships the player owns (blueprints plus damage state). |
| **Edit session** | A draft copy of a structure being edited, with undo/redo; changes nothing until committed. |
| **Bill of materials (BOM)** | The block counts required and refunded by a commit, computed as draft minus original. |
| **Blueprint Lab** | The economy-free editor scene used to build and test designs. |
| **Commit** | Applying an edit session: validate, pay, swap the structure. |

## Art and presentation

| Term | Meaning |
|---|---|
| **Paint palette** | The global table of 32 colours (`ArtDirectionDefinition.PaintPalette`) that turns a `PaletteEntry.Color` byte into a colour. Not the per-grid *palette*. |
| **Team colour slot** | A paint-palette index (17 primary, 18 secondary, 19 accent) resolved per grid from its faction's `ArtFactionLook`. |
| **Function colour** | The colour that marks what a functional block does (paint indices 20–25: thruster, weapon, power, shield, core, utility). Always paired with a glyph and a silhouette. |
| **Brick layer** | One slice of the brick texture arrays, selected by `BlockDefinition.materialLayer` (0–63; 63 = missing). |
| **Look test** | An in-engine review scene for a finalist art direction (SPEC-80 `ART-009`). Review material, not game content; deleted at A2. |
| **Golden image** | An approved look-test capture in `docs/design/golden/`: the visual target that rendering and asset work must match. |

## Process

| Term | Meaning |
|---|---|
| **Spec** | A file in `docs/specs/` defining one system or part of the game, with numbered tasks. |
| **Task** | One unit of work with an ID like `VOX-003`, defined in a spec and tracked in the ledger. |
| **Ledger** | `docs/LEDGER.md`: task status, owners, open questions and the work log. |
| **ADR** | Architecture Decision Record in `docs/decisions/`. The only way to change a binding rule. |
| **Gate** | A measurable exit criterion (test, perf number, owner approval) that must pass before dependent work starts. |
| **Placeholder** | Any asset in `Assets/Game/Art/_Placeholder/`. To be replaced after the design system is approved. |
