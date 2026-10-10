/* Look Book. Round 2: the owner's finalists (DRYDOCK, NOCTURNE), the new OPERA direction, and alien enemies.
   Round 1 (six directions) stays available as an archive. Renders come from ArtSource/LookDev/lookdev.py (Blender).
   Game screens are HTML at 1920x1080 composited over the renders and scaled to fit. */
"use strict";

/* ---------------------------------------------------------------- round 2 data */

const R2 = [
  {
    key: "drydock", base: "img/r2/drydock", name: "DRYDOCK", line: "Industrial used-future",
    pitch: "Working ships in a working universe. Bone plating in faction liveries, safety yellow, colour-coded machinery you can read at a glance, container decks, hazard paint, work lights, under a dusty galactic sky with ringed rust worlds.",
    changed: [
      "Background is no longer black: a galactic band with dust lanes, rust and teal nebula, and subtle colour from warm to cool",
      "Colour definition: faction liveries (signal blue, oxide red) and function colours on every block (weapons oxide red, power teal, cargo orange, engines graphite)",
      "Planet gets real rings (lit by the sun and shadowed by the planet) and a proper atmosphere glow that hugs the limb and fades out, instead of a shield-like shell",
      "Seven new weapons: rotary cannon, heavy twin cannon, spinal mass driver, arc projector, plasma mortar, torpedo launcher, salvage harpoon",
      "Every existing block redone: cockpit is a heavy cab with a window band, the reactor is a fission pile with control rods, cores are a gyro, a gimbal, a foundry heart and a linear accelerator (no more glowing cube in a cage)",
      "Hull corners and slopes now line up: hulls are built from straight-sided sections, so every slope is one plane across all its cells",
      "Alien enemies (bone and jade by default) in this world",
      "UI polish: hazard-striped headers, function-coloured inventory tiles, stencil discovery stamp",
    ],
    ships: ["MULE", "salvage gunship"], heavy: ["BULWARK", "container frigate"], station: ["KESSLER YARD", "dry dock with a frigate under construction"],
    planet: "OSTRAVA IV", ptype: "Ringed rust world · 0.8 g · dust storms · mining towns", sys: "KESSLER BELT",
    speaker: "Dockmaster Oyelaran", where: "Kessler Yard, berth 6",
    enemy: ["BROODMOTHER", "alien leviathan"], enemySys: ["CARAPACE BREACHED", "SAC CLUSTER EXPOSED", "SPINES ARMED"],
    obj: ["Break the BROODMOTHER's carapace", "2 / 3 breaches open"], nora: "The sacs along its flank are feeding the shell. Burst them and the carapace goes soft.",
  },
  {
    key: "nocturne", base: "img/r2/nocturne", name: "NOCTURNE", line: "Industrial gothic horror",
    pitch: "The ALIEN-franchise mood: hunched, ribbed ships in worn grey and ochre with oxblood and mustard machinery, slit windows of sodium light, red strobes, a refinery cathedral towed between stars, a storm world below, and something black and living out in the dark.",
    changed: [
      "A distinct style from DRYDOCK: hunched silhouettes, ribbed housings, spires, hanging hoses, slit windows (DRYDOCK is boxy and clean-painted)",
      "No more single colour: worn grey plating with ochre panels, oxblood weapons, mustard power, olive cargo, warning-red bands",
      "Black vents are gone: vents are deep ribbed grilles with an ember glow, so they read as depth, not as missing pixels",
      "More experimental background: smoke and dark dust against a sickly green glow, very few stars, a thin haze that catches work lights",
      "Station: THE ROOD, an inverted cathedral refinery towed by the CANTICLE",
      "Alien enemies in obsidian and acid green: the horror half of the franchise",
      "UI unchanged, as asked",
    ],
    ships: ["WRAITH", "dropship"], heavy: ["CANTICLE", "refinery tug"], station: ["THE ROOD", "refinery cathedral under tow"],
    planet: "VESPER-9", ptype: "Storm world · permanent lightning · no surface signals", sys: "THE HOLLOW REACH",
    speaker: "Warden Ilse Marrow", where: "The Rood, lower nave",
    enemy: ["BROODMOTHER", "alien leviathan"], enemySys: ["CARAPACE BREACHED", "SAC CLUSTER EXPOSED", "SPINES ARMED"],
    obj: ["Break the BROODMOTHER's carapace", "2 / 3 breaches open"], nora: "The sacs along its flank are feeding the shell. Burst them and the carapace goes soft.",
  },
  {
    key: "opera", base: "img/r2/opera", name: "OPERA", line: "Space opera (new)",
    pitch: "Paperback space opera on a grand scale. Cream hulls in bold liveries with gold trim, a 2001-style capital ship and double-wheel station, Dune-like monumental masses, Fifth Element warmth, and No Man's Sky skies with painted nebulae and ringed giants.",
    changed: [
      "New direction from your references: space-opera covers, DUNE, THE FIFTH ELEMENT, 2001, NO MAN'S SKY",
      "Ships: LARK (swept-wing courier) and SOVEREIGN (geodesic command sphere, spine with a spinal gun, rotating ring, engine monolith)",
      "Station: THE CROWN, a double wheel on one axle with checkered liveries and ring windows",
      "Blocks: art-deco hard surface (tapered chamfered housings, chrome visors, gold pinstripes, fluted barrels); cores are an armillary sphere, an orrery and cut gems",
      "Painted skies: deep ultramarine to magenta, glowing nebula cores, star clusters, a banded gas giant with rings",
      "Its own UI: art-deco panels with gold rules and diamonds, Cinzel and Josefin Sans (both OFL)",
    ],
    ships: ["LARK", "swept-wing courier"], heavy: ["SOVEREIGN", "monumental capital ship"], station: ["THE CROWN", "double wheel station"],
    planet: "AURELIA", ptype: "Banded gas giant · ring system · 2 moons", sys: "THE GILDED REACH",
    speaker: "Steward Cassius Vey", where: "The Crown, upper wheel",
    enemy: ["SOVEREIGN", "rival capital ship (Solar livery)"], enemySys: ["SPINE BREACHED", "RING OFFLINE", "MASS DRIVER CHARGING"],
    obj: ["Silence the SOVEREIGN's spinal gun", "2 / 3 capacitor banks down"], nora: "Their ring is offline. Hit the spine behind it before the mass driver finishes charging.",
  },
];

