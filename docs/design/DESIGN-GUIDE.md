# Design guide: NOCTURNE

| | |
|---|---|
| **Status** | **BINDING — approved 2026-10-11 (NOCTURNE).** The source of truth for the look of the game; design-system gate `ART-013` passed |
| **Owner** | Frank |
| **Main guide** | NOCTURNE, as rendered in the look book (rounds 2–4), not as first written in the proposal |
| **Visual reference** | [Look book](lookbook/index.html) and [ship silhouettes](lookbook/silhouettes.html), kept as they are (§10) |
| **Technical appendix** | [DESIGN-SYSTEM.md](DESIGN-SYSTEM.md) §4 and §10–§11 (§0.3) |
| **Process spec** | [SPEC-80](../specs/SPEC-80-art-direction.md) |
| **Last edited** | 2026-10-11 |

> Owner, 2026-10-11, verbatim: "to be honest im in love with the NOCTURNE version so we will use it as the main guide, however, we must keep the look book as it is, as a visual reference. and make a new Design system guideline to use as the source of truth. this is because i don't want to lose the drydock, or aliens, or the 2d ship ideas (which btw are not yet approved)"

---

## 0. How to use this guide

### 0.1 What it decides

This guide says what the game looks like. When it disagrees with any other design document, render, mock or comment, **this guide wins**. The look book and the silhouette page show ideas. They are not rules unless a section here adopts them.

### 0.2 Status labels

Every rule and every reference item carries one of these labels.

| Label | Meaning | Who may change it |
|---|---|---|
| **Binding** | Use it as written. Code, content and art follow it | Owner, through a row in the decision log (§13) |
| **Guide** | The intended look. Follow it; adjust details when the in-engine result needs it, and note the change in §13 | Lead worker, recorded in §13 |
| **Reference** | Kept on purpose for later use or borrowing. Not part of the game until adopted here | Owner adopts it into a Binding or Guide section |
| **Not approved** | An idea waiting for the owner. Never built into the game as is | Owner |

### 0.3 The technical appendix

[DESIGN-SYSTEM.md](DESIGN-SYSTEM.md) was the three-direction proposal. It stays in the repo as the record of that proposal; only its header now points here. Its **direction sections** (§5 DRYDOCK, §6 NOCTURNE, §7 PULP HORIZON), its scorecard and its recommendation are **Reference** only. In particular, §6 describes an early NOCTURNE (glossy black hulls with light-line seams) that the look book replaced with industrial gothic.

Its **shared rules** stay **Binding** and are part of this guide by reference, except where a section here overrides them:

- §4 reference viewing conditions and rules R1–R4 (the key-light pitch in R4 is overridden by §4 here);
- §10 shared rules: token contract, colour-blind safety, contrast and luminance limits, sizes, icon grid, function glyphs, naming, folders, file formats, budgets, the brick layer plan, the paint palette layout, the brick rendering contract, prop conventions, technical rules for space, VFX and audio, licensing, and the review checklist;
- §11 proposed tokens, with their NOCTURNE column, and §12 decision row 1.

Specs that cite DESIGN-SYSTEM §10 keep working unchanged.

---

## 1. The direction in one paragraph (Binding)

**NOCTURNE is industrial gothic horror.** It is the mood of the *Alien* films with a cathedral's bones:
- hunched, ribbed ships in worn grey and ochre;
- oxblood and mustard machinery;
- slit windows of sodium light and red strobes;
- spires and flying buttresses on the capital ships;
- a storm world below, and something black and living out in the dark.

Human technology is heavy, old and lived-in, with ships patched for decades. Everything alien breaks those rules on purpose. The UI is the calm opposite: thin cyan holographic lines over the dark (§8).

The touchstones are described in words; no copyrighted images are stored.
- *Alien* (1979) and *Aliens*: the Nostromo's industrial weight, sodium light, and steam and grime.
- Gothic cathedrals: ribs, buttresses, lancet arches, spires and bells.
- *Warhammer 40,000* ship architecture, used sparingly: cathedral silhouettes on capital ships only.
- *Event Horizon*: the gothic ship as a threat.

