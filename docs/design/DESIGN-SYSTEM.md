# Design system and art direction

| | |
|---|---|
| **Status** | **PROPOSAL, superseded as the source of truth by [DESIGN-GUIDE.md](DESIGN-GUIDE.md)**, approved 2026-10-11 (NOCTURNE as rendered in the look book). The direction sections (§5–§9) are reference only; §4 and §10–§11 stay binding through the guide (DESIGN-GUIDE §0.3) |
| **Owner** | Frank |
| **Process spec** | [SPEC-80 Art direction and design system](../specs/SPEC-80-art-direction.md) (area `ART`) |
| **Production spec** | [SPEC-81 Asset production](../specs/SPEC-81-asset-production.md) (area `AST`), blocked behind the gate `ART-013` |
| **Binding inputs** | [00-vision](../00-vision.md) pillars, [02-conventions §4–§5](../02-conventions.md), [ADR-0009](../decisions/ADR-0009-placeholder-art-and-design-gate.md), [SPEC-04 §4.5](../specs/SPEC-04-content-and-blocks.md) (`materialLayer`, `propMesh`, `icon`) |
| **Last edited** | 2026-10-10 |

This document proposes **three complete art directions** for ships, blocks, space, VFX, UI, icons, typography, motion and audio mood, recommends one, and fixes the **shared rules** that hold whichever direction is chosen. When the owner approves (gate `ART-013`), the chosen direction becomes **binding**, the two others move to Appendix A for the record, and SPEC-81 production starts. Until then every asset in the game stays placeholder ([ADR-0009](../decisions/ADR-0009-placeholder-art-and-design-gate.md)) and **no production art may be made or committed**.

Words such as *cell*, *brick*, *block*, *grid*, *hulk*, *debris particle*, *chunk body*, *palette* (per-grid table) follow [03-glossary](../03-glossary.md). This document adds four terms (proposed for the glossary, see SPEC-80 §10): **paint palette** (the global table that turns a `PaletteEntry.Color` byte into a colour), **team colour slot** (a paint-palette index resolved per grid from its faction), **function colour** (the colour that marks what a functional block does) and **brick layer** (one slice of the brick texture array, selected by `BlockDefinition.materialLayer`).

---

## 1. How to review and approve

### 1.1 What to read, in order (about 30 minutes)

1. §2 *What we are moving away from* (2 min) and §3 *Principles* (3 min).
2. The **pitch** and **palette** of each direction: §5.1–§5.3, §6.1–§6.3, §7.1–§7.3 (10 min).
3. §8 side-by-side token table and scorecard, §9 recommendation (5 min).
4. Skim the rest of each direction for anything you dislike; write it down in the block below.
5. Later, when SPEC-80 has produced them: the interactive board `docs/design/board/index.html` (ART-005), the block-kit renders (ART-004) and the in-engine look tests (ART-009/010). The final approval is given on the look tests, not on this text alone.

### 1.2 The three owner decisions

| Step | Task | What you decide | What happens next |
|---|---|---|---|
| 1 | `ART-006` Shortlist | Pick **two finalist directions** (or one, or ask for a fourth). You may borrow named elements between directions | Workers build look-test scenes for the finalists only |
| 2 | `ART-011` Look-test review | Compare the finalists in engine; list changes | Workers revise once and finalize this document for one direction |
| 3 | `ART-013` **GATE** | Approve the design system | SPEC-81 production (A1) starts; tokens get their final values |

### 1.3 Approval block (owner fills in)

```
SHORTLIST (ART-006)
  Finalists:      [ ] DRYDOCK   [ ] NOCTURNE   [ ] PULP HORIZON   [ ] other: __________
  Date:           ____-__-__

APPROVAL (ART-013)
  Chosen direction:                         ______________________
  Elements borrowed from other directions   (section numbers, e.g. "§6.7 shield ripple"):
    __________________________________________________________
  Changes requested (numbered; each becomes a Decision log row in §12):
    1. ______________________________________________________
    2. ______________________________________________________
    3. ______________________________________________________
  Fonts approved:  display ______________  body ______________  mono ______________
  Approved on:     ____-__-__
  Signature:       Frank ______________________
```

### 1.4 What approval unlocks, and what it does not

**Unlocks:**
- Every SPEC-81 task with milestone `A1` (block kit, brick texture array, materials, VFX, UI kit, icons, cursors, fonts, audio pass).
- `ART-014`: the chosen direction's values are written into `Assets/Game/UI/Theme/tokens.uss` and the chosen `ArtDirectionDefinition` becomes the active one.
- This document's status changes to **BINDING (approved YYYY-MM-DD)**; §5/§6/§7 of the non-chosen directions move to Appendix A unchanged.

**Does not unlock:**
- New third-party art. Only the fonts listed for the chosen direction (all SIL Open Font License 1.1) and audio that passes §10.19 may enter the project.
- Renaming any binding token (§10.1) or any SPEC-04 contract field.
- Any change to gameplay, scope or performance budgets.

Changing the approved direction later requires a new row in §12 signed by the owner and a re-run of the look test (`ART-009`/`ART-010` procedure).

---

## 2. What we are moving away from

The audit screenshots (`docs/reference/audit-2026-10/screenshots/`) show the placeholder look. Each observation became a rule.

| Observation (screenshot) | Why it hurts | Rule in this document |
|---|---|---|
| A bright salmon-orange nebula fills most of the frame behind the ship (`05-flying`, `11-firing`, `13-ai-fight`) | Ships, debris and the station lose contrast; the eye goes to the background | Backdrop luminance cap (§10.3, rule L1) |
| The ARK station reads as a flat beige silhouette with no lit or shadow side (`06-ark-station`, `07-ark-dialog`) | Fog and flat ambient remove form; a 300 m structure looks like a decal | One hard key light pitched above the plane, a fill light, no fog (§10.16) |
| The player ship is about 10 % of the screen width and its red/grey colours mean nothing | Block function is not readable | Function colours + function glyphs + silhouettes (§3 P3, §10.6) |
| Five UI vocabularies at once: rounded grey cards with white bevel buttons (`01-start`), cyan sci-fi slot frames (`22-inventory`), slanted hazard bars (`05-flying`), navy grid panels (`23-fleet`), painted illustrations (`03-galaxy-start`) | No coherence | One token set (§8.1), one component kit (SPEC-81 `AST-022`), one icon grid (§10.5) |
| Item icons are outlined pixel art at mixed scales (`22-inventory`, `09-trade`) | Clashes with vector UI, unreadable at 32 px | One icon style per direction; block icons rendered from the real block assets (SPEC-81 `AST-026`) |
| Every beam is the same white line (`18-12-enemies`) | Weapons and damage types are indistinguishable | VFX colour by damage type and weapon family; core/halo structure (§5.7, §6.7, §7.7) |
| Debris cubes are pale beige whatever they came from (`11-firing`) | Destruction does not read as "my ship lost those blocks" | Debris inherits the cell's paint colour and starts hot (§10.17) |
| HUD texts overlap ("AUTOFIRE" over "FUEL"), stats sit on top of each other (`23-fleet`) | Unreadable | Fixed HUD zones (§10.4.2) and layout tests |

**Kept from the prototype:** number-key dialog choices (`[1] Visit Marketplace`), cube debris, segmented resource bars as an idea, the calm dark-navy screen backgrounds, NORA as a text-first companion.

---

## 3. Design principles

These derive from the pillars in [00-vision](../00-vision.md) in the same priority order. When two rules conflict, the earlier principle wins.

**P1 — Readability first (pillar 1).** The screen has a fixed value hierarchy, darkest to brightest: space backdrop < hull shadow side < hull lit side < function lights < hit flashes and explosions < critical UI alerts. Measurable limits are in §10.3. The backdrop is never brighter than the ships on it.

**P2 — Destruction you can read (pillar 1).** Every damage stage is visible at 150 m as a change of value or hue (§10.14 damage stages). A removed cell leaves faces that look different from original hull (exposed internals). Debris inherits the colour of the cell it came from. A ship that loses its core is visibly dead within 1 second: its lights go out and its colours dull. Nothing pops out of existence: chunk bodies dissolve over 1.2 s, debris shrinks to zero.

**P3 — Block function is legible by silhouette, glyph and colour (pillar 2).** In that order of robustness. Every functional block has a distinct mesh silhouette, a function glyph (§10.6) and a function colour. At 300 m the function colour/light must read; at 150 m the glyph on blocks at least 2 cells wide, and silhouette plus function colour/light on 1-cell-wide blocks (R1); at 50 m every glyph and the details. Colour alone never carries meaning (colour-blind safety, §10.2): where a 1-cell block shows only its colour, its silhouette and the HUD target marker carry the type.

**P4 — Massive scale is shown, not told (pillar 3).** Detail is tied to the 0.5 m cell, so a 300 m station shows 600 cells of grain across and reads as huge. Scale cues are mandatory on every structure over 100 m: navigation lights, hull numbers, window/light strips, docking markings. No tilt-shift, no depth of field, no toy-like specular highlights.

**P5 — Mystery mood (pillar 4).** Human technology is legible and familiar. Everything alien — the Vanguard, the Silence, the Whisper — deliberately breaks the human visual language (different light colour, different geometry rules, different sound). Darkness is a narrative tool used on purpose, never a readability tax on combat.

**P6 — One coherent system (pillar 5).** One token set, one brick material, one prop material, one VFX grammar, one icon grid, one type ramp. Every asset can be traced to a section of this document.

**P7 — Cost discipline.** The project has a solo developer or a small team. Detail goes where the camera sees it (top faces, 50–150 m). Procedural or parametric production is preferred. Block icons are rendered, not drawn. All prop meshes share one material and one trim sheet.

---

## 4. Reference viewing conditions

All readability numbers in this document assume the **reference camera** below. SPEC-62 owns the real camera; if its values change, `ART-010` re-measures the captures but the rules (in cells and pixels) stay.

| Parameter | Reference value |
|---|---|
| Resolution | 1920 × 1080, UI reference resolution 1920 × 1080, match 0.5 |
| Vertical field of view | 40° |
| Camera pitch | 55° below the flight plane (looking down) |
| Distances | 50 m (close), 150 m (default combat), 300 m (zoomed out); Blueprint Lab close-up 5 m |

Size of one 0.5 m cell at the centre of the screen:

| Camera distance | Visible height | Pixels per metre | **Pixels per cell** | What can be read |
|---|---|---|---|---|
| 5 m (editor) | 3.6 m | 297 | 148 | Texture detail: bolts, seams, stencils |
| 50 m | 36.4 m | 29.7 | 14.8 | Panel seams, glyphs on 1-cell faces, damage stages |
| 150 m | 109 m | 9.9 | 4.9 | Glyphs on blocks ≥ 2 cells, function colour, cracks as texture |
| 300 m | 218 m | 4.95 | 2.5 | Silhouettes, function lights, damage as value change |

Derived rules (all directions):

- **R1 (4-pixel rule).** Anything the player must read in combat is at least 4 px at 150 m, which means **at least 2 cells (1 m)** on its smallest axis. Single-cell functional blocks therefore use their whole visible face for the glyph. Even so, a 1-cell face is about 5 px at 150 m, so on 1-cell-wide blocks the glyph is a **50 m feature**; at 150 m they read by silhouette and function colour/light (§12 row 1).
- **R2 (detail budget).** Texture detail finer than 1/8 cell (6 cm) is visible only in the Blueprint Lab. It may take at most 15 % of texture authoring time.
- **R3 (top faces).** At 55° pitch, faces pointing up (+Y world) cover about 70 % of the visible hull area. Because blocks rotate (24 orientations), function signifiers must be on **every** face of a block that can be exposed, and are judged from above.
- **R4 (key light above the plane).** Stars sit in the flight plane, so a light pointing from the star would graze the top faces (N·L ≈ 0) and leave them dark. The key light is therefore always **pitched downward** by the direction's key pitch (35° DRYDOCK, 25° NOCTURNE, 45° PULP HORIZON); its azimuth follows the star (§10.16).

---

## 5. Direction A — DRYDOCK

*Industrial used-future. Bone and graphite plating, safety-coded systems, stencils and navigation lights; black space; the alien is clean and wrong.*

### 5.1 Pitch

Working ships in a working universe. The ARK expedition's ships are industrial machines: bone-white and graphite plates, systems marked in safety colours, stencilled numbers, red and green navigation lights blinking along hundreds of metres of hull. Everything human was built to be repaired — and you can see how it was built, because when a hull is cut open you see grey girders and copper conduits inside, scorched and glowing at the edges. Light is hard and unflattering, like photographs of spacewalks; space is black and sparse. Against this grounded vocabulary the alien is a shock: Vanguard ceramic that never weathers and glows with warm gold light, and the Silence — matte shapes that reflect nothing, fringed with violet "black light", that erase what they touch.

### 5.2 Touchstones (described in words; no copyrighted images are stored in the repo)

- Ron Cobb's industrial spacecraft design for *Alien* (1979): utilitarian layering, stencils, worn paint.
- NASA and ESA hardware photography (ISS trusses, Apollo service module, Shuttle payload bays) — public-domain reference, usable in mood boards.
- *The Expanse* (TV): practical ships, colour-coded interiors, believable scale.
- *Hardspace: Shipbreaker*: industrial ships that are readable while being cut apart; safety colour coding.
- *Homeworld: Deserts of Kharak*: weathered industrial palettes under a single hard light.
- *2001: A Space Odyssey*: the monolith as the template for "clean and wrong" alien objects.
- Safety colour conventions (ISO 3864 safety colours, aviation and maritime navigation lights): players already read them without being taught.

### 5.3 Palette

**Space**

| Element | Value | Rule |
|---|---|---|
| Backdrop base | `#040507` | Relative luminance 0.0015 |
| Stars | 3 tiers: bright (≤ 40 per sky, 2–3 px), mid (about 2,000, 1–2 px), faint (dense, 1 px at 30 % opacity) | Spectral tint at **60 % saturation** of the shared spectral table (§10.16) |
| Galaxy band | Peak `#2A2622` | Luminance ≤ 0.02 |
| Nebula dust | Warm `#1C1510`, cool `#0C121B` | Luminance ≤ 0.02; covers ≤ 30 % of the sky; low-frequency, no bright cores |
| Motion dust (VFX) | Motes 0.2–0.6 m, `#8A8478` at 25 % opacity | 3 parallax layers 20 m, 60 m and 150 m below the flight plane |

**Hull neutrals** (albedo, sRGB)

