# ADR-0013 — Block functions are composable modules

**Status:** Accepted (owner, 2026-10-10; extensibility review X1 and X11)

## Context
SPEC-04 gave every block exactly one `FunctionKind` from a closed enum. The parameters of every kind sat in one `FunctionParams` struct on every block. `GridSpawnSystem` kept one functional prototype per kind in a fixed eight-entry list, and SPEC-22 switched on the kind in two central systems. A new block function (a hangar, a tractor emitter, a repair bay) therefore edited SPEC-04, SPEC-10 and SPEC-22. A block could not combine functions: a core could not also store power, and a weapon could not carry its own capacitor. `UtilityKind` was a second closed enum inside the first, and two of its three values had no behaviour anywhere.

## Decision
1. **Modules.** `BlockDefinition` holds `[SerializeReference] BlockModule[] modules`. `BlockModule` is an abstract `[Serializable]` class in `Game.Data.Blocks`. Each concrete module derives from `BlockModule<TRow>`, where `TRow` is an unmanaged row struct baked into the blob. A block has at most one module of each `BlockModuleId`.
2. **Module identity.** `BlockModuleId : byte` is one append-only list in `Game.Data.Blocks`, with at most 63 values. `BlockDef` carries `ulong ModuleMask` (bit = id), so `Has(id)` is one AND.
3. **Baking.** SPEC-04 bakes every module of every block into one `BlockModulesBlob` (ECS singleton `BlockModulesRef`). It has one table per module id: row size, a row-by-block index, and rows. The builder never names a concrete module: each module writes its own row (`BlockModule<TRow>.Bake`). Burst code reads rows with `TryGet<TRow>(id, block)`. `BlockDefsHash` covers the row bytes.
4. **Kinds.** `BlockKind.Functional` ⇔ at least one module. Every functional block still gets one functional entity.
5. **Components by registration.** `FunctionalComponentRegistration` is keyed by `BlockModuleId`. `GridSpawnSystem` caches one functional prototype per distinct `ModuleMask`: the base types plus the union of the types registered for each module in the mask.
6. **Display category.** `BlockCategory` (Structure, Detail, Core, Propulsion, Power, Weapon, Shield, Utility) is a display-only field authored on the block. Editor palette tabs and art colours and glyphs use it. Gameplay code never reads it.
7. **Editor stats.** Each module adds its editor stats through `BlockModule.ContributeStats`. `BlockStatMath` (Game.Data) sums them for SPEC-51 and SPEC-61, so a new module brings its own stat preview.
8. **v1 module set.** SPEC-04 designs it: `Core`, `PowerSource`, `PowerStorage`, `Thruster`, `WeaponMount` (holds the `WeaponDefinition` link), `ShieldEmitter`, `Cargo`, `Explosive`. Later modules are designed by their own spec, and their class files go in `Game.Data` (architecture §3.2).
9. **Ability grants are unchanged.** They stay a `BlockDefinition` list (`grantedAbilities`, baked to `BlockDef.AbilityIndices`), which is already composable.
10. **Removed.** `FunctionKind`, `FunctionParams`, `ReactorParams`, `CapacitorParams`, `UtilityParams` and `UtilityKind`. The legacy portal and prismatic-mirror blocks migrate as non-functional props (X11).

## Consequences
- A new block function adds one module class with its row struct and one registration line in the systems that use it. No existing system or table changes.
- Mixed blocks become possible, for example a reactor with an explosive module, or a core with power source and storage.
- The rewrite touches SPEC-04 (§3, §4.2, §4.5–§4.7, §4.10, §5.5, §5.8, DATA-001/006/009/010/011), SPEC-10 (§4.5, §5.7, §7), SPEC-13 (reactor blasts), SPEC-20/21/22 (registration and reads), SPEC-50 (cargo), SPEC-51 (categories, stats), SPEC-72 (MIG-006 writes modules), SPEC-80 and DESIGN-SYSTEM (category names).
- Cost: one indirection per module read (table lookup by block index). This is negligible next to the per-instance work.

## Alternatives considered
- **Keep `FunctionKind` and size the prototype table from the enum.** This fixes the hard-coded eight, but every new function still edits the enum, the fat params struct and every switch, and blocks stay single-function.
- **One blob per module type, built by each owning spec.** This gives better type isolation but needs a boot step per spec, and `BlockDefsHash` could not cover module data.
- **Functions as entity prefabs authored per block.** Rejected: prefab authoring per block is a second source of truth beside the content definition (architecture rule 2), and baking prefabs needs subscenes, which v1 does not use.
