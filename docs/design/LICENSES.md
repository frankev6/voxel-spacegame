# Asset provenance register

SPEC-81 §4.3 register, audited from the repository on 2026-10-13 (AST-003). The table contains one row for every file under `Assets/Game/Art/_Placeholder/` and `Assets/Game/Audio/`, including companion/folder metadata and the placeholder README. Office Code Pro's current font family is included at its canonical UI path; prior REN-008/VFX-004 rows are preserved. Planned manifest rows are not evidence of produced or licensed assets.

`verified` means the row has an actual retained proof or dated owner confirmation; only Office Code Pro has qualifying proof in this audit. `placeholder-only` retains an unresolved asset without approving shipment. `remove` identifies an excluded file; no asset was deleted by this audit. Unknown Source/Licence/Author/Date/Proof values use `-` or explicit unresolved text rather than invented sources or permissions. Metadata is separately inventoried and does not inherit a font's or its referenced asset's verification.

The `own work` source on unresolved prototype mesh/material/shader/VFX rows preserves the existing placeholder README's reported attribution, not an owner confirmation. Existing generated REN-008/VFX-004 rows retain their previous author and creation dates; those dates are not licence-confirmation dates. Their prior no-download/no-image-generation assertions remain evidence only. No unknown row was upgraded on the strength of a filename, git author, source generator or design approval.

Office Code Pro's proof is a byte-for-byte copy of the already bundled OFL 1.1 file. The eight TTFs identify Office Code Pro and SIL OFL in their embedded name tables; both SDF assets reference the matching TTF GUIDs. Its verified date records examination of that existing evidence, not a new source download. This confirms licensing, not production-font/design approval.

See [audit evidence and unresolved file list](licenses/placeholder-audit-2026-10-13.md) and [exact inventory with hashes and GUIDs](licenses/placeholder-inventory-2026-10-13.csv). Receipt, authorship and AI-approval gaps remain explicit; the existing audio table's 17 Unverified and two Blocked clips remain unchanged.

