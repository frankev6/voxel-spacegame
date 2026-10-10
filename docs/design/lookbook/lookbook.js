/* Look Book: six 3D art directions. Renders come from ArtSource/LookDev/lookdev.py (Blender).
   Game screens are HTML at 1920x1080 composited over the renders and scaled to fit. */
"use strict";

const DIRS = [
  {
    key: "drydock", name: "DRYDOCK", line: "Industrial used-future",
    pitch: "Working ships in a working universe. Bone and graphite plates, safety-yellow cargo, stencils, pipes, nav lights and hard sunlight on black space. Everything was built to be repaired, and you can see how.",
    ships: ["MULE", "light utility gunship"], heavy: ["BULWARK", "hauler-frigate"], station: ["KESSLER YARD", "shipyard with a hull under construction"],
    planet: "OSTRAVA IV", ptype: "Rust desert world · 0.8 g · dust storms · mining towns", sys: "KESSLER BELT",
    speaker: "Dockmaster Oyelaran", where: "Kessler Yard, berth 6",
    strengths: ["Readable at any zoom: two-tone hulls and safety colours", "Cheapest to extend: greebles and stencils carry variety", "Damage reads instantly against bone plating"],
    risks: ["Familiar look (Space Engineers / Expanse territory)", "Needs strong lighting to avoid flat grey"],
  },
  {
    key: "nocturne", name: "NOCTURNE", line: "Pearl ceramic & light",
    pitch: "Ships are pale pearl ceramic traced by thin light-lines, with violet drives and glowing glyph systems, set against deep blue-black space with luminous filaments. Calm, eerie, elegant: the Silence feels close.",
    ships: ["KESTREL", "swept-wing interceptor"], heavy: ["VIGIL", "blade cruiser"], station: ["LANTERN SPIRE", "vertical spire with light rings"],
    planet: "HALVARD", ptype: "Night world · city lights · polar aurora", sys: "THE DIM REACH",
    speaker: "Spire Warden Ilse", where: "Lantern Spire, upper ring",
    strengths: ["Most atmospheric; light-lines make silhouettes pop", "Pearl hulls fix the old dark-hull contrast problem", "UI already liked (thin holographic instrument)"],
    risks: ["Bloom and emissive budget must be managed in big fights", "Pale hulls on bright nebula need rim light"],
  },
  {
    key: "pulp-horizon", name: "PULP HORIZON", line: "Paperback-cover sci-fi",
    pitch: "Colour-blocked rockets with fins, domes and portholes, crisp ink outlines and toon shading under painted gradient skies. Every system has its own three-colour key. Bold, warm, instantly recognisable.",
    ships: ["COMET", "finned rocket fighter"], heavy: ["BIG BERTHA", "colour-block hauler"], station: ["BIG WHEEL", "classic wheel station"],
    planet: "MARIGOLD", ptype: "Banded giant · ring system · 3 moons", sys: "TANGERINE DRIFT",
    speaker: "Captain Rosa Vance", where: "Big Wheel, hub promenade",
    strengths: ["Most distinctive screenshot; reads as one artist", "Cheapest textures (flat colour + ink)", "Toon shading hides voxel stair-steps"],
    risks: ["Can tip toy-like; tension must come from colour scripting", "Outlines need tuning for 300 m zoom"],
  },
  {
    key: "orbital", name: "ORBITAL", line: "NASA-punk hard SF",
    pitch: "White thermal blankets, crumpled gold foil, trusses, radiators and solar wings under raw unfiltered sunlight. Real-world spaceflight hardware scaled up into voxels, over a blue-marble planet.",
    ships: ["TUG", "service tug with solar wings"], heavy: ["PATHFINDER", "truss-spine explorer"], station: ["GATEWAY", "truss station with arrays"],
    planet: "NEW ARDEN", ptype: "Earth-like · oceans 71% · breathable", sys: "ARDEN SYSTEM",
    speaker: "Flight Director Okafor", where: "Gateway, node 2",
    strengths: ["Grounded and credible; strong 'explorer' fantasy", "Solar wings, radiators and foil make modules visible", "Real planets make discovery moments land"],
    risks: ["Lots of white: needs exposure discipline", "Combat feels less 'gamey'; weapons need invention"],
  },
  {
    key: "reef", name: "REEF", line: "Grown biotech",
    pitch: "Ships and stations are grown, not built: nacre shells with iridescent sheen, bioluminescent veins, glowing pores and coral-branch stations above an ocean world with living light in its seas.",
    ships: ["RAY", "manta skimmer"], heavy: ["LEVIATHAN", "segmented carrier"], station: ["CORAL BLOOM", "branching coral station"],
    planet: "THALASSA", ptype: "Ocean world · bioluminescent blooms", sys: "PELAGIC CLUSTER",
    speaker: "Tender Mirae", where: "Coral Bloom, lower pods",
    strengths: ["Truly unusual for a block game", "Smoothing hides the voxel grid entirely", "Strong alien/mystery tone"],
    risks: ["Highest tech cost: smoothing per brick at runtime", "Block readability: functions hide in organic shapes"],
  },
  {
    key: "facet", name: "FACET", line: "Low-poly bold",
    pitch: "Flat-shaded facets in white, cobalt and signal orange under candy-gradient skies with big soft stars and gem-cut planets. Chunky, cheerful, crisp: a toy box that still blows apart properly.",
    ships: ["DART", "chunky arrow fighter"], heavy: ["BASTION", "ziggurat battleship"], station: ["PRISM HUB", "bipyramid hub with docking pads"],
    planet: "GEMMA", ptype: "Gem-cut world · ring · 2 moons", sys: "SUGARGLASS",
    speaker: "Hub Boss Teo", where: "Prism Hub, pad 3",
    strengths: ["Extremely readable at all zooms", "Cheapest to produce and to render", "Bright palette suits a long play session"],
    risks: ["Least 'mysterious'; tone may fight the story", "Close to many indie looks"],
  },
];

