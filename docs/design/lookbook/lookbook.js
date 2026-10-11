/* Look Book. Round 3: DRYDOCK, NOCTURNE and OPERA with connected ships, cruisers and dreadnoughts, capital weapons,
   defended stations and five alien types. Round 4 (ARK tab): ships built in 3D from approved batch-3 silhouettes. Rounds 1 and 2 stay available as an archive. Renders come from
   ArtSource/LookDev/lookdev.py (Blender); lineups.js and buildcheck.js are written by the same tool.
   Game screens are HTML at 1920x1080 composited over the renders and scaled to fit. */
"use strict";

/* ---------------------------------------------------------------- round 3 data */

const R3 = [
  {
    key: "drydock", base: "img/r3/drydock", prev: "img/r2/drydock", name: "DRYDOCK", line: "Industrial used-future",
    pitch: "Working ships in a working universe. Bone plating in faction liveries, safety yellow, colour-coded machinery you can read at a glance, container decks, hazard paint and work lights, from 18 m salvage gunships up to a 250 m dreadnought, around a busy trade port under a ringed rust world.",
    changed: [
      "Every ship passes a build check: no floating blocks, every turret sits on its base (guns under the hull hang upside down from it), sponsons and wings are joined through real faces",
      "Two new classes with a clear scale ladder: STEVEDORE heavy cruiser and ANVIL dreadnought (line-up below, 18 m to 250 m)",
      "Large hull blocks: cruisers are built from 1 m blocks (2×2×2 cells), dreadnoughts from 1.5 m blocks (3×3×3). Bigger plating that reads at scale; windows, hatches and small guns stay human-sized",
      "Capital weapons for the big ships: spinal particle lance, singularity projector, storm coil, graviton snare, nova silo, drone carrier bay, triple battery",
      "Station rethought from scratch: PORT KESSLER, a trade hub with four docking piers, gantries, container yards, guide lights, ships berthed and traffic coming and going",
      "Stations are defended, with the same blocks they are built from: heavy cannons and flak on the hub ledge, flak on every berth head, autocannons along the piers, a graviton snare on top",
      "Three new aliens in this world, including GRAVID, the behemoth carrier (ALIENS tab)",
    ],
    ships: ["MULE", "salvage gunship"], heavy: ["BULWARK", "container frigate"], cruiser: ["STEVEDORE", "heavy cruiser"], dread: ["ANVIL", "dreadnought"],
    station: ["PORT KESSLER", "trade hub with four docking piers"],
    planet: "OSTRAVA IV", ptype: "Ringed rust world · 0.8 g · dust storms · mining towns", sys: "KESSLER BELT",
    speaker: "Dockmaster Oyelaran", where: "Port Kessler, pier 3",
    enemy: ["BROODMOTHER", "alien leviathan"], enemySys: ["CARAPACE BREACHED", "SAC CLUSTER EXPOSED", "SPINES ARMED"],
    obj: ["Break the BROODMOTHER's carapace", "2 / 3 breaches open"], nora: "The sacs along its flank are feeding the shell. Burst them and the carapace goes soft.",
    fleet: "The fleet: ANVIL, STEVEDORE, a BULWARK and two MULEs", superweapon: "ANVIL's spinal lance and singularity projector against GRAVID",
  },
  {
    key: "nocturne", base: "img/r3/nocturne", prev: "img/r2/nocturne", name: "NOCTURNE", line: "Industrial gothic horror",
    pitch: "The ALIEN-franchise mood with a cathedral's bones: hunched, ribbed ships in worn grey and ochre with oxblood and mustard machinery, slit windows of sodium light, spires and flying buttresses on the capital ships, a refinery cathedral under tow, a storm world below, and something black and living out in the dark.",
    changed: [
      "Every ship passes a build check: the tail fins and stern spires now stand on flat hull, wing guns and the chin gun hang from their bases, the CANTICLE's railguns are side-mounted on the hull wall",
      "Two new classes: REQUIEM heavy cruiser (a lightning tower and organ-pipe silos at the stern) and SEPULCHRE dreadnought (a flying cathedral-fortress: spires, flying buttresses, keel spikes, an organ of nova silos)",
      "More range, not more saturation: deeper blacks, brighter highlights, stronger windows, engines and strobes",
      "The sky is no longer all green: a violet nebula on one side and a low amber glow on another, over the green smoke",
      "The storm planet has colour variety: rust, bruise-violet and teal bands, swirls, a dark polar hood and one great pale storm",
      "THE ROOD is defended: heavy cannons between the spire rows, flak at the deck corners, turrets hanging under the deck",
      "UI unchanged",
    ],
    ships: ["WRAITH", "dropship"], heavy: ["CANTICLE", "refinery tug"], cruiser: ["REQUIEM", "heavy cruiser"], dread: ["SEPULCHRE", "dreadnought"],
    station: ["THE ROOD", "refinery cathedral under tow, now defended"],
    planet: "VESPER-9", ptype: "Storm world · permanent lightning · no surface signals", sys: "THE HOLLOW REACH",
    speaker: "Warden Ilse Marrow", where: "The Rood, lower nave",
    enemy: ["BROODMOTHER", "alien leviathan"], enemySys: ["CARAPACE BREACHED", "SAC CLUSTER EXPOSED", "SPINES ARMED"],
    obj: ["Break the BROODMOTHER's carapace", "2 / 3 breaches open"], nora: "The sacs along its flank are feeding the shell. Burst them and the carapace goes soft.",
    fleet: "The fleet: SEPULCHRE, REQUIEM, a CANTICLE and two WRAITHs", superweapon: "SEPULCHRE's singularity projector and triple batteries against GRAVID",
  },
  {
    key: "opera", base: "img/r3/opera", prev: "img/r2/opera", name: "OPERA", line: "Space opera",
    pitch: "Paperback space opera on a grand scale. Cream hulls in bold liveries with gold trim, a 2001-style capital ship and double-wheel station, Dune-like monumental masses, Fifth Element warmth, and No Man's Sky skies with painted nebulae and ringed giants.",
    changed: [
      "Build-check fixes: the LARK's wing roots now run into the fuselage and its cockpit and sail fins are seated; the SOVEREIGN's mast and shield sit on the sphere",
      "THE CROWN fixed: the spokes now reach the hub (the wheels were floating around it), and the 45° seams of both wheels are closed",
      "THE CROWN is defended: flak on the upper rim and under the lower rim, heavy cannons on all eight spokes",
      "The capital kit (large hull blocks, capital weapons) in OPERA's art-deco style. No cruiser or dreadnought designs for OPERA this round",
    ],
    ships: ["LARK", "swept-wing courier"], heavy: ["SOVEREIGN", "monumental capital ship"], station: ["THE CROWN", "double wheel station, now defended"],
    planet: "AURELIA", ptype: "Banded gas giant · ring system · 2 moons", sys: "THE GILDED REACH",
    speaker: "Steward Cassius Vey", where: "The Crown, upper wheel",
    enemy: ["SOVEREIGN", "rival capital ship (Solar livery)"], enemySys: ["SPINE BREACHED", "RING OFFLINE", "MASS DRIVER CHARGING"],
    obj: ["Silence the SOVEREIGN's spinal gun", "2 / 3 capacitor banks down"], nora: "Their ring is offline. Hit the spine behind it before the mass driver finishes charging.",
  },
];
const R2 = R3.map(d => ({ key: d.key, name: d.name, line: d.line, base: d.prev }));

