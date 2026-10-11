# LookDev: 3D look-development renders

Procedural Blender 5.2 scenes for the art directions in `docs/design/lookbook/`. Everything is generated from code: no imported art, no AI images, fonts are SIL OFL.

Round 3 (current) builds DRYDOCK, NOCTURNE and OPERA into `docs/design/lookbook/img/r3/`. That covers the cruiser and dreadnought classes, capital weapons, defended stations and five alien types. Round 2 renders stay in `img/r2/` (code at `f3d4c7d`); round 1 renders stay in `img/<direction>/` (code at `1534b2e`).

```bash
python3 ArtSource/LookDev/lookdev.py                                   # all directions, kit icons + scenes (~15 min)
python3 ArtSource/LookDev/lookdev.py --direction nocturne --stage scenes --draft --only fleet,lineup
python3 ArtSource/LookDev/lookdev.py --stage check                     # build rules for every design (~30 s, no rendering)
python3 ArtSource/LookDev/lookdev.py --stage r4 --direction drydock,nocturne   # round-4 ships from approved silhouettes (~1.5 min)
python3 ArtSource/LookDev/silhouettes.py                               # the silhouette page, plus reference SVGs for ships built in 3D
```

The check stage applies the rules the game's builder will enforce:

- **Attachment:** every block joins the ship through real shared faces. Touching at an edge or a corner does not count.
- **Mounting:** every module sits on its mount face, for example turrets on their base and engines on their front.
- **Shapes:** it counts how many distinct cut-cell shapes the hulls use, up to rotation and mirroring.

It writes `docs/design/lookbook/buildcheck.js`. The scenes stage also writes `lineups.js`, which holds the label positions for the scale line-ups.

| File | What it holds |
|---|---|
| `ld_styles.py` | The directions: <ul><li>palette and function colours, liveries, materials</li><li>lighting rig (azimuth and elevation relative to the camera)</li><li>sky, planet, greeble densities, UI fonts</li><li>the alien colourways</li></ul> |
| `ld_geom.py` | <ul><li>Primitives</li><li>The voxel mesher with exactly clipped cells: lofts and convex pieces, so slopes stay single planes</li><li>Surface nets for alien hulls: a dict version and a vectorised dense-grid version</li></ul> |
| `ld_blocks.py` | The block kit, built per flavour (industrial, gothic, opera) inside each grid footprint; procedural hull greebles |
| `ld_capital.py` | The capital kit: <ul><li>large hull blocks: 2×2×2, 3×3×3, slopes, corner, armour block, truss</li><li>capital weapons: triple battery, spinal particle lance, singularity projector, storm coil, graviton snare, nova silo, drone carrier bay</li><li>the capital engine</li></ul> |
| `ld_ships.py` | The `Ship` class: <ul><li>0.5 m cells, plus an optional big-block layer (`unit`) for large ships</li><li>building pieces: lofts along y or x, towers along z, convex pieces, spires, faceted spheres and rings</li><li>modules, liveries, carving</li><li>exact merging of two cut pieces in one cell, and sliver pruning</li></ul> Also the zoned greebles, and the light and frigate designs for each direction |
| `ld_capships.py` | Cruisers and dreadnoughts: STEVEDORE and ANVIL (DRYDOCK), REQUIEM and SEPULCHRE (NOCTURNE) |
| `ld_ark.py` | Round 4: the ARK ships built from approved batch-3 silhouettes (`sil_batch3.py`). So far the HALO (ARK war line), in the `arkw` livery with `ark` plating |
| `ld_r4.py` | The round-4 stage: hero, game camera, stern, plan and detail shots of each built ship, plus its build check, into `img/r4/<direction>/` |
| `ld_check.py` | The build rules (attachment, mount faces, shape catalogue) |
| `ld_aliens.py` | Alien hulls: signed-distance primitives sampled on the cell grid and meshed by surface nets. Parts (spines, sacs, eyes, tendrils). HUSK, BROODMOTHER, LAMPREY, CRINOID, GRAVID |
| `ld_stations.py` | PORT KESSLER (DRYDOCK hub), THE ROOD (NOCTURNE), THE CROWN (OPERA), each with defences |
| `ld_mats.py`, `ld_world.py` | <ul><li>Materials: panel mosaic, scaled plating for big blocks, streaks, paint bands, heat tint, alien carapace</li><li>Skies, lights, planets, limb-scattering atmospheres, lit and shadowed rings, haze, moons, asteroids</li></ul> |
| `ld_scenes.py` | The shots: <ul><li>hero, flight and combat at the game camera (55° pitch, 40° FOV)</li><li>station, planet, Blueprint Lab</li><li>close-ups of each ship class</li><li>scale line-ups, fleet, superweapon, the alien shots</li><li>kit icons, hull-variant slab</li></ul> |

Logs go to `Logs/tools/lookdev-<direction>-<stage>.log`.