const KIT = {
  "Structure": ["hull_cube", "hull_wedge", "hull_corner", "hull_inner", "hull_slope2", "hull_round", "hull_armor", "girder"],
  "Procedural hull variants": ["hull_window", "hull_hatch", "hull_vent", "hull_pipes"],
  "Weapons": ["autocannon", "railgun", "missile", "beam", "flak"],
  "Modules": ["cockpit", "thruster_s", "thruster_l", "reactor", "shield", "capacitor", "cargo", "radiator", "sensor", "solar"],
  "Cores": ["core_1", "core_2", "core_3", "core_35"],
};
const LABEL = {
  hull_cube: "Hull", hull_wedge: "Hull slope", hull_corner: "Hull corner", hull_inner: "Inner corner", hull_slope2: "Long slope 1×2",
  hull_round: "Rounded edge", hull_window: "Hull · windows", hull_hatch: "Hull · hatch & latches", hull_vent: "Hull · vents",
  hull_pipes: "Hull · pipes", hull_armor: "Heavy armour", girder: "Girder frame", cockpit: "Cockpit", autocannon: "Autocannon",
  railgun: "Railgun", missile: "Missile pod", beam: "Beam lance", flak: "Flak turret", thruster_s: "Thruster", thruster_l: "Main engine",
  reactor: "Reactor", shield: "Shield emitter", capacitor: "Capacitor bank", cargo: "Cargo pod", radiator: "Radiator", sensor: "Sensor mast",
  solar: "Solar wing", core_1: "Core", core_2: "Core", core_3: "Core", core_35: "Core",
};
const SIZE = {
  hull_cube: "1×1×1", hull_wedge: "1×1×1", hull_corner: "1×1×1", hull_inner: "1×1×1", hull_slope2: "1×2×1", hull_round: "1×1×1",
  hull_window: "1×1×1", hull_hatch: "1×1×1", hull_vent: "1×1×1", hull_pipes: "1×1×1", hull_armor: "1×1×1", girder: "1×1×1",
  cockpit: "4×4×3", autocannon: "2×2×2", railgun: "2×6×2", missile: "3×3×1", beam: "2×3×2", flak: "3×3×3", thruster_s: "2×3×2",
  thruster_l: "4×6×4", reactor: "3×3×3", shield: "2×2×2", capacitor: "2×2×2", cargo: "2×4×2", radiator: "1×6×4", sensor: "2×2×3",
  solar: "1×8×4", core_1: "1×1×1", core_2: "2×2×2", core_3: "3×3×3", core_35: "3×5×3",
};

