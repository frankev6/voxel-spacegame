# Mood boards

One board per art direction of [DESIGN-SYSTEM.md](../DESIGN-SYSTEM.md): [DRYDOCK](drydock.md), [NOCTURNE](nocturne.md), [PULP HORIZON](pulp-horizon.md). Made by `ART-003` ([SPEC-80](../../specs/SPEC-80-art-direction.md)); they feed the design board (`ART-005`) and the owner's shortlist (`ART-006`).

A mood board shows what each direction is reaching for and what it must avoid. It is not a spec: every value (colours, timings, sizes) lives in DESIGN-SYSTEM.md.

## Rules

- **No copyrighted images in the repository** (SPEC-80 Decision 12, DESIGN-SYSTEM §10.19). A reference is a link unless its image is public domain, CC0 or own work.
- Images: JPEG quality 85, long side ≤ 1600 px, ≤ 400 KB, at most 12 per direction, stored in `img/<direction>/`.
  Encode with a libjpeg-based encoder (Pillow, ImageMagick, GIMP) so the checker can verify the quality; macOS `sips` uses other tables and the quality check is then skipped.
- No Starsector material and no screenshots of other games stored here; link to them.

## Format (checked by `tools/art/check_art_docs.py moodboards`)

```markdown
## Ships and stations                    ← one of the six groups, or "Anti-references"

### Nostromo and the Alien hardware      ← one reference
- **Creator:** Ron Cobb (concept design); Ridley Scott (director)
- **Year:** 1979                         ← YYYY or YYYY–YYYY
- **Take:** One sentence on what to take (anti-references: what to avoid).
- **Touchstone:** Ron Cobb               ← optional; must be a phrase from the direction's DESIGN-SYSTEM §x.2 list
- **Link:** https://…                   ← or a markdown link to a file in this repository

![Alt text](img/drydock/example.jpg)    ← optional; only public domain, CC0 or own work
Source: https://… · Licence: Public domain
```

Groups: *Ships and stations*, *Space and lighting*, *Destruction and VFX*, *UI and typography*, *Otherness*, *Audio playlist* (at least 2 each, 16–24 in total), then *Anti-references* (at least 3). Each visual group names at least one DESIGN-SYSTEM touchstone.
