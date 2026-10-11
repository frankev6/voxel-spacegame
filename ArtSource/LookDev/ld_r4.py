"""Round 4 renders: ships built from approved batch-3 silhouettes (ld_ark), each shown the same way: a hero close-up,
the game camera, the stern, a straight-down plan view to hold against the silhouette, and a detail shot.

Run through lookdev.py:  python3 ArtSource/LookDev/lookdev.py --stage r4 [--direction drydock] [--only halo] [--draft]
Writes docs/design/lookbook/img/r4/<direction>/<ship>_<shot>.jpg and prints the build check for each ship."""

import json
import os

import bpy
from mathutils import Vector

import ld_check
import ld_world
from ld_ark import ARK_SHIPS
from ld_scenes import Things, build_ship, make_camera, aim, orbit, render, frame_objects

R4_SHIPS = ARK_SHIPS


def check_ships(only=None):
    rows = []
    for key, fn in R4_SHIPS:
        if only and key not in only.split(","):
            continue
        sh = fn()
        ok, lines = ld_check.report([sh])
        for l in lines:
            print("CHECK " + l)
        r = ld_check.check(fn())
        rows.append(dict(name=r["name"], cells=r["cells"], big=r["big"], u=r["u"], mods=r["mods"], shapes=r["shape_types"],
                         ok=not r["problems"]))
    for r in rows:
        print("CHECKJSON " + json.dumps(r))


def stage_r4(S, scene, mats, out, only=None, draft=False):
    ld_world.setup_lights(S, scene)
    sd = ld_world.sun_dir(S)
    check_ships(only)
    things = Things()
    P = S["planet"]
    ld_world.planet(S, "planet", 3200.0, (0, 0, 0), P, sd)
    proot = bpy.data.objects.new("planet_root", None)
    scene.collection.objects.link(proot)
    for o in [o for o in scene.objects if o.name.startswith("planet") and o is not proot]:
        o.parent = proot
    things.add("planet", proot)
    mo = ld_world.moon(S, "moon", 380.0, (0, 0, 0))
    mroot = bpy.data.objects.new("moon_root", None)
    scene.collection.objects.link(mroot)
    mo.parent = mroot
    things.add("moon", mroot)
    cam = make_camera(scene)
    for key, fn in R4_SHIPS:
        if only and key not in only.split(","):
            continue
        build_ship(fn(), S, mats, things, key)
        _, size = build_ship(fn(), S, mats, things, key + "_still", plumes=False)
        L = Vector(size).length
        # hero: three-quarter from the bow, planet and moon behind
        things.show({key + "_still", "planet", "moon"})
        things.place("planet", (2600, -8200, -2600))
        things.place("moon", (-1500, -7000, 900))
        aim(cam, orbit((0, 0, 0), L * 1.2, 38, 22), (0, 0, -1.0), lens=50)
        render(scene, os.path.join(out, f"{key}_hero.jpg"))
        # the game camera: 55 degree pitch, 40 degree vertical FOV, engines lit
        things.show({key, "planet"})
        things.place("planet", (-3600, 9800, -3800))
        aim(cam, orbit((0, 0, 0), L * 1.05, -20, 55), (0, 0, 0), fov_v=40)
        render(scene, os.path.join(out, f"{key}_game.jpg"))
        # the stern: engines, the radiator wing and the magazine
        things.show({key, "moon"})
        things.place("moon", (2400, 6800, -500))
        aim(cam, orbit((0, 0, 0), L * 1.15, -145, 24), (0, 0, -1.0), lens=50)
        render(scene, os.path.join(out, f"{key}_rear.jpg"))
        # plan view, bow to the right (as on the silhouette page), to hold against the approved silhouette
        things.show({key + "_still"})
        frame_objects(cam, things.objects(key + "_still"), 90, 89.9, 1.06)
        render(scene, os.path.join(out, f"{key}_plan.jpg"))
        # detail: low and close along the port side
        things.show({key + "_still", "planet"})
        things.place("planet", (-6000, 3000, -2400))
        aim(cam, orbit((0, 8, 0), L * 0.5, 120, 12), (0, 10, 2.0), lens=40)
        render(scene, os.path.join(out, f"{key}_detail.jpg"))
        things.show(set())
