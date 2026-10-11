# Legacy migration report

Converters: Items (100), Blocks (110)

## Assets

| Asset | Outcome |
|---|---|
|Assets/Game/Content/Blocks/block_capacitor_small.asset|Unchanged|
|Assets/Game/Content/Blocks/block_core_cross.asset|Unchanged|
|Assets/Game/Content/Blocks/block_core_small.asset|Unchanged|
|Assets/Game/Content/Blocks/block_hull.asset|Unchanged|
|Assets/Game/Content/Blocks/block_hull_angled.asset|Unchanged|
|Assets/Game/Content/Blocks/block_portal_small.asset|Unchanged|
|Assets/Game/Content/Blocks/block_prismatic_mirror_fixed_small.asset|Unchanged|
|Assets/Game/Content/Blocks/block_prismatic_mirror_small.asset|Unchanged|
|Assets/Game/Content/Blocks/block_shield_small.asset|Unchanged|
|Assets/Game/Content/Blocks/block_thruster_large.asset|Unchanged|
|Assets/Game/Content/Blocks/block_thruster_medium.asset|Unchanged|
|Assets/Game/Content/Blocks/block_thruster_small.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_ark_lance_medium.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_fixed_beam_turret_small.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_flak_cannon_medium.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_missile_launcher_small.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_plasma_turret_medium.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_plasma_turret_small.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_plasma_turret_small_fixed.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_railgun_medium.asset|Unchanged|
|Assets/Game/Content/Blocks/weapon_small_beam_turret.asset|Unchanged|
|Assets/Game/Content/Items/item_regeneration_serum.asset|Unchanged|
|Assets/Game/Content/Items/item_research_blueprint.asset|Unchanged|
|Assets/Game/Content/Items/item_slot_expansion.asset|Unchanged|
|Assets/Game/Content/Items/item_strange_matter.asset|Unchanged|
|Assets/Game/Content/Items/item_strange_matter_container_empty.asset|Unchanged|
|Assets/Game/Content/Items/item_strange_matter_container_full.asset|Unchanged|
|Assets/Game/Content/Items/module_armor_plating.asset|Unchanged|
|Assets/Game/Content/Items/module_coolant_injector.asset|Unchanged|
|Assets/Game/Content/Items/module_increase_weapon_rotation.asset|Unchanged|
|Assets/Game/Content/Items/module_micro_plating.asset|Unchanged|
|Assets/Game/Content/Items/module_scrap_collector.asset|Unchanged|
|Assets/Game/Content/Items/module_shield_efficiency.asset|Unchanged|
|Assets/Game/Content/Items/module_trained_crew.asset|Unchanged|
|Assets/Game/Content/Items/officer_august.asset|Unchanged|
|Assets/Game/Content/Items/officer_gunner_vax.asset|Unchanged|
|Assets/Game/Content/Items/paint_00_default.asset|Unchanged|
|Assets/Game/Content/Items/paint_01_dark_gray.asset|Unchanged|
|Assets/Game/Content/Items/paint_02_red.asset|Unchanged|
|Assets/Game/Content/Items/paint_03_green.asset|Unchanged|
|Assets/Game/Content/Items/paint_04_blue.asset|Unchanged|
|Assets/Game/Content/Items/paint_05_yellow.asset|Unchanged|
|Assets/Game/Content/Items/paint_06_orange.asset|Unchanged|
|Assets/Game/Content/Items/paint_07_purple.asset|Unchanged|
|Assets/Game/Content/Items/paint_08_cyan.asset|Unchanged|
|Assets/Game/Content/Items/paint_09_pink.asset|Unchanged|
|Assets/Game/Content/Items/paint_10_brown.asset|Unchanged|
|Assets/Game/Content/Items/paint_11_black.asset|Unchanged|
|Assets/Game/Content/Items/paint_12_military_green.asset|Unchanged|
|Assets/Game/Content/Items/paint_13_steel_blue.asset|Unchanged|
|Assets/Game/Content/Items/paint_14_tan.asset|Unchanged|
|Assets/Game/Content/Items/paint_15_dark_red.asset|Unchanged|
|Assets/Game/Content/Items/paint_16_white.asset|Unchanged|
|Assets/Game/Content/Items/resource_crew.asset|Unchanged|
|Assets/Game/Content/Items/resource_fuel.asset|Unchanged|
|Assets/Game/Content/Items/resource_palladium.asset|Unchanged|

## Ships

| Ship | Legacy voxels | Empty keys | Placed | Dropped overlaps | Cells | Palette | Instances | Cores |
|---|---|---|---|---|---|---|---|---|

## Entries