/* round 1 archive (six directions, as delivered on 2026-10-10) */
const R1 = [
  ["drydock", "DRYDOCK", "Industrial used-future"], ["nocturne", "NOCTURNE", "Pearl ceramic & light"],
  ["pulp-horizon", "PULP HORIZON", "Paperback-cover sci-fi"], ["orbital", "ORBITAL", "NASA-punk hard SF"],
  ["reef", "REEF", "Grown biotech"], ["facet", "FACET", "Low-poly bold"],
].map(([key, name, line]) => ({ key, name, line, base: `img/${key}` }));

/* capital weapons: what they do and what they cost (look-dev numbers, not balance) */
const CAPITAL = [
  ["battery", "Triple battery", "12×18×8 (dreadnought mount 18×27×12)", "Three long barrels in an armoured house. Slow to turn; a salvo lands like a building falling.", "3 × 900 dmg · 4 km · 120 MJ per salvo"],
  ["lance", "Spinal particle lance", "12×80×12 (ANVIL carries 12×180×12)", "Built into the keel, so the whole ship aims it. Charges for six seconds, then a beam that cuts a hull from end to end.", "1.2 GJ per shot · 6 s charge"],
  ["singularity", "Singularity projector", "16×24×16 (dreadnought mount 24×36×24)", "Holds a pinhead black hole in a gimbal and throws it. It drifts, swallows every cell it touches, then evaporates in a flash.", "full reactor output for 20 s · lives 8 s"],
  ["storm", "Storm coil", "10×10×20", "A lightning tower. The discharge chains through up to six hulls, jumping to the nearest metal blocks.", "300 MJ per discharge · 6 chain jumps"],
  ["graviton", "Graviton snare", "14×12×12", "Pulls a ship in or pins it in place: salvage, boarding, or holding a target still for the big guns.", "40 MW while held"],
  ["nova", "Nova silo", "6×6×14", "One slow, heavy warhead. The blast wave strips armour from everything in a 60 m sphere.", "1 warhead · 90 s reload"],
  ["hangar", "Drone carrier bay", "16×24×6", "An open deck with launch rails and six drones that harass, intercept or patch hull.", "6 drones · 20 s relaunch"],
];

const KIT = {
  "Structure": ["hull_cube", "hull_wedge", "hull_corner", "hull_inner", "hull_slope2", "hull_round", "hull_armor", "girder"],
  "Large hull (cruisers and dreadnoughts)": ["hull_2", "hull_3", "wedge_3", "corner_3", "slope_3x6", "armor_3", "truss_3"],
  "Procedural hull variants": ["hull_window", "hull_hatch", "hull_vent", "hull_pipes"],
  "Weapons": ["autocannon", "gatling", "railgun", "beam", "harpoon", "tesla", "missile", "flak", "plasma", "torpedo", "heavy_cannon", "mass_driver"],
  "Capital weapons": ["battery", "lance", "singularity", "storm", "graviton", "nova", "hangar"],
  "Modules": ["cockpit", "thruster_s", "thruster_l", "thruster_xl", "reactor", "shield", "capacitor", "cargo", "radiator", "sensor", "solar"],
  "Cores": ["core_1", "core_2", "core_3", "core_35"],
};
const NEW = new Set(["hull_2", "hull_3", "wedge_3", "corner_3", "slope_3x6", "armor_3", "truss_3", "battery", "lance", "singularity", "storm", "graviton", "nova", "hangar", "thruster_xl"]);
const LABEL = {
  hull_cube: "Hull", hull_wedge: "Hull slope", hull_corner: "Hull corner", hull_inner: "Inner corner", hull_slope2: "Long slope 1×2",
  hull_round: "Rounded edge", hull_window: "Hull · windows", hull_hatch: "Hull · hatch & latches", hull_vent: "Hull · vents",
  hull_pipes: "Hull · pipes", hull_armor: "Heavy armour", girder: "Girder frame", cockpit: "Cockpit", autocannon: "Autocannon",
  gatling: "Rotary cannon", railgun: "Railgun", heavy_cannon: "Heavy twin cannon", mass_driver: "Spinal mass driver", beam: "Laser lance",
  tesla: "Arc projector", plasma: "Plasma mortar", torpedo: "Torpedo launcher", missile: "Missile cells", flak: "Flak turret",
  harpoon: "Salvage harpoon", thruster_s: "Thruster", thruster_l: "Main engine", thruster_xl: "Capital engine", reactor: "Reactor", shield: "Shield projector",
  capacitor: "Capacitor bank", cargo: "Cargo pod", radiator: "Radiator", sensor: "Sensor mast", solar: "Solar wing",
  core_1: "Core", core_2: "Core", core_3: "Core", core_35: "Core",
  hull_2: "Hull 2×2×2", hull_3: "Hull 3×3×3", wedge_3: "Slope 3×3×3", corner_3: "Corner 3×3×3", slope_3x6: "Long slope 3×6×3",
  armor_3: "Armour block 3×3×3", truss_3: "Truss 3×6×3", battery: "Triple battery", lance: "Spinal particle lance",
  singularity: "Singularity projector", storm: "Storm coil", graviton: "Graviton snare", nova: "Nova silo", hangar: "Drone carrier bay",
};
const SIZE = {
  hull_cube: "1×1×1", hull_wedge: "1×1×1", hull_corner: "1×1×1", hull_inner: "1×1×1", hull_slope2: "1×2×1", hull_round: "1×1×1",
  hull_window: "1×1×1", hull_hatch: "1×1×1", hull_vent: "1×1×1", hull_pipes: "1×1×1", hull_armor: "1×1×1", girder: "1×1×1",
  cockpit: "4×4×3", autocannon: "2×2×2", gatling: "2×3×2", railgun: "2×6×2", heavy_cannon: "4×6×3", mass_driver: "3×12×3",
  beam: "2×4×2", tesla: "2×2×3", plasma: "3×3×3", torpedo: "3×6×2", missile: "3×3×1", flak: "3×3×3", harpoon: "2×4×2",
  thruster_s: "2×3×2", thruster_l: "4×6×4", thruster_xl: "8×12×8", reactor: "3×3×3", shield: "2×2×2", capacitor: "2×2×2", cargo: "2×4×2",
  radiator: "1×6×4", sensor: "2×2×3", solar: "1×8×4", core_1: "1×1×1", core_2: "2×2×2", core_3: "3×3×3", core_35: "3×5×3",
  hull_2: "2×2×2 · 1 m", hull_3: "3×3×3 · 1.5 m", wedge_3: "3×3×3", corner_3: "3×3×3", slope_3x6: "3×6×3", armor_3: "3×3×3", truss_3: "3×6×3",
  battery: "12×18×8", lance: "12×80×12", singularity: "16×24×16", storm: "10×10×20", graviton: "14×12×12", nova: "6×6×14", hangar: "16×24×6",
};
const WPN = ["autocannon", "gatling", "railgun", "heavy_cannon", "mass_driver", "beam", "tesla", "plasma", "torpedo", "missile", "flak", "harpoon",
  "battery", "lance", "singularity", "storm", "graviton", "nova", "hangar"];
