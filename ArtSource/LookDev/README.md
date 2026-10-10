# LookDev: 3D look-development renders

Procedural Blender 5.2 scenes for the art directions in `docs/design/lookbook/`. Everything is generated from code: no imported art, no AI images, fonts are SIL OFL.

Round 2 (current) builds DRYDOCK, NOCTURNE and OPERA, plus the alien enemies, into `docs/design/lookbook/img/r2/`. Round 1 (six directions) is kept as renders in `img/<direction>/`; its code is at commit `1534b2e`.

```bash
python3 ArtSource/LookDev/lookdev.py                                   # all directions, kit icons + scenes (~5 min)
python3 ArtSource/LookDev/lookdev.py --direction nocturne --stage scenes --draft --only combat,hero
```

| File | What it holds |
|---|---|
| `ld_styles.py` | The directions: palette and function colours, liveries, materials, lighting rig (azimuth/elevation relative to the camera), sky, planet, greeble densities, UI fonts; the alien colourways |
| `ld_geom.py` | Primitives; the voxel mesher with exactly clipped cells (lofts and convex pieces, so slopes stay single planes); surface nets for alien hulls |
| `ld_blocks.py` | The 38-block kit, built per flavour (industrial, gothic, opera) inside each grid footprint; procedural hull greebles |
| `ld_ships.py` | Ship class (cells at 0.5 m, lofts, convex pieces, spires, faceted spheres and rings, modules, liveries, carving), zoned greebles, two ship designs per direction |
| `ld_aliens.py` | Alien hulls: signed-distance cells meshed by surface nets, alien parts (spines, sacs, eyes, tendrils), HUSK and BROODMOTHER |
| `ld_stations.py` | One station per direction |
| `ld_mats.py`, `ld_world.py` | Materials (panel mosaic, streaks, paint bands, heat tint, alien carapace); skies, lights, planets, limb-scattering atmospheres, lit and shadowed rings, haze, moons, asteroids |
| `ld_scenes.py` | Shots: hero, flight and combat at the game camera (55° pitch, 40° FOV), station, planet, Blueprint Lab, ship close-ups, alien close-up and colourways, kit icons, hull-variant slab |

Logs go to `Logs/tools/lookdev-<direction>-<stage>.log`.