---

## 2. Principles (Binding)

They follow the pillars in [00-vision](../00-vision.md), in priority order. When two principles conflict, the earlier one wins.

1. **Readability first.** Combat must read in a 30-ship fight. The value order on screen, from darkest to brightest:
   - space backdrop;
   - hull shadow side;
   - hull lit side;
   - windows, strobes and engines;
   - hits and explosions;
   - critical UI alerts.

   NOCTURNE gets its mood from **range** (deep blacks and bright highlights), not from darkening the whole frame. The owner's round-2 note "the black vents just look like pixel errors" is the cautionary example: dark detail that reads as noise is cut.
2. **Destruction you can read.** Damage shows as a change of value and colour at 150 m. Exposed internals look different from plate, and debris keeps the colour of the cell it came from (DESIGN-SYSTEM §10.17).
3. **Function by silhouette, light and colour, in that order.** Every functional block has its own silhouette and its own light colour (§3.3). Colour never carries meaning alone (DESIGN-SYSTEM §10.2).
4. **Scale is shown, not told.** Every structure over 100 m carries human-scale cues: plating at 1.5 m, lit slit windows on deck rows every 3 m, navigation lights, strobes and stencils.
5. **Form follows function, and every ship tells a story** (§5). This is the owner's rule from round 4: no novelty shapes, and no plain rectangles.
6. **Humans are blocky; aliens are smooth.** Human hulls are block-built and legible. Alien life is grown and smooth (§7).
7. **One coherent system.** One token set, one brick material, one prop material, one VFX grammar. Every asset traces back to a section of this guide.

---

## 3. Colour (Binding)

The values below are the ones the look book renders with (`ArtSource/LookDev/ld_styles.py`, `STYLES["nocturne"]`). Hex values are sRGB albedo unless marked as emission.

### 3.1 Hull base

| Role | Name | Hex | Notes |
|---|---|---|---|
| Hull plate | Worn grey | `#A3A49C` | Mosaic of plate tones: `#A3A49C` 42 %, `#AEADA4` 20 %, `#94968E` 22 %, `#A69E86` 10 %, `#8A8E7E` 6 % |
| Secondary hull | Ochre | `#A89060` | Default livery colour (`company`) |
| Armour | Gunmetal | `#575A52` | Metallic 0.6 |
| Trim, frames | Dark iron | `#34362F` | |
| Metal | Old steel | `#6A665B` | Metallic 0.85 |
| Shadow, interior | Near black | `#0D0E0C` | |
| Accent | Oxblood | `#B23A2A` | Stripes, hazard paint, faction marks |

### 3.2 Machinery colours (function paint)

| Function | Name | Hex |
|---|---|---|
| Weapons | Oxblood | `#7A2E24` |
| Power | Mustard | `#B08A2E` |
| Engines | Grey-green | `#4E524D` |
| Cargo | Olive | `#5E6448` |

### 3.3 Light (emission)

| What | Colour | Strength in look-dev | Rule |
|---|---|---|---|
| Windows | Sodium `#FFAE4A` | 7.5 | Slits on vertical faces only, on deck rows |
| Engines | Cold white-blue `#BFD6FF` | 8 | The only cold light on a human hull |
| Weapons | Red-orange `#FF5530` | 9 | |
| Core and power | Amber `#FFB347` | 9 / 7 | |
| Shield | Pale jade `#9FE0C8` | 5 | |
| Navigation | Red `#FF2410` (port), green `#7CFF9A` (starboard), white `#FFF1D6` (top) | 28 / 14 / 24 | Red strobes are part of the signature |
| Embers, heat | `#FF6A2A` | 9 | Hot damage, radiators |
| Exotic | `#A8CCFF` | 14 | Capital weapons only |

The function colours in the paint palette (DESIGN-SYSTEM §10.13, indices 20–25) take the **light** colours above. The **paint** colours (indices 1–16) use the NOCTURNE column of §10.13. Hazard colours (indices 26 and 27) are `#B23A2A` and `#0D0E0C`, replacing the proposal's `#FFB84D` and `#1B1E24`.