const FUNC = (k) => WPN.includes(k) ? "f-wpn"
  : ["reactor", "capacitor", "shield"].includes(k) ? "f-pwr" : ["thruster_s", "thruster_l", "thruster_xl", "radiator"].includes(k) ? "f-eng"
  : k === "cargo" ? "f-cargo" : k.startsWith("core") ? "f-core" : "f-hull";
const COLOURWAYS = [["bone", "Bone & jade"], ["obsidian", "Obsidian & acid"], ["coral", "Blood coral"], ["slick", "Oil slick"]];

const $ = (s, r = document) => r.querySelector(s);
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const img = (d, n) => `${d.base}/${n}`;
const icon = (d, k) => `${d.base}/icons/${k}.webp`;

/* ---------------------------------------------------------------- game screens (1920x1080 stages) */

function stage(d, bg, inner, cls) {
  const wrap = el("div", "stage-wrap");
  const st = el("div", `stage ui-${d.key} ${cls || ""}`);
  st.style.backgroundImage = `url(${img(d, bg)})`;
  st.innerHTML = inner;
  wrap.appendChild(st);
  wrap.dataset.zoom = "stage";
  return wrap;
}

function bar(label, pct, kind, val) {
  const segs = Array.from({ length: 10 }, (_, i) => `<i class="${i < Math.round(pct / 10) ? "on" : ""}"></i>`).join("");
  return `<div class="bar ${kind}"><span class="bl">${label}</span><span class="bt"><b style="width:${pct}%"></b><span class="segs">${segs}</span></span><span class="bv">${val}</span></div>`;
}

function avatar() {
  return `<div class="nora"><svg viewBox="0 0 96 96"><circle cx="48" cy="48" r="30" class="n-ring"/>` +
    Array.from({ length: 24 }, (_, i) => { const a = i / 24 * Math.PI * 2; return `<circle cx="${48 + Math.cos(a) * 38}" cy="${48 + Math.sin(a) * 38}" r="${2 + (i % 3)}" class="n-dot"/>`; }).join("") +
    `<path d="M18 48 Q28 30 36 48 T54 48 T72 48 T80 48" class="n-wave"/></svg></div>`;
}

function hud(d) {
  const w = (k, n, key, cd, ammo) => `<div class="wpn ${FUNC(k)} ${cd ? "cool" : ""}"><span class="wk">${key}</span><img src="${icon(d, k)}" alt=""><span class="wn">${n}</span><span class="wa">${ammo}</span>${cd ? `<span class="wc" style="width:${cd}%"></span>` : ""}</div>`;
  const [en, ed] = d.enemy;
  return stage(d, "combat.jpg", `
    <div class="z tl panel"><div class="ph">${d.sys}</div><div class="loc">Hostile contact · ${en}</div>
      <div class="obj"><span class="ok">◆</span> ${d.obj[0]}<br><span class="sub">${d.obj[1]}</span></div></div>
    <div class="z tr radar"><div class="rr"></div><div class="rr r2"></div><div class="rr r3"></div><i class="me"></i><i class="bp e" style="left:64%;top:30%"></i><i class="bp e" style="left:58%;top:24%"></i><i class="bp e" style="left:72%;top:46%"></i><i class="bp f" style="left:44%;top:56%"></i><i class="bp n" style="left:26%;top:70%"></i><span class="rl">RADAR · 2 km</span></div>
    <div class="target"><span class="c c1"></span><span class="c c2"></span><span class="c c3"></span><span class="c c4"></span>
      <div class="tcard panel"><div class="ph">${en}</div><div class="tsub">${ed} · 31 m away</div>
      ${bar("HULL", 46, "hull", "46%")}${bar("SHLD", 0, "shield", "DOWN")}
      <div class="sys"><span class="bad">${d.enemySys[0]}</span><span>${d.enemySys[1]}</span><span>${d.enemySys[2]}</span></div></div></div>
    <div class="callout c-a">+ BREACH · 41 CELLS</div><div class="callout c-b">WEAK POINT EXPOSED</div>
    <div class="z bl panel status"><div class="ph">${d.ships[0]}</div>${bar("HULL", 82, "hull", "1 640")}${bar("SHLD", 38, "shield", "114")}${bar("ENRG", 64, "energy", "640")}
      <div class="spd">SPEED <b>142</b> m/s · BOOST <b>READY</b></div></div>
    <div class="z bc wpns">${w("beam", "LASER LANCE", "1", 0, "∞")}${w("gatling", "ROTARY", "2", 0, "1 280")}${w("missile", "MISSILES", "3", 62, "6")}${w("harpoon", "HARPOON", "4", 35, "2")}</div>
    <div class="z br panel comms">${avatar()}<div><div class="ph">NORA</div><p>${d.nora}</p></div></div>
    <div class="reticle"></div>`, "s-hud");
}