/* round 1 archive (six directions, as delivered on 2026-10-10) */
const R1 = [
  ["drydock", "DRYDOCK", "Industrial used-future"], ["nocturne", "NOCTURNE", "Pearl ceramic & light"],
  ["pulp-horizon", "PULP HORIZON", "Paperback-cover sci-fi"], ["orbital", "ORBITAL", "NASA-punk hard SF"],
  ["reef", "REEF", "Grown biotech"], ["facet", "FACET", "Low-poly bold"],
].map(([key, name, line]) => ({ key, name, line, base: `img/${key}` }));

const KIT = {
  "Structure": ["hull_cube", "hull_wedge", "hull_corner", "hull_inner", "hull_slope2", "hull_round", "hull_armor", "girder"],
  "Procedural hull variants": ["hull_window", "hull_hatch", "hull_vent", "hull_pipes"],
  "Weapons": ["autocannon", "gatling", "railgun", "beam", "harpoon", "tesla", "missile", "flak", "plasma", "torpedo", "heavy_cannon", "mass_driver"],
  "Modules": ["cockpit", "thruster_s", "thruster_l", "reactor", "shield", "capacitor", "cargo", "radiator", "sensor", "solar"],
  "Cores": ["core_1", "core_2", "core_3", "core_35"],
};
const NEW = new Set(["gatling", "heavy_cannon", "mass_driver", "tesla", "plasma", "torpedo", "harpoon"]);
const LABEL = {
  hull_cube: "Hull", hull_wedge: "Hull slope", hull_corner: "Hull corner", hull_inner: "Inner corner", hull_slope2: "Long slope 1×2",
  hull_round: "Rounded edge", hull_window: "Hull · windows", hull_hatch: "Hull · hatch & latches", hull_vent: "Hull · vents",
  hull_pipes: "Hull · pipes", hull_armor: "Heavy armour", girder: "Girder frame", cockpit: "Cockpit", autocannon: "Autocannon",
  gatling: "Rotary cannon", railgun: "Railgun", heavy_cannon: "Heavy twin cannon", mass_driver: "Spinal mass driver", beam: "Laser lance",
  tesla: "Arc projector", plasma: "Plasma mortar", torpedo: "Torpedo launcher", missile: "Missile cells", flak: "Flak turret",
  harpoon: "Salvage harpoon", thruster_s: "Thruster", thruster_l: "Main engine", reactor: "Reactor", shield: "Shield projector",
  capacitor: "Capacitor bank", cargo: "Cargo pod", radiator: "Radiator", sensor: "Sensor mast", solar: "Solar wing",
  core_1: "Core", core_2: "Core", core_3: "Core", core_35: "Core",
};
const SIZE = {
  hull_cube: "1×1×1", hull_wedge: "1×1×1", hull_corner: "1×1×1", hull_inner: "1×1×1", hull_slope2: "1×2×1", hull_round: "1×1×1",
  hull_window: "1×1×1", hull_hatch: "1×1×1", hull_vent: "1×1×1", hull_pipes: "1×1×1", hull_armor: "1×1×1", girder: "1×1×1",
  cockpit: "4×4×3", autocannon: "2×2×2", gatling: "2×3×2", railgun: "2×6×2", heavy_cannon: "4×6×3", mass_driver: "3×12×3",
  beam: "2×4×2", tesla: "2×2×3", plasma: "3×3×3", torpedo: "3×6×2", missile: "3×3×1", flak: "3×3×3", harpoon: "2×4×2",
  thruster_s: "2×3×2", thruster_l: "4×6×4", reactor: "3×3×3", shield: "2×2×2", capacitor: "2×2×2", cargo: "2×4×2",
  radiator: "1×6×4", sensor: "2×2×3", solar: "1×8×4", core_1: "1×1×1", core_2: "2×2×2", core_3: "3×3×3", core_35: "3×5×3",
};
const FUNC = (k) => ["autocannon", "gatling", "railgun", "heavy_cannon", "mass_driver", "beam", "tesla", "plasma", "torpedo", "missile", "flak", "harpoon"].includes(k) ? "f-wpn"
  : ["reactor", "capacitor", "shield"].includes(k) ? "f-pwr" : ["thruster_s", "thruster_l", "radiator"].includes(k) ? "f-eng"
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