### 3.4 Liveries (Guide)

A livery recolours named tags on a hull. They are defined in `ld_styles.py`.

| Livery | Look | Use |
|---|---|---|
| `company` | Ochre secondary, oxblood accent | Default civilian and industrial |
| `salvage` | Grey-green secondary, pale gold accent | Salvagers, scavengers |
| `penal` | Rust-red secondary, bone accent | Penal and military |
| `knell` | Bone plate, blackened bronze ribs, gilt trim, candle-lit lancet windows (`#FFB65A`) | The Knell choir (not approved, §10) |
| `arkw` | Blackened graphite, ash-grey armour blocks, sodium-yellow magazines, red team stripe | The ARK war line (not approved, §10) |

---

## 4. Light and space (Binding)

### 4.1 Lights

| Light | Colour | Strength | Direction (relative to the camera) |
|---|---|---|---|
| Key (the star) | `#EEF2FF` | 3.4 | Azimuth −60°, elevation 40°; shadows on |
| Fill | `#4A5A48` (green-grey) | 0.18 | Azimuth 80°, elevation −20° |
| Rim | `#D0E4D8` | 3.0 | Azimuth 170°, elevation 15°; separates dark hulls from dark space |
| Ambient | `#0C100E` | 0.18 | Flat |

The key light is pitched 40° above the flight plane. This overrides DESIGN-SYSTEM rule R4's 25° for NOCTURNE: the look book showed that top faces need the steeper key.

### 4.2 Grade

| Item | Value |
|---|---|
| Tonemapping | AgX, *High Contrast* look, exposure −0.02 |
| Bloom | Threshold 0.85, intensity 0.5 |
| Haze | `#58605A`, very thin (density 0.0006), forward scattering 0.65 |
| Not used | Depth of field, tilt-shift, chromatic aberration, motion blur |

### 4.3 Sky

- Near-black base `#020303`, with sparse stars (0.6 of the reference density).
- Green smoke nebula (`#2A3A30` to `#4A3A22`) with a pale green glow.
- On one side, a violet nebula (`#4A2468` to `#22285E`); on another, a low amber glow (`#8A3E1E`); thin filaments throughout. The owner's round-2 note asked for this variety: "the sky is no longer all green".
- Dark dust lanes (`#070605`).
- The luminance cap of DESIGN-SYSTEM §10.3 applies: the sky is never brighter than the ships on it.

### 4.4 Worlds

- **Storm worlds**, the signature planet (VESPER-9): bands of rust, bruise-violet and teal, swirls, a dark polar hood, one great pale storm, and permanent lightning (`#CFE6FF`). The atmosphere rim is pale sand (`#C9B98A` to `#E8DCB8`).
- **Moons:** dark rock (`#3A3A36` to `#55534C`).
- **Other planet types** follow the same grade: desaturated, banded and heavy. They are designed per system in SPEC-31.

---

## 5. Ships and hulls

### 5.1 Hull language (Binding)

