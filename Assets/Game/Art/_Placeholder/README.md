# Placeholder art — do not build on this

Everything in this folder was carried over from the old prototype so the game can run before real art exists.
**All of it will be replaced** after the owner approves the design system (`docs/specs/SPEC-80-art-direction.md`),
and this folder will then be deleted (`docs/specs/SPEC-81-asset-production.md`, step A2).

Rules (docs/02-conventions.md §5):
- Never reference these assets from code by path or name. Reference them from a content definition, a config
  ScriptableObject or a theme token (`Assets/Game/UI/Theme/tokens.uss`) so each can be swapped in one place.
- Don't tune gameplay to these meshes (hitboxes, sizes). Gameplay sizes come from cells and definitions.
- Don't add anything new here.

| Folder | Contents | Origin |
|---|---|---|
| Meshes | `shipVoxels.obj` (block meshes), `hulls.obj`, shield dome, warp gate, missile, projectile quad | Old prototype (own work) |
| Materials, Shaders | Metallic ship, shield, weapon arc, ghost block, voxel outline, heat distortion, sun, fresnel | Old prototype (own work) |
| Textures | `metalpatern.png`, `T_YFMeM_13_s.tga` (Yughues Free Metal Materials, Unity Asset Store), noise and cloud textures | Mixed; see notes |
| VFX | GalaxyVFX, NebulaDustVFX, SpaceDebrisVFX | Old prototype |
| Fonts | Kontrapunkt-Bold; OfficeCodePro family (SIL OFL, licence file included) | Free fonts |
| Icons | `item_icons_sheet.png` (block/item icons) | Old prototype |
