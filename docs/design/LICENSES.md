# Asset provenance register

SPEC-81 §4.3 register. AST-001 preserves the prior REN-008/VFX-004 authorship evidence below and normalizes its table; it does not audit or confirm licences. Dates are the previously recorded creation dates, not new licence confirmation dates. All retained entries remain `placeholder-only` pending AST-003's actual rights/proof review. No planned asset is listed as produced or verified.

REN-008's existing evidence: the procedural generator is `Assets/Game/Editor/Voxel/VoxelPlaceholderArtBuilder.cs`, with ADR-0040 and the approved DESIGN-GUIDE palette. The prior register states that no downloaded image or image-generation model was used. BrickPlaceholder and PropPlaceholder reference those generated textures. VFX-004's existing evidence: its own 64×64 radial alpha texture and five authored template graphs, generator reference `Logs/playtest/vfx004/generate.cs` and DESIGN-GUIDE §9 colours. The grouped template entry is split into exact paths below; later Debris/Warp templates await AST-003 inventory. This preserves the old provenance assertions without claiming contributor rights were confirmed.

`verified` requires actual proof or an actual own-work confirmation; `placeholder-only` awaits it, and `remove` identifies material that cannot ship. Proof files belong in [licenses/](licenses/README.md). Planned sourcing in [asset-manifest.csv](asset-manifest.csv) is not a licence claim.

| Path | Kind | Source | Licence | Author | Date | Proof | Status |
|---|---|---|---|---|---|---|---|
| `Assets/Game/Art/_Placeholder/Textures/BrickAlbedoArray.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickLayerParams.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickMaskArray.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/BrickNormalArray.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/CrackTex.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/DamageRamp.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/MacroNoiseTex.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PaintPalette.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropAlbedo.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropMask.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/PropNormal.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Textures/ToonRamp.asset` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/BrickPlaceholder.mat` | material | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Art/_Placeholder/Materials/PropPlaceholder.mat` | material | own work | Original project work | Codex GPT-6, for Frank | 2026-10-11 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/fx_particle_soft.png` | texture | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxHitTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxMuzzleTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxExplosionSmallTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxExplosionLargeTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |
| `Assets/Game/Presentation/Vfx/Templates/FxShieldSparkTemplate.vfx` | vfx | own work | Original project work | Codex GPT-6, for Frank | 2026-10-12 | - | placeholder-only |

TODO(AST-003): inventory every carried-over file, preserve historical evidence, obtain the necessary confirmations/proofs and record the truthful status per asset. AST-001 creates no new verified rows.