| Path | Kind | Source | Licence | Author | Date | Proof | Status |
|---|---|---|---|---|---|---|---|
| `Assets/Game/Art/_Placeholder/Fonts.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Fonts/Kontrapunkt-Bold.otf` | font | - | No licence file; excluded | Unknown | - | - | remove |
| `Assets/Game/Art/_Placeholder/Fonts/Kontrapunkt-Bold.otf.meta` | definition | - | No licence file; excluded | Unknown | - | - | remove |
| `Assets/Game/Art/_Placeholder/Icons.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Icons/item_icons_sheet.png` | icon | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Icons/item_icons_sheet.png.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/AllyOutlineMat.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/AllyOutlineMat.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/BrickPlaceholder.mat` | material | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/BrickPlaceholder.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/EnemyOutlineMat.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/EnemyOutlineMat.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/GhostBlockMat.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/GhostBlockMat.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/HeatDistortion_Mat.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/HeatDistortion_Mat.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/MetallicShipMaterial.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/MetallicShipMaterial.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/PropPlaceholder.mat` | material | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/PropPlaceholder.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/SelectOutlineMat.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/SelectOutlineMat.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/ShieldMat.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/ShieldMat.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/WeaponArcMaterial.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/WeaponArcMaterial.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/ghostPiece_Mat.mat` | material | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/ghostPiece_Mat.mat.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/hulls.obj` | mesh | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/hulls.obj.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/missile_placeholder.obj` | mesh | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/missile_placeholder.obj.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/projectileQuad.obj` | mesh | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/projectileQuad.obj.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/semi_sphere_shield.obj` | mesh | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/semi_sphere_shield.obj.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/shipVoxels.obj` | mesh | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/shipVoxels.obj.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/warpgate.obj` | mesh | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Meshes/warpgate.obj.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/README.md` | definition | own work | Project documentation; confirmation pending | Project contributor; confirmation pending | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/README.md.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/FresnelShader.shadergraph` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/FresnelShader.shadergraph.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/GhostBlockShader.shadergraph` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/GhostBlockShader.shadergraph.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/HeatDistortionShader.shadergraph` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/HeatDistortionShader.shadergraph.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/MetallicShipShader.shadergraph` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/MetallicShipShader.shadergraph.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/ShieldShader.shader` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/ShieldShader.shader.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/SunShader.shadergraph` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/SunShader.shadergraph.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/VoxelOutlineShader.shadergraph` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/VoxelOutlineShader.shadergraph.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/WeaponArcShader.shader` | shader | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Shaders/WeaponArcShader.shader.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickAlbedoArray.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickAlbedoArray.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickLayerParams.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickLayerParams.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickMaskArray.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickMaskArray.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickNormalArray.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickNormalArray.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/CrackTex.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/CrackTex.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/DamageRamp.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/DamageRamp.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/MacroNoiseTex.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/MacroNoiseTex.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PaintPalette.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PaintPalette.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropAlbedo.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropAlbedo.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropMask.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropMask.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropNormal.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropNormal.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/T_YFMeM_13_s.tga` | texture | - | Unresolved; reported Yughues Free Metal Materials | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/T_YFMeM_13_s.tga.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/ToonRamp.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/ToonRamp.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/cloud.png` | texture | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/cloud.png.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/metalpatern.png` | texture | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/metalpatern.png.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/noise_vhs.jpg` | texture | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/noise_vhs.jpg.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/sun_noise.png` | texture | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/sun_noise.png.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/thruster_alpha.png` | texture | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/thruster_alpha.png.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/VFX.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/VFX/GalaxyVFX.vfx` | vfx | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/VFX/GalaxyVFX.vfx.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/VFX/NebulaDustVFX.vfx` | vfx | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/VFX/NebulaDustVFX.vfx.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/VFX/SpaceDebrisVFX.vfx` | vfx | own work | Reported prototype work; confirmation pending | Unconfirmed prototype attribution | - | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/VFX/SpaceDebrisVFX.vfx.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/AudioProvenance.asset` | definition | own work | Project-authored; confirmation pending | Project contributor; confirmation pending | - | - | placeholder-only |
| `Assets/Game/Audio/AudioProvenance.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Mixer.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Mixer/GameMixer.mixer` | definition | own work | Project-authored; confirmation pending | Project contributor; confirmation pending | - | - | placeholder-only |
| `Assets/Game/Audio/Mixer/GameMixer.mixer.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/02 The Restricted Hangar.wav` | music | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/02 The Restricted Hangar.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/03 Promising Development.wav` | music | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/03 Promising Development.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/10 Boarding the Mechanism.wav` | music | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/10 Boarding the Mechanism.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/ComfyUI_00018_.mp3` | music | - | Unresolved; audio table status Blocked | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/ComfyUI_00018_.mp3.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/upbeat.mp3` | music | - | Unresolved; audio table status Blocked | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Music/upbeat.mp3.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/DSGNMisc_MOVEMENT-Mecha Large Takeoff_HY_PC-004.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/DSGNMisc_MOVEMENT-Mecha Large Takeoff_HY_PC-004.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/DSGNTonl_USABLE-Coin Toss_HY_PC-002.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/DSGNTonl_USABLE-Coin Toss_HY_PC-002.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/EXPLOSION-Cruncher_HY_PC-003.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/EXPLOSION-Cruncher_HY_PC-003.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/EXPLOSION-Cruncher_HY_PC-005.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/EXPLOSION-Cruncher_HY_PC-005.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Failure Stinger.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Failure Stinger.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Fantasy_Stinger_1.mp3` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Fantasy_Stinger_1.mp3.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/INTERFACE-MetallicClick.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/INTERFACE-MetallicClick.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/PROJECTILE-Laser Bursts_HY_PC-005.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/PROJECTILE-Laser Bursts_HY_PC-005.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Projectile_EXPLOSION-Bit Bomb_HY_PC-001.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Projectile_EXPLOSION-Bit Bomb_HY_PC-001.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Rooted_Dirt_break3_placeholder.ogg` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Rooted_Dirt_break3_placeholder.ogg.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Success Stinger.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/Success Stinger.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/UIClick_INTERFACE-OpenClose.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/UIClick_INTERFACE-OpenClose.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/UIHover_INTERFACE-Positive Click_HY_PC-002.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/UIHover_INTERFACE-Positive Click_HY_PC-002.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/explosion_hull_impact.wav` | sfx | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Audio/Sfx/explosion_hull_impact.wav.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxExplosionLargeTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxExplosionSmallTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxHitTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxMuzzleTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxShieldSparkTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/fx_particle_soft.png` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OFL.txt` | definition | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OFL.txt.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Bold.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Bold.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-BoldItalic.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-BoldItalic.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Bold_SDF.asset` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Bold_SDF.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Light.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Light.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-LightItalic.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-LightItalic.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Medium.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Medium.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-MediumItalic.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-MediumItalic.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Regular.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Regular.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-RegularItalic.ttf` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-RegularItalic.ttf.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Regular_SDF.asset` | font | http://scripts.sil.org/OFL | SIL OFL 1.1 | Adobe Systems; Nathan Rutzky; Paul D. Hunt | 2026-10-13 | docs/design/licenses/office-code-pro-ofl-1.1.txt | verified |
| `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Regular_SDF.asset.meta` | definition | - | Unresolved | Unknown | - | - | placeholder-only |