const $ = (s, r = document) => r.querySelector(s);
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const img = (d, n) => `img/${d}/${n}`;
const icon = (d, k) => `img/${d}/icons/${k}.webp`;

/* ---------------------------------------------------------------- game screens (1920x1080 stages) */

function stage(d, bg, inner, cls) {
  const wrap = el("div", "stage-wrap");
  const st = el("div", `stage ui-${d.key} ${cls || ""}`);
  st.style.backgroundImage = `url(${img(d.key, bg)})`;
  st.innerHTML = inner;
  wrap.appendChild(st);
  wrap.dataset.zoom = "stage";
  return wrap;
}

function bar(label, pct, kind, val) {
  const segs = Array.from({ length: 10 }, (_, i) => `<i class="${i < Math.round(pct / 10) ? "on" : ""}"></i>`).join("");
  return `<div class="bar ${kind}"><span class="bl">${label}</span><span class="bt"><b style="width:${pct}%"></b><span class="segs">${segs}</span></span><span class="bv">${val}</span></div>`;
}

function avatar(d) {
  return `<div class="nora"><svg viewBox="0 0 96 96"><circle cx="48" cy="48" r="30" class="n-ring"/>` +
    Array.from({ length: 24 }, (_, i) => { const a = i / 24 * Math.PI * 2; return `<circle cx="${48 + Math.cos(a) * 38}" cy="${48 + Math.sin(a) * 38}" r="${2 + (i % 3)}" class="n-dot"/>`; }).join("") +
    `<path d="M18 48 Q28 30 36 48 T54 48 T72 48 T80 48" class="n-wave"/></svg></div>`;
}

function hud(d) {
  const w = (k, n, key, cd, ammo) => `<div class="wpn ${cd ? "cool" : ""}"><span class="wk">${key}</span><img src="${icon(d.key, k)}" alt=""><span class="wn">${n}</span><span class="wa">${ammo}</span>${cd ? `<span class="wc" style="width:${cd}%"></span>` : ""}</div>`;
  return stage(d, "combat.jpg", `
    <div class="z tl panel"><div class="ph">${d.sys}</div><div class="loc">Hostile contact · ${d.heavy[0]}-class</div>
      <div class="obj"><span class="ok">◆</span> Disable the ${d.heavy[0]}'s engines<br><span class="sub">2 / 3 thrusters destroyed</span></div></div>
    <div class="z tr radar"><div class="rr"></div><div class="rr r2"></div><div class="rr r3"></div><i class="me"></i><i class="bp e" style="left:64%;top:30%"></i><i class="bp e" style="left:58%;top:24%"></i><i class="bp f" style="left:44%;top:56%"></i><i class="bp n" style="left:26%;top:70%"></i><span class="rl">RADAR · 2 km</span></div>
    <div class="target"><span class="c c1"></span><span class="c c2"></span><span class="c c3"></span><span class="c c4"></span>
      <div class="tcard panel"><div class="ph">${d.heavy[0]}</div><div class="tsub">${d.heavy[1]} · 31 m away</div>
      ${bar("HULL", 46, "hull", "46%")}${bar("SHLD", 0, "shield", "DOWN")}
      <div class="sys"><span class="bad">ENGINE ×2 DESTROYED</span><span>REACTOR EXPOSED</span><span>FLAK 1 ONLINE</span></div></div></div>
    <div class="callout c-a">+ THRUSTER DESTROYED</div><div class="callout c-b">BREACH · 41 CELLS</div>
    <div class="z bl panel status"><div class="ph">${d.ships[0]}</div>${bar("HULL", 82, "hull", "1 640")}${bar("SHLD", 38, "shield", "114")}${bar("ENRG", 64, "energy", "640")}
      <div class="spd">SPEED <b>142</b> m/s · BOOST <b>READY</b></div></div>
    <div class="z bc wpns">${w("beam", "BEAM LANCE", "1", 0, "∞")}${w("autocannon", "AUTOCANNON", "2", 0, "412")}${w("missile", "MISSILES", "3", 62, "6")}${w("railgun", "RAILGUN", "4", 35, "18")}</div>
    <div class="z br panel comms">${avatar(d)}<div><div class="ph">NORA</div><p>Their reactor is exposed behind the breach. Hit it and the whole hull goes.</p></div></div>
    <div class="reticle"></div>`, "s-hud");
}

