# Nocturne flight environment

Own-work procedural content from `ArtSource/LookDev/ld_world.py` and `ld_styles.py`, following DESIGN-GUIDE §4. No third-party imagery or AI assets.

Rebuild the planet surface using `ArtSource/LookDev/export_nocturne_world.py` with Blender 5.2 (command in its header). The surface is 4096×2048, AgX High Contrast, exposure −0.02, matching the approved look book.

- `M_NocturneDistant` / `NocturneSky.shader`: fully procedural sky, with fixed hashed stars in three tiers and slowly evolving green/violet/amber smoke, filaments and dark dust lanes. No sky texture; animation is shader time, and sky direction is independent of camera translation.
- `T_VesperSurface`: existing storm-world procedural surface, fixed stellar illumination and permanent lightning. UV seam duplicated explicitly during baking.
- `VesperSphere`: radius-one latitude/longitude mesh (128×64); in World it is a physical sphere of radius 1400 m, centred at (1400, −2000, 1700). The flight plane passes 600 m above its highest point. This is a presentation placeholder without collision, landing or save state.
- `M_VesperAtmosphere`: separate 3% larger sphere, analytical view-dependent pale-sand limb; body depth occludes its far side.
- `M_SpatialDebris`: stationary world-space cosmetic particles. Boundary copies recycle with fading, never accumulating a drift velocity.

World owns all geometry. The camera-specific sky override is restored on exit; the persistent camera is not duplicated. SYS-011 will replace this staged planet with canonical celestial streaming.