```text
[W] FND-E001 Assets/Game/Content/Config/config_game.asset startBlueprint: No start blueprint: New Game is unavailable until one is set.
[I] MIG-I091 content/Items/Blocks/Cores/block_core_cross.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Cores/block_core_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Hulls/block_hull.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Hulls/block_hull_angled.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Misc/block_capacitor_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Misc/block_portal_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, EntryPosition, EntryDirection, ExitPosition, ExitDirection, EntryRadius.
[W] MIG-W034 content/Items/Blocks/Misc/block_portal_small.json ClearanceDirections: Dropped (1,0,0) to keep the portal achiral.
[I] MIG-I091 content/Items/Blocks/Misc/block_prismatic_mirror_fixed_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint, BeamThickness, BeamDamageMultiplier, IsRotating, RotationSpeed, MaxRotationAngle.
[I] MIG-I091 content/Items/Blocks/Misc/block_prismatic_mirror_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint, BeamThickness, BeamDamageMultiplier, IsRotating, RotationSpeed, MaxRotationAngle.
[I] MIG-I091 content/Items/Blocks/Misc/block_shield_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, ShieldRadius, ShieldForwardDirection.
[I] MIG-I091 content/Items/Blocks/Misc/block_thruster_large.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Misc/block_thruster_medium.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Misc/block_thruster_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate.
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_ark_lance_medium.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[W] MIG-W032 content/Items/Blocks/Weapons/weapon_ark_lance_medium.json BlockMesh: Mesh bounds differ from the footprint by more than 0.6 cell.
[W] MIG-W033 content/Items/Blocks/Weapons/weapon_ark_lance_medium.json ClearanceDirections: Dropped footprint cell (0, 0, 1).
[W] MIG-W033 content/Items/Blocks/Weapons/weapon_ark_lance_medium.json ClearanceDirections: Dropped footprint cell (0, 0, 2).
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_fixed_beam_turret_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_flak_cannon_medium.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_missile_launcher_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_plasma_turret_medium.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[W] MIG-W032 content/Items/Blocks/Weapons/weapon_plasma_turret_medium.json BlockMesh: Mesh bounds differ from the footprint by more than 0.6 cell.
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_plasma_turret_small.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_plasma_turret_small_fixed.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_railgun_medium.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[W] MIG-W033 content/Items/Blocks/Weapons/weapon_railgun_medium.json ClearanceDirections: Dropped footprint cell (0, 0, 1).
[I] MIG-I091 content/Items/Blocks/Weapons/weapon_small_beam_turret.json: Dropped: RotationLocked, SocketTypeProvided, SocketsProvidedCount, Modifiers, EnergyRegenRate, MaxEnergyStorage, PassiveDrainRate, disableInputTrigger, MuzzlePoint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_00_Default.json: Dropped: PaletteIndex 0, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_01_DarkGray.json: Dropped: PaletteIndex 1, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_02_Red.json: Dropped: PaletteIndex 2, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_03_Green.json: Dropped: PaletteIndex 3, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_04_Blue.json: Dropped: PaletteIndex 4, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_05_Yellow.json: Dropped: PaletteIndex 5, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_06_Orange.json: Dropped: PaletteIndex 6, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_07_Purple.json: Dropped: PaletteIndex 7, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_08_Cyan.json: Dropped: PaletteIndex 8, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_09_Pink.json: Dropped: PaletteIndex 9, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_10_Brown.json: Dropped: PaletteIndex 10, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_11_Black.json: Dropped: PaletteIndex 11, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_12_MilitaryGreen.json: Dropped: PaletteIndex 12, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_13_SteelBlue.json: Dropped: PaletteIndex 13, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_14_Tan.json: Dropped: PaletteIndex 14, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_15_DarkRed.json: Dropped: PaletteIndex 15, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/Paint/PaintBucket_16_White.json: Dropped: PaletteIndex 16, legacy Category Consumable, EffectType Paint.
[I] MIG-I091 content/Items/Consumables/item_strange_matter.json: Dropped: legacy Category Block.
[I] MIG-I091 content/Items/Consumables/slot_expansion_item.json: Dropped: EffectType AddModuleSlot.
[I] MIG-I091 content/Items/Consumables/strange_matter_container_empty.json: Dropped: legacy Category Misc.
[I] MIG-I091 content/Items/Modules/Module_Armor_Plating.json: Dropped: RequiredSocketType Core, 1 Modifiers.
[I] MIG-I091 content/Items/Modules/Module_Coolant_Injector.json: Dropped: RequiredSocketType Core, 1 Modifiers.
[I] MIG-I091 content/Items/Modules/Module_Scrap_Collector.json: Dropped: RequiredSocketType Core, 1 Modifiers.
[I] MIG-I091 content/Items/Modules/module_increase_weapon_rotation.json: Dropped: legacy Category Block, RequiredSocketType Core, 1 Modifiers.
[I] MIG-I091 content/Items/Modules/module_micro_plating.json: Dropped: RequiredSocketType Core, 1 Modifiers.
[I] MIG-I091 content/Items/Modules/module_shield_efficiency.json: Dropped: RequiredSocketType Core, 1 Modifiers.
[I] MIG-I091 content/Items/Modules/module_trained_crew.json: Dropped: RequiredSocketType Core, 1 Modifiers.
[I] MIG-I091 content/Items/Modules/strange_matter_container_full.json: Dropped: legacy Category Misc, RequiredSocketType Core, 3 Modifiers.
[I] MIG-I091 content/Items/Officers/officer_august.json: Dropped: RequiredSocketType Core.
[I] MIG-I091 content/Items/Officers/officer_gunner_vax.json: Dropped: RequiredSocketType Core, 2 Modifiers.
0 errors, 7 warnings, 51 infos
```

## Summary

0 errors, 7 warnings, 51 infos

Created: 0
Updated: 0
Unchanged: 56
SkippedHandEdited: 0
Failed: 0