function inventory(d) {
  const tiles = ["autocannon", "beam", "railgun", "missile", "flak", "thruster_s", "thruster_l", "reactor", "shield", "capacitor", "cargo",
    "radiator", "sensor", "solar", "cockpit", "core_1", "core_2", "core_3", "core_35", "hull_cube", "hull_wedge", "hull_slope2", "hull_armor", "hull_window"];
  const counts = [4, 2, 1, 3, 1, 6, 2, 1, 2, 4, 3, 2, 1, 2, 1, 1, 1, 0, 0, 999, 640, 212, 96, 120];
  const rar = (k) => k.startsWith("core_3") || k === "core_35" ? "r4" : k.startsWith("core") || k === "reactor" || k === "railgun" ? "r3" : k === "flak" || k === "beam" || k === "shield" ? "r2" : "r1";
  const grid = tiles.map((k, i) => `<div class="tile ${rar(k)} ${k === "core_3" ? "sel" : ""} ${counts[i] === 0 ? "none" : ""}"><img src="${icon(d.key, k)}" alt=""><span class="cnt">${counts[i] === 0 ? "—" : counts[i] > 99 ? counts[i] : "×" + counts[i]}</span></div>`).join("");
  const hot = ["hull_cube", "hull_wedge", "autocannon", "beam", "thruster_s", "shield", "reactor", "core_2", "cargo"].map((k, i) => `<div class="hk ${i === 1 ? "sel" : ""}"><span>${i + 1}</span><img src="${icon(d.key, k)}" alt=""></div>`).join("");
  return stage(d, "builder.jpg", `
    <div class="topbar panel"><span class="ph">BLUEPRINT LAB</span><span class="tb-ship">${d.ships[0]}</span><span class="tb-btn">↶ UNDO</span><span class="tb-btn">↷ REDO</span><span class="tb-btn">⇋ MIRROR X</span><span class="tb-sp"></span><span class="tb-cells">CELLS 1 284 / 4 000</span><span class="tb-btn pri">COMMIT ⏎</span></div>
    <div class="inv panel"><div class="ph">INVENTORY</div>
      <div class="cats"><span>ALL</span><span>STRUCTURE</span><span>WEAPONS</span><span>MODULES</span><span class="on">CORES</span></div>
      <div class="grid">${grid}</div></div>
    <div class="tip panel"><div class="tip-head"><img src="${icon(d.key, "core_3")}" alt=""><div><div class="ph">CORE 3×3×3</div><div class="rar r4">EXOTIC · FUNCTIONAL</div></div></div>
      <p>The heart of a ship. Holds the blueprint together, stores power and anchors up to 8 000 cells. Lose it and the hull goes dark.</p>
      <div class="kv"><span>MAX CELLS</span><b>8 000</b><span>POWER</span><b>+40 /s</b><span>STORAGE</span><b>200</b><span>MASS</span><b>1.6 t</b></div></div>
    <div class="stats panel"><div class="ph">SHIP STATS</div>
      <div class="st"><span>MASS</span><b>38.2 t</b><em class="up">+1.6</em></div>
      <div class="st"><span>THRUST</span><b>120 kN</b><em></em></div>
      <div class="st"><span>TOP SPEED</span><b>168 m/s</b><em class="dn">−6</em></div>
      <div class="st"><span>POWER</span><b>+92 / −71</b><em class="up">+40</em></div>
      <div class="st"><span>SHIELD</span><b>300</b><em></em></div>
      <div class="st"><span>HULL</span><b>2 140</b><em class="up">+300</em></div>
      ${bar("PWR", 76, "energy", "76%")}</div>
    <div class="hotbar">${hot}</div>`, "s-inv");
}