function directionPage(d) {
  const p = el("div", `dir dir-${d.key}`);
  const hero = el("div", "hero");
  hero.innerHTML = `<img src="${img(d, "hero.jpg")}" alt="${d.name} hero scene">
    <div class="hero-text"><div class="hkick">${d.line}</div><h2 class="hn ui-font-${d.key}">${d.name}</h2><p>${d.pitch}</p></div>`;
  hero.dataset.zoom = "img";
  p.appendChild(hero);
  const ch = el("div", "changed");
  ch.innerHTML = `<h4>What changed from your feedback</h4><ul>${d.changed.map(s => `<li>${s}</li>`).join("")}</ul>`;
  p.appendChild(ch);

  let s = section("In game", "gameplay camera: 55° pitch, 40° FOV");
  let g = el("div", "g2");
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

  s = section("Ships", "built from the block kit below; hull slopes are single planes, so edges and corners line up");
  g = el("div", "g2");
  g.appendChild(shot(d, "ship_light.jpg", `${d.ships[0]} · ${d.ships[1]}`));
  g.appendChild(shot(d, "ship_heavy.jpg", `${d.heavy[0]} · ${d.heavy[1]}`));
  s.appendChild(g);
  p.appendChild(s);

  if (d.key !== "opera") {
    s = section("The enemy", "alien hulls in this world: see the ALIENS tab for how they are built and the four colourways");
    g = el("div", "g2");
    g.appendChild(shot(d, "alien.jpg", `BROODMOTHER and a HUSK in ${d.name}'s world`));
    const cw = el("div", "cw4");
    for (const [k, n] of COLOURWAYS) cw.appendChild(shot(d, `alien_cw_${k}.jpg`, n));
    g.appendChild(cw);
    s.appendChild(g);
    p.appendChild(s);
  }

  s = section("Station & planet");
  g = el("div", "g2");
  g.appendChild(shot(d, "station.jpg", `${d.station[0]} · ${d.station[1]}`));
  g.appendChild(shot(d, "planet.jpg", `${d.planet} · ${d.ptype}`));
  s.appendChild(g);
  p.appendChild(s);

  p.appendChild(kitSection(d, "38 blocks. Each fills its grid footprint its own way; hulls stay calm, weapons, modules and cores get the detail"));
  return p;
}