function inventory(d) {
  const tiles = ["heavy_cannon", "mass_driver", "plasma", "tesla", "torpedo", "harpoon", "autocannon", "gatling", "beam", "railgun", "missile", "flak",
    "thruster_s", "thruster_l", "reactor", "shield", "capacitor", "cargo", "radiator", "sensor", "core_1", "core_2", "core_3", "core_35"];
  const counts = [1, 0, 2, 1, 3, 2, 4, 2, 2, 1, 3, 1, 6, 2, 1, 2, 4, 3, 2, 1, 1, 1, 1, 0];
  const rar = (k) => k === "core_35" || k === "mass_driver" ? "r4" : k.startsWith("core") || k === "reactor" || k === "heavy_cannon" || k === "plasma" ? "r3" : ["flak", "beam", "shield", "tesla", "torpedo"].includes(k) ? "r2" : "r1";
  const grid = tiles.map((k, i) => `<div class="tile ${FUNC(k)} ${rar(k)} ${k === "heavy_cannon" ? "sel" : ""} ${counts[i] === 0 ? "none" : ""}"><img src="${icon(d, k)}" alt=""><span class="cnt">${counts[i] === 0 ? "—" : "×" + counts[i]}</span></div>`).join("");
  const hot = ["hull_cube", "hull_wedge", "hull_corner", "gatling", "heavy_cannon", "thruster_s", "shield", "reactor", "cargo"].map((k, i) => `<div class="hk ${i === 4 ? "sel" : ""}"><span>${i + 1}</span><img src="${icon(d, k)}" alt=""></div>`).join("");
  return stage(d, "builder.jpg", `
    <div class="topbar panel"><span class="ph">BLUEPRINT LAB</span><span class="tb-ship">${d.ships[0]}</span><span class="tb-btn">↶ UNDO</span><span class="tb-btn">↷ REDO</span><span class="tb-btn">⇋ MIRROR X</span><span class="tb-sp"></span><span class="tb-cells">CELLS 1 284 / 4 000</span><span class="tb-btn pri">COMMIT ⏎</span></div>
    <div class="inv panel"><div class="ph">INVENTORY</div>
      <div class="cats"><span>ALL</span><span>STRUCTURE</span><span class="on">WEAPONS</span><span>MODULES</span><span>CORES</span></div>
      <div class="grid">${grid}</div></div>
    <div class="tip panel"><div class="tip-head"><img src="${icon(d, "heavy_cannon")}" alt=""><div><div class="ph">HEAVY TWIN CANNON</div><div class="rar r3">RARE · 4×6×3 · TURRET</div></div></div>
      <p>Two long barrels in an armoured house. Slow to turn, hits like a freight train. Changes your silhouette from across a system.</p>
      <div class="kv"><span>DAMAGE</span><b>2 × 380</b><span>RANGE</span><b>2.4 km</b><span>ARC</span><b>270°</b><span>MASS</span><b>9.8 t</b></div></div>
    <div class="stats panel"><div class="ph">SHIP STATS</div>
      <div class="st"><span>MASS</span><b>48.0 t</b><em class="up">+9.8</em></div>
      <div class="st"><span>THRUST</span><b>120 kN</b><em></em></div>
      <div class="st"><span>TOP SPEED</span><b>151 m/s</b><em class="dn">−23</em></div>
      <div class="st"><span>POWER</span><b>+92 / −88</b><em class="dn">−17</em></div>
      <div class="st"><span>SHIELD</span><b>300</b><em></em></div>
      <div class="st"><span>FIREPOWER</span><b>1 940</b><em class="up">+760</em></div>
      ${bar("PWR", 96, "energy", "96%")}</div>
    <div class="hotbar">${hot}</div>`, "s-inv");
}

function dialog(d) {
  return stage(d, "station.jpg", `
    <div class="dim"></div>
    <div class="dlg panel"><div class="portrait">${avatar()}</div>
      <div class="dbody"><div class="ph">${d.speaker}</div><div class="dsub">${d.where} · COMMS</div>
      <p>You're the one flying the ${d.ships[0]}? Your hull's got scoring I haven't seen since the old expedition came through. Whatever hit you wasn't pirates.</p>
      <ol class="choices"><li><span class="k">1</span>What do you know about the old expedition?</li>
      <li class="clue"><span class="k">2</span><span class="tag">CLUE</span> Show her the signal fragment from the derelict.</li>
      <li><span class="k">3</span>I need repairs and blocks. What's for sale?</li>
      <li class="dim2"><span class="k">4</span>[Leave] Not your business.</li></ol></div></div>`, "s-dlg");
}

function discovery(d) {
  return stage(d, "planet.jpg", `
    <div class="disc">
      <div class="dkick">NEW SYSTEM CHARTED</div>
      <div class="dname">${d.planet}</div>
      <div class="dtype">${d.ptype}</div>
      <div class="drule"></div>
      <div class="drow"><span>SYSTEM</span><b>${d.sys}</b><span>LANES</span><b>2 known · 1 sealed</b><span>SIGNALS</span><b>3</b></div>
    </div>
    <div class="clue panel"><div class="ph">CLUE FOUND · SIGNAL FRAGMENT 2 / 5</div>
      <p>A repeating burst from the planet's night side, timed to the old expedition's beacon. It points past the sealed lane.</p>
      <div class="reveal">◆ LANE REVEALED → THE QUIET GATE</div></div>
    <div class="rewards panel"><div class="ph">SALVAGE</div><div class="rw">
      <div><img src="${icon(d, "core_2")}" alt=""><span>Core 2×2×2</span></div>
      <div><img src="${icon(d, "tesla")}" alt=""><span>Arc projector</span></div>
      <div><img src="${icon(d, "radiator")}" alt=""><span>Radiator ×2</span></div></div></div>
    <div class="codex">+ CODEX · ${d.planet}</div>`, "s-disc");
}

/* ---------------------------------------------------------------- page building */

function shot(d, n, cap) {
  const f = el("figure", "shot");
  f.innerHTML = `<img src="${img(d, n)}" alt="${cap}" loading="lazy"><figcaption>${cap}</figcaption>`;
  f.dataset.zoom = "img";
  return f;
}

function section(title, sub) {
  const s = el("section", "sec");
  s.appendChild(el("h3", "", title + (sub ? `<span>${sub}</span>` : "")));
  return s;
}

function kitSection(d, sub) {
  const s = section("Block kit", sub);
  for (const [cat, list] of Object.entries(KIT)) {
    s.appendChild(el("h5", "cat", cat));
    const k = el("div", "kit");
    for (const id of list) {
      const t = el("div", "kt");
      t.innerHTML = `<img src="${icon(d, id)}" alt="${LABEL[id]}" loading="lazy"><span class="kl">${LABEL[id]}${NEW.has(id) ? ' <em class="new">NEW</em>' : ""}</span><span class="ks">${SIZE[id]}</span>`;
      k.appendChild(t);
    }
    s.appendChild(k);
  }
  const hv = shot(d, "hull_variants.jpg", "Procedural hull variety, placed in zones: window rows at deck heights, pipe runs, vent clusters, hatches. Plain cells stay plain");
  hv.classList.add("wide");
  s.appendChild(hv);
  return s;
}

/* a line-up render with its labels: names, class and length, placed from the JSON the renderer writes */
function lineupFig(d, name, cap) {
  const f = el("figure", "shot lineup");
  f.innerHTML = `<div class="lu"><img src="${img(d, name + ".jpg")}" alt="${cap}" loading="lazy"></div><figcaption>${cap}</figcaption>`;
  const lu = f.querySelector(".lu");
  const data = (window.LINEUPS || {})[`${d.key}/${name}`] || [];
  const placed = [];
  for (const l of [...data].sort((a, b) => b.x - a.x)) {
    // stack labels that would overlap (small ships side by side) into rows, each with a longer leader line
    let row = 0;
    while (placed.some(p => p.row === row && Math.abs(p.x - l.x) < 0.13 && Math.abs(p.y - l.y) < 0.2)) row++;
    placed.push({ x: l.x, y: l.y, row });
    const t = el("div", `lbl ui-font-${d.key}`);
    t.style.left = `${l.x * 100}%`;
    t.style.top = `${l.y * 100}%`;
    t.style.setProperty("--lift", `${14 + row * 48}px`);
    t.innerHTML = `<b>${l.name}</b><span>${l.cls} · ${l.length} m</span>`;
    lu.appendChild(t);
  }
  f.dataset.zoom = "img";
  return f;
}