- **Hunched and ribbed.** Hulls are heavy at the shoulders, with exposed ribs (vertical fins on the hull walls), slit windows in rows, and vents and pipe runs. The cut-cell slopes and large blocks used in the look book are the intended look; how they become game blocks is still open (§12, item 2).
- **Capital ships carry the cathedral:** spires, flying buttresses, keel spikes, organ-pipe silos and lancet arches. Smaller ships stay plain and industrial, so the cathedral reads as rank.
- **Every hull has a lit side and a slit-lit dark side.** Windows, strobes and engines carry the dark side; the rim light carries the silhouette.
- **Large ships use large blocks:** 1 m (2×2×2 cells) for cruisers and 1.5 m (3×3×3) for dreadnoughts, with human-sized windows, hatches and small guns on top.
- **Avoid:** flat single-colour hulls (the round-2 failure), dark vents that read as pixel errors, and jagged misaligned corners (the owner's round-2 note).

### 5.2 Plating (Guide)

The plated hull textures (`ArtSource/LookDev/ld_plating.py`) are a 6 m procedural tile on the 1.5 m block grid. Each block is split into plates down to about 0.2 m, with:
- relief, bolts and rivets;
- grilles, hatches and stencilled numbers;
- lit windows on vertical faces only.

The plating is projected at the same human scale on every block, so big hulls read big. In NOCTURNE, human hulls use the worn-grey plating. The Knell and ARK plates are variants (§3.4).

### 5.3 Ship design rules (Binding)

These rules come from the owner's round-4 feedback.

- **Silhouettes first.** A 2D plan-view silhouette is approved before any 3D build. This is an approval step: an unapproved silhouette is an idea (§10).
- **Form follows function.** Every ship has one niche and one counter: "a group of 5 skilled frigates can EASILY down a cruiser class if used correctly". The outline tells that story. No plain rectangles, and no novelty tool shapes ("its supposed to be the future and you're making hammers and hooks??").
- **A ship line is unified by its paint and build pattern,** not by one outline. The black silhouette must be memorable on its own.
- **Asymmetry with a reason:** offset bridges, tanks, cranes, decks, lances and bolted-on refits.
- **A mix of builds:** bulky (low tech), slick (high tech) and hybrid hulls.
- **Refits:** civilian hulls re-specced for war. Pirate refits are faster and weaker; military refits are heavier.
- **Codex depth** on every hull: faction, tech level, niche, fit, ship system, built-in mods, variants and what it is weak to.
- **Rammers are a role:** a heavy replaceable prow, a load-bearing spine, reverse thrust, and nothing fragile at the bow.

### 5.4 Damage stages (Guide)

Industrial gothic damage is **physical**: plates scorch and blacken, edges glow ember (`#FF6A2A`) for a few seconds, ribs and frames show through the holes, the windows in a hit section go dark, and red strobes keep blinking on a dead hull. A ship that loses its core goes dark within 1 second: windows and engines out, colours dulled.

This replaces the "light dying" damage table in DESIGN-SYSTEM §6.3. It is checked against principle 1 in the first in-engine look test.

---

## 6. Factions (Guide; the roster is not approved)

Faction colours in NOCTURNE use the NOCTURNE paint column of the batch-3 silhouette page (`sil_batch3.py`, `PAL[...][1]`). The functional faction tokens (DESIGN-SYSTEM §11, NOCTURNE column) stay as they are.

| Faction | NOCTURNE look | Status |
|---|---|---|
| ARK expedition (the player, strangers here) | Graphite and ash grey, warm-white light, hazard stripes, red and green nav lights; plainer and more foreign than the clans | Faction from the lore; ship lines not approved |
| Khuchad Clan | Burnt hulls, rust armour, ember accents | Faction from the lore; ships not approved |
| Triple Alliance (Tlaxcala) | Deep green, jade modules, lime light, house bands | Faction from the lore; house names not approved |
| Vestari | Obsidian blue, pearl planes, ice-blue light | Faction from the lore; ships not approved |
| Scavengers | Soot, oxide, sodium light, mismatched panels | Faction from the lore; ships not approved |
| Knell choir | Bone plate, blackened bronze, gilt, candles and bells | Not approved (batch 2, roster placement open: ledger question 7) |

---

## 7. The alien and the unknown (Binding)

Everything alien breaks the human rules on purpose: different geometry, different light and different sound.

- **Xeno ships (the aliens)** are grown, not built. The cells hold depth values and are meshed smooth by surface nets, so damage comes out organic, with glowing flesh inside. In NOCTURNE the default colourway is **Obsidian & acid**: shell `#121315` and `#26272C`, grooves `#030304`, veins `#B8FF2E`, glow `#D4FF6A`. The other colourways (Bone & jade, Blood coral, Oil slick) are Reference, kept for hive variants. The look book's five aliens are the reference designs: HUSK, BROODMOTHER, LAMPREY, CRINOID and GRAVID.
- **The Vanguard (precursor derelicts)** use mirror-white ceramic and glass-black panels, with chevron and fluted plates. They never have the human square seam. Their seams are prismatic in NOCTURNE, cycling hue every 6 s.
- **The Silence** is matte void that reflects nothing (`#000000`), with a violet fringe (`#6E3BFF`). It has no engines, no debris and no wreck: what it touches is gone.

---

## 8. UI (Binding)

The owner, round 2, verbatim: "great UI … (no ui changes, its good)". The NOCTURNE UI in the look book is the UI.

- **Tokens:** `tokens.uss` takes the **NOCTURNE column** of DESIGN-SYSTEM §8.1 and §11, which have been checked for contrast. For example, the background is `#05070D`, text `#DCE8F5`, accent `#39D5FF`, and the corner radii are 0 / 0 / 2 px. The look book's mock uses `#5FD0FF` as its accent; the token value wins.
- **Fonts** (all SIL OFL 1.1): display **Michroma** (wide, uppercase, letter-spaced), body **Inter**, mono **JetBrains Mono**.
- **Style:** translucent dark panels with a light blur, 1 px lines, and corner brackets on focused panels. Thin bars with a soft glow. Display type has a faint cyan glow. Icons are 1.5 px line glyphs with a 30 % accent glow. Motion runs at 120 / 250 / 500 ms with sine easing.
- **HUD:** the shared zones (DESIGN-SYSTEM §10.4.2), with the NOCTURNE variations from DESIGN-SYSTEM §6.8: hull and shield arcs around the player ship, a circular radar, and NORA as a ring of dots.
- **Contrast with the world:** the cyan UI sits over sodium and ochre on purpose. It is the crew's instrument, clean against a dirty world.

---

## 9. VFX and audio (Guide)

**VFX.** The proposal's NOCTURNE effects (DESIGN-SYSTEM §6.7: implosions, ion-violet light) were written for the glossy direction and are **Reference** only. Industrial gothic effects:
- **Explosions:** a fireball that chokes into black smoke quickly, embers and hull fragments.
- **Kinetic hits:** sparks and dust. **Energy hits:** a red-orange scorch.
- **Engine plumes:** cold white-blue.
- **Shields:** a pale jade ripple, mostly invisible until hit.

Damage-type colours: kinetic `#FFFFFF`, energy `#FF5530`, explosive `#FFB347`, ram `#FF6A2A`. They are designed in SPEC-63 against this list and principle 1.

**Audio.** DESIGN-SYSTEM §6.9 is adopted, with the horror turned up:
- **Music:** dark ambient (drones, choir pads, long reverbs).
- **Hulls:** creak, and alarms sound like sirens.
- **The Silence:** cuts every reverb tail to nothing.

---

## 10. Reference library (kept on purpose)

None of this is lost. Each item keeps its status until the owner changes it here.

| Item | Where | Status | Notes |
|---|---|---|---|
| NOCTURNE renders, rounds 2–3 | Look book, NOCTURNE tab; `img/r2/nocturne`, `img/r3/nocturne` | **Reference** for this guide | The look this guide describes. Ship designs in them (WRAITH, CANTICLE, REQUIEM, SEPULCHRE, THE ROOD) are concepts, not approved hulls |
| DRYDOCK, rounds 1–3 | Look book, DRYDOCK tab | **Reference** | Industrial used-future. Elements may be borrowed into NOCTURNE through §13, for example work lights, container decks or safety colour coding |
| OPERA, PULP HORIZON, round-1 directions | Look book, OPERA tab and archive | **Reference** | |
| Aliens (five types, four colourways) | Look book, ALIENS tab | **Reference**; the technique and NOCTURNE colourway are **Binding** (§7) | The owner on GRAVID: "that alien behemoth looks so scary i love it" |
| Ship silhouettes, batch 2 (Caisson, Knell) | `silhouettes.html`, Batch 2 tab | **Not approved** | |
| Ship silhouettes, batch 3 (the clans, the ARK, rammers, refits) | `silhouettes.html`, Batch 3 tab | **Not approved** | The owner liked batch 3 better than batch 2; nothing is approved yet |
| Ship silhouettes v1 | `silhouettes-v1.html` | **Rejected**, kept for the record | Novelty tool shapes |
| HALO in 3D | Look book, ARK tab | **Reference** (concept) | The owner: "its fine this is just visual concepts not necessarily the final look of the ships" |
| Block kits and kit icons (rounds 1–4) | Look book, each direction tab | **Reference** | The game's block list is set by SPEC-04, not by the kits |
| Mood boards, design board | `docs/design/moodboards/`, `docs/design/board/` | **Reference** | Tasks `ART-003` and `ART-005` |
| DESIGN-SYSTEM.md direction sections | §5–§9 there | **Reference** | §0.3 |

The look book and silhouette page are **frozen as visual reference**. New look-dev work adds new tabs or new rounds; it never rewrites what is there.

---

## 11. Token and content contract (Binding)

- **Token names** never change (DESIGN-SYSTEM §10.1). `ART-014` writes the NOCTURNE values into `Assets/Game/UI/Theme/tokens.uss`.
- **Paint palette:** indices 1–16 take the NOCTURNE column; indices 17–31 take the values in §3.3, §6 and §7 (DESIGN-SYSTEM §10.13 layout).
- **The active `ArtDirectionDefinition`** is NOCTURNE.
- **Fonts allowed:** Michroma, Inter and JetBrains Mono (SIL OFL 1.1). Other fonts from the look book stay in the look book.

---

## 12. Open decisions

| # | Decision | Default until answered | Decider |
|---|---|---|---|
| 1 | Is this guide the design-system approval (gate `ART-013`), so that game systems can start? | **Answered 2026-10-11: yes** (§13 row 5). The in-engine look test (`ART-008`…`ART-011`) runs after the renderer exists, as a check | Owner |
| 2 | How hulls become game blocks: cut cells, a fixed slope set, or cube and wedge (ledger question 6) | Specs as written (cube and wedge) until the owner picks a kit from the slope study (look book, *Slopes* tab): slope + hip corner (2 blocks), plus a long 1×2 slope (3), or plus a long hip corner (4). Owner, 2026-10-11: "lets model the different sloped blocks so we can see how many are needed (ideally as least as possible)" | Owner |
| 3 | Which ship lines and factions are approved (the silhouettes, ledger question 7) | None approved | Owner |
| 4 | Industrial gothic VFX (§9) and damage (§5.4) in engine | As written here, checked in the first look test | Lead worker |

---

## 13. Decision log

| # | Date | Decision | By | Applied in |
|---|---|---|---|---|
| 1 | 2026-10-10 | At 150 m, 1-cell-wide functional blocks are identified by silhouette and function colour; their glyph is a 50 m feature | Owner | DESIGN-SYSTEM §12 row 1 (carried over) |
| 2 | 2026-10-10 | Round 2: "no ui changes, its good" for NOCTURNE; NOCTURNE 3D to go "horror sci-fi … like the ALIEN franchise movies" | Owner | §1, §8 |
| 3 | 2026-10-11 | NOCTURNE is the main guide; this guide is the source of truth; the look book stays as it is as visual reference; DRYDOCK, the aliens and the silhouettes are kept; the silhouettes are not approved | Owner | Whole guide; §10 |
| 5 | 2026-10-11 | **Design system approved: NOCTURNE, as this guide.** Owner, verbatim: "yes I approve" (answer to: does choosing NOCTURNE plus this guide count as the design-system approval). Production art (SPEC-81, A1) and the game milestones M0–M4 may start | Owner | Gate `ART-013`; ledger *Owner gates*; ROADMAP; SPEC-80 §12 |
| 4 | 2026-10-11 | Key light pitched 40° (overrides R4's 25° for NOCTURNE); hazard colours `#B23A2A` and `#0D0E0C`; function colours are the light colours of §3.3; NOCTURNE damage and VFX are physical (ember, scorch, smoke), not "light dying" | Lead worker (claude-opus-5-5), from the look-book renders the owner chose | §3.3, §4.1, §5.4, §9 |