function dialog(d) {
  return stage(d, "station.jpg", `
    <div class="dim"></div>
    <div class="dlg panel"><div class="portrait">${avatar(d)}</div>
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
      <div><img src="${icon(d.key, "core_2")}" alt=""><span>Core 2×2×2</span></div>
      <div><img src="${icon(d.key, "sensor")}" alt=""><span>Sensor mast</span></div>
      <div><img src="${icon(d.key, "radiator")}" alt=""><span>Radiator ×2</span></div></div></div>
    <div class="codex">+ CODEX · ${d.planet}</div>`, "s-disc");
}

/* ---------------------------------------------------------------- page */

function shot(d, n, cap) {
  const f = el("figure", "shot");
  f.innerHTML = `<img src="${img(d.key, n)}" alt="${cap}" loading="lazy"><figcaption>${cap}</figcaption>`;
  f.dataset.zoom = "img";
  return f;
}

function section(title, sub) {
  const s = el("section", "sec");
  s.appendChild(el("h3", "", title + (sub ? `<span>${sub}</span>` : "")));
  return s;
}

function directionPage(d) {
  const p = el("div", `dir dir-${d.key}`);
  const hero = el("div", "hero");
  hero.innerHTML = `<img src="${img(d.key, "hero.jpg")}" alt="${d.name} hero scene">
    <div class="hero-text"><div class="hk">${d.line}</div><h2 class="hn ui-font-${d.key}">${d.name}</h2><p>${d.pitch}</p></div>`;
  hero.dataset.zoom = "img";
  p.appendChild(hero);
  const sw = el("div", "sw");
  sw.innerHTML = `<div><h4>Why pick it</h4><ul>${d.strengths.map(s => `<li>${s}</li>`).join("")}</ul></div><div><h4>Watch out for</h4><ul>${d.risks.map(s => `<li>${s}</li>`).join("")}</ul></div>`;
  p.appendChild(sw);

  let s = section("In game", "gameplay camera: 55° pitch, 40° FOV, as specified for flight");
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
  const f = shot(d, "combat.jpg", "Combat without HUD: cell damage, debris, explosions, shield hit");
  g.appendChild(f);
  s.appendChild(g);
  p.appendChild(s);

  s = section("Ships", "two ships per direction, built from the block kit below");
  g = el("div", "g2");
  g.appendChild(shot(d, "ship_light.jpg", `${d.ships[0]} · ${d.ships[1]}`));
  g.appendChild(shot(d, "ship_heavy.jpg", `${d.heavy[0]} · ${d.heavy[1]}`));
  s.appendChild(g);
  p.appendChild(s);

  s = section("Station & planet");
  g = el("div", "g2");
  g.appendChild(shot(d, "station.jpg", `${d.station[0]} · ${d.station[1]}`));
  g.appendChild(shot(d, "planet.jpg", `${d.planet} · ${d.ptype}`));
  s.appendChild(g);
  p.appendChild(s);

  s = section("Block kit", "every block fills its grid footprint its own way; hulls stay calm, cores and weapons get the detail");
  for (const [cat, list] of Object.entries(KIT)) {
    s.appendChild(el("h5", "cat", cat));
    const k = el("div", `kit ui-${d.key}`);
    for (const id of list) {
      const t = el("div", "kt");
      t.innerHTML = `<img src="${icon(d.key, id)}" alt="${LABEL[id]}" loading="lazy"><span class="kl">${LABEL[id]}</span><span class="ks">${SIZE[id]}</span>`;
      k.appendChild(t);
    }
    s.appendChild(k);
  }
  const hv = shot(d, "hull_variants.jpg", "Procedural hull variety: the same plain hull cells get windows, hatches, vents, pipes or plates by a per-cell hash, so large surfaces never look smooth and empty");
  hv.classList.add("wide");
  s.appendChild(hv);
  p.appendChild(s);
  return p;
}

