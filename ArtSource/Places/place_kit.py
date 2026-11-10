"""The place design kit: a place (station, den, mission site) as a 3D voxel layout, for owner review.

DEV-023's hub_layouts.py drew places as flat maps on the flight plane. With full 3-axis flight (ADR-0123) places have
height: shafts, decks above and below, rooms you fly into. A design here is a list of solid and carved shapes, run in
order (later shapes win), plus what makes it a place: points of interest, flight routes, lamps, signs, moored ships and
notes. places.py voxelises it, renders it low-res in Blender (overview, top-down cutaways, sections, views from inside
at ship scale) and draws the sheets.

Pure python (no numpy, no Pillow), so a design module runs in any interpreter. Coordinates: x east, y north, z up,
metres (the game's (x, y, z) is (x, z, y) here). Everything solid is voxel, so all of it can be cut and destroyed.
"""
import math

# Materials (ids are the voxel values; 0 is empty space). Colours follow DESIGN-GUIDE §3 / the Hollow's rock layer.
MATS = {
    "rock": (1, "#4A4640"), "rock_pale": (2, "#6E675C"), "ochre": (3, "#7A5E3A"), "rust": (4, "#6A3A26"),
    "basalt": (5, "#2C2B29"), "ice": (6, "#9AA8B0"), "hull": (7, "#7E7F78"), "hull2": (8, "#8A7650"),
    "armor": (9, "#4A4C46"), "trim": (10, "#2E302A"), "plate_dark": (11, "#3A3C38"), "obsidian": (12, "#1A2230"),
    "pearl": (13, "#C9D0D8"), "jade": (14, "#24604A"), "gold": (15, "#9A7A3A"), "glass": (16, "#1E2A2E"),
    "crate": (17, "#5E6448"), "crate2": (18, "#7A2E24"), "deck": (19, "#55574F"), "bone": (20, "#B8AE96"),
}
MAT_ID = {k: v[0] for k, v in MATS.items()}

# District light colours (the Hollow's convention: each place has its colour, guides lead to it in that colour)
DISTRICT = {"hearth": "#2EC4B6", "berths": "#FFB347", "breaker": "#FF6A2A", "trade": "#FF2E88", "foundry": "#FF8A2A",
            "gates": "#A8CCFF", "arrival": "#FFF1D6", "danger": "#FF463C", "vault": "#9FD8FF", "lime": "#9DFF3C",
            "violet": "#B89CFF", "white": "#DCEBFF", "amber": "#FFAE4A"}