function capitalSection(d) {
  const s = section("Capital weapons", "for cruisers and dreadnoughts: big, power-hungry, and not all of them guns");
  const g = el("div", "capw");
  for (const [k, n, size, what, cost] of CAPITAL) {
    const c = el("div", `cw ${FUNC(k)}`);
    c.innerHTML = `<img src="${icon(d, k)}" alt="${n}" loading="lazy"><div><div class="cwn">${n}</div><div class="cws">${size}</div><p>${what}</p><div class="cwc">${cost}</div></div>`;
    g.appendChild(c);
  }
  s.appendChild(g);
  return s;
}

function buildNote(d) {
  const n = el("div", "changed note");
  const stats = (window.BUILDCHECK || {})[d.key] || [];
  const rows = stats.map(r => `<li><b>${r.name}</b> · ${r.cells.toLocaleString("en")} cells${r.big ? ` · ${r.big.toLocaleString("en")} big blocks (${r.u}×${r.u}×${r.u})` : ""} · ${r.mods} blocks placed · ${r.shapes} cut shapes</li>`).join("");
  n.innerHTML = `<h4>Build check: every design below passes</h4>
    <p>Every ship and station is checked against the rules the builder will enforce: <b>all blocks join through real faces</b> (touching at an edge or a corner does not count), and <b>every module sits on its mount face</b> (turrets and masts on their base, engines on their front). Turrets under a hull are placed upside down so they hang from their base.</p>
    <ul>${rows}</ul>`;
  return n;
}

function directionPage(d) {
  const p = el("div", `dir dir-${d.key}`);
  const hero = el("div", "hero");
  hero.innerHTML = `<img src="${img(d, "hero.jpg")}" alt="${d.name} hero scene">
    <div class="hero-text"><div class="hkick">${d.line}</div><h2 class="hn ui-font-${d.key}">${d.name}</h2><p>${d.pitch}</p></div>`;
  hero.dataset.zoom = "img";
  p.appendChild(hero);
  const ch = el("div", "changed");
  ch.innerHTML = `<h4>Round 3: what changed from your feedback</h4><ul>${d.changed.map(s => `<li>${s}</li>`).join("")}</ul>`;
  p.appendChild(ch);

  let s, g;
  if (d.cruiser) {
    s = section("Fleet classes", "light, frigate, cruiser, dreadnought: sterns lined up on a 10 m grid");
    s.appendChild(lineupFig(d, "lineup", `${d.ships[0]} → ${d.heavy[0]} → ${d.cruiser[0]} → ${d.dread[0]}`));
    g = el("div", "g2");
    g.appendChild(shot(d, "ship_cruiser.jpg", `${d.cruiser[0]} · ${d.cruiser[1]}, built from 1 m hull blocks`));
    g.appendChild(shot(d, "ship_dreadnought.jpg", `${d.dread[0]} · ${d.dread[1]}, built from 1.5 m hull blocks`));
    g.appendChild(shot(d, "fleet.jpg", d.fleet));
    g.appendChild(shot(d, "superweapon.jpg", d.superweapon));
    s.appendChild(g);
    p.appendChild(s);
  }

  s = section("Ships", "built from the block kit below; hull slopes are single planes, so edges and corners line up");
  g = el("div", "g2");
  g.appendChild(shot(d, "ship_light.jpg", `${d.ships[0]} · ${d.ships[1]}`));
  g.appendChild(shot(d, "ship_heavy.jpg", `${d.heavy[0]} · ${d.heavy[1]}`));
  s.appendChild(g);
  s.appendChild(buildNote(d));
  p.appendChild(s);

  s = section("In game", "gameplay camera: 55° pitch, 40° FOV");
  g = el("div", "g2");
  g.appendChild(hud(d));
  g.appendChild(shot(d, "flight.jpg", `Free flight past ${d.station[0]} (no HUD)`));
  s.appendChild(g);
  p.appendChild(s);

  s = section("Game screens", "UI composited over the 3D renders, at 1920×1080");
  g = el("div", "g2");
  g.appendChild(inventory(d));
  g.appendChild(dialog(d));
  g.appendChild(discovery(d));
  g.appendChild(shot(d, "combat.jpg", `Combat without HUD: ${d.enemy[0]} breached, debris, explosions, shield hit`));
  s.appendChild(g);
  p.appendChild(s);

  s = section("Station & planet", "stations are built from the same blocks as ships, defences included, because they will be attacked too");
  g = el("div", "g2");
  g.appendChild(shot(d, "station.jpg", `${d.station[0]} · ${d.station[1]}`));
  g.appendChild(shot(d, "planet.jpg", `${d.planet} · ${d.ptype}`));
  s.appendChild(g);
  p.appendChild(s);

  if (d.key !== "opera") {
    s = section("The enemy", "five alien types in this world: see the ALIENS tab for all of them in both worlds and the four colourways");
    g = el("div", "g2");
    g.appendChild(shot(d, "alien_gravid.jpg", `GRAVID, the behemoth carrier, in ${d.name}'s world`));
    g.appendChild(shot(d, "alien.jpg", `BROODMOTHER and a HUSK in ${d.name}'s world`));
    s.appendChild(g);
    p.appendChild(s);
  }

  p.appendChild(capitalSection(d));
  p.appendChild(kitSection(d, "53 blocks. Each fills its grid footprint its own way; hulls stay calm, weapons, modules and cores get the detail"));
  return p;
}

const ALIENS = [
  ["HUSK", "Skimmer, ~24 m", "A manta with cephalic horns, a ridged back, sac rows on the wings and a whip tail. Comes in swarms."],
  ["BROODMOTHER", "Frigate class, ~65 m", "An armoured isopod: overlapping carapace plates, swept side spines, a head shield with horns, glowing sacs, legs hanging underneath."],
  ["LAMPREY", "Boarder, ~100 m", "No eyes. The whole front is a round funnel mouth lined with rings of teeth; it latches onto a hull and bores in. Seven glowing gill pores along each flank."],
  ["CRINOID", "Cruiser class, ~75 m across", "A sea lily with no front and no back: five-fold symmetric, a ring of eyes around a glowing mouth, ten feathered arms curling up like a closing hand, a jointed stalk trailing behind."],
  ["GRAVID", "Behemoth carrier, ~480 m", "A living carrier almost twice the length of a dreadnought. A swollen, banded brood body pitted with egg chambers, an armoured head with an eye cluster and sweeping horns, sail spines, birthing orifices underneath and a forest of trailing tendrils."],
];

