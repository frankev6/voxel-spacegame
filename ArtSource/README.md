# Art sources and production plan

This folder holds editable sources for SPEC-81. AST-001 creates bookkeeping only: no models, images, fonts or audio were produced or downloaded. Existing LookDev and StyleTest sources remain in place. The binding look is [DESIGN-GUIDE.md](../docs/design/DESIGN-GUIDE.md); DESIGN-SYSTEM §10 supplies the shared technical rules.

| Source category | Planned source folder | Imported output |
|---|---|---|
| Brick layers and support maps | `Bricks/` (`.mmg`, `.blend`, texture masters) | `Assets/Game/Art/Bricks/Layers/`, `Arrays/`, support textures and shared brick material |
| Trim sheet | `Props/` (trim master and `trim_layout.md`) | `Assets/Game/Art/Props/Textures/` and `M_Prop_Trim.mat` |
| Functional blocks and detail props | `Blocks/` (`<content_id>.blend`) | `Assets/Game/Art/Props/Meshes/SM_<content_id>[_<Part>]_LOD<n>.fbx` |
| Space bodies and gates | `Space/` | `Assets/Game/Art/Space/` and art-owned shaders in `Assets/Game/Art/Shaders/` |
| VFX and overlays | `VFX/`, `Overlays/` | `Assets/Game/Art/VFX/`, `Overlays/` and shared shader assets |
| System glyphs and crests | `Glyphs/` (SVG masters) | `Assets/Game/UI/Theme/Glyphs/` |
| Rendered/drawn icons | `Icons/` (256-pixel masters) | `Assets/Game/UI/Theme/Icons/` (128-pixel sprites) |
| UI artwork and cursors | `UI/`, `Cursors/` | `Assets/Game/UI/Theme/Sprites/`, `Cursors/` |
| Audio masters | `Audio/` (24-bit WAV masters) | `Assets/Game/Audio/Sfx/`, `Music/`, `Ambience/` |

Runtime definitions belong under `Assets/Game/Content/`, and definition classes remain under `Assets/Game/Data/Definitions/`. Fonts, their OFL text, theme styles and UI templates belong under `Assets/Game/UI/Theme/`; SPEC-60's canonical theme is `Game.tss` with lowercase `components/`. Font families remain Michroma, Inter and JetBrains Mono; each has its own planned Fonts/<Family>/ folder with the unmodified OFL.txt alongside its sources. Sources never go under `Assets/`.

[asset-manifest.csv](../docs/design/asset-manifest.csv) records the complete planned output inventory for AST-004–032. Its nine columns are the SPEC-81 contract. Every row is `planned`, including rows for future edits of existing assets. A planned path is not evidence that the file exists, is approved, or has a verified licence. `source` is an intended sourcing route and `licence` an intended licence (or `-`), never a verification claim. The separate [licence register](../docs/design/LICENSES.md) tracks actual evidence.

The manifest keys physical outputs by `path`, with one row per shared output and its earliest producing task. `asset_id` is the filename stem and can repeat for different file types/folders; content IDs remain canonical. `definition` includes authored Unity text definitions (content assets, theme sheets, UXML and scene composition), so no new manifest kind is required. Source masters, tool code, receipts and review captures are documented by their producing tasks rather than counted as shipped art outputs. Wildcards expand to named files; no wildcard is a deliverable row.

The inventory reserves the minimum specified variation count: two variations for the specified UI/editor sounds and each combat cue; four combat stem files represent two base/intensity pairs. Both LODs are listed, with moving-part triangle reservations split within the whole-block budget. These are inventory/budget reservations, not geometry or visual decisions. Copy layers still have a planned named source for each channel; reserved slices are baker copies and need no invented source files. NOCTURNE does not require PULP's ToonRamp.

TODO(AST-004): obtain actual font proofs and resolve the proposed body-strong token before wiring it. TODO(AST-014): extend station-blueprint rows when registered station blueprints exist; the current procedural firing target is not an authored blueprint. TODO(AST-018): confirm producers/kinds for hulk sparks and edge embers before writing FxDefinitions. TODO(AST-030): resolve any new cue aliases with SPEC-64's config while preserving existing cue IDs. TODO(AST-032): confirm actual music sourcing and stem/stinger playback before authoring; the CC0 plan does not authorize a purchase or certify a file.

Binary masters (`.blend`, `.kra`, `.mmg`, `.psd`, `.wav`, `.exr`) use local `binary` attributes. SVG, shader, script and documentation sources stay text. Git LFS filters remain commented under ADR-0008; the lead/owner must migrate all binary patterns, including source masters, before the first authorized push. AST-001 performs no LFS or remote operation.