| Name | Hex | Metallic | Smoothness | Use |
|---|---|---|---|---|
| Bone | `#CFC8B8` | 0.0 | 0.35 | Default plates; ARK team primary |
| Ash | `#A6A6A0` | 0.0 | 0.40 | Secondary plates |
| Gunmetal | `#6C7075` | 0.6 | 0.45 | Armour, heavy plates |
| Graphite | `#3B3E42` | 0.3 | 0.35 | Frames, trims, girders |
| Bare steel | `#9A9C9E` | 1.0 | 0.55 | Unpainted metal, worn edges, scratches |
| Cavity | `#1E2023` | 0.0 | 0.20 | Interior background behind exposed internals |

**Factions** (team colour slots, §10.13)

| Faction (content ID) | Primary (slot 17) | Secondary (slot 18) | Accent (slot 19) | Faction light (emissive) |
|---|---|---|---|---|
| ARK expedition — player default (`faction_ark`, proposed) | Bone `#CFC8B8` | Graphite `#3B3E42` | Safety Yellow `#F2B705` | Work-light `#FFE2B0` |
| Vestari (`faction_vestari`) | Pearl `#E4E9EE` | Azure `#2F7FD0` | Ice `#9FD8FF` | Ice `#9FD8FF` |
| Khuchad Clan (`faction_khuchad_clan`) | Rust `#A8471E` | Soot `#2B2522` | Ember `#FF7A2F` | Ember `#FF7A2F` |
| Tlaxcala / Triple Alliance (`faction_tlaxcala`, `faction_4`) | Corporate Green `#1F7A4D` | Ivory `#E6E0CF` | Mint `#5BE3A1` | Mint `#5BE3A1` |
| Scavengers (pirates, the Scavenger King) | Primer Grey `#5B5E5A` | Oxide Red `#8E2B25` | Mismatch Yellow `#D9B23A` | Sodium `#FFB347` |
| Vanguard (precursor derelicts) | Ceramic `#ECE7DC` | Glass Black `#101317` | Precursor Gold `#FFC86B` | Gold `#FFC86B` |
| Silence | Void `#060508` | Void `#060508` | Black-light Violet `#6E3BFF` | Violet `#6E3BFF` |
| Xeno probes | Chitin `#2E2A33` | Chitin Dark `#1A171E` | Acid `#C8FF2E` | Acid `#C8FF2E` |

**Function colours** (paint-palette indices 20–25, §10.13; glyphs §10.6)

| Function (`BlockCategory`) | Colour | Emissive intensity (§10.14 units) | Where it appears |
|---|---|---|---|
| Propulsion (thrusters) | Amber `#FF9A1F` | 0.6 idle → 3.0 full thrust | Nozzle ring, 1-cell band around the mount face |
| Weapon | Signal Red `#E5332A` | 2.0, pulses to 3.0 while charging | Muzzle ring, mount band |
| Power (reactors, capacitors) | Volt Yellow `#F5D33A` | 2.5 | Vents, top panel |
| Shield (shield emitters) | Shield Blue `#3D8BFF` | 2.5 | Emitter dish rim |
| Core | Command Green `#3DDC84` | 3.0, pulsing at 0.5 Hz | Beacon dome, top panel |
| Utility (cargo; later hangars and other utility modules) | Utility Magenta `#E04AC8` | 1.5 | Hatch frame |
| Structure, Detail | none | 0 | Paint only |

**Damage and heat** (stages are defined in §10.14)

| Stage | Look |
|---|---|
| 0 Clean (HP ≥ 75 %) | As authored |
| 1 Scuffed (50–75 %) | Albedo moved 30 % toward Scorch `#3A332C`; bare-steel edge wear +40 %; smoothness −0.10 |
| 2 Cracked (25–50 %) | Crack mask at 50 %; scorch 60 %; crack lines Char `#141210` |
| 3 Critical (< 25 %) | Crack mask 100 %; char 85 %; embers in cracks Ember `#FF5A1A` at intensity 4.0, fading to 0 over 8 s after the grid's last hit |
| Fresh cut | Newly exposed faces use the exposed-internals layers (girders Graphite, conduits Copper `#B8862E`, cavity `#1E2023`); VFX edge glow `#FFB066` at 6.0 for 1.5 s |
| Hulk | `_GridPowered` 1 → 0 over 0.6 s (all function lights off); albedo desaturated 30 % and ×0.85; spark VFX every 2–6 s for 60 s |

**Otherness**

| Thing | Look |
|---|---|
| Vanguard structures | Ceramic `#ECE7DC`, smoothness 0.85, never scorches (no damage ramp; cracks only, glowing Precursor Gold `#FFC86B` at 2.0). Plates use chevron and fluted patterns — never the human 1 m square seam |
| Silence | Albedo Void `#060508`, smoothness 0, metallic 0, ignores AO lightening; fresnel rim Black-light Violet `#6E3BFF` at grazing angles (rim strength 0.7). Silence damage erases: cells vanish with a white `#F4F1FF` flash and leave **no debris** |
| The Whisper signal | UI and VFX only: a thin sine ribbon `#BFF6FF` |

### 5.4 Lighting and space environment

| Item | Value |
|---|---|
| Key light | One directional light per star system. Colour = star spectral tint at 60 % saturation. Intensity 2.0. **Pitch 35° below the plane**, azimuth from the star (R4). Shadows on |
| Fill light | Directional, **no shadows**, intensity 0.3 (15 % of key), colour `#8FA6C8`, azimuth opposite the key, pitch 50° below the plane |
| Ambient | Flat colour `#14171C` |
| Reflections | Sky cubemap 128 px, regenerated on star-system load (§10.16) |
| Fog / GI / SSAO | Off / off / off (per-vertex voxel AO only) |
| Tonemapping | Neutral |
| Bloom | Threshold 1.1, intensity 0.25, scatter 0.55 |
| Other post | Vignette 0.20; film grain 0.12 (type Thin1); saturation −10; no chromatic aberration, no depth of field, no motion blur |
| Star | Emissive disc intensity 30; corona billboard radius 6× disc; 4 thin diffraction spikes on the star only |
| Planets | Physically lit, hard terminator, atmosphere rim 2 % of radius tinted toward the star colour, no cloud animation |
| Scale lights | Navigation lights: red `#FF2A1F` on the left (−X) extremity, green `#2AFF6A` on the right (+X), white `#FFFFFF` aft (−Z); 1 Hz blink with 0.1 s flash; one set per 20–40 m of hull length. Station window strips `#FFE2B0` at 1.2 |

### 5.5 Block design language

- **Plates.** Brick-layer textures are authored as **1 m macro-panels (2 × 2 cells, `macroTile = 2`)**. A dark seam 3 texels wide (on the 256 px layer) runs only around the macro-panel. Each cell border carries a painted 45° bevel: 4 texels in the normal map plus a 1-texel highlight on the top-left edge at 15 % strength. Up close the 0.5 m grid is visible; at 150 m the mip chain removes it and the AO gives form.
- **Greebles.** Only on the layers `hull_grate`, `hull_vent` and `hull_heavy` (bolts in each macro-panel corner). Target: at most one feature per macro-panel. Larger detail comes from prop blocks (antennas, vents, lights) placed by builders.
- **Functional blocks** are prop meshes (SPEC-04 §4.10 gives every functional block a `propMesh`). Each has: a 1-cell function band in its function colour around the mount face; its function glyph (§10.6) stencilled on every exposed face; one emissive indicator ≥ 2 cells long or a full 1-cell face. Readability targets: 300 m → a 2–4 px coloured light; 150 m → the glyph on blocks ≥ 2 cells wide, the function colour on 1-cell-wide blocks (R1); 50 m → every glyph and the stencil code (`THR-S`, `WPN-PL`, `PWR-R`, `SHD`, `CORE`, `CRG`).
- **Team colours.** Primary covers ≥ 60 % of hull plates, secondary frames and bands, accent ≤ 10 % (stripes, edges).
- **Hazard stripes.** 45° stripes Safety Yellow `#F2B705` / Graphite `#3B3E42`, two stripes per cell, on the layer `hull_hazard`. Builders use it around thruster clearance zones, docking ports and weapon arcs.
- **Damage** follows the table in §5.3: scorch → char → cracks with embers → exposed girders and copper conduits.
- **Silhouette guidance for blueprint authors (SPEC-81 `AST-014`).** Three value tiers: bone plates, graphite frames, gunmetal armour. Every ship has a readable spine and asymmetric detail (antennas, a bridge block) so its heading reads from above.

### 5.6 Materials

| Item | Rule |
|---|---|
| Shading model | URP Lit (metallic workflow) in the single brick shader (§10.14) and single prop shader (§10.15) |
| UV mapping | **No triplanar.** Brick faces use cell-face UVs derived from the packed vertex position (`uv = frac(cellPosOnFace / macroTile)`); prop meshes use the trim sheet |
| Painted plates | Metallic 0–0.1, smoothness 0.30–0.45 |
| Bare metal | Metallic 1.0, smoothness 0.45–0.65 |
| Armour | Metallic 0.6, smoothness 0.40 |
| Glass (windows) | Metallic 0, smoothness 0.90 |
| Seals, rubber | Metallic 0, smoothness 0.20 |
| Albedo range | sRGB 50–235 per channel (never pure black or white) except Silence Void |
| Macro variation | Noise at 64-cell scale, ±6 % albedo (`_MacroStrength` 0.06) |
| Brick layers used | 34 of the 64-slot plan (§10.12) |
| Prop trim sheet | 2048² with 16 trims: plates, bolted plates, pipes, cables, vents, nozzle interior (heat-tinted), glass, hazard stripes, stencil digits 0–9, stencil block codes, grilles, emissive strips, dish, ceramic (Vanguard), chitin (Xeno), Void (Silence) |

### 5.7 VFX language

| Effect | Shape | Colours | Timing |
|---|---|---|---|
| Cell debris (`CellRemoved`) | Cube particles, edge = cell size; spin 90–360°/s | Cell paint colour; first 0.4 s lerps from hot `#FFB066` (3.0) to paint | Life 3–6 s; scale to zero over the last 20 % |
| Chunk body dissolve | Dissolve from the edges inward | Char edge with ember rim `#FF7A2F` at 4.0 | 1.2 s, starting 10–30 s after the split |
| Kinetic hit | Spark cone 25°, 12–30 sparks; small grey dust puff | Sparks `#FFD9A0`; dust `#8A8478` | Sparks 0.15–0.35 s; puff 0.6 s |
| Energy hit | White-hot point flash; molten droplets | Flash `#FFFFFF` 6.0; drops `#FFB066` | Flash 0.08 s; drops 0.5 s |
| Explosion small (radius ≤ 3 cells) | Flash → fireball → soot puff; screen-space shock ring | Flash `#FFF4E0` 8.0; fireball `#FF8A2A` → `#7A2A10`; soot `#2B2522` at 30 % | Flash 0.06 s, fireball 0.5 s, soot 0.8 s, ring 0.3 s |
| Explosion large / reactor | As small plus 3–6 secondary pops within 1.5 s and a debris burst; reactor starts with a Volt Yellow core flash | Reactor core `#F5D33A` 10.0 → white | 1.8 s (large), 2.5 s (reactor) |
| Beam | White core 0.4 m wide, halo 1.2 m, ±10 % flicker at 30 Hz, 1.5 m impact glow | Core `#FFFFFF` 6.0; halo = damage-type colour | On 0.05 s, off 0.12 s |
| Projectiles | Tracer length = speed × 0.033 s, clamped 1–8 m. Plasma: elongated capsule. Railgun: thin line plus ionized trail. Flak: amber dot. Missile: mesh, exhaust, grey trail | Plasma `#FFC27A`; railgun `#FFFFFF` + trail `#BFE4FF`; flak `#FF9A1F`; missile exhaust `#FF9A1F`, trail `#8A8478` | Railgun trail 0.3 s; missile trail 1.5 s |
| Muzzle flash | 3-petal star | Weapon damage-type colour | 0.05 s |
| Shield | Invisible at rest. Hit: ripple ring at the impact point; whole-ellipsoid fresnel flash at 10 %. Collapse: crackle. Raise: wipe | Shield Blue `#3D8BFF` 2.0 → 0 | Ripple 0.4 s; flash 0.15 s; collapse 0.6 s; raise 0.5 s |
| Thruster plume | Cone, length 1–6 cells × throttle; heat shimmer only when the camera is < 100 m away | Core `#FFF0D0`, outer Amber `#FF9A1F` → transparent | Follows throttle with 0.1 s smoothing |
| Lane jump / warp | Star streaks align to the travel direction, white-out, reverse on arrival | White-out `#F4F6FA` | Spool 0.5 s, white-out 0.5 s (load hidden here), arrival 0.5 s |
| Interact beacon | Ring pulse | Safety Yellow `#F2B705` | 1 Hz |

**Damage-type colours** (written into `FxEvent.Color`, §10.17): Kinetic `#FFD9A0`, Energy `#FFC27A`, Explosive `#FF8A2A`, Ram `#E8E2D4`.

### 5.8 UI style

| Item | Rule |
|---|---|
| Panels | Rectangles, radius `--radius-s` 0 px / `--radius-m` 2 px / `--radius-l` 4 px. 1 px `--color-border`; the focused or active panel gets a 2 px `--color-accent` bar on its left edge. Header strip: title in the display font, uppercase, letter-spacing 6 %, plus a panel code in mono (`INV-01`, `FLT-02`) right-aligned in `--color-text-muted` |
| Surfaces | Screens on `--color-surface-1` at 100 %; HUD panels on `--color-surface-1` at 85 % opacity |
| Buttons | Height 36 px (`m`) / 44 px (`l`); `--color-surface-2` fill, 1 px `--color-border-strong` (proposed token, §11). Primary: `--color-accent` fill, `--color-text-on-accent` text. Hover: fill +8 % lightness. Pressed: content moves 1 px down. Disabled: opacity 0.4 |
| Resource bars | 10 segments with 2 px gaps (hull `--color-hull`, shield `--color-shield`, energy `--color-energy`); the last 2 segments turn `--color-danger` below 20 % |
| Iconography | 2 px stroke on the 24 px grid, square caps, miter joins; solid fill for the "on" state; pictograms in the spirit of industrial safety signs |
| Typography | Display **Barlow Condensed SemiBold** (OFL); body **Barlow Regular**, strong **Barlow SemiBold** (OFL); mono **IBM Plex Mono Regular** (OFL) for numbers, codes and stats. In-world stencils (textures only, not UI): **Saira Stencil One** (OFL) |
| Type ramp | `--text-xs` 12 px (body), `--text-s` 14 (body), `--text-m` 16 (body), `--text-l` 20 (display), `--text-xl` 26 (display), `--text-xxl` 36 (display) |
| Motion | `--duration-fast` 80 ms, `--duration-normal` 150 ms, `--duration-slow` 300 ms; easing `ease-out-cubic`; panels slide 8 px and fade in; new **alerts** "relay flicker" (opacity 0 → 1 → 0.4 → 1 over 120 ms); no bounce, no scale |
| HUD layout | Shared zones (§10.4.2). Status block Z-BL as stacked segmented bars; weapon groups Z-BC as numbered plates `1`–`5`; square radar Z-TR with range rings at 25 %, 50 %, 100 % |
| NORA and dialog | Comms panel in Z-L; NORA's avatar is an oscilloscope trace in `--color-accent` on a 96 × 96 px `--color-surface-2` tile; choices numbered `[1]`…`[9]` |
| Galaxy map | Nautical-chart look: `--color-bg` field, `--color-text-muted` grid every 5 ly, lanes as 2 px Bone lines, stencil labels |