function aliensPage() {
  const p = el("div", "dir aliens");
  const dd = R3[0], nc = R3[1];
  const hero = el("div", "hero");
  hero.innerHTML = `<img src="${img(nc, "alien_gravid.jpg")}" alt="GRAVID in NOCTURNE">
    <div class="hero-text"><div class="hkick">Enemies for DRYDOCK and NOCTURNE</div><h2 class="hn">ALIENS</h2>
    <p>REEF's grown ships as the enemy in both worlds. Round 3 adds three that are meant to feel wrong: an eyeless LAMPREY that bores into hulls, a radially symmetric CRINOID, and GRAVID, a behemoth carrier bigger than anything we can build.</p></div>`;
  hero.dataset.zoom = "img";
  p.appendChild(hero);
  const how = el("div", "changed");
  how.innerHTML = `<h4>How they get the REEF look without per-voxel smoothing</h4><ul>
    <li>Same grid as every other ship. Instead of a block shape, each alien cell stores one small number: how deep inside the shell it is (one byte)</li>
    <li>A surface-nets mesher reads those numbers and builds a smooth shell in a single pass. No smoothing step, no subdivision, same cost class as the block mesher</li>
    <li>Damage just removes cells: craters come out organic for free, with glowing flesh inside</li>
    <li>Spines, sacs, eyes, teeth and tendrils are the alien blocks placed on top; the vein pattern is a shader, so any colourway is a palette swap</li>
    <li>GRAVID is sampled at 2 m in these renders (it is seen from far away); in game it would stream in chunks at full resolution</li></ul>`;
  p.appendChild(how);

  let s = section("Five alien types", "lined up on a 10 m grid next to our dreadnought, for scale");
  s.appendChild(lineupFig(nc, "alien_lineup", "HUSK, BROODMOTHER, LAMPREY, CRINOID, SEPULCHRE (ours), GRAVID"));
  const cards = el("div", "acards");
  for (const [n, c, t] of ALIENS) cards.appendChild(el("div", "acard", `<b>${n}</b><span>${c}</span><p>${t}</p>`));
  s.appendChild(cards);
  p.appendChild(s);

  s = section("The new three", "LAMPREY and CRINOID in DRYDOCK's world (bone and jade), GRAVID in both");
  let g = el("div", "g2");
  g.appendChild(shot(dd, "alien_lamprey.jpg", "LAMPREY: the funnel mouth and its rings of teeth"));
  g.appendChild(shot(dd, "alien_crinoid.jpg", "CRINOID: no front, no back; ten feathered arms and a ring of eyes"));
  g.appendChild(shot(nc, "alien_lamprey.jpg", "LAMPREY in obsidian and acid"));
  g.appendChild(shot(nc, "alien_crinoid.jpg", "CRINOID in obsidian and acid"));
  g.appendChild(shot(dd, "alien_gravid.jpg", "GRAVID with BROODMOTHER, HUSKs and a STEVEDORE cruiser for scale"));
  g.appendChild(shot(nc, "superweapon.jpg", "GRAVID under fire from SEPULCHRE"));
  s.appendChild(g);
  p.appendChild(s);

  s = section("In both worlds");
  g = el("div", "g2");
  g.appendChild(shot(dd, "alien.jpg", "BROODMOTHER and HUSK near Ostrava IV"));
  g.appendChild(shot(nc, "alien.jpg", "BROODMOTHER and HUSK near Vesper-9"));
  g.appendChild(shot(dd, "combat.jpg", "Combat: MULEs against a breached BROODMOTHER"));
  g.appendChild(shot(nc, "combat.jpg", "Combat: WRAITHs against a breached BROODMOTHER"));
  s.appendChild(g);
  p.appendChild(s);

  s = section("Colourways", "the same alien, four palettes. Pick a default per world, or use them for different hives");
  for (const d of [dd, nc]) {
    s.appendChild(el("h5", "cat", `Lit in ${d.name}'s world`));
    const cw = el("div", "g4");
    for (const [k, n] of COLOURWAYS) cw.appendChild(shot(d, `alien_cw_${k}.jpg`, n));
    s.appendChild(cw);
  }
  p.appendChild(s);
  return p;
}

function comparePage() {
  const p = el("div", "dir compare");
  p.appendChild(el("p", "lead", "Round 3 side by side: same camera, same scene layout, same block list. Click any image to enlarge. OPERA has no cruiser or dreadnought this round."));
  const rows = [["hero.jpg", "Hero"], ["lineup.jpg", "Classes"], ["fleet.jpg", "Fleet"], ["combat.jpg", "Combat"], ["flight.jpg", "Flight"],
    ["ship_heavy.jpg", "Heavy"], ["station.jpg", "Station"], ["planet.jpg", "Planet"], ["builder.jpg", "Blueprint Lab"]];
  const t = el("div", "cmp cmp3");
  t.appendChild(el("div", "cmp-h", ""));
  for (const d of R3) t.appendChild(el("div", "cmp-h", `<button class="lnk" data-go="${d.key}">${d.name}</button><span>${d.line}</span>`));
  for (const [n, label] of rows) {
    t.appendChild(el("div", "cmp-r", label));
    for (const d of R3) {
      const c = el("div", "cmp-c");
      if (d.key === "opera" && ["lineup.jpg", "fleet.jpg"].includes(n)) { c.classList.add("none"); c.textContent = "—"; t.appendChild(c); continue; }
      const i = el("img"); i.src = img(d, n); i.loading = "lazy"; i.alt = `${d.name} ${label}`; c.appendChild(i); c.dataset.zoom = "img"; t.appendChild(c);
    }
  }
  t.appendChild(el("div", "cmp-r", "Kit"));
  for (const d of R3) {
    const c = el("div", "cmp-c kitmini");
    c.innerHTML = ["singularity", "battery", "storm", "core_3", "thruster_xl", "graviton"].map(k => `<img src="${icon(d, k)}" alt="">`).join("");
    t.appendChild(c);
  }
  p.appendChild(t);
  return p;
}

function archivePage() {
  const p = el("div", "dir compare");
  p.appendChild(el("p", "lead", "Earlier rounds as delivered. Round 2 (10 October): DRYDOCK and NOCTURNE polished, OPERA new, aliens introduced. Round 1 (10 October): six directions; finalists DRYDOCK and NOCTURNE (3D and UI), DRYDOCK and PULP HORIZON (visual style). PULP HORIZON is parked for later; REEF became the aliens."));
  p.appendChild(el("h5", "cat", "Round 2"));
  let rows = [["hero.jpg", "Hero"], ["ship_light.jpg", "Light"], ["ship_heavy.jpg", "Heavy"], ["station.jpg", "Station"], ["planet.jpg", "Planet"]];
  let t = el("div", "cmp cmp3");
  t.appendChild(el("div", "cmp-h", ""));
  for (const d of R2) t.appendChild(el("div", "cmp-h", `<b>${d.name}</b><span>${d.line}</span>`));
  for (const [n, label] of rows) {
    t.appendChild(el("div", "cmp-r", label));
    for (const d of R2) { const c = el("div", "cmp-c"); const i = el("img"); i.src = img(d, n); i.loading = "lazy"; i.alt = `${d.name} ${label}`; c.appendChild(i); c.dataset.zoom = "img"; t.appendChild(c); }
  }
  p.appendChild(t);
  p.appendChild(el("h5", "cat", "Round 1"));
  rows = [["hero.jpg", "Hero"], ["combat.jpg", "Combat"], ["station.jpg", "Station"], ["planet.jpg", "Planet"]];
  t = el("div", "cmp");
  t.appendChild(el("div", "cmp-h", ""));
  for (const d of R1) t.appendChild(el("div", "cmp-h", `<b>${d.name}</b><span>${d.line}</span>`));
  for (const [n, label] of rows) {
    t.appendChild(el("div", "cmp-r", label));
    for (const d of R1) { const c = el("div", "cmp-c"); const i = el("img"); i.src = img(d, n); i.loading = "lazy"; i.alt = `${d.name} ${label}`; c.appendChild(i); c.dataset.zoom = "img"; t.appendChild(c); }
  }
  p.appendChild(t);
  return p;
}