function comparePage() {
  const p = el("div", "dir compare");
  p.appendChild(el("p", "lead", "All six directions side by side. Same camera, same scene layout, same block list. Only the art direction changes. Click any image to enlarge."));
  const rows = [["hero.jpg", "Hero"], ["combat.jpg", "Combat"], ["flight.jpg", "Flight"], ["station.jpg", "Station"], ["planet.jpg", "Planet"], ["builder.jpg", "Blueprint Lab"]];
  const t = el("div", "cmp");
  t.appendChild(el("div", "cmp-h", ""));
  for (const d of DIRS) { const h = el("div", "cmp-h", `<button class="lnk" data-go="${d.key}">${d.name}</button><span>${d.line}</span>`); t.appendChild(h); }
  for (const [n, label] of rows) {
    t.appendChild(el("div", "cmp-r", label));
    for (const d of DIRS) { const c = el("div", "cmp-c"); const i = el("img"); i.src = img(d.key, n); i.loading = "lazy"; i.alt = `${d.name} ${label}`; c.appendChild(i); c.dataset.zoom = "img"; t.appendChild(c); }
  }
  t.appendChild(el("div", "cmp-r", "Kit"));
  for (const d of DIRS) {
    const c = el("div", "cmp-c kitmini");
    c.innerHTML = ["core_3", "flak", "thruster_l", "reactor", "cockpit", "hull_pipes"].map(k => `<img src="${icon(d.key, k)}" alt="">`).join("");
    t.appendChild(c);
  }
  p.appendChild(t);
  return p;
}

const pages = {};
function show(key) {
  for (const b of document.querySelectorAll("#tabs button")) b.setAttribute("aria-selected", b.dataset.key === key ? "true" : "false");
  const main = $("#main");
  if (!pages[key]) pages[key] = key === "compare" ? comparePage() : directionPage(DIRS.find(d => d.key === key));
  main.replaceChildren(pages[key]);
  requestAnimationFrame(fit);
  try { localStorage.setItem("lookbook-tab", key); } catch (e) { /* storage unavailable */ }
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
  for (const d of DIRS.concat([{ key: "compare", name: "Compare all" }])) {
    const b = el("button", d.key === "compare" ? "cmp-tab" : "", d.name);
    b.dataset.key = d.key;
    b.setAttribute("role", "tab");
    b.addEventListener("click", () => show(d.key));
    tabs.appendChild(b);
  }
  document.addEventListener("click", (e) => {
    const go = e.target.closest("[data-go]");
    if (go) { show(go.dataset.go); return; }
    if (e.target.closest("#lightbox")) { if (!e.target.closest(".lb-inner") || e.target.closest(".lb-close")) $("#lightbox").hidden = true; else $("#lightbox").hidden = true; return; }
    const z = e.target.closest("[data-zoom]");
    if (z) lightbox(z);
  });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape") $("#lightbox").hidden = true; });
  window.addEventListener("resize", fit);
  let start = "compare";
  try { start = localStorage.getItem("lookbook-tab") || "compare"; } catch (e) { /* storage unavailable */ }
  show(start);
}
init();