function aliensPage() {
  const p = el("div", "dir aliens");
  const dd = R2[0], nc = R2[1];
  const hero = el("div", "hero");
  hero.innerHTML = `<img src="${img(nc, "alien.jpg")}" alt="Alien ship in NOCTURNE">
    <div class="hero-text"><div class="hkick">Enemies for DRYDOCK and NOCTURNE</div><h2 class="hn">ALIENS</h2>
    <p>REEF's grown ships, brought into the finalists' worlds as the enemy. A HUSK skimmer and a BROODMOTHER, an armoured isopod the size of a frigate, with carapace plates, side spines, glowing sacs, eyes and hanging legs.</p></div>`;
  hero.dataset.zoom = "img";
  p.appendChild(hero);
  const how = el("div", "changed");
  how.innerHTML = `<h4>How they get the REEF look without per-voxel smoothing</h4><ul>
    <li>Same 0.5 m grid as every other ship. Instead of a block shape, each alien cell stores one small number: how deep inside the shell it is (one byte)</li>
    <li>A surface-nets mesher reads those numbers and builds a smooth shell in a single pass. No smoothing step, no subdivision, same cost class as the block mesher</li>
    <li>Damage just removes cells: craters come out organic for free, with glowing flesh inside</li>
    <li>Spines, sacs, eyes and legs are the alien blocks placed on top; the vein pattern is a shader, so any colourway is a palette swap</li></ul>`;
  p.appendChild(how);
  let s = section("In DRYDOCK's world", "default colourway: bone and jade");
  let g = el("div", "g2");
  g.appendChild(shot(dd, "alien.jpg", "BROODMOTHER and HUSK near Ostrava IV"));
  g.appendChild(shot(dd, "combat.jpg", "Combat: MULEs against a breached BROODMOTHER"));
  s.appendChild(g);
  p.appendChild(s);
  s = section("In NOCTURNE's world", "default colourway: obsidian and acid");
  g = el("div", "g2");
  g.appendChild(shot(nc, "alien.jpg", "BROODMOTHER and HUSK near Vesper-9"));
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
  p.appendChild(el("p", "lead", "Round 2 side by side: the two finalists and the new OPERA. Same camera, same scene layout, same block list. Click any image to enlarge."));
  const rows = [["hero.jpg", "Hero"], ["combat.jpg", "Combat"], ["flight.jpg", "Flight"], ["ship_heavy.jpg", "Heavy"], ["station.jpg", "Station"], ["planet.jpg", "Planet"], ["builder.jpg", "Blueprint Lab"]];
  const t = el("div", "cmp cmp3");
  t.appendChild(el("div", "cmp-h", ""));
  for (const d of R2) t.appendChild(el("div", "cmp-h", `<button class="lnk" data-go="${d.key}">${d.name}</button><span>${d.line}</span>`));
  for (const [n, label] of rows) {
    t.appendChild(el("div", "cmp-r", label));
    for (const d of R2) { const c = el("div", "cmp-c"); const i = el("img"); i.src = img(d, n); i.loading = "lazy"; i.alt = `${d.name} ${label}`; c.appendChild(i); c.dataset.zoom = "img"; t.appendChild(c); }
  }
  t.appendChild(el("div", "cmp-r", "Kit"));
  for (const d of R2) {
    const c = el("div", "cmp-c kitmini");
    c.innerHTML = ["core_3", "heavy_cannon", "thruster_l", "reactor", "cockpit", "tesla"].map(k => `<img src="${icon(d, k)}" alt="">`).join("");
    t.appendChild(c);
  }
  p.appendChild(t);
  return p;
}

function archivePage() {
  const p = el("div", "dir compare");
  p.appendChild(el("p", "lead", "Round 1 as delivered on 10 October: six directions. Finalists picked: DRYDOCK and NOCTURNE (3D), DRYDOCK and NOCTURNE (UI), DRYDOCK and PULP HORIZON (visual style). PULP HORIZON is parked for later; REEF became the alien enemies."));
  const rows = [["hero.jpg", "Hero"], ["combat.jpg", "Combat"], ["station.jpg", "Station"], ["planet.jpg", "Planet"]];
  const t = el("div", "cmp");
  t.appendChild(el("div", "cmp-h", ""));
  for (const d of R1) t.appendChild(el("div", "cmp-h", `<b>${d.name}</b><span>${d.line}</span>`));
  for (const [n, label] of rows) {
    t.appendChild(el("div", "cmp-r", label));
    for (const d of R1) { const c = el("div", "cmp-c"); const i = el("img"); i.src = img(d, n); i.loading = "lazy"; i.alt = `${d.name} ${label}`; c.appendChild(i); c.dataset.zoom = "img"; t.appendChild(c); }
  }
  p.appendChild(t);
  return p;
}

const pages = {};
const TABS = R2.map(d => ({ key: d.key, name: d.name })).concat([{ key: "aliens", name: "ALIENS" }, { key: "compare", name: "Compare", cls: "cmp-tab" }, { key: "round1", name: "Round 1 archive", cls: "arch-tab" }]);

function show(key) {
  if (!TABS.some(t => t.key === key)) key = "drydock";
  for (const b of document.querySelectorAll("#tabs button")) b.setAttribute("aria-selected", b.dataset.key === key ? "true" : "false");
  const main = $("#main");
  if (!pages[key]) {
    pages[key] = key === "compare" ? comparePage() : key === "aliens" ? aliensPage() : key === "round1" ? archivePage() : directionPage(R2.find(d => d.key === key));
  }
  main.replaceChildren(pages[key]);
  requestAnimationFrame(fit);
  try { localStorage.setItem("lookbook-tab-r2", key); } catch (e) { /* storage unavailable */ }
  window.scrollTo(0, 0);
}

function fit() {
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
  try { start = localStorage.getItem("lookbook-tab-r2") || "drydock"; } catch (e) { /* storage unavailable */ }
  show(start);
}
init();