### 5.9 Audio mood

- **Music:** sparse ambient synth with processed acoustic sources (bowed metal, cello, low piano); combat layers add industrial percussion (metal hits, low toms) at 90–110 BPM. Touchstones: the scores of *Alien: Isolation* and *Hardspace: Shipbreaker*, the darker cues of Vangelis.
- **SFX:** diegetic and "felt through the hull": low-mid weight, metallic creaks, relay clicks; weapons are mechanical thumps, not "pew". UI: relay clicks, solenoid thunks, teleprinter ticks.
- **Mystery:** Vanguard = clean sine choirs; the Silence = absence: the mix ducks −18 dB to a sub-bass rumble.

### 5.10 Risks and cost

| Risk | Mitigation |
|---|---|
| Looks like other block-building space games | The Otherness layer (§5.3), safety coding and the stencil UI give the identity; the look test must show it |
| Texture detail is invisible at 150–300 m | Detail budget R2; macro-panels; detail lives in prop blocks |
| Specular shimmer on metal at distance | Smoothness ≤ 0.65 and mip-mapped masks |

Estimated production after approval (one person): brick layers 34 × 2 h procedural ≈ 9 days; prop meshes 21 × 0.5–1.5 days ≈ 16 days; trim sheet 3 days; VFX 18 effects ≈ 12 days; UI kit 6 days; system glyphs 60 × 20 min ≈ 3 days; icons ≈ 5 days; sky and planets 6 days; audio sourcing and licensing 8 days. **Total ≈ 68 working days.** Procedural potential: high (layers in Material Maker, sky, planets, rendered icons). Tools: Blender (GPL), Material Maker (MIT), Krita (GPL), Inkscape (GPL), Reaper or Audacity — no paid tool is required.

---

## 6. Direction B — NOCTURNE

*Dark glossy hulls traced by thin light-lines, glyphs of light on every system, deep blue-black space with luminous filaments. Damage is darkness spreading.*

### 6.1 Pitch

The dark between the stars is listening. Ships are near-black, glossy metal — almost invisible until their light-lines trace them: a thin glowing seam around every 1 m plate, and shapes of coloured light that mark every system. Space is deep blue-black with luminous filaments of nebula. Damage is darkness spreading: seams flicker and die, cracks burn white-cyan for a few seconds, and a dead ship is a black silhouette you only see against the nebula behind it. The UI is a quiet holographic instrument: thin lines, wide display type, cyan on black. This is the most atmospheric direction and the closest to the Silence. Its cost is readability work: rim light, bloom and emissives carry everything, and the look test must prove that dark hulls still read in a 30-ship fight.

### 6.2 Touchstones

- *Arrival* (2016): restraint, monolithic shells, fog-less darkness.
- *Blade Runner 2049*: single-colour light fields, silhouettes against haze.
- *Annihilation* (2018): the shimmer — otherness through light.
- *Tron: Legacy*: light-lines as a graphic device (used sparingly here).
- *Homeworld 2* and *EVE Online*: luminous nebula skies behind dark ships.
- *Dead Space*: diegetic holographic UI.
- Hubble and JWST false-colour imagery (public domain): filament structure for the sky.

### 6.3 Palette

**Space**

| Element | Value | Rule |
|---|---|---|
| Backdrop base | `#03040A` | Relative luminance 0.0013 |
| Nebula filaments | Teal `#0E3A4A`, violet `#261447`; filament cores `#1F6E86` | Field luminance ≤ 0.035; cores (luminance 0.13) cover ≤ 3 % of the screen and never sit behind the screen centre ± 20 % |
| Stars | 3 tiers as DRYDOCK; **full** spectral saturation; the 10 brightest get a soft 6-point glare | — |
| Motion dust (VFX) | Motes faintly emissive `#4A6E8A` at 15 % | Same 3 parallax layers as §5.3 |

**Hull neutrals** (albedo, sRGB)

| Name | Hex | Metallic | Smoothness | Use |
|---|---|---|---|---|
| Obsidian | `#1B1E24` | 0.85 | 0.70 | Default plates |
| Gunmetal | `#2C3139` | 0.80 | 0.60 | Secondary plates; ARK team primary |
| Slate | `#424954` | 0.60 | 0.50 | Armour |
| Pewter | `#6B7380` | 1.00 | 0.65 | Trims, frames, edges |
| Bare | `#5D636B` | 1.00 | 0.55 | Exposed metal |
| Cavity | `#08090C` | 0.00 | 0.20 | Interior background |

**Light-line seams.** Every hull layer carries an emissive seam (2 texels on the 256 px layer, around each 1 m macro-panel) in the grid's **team accent** (slot 19) at intensity 0.35. At 150 m the seams merge into a faint glow that keeps dark hulls visible. Seams turn off where the cell is damaged (§6.3 damage table).

**Factions**

| Faction | Primary (17) | Secondary (18) | Accent / seam light (19) |
|---|---|---|---|
| ARK expedition (`faction_ark`, proposed) | Gunmetal `#2C3139` | Pewter `#6B7380` | Warm White `#FFE9C7` |
| Vestari | Obsidian Blue `#18202E` | Pearl `#C9D6E3` | Ice `#9FE8FF` |
| Khuchad Clan | Burnt `#2E1E18` | Rust `#6E3420` | Ember `#FF7A3D` |
| Tlaxcala / Triple Alliance | Deep Green `#142820` | Jade `#2F6B55` | Lime `#B8FF6A` |
| Scavengers | Soot `#24211F` | Oxide `#5E2A24` | Sodium `#FFB347` |
| Vanguard | Mirror White `#DADDE2` (smoothness 0.95) | Glass `#0A0C10` | Prism: hue cycles through the spectrum every 6 s |
| Silence | Absolute `#000000` (unlit) | — | none (no seams) |
| Xeno probes | Chitin `#1E1A22` | `#2E2733` | Acid `#C8FF2E` |

**Function colours** (emissive glyph shapes on every face; §10.6)

| Function | Colour | Emissive intensity |
|---|---|---|
| Thruster | Ion Violet `#A98BFF` | 1.0 idle → 5.0 full thrust |
| Weapon | Hot Magenta-Red `#FF3D6E` | 3.0, 5.0 while charging |
| Power (Reactor, Capacitor) | Solar Gold `#FFB84D` | 3.5 |
| Shield | Cyan `#3FD8FF` | 3.5 |
| Core | White `#F4F8FF` | 4.0, slow "breathing" at 0.25 Hz |
| Utility | Jade `#6FFFC8` | 2.5 |

**Damage and heat**

| Stage | Look |
|---|---|
| 0 Clean | Seams on |
| 1 Scuffed | Seams flicker (off 5 % of the time, random per macro-panel); smoothness −0.15 |
| 2 Cracked | Seams off; cracks glow white-cyan `#CFF6FF` at 3.0 |
| 3 Critical | Cracks glow `#FFFFFF` at 6.0, fading to 0 over 6 s after the grid's last hit, then stay dark |
| Fresh cut | Exposed internals glow "molten light" `#9FE8FF` at 4.0 for 2 s, then fade to Cavity |
| Hulk | `_GridPowered` 1 → 0 over 1.2 s; every seam and glyph goes dark; the hulk is a black silhouette lit only by the rim light |

**Otherness**

| Thing | Look |
|---|---|
| Vanguard | Mirror-white glass (smoothness 0.95) with prismatic seams (hue cycle 6 s); reflects the nebula strongly |
| Silence | Pure black `#000000`, unlit, no specular, no rim: it reads as a hole cut out of the sky. Erasure flashes `#FFFFFF` at 8.0 for 0.1 s; no debris |
| The Whisper | Violet-white ribbon `#E0C3FF` |

### 6.4 Lighting and space environment

| Item | Value |
|---|---|
| Key light | Star spectral tint at full saturation; intensity 1.6; **pitch 25°** below the plane; shadows on |
| Rim light | Directional, no shadows, colour `#5FD0FF`, intensity 0.56 (35 % of key), azimuth = camera forward (light comes from the far side of the ships as seen by the camera), pitch 20° |
| Shader rim | `_RimStrength` 0.35, `_RimColor` `#5FD0FF` (§10.14) |
| Ambient | Flat `#0A0E18` |
| Reflections | Sky cubemap 128 px (essential: glossy hulls show the nebula) |
| Fog / GI / SSAO | Off / off / off |
| Tonemapping | ACES |
| Bloom | Threshold 0.8, intensity 0.9, scatter 0.75 |
| Other post | Vignette 0.35; film grain 0.08; contrast +10; no chromatic aberration, depth of field or motion blur |
| Star | Disc intensity 40; long corona streamers (6 billboards rotating at 1°/s) |
| Planets | Dark day side, bright atmosphere rim (fresnel), city lights `#FFD9A0` on inhabited worlds |
| Scale cues | The seam grid itself; on structures over 100 m a light pulse travels along seams every 4 s at 20 m/s; navigation lights as in DRYDOCK at 1.5× intensity |

### 6.5 Block design language

- Plates are nearly flat dark colour; form comes from reflections, the rim light and the seam grid (macro-panels 2 × 2 cells).
- Functional blocks (prop meshes) carry their **function glyph as an emissive shape covering 60 % of each exposed face**, plus emissive strips along their length (≥ 2 cells).
- Team identity is light: the accent colour drives the seams; primary and secondary are dark tints that differ little.
- No hazard stripes, no stencils on hulls (the HUD names things).
- Damage reads as light dying (table above): healthy areas glow faintly, damaged areas are dark with bright cracks.

### 6.6 Materials

| Item | Rule |
|---|---|
| Shading | URP Lit, glossy metal: metallic 0.8–1.0, smoothness 0.55–0.80 |
| Albedo range | sRGB 20–110 per channel (dark) except Vanguard and UI |
| Specular aliasing | Smoothness is reduced by mip level (Toksvig-style, in the layer mask mips baked by `ART-007`) so distant voxels do not sparkle |
| UV mapping | Same as DRYDOCK (cell-face UVs, `macroTile = 2`, no triplanar) |
| Authored channel | The emissive mask (alpha of the mask array) is the main hand-authored content |
| Brick layers used | 30 of the 64-slot plan: `hull_hazard`, `hull_marking` and `hull_dock` are copies of `hull_panel_a`, and `station_solar` is a copy of `station_plating` (§10.12) |

### 6.7 VFX language

| Effect | Shape | Colours | Timing |
|---|---|---|---|
| Cell debris | Dark cubes with glowing edges | Edge `#9FE8FF` 3.0 → 0 | Glow 0.8 s; life 3–6 s |
| Chunk dissolve | Edges dissolve into light motes | `#9FE8FF` | 1.2 s |
| Kinetic hit | Short white streak sparks | `#FFFFFF` 4.0 | 0.12–0.25 s |
| Energy hit | Expanding light ring on the surface | Damage-type colour | 0.3 s |
| Explosions | Implosion: flash, inward-collapsing rings, then an outward ring of light filaments; no fireball, no smoke | White → cyan `#3FD8FF` → violet `#A98BFF` | Small 0.6 s, large 1.8 s, reactor 2.5 s |
| Beam | Thin core 0.25 m, wide bloom halo 2 m, crackling side arcs | Core `#FFFFFF` 8.0, halo `#FF3D6E` family | On 0.05 s, off 0.12 s |
| Projectiles | Light streaks (length = speed × 0.05 s, clamped 2–10 m) | Damage-type colour | — |
| Shield | Always faintly visible iridescent fresnel (5 % opacity); bright ripple on hit | Cyan `#3FD8FF` | Ripple 0.4 s |
| Thruster plume | Long ion trail, 4–12 cells × throttle | Ion Violet `#A98BFF` | Trail fade 0.6 s |
| Lane jump / warp | Light tunnel of converging seam lines, then **black-out** | — | Spool 0.5 s, black-out 0.5 s, arrival 0.5 s |

**Damage-type colours:** Kinetic `#FFFFFF`, Energy `#FF3D6E`, Explosive `#FFB84D`, Ram `#9FE8FF`.

### 6.8 UI style

