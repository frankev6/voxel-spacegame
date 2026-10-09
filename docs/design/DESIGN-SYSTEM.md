# Design system and art direction

| | |
|---|---|
| **Status** | **PROPOSAL — awaiting owner approval** |
| **Owner** | Frank |
| **Process spec** | [SPEC-80 Art direction and design system](../specs/SPEC-80-art-direction.md) (area `ART`) |
| **Production spec** | [SPEC-81 Asset production](../specs/SPEC-81-asset-production.md) (area `AST`), blocked behind the gate `ART-013` |
| **Binding inputs** | [00-vision](../00-vision.md) pillars, [02-conventions §4–§5](../02-conventions.md), [ADR-0009](../decisions/ADR-0009-placeholder-art-and-design-gate.md), [SPEC-04 §4.5](../specs/SPEC-04-content-and-blocks.md) (`materialLayer`, `propMesh`, `icon`) |
| **Last edited** | 2026-10-09 |

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
| Item icons are outlined pixel art at mixed scales (`22-inventory`, `09-trade`) | Clashes with vector UI, unreadable at 32 px | One icon style per direction; block icons rendered from the real block assets (SPEC-81 `AST-025`) |
| Every beam is the same white line (`18-12-enemies`) | Weapons and damage types are indistinguishable | VFX colour by damage type and weapon family; core/halo structure (§5.7, §6.7, §7.7) |
| Debris cubes are pale beige whatever they came from (`11-firing`) | Destruction does not read as "my ship lost those blocks" | Debris inherits the cell's paint colour and starts hot (§10.17) |
| HUD texts overlap ("AUTOFIRE" over "FUEL"), stats sit on top of each other (`23-fleet`) | Unreadable | Fixed HUD zones (§10.4.2) and layout tests |

**Kept from the prototype:** number-key dialog choices (`[1] Visit Marketplace`), cube debris, segmented resource bars as an idea, the calm dark-navy screen backgrounds, NORA as a text-first companion.

---

## 3. Design principles

These derive from the pillars in [00-vision](../00-vision.md) in the same priority order. When two rules conflict, the earlier principle wins.

**P1 — Readability first (pillar 1).** The screen has a fixed value hierarchy, darkest to brightest: space backdrop < hull shadow side < hull lit side < function lights < hit flashes and explosions < critical UI alerts. Measurable limits are in §10.3. The backdrop is never brighter than the ships on it.

**P2 — Destruction you can read (pillar 1).** Every damage stage is visible at 150 m as a change of value or hue (§10.14 damage stages). A removed cell leaves faces that look different from original hull (exposed internals). Debris inherits the colour of the cell it came from. A ship that loses its core is visibly dead within 1 second: its lights go out and its colours dull. Nothing pops out of existence: chunk bodies dissolve over 1.2 s, debris shrinks to zero.

**P3 — Block function is legible by silhouette, glyph and colour (pillar 2).** In that order of robustness. Every functional block has a distinct mesh silhouette, a function glyph (§10.6) and a function colour. At 300 m the function colour/light must read; at 150 m the glyph; at 50 m the details. Colour alone never carries meaning (colour-blind safety, §10.2).

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

- **R1 (4-pixel rule).** Anything the player must read in combat is at least 4 px at 150 m, which means **at least 2 cells (1 m)** on its smallest axis. Single-cell functional blocks therefore use their whole visible face for the glyph.
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

| Function (`FunctionKind`) | Colour | Emissive intensity (§10.14 units) | Where it appears |
|---|---|---|---|
| Thruster | Amber `#FF9A1F` | 0.6 idle → 3.0 full thrust | Nozzle ring, 1-cell band around the mount face |
| Weapon | Signal Red `#E5332A` | 2.0, pulses to 3.0 while charging | Muzzle ring, mount band |
| Power (`Reactor`, `Capacitor`) | Volt Yellow `#F5D33A` | 2.5 | Vents, top panel |
| Shield (`ShieldEmitter`) | Shield Blue `#3D8BFF` | 2.5 | Emitter dish rim |
| Core | Command Green `#3DDC84` | 3.0, pulsing at 0.5 Hz | Beacon dome, top panel |
| Utility (`Cargo`, `BeamCombiner`, `ProjectilePortal`) | Utility Magenta `#E04AC8` | 1.5 | Hatch frame |
| Structure | none | 0 | Paint only |

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
- **Functional blocks** are prop meshes (SPEC-04 §4.10 gives every functional block a `propMesh`). Each has: a 1-cell function band in its function colour around the mount face; its function glyph (§10.6) stencilled on every exposed face; one emissive indicator ≥ 2 cells long or a full 1-cell face. Readability targets: 300 m → a 2–4 px coloured light; 150 m → the glyph; 50 m → the stencil code (`THR-S`, `WPN-PL`, `PWR-R`, `SHD`, `CORE`, `CRG`).
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
| Brick layers used | 30 of the 64-slot plan (no `hull_hazard`, `hull_marking_a`, `hull_dock`, `station_solar` as separate looks; they reuse `hull_panel_a`) |

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
| Brick layers used | 26 of the 64-slot plan (asteroid layers reduced to 4, station layers to 3) |

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

<!-- PART-3 -->
