# LookDev: 3D look-development renders

Procedural Blender 5.2 scenes for the six art directions in `docs/design/lookbook/`. Everything is generated from code: no imported art, no AI images, fonts are SIL OFL.

```bash
python3 ArtSource/LookDev/lookdev.py                                   # all directions, kit icons + scenes (~4 min)
python3 ArtSource/LookDev/lookdev.py --direction reef --stage scenes --draft --only combat,hero
```

| File | What it holds |
|---|---|
| `ld_styles.py` | The six directions: palette, materials, lighting rig (azimuth/elevation relative to the camera), sky, planet, greeble densities, UI fonts |
| `ld_geom.py` | Primitives, the voxel mesher (exposed faces, wedge slopes on edge runs, chamfered corners) |
| `ld_blocks.py` | The block kit: every block built per direction "shape flavour" inside its grid footprint; procedural hull greebles |
| `ld_ships.py` | Ship class (cells at 0.5 m, mirrored, modules, carving) and two ship designs per direction |
| `ld_stations.py` | One station per direction |
| `ld_mats.py`, `ld_world.py` | Materials; sky, lights, planets (unlit sun shading with city lights, clouds, atmosphere, rings), moons, asteroids |
| `ld_scenes.py` | Shots: hero, flight and combat at the game camera (55° pitch, 40° FOV), station, planet, Blueprint Lab, ship close-ups, kit icons, hull-variant slab |

Logs go to `Logs/tools/lookdev-<direction>-<stage>.log`.