| Item | Rule |
|---|---|
| Panels | Translucent `--color-surface-1` at 80 %; 1 px lines; radius 0 / 0 / 2 px; **corner brackets** (8 px L-shapes, 1 px, `--color-accent`) on focused panels, drawn by a 9-slice sprite `UI_Bracket_Focus.png` (`-unity-slice-*` 8 px) |
| Buttons | Ghost style: transparent fill, 1 px `--color-border-strong`, text `--color-accent`; hover fills `--color-surface-3`; primary: `--color-accent` 1 px border plus 20 % accent fill |
| Bars | Thin (6 px) continuous arcs or lines; numbers appear only on hover or below 30 % |
| Iconography | 1.5 px line at 24 px, open shapes, round caps and joins; glyphs carry a 30 % accent glow (a second, blurred copy of the glyph sprite) |
| Typography | Display **Michroma Regular** (OFL; wide, uppercase only, letter-spacing 10 %); body **Inter Regular**, strong **Inter SemiBold** (OFL); mono **JetBrains Mono Regular** (OFL) |
| Type ramp | xs 12, s 14, m 16, l 20, xl 26, xxl 34 px (Michroma is wide, so display sizes are smaller) |
| Motion | fast 120 ms, normal 250 ms, slow 500 ms; easing `ease-in-out-sine`; reveal = opacity 0 → 1 with a 6 px upward translate; alerts pulse their glow at 1 Hz |
| HUD layout | Shared zones (§10.4.2) with one change: **hull and shield are arcs around the player ship** (hull left 120°, shield right 120°, radius 150 px around the ship's screen position, 6 px thick), energy a thin arc below. Z-BL shows numbers only. Radar is a circular scope in Z-TR |
| NORA and dialog | NORA is a ring of 24 dots that ripple outward while she speaks; dialog choices numbered `[1]`…`[9]` in mono |
| Galaxy map | Holographic star map: stars as glowing points, lanes as thin animated dashed lines |

### 6.9 Audio mood

- **Music:** dark ambient — drones, choir pads, granular textures, long reverbs. Touchstones: Jóhann Jóhannsson (*Arrival*), Barrow and Salisbury (*Annihilation*), Vangelis.
- **SFX:** tonal and spacious: pitched whooshes, sub-bass impacts, glassy energy weapons. UI: soft glassy ticks and sine blips.
- **Mystery:** the Silence inverts the mix — every reverb tail is cut to zero and only a high, thin tone remains.

### 6.10 Risks and cost

| Risk | Mitigation |
|---|---|
| Dark hulls disappear against dark space (pillar 1) | Rim light, seams, reflections; the look test must pass L2/L3 (§10.3) or the direction fails |
| Bloom smears a 30-ship fight into haze | Bloom threshold 0.8 and caps on emissive area; measured in `ART-010` |
| Seams on a 1M-cell station produce a large glowing area | Seams fade with distance: seam intensity × `saturate((450 − d) / 300)` |
| Specular sparkle on glossy voxels | Mip-reduced smoothness (§6.6) |
| GPU cost of bloom + glossy reflections | +0.4 ms at 1080p (estimate), measured in `ART-010` |

Estimated production: textures ≈ 5 days (flat albedo, authored emissive masks); lighting and post tuning ≈ 8 days; VFX ≈ 15 days (all light-based, more tuning); UI ≈ 8 days (9-slice brackets, glow copies, arc HUD); prop meshes ≈ 16 days; icons and glyphs ≈ 8 days; sky ≈ 6 days; audio ≈ 8 days. **Total ≈ 74 working days.**

---

## 7. Direction C — PULP HORIZON

*Graphic colour blocks, crisp ink edges, painterly gradient skies with a three-colour key per star system. A paperback cover you can blow apart.*

### 7.1 Pitch

A paperback cover you can blow apart. Clean fields of colour, crisp ink edges, bold flat shapes. Ships are painted in big honest areas — cream hulls, teal frames — and every system is painted in its function colour, like a schematic come alive. Skies are painterly gradients with posterised nebula bands, and every star system has its own three-colour key, so each place you visit is remembered by its colours. Destruction is graphic: cells burst into colour confetti, wounds are drawn in halftone ink. The mystery comes through composition and colour scripting: the Silence is negative space — flat ink-black shapes that eat the poster. This is the most unique, the cheapest to produce and the most legible direction; its risk is tone, because it can tip into toy-like unless lighting, music and colour scripting hold the dread.

### 7.2 Touchstones

- 1970s British science-fiction paperback covers (Chris Foss's colour-blocked spacecraft).
- Moebius (Jean Giraud): clean line and flat colour; the sense of vast, quiet places.
- Rick Guidice's 1970s space-colony paintings for NASA Ames (public domain): warm, optimistic, huge scale.
- *Outer Wilds*: a stylised world that is still eerie and full of mystery.
- *Sable*: graphic line rendering in 3D.
- *Into the Breach* and *FTL*: flat, crystal-clear tactical UI.
- *Teardown*: voxel destruction that stays readable.
- Saul Bass title sequences: bold motion of flat shapes.

### 7.3 Palette

**Space** (per star system: a three-colour **sky key**, chosen by the generator from this list using the star system seed)

| Sky key | Top | Horizon | Nebula band |
|---|---|---|---|
| Teal Dusk | `#0E1C2E` | `#163049` | `#1F4E5F` |
| Plum Night | `#140F24` | `#241838` | `#3A2547` |
| Rust Haze | `#160E0E` | `#2A1612` | `#5A2E22` |
| Ink Blue | `#0B1220` | `#14223A` | `#24406A` |
| Moss Dark | `#0E1712` | `#18281E` | `#2F4A32` |

Rule: band luminance ≤ 0.08 and covers ≤ 25 % of the screen; everything else ≤ 0.05. Stars: cream `#F6EEDC` dots; the 10 brightest are 4-point sparkles. Motion dust: short cream dashes `#F6EEDC` at 30 % (speed lines).

**Hull neutrals**

| Name | Hex | Metallic | Smoothness | Use |
|---|---|---|---|---|
| Cream | `#EFE4C8` | 0.0 | 0.30 | Default plates; ARK team primary |
| Sand | `#D9C9A3` | 0.0 | 0.30 | Secondary plates |
| Teal-grey | `#5F7F86` | 0.1 | 0.35 | Frames, ARK team secondary |
| Ink | `#2A3140` | 0.0 | 0.25 | Lines, girders, trims |
| Bare | `#9FA8B0` | 0.3 | 0.40 | Exposed metal |
| Cavity | `#161B26` | 0.0 | 0.20 | Interior background |

**Factions**

| Faction | Primary (17) | Secondary (18) | Accent (19) |
|---|---|---|---|
| ARK expedition (`faction_ark`, proposed) | Cream `#EFE4C8` | Teal-grey `#5F7F86` | Sunflower `#FFC233` |
| Vestari | White `#F6F3EE` | Cerulean `#2F6FD6` | Sky `#8FD3FF` |
| Khuchad Clan | Brick `#B5482E` | Ink `#2A3140` | Tangerine `#FF8A2B` |
| Tlaxcala / Triple Alliance | Leaf `#3FA34D` | Cream `#EFE4C8` | Lime `#C5E84A` |
| Scavengers | Mustard `#C9A13A` | Oxide `#8E3B2E` | Pink `#F27BAA` |
| Vanguard | Gold `#E3C38E` | White `#FBF7EC` | Pale Gold light `#FFE08A` |
| Silence | Ink Black `#07080C` (flat, unlit) | — | none; a 1 px cream `#F6EEDC` halo offset 2 px (ink misregistration) |
| Xeno probes | Olive `#6E8040` | Plum `#3A2547` | Acid `#D7FF3A` |

**Function colours** (the whole block body is painted in it; a cream `#F6EEDC` glyph on top)

| Function | Colour | Emissive |
|---|---|---|
| Thruster | Orange `#FF8A2B` | Nozzle only, 1.5 |
| Weapon | Red `#E2483D` | 0 |
| Power (Reactor, Capacitor) | Sunflower `#FFC233` | 0 |
| Shield | Blue `#2F9BFF` | 0 |
| Core | Green `#3FB950` | Beacon, 1.5 |
| Utility | Purple `#8A5CD6` | 0 |

**Damage and heat** (halftone = a screen-aligned dot pattern in Ink `#2A3140`, density by stage)

| Stage | Look |
|---|---|
| 0 Clean | As authored |
| 1 Scuffed | 15 % halftone |
| 2 Cracked | 40 % halftone + 2-texel ink crack lines |
| 3 Critical | 75 % halftone + flat orange `#FF8A2B` ember dots blinking at 2 Hz while the grid was hit in the last 6 s |
| Fresh cut | Exposed internals as flat stripes: ink cavity, cream pipes, red and blue cable bands |
| Hulk | Palette shifts to two tones over 0.8 s: every colour desaturates 60 % toward Sand; lights off |

**Otherness:** Vanguard = Art-Deco gold and white shapes with stepped, fan-like plates. Silence = flat ink-black negative space with the misregistration halo; erased cells vanish with a cream flash. The Whisper = hot pink `#FF4FA3` ribbon.

### 7.4 Lighting and space environment

| Item | Value |
|---|---|
| Shading | **Toon ramp** in the brick and prop shaders (`_ShadingMode` 1, §10.14): 3 steps at 0.35 / 0.70 / 1.0 of albedo; shadow tone = Ink `#2A3140` at 60 % |
| Key light | Warm white `#FFF2DC` tinted 30 % toward the star colour; intensity 1.8; **pitch 45°**; shadows on (flat ink tone) |
| Fill light | Directional, no shadows, colour = the system's sky-key band colour, intensity 0.54 (30 %), opposite azimuth, pitch 45° |
| Ambient | Gradient: sky = band colour, equator = horizon colour, ground = top colour |
| Outlines | Screen-space depth/normal edge detection (URP renderer feature), 1.5 px, Ink `#1A2030`, constant width; only for opaque geometry |
| Reflections | Off (smoothness ≤ 0.40; metallic ≤ 0.3) |
| Fog / GI / SSAO | Off / off / off |
| Tonemapping | Neutral |
| Bloom | Threshold 1.6, intensity 0.12 |
| Other post | No vignette, no film grain, saturation +5 |
| Star | Flat disc with 3 concentric rings in the star colour at 60 %, 30 %, 10 % |
| Planets | Posterised: 4 flat bands from the terminator outward, 2 px ink rim line |
| Scale cues | Big hull numbers (2 m tall) and stripe bands; navigation lights as flat dots; station windows as cream dashes |

### 7.5 Block design language

- **Flat colour fields.** Each cell face is one flat colour with a **drawn bevel**: a 2-texel light line (albedo +20 %) on the top and left edges and a 2-texel ink line on the bottom and right edges of every cell. One ink seam per 1 m macro-panel.
- **No greebles in textures.** Detail is shape: prop meshes with chunky forms, 45° chamfers and big rounded caps.
- **Functional blocks** are painted entirely in their function colour with a cream glyph on every exposed face — the most legible of the three directions.
- **Team colours** cover hull plates only (functional blocks keep function colours).
- **Damage** is halftone ink (table above); debris is confetti in the cell's colour.

### 7.6 Materials

| Item | Rule |
|---|---|
| Shading | Toon ramp (`_ShadingMode` 1) on bricks and props |
| Metallic / smoothness | 0–0.3 / 0.20–0.40 |
| Normal maps | Bevel only (4 texels at cell edges) |
| Albedo | Flat fields; macro variation 0 |
| UV mapping | Cell-face UVs, `macroTile = 2`, no triplanar |
| Brick layers used | 29 of the 64-slot plan: 4 distinct asteroid layers, 3 distinct station layers, `hull_vent` and `xeno_membrane` are copies (§10.12) |

### 7.7 VFX language

| Effect | Shape | Colours | Timing |
|---|---|---|---|
| Cell debris | Flat-shaded cubes plus "confetti" quads | Cell paint colour, cream highlights | Life 3–6 s |
| Chunk dissolve | Cells pop into confetti one by one from the edge | Cell colours | 1.2 s |
| Hits | 2-tone starbursts (8 points), flipbook at 12 fps | Damage-type colour + cream core | 0.17 s (2 frames) to 0.33 s (4 frames) |
| Explosions | Flat starburst, expanding ink ring line, flat ink smoke puffs | Orange `#FF8A2B`, Sunflower `#FFC233`, cream core, ink smoke `#2A3140` | Small 0.6 s, large 1.8 s, reactor 2.5 s; flipbooks at 12 fps |
| Beam | Solid flat bar with a cream core and a hard ink edge | Damage-type colour | On 0.05 s, off 0.12 s |
| Projectiles | Capsules with an ink outline | Damage-type colour | — |
| Shield | Flat 2 px outline of the ellipsoid on hit, plus a "dent" ring at the impact | Blue `#2F9BFF` | 0.4 s |
| Thruster plume | 2-tone flame shape, stepped flicker at 12 fps | Cream core, Orange outer | — |
| Lane jump / warp | Radial speed lines, then a colour wipe in the target system's sky-key colour | — | Spool 0.5 s, wipe 0.5 s, arrival 0.5 s |

**Damage-type colours:** Kinetic `#FFE08A`, Energy `#FF6F59`, Explosive `#FF8A2B`, Ram `#F6EEDC`.

### 7.8 UI style

| Item | Rule |
|---|---|
| Panels | Flat cards, radius 4 / 8 / 14 px, borders 2 / 3 px; a **print shadow**: an `--color-bg`-coloured copy of the card offset 4 px right and 4 px down (a sibling element in the component template; USS has no box-shadow) |
| Buttons | Height 40 px (`m`) / 48 px (`l`); `--color-surface-2` fill with a 2 px `--color-border-strong` border; primary: `--color-accent` fill with `--color-text-on-accent` text; pressed: the button moves onto its shadow (translate 4 px, 4 px) |
| Bars | Chunky 16 px rounded bars with a 2 px ink outline |
| Iconography | Filled solid glyphs on the 24 px grid, 2 px corner radius, cream on colour chips |
| Typography | Display **Archivo Expanded ExtraBold** (OFL static instance of Archivo, width 125, weight 800); body **Atkinson Hyperlegible Regular**, strong **Atkinson Hyperlegible Bold** (OFL); mono **Space Mono Regular** (OFL) |
| Type ramp | xs 13, s 15, m 17, l 22, xl 30, xxl 44 px |
| Motion | fast 100 ms, normal 200 ms, slow 350 ms; easing `ease-out-back` (overshoot) for appearing, `ease-in-cubic` for leaving; cards drop 12 px; "stamps" rotate from −3° to 0° |
| HUD layout | Shared zones with one change: Z-BL and Z-BC merge into one **dashboard** strip 112 px tall at the bottom (cards for hull/shield/energy, weapon groups, abilities); location ribbon in Z-TL; round radar in Z-TR |
| NORA and dialog | NORA is a flat circle with a single animated waveform "mouth" line; dialog in a cream-outlined card |
| Galaxy map | Illustrated poster map: star systems as flat discs in their sky-key colour, lanes as dashed ink lines |

### 7.9 Audio mood

- **Music:** warmer and melodic: analogue synthesizers with acoustic instruments (guitar, harmonium, glockenspiel). Touchstones: Andrew Prahlow (*Outer Wilds*), Ben Prunty (*FTL*), Jean-Michel Jarre (*Oxygène*).
- **SFX:** punchy and stylised but weighty; tonal impacts. UI: marimba and woodblock ticks.
- **Mystery:** detuned tape warble; the Silence cuts the music to tape hiss.

### 7.10 Risks and cost

| Risk | Mitigation |
|---|---|
| Tone becomes toy-like; conflicts with "break apart realistically" in the vision | Colour scripting (desaturate toward ink during Silence events), sombre music; owner judges on the look test |
| Needs toon shading and an outline pass that SPEC-11 does not plan | `_ShadingMode` switch in the master brick and prop shaders (a uniform branch, no keyword) + one renderer feature; +0.3–0.5 ms GPU at 1080p (estimate) |
| Halftone damage needs screen-space dots in the shader | Part of the same shader work |
| Scale reads weaker with flat colour | Mandatory hull numbers, stripe bands and navigation lights on structures > 100 m |

Estimated production: textures ≈ 3 days; shader work (toon ramp, outline feature, halftone) ≈ 8 days, mostly in SPEC-11's shader; prop meshes ≈ 14 days (simpler forms); VFX ≈ 10 days (drawn flipbooks); UI ≈ 5 days; icons and glyphs ≈ 6 days; sky ≈ 4 days; audio ≈ 8 days. **Total ≈ 58 working days.**

---

## 8. Side by side

### 8.1 UI token values for every binding token

Token **names** are binding (§10.1); this table proposes their **values**. The *Placeholder* column is what `ART-001` writes into `Assets/Game/UI/Theme/tokens.uss` now, so UI work can proceed without pre-judging the decision; `ART-014` replaces it with the approved column. Font files are under `Assets/Game/UI/Theme/Fonts/` (§10.8); the USS value is `url("project://database/Assets/Game/UI/Theme/Fonts/<file>")`.

| Token | Placeholder (until approval) | DRYDOCK | NOCTURNE | PULP HORIZON |
|---|---|---|---|---|
| `--color-bg` | `#111214` | `#0D0F11` | `#05070D` | `#0F1626` |
| `--color-surface-1` | `#1A1B1E` | `#16191C` | `#0A0F1A` | `#18223A` |
| `--color-surface-2` | `#232428` | `#1F2327` | `#101826` | `#22304E` |
| `--color-surface-3` | `#2D2F33` | `#2A2F34` | `#172233` | `#2E3F63` |
| `--color-border` | `#4A4D52` | `#454C53` | `#2A3B55` | `#5B6E96` |
| `--color-text` | `#EDEDED` | `#ECE6D8` | `#DCE8F5` | `#F6EEDC` |
| `--color-text-muted` | `#A6A6A6` | `#A9A395` | `#8FA6C2` | `#C7C0AE` |
| `--color-accent` | `#7AA7D9` | `#2EC4B6` | `#39D5FF` | `#FFC233` |
| `--color-accent-strong` | `#B3CDEB` | `#7FE6DB` | `#A6EEFF` | `#FFE08A` |
| `--color-positive` | `#8CCB6A` | `#8BD64A` | `#3DD6A3` | `#A3DE52` |
| `--color-warning` | `#E3BE5A` | `#F2B33D` | `#FFC247` | `#FF9F1C` |
| `--color-danger` | `#F06A7E` | `#FF5277` | `#FF5C7A` | `#FF7085` |
| `--color-info` | `#6CB8F0` | `#55B8F5` | `#8FB0FF` | `#62CCFF` |
| `--color-ally` | `#5C9BFF` | `#4D9BFF` | `#5B8CFF` | `#4FA3FF` |
| `--color-enemy` | `#FF7A4D` | `#FF6A3D` | `#FF7A45` | `#FF6B35` |
| `--color-neutral` | `#C8C8C8` | `#CFC8B6` | `#B9C3D1` | `#E9DFC7` |
| `--color-shield` | `#5CC8FF` | `#47C8FF` | `#3FD8FF` | `#4FD1FF` |
| `--color-hull` | `#E0E0E0` | `#E5DCC8` | `#EDE6DA` | `#F6EEDC` |
| `--color-energy` | `#EFD25A` | `#F5D33A` | `#FFB84D` | `#FFB020` |
| `--space-1` … `--space-8` | 4, 8, 12, 16, 24, 32, 48, 64 px | same (shared) | same | same |
| `--radius-s` / `-m` / `-l` | 2 / 4 / 8 px | 0 / 2 / 4 px | 0 / 0 / 2 px | 4 / 8 / 14 px |
| `--border-thin` / `--border-thick` | 1 / 2 px | 1 / 2 px | 1 / 2 px | 2 / 3 px |
| `--font-display` | `OfficeCodePro-Bold_SDF.asset` (in `_Placeholder/Fonts/`) | `FNT_BarlowCondensed-SemiBold_SDF.asset` | `FNT_Michroma-Regular_SDF.asset` | `FNT_ArchivoExpanded-ExtraBold_SDF.asset` |
| `--font-body` | `OfficeCodePro-Regular_SDF.asset` (in `_Placeholder/Fonts/`) | `FNT_Barlow-Regular_SDF.asset` | `FNT_Inter-Regular_SDF.asset` | `FNT_AtkinsonHyperlegible-Regular_SDF.asset` |
| `--font-mono` | `OfficeCodePro-Regular_SDF.asset` (in `_Placeholder/Fonts/`) | `FNT_IBMPlexMono-Regular_SDF.asset` | `FNT_JetBrainsMono-Regular_SDF.asset` | `FNT_SpaceMono-Regular_SDF.asset` |
| `--text-xs` | 12 px | 12 px | 12 px | 13 px |
| `--text-s` | 14 px | 14 px | 14 px | 15 px |
| `--text-m` | 16 px | 16 px | 16 px | 17 px |
| `--text-l` | 20 px | 20 px | 20 px | 22 px |
| `--text-xl` | 28 px | 26 px | 26 px | 30 px |
| `--text-xxl` | 40 px | 36 px | 34 px | 44 px |
| `--duration-fast` | 100 ms | 80 ms | 120 ms | 100 ms |
| `--duration-normal` | 200 ms | 150 ms | 250 ms | 200 ms |
| `--duration-slow` | 400 ms | 300 ms | 500 ms | 350 ms |

Measured contrast (WCAG 2.x ratio) of every text-capable token, worst case over `--color-bg`, `--color-surface-1`, `--color-surface-2`:

| Token | Placeholder | DRYDOCK | NOCTURNE | PULP HORIZON |
|---|---|---|---|---|
| `--color-text` | 13.24 | 12.71 | 14.32 | 11.36 |
| `--color-text-muted` | 6.37 | 6.30 | 7.12 | 7.23 |
| `--color-accent` | 6.17 | 7.30 | 10.28 | 8.13 |
| `--color-positive` | 8.02 | 8.90 | 9.61 | 8.20 |
| `--color-warning` | 8.70 | 8.50 | 11.06 | 6.39 |
| `--color-danger` | 5.22 | 5.06 | 5.98 | 4.94 |
| `--color-info` | 7.20 | 7.20 | 8.32 | 7.25 |
| `--color-ally` | 5.60 | 5.61 | 5.62 | 5.01 |
| `--color-enemy` | 6.01 | 5.56 | 6.88 | 4.63 |

All ≥ 4.5:1 (WCAG AA for body text). On `--color-surface-3` some semantic colours fall below 4.5:1 (lowest: PULP HORIZON `--color-danger` 3.94); rule T3 in §10.3 forbids semantic-coloured **text** on `--color-surface-3`.

### 8.2 One-line comparison

| | DRYDOCK | NOCTURNE | PULP HORIZON |
|---|---|---|---|
| Hull value | Light to mid | Dark | Light, saturated |
| Readability carried by | Light hulls on black, safety colours | Rim light, seams, emissive glyphs | Flat colour fields, ink outlines |
| Material model | PBR Lit | PBR Lit, glossy | Toon ramp + outlines |
| Destruction look | Scorch, char, embers, girders | Light dying, white-hot cracks | Halftone ink, confetti |
| Space | Black, sparse, photographic | Blue-black, luminous filaments | Painted gradients, per-system colour key |
| UI | Utilitarian plates, stencil, teal accent | Thin holographic lines, cyan | Bold print cards, sunflower accent |
| Needs new shader features beyond URP Lit | No | Seam emission, rim term | Toon ramp, outline pass, halftone |
| Estimated production days | 68 | 74 | 58 |

### 8.3 Scorecard

Scores 1–5 (5 = best; for cost and risk, 5 = cheapest / lowest risk). Weights follow the pillar order. These are the author's estimates; the owner may re-weight.

| Criterion | Weight | DRYDOCK | NOCTURNE | PULP HORIZON |
|---|---|---|---|---|
| Destruction readability at 50–300 m (pillar 1) | 3 | 5 | 3 | 5 |
| Block function legibility (pillar 2) | 3 | 4 | 5 | 5 |
| Massive-scale cues (pillar 3) | 2 | 5 | 4 | 3 |
| Mystery mood (pillar 4) | 2 | 4 | 5 | 3 |
| Production cost | 2 | 4 | 3 | 5 |
| Technical risk (fits SPEC-11's master shader and architecture §11) | 2 | 5 | 3 | 2 |
| Uniqueness | 1 | 2 | 3 | 5 |
| **Weighted total (max 75)** | | **65** | **57** | **61** |

---

## 9. Recommendation

**Recommended: DRYDOCK.** Recommended second finalist for the look test: **PULP HORIZON**.

Reasons, in pillar order:

1. **Destruction readability.** Light-to-mid hulls on a black backdrop give the largest value contrast of the three (rule L3), and the damage ladder scorch → char → cracks with embers → exposed girders reads as a value change at 300 m and as material detail at 50 m. NOCTURNE's dark hulls make readability depend on rim light and bloom, which is the riskiest place to spend pillar 1.
2. **"Break apart realistically".** The vision's wording asks for realistic breakup. Burnt plating, glowing edges and exposed structure sell physical destruction; PULP HORIZON's halftone and confetti sell graphic destruction instead.
3. **Scale.** Navigation lights, stencils, window strips and the 1 m plate grain make a 1M-cell station feel kilometres long.
4. **Lowest technical risk and best fit with architecture §11.3.** DRYDOCK needs nothing beyond URP Lit in the one shared brick material and the one shared prop material (§10.14–§10.15): no toon ramp, no outline renderer feature, no seam-emission distance fade, no new keywords.
5. **Mood without a readability tax.** Industrial human technology makes the Vanguard (gold-lit ceramic) and the Silence (light-eating void) stand out by contrast — the mystery is carried by the Otherness rules, not by darkening combat.
6. **Cost.** About 68 working days, mostly procedural.

Its weakness is uniqueness (2/5). The mitigation is in the plan: the Otherness layer, safety-coded blocks and the stencil UI are the signature, and the look test must show them. **If the owner values uniqueness and the lowest art cost above realism, PULP HORIZON is the strong alternative** (61 points) — that is why it is the recommended second finalist. NOCTURNE is the mood-first option; choose it only if the look test proves rules L2–L5 in a 30-ship fight.

---

## 10. Shared rules (all directions)

### 10.1 Token contract

- **Binding names.** `Assets/Game/UI/Theme/tokens.uss` defines exactly these custom properties on `:root`, and no other file defines them: `--color-bg`, `--color-surface-1`, `--color-surface-2`, `--color-surface-3`, `--color-border`, `--color-text`, `--color-text-muted`, `--color-accent`, `--color-accent-strong`, `--color-positive`, `--color-warning`, `--color-danger`, `--color-info`, `--color-ally`, `--color-enemy`, `--color-neutral`, `--color-shield`, `--color-hull`, `--color-energy`, `--space-1` … `--space-8`, `--radius-s`, `--radius-m`, `--radius-l`, `--border-thin`, `--border-thick`, `--font-display`, `--font-body`, `--font-mono`, `--text-xs`, `--text-s`, `--text-m`, `--text-l`, `--text-xl`, `--text-xxl`, `--duration-fast`, `--duration-normal`, `--duration-slow`. They are never renamed. Proposed additions are in §11 and become binding only through the decision log (§12).
- **Usage.** USS rules use `var(--token)`; UXML and C# never contain colours, sizes, fonts or durations (conventions §4.2).

| Token | Meaning | Allowed use |
|---|---|---|
| `--color-bg` | Screen backdrop behind full-screen UI | Screen root backgrounds, scrims (with opacity) |
| `--color-surface-1/2/3` | Raised surfaces, each one step lighter | 1 panels, 2 controls and cards inside panels, 3 hover/selected rows |
| `--color-border` | Decorative separators | Dividers, panel outlines. Not enough contrast for input boundaries — use `--color-border-strong` (§11) |
| `--color-text` / `--color-text-muted` | Primary / secondary text | Any surface 1–3 |
| `--color-accent` / `--color-accent-strong` | Interactive highlight / its hover-pressed state and accent text on dark | Focus, selection, primary buttons, objective markers |
| `--color-positive/warning/danger/info` | Notices and validation | **Always paired with a glyph** (✓, ⚠, ✕, ⓘ shapes, §10.6) |
| `--color-ally/enemy/neutral` | Relationship of a world object to the player | Markers, radar blips, target frames. **Always paired with a marker shape** (§10.2) |
| `--color-shield/hull/energy` | Ship resources | Bars and their numbers |

### 10.2 Colour-blind safety

Rules:

- **C1.** Ally / enemy / neutral differ by colour **and** shape: ally = circle or rounded chevron, enemy = diamond or sharp chevron, neutral = square. Radar blips use the same shapes.
- **C2.** Notices pair colour with a glyph; validation messages also say the problem in words.
- **C3.** Functional blocks are identified by silhouette and glyph (§10.6) as well as colour; the six function colours are not required to be distinguishable from each other under colour-vision deficiency, but each must differ from every hull neutral of the direction by ΔE2000 ≥ 10 under all simulations (so a functional block never looks like plain hull).
- **C4.** Measurement: colours are simulated with the Machado-2009 matrices at severity 1.0 (protanopia, deuteranopia, tritanopia) in linear RGB and compared with CIEDE2000. Required minimum over the normal view and the three simulations: ally–enemy ≥ 30, ally–neutral and enemy–neutral ≥ 20, hull–shield–energy pairs ≥ 12, positive–danger ≥ 10.

Measured (minimum ΔE2000 over normal + 3 simulations):

| Pair | Placeholder | DRYDOCK | NOCTURNE | PULP HORIZON |
|---|---|---|---|---|
| ally–enemy | 46.9 | 48.6 | 47.9 | 49.6 |
| ally–neutral | 25.1 | 32.2 | 20.2 | 35.7 |
| enemy–neutral | 25.1 | 21.1 | 32.0 | 23.3 |
| hull–shield | 19.3 | 29.6 | 23.1 | 27.5 |
| hull–energy | 18.9 | 12.4 | 20.7 | 22.6 |
| shield–energy | 43.9 | 44.0 | 45.9 | 49.5 |
| positive–danger | 10.5 | 14.4 | 10.4 | 15.3 |

The check is automated by `tools/art/check_tokens.py` (SPEC-80 `ART-002`). Simulated colours are clamped to [0, 1] in linear RGB before the Lab conversion. The script recomputed this table on 2026-10-10. Three cells moved by 0.1–0.2 from the first hand-computed version. DRYDOCK `--color-hull` changed from `#E6DCC8` to `#E5DCC8` (ΔE 0.5, not visible) because hull–energy measured 11.99, just under the C4 minimum of 12, which the earlier "12.0" had rounded up.

### 10.3 Contrast and luminance minimums

UI text (WCAG 2.x, AA):

- **T1.** Body text (< 24 px regular, < 19 px bold) ≥ 4.5:1 against its actual background.
- **T2.** Large text and non-text UI that identifies a control (input boundary, button outline, focus ring, slider track) ≥ 3:1 against adjacent colours.
- **T3.** Semantic colours (`positive`, `warning`, `danger`, `info`, `ally`, `enemy`) may be used as text only on `--color-bg`, `--color-surface-1` and `--color-surface-2`.
- **T4.** HUD text drawn directly over the world gets a 2 px `--color-bg` outline at 80 % opacity (`-unity-text-outline`) so it stays ≥ 4.5:1 over any backdrop.

In-world luminance (measured on 1920×1080 look-test captures, relative luminance per WCAG of the final sRGB pixel):

| Rule | Requirement |
|---|---|
| **L1 Backdrop** | ≥ 95 % of backdrop pixels (not covered by ships, bodies, VFX or UI) ≤ 0.03 (DRYDOCK, NOCTURNE) or ≤ 0.05 (PULP HORIZON); no backdrop pixel inside the central 40 % of the screen above 0.08 |
| **L2 Lit hull** | Median luminance of lit, upward-facing default hull: DRYDOCK 0.25–0.60; NOCTURNE 0.06–0.25 (seams included); PULP HORIZON 0.35–0.75 |
| **L3 Silhouette** | Median hull luminance ≥ 3× the median of a 32 px ring of backdrop around the ship |
| **L4 Shadow side** | Hull pixels in shadow have median ≥ 0.02 (never pure black), except Silence |
| **L5 Function lights** | Function emissive pixels ≥ 2× the median luminance of the adjacent hull |

### 10.4 Sizes at 1080p

#### 10.4.1 Components (logical px at the 1920 × 1080 reference)

| Item | Size |
|---|---|
| Minimum pointer target | 32 × 32 |
| Button height | 36 (`m`) and 44 (`l`); PULP HORIZON 40 and 48 |
| Text field height | 36 |
| Tab height | 36 |
| List row | 40 |
| Inventory slot | 64 × 64, gap `--space-1` |
| Tooltip | max width 360, padding `--space-3` |
| Toast | width 480, ≤ 3 lines, ≤ 3 visible |
| Modal | max width 960, padding `--space-6` |
| Scrollbar | 8 visible, 16 hit area |
| Focus ring | 2 px `--color-accent-strong` |
| Minimum sentence text | `--text-s`; `--text-xs` only for units, tags and key hints |
| HUD safe margin | 32 on every edge |

#### 10.4.2 HUD zones (shared skeleton; directions vary only as stated in their §x.8)

| Zone | Rectangle x, y, w, h | Content | Visible |
|---|---|---|---|
| Z-TL | 32, 32, 480, 128 | Star system and location; objective tracker (≤ 3 lines) | Always |
| Z-TC | 672, 32, 576, 112 | Toasts (≤ 3), alert banner | On event |
| Z-TR | 1568, 32, 320, 320 | Radar / minimap | Always |
| Z-L | 32, 200, 528, 600 | Comms and dialog (NORA) | When open |
| Z-C | 560, 200, 800, 600 | World only: reticle, markers, weapon arcs; interaction prompt centred at y = 740 | Always; **no panels** |
| Z-R | 1488, 400, 400, 360 | Target panel | With a target |
| Z-BL | 32, 856, 528, 192 | Ship status: hull, shield, energy, speed, heat | Always |
| Z-BC | 640, 952, 640, 96 | Weapon groups 1–5, abilities, key hints | Always |
| Z-BR | 1408, 936, 480, 112 | Cargo, credits, fuel | Always |

Zones never overlap. In flight with no screen open, HUD pixels with opacity ≥ 70 % cover ≤ 15 % of the screen (measured in `ART-010`).

### 10.5 Icon grid

| Kind | Master | Shipped as | Displayed at |
|---|---|---|---|
| System glyphs (UI actions, stats, tools) | 24 × 24 px grid, 2 px padding (live area 20 × 20), strokes on whole pixels | SVG → UI Toolkit `VectorImage` | 16, 24, 32, 48 px |
| Item and block icons | 256 × 256 px, subject fills the central 224 × 224 | PNG Sprite 128 × 128 | 64 px (slot), 128 px (tooltip, detail) |
| Faction crests | 256 × 256 vector | SVG → `VectorImage` | 24, 64, 128 px |
| Cursors | 32 × 32 and 64 × 64 PNG | Texture (Cursor) | Native |

Item and block icons share one camera and light rig (SPEC-81 `AST-026`): orthographic, yaw 35°, pitch 30°, key light from the top left, transparent background, the direction's functional colours, no drop shadow (DRYDOCK, NOCTURNE) or a 4 px ink offset shadow (PULP HORIZON).

### 10.6 Function glyphs

Every functional block shows its glyph on every face that can be exposed (R3). Shapes are the same in all directions; only the rendering differs (DRYDOCK stencil paint, NOCTURNE emissive line, PULP HORIZON cream print).

| Function (`BlockCategory`) | Glyph | Description |
|---|---|---|
| Propulsion | Chevron | Two nested chevrons pointing toward thrust |
| Weapon | Crosshair | Circle with four ticks |
| Power | Bolt | Zig-zag lightning bolt (capacitor: bolt inside a rounded rectangle) |
| Shield | Ring | Thick circle open at the bottom |
| Core | Diamond | Diamond with a centre dot |
| Utility | Square | Square inside a square (cargo); later utility modules get their own inner mark |
| Notice: positive / warning / danger / info | ✓ check / ⚠ triangle with ! / ✕ cross / ⓘ circled i | UI only |

### 10.7 Naming conventions

ASCII only, no spaces. Content IDs stay lowercase `snake_case` (conventions §1); file-name segments use PascalCase joined by `_`.

| Asset type | Pattern | Example |
|---|---|---|
| Brick layer source texture | `TL_<NN>_<layerName>_<Map>.png` (`Map` = `Albedo`, `Mask`, `Normal`) | `TL_00_hull_plain_Albedo.png` |
| Baked texture array | `TA_Brick_<Map>.asset` | `TA_Brick_Albedo.asset` |
| Other texture | `T_<Group>_<Name>_<Map>.png` | `T_Prop_Trim_Albedo.png`, `T_Fx_Spark_Flipbook.png` |
| Material | `M_<Name>.mat` | `M_Brick.mat`, `M_Prop_Trim.mat`, `M_Sky_Procedural.mat` |
| Shader / Shader Graph | `SH_<Name>` | `SH_Sky_Procedural.shadergraph` |
| Mesh | `SM_<content_id>[_<Part>]_LOD<n>.fbx` | `SM_weapon_plasma_turret_small_Yaw_LOD0.fbx` |
| VFX Graph | `VFX_<Family>_<Name>.vfx` | `VFX_Debris_Cells.vfx` |
| Item / block icon | `ICO_<content_id>.png` | `ICO_block_hull.png` |
| UI glyph | `GLY_<name>.svg` | `GLY_undo.svg` |
| UI sprite | `UI_<Component>_<Variant>.png` | `UI_Bracket_Focus.png` |
| Cursor | `CUR_<Name>_<Size>.png` | `CUR_Default_32.png` |
| Font source / font asset | `FNT_<Family>-<Style>.ttf` / `FNT_<Family>-<Style>_SDF.asset` | `FNT_Barlow-Regular.ttf` |
| Sound effect | `SFX_<Category>_<Name>_<NN>.wav` | `SFX_Weapon_PlasmaFire_01.wav` |
| Music / ambience | `MUS_<Context>_<Name>.ogg` / `AMB_<Context>_<Name>.ogg` | `MUS_Explore_DriftingLight.ogg` |
| Definition asset | `<content_id>.asset` | `artdir_drydock.asset`, `brickmat_drydock.asset`, `vis_weapon_railgun_medium.asset` |
| Source file (outside `Assets/`) | `ArtSource/<Category>/<name>.<ext>` | `ArtSource/Blocks/weapon_railgun_medium.blend` |

### 10.8 Folder layout

| What | Folder |
|---|---|
| Brick layer sources, baked arrays, `M_Brick.mat` | `Assets/Game/Art/Bricks/` (`Layers/`, `Arrays/`) |
| Prop meshes, trim sheet, `M_Prop_Trim.mat` | `Assets/Game/Art/Props/` (`Meshes/`, `Textures/`) |
| Space: sky, stars, planets, gates | `Assets/Game/Art/Space/` |
| VFX graphs and textures | `Assets/Game/Art/VFX/` |
| Art-owned shaders (sky, planet, star, VFX, look-test reference) | `Assets/Game/Art/Shaders/` |
| Look-test scenes and their assets (removed at A2) | `Assets/Game/Art/LookTest/<direction>/` |
| Tokens, theme, component styles | `Assets/Game/UI/Theme/` (`tokens.uss`, `theme.tss`, `Components/*.uss`) |
| Fonts, glyphs, sprites, cursors | `Assets/Game/UI/Theme/Fonts/`, `Glyphs/`, `Sprites/`, `Cursors/` |
| Item and block icons | `Assets/Game/UI/Theme/Icons/` |
| Audio | `Assets/Game/Audio/Sfx/`, `Music/`, `Ambience/` |
| Art definition classes | `Assets/Game/Data/Definitions/Art/` (namespace `Game.Data.Definitions`) |
| Art definition assets | `Assets/Game/Content/Art/` (SPEC-04 decision 16) |
| Editable sources (`.blend`, `.kra`, `.svg` masters, `.mmg`, audio masters) | `ArtSource/` at the repository root, never under `Assets/`; binaries are marked `binary` in `.gitattributes` and move to Git LFS before the first push ([ADR-0008](../decisions/ADR-0008-no-lfs-yet.md)) |
| Licences and receipts | `docs/design/licenses/` and the register `docs/design/LICENSES.md` (SPEC-81 `AST-001`) |
| Placeholders (removed at A2) | `Assets/Game/Art/_Placeholder/` |

### 10.9 File formats and import settings

| Kind | Source format | Unity import settings |
|---|---|---|
| Colour textures (albedo, UI) | PNG 8-bit sRGB | sRGB on; BC7; mipmaps on (world) / off (UI); aniso 4 (world); wrap Repeat (world) / Clamp (UI) |
| Mask textures | PNG 8-bit, linear | sRGB off; BC7; mipmaps on |
| Normal maps | PNG 16-bit | Texture Type *Normal map*; BC5; mipmaps on |
| Brick layer sources | PNG exactly 256 × 256 | Read/Write on (the baker reads them); not referenced at runtime |
| Item / block icons | PNG 128 × 128, straight alpha | Texture Type *Sprite (2D and UI)*, single; BC7; mipmaps off; filter Bilinear |
| UI glyphs, crests | SVG | Imported as UI Toolkit *Vector Image* (built-in vector graphics module); fallback PNG at 2× only if an SVG fails to import |
| Meshes | FBX 7.4 binary from Blender: Forward −Z, Up Y, *Apply Transform* on, scale 1 | Scale factor 1; Read/Write off; normals Import; tangents Calculate Mikktspace; mesh compression Off; materials None; no animation, no cameras, no lights |
| Fonts | TTF/OTF static instances + `OFL.txt` next to them | FontAsset (TextCore): SDF AA, sampling point size 90, padding 9, atlas 1024 × 1024, dynamic, character set ASCII + Latin-1 Supplement + General Punctuation + arrows (U+2190–U+21FF) + ✓ ⚠ ✕ ⓘ |
| SFX | WAV 48 kHz 16-bit (masters 24-bit in `ArtSource/Audio/`) | 3D sounds Force To Mono; < 2 s Decompress On Load (Vorbis q 0.7); 2–10 s Compressed In Memory; > 10 s Streaming |
| Music, ambience | OGG Vorbis q 8 (masters WAV in `ArtSource/Audio/`) | Streaming; Load In Background |
| Cursors | PNG 32 × 32 and 64 × 64 | Texture Type *Cursor*; Read/Write on (required by `Cursor.SetCursor`) |

### 10.10 Texture memory budgets (runtime VRAM, approved direction only)

| Group | Contents | Budget |
|---|---|---|
| Brick arrays | 3 × `Texture2DArray` (albedo BC7, mask BC7, normal BC5), 256 × 256, ≤ 64 slices, full mips: 87,381 B per slice per array | ≤ 18 MB |
| Brick support | `_PaintPalette` 256 × 1, `_BrickLayerParams` 64 × 2, `_DamageRamp` 256 × 1, `_CrackTex` 256², `_MacroNoiseTex` 256², `_ToonRamp` 256 × 1 | ≤ 0.5 MB |
| Prop trim sheet | 3 × 2048² (BC7, BC7, BC5) with mips | ≤ 17 MB |
| VFX | Flipbooks ≤ 1024², noise textures | ≤ 16 MB |
| Space | Noise volumes (≤ 128³ R8), gradient LUTs, reflection cubemap 128 px RGBA Half | ≤ 8 MB |
| Planets and stars | Shared noise, 256 × 1 ramps; no per-planet bitmaps | ≤ 8 MB |
| UI | Icons (~120 × 128² BC7), sprites, font atlases (≤ 6 × 1024² A8) | ≤ 24 MB |
| **Total art textures** | | **≤ 92 MB (hard cap 128 MB)** |

No texture is larger than 2048². No texture is uncompressed except the 1-pixel-high LUTs and palettes (they must stay exact).

### 10.11 Polygon and LOD budgets

**Prop and functional block meshes** (counted for the whole block including moving parts; footprint from SPEC-04):

| Footprint cells | LOD0 triangles | LOD1 triangles (≤ 25 % of LOD0) |
|---|---|---|
| 1 | ≤ 150 | ≤ 40 |
| 2–4 | ≤ 600 | ≤ 150 |
| 5–18 | ≤ 1,500 | ≤ 375 |
| 19–64 | ≤ 3,000 | ≤ 750 |
| > 64 | ≤ 5,000 | ≤ 1,250 |

Other meshes: projectile / missile ≤ 100 triangles (no LOD); gate body LOD0 ≤ 20,000, LOD1 ≤ 5,000; planet sphere ≤ 8,192 triangles (shared mesh); star sphere ≤ 2,048. Brick meshes follow 06-performance-budgets (≤ 2 triangles per exposed face at LOD0).

**LOD and impostor visual rules** (SPEC-11 owns the switch logic and may tune distances; these visual rules are binding for art):

| Level | Default switch (camera distance to grid bounds, 10 % hysteresis) | What must still read |
|---|---|---|
| LOD0 | < 120 m | Everything: layers, AO, damage stages, props at LOD0 |
| LOD1 | 120–450 m | 2× downsampled brick mesh. Each 2 × 2 × 2 group takes the majority layer and paint of its solid cells, **except that a cell with a function paint (20–25) or an emissive layer always wins**; damage = the worst stage of the group; per-vertex AO off; props use their LOD1 mesh; no shadow casting |
| LOD2 / impostor | > 450 m or projected height < 96 px | 4× downsampled merged mesh or an impostor (SPEC-11 chooses); functional blocks drawn as their footprint cells in their function colour **with emissive on**; props hidden; no shadow casting |
| Debris particles and chunk bodies | Always | No shadow casting |

Function lights stay visible at every level (principle P3).

### 10.12 Brick layer plan

`BlockDefinition.materialLayer` (SPEC-04, 0–63) selects one slice of the brick texture arrays. The plan is shared by all directions, so changing direction never changes a block's layer number. Each layer is authored as a 2 × 2-cell macro-panel (`macroTile = 2`) unless marked 1, and must look correct on all six faces and on the wedge slope (stretched by √2 along the slope; no text, no circles on hull layers).

| Index | Name | Use | macroTile | DRYDOCK | NOCTURNE | PULP |
|---|---|---|---|---|---|---|
| 0 | `hull_plain` | Default plate (all legacy blocks) | 2 | ✓ | ✓ | ✓ |
| 1 | `hull_panel_a` | Plate variant A | 2 | ✓ | ✓ | ✓ |
| 2 | `hull_panel_b` | Plate variant B | 2 | ✓ | ✓ | ✓ |
| 3 | `hull_heavy` | Armour plate (bolts) | 2 | ✓ | ✓ | ✓ |
| 4 | `hull_hazard` | Hazard stripes (paint-locked to slots 26/27) | 1 | ✓ | → 1 | ✓ |
| 5 | `hull_window` | Window strip, emissive | 1 | ✓ | ✓ | ✓ |
| 6 | `hull_grate` | Grating | 1 | ✓ | ✓ | ✓ |
| 7 | `hull_vent` | Vents | 1 | ✓ | ✓ | → 6 |
| 8 | `hull_bare` | Unpainted metal frame | 2 | ✓ | ✓ | ✓ |
| 9 | `hull_light_strip` | Emissive light strip (team accent) | 1 | ✓ | ✓ | ✓ |
| 10 | `hull_marking` | Stencil band / hull-number field | 2 | ✓ | → 1 | ✓ |
| 11 | `hull_dock` | Docking marking | 2 | ✓ | → 1 | ✓ |
| 12–15 | reserved hull | — | — | | | |
| 16 | `internals_girder` | Exposed internals (shader-selected) | 1 | ✓ | ✓ | ✓ |
| 17 | `internals_conduit` | Exposed internals | 1 | ✓ | ✓ | ✓ |
| 18 | `internals_cavity` | Exposed internals | 1 | ✓ | ✓ | ✓ |
| 19 | reserved internals | — | — | | | |
| 20 | `station_plating` | Station hull | 2 | ✓ | ✓ | ✓ |
| 21 | `station_habitat` | Habitat with window rows | 2 | ✓ | ✓ | ✓ |
| 22 | `station_solar` | Solar panel | 2 | ✓ | → 20 | ✓ |
| 23 | `station_glass` | Glass | 1 | ✓ | ✓ | → 21 |
| 24–27 | reserved station | — | — | | | |
| 28 | `asteroid_rock` | Rock | 2 | ✓ | ✓ | ✓ |
| 29 | `asteroid_rock_dark` | Dark rock | 2 | ✓ | ✓ | → 28 |
| 30 | `asteroid_ice` | Ice | 2 | ✓ | ✓ | ✓ |
| 31 | `asteroid_metal` | Metal ore | 2 | ✓ | ✓ | ✓ |
| 32 | `asteroid_crystal` | Strange-matter crystal (emissive) | 1 | ✓ | ✓ | ✓ |
| 33 | `asteroid_regolith` | Dust-covered rock | 2 | ✓ | ✓ | → 28 |
| 34–39 | reserved asteroid | — | — | | | |
| 40 | `vanguard_ceramic` | Vanguard plate | 2 | ✓ | ✓ | ✓ |
| 41 | `vanguard_glass` | Vanguard glass | 1 | ✓ | ✓ | ✓ |
| 42 | `vanguard_light` | Vanguard light seam (emissive) | 1 | ✓ | ✓ | ✓ |
| 43 | `silence_void` | Silence matter | 1 | ✓ | ✓ | ✓ |
| 44 | `silence_growth` | Silence growth on human hull | 1 | ✓ | ✓ | ✓ |
| 45 | `xeno_chitin` | Xeno probe shell | 1 | ✓ | ✓ | ✓ |
| 46 | `xeno_membrane` | Xeno membrane (emissive) | 1 | ✓ | ✓ | → 45 |
| 47–61 | reserved | — | — | | | |
| 62 | `debug_checker` | Development checker | 1 | ✓ | ✓ | ✓ |
| 63 | `missing` | `block_missing` (SPEC-04, binding): magenta/black 1-cell checker | 1 | ✓ | ✓ | ✓ |

"→ n" means the slice is a copy of layer n in that direction (the index must still exist). Unused reserved slices are filled by the baker with a copy of layer 63. Layers that exist as distinct content: DRYDOCK 34, NOCTURNE 30, PULP HORIZON 29 (counting 62 and 63).

### 10.13 Paint palette and team colour slots

`PaletteEntry.Color` (architecture §6.3, one byte per palette entry) is an index into the global **paint palette**, a 256 × 1 texture. The layout is shared by all directions:

| Index | Meaning | Resolved |
|---|---|---|
| 0 | Default | Same as 17 (team primary) on paintable surfaces |
| 1–16 | Player paints `paint_01` … `paint_16` (legacy `PaintBucket_01`…`_16` order) | From the palette texture |
| 17 / 18 / 19 | Team primary / secondary / accent | **Per grid**, from the material-property components `_TeamPrimary/_TeamSecondary/_TeamAccent` (§10.14.4); the palette texture holds the ARK colours as fallback |
| 20–25 | Function colours: thruster, weapon, power, shield, core, utility | From the palette texture; never selectable by the player |
| 26 / 27 | Hazard light / hazard dark | From the palette texture |
| 28–31 | Vanguard ceramic, Vanguard light, Silence void, Xeno chitin | From the palette texture |
| 32–255 | Reserved; never written by content (validator error) | Copy of index 0 |

Player paints per direction (index: name → DRYDOCK / NOCTURNE / PULP HORIZON). Before approval the placeholder art direction uses the DRYDOCK column.

| Index | Paint (`paint_NN_…`) | DRYDOCK | NOCTURNE | PULP HORIZON |
|---|---|---|---|---|
| 1 | Dark Gray | `#4A4D50` | `#3A3F48` | `#5E6470` |
| 2 | Red | `#B3362C` | `#8E1F35` | `#E2483D` |
| 3 | Green | `#3E7D4A` | `#1F6B55` | `#3FA34D` |
| 4 | Blue | `#2F5F9E` | `#23458F` | `#2F6FD6` |
| 5 | Yellow | `#E0B021` | `#B8902A` | `#FFC233` |
| 6 | Orange | `#D9661F` | `#A8501F` | `#FF8A2B` |
| 7 | Purple | `#6B4A8E` | `#5A2E8C` | `#8A5CD6` |
| 8 | Cyan | `#2C9AA6` | `#1D7F95` | `#2FC2C9` |
| 9 | Pink | `#C76A8A` | `#9A3F72` | `#F27BAA` |
| 10 | Brown | `#6E4B33` | `#4D3428` | `#9A6440` |
| 11 | Black | `#1E1F21` | `#0E1014` | `#23252B` |
| 12 | Military Green | `#4F5B3A` | `#34432F` | `#6E8040` |
| 13 | Steel Blue | `#4D6D8A` | `#33506E` | `#4F7FA8` |
| 14 | Tan | `#B59E78` | `#8A7A5E` | `#E3C38E` |
| 15 | Dark Red | `#6E2024` | `#4F1622` | `#9C2F35` |
| 16 | White | `#E3DED2` | `#C9CED6` | `#F6EEDC` |

Indices 17–31 take their values from each direction's §x.3 tables (team fallback = ARK; function colours; hazard = DRYDOCK `#F2B705`/`#3B3E42`, NOCTURNE `#FFB84D`/`#1B1E24`, PULP `#FFC233`/`#2A3140`; Otherness colours).

### 10.14 Brick rendering contract (the master brick shader)

SPEC-11 implements the brick shader; this section is the **art-facing contract** it must expose. It is written to satisfy architecture §11.3.

#### 10.14.1 Scalability rules (binding for art, from architecture §11.3)

1. **One shared material for all opaque brick meshes** of every grid, every faction and every direction: `M_Brick.mat` using SPEC-11's master brick shader. All 64 brick layers live in the texture arrays of that one material, and the layer is selected **per vertex** (from `materialLayer`). This is the strictest form of "materials are shared, one per material layer, registered once": there is one material for the brick layer set. If SPEC-11 instead registers one material per `materialLayer` value, each of those materials uses the same shader, the same arrays and a fixed `_LayerIndex`; nothing in this contract changes.
2. **Per-cell variation comes only from the packed vertex** (layer, paint index, damage stage, AO, flags). **Per-grid variation comes only from material-property components** (§10.14.4). **Per-block variation on props comes only from material-property components** (§10.15). No material instance is ever created per grid, per brick, per block, per faction or for damage; no entity gets its own `RenderMeshArray`.
3. **SRP Batcher and Entities Graphics compatible.** Every material-level property in §10.14.3 lives in the `UnityPerMaterial` constant buffer; every per-grid property in §10.14.4 is declared for DOTS instancing (`UNITY_DOTS_INSTANCED_PROP`) and fed by an `IComponentData` with `[MaterialProperty("<name>")]`.
4. **No art switch adds a shader keyword.** Direction differences (toon ramp, rim, seams, halftone) are uniform branches on material properties. The brick and prop shaders add no `multi_compile` or `shader_feature` for art.
5. **Damage tint and heat** come from the per-vertex damage stage plus the per-grid `_LastHitTime` — never from a new material.

#### 10.14.2 Per-vertex data the art needs (SPEC-11 owns the exact packed layout, architecture §11.10)

| Field | Minimum bits | Source |
|---|---|---|
| Position in brick-local cells (0–16 per axis) | 6 per axis: half-cell ticks 0–32 packed in bytes, decoded ×0.5 (ADR-0027) | Mesher |
| Normal index (42 axis/diagonal/long-slope directions, ADR-0018) | 6 | Mesher |
| Brick layer (`materialLayer`) | 6 | `BlockDef.MaterialLayer` |
| Paint index (`PaletteEntry.Color`) | 8 | Grid palette |
| Ambient occlusion | 2 | Mesher (4 levels) |
| Damage stage (§10.14.5) | 2 | `Cell.Hp` |
| Exposed flag (face became visible because a neighbour cell was removed) | 1 | `Cell.Flags` (SPEC-10/13; optional — if absent, exposed faces look like normal hull) |

#### 10.14.3 Material properties the art team may change without code

All live on `M_Brick.mat`, written by the `BrickMaterialDefinition` baker (SPEC-80 `ART-007`); people edit the definition, not the material.

| Property | Type | Range / format | DRYDOCK | NOCTURNE | PULP |
|---|---|---|---|---|---|
| `_BrickAlbedoArray` | Texture2DArray | 256², ≤ 64 slices, BC7 sRGB, mips | baked | baked | baked |
| `_BrickMaskArray` | Texture2DArray | BC7 linear: R metallic, G smoothness, B paint mask (1 = replace albedo hue with the paint colour, keep albedo luminance), A emissive mask | baked | baked | baked |
| `_BrickNormalArray` | Texture2DArray | BC5 | baked | baked | baked |
| `_BrickLayerParams` | Texture2D 64 × 2 RGBAHalf, point, no mips | Row 0: emissive RGB (linear HDR), A = emissive mode (0 layer colour, 1 team accent); row 1: R macroTile (1 or 2), G scorchable (0/1), B crack strength 0–1, A edge wear 0–1 | baked | baked | baked |
| `_PaintPalette` | Texture2D 256 × 1 RGBA8 sRGB, point, clamp, no mips | §10.13 | baked | baked | baked |
| `_CrackTex` | Texture2D 256² R8 linear, repeat | Crack distance field | ✓ | ✓ | ✓ |
| `_CrackTiling` | float | cells per crack tile, 1–4 | 2 | 2 | 1 |
| `_DamageRamp` | Texture2D 256 × 1 sRGB | x = damage 0–1 → albedo multiply | ✓ | ✓ | ✓ |
| `_ScorchColor` | Color | | `#3A332C` | `#0A0B0E` | `#2A3140` |
| `_ScorchStrength` | float | 0–1 | 0.85 | 0.60 | 0.00 |
| `_EmberColor` | Color (HDR) | | `#FF5A1A` | `#FFFFFF` | `#FF8A2B` |
| `_EmberIntensity` | float | 0–16 | 4.0 | 6.0 | 1.5 |
| `_EmberFadeSeconds` | float | 0–30 | 8 | 6 | 6 |
| `_ExposedLayers` | Vector4 | x, y, z = layer indices, w = count | 16, 17, 18, 3 | 16, 17, 18, 3 | 16, 17, 18, 3 |
| `_AOStrength` | float | 0–1 | 0.80 | 0.60 | 0.50 |
| `_AOColor` | Color | | `#000000` | `#000000` | `#2A3140` |
| `_MacroNoiseTex` | Texture2D 256² R8, repeat | | ✓ | ✓ | ✓ |
| `_MacroScale` | float | cells, 16–256 | 64 | 64 | 64 |
| `_MacroStrength` | float | 0–0.3 | 0.06 | 0.03 | 0.00 |
| `_EmissiveIntensity` | float | global multiplier 0–16 | 1.0 | 1.0 | 1.0 |
| `_SeamEmission` | float | 0–2 | 0.00 | 0.35 | 0.00 |
| `_SeamFadeStart` / `_SeamFadeEnd` | float | metres | 0 / 0 | 150 / 450 | 0 / 0 |
| `_RimColor` | Color | | `#8FA6C8` | `#5FD0FF` | `#000000` |
| `_RimStrength` | float | 0–1 | 0.00 | 0.35 | 0.00 |
| `_ShadingMode` | float | 0 PBR, 1 toon ramp | 0 | 0 | 1 |
| `_ToonRamp` | Texture2D 256 × 1 | 3 steps 0.35 / 0.70 / 1.0 | — | — | ✓ |
| `_HalftoneScale` | float | screen px per dot cell, 4–12 | 0 | 0 | 6 |

#### 10.14.4 Per-grid material-property components (DOTS instanced; written by Presentation, never by art)

| Shader property | Type | Written from | Meaning |
|---|---|---|---|
| `_TeamPrimary`, `_TeamSecondary`, `_TeamAccent` | float4 (linear colour) | Grid `Allegiance` → faction → `ArtDirectionDefinition` faction look | Resolve paint indices 0, 17, 18, 19 |
| `_GridPowered` | float 0–1 | 1 while the grid has a core and power; animates to 0 over the direction's hulk time | Multiplies all emissive |
| `_LastHitTime` | float (shader time `_Time.y`) | Time of the most recent damage event on the grid | Drives ember fade (`_EmberFadeSeconds`) |
| `_Highlight` | float4 (rgb, strength) | Selection, target lock, editor hover | Additive rim highlight |
| `_Dissolve01` | float 0–1 | Chunk bodies only: 0 → 1 over 1.2 s before removal (SPEC-13) | Edge dissolve with the direction's ember colour (§x.7) |

#### 10.14.5 Damage stages

Stage = from `Cell.Hp` as a fraction of the block's `CellHp`: **0** ≥ 75 %, **1** 50–75 %, **2** 25–50 %, **3** < 25 %. A stage change must be visible within 0.25 s (SPEC-11/13 mark the brick mesh-dirty when a cell's stage changes; see SPEC-80 §10). Each direction's §x.3 damage table defines the look; prop meshes use `_Damage01 = 1 − FunctionHp / maxFunctionHp` with the same four thresholds.

#### 10.14.6 What needs code (SPEC-11 change through its Open questions)

New shader properties, a different packed vertex, a new shading model, a new per-grid or per-instance component, any new render pass. Everything in §10.14.3 is data.

### 10.15 Prop and functional mesh conventions

- **Units and pivot (SPEC-04):** 1 FBX unit = 1 cell; pivot at the centre of the root cell (0,0,0); footprint extends toward +X, +Y, +Z; SPEC-11 scales by `VoxelGrid.CellSize`. The mesh must stay inside its footprint cells plus 0.1 cell, except barrels and nozzles, which may extend 1 cell into the block's clearance cells.
- **Forward:** +Z is the block's business direction: weapons fire toward +Z; thruster exhaust leaves toward +Z (SPEC-04 clearance cells are at +Z).
- **One material:** every prop and functional mesh uses `M_Prop_Trim.mat` (one shared material, one 2048² trim sheet set). UV0 maps to the trim sheet. No other material is ever assigned to a prop mesh.
- **Vertex colour = paint selection:** R → team primary, G → team secondary, B → team accent, A → function colour; black = trim-sheet albedo. Channels are 0 or 1 (no blends).
- **Per-instance material-property components** (DOTS instanced): `_TeamPrimary`, `_TeamSecondary`, `_TeamAccent`, `_FunctionPaint` (float palette index 20–25), `_Damage01`, `_Powered` (0–1), `_Throttle` (0–1, thrusters only; drives nozzle emissive), `_LastHitTime`, `_Highlight`.
- **Blinking lights without code:** UV1.x on a prop mesh vertex is a blink phase. 0 = steady; a value in (0, 1] makes that vertex's emissive flash for 0.1 s once per second, offset by the phase (navigation lights use 0.25 left, 0.5 right, 0.75 aft). Material-level properties are the brick set minus the arrays, plus `_PropAlbedo`, `_PropMask`, `_PropNormal` (same channel conventions as the arrays).
- **Moving parts:** a turret is two meshes: the base (`SM_<id>_LOD0.fbx`, assigned to `BlockDefinition.propMesh`) and the yaw part (`SM_<id>_Yaw_LOD0.fbx`, in `BlockVisualDefinition.parts`, pivot on the yaw axis).
- **Sockets:** empties named `MUZZLE_00…`, `NOZZLE_00…`, `EMITTER_00`, `LIGHT_00…`, `FX_00…` in the FBX are converted at import into `BlockVisualDefinition.sockets` (SPEC-80 §4) and stripped from the mesh. A visual muzzle socket lies within 0.25 cell of the gameplay muzzle in `WeaponParams` (SPEC-04 §4.10); gameplay data always wins.
- **Glyph and band:** every functional mesh shows its function glyph (§10.6) on every exposed face and a function-colour area ≥ 2 cells long or a full 1-cell face (rule R1).

### 10.16 Space and lighting technical rules

- **No fog, no realtime or baked GI, no SSAO** in space scenes; voxel AO comes from the mesh.
- **Key light pitch** per direction (R4); azimuth = from the star toward the camera target. One shadow-casting light per scene.
- **Shadows** (SPEC-11 owns settings): at least 4 shadow-map texels per cell within 60 m of the camera.
- **Reflections:** one 128 px cubemap of the procedural sky, rendered once on star-system load; no realtime reflection probes.
- **Exposure:** fixed (no auto-exposure); post exposure 0 EV.
- **Spectral table** (key light and star disc colour by spectral class; each direction applies its saturation): O `#9BB0FF`, B `#AABFFF`, A `#CAD7FF`, F `#F8F7FF`, G `#FFF4EA`, K `#FFD2A1`, M `#FFCC6F`.
- **Sky:** one procedural sky shader `SH_Sky_Procedural` (stars hashed in 3 tiers, galaxy band, nebula noise); no cubemap or 6-sided skybox textures. Because the camera looks down, sky features are authored for the **lower hemisphere and the horizon band** (directions below the flight plane); the upper hemisphere is a plain gradient.
- **Emissive units:** shader emission = linear colour × intensity × `_EmissiveIntensity`; at the reference exposure a lit default hull is about 0.5, which is the scale used by the bloom thresholds in §x.4.

### 10.17 VFX technical rules

- All gameplay VFX are VFX Graph assets driven by `FxEvent` and `CellRemoved` (architecture §6.6); `FxEvent.Color` carries the damage-type colour from `ArtDirectionDefinition` (SPEC-80 §4).
- Debris inherits the cell's paint colour (palette index from `CellRemoved.Color`).
- Shared timings: hit flash 0.05–0.1 s; muzzle flash ≤ 0.06 s; small explosion 0.6 s; large 1.8 s; reactor 2.5 s; shield ripple 0.4 s; beam on 0.05 s / off 0.12 s; debris life 3–6 s; chunk dissolve 1.2 s; lane jump 1.5 s total (covers the ≤ 1.5 s star-system load in 06-performance-budgets).
- Budgets: debris follows 06 (50,000 live particles ≤ 1 ms GPU); at most 20,000 new debris particles per frame; transparent overdraw in heavy combat ≤ 4 full-screen layers at 1080p; no VFX texture over 1024².
- Fewer, larger particles beyond 300 m (half the count, double the size).

### 10.18 Audio technical rules

- Loudness: music −16 LUFS integrated; ambience −24 LUFS; SFX peaks ≤ −1 dBTP; UI sounds ≤ −20 LUFS short-term.
- 48 kHz; 3D SFX mono; each looping SFX loops seamlessly (zero-crossing, no click at 0 dB gain).
- Every cue has 2–4 variations where it repeats more than once per second (weapons, impacts, debris).
- Cues are referenced through `AudioCueDefinition` (SPEC-64), never by path.

### 10.19 Licensing and provenance

| Allowed | Not allowed |
|---|---|
| Made by the owner or a contributor who assigned rights in writing | Anything without a licence file or a receipt |
| CC0 / public domain (including NASA and ESA imagery marked public domain) | "Free for personal use", non-commercial (`NC`) or no-derivatives (`ND`) licences |
| SIL Open Font License 1.1 fonts (licence text shipped next to the font) | Share-alike (`SA`) licences for assets that ship in builds |
| CC-BY 4.0 with an entry in the credits file | Assets extracted from other games (the excluded Starsector files are the example) |
| Paid licences whose terms allow commercial games, with the licence file or receipt in `docs/design/licenses/` | Fonts with unverified licences (the placeholder **Kontrapunkt** has no licence file and is not used) |
| AI-generated assets only with the owner's explicit approval per asset and a tool whose terms allow commercial use, recorded in the register | Copyrighted images in mood boards stored in the repository (use links instead) |

Every shipped asset has a row in `docs/design/LICENSES.md` (path, source, licence, author, date, proof file).

### 10.20 Asset review checklist (every SPEC-81 task)

- [ ] Named and placed per §10.7–§10.8; import settings per §10.9.
- [ ] Within its texture budget (§10.10) and polygon budget (§10.11).
- [ ] Uses only `M_Brick.mat` or `M_Prop_Trim.mat` (world) — no new material for a block, grid, faction or damage state.
- [ ] Colours come from the approved palette tables (no new hex values without a §12 decision).
- [ ] Function glyph and function colour present on every exposed face (functional blocks).
- [ ] Reads at 50 / 150 / 300 m in the look-test camera (screenshot attached to the ledger log).
- [ ] Damage stages 0–3 visible (blocks and layers).
- [ ] UI: tokens only; contrast T1–T4 pass; colour-blind rules C1–C3 pass.
- [ ] Licence row in `docs/design/LICENSES.md`.
- [ ] Source file committed under `ArtSource/`.

---

## 11. Proposed additional tokens

Not binding until accepted in §12. `ART-002` writes them into the per-direction sheets; `ART-014` adds the accepted ones to `tokens.uss`. Exception: tokens marked **functional** are read by game code before approval (SPEC-32, SPEC-33, SPEC-61); their **names** are binding now and `tokens.uss` carries their placeholder values from `ART-001` (SPEC-80 §4.4). Only their values wait for §12.

| Token | Purpose | Placeholder | DRYDOCK | NOCTURNE | PULP HORIZON |
|---|---|---|---|---|---|
| `--color-border-strong` | Input and button boundaries (≥ 3:1, rule T2) | `#767A80` | `#737B84` | `#56769F` | `#8C9DC4` |
| `--color-text-on-accent` | Text on accent fills | `#111214` | `#0D0F11` | `#05070D` | `#0F1626` |
| `--color-overlay` | Modal scrim | `rgba(0, 0, 0, 0.6)` | `rgba(4, 5, 7, 0.65)` | `rgba(2, 4, 10, 0.70)` | `rgba(8, 12, 22, 0.60)` |
| `--color-objective` | Objective markers | `#E3BE5A` | `#FFD24A` | `#FFE08A` | `#FFE08A` |
| `--color-clue` | Clue, rumor and Whisper markers | `#C9A7FF` | `#B89CFF` | `#E0C3FF` | `#FF7FC8` |
| `--font-body-strong` | Bold body text | `OfficeCodePro-Bold_SDF.asset` | `FNT_Barlow-SemiBold_SDF.asset` | `FNT_Inter-SemiBold_SDF.asset` | `FNT_AtkinsonHyperlegible-Bold_SDF.asset` |
| `--easing-in` | Appearing elements | `ease-out` | `ease-out-cubic` | `ease-in-out-sine` | `ease-out-back` |
| `--easing-out` | Leaving elements | `ease-in` | `ease-in-cubic` | `ease-in-out-sine` | `ease-in-cubic` |
| `--icon-s` / `--icon-m` / `--icon-l` | Glyph sizes | 16 / 24 / 32 px | same | same | same |
| `--hud-margin` | HUD safe margin | 32 px | same | same | same |
| `--opacity-disabled` | Disabled controls | 0.4 | 0.4 | 0.4 | 0.4 |
| `--opacity-hud-panel` | HUD panel background opacity | 0.85 | 0.85 | 0.80 | 0.90 |
| `--color-faction-ark` | Player faction (map owner colour, HUD ally tint) — **functional: SPEC-33/61 read it from M3** | `#7AA7D9` | `#6FA8DC` | `#7FB2FF` | `#3FA7FF` |
| `--color-faction-raiders` | Raider owner/marker colour — functional | `#E5533D` | `#E0583A` | `#FF5A66` | `#FF4F3A` |
| `--color-faction-xeno` | Xeno owner/marker colour — functional | `#9B6CF0` | `#9C7BE8` | `#B48CFF` | `#C266FF` |
| `--color-faction-vestari` | Vestari — functional | `#4FC3A1` | `#4DBA9A` | `#5CE0C0` | `#2FD6A8` |
| `--color-faction-tlaxcala` | Tlaxcala — functional | `#E3A04F` | `#D99A4A` | `#FFB866` | `#FF9F1C` |
| `--color-faction-khuchad-clan` | Khuchad Clan — functional | `#C9C25A` | `#BFB656` | `#E6E07A` | `#E8D23A` |
| `--color-faction-neutral` | No owner / unknown faction — functional | `#C8C8C8` | `#B8BDC4` | `#A9B4C8` | `#D0D6E2` |
| `--color-travel-cover` | Jump cover colour (SPEC-32 §5.8): white-out, black-out or sky-key wipe — functional | `#F4F6FA` | `#F4F6FA` (white-out) | `#000000` (black-out) | `#FFB84D` (sky-key wipe) |
| `--duration-travel-fade` | Jump cover fade — functional, **timing contract, the same in every direction** | `0.5s` | `0.5s` | `0.5s` | `0.5s` |

---

## 12. Decision log

Filled at approval (`ART-013`) and for every later change. Each row: what was decided, by whom, and where it is applied.

| # | Date | Decision | By | Applied in |
|---|---|---|---|---|
| 1 | 2026-10-10 | At 150 m, 1-cell-wide functional blocks are identified by silhouette and function colour/light; their glyph is a 50 m feature (the ART-004 style test measured about 5 px per glyph at 150 m in all three directions). Owner's answer: "Colour at 150 m (Recommended)" | Owner | §3 P3, §4 R1, §5.5; SPEC-80 `ART-004` criterion 3 |

---

## Appendix A — Directions not chosen

Empty until approval. At `ART-013`, the sections of the two non-chosen directions are moved here unchanged, for the record.
