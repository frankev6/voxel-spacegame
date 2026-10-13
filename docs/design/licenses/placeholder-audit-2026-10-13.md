# AST-003 repository evidence audit — 2026-10-13

This is an evidence inventory, not a licence or a written assignment of rights. No network, source download, owner receipt request, asset import/generation, Unity run, asset deletion or provenance-asset edit occurred. All runtime art, audio, font and importer bytes were preserved. The owner/lead instruction for this audit replaces AST-003's request to ask for receipts now: list unresolved evidence and continue without a new gate.

The exact inventory has 105 placeholder files (49 non-meta, 56 metadata) and 45 audio files (21 non-meta, 24 metadata). Every one has exactly one register row. There are 178 register rows in total: 11 verified Office Code Pro font/proof-text rows, two remove rows for Kontrapunkt/font metadata, and 165 placeholder-only rows. The additional rows comprise 22 Office Code Pro family files and six preserved VFX-004 records outside the two scanned roots. A row with an unresolved status is complete bookkeeping, not a verified asset.

## Retained evidence

- `Assets/Game/Art/_Placeholder/README.md` reports prototype own-work meshes/materials/shaders and describes mixed texture sourcing; it does not contain Frank's dated rights confirmation. `docs/reference/audit-2026-10/00-synthesis.md` §5/§7 and `07-project-health.md` describe retained assets and original pack names, not per-file licence proofs. The synthesis's old "keep Kontrapunkt" suggestion is superseded by binding DESIGN-SYSTEM §10.19 / SPEC-81 AST-003: the font has no licence file and is marked remove.
- Existing REN-008/VFX-004 register assertions, including original creation dates, are retained unchanged as placeholder-only. Source generators and the approved palette demonstrate process, not an actual assignment of rights. No owner/contributor confirmation date was located in the cited evidence.
- `Assets/Game/Audio/AudioProvenance.asset` (AUD-010 commit `8879cfa`) names all 19 current clip GUIDs: 17 status 0 Unverified, two status 2 Blocked. No entry is Confirmed. SPEC-64 names the same enum. The audit leaves this asset, the release build guard and all clips unchanged; register placeholder-only does not clear a Blocked entry.
- `Assets/Game/Audio/Mixer/GameMixer.mixer` is project-authored (AUD-002 commit `d1d654c`); the provenance asset is project bookkeeping. That creation evidence is retained without inferring a rights confirmation. Metadata and the placeholder README likewise remain separately recorded, unresolved project bookkeeping.
- No tracked filename contains "Starsector" in the current checkout. The old audit's Starsector filenames and legacy reference strings are not current asset files. This bounded name scan does not certify the unknown icon sheet or textures' origin. Any subsequently found prohibited Starsector file must receive remove status; no absent file was invented as a current row.

## Office Code Pro proof scope

`office-code-pro-ofl-1.1.txt` is copied byte-for-byte from `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OFL.txt`; neither that bundled licence nor the fonts changed. The licence names Office Code Pro, Nathan Rutzky and Adobe, states SIL OFL v1.1 and provides `http://scripts.sil.org/OFL`. The date 2026-10-13 is this local examination/copy date; the original acquisition date is not known. No new upstream source URL was invented or visited.

Each font's embedded name table declares family Office Code Pro, licence SIL Open Font License, copyright 2015 Adobe & Nathan Rutzky and designers Nathan Rutzky & Paul D. Hunt:

- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Bold.ttf` — Bold
- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-BoldItalic.ttf` — Bold Italic
- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Light.ttf` — Light
- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-LightItalic.ttf` — Light Italic
- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Medium.ttf` — Medium
- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-MediumItalic.ttf` — Medium Italic
- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-Regular.ttf` — Regular
- `Assets/Game/UI/Theme/Fonts/OfficeCodePro/OfficeCodePro-RegularItalic.ttf` — Regular Italic

The Regular and Bold SDF assets record family Office Code Pro and source GUIDs matching their corresponding TTF metadata. The OFL proof covers the eight TTFs, those two font-data derivatives and the bundled licence text. Their companion/folder metadata is not independently licensed by that proof and remains placeholder-only. The audit makes no change to theme fonts or any production design decision.

## Exact unresolved clip evidence

Source/author, the applicable commercial-game use/redistribution terms, and a file-scoped receipt/licence/permission are absent for every current clip. `_HY_PC` filenames are a pack clue only; they do not establish a vendor, purchase or licence. ComfyUI has the pre-existing AI provenance concern; its exact-asset approval and tool terms are absent. Upbeat remains source/AI-status unresolved and Blocked in the existing table.

- `Assets/Game/Audio/Music/02 The Restricted Hangar.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Music/03 Promising Development.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Music/10 Boarding the Mechanism.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Music/ComfyUI_00018_.mp3` — Blocked; existing note: AI-generated-looking / unknown origin, awaiting owner (AUD-011).
- `Assets/Game/Audio/Music/upbeat.mp3` — Blocked; existing note: AI-generated-looking / unknown origin, awaiting owner (AUD-011).
- `Assets/Game/Audio/Sfx/DSGNMisc_MOVEMENT-Mecha Large Takeoff_HY_PC-004.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/DSGNTonl_USABLE-Coin Toss_HY_PC-002.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/EXPLOSION-Cruncher_HY_PC-003.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/EXPLOSION-Cruncher_HY_PC-005.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/Failure Stinger.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/Fantasy_Stinger_1.mp3` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/INTERFACE-MetallicClick.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/PROJECTILE-Laser Bursts_HY_PC-005.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/Projectile_EXPLOSION-Bit Bomb_HY_PC-001.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/Rooted_Dirt_break3_placeholder.ogg` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/Success Stinger.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/UIClick_INTERFACE-OpenClose.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/UIHover_INTERFACE-Positive Click_HY_PC-002.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).
- `Assets/Game/Audio/Sfx/explosion_hull_impact.wav` — Unverified; existing note: Awaiting owner licence confirmation (AUD-011).

## Exact unresolved visual/bookkeeping evidence

No qualifying source/terms were found for the following seven carried-over image files. The Yughues product attribution is from the placeholder README/audit and is not a verified licence:

- `Assets/Game/Art/_Placeholder/Icons/item_icons_sheet.png`
- `Assets/Game/Art/_Placeholder/Textures/T_YFMeM_13_s.tga`
- `Assets/Game/Art/_Placeholder/Textures/cloud.png`
- `Assets/Game/Art/_Placeholder/Textures/metalpatern.png`
- `Assets/Game/Art/_Placeholder/Textures/noise_vhs.jpg`
- `Assets/Game/Art/_Placeholder/Textures/sun_noise.png`
- `Assets/Game/Art/_Placeholder/Textures/thruster_alpha.png`

The following 43 non-meta art/audio/bookkeeping files retain reported own-work or project-authored evidence, but require an actual dated owner/contributor confirmation of authorship/rights before verification. The generated texture creation dates in the register are not that confirmation:

- `Assets/Game/Art/_Placeholder/Materials/AllyOutlineMat.mat`
- `Assets/Game/Art/_Placeholder/Materials/BrickPlaceholder.mat`
- `Assets/Game/Art/_Placeholder/Materials/EnemyOutlineMat.mat`
- `Assets/Game/Art/_Placeholder/Materials/GhostBlockMat.mat`
- `Assets/Game/Art/_Placeholder/Materials/HeatDistortion_Mat.mat`
- `Assets/Game/Art/_Placeholder/Materials/MetallicShipMaterial.mat`
- `Assets/Game/Art/_Placeholder/Materials/PropPlaceholder.mat`
- `Assets/Game/Art/_Placeholder/Materials/SelectOutlineMat.mat`
- `Assets/Game/Art/_Placeholder/Materials/ShieldMat.mat`
- `Assets/Game/Art/_Placeholder/Materials/WeaponArcMaterial.mat`
- `Assets/Game/Art/_Placeholder/Materials/ghostPiece_Mat.mat`
- `Assets/Game/Art/_Placeholder/Meshes/hulls.obj`
- `Assets/Game/Art/_Placeholder/Meshes/missile_placeholder.obj`
- `Assets/Game/Art/_Placeholder/Meshes/projectileQuad.obj`
- `Assets/Game/Art/_Placeholder/Meshes/semi_sphere_shield.obj`
- `Assets/Game/Art/_Placeholder/Meshes/shipVoxels.obj`
- `Assets/Game/Art/_Placeholder/Meshes/warpgate.obj`
- `Assets/Game/Art/_Placeholder/README.md`
- `Assets/Game/Art/_Placeholder/Shaders/FresnelShader.shadergraph`
- `Assets/Game/Art/_Placeholder/Shaders/GhostBlockShader.shadergraph`
- `Assets/Game/Art/_Placeholder/Shaders/HeatDistortionShader.shadergraph`
- `Assets/Game/Art/_Placeholder/Shaders/MetallicShipShader.shadergraph`
- `Assets/Game/Art/_Placeholder/Shaders/ShieldShader.shader`
- `Assets/Game/Art/_Placeholder/Shaders/SunShader.shadergraph`
- `Assets/Game/Art/_Placeholder/Shaders/VoxelOutlineShader.shadergraph`
- `Assets/Game/Art/_Placeholder/Shaders/WeaponArcShader.shader`
- `Assets/Game/Art/_Placeholder/Textures/BrickAlbedoArray.asset`
- `Assets/Game/Art/_Placeholder/Textures/BrickLayerParams.asset`
- `Assets/Game/Art/_Placeholder/Textures/BrickMaskArray.asset`
- `Assets/Game/Art/_Placeholder/Textures/BrickNormalArray.asset`
- `Assets/Game/Art/_Placeholder/Textures/CrackTex.asset`
- `Assets/Game/Art/_Placeholder/Textures/DamageRamp.asset`
- `Assets/Game/Art/_Placeholder/Textures/MacroNoiseTex.asset`
- `Assets/Game/Art/_Placeholder/Textures/PaintPalette.asset`
- `Assets/Game/Art/_Placeholder/Textures/PropAlbedo.asset`
- `Assets/Game/Art/_Placeholder/Textures/PropMask.asset`
- `Assets/Game/Art/_Placeholder/Textures/PropNormal.asset`
- `Assets/Game/Art/_Placeholder/Textures/ToonRamp.asset`
- `Assets/Game/Art/_Placeholder/VFX/GalaxyVFX.vfx`
- `Assets/Game/Art/_Placeholder/VFX/NebulaDustVFX.vfx`
- `Assets/Game/Art/_Placeholder/VFX/SpaceDebrisVFX.vfx`
- `Assets/Game/Audio/AudioProvenance.asset`
- `Assets/Game/Audio/Mixer/GameMixer.mixer`

All 80 metadata files in the inventory are recorded alongside their associated family evidence gaps. Kontrapunkt's companion metadata is remove; the other 79 are placeholder-only. Their exact paths and hashes are in the CSV. Office Code Pro's 11 metadata files remain placeholder-only as distinct inventory rows. A single actual confirmation/proof can cover a clearly scoped family, including metadata; no separate per-file approvals are requested. Existing VFX-004 extra records retain their prior placeholder-only status; generated files absent in this worktree were not represented as newly produced.

## AST-002 integration

Use the exact eight-column LICENSES table as the canonical per-path status register. The inventory CSV is readback evidence: `path`, `kind`, `sha256`, `guid`, `status`, `evidence`. Metadata rows use definition kind; paths are case-sensitive and include spaces. For placeholder audio scanning, take the 19 register/inventory paths under `Assets/Game/Audio/` whose extensions are `.wav`, `.mp3` or `.ogg`, and their companion metadata GUIDs. Exclude AudioProvenance and GameMixer from the clip set. All 19 remain unresolved; the CSV provides the exact GUID mapping without changing the existing AUD-010 table.

There are zero missing rows for either audited root. This is not an AST-E007 "all verified" result: placeholder-only/remove rows still cause the specified A1 warnings/A2 errors. Office Code Pro licensing verification does not approve it as a final NOCTURNE production font. AST-002's Unity validator was not run by this worker; no new tests were added, and lead integration must verify its parser and status/placeholder-reference behavior. Do not broaden AST-003 into purchases, owner questions, asset deletion or a release-approval request.