/* ---------------------------------------------------------------- round 4: ships built from approved batch-3 silhouettes */

const R4 = [
  { key: "drydock", base: "img/r4/drydock", name: "DRYDOCK" },
  { key: "nocturne", base: "img/r4/nocturne", name: "NOCTURNE" },
];

const R4_SHIPS = [{
  key: "halo", name: "HALO", line: "ARK war line · fire-control destroyer · ARK war pattern 24", length: "86 m",
  story: "The expedition can't make shields, so the HALO is the team's shield: six point-defence turrets and a flak battery that shoot down missiles and fighters aimed at anyone nearby. "
    + "The fire-control dish on its port boom marks one target, and every linked ARK gun in range fires on it together. It punches by making everyone else hit.",
  parts: [
    "<b>Heat to port:</b> a radiator deck with 16 fins (they glow when the reactor runs hot), and the fire-control dish on a lattice boom",
    "<b>Ammunition to starboard:</b> the flak magazine, a plated safety-yellow box against the hull, with a flak turret on top and a hazard band at its bow end",
    "<b>Six point-defence turrets</b> (rotary cannons): two on the forward sponsons, one on the bridge roof, one amidships, two on the engine block",
    "<b>Twin coilgun turret</b> on the bone armour prow; the prow and the bridge are the bone standard blocks, the hull is graphite",
    "<b>Red fire-team stripe</b> across the hull, two main engines and two manoeuvring thrusters, reactor and core inside",
    "<b>New:</b> the <code>arkw</code> livery (both worlds) and the <code>ark</code> plating: even 1.5 m blocks, bolts and stencilled pattern numbers",
  ],
  open: "Turrets, the flak and the engines use the existing kit blocks at twice their frigate size (as the cruisers already do with the triple battery). A destroyer needs its own sizes in the kit, or a rule for scaled blocks.",
}];

function arkPage() {
  const p = el("div", "dir ark");
  const dd = R4[0], nc = R4[1];
  for (const sh of R4_SHIPS) {
    const hero = el("div", "hero");
    hero.innerHTML = `<img src="${img(dd, sh.key + "_hero.jpg")}" alt="${sh.name} in DRYDOCK">
      <div class="hero-text"><div class="hkick">Round 4 · batch 3 in 3D · the first build</div><h2 class="hn">${sh.name}</h2><p>${sh.line} · ${sh.length}</p><p>${sh.story}</p></div>`;
    hero.dataset.zoom = "img";
    p.appendChild(hero);
    const stats = ((window.BUILDCHECK || {}).r4 || []).find(r => r.name === sh.name);
    const ch = el("div", "changed");
    ch.innerHTML = `<h4>What is built where</h4><ul>${sh.parts.map(s => `<li>${s}</li>`).join("")}</ul>`
      + (stats ? `<p class="lead">Build check: ${stats.ok ? "passes" : "FAILS"} (every block attached through shared faces, every module on its mount face). ${stats.cells.toLocaleString()} cells of 0.5 m, ${stats.mods} modules, ${stats.shapes} distinct cut shapes.</p>` : "")
      + `<p class="lead"><b>Open question:</b> ${sh.open}</p>`;
    p.appendChild(ch);
    let s = section("Silhouette to blocks", "the approved plan view beside the 3D build seen from straight above; bow to the right, port at the top");
    let g = el("div", "g2");
    g.appendChild(shot(dd, sh.key + "_silhouette.svg", "Approved silhouette (batch 3), DRYDOCK paint"));
    g.appendChild(shot(dd, sh.key + "_plan.jpg", "The 3D build from above"));
    s.appendChild(g);
    p.appendChild(s);
    for (const d of [dd, nc]) {
      s = section(`In ${d.name}`, d === dd ? "graphite, bone blocks, safety yellow" : "blackened graphite, ash-grey blocks, sodium yellow");
      g = el("div", "g2");
      if (d === nc) g.appendChild(shot(d, sh.key + "_hero.jpg", `${sh.name} near Vesper-9`));
      g.appendChild(shot(d, sh.key + "_game.jpg", "The game camera: 55° pitch, 40° field of view"));
      g.appendChild(shot(d, sh.key + "_rear.jpg", "Stern: two main engines, the hot radiator deck to port, the magazine to starboard"));
      g.appendChild(shot(d, sh.key + "_detail.jpg", "Bridge, bone prow and the fire-control boom"));
      if (d === nc) g.appendChild(shot(d, sh.key + "_plan.jpg", "From above"));
      s.appendChild(g);
      p.appendChild(s);
    }
  }
  return p;
}

/* ---------------------------------------------------------------- slope blocks for builders (ledger open question 6) */

const SLOPE_BLOCKS = [
  ["w11", "Slope", "1 × 1 · 45°"],
  ["hip", "Hip corner", "1 × 1 · where two 45° slopes meet"],
  ["s12", "Long slope", "1 × 2 · 27°, one block"],
  ["hip12", "Long hip corner", "2 × 2 · where two long slopes meet"],
];
const SLOPE_KITS = [
  ["spec", "Today's spec", "cube + slope", "9.0 %", "Every diagonal edge where two slopes meet becomes a stair step."],
  ["k2", "Option 1 · 2 blocks", "slope, hip corner", "1.5 %", "Everything at 45°. At that angle the corner where two slopes meet is the same block whichever way it faces, so one hip corner closes ridges, pyramid noses and chamfered edges."],
  ["k3", "Option 2 · 3 blocks", "slope, hip corner, long slope 1×2", "3.3 %", "Adds a shallow 27° slope for long flat runs: decks, glacis armour, wings. Its edges have no corner block, so wherever it meets another slope the edge steps (see the nose)."],
  ["k4", "Option 3 · 4 blocks", "option 2 + long hip corner 2×2", "2.3 %", "Closes the ridge where two long slopes meet, but not a long-slope nose or a long slope meeting a 45° edge: those are different corners again. A fully closed 1×2 family needs about four blocks of its own."],
];