class Place:
    def __init__(self, key, title, subtitle, bounds, cell=8.0):
        self.key, self.title, self.subtitle = key, title, subtitle
        self.bounds = bounds                    # ((x0, y0, z0), (x1, y1, z1))
        self.cell = cell
        self.ops = []                           # voxel ops in order
        self.pois, self.routes, self.lamps, self.signs, self.ships, self.holos = [], [], [], [], [], []
        self.gates, self.notes, self.views, self.levels, self.sections = [], {}, [], [], []
        self.strips, self.markers = [], []
        self.window_density, self.planets = 1.0, []

    # ---------------------------------------------------------------------------------------------- voxel shapes
    def _op(self, kind, mat, **kw):
        self.ops.append(dict(kind=kind, mat=0 if mat is None else MAT_ID[mat], **kw))

    def ellipsoid(self, c, r, mat, noise=0.0, scale=60.0, seed=0, strata=False):
        """A rock or hull ellipsoid; noise roughens its surface (fraction of radius); strata bands rock colours."""
        self._op("ellipsoid", mat, c=list(c), r=list(r), noise=noise, scale=scale, seed=seed, strata=strata)

    def box(self, c, size, mat, yaw=0.0):
        self._op("box", mat, c=list(c), size=list(size), yaw=yaw)

    def cylinder(self, a, b, r, mat, r2=None):
        """A cylinder (or cone, with r2) from point a to point b."""
        self._op("cylinder", mat, a=list(a), b=list(b), r=r, r2=r if r2 is None else r2)

    def tube(self, pts, r, mat, rz=None):
        """A capsule chain along a polyline (rz: vertical half-height, for flattened tunnels)."""
        self._op("tube", mat, pts=[list(p) for p in pts], r=r, rz=rz if rz else r)

    def torus(self, c, R, r, mat, axis="z", arc=None):
        self._op("torus", mat, c=list(c), R=R, r=r, axis=axis, arc=arc)

    def carve_ellipsoid(self, c, r, noise=0.0, scale=40.0, seed=0, floor=None):
        """A cavern. floor: flatten its bottom at this height (a deck to moor on)."""
        self._op("ellipsoid", None, c=list(c), r=list(r), noise=noise, scale=scale, seed=seed, floor=floor)

    def carve_box(self, c, size, yaw=0.0):
        self._op("box", None, c=list(c), size=list(size), yaw=yaw)

    def carve_cylinder(self, a, b, r, r2=None):
        self._op("cylinder", None, a=list(a), b=list(b), r=r, r2=r if r2 is None else r2)

    def carve_tube(self, pts, r, rz=None, noise=0.0, seed=0):
        self._op("tube", None, pts=[list(p) for p in pts], r=r, rz=rz if rz else r, noise=noise, seed=seed)

    def loft(self, sections, mat, axis="y", shell=None, carve=False):
        """A hull lofted through cross-sections along an axis. Each section is (s, u, v, w, h, n): position along the
        axis, centre offset in the other two axes (for y: x and z), half-width, half-height, and the shape exponent n
        (1 diamond, 2 ellipse, 4+ a rounded box). shell: leave only a skin this thick (the inside becomes space)."""
        self._op("loft", None if carve else mat, sections=[list(x) for x in sections], axis=axis, shell=shell)

    def prism(self, poly, a0, a1, mat, axis="z", carve=False):
        """A polygon (list of 2D points in the other two axes) extruded from a0 to a1 along the axis."""
        self._op("prism", None if carve else mat, poly=[list(p) for p in poly], a0=a0, a1=a1, axis=axis)

    def room(self, c, size, wall="plate_dark", floor="deck", t=8.0, yaw=0.0):
        """An interior space with lined walls and a deck floor (the lining shows inside; outside stays hull)."""
        self.box(c, [size[0] + 2 * t, size[1] + 2 * t, size[2] + 2 * t], wall, yaw)
        self.carve_box(c, size, yaw)
        if floor:
            self.box([c[0], c[1], c[2] - size[2] / 2 - t / 2], [size[0], size[1], t], floor, yaw)

    def shell(self, c, size, t, mat, yaw=0.0):
        """A hollow box: walls t thick (a hull section you fly inside)."""
        self.box(c, size, mat, yaw)
        self.carve_box(c, [size[0] - 2 * t, size[1] - 2 * t, size[2] - 2 * t], yaw)

    # ---------------------------------------------------------------------------------------------- what makes a place
    def poi(self, n, name, p, district, text, view=None, inside=True):
        """A numbered point of interest. view: (eye, target) for its inside shot; None for an automatic one."""
        self.pois.append(dict(n=n, name=name, p=list(p), district=district, text=text,
                              view=[list(view[0]), list(view[1])] if view else None, inside=inside))

    def route(self, name, pts, colour, kind="lane", note="", frames=6):
        """A route (3D polyline): lanes, patrols, escape paths. kind "flight": a flight line the tool analyses (clearance to
        the voxels along it, coloured on the plans) and flies (a strip of chase-camera frames along it)."""
        self.routes.append(dict(name=name, pts=[list(p) for p in pts], colour=colour, kind=kind, note=note, frames=frames))

    def strip(self, pts, colour, below=None):
        """A guide light strip (the Hollow's wall-foot strips) along a 3D polyline."""
        self.strips.append(dict(pts=[list(p) for p in pts], colour=colour, below=below))

    def lamp(self, p, colour, power=1.0):
        self.lamps.append(dict(p=list(p), colour=colour, power=power))

    def sign(self, p, text, colour, yaw=0.0, size=4.0, up="z"):
        """Dot-matrix holo lettering (the Hollow's signs). yaw: the direction the text faces (deg, 0 = toward -y)."""
        self.signs.append(dict(p=list(p), text=text.upper(), colour=colour, yaw=yaw, size=size))

    def holo(self, p, colour, r=10.0, kind="ring"):
        """A projector ring with its turning emblem over it (the Hollow's station markers)."""
        self.holos.append(dict(p=list(p), colour=colour, r=r, kind=kind))

    def ship(self, p, yaw, cls="light", pitch=0.0, livery=None, lit=True):
        """A moored or flying ship (look-book NOCTURNE designs; cls light (23 m) or heavy)."""
        self.ships.append(dict(p=list(p), yaw=yaw, cls=cls, pitch=pitch, livery=livery, lit=lit))

    def gate(self, c, R, facing=0.0, tilt=90.0, lit=True, label=""):
        self.gates.append(dict(c=list(c), R=R, facing=facing, tilt=tilt, lit=lit, label=label))

    def marker(self, p, r, colour, label=""):
        """A translucent sphere: sensor reach, alarm zone, interdiction radius."""
        self.markers.append(dict(p=list(p), r=r, colour=colour, label=label))

    def planet(self, p, r, kind="giant"):
        """A backdrop planet (look-dev planet shader), for the spectacle of the approach."""
        self.planets.append(dict(p=list(p), r=r, kind=kind))

    def level(self, z, name):
        """A plan to draw: the place cut away above z."""
        self.levels.append(dict(z=z, name=name))

    def section(self, axis, at, name):
        """A section to draw: cut along x or y at 'at', looking from the cut side."""
        self.sections.append(dict(axis=axis, at=at, name=name))

    def view(self, name, eye, target, fov=50.0, hero=None, cut=None):
        """An extra shot. hero: (position, yaw, pitch) of the player's ship in frame. cut: hide voxels above this z."""
        self.views.append(dict(name=name, eye=list(eye), target=list(target), fov=fov,
                               hero=[list(hero[0]), hero[1], hero[2]] if hero else None, cut=cut))

    def spec(self):
        return dict(key=self.key, title=self.title, subtitle=self.subtitle, bounds=self.bounds, cell=self.cell,
                    ops=self.ops, pois=self.pois, routes=self.routes, lamps=self.lamps, signs=self.signs,
                    ships=self.ships, holos=self.holos, gates=self.gates, notes=self.notes, views=self.views,
                    levels=self.levels, sections=self.sections, strips=self.strips, markers=self.markers,
                    window_density=self.window_density, planets=self.planets,
                    mats={k: v for k, v in MATS.items()})


# ---------------------------------------------------------------------------------------------- helpers for designs
def polar(deg, r, c=(0.0, 0.0), z=0.0):
    a = math.radians(deg)
    return (c[0] + r * math.sin(a), c[1] + r * math.cos(a), z)


def lerp3(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def arc_pts(c, r, d0, d1, z, n=16):
    return [polar(d0 + (d1 - d0) * i / n, r, c, z) for i in range(n + 1)]


def helix(c, r, z0, z1, turns, d0=0.0, n=48):
    return [polar(d0 + 360.0 * turns * i / n, r, c, z0 + (z1 - z0) * i / n) for i in range(n + 1)]