function slopesPage() {
  const p = el("div", "dir slopes");
  const d = { base: "img/r4/slopes" };
  p.appendChild(el("p", "lead", "How few sloped blocks do builders need? Each block below is <b>one placeable block</b> (the long slope is a single 1×2 block) and rotates any way. The cube is always there. Each kit builds the same small frigate the way a player with that kit would: faces only at the kit's angles, and only the kit's blocks."));
  const ch = el("div", "changed");
  ch.innerHTML = `<h4>What the study found</h4><ul>
    <li><b>Two blocks build everything at 45°:</b> the slope and the hip corner. At 45° the corner where two slopes meet is the same shape in every orientation, so one block closes ridges, pyramid noses and chamfered edges</li>
    <li><b>Every extra angle brings its own corners.</b> A 1×2 slope needs a ridge corner, a nose corner and a corner where it meets a 45° edge, all different blocks. That is why kits 3 and 4 still step at the nose</li>
    <li>The classic builder set (slope, three-face corner, inverted corner) did worse than slope plus hip corner (8.4 % of cells off against 1.5 %): the hip corner is the block that matters</li>
    <li>"Cells off" is the share of cut cells that the kit cannot build within 10 %, across every round-3 hull and the HALO, each redesigned on <i>that kit's</i> angles. The hulls differ between kits, so the numbers are not a ranking: a kit with more blocks can never do worse on the same hull. Option 2 scores higher only because its long slopes create edges that have no corner block</li></ul>`;
  p.appendChild(ch);
  let s = section("The blocks", "cut faces in ochre; the dashed box is the block's size in cells (0.5 m each)");
  const g = el("div", "g4");
  for (const [k, n, sub] of SLOPE_BLOCKS) {
    const f = el("figure", "shot");
    f.innerHTML = `<img src="${img(d, "block_" + k + ".svg")}" alt="${n}" loading="lazy" style="background:#0A0F1A;aspect-ratio:1/1;object-fit:contain;height:auto"><figcaption><b>${n}</b> · ${sub}</figcaption>`;
    g.appendChild(f);
  }
  s.appendChild(g);
  p.appendChild(s);
  for (const [k, n, blocks, off, note] of SLOPE_KITS) {
    s = section(`${n}: ${blocks}`, `cells off: ${off} · ${note}`);
    const g2 = el("div", "g2");
    g2.appendChild(shot(d, `demo_${k}.jpg`, `${n}: the demo frigate`));
    g2.appendChild(shot(d, `demo_${k}_nose.jpg`, `${n}: the nose, where the kits differ`));
    s.appendChild(g2);
    p.appendChild(s);
  }
  s = section("WRAITH rebuilt with options 1 and 2", "same layout and sizes, tapers redrawn at each kit's angles, only the kit's blocks; both pass the build check");
  const gw = el("div", "g2");
  const fw = shot(d, "wraith_compare.jpg", "Left to right: original, option 1 (slope + hip corner), option 2 (+ long slope 1×2). Option 2's nose steps where its long slopes meet the 45° chamfers");
  fw.classList.add("wide");
  gw.appendChild(fw);
  s.appendChild(gw);
  p.appendChild(s);
  s = section("Reference", "the same frigate with free cut cells, as the look-dev renders do it");
  const g3 = el("div", "g2");
  g3.appendChild(shot(d, "demo_free.jpg", "Free cut cells"));
  g3.appendChild(shot(d, "demo_free_nose.jpg", "Free cut cells: the nose"));
  s.appendChild(g3);
  p.appendChild(s);
  return p;
}

const pages = {};
const TABS = R3.map(d => ({ key: d.key, name: d.name })).concat([{ key: "ark", name: "ARK · round 4" }, { key: "slopes", name: "Slopes" }, { key: "aliens", name: "ALIENS" }, { key: "compare", name: "Compare", cls: "cmp-tab" }, { key: "archive", name: "Earlier rounds", cls: "arch-tab" }]);

function show(key) {
  if (!TABS.some(t => t.key === key)) key = "drydock";
  for (const b of document.querySelectorAll("#tabs button")) b.setAttribute("aria-selected", b.dataset.key === key ? "true" : "false");
  const main = $("#main");
  if (!pages[key]) {
    pages[key] = key === "compare" ? comparePage() : key === "aliens" ? aliensPage() : key === "ark" ? arkPage() : key === "slopes" ? slopesPage() : key === "archive" ? archivePage() : directionPage(R3.find(d => d.key === key));
  }
  main.replaceChildren(pages[key]);
  requestAnimationFrame(fit);
  try { localStorage.setItem("lookbook-tab-r3", key); } catch (e) { /* storage unavailable */ }
  window.scrollTo(0, 0);
}

function declutter() {
  // lift any line-up label that still overlaps one already placed (measured boxes, so it holds at every width)
  for (const lu of document.querySelectorAll(".lineup .lu")) {
    const labels = [...lu.querySelectorAll(".lbl")].sort((a, b) => parseFloat(b.style.left) - parseFloat(a.style.left));
    const done = [];
    for (const t of labels) {
      let lift = parseFloat(t.style.getPropertyValue("--lift")) || 14;
      for (let k = 0; k < 12; k++) {
        const r = t.getBoundingClientRect();
        if (!done.some(o => r.left < o.right + 4 && o.left < r.right + 4 && r.top < o.bottom + 2 && o.top < r.bottom + 2)) break;
        lift += 16;
        t.style.setProperty("--lift", `${lift}px`);
      }
      done.push(t.getBoundingClientRect());
    }
  }
}

function fit() {
  declutter();
  for (const w of document.querySelectorAll(".stage-wrap")) {
    const s = w.firstElementChild;
    const k = w.clientWidth / 1920;
    s.style.transform = `scale(${k})`;
    w.style.height = `${1080 * k}px`;
  }
}

function lightbox(node) {
  const lb = $("#lightbox"), inner = $("#lb-inner");
  inner.replaceChildren();
  if (node.dataset.zoom === "stage") {
    const c = node.cloneNode(true);
    c.classList.add("lb-stage");
    inner.appendChild(c);
    const k = Math.min(window.innerWidth * 0.96 / 1920, window.innerHeight * 0.94 / 1080);
    c.style.width = `${1920 * k}px`;
    c.style.height = `${1080 * k}px`;
    c.firstElementChild.style.transform = `scale(${k})`;
  } else {
    const i = node.querySelector("img").cloneNode();
    i.loading = "eager";
    inner.appendChild(i);
  }
  lb.hidden = false;
}

function init() {
  const tabs = $("#tabs");
  for (const t of TABS) {
    const b = el("button", t.cls || "", t.name);
    b.dataset.key = t.key;
    b.setAttribute("role", "tab");
    b.addEventListener("click", () => show(t.key));
    tabs.appendChild(b);
  }
  document.addEventListener("click", (e) => {
    const go = e.target.closest("[data-go]");
    if (go) { show(go.dataset.go); return; }
    if (e.target.closest("#lightbox")) { $("#lightbox").hidden = true; return; }
    const z = e.target.closest("[data-zoom]");
    if (z) lightbox(z);
  });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape") $("#lightbox").hidden = true; });
  window.addEventListener("resize", fit);
  let start = "drydock";
  try { start = localStorage.getItem("lookbook-tab-r3") || "drydock"; } catch (e) { /* storage unavailable */ }
  show(start);
}
init();
