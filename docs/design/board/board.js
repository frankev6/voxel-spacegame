/* Design board (SPEC-80 ART-005). Plain JS, no framework, no fetch: it must open from file://.
 * Token values are read from the generated tokens/<direction>.css with getComputedStyle, so every value shown is the
 * value in Assets/Game/UI/Theme/Directions/tokens-<direction>.uss. Everything else below is transcribed from
 * docs/design/DESIGN-SYSTEM.md (section numbers in comments). */
"use strict";

const DIRECTIONS = ["drydock", "nocturne", "pulp-horizon"];

const DATA = {
  "drydock": {
    name: "DRYDOCK", letter: "A", section: 5,
    tagline: "Industrial used-future. Bone and graphite plating, safety-coded systems, stencils and navigation lights; black space; the alien is clean and wrong.",
    fonts: { display: "Barlow Condensed SemiBold", body: "Barlow Regular / SemiBold", mono: "IBM Plex Mono Regular" },
    displaySizes: ["l", "xl", "xxl"], easing: "cubic-bezier(0.33, 1, 0.68, 1)", easingName: "ease-out-cubic",
    hull: [["Bone", "#CFC8B8", "Default plates; ARK primary"], ["Ash", "#A6A6A0", "Secondary plates"], ["Gunmetal", "#6C7075", "Armour, heavy plates"],
           ["Graphite", "#3B3E42", "Frames, trims, girders"], ["Bare steel", "#9A9C9E", "Unpainted metal, worn edges"], ["Cavity", "#1E2023", "Behind exposed internals"]],
    functions: [["Propulsion", "#FF9A1F", "0.6 → 3.0"], ["Weapon", "#E5332A", "2.0 → 3.0"], ["Power", "#F5D33A", "2.5"], ["Shield", "#3D8BFF", "2.5"], ["Core", "#3DDC84", "3.0, 0.5 Hz"], ["Utility", "#E04AC8", "1.5"]],
    team: [["ARK expedition", "#CFC8B8", "#3B3E42", "#F2B705"], ["Vestari", "#E4E9EE", "#2F7FD0", "#9FD8FF"], ["Khuchad Clan", "#A8471E", "#2B2522", "#FF7A2F"],
           ["Tlaxcala", "#1F7A4D", "#E6E0CF", "#5BE3A1"], ["Scavengers", "#5B5E5A", "#8E2B25", "#D9B23A"], ["Vanguard", "#ECE7DC", "#101317", "#FFC86B"],
           ["Silence", "#060508", "#060508", "#6E3BFF"], ["Xeno probes", "#2E2A33", "#1A171E", "#C8FF2E"]],
    space: [["Backdrop base", "#040507", "luminance 0.0015"], ["Galaxy band peak", "#2A2622", "≤ 0.02"], ["Nebula dust, warm", "#1C1510", "≤ 30 % of sky"],
            ["Nebula dust, cool", "#0C121B", "low frequency"], ["Motion dust", "#8A8478", "25 % opacity"], ["Window strips", "#FFE2B0", "1.2"],
            ["Nav light, port", "#FF2A1F", "1 Hz"], ["Nav light, starboard", "#2AFF6A", "1 Hz"]],
    spaceNote: "Black, sparse, photographic. Stars in 3 tiers at 60 % spectral saturation; key light pitched 35° below the plane.",
    vfx: [["Cell debris", ["#FFB066"], 0.4, "hot → paint over 0.4 s; life 3–6 s"], ["Chunk dissolve", ["#FF7A2F"], 1.2, "char edge, ember rim"],
          ["Kinetic hit", ["#FFD9A0", "#8A8478"], 0.35, "sparks 0.15–0.35 s, puff 0.6 s"], ["Energy hit", ["#FFFFFF", "#FFB066"], 0.08, "flash 0.08 s, drops 0.5 s"],
          ["Explosion small", ["#FFF4E0", "#FF8A2A", "#7A2A10", "#2B2522"], 0.5, "flash 0.06, fireball 0.5, soot 0.8 s"],
          ["Reactor", ["#F5D33A", "#FFFFFF"], 2.5, "core flash then white"], ["Beam", ["#FFFFFF"], 0.05, "on 0.05 s, off 0.12 s"],
          ["Muzzle flash", ["#FFD9A0"], 0.05, "3-petal star"], ["Shield ripple", ["#3D8BFF"], 0.4, "flash 0.15, collapse 0.6, raise 0.5 s"],
          ["Lane jump", ["#F4F6FA"], 0.5, "spool, white-out, arrival 0.5 s each"], ["Interact beacon", ["#F2B705"], 1.0, "1 Hz ring pulse"]],
    damageTypes: [["Kinetic", "#FFD9A0"], ["Energy", "#FFC27A"], ["Explosive", "#FF8A2A"], ["Ram", "#E8E2D4"]],
    ui: "Utilitarian plates, stencil, teal accent. Active panel: 2 px accent bar on the left; header in display caps with a mono panel code.",
    motion: "Panels slide 8 px and fade in; alerts “relay flicker”; no bounce, no scale.",
  },
  "nocturne": {
    name: "NOCTURNE", letter: "B", section: 6,
    tagline: "Dark glossy hulls traced by thin light-lines, glyphs of light on every system, deep blue-black space with luminous filaments. Damage is darkness spreading.",
    fonts: { display: "Michroma Regular (caps, +10 % tracking)", body: "Inter Regular / SemiBold", mono: "JetBrains Mono Regular" },
    displaySizes: ["l", "xl", "xxl"], easing: "cubic-bezier(0.37, 0, 0.63, 1)", easingName: "ease-in-out-sine",
    hull: [["Obsidian", "#1B1E24", "Default plates"], ["Gunmetal", "#2C3139", "Secondary; ARK primary"], ["Slate", "#424954", "Armour"],
           ["Pewter", "#6B7380", "Trims, frames, edges"], ["Bare", "#5D636B", "Exposed metal"], ["Cavity", "#08090C", "Interior background"]],
    functions: [["Thruster", "#A98BFF", "1.0 → 5.0"], ["Weapon", "#FF3D6E", "3.0 → 5.0"], ["Power", "#FFB84D", "3.5"], ["Shield", "#3FD8FF", "3.5"], ["Core", "#F4F8FF", "4.0, 0.25 Hz"], ["Utility", "#6FFFC8", "2.5"]],
    team: [["ARK expedition", "#2C3139", "#6B7380", "#FFE9C7"], ["Vestari", "#18202E", "#C9D6E3", "#9FE8FF"], ["Khuchad Clan", "#2E1E18", "#6E3420", "#FF7A3D"],
           ["Tlaxcala", "#142820", "#2F6B55", "#B8FF6A"], ["Scavengers", "#24211F", "#5E2A24", "#FFB347"], ["Vanguard", "#DADDE2", "#0A0C10", "#FFFFFF"],
           ["Silence", "#000000", "#000000", "#000000"], ["Xeno probes", "#1E1A22", "#2E2733", "#C8FF2E"]],
    space: [["Backdrop base", "#03040A", "luminance 0.0013"], ["Filament, teal", "#0E3A4A", "field ≤ 0.035"], ["Filament, violet", "#261447", "field ≤ 0.035"],
            ["Filament core", "#1F6E86", "≤ 3 % of screen"], ["Motion dust", "#4A6E8A", "15 % opacity"], ["Rim light", "#5FD0FF", "0.56"],
            ["City lights", "#FFD9A0", "inhabited worlds"], ["Seam light (ARK)", "#FFE9C7", "0.35"]],
    spaceNote: "Blue-black with luminous filaments; full spectral saturation; key light 25°, rim light from the far side; ACES, bloom 0.8 / 0.9.",
    vfx: [["Cell debris", ["#9FE8FF"], 0.8, "glowing edges 0.8 s; life 3–6 s"], ["Chunk dissolve", ["#9FE8FF"], 1.2, "edges dissolve into motes"],
          ["Kinetic hit", ["#FFFFFF"], 0.25, "white streak sparks 0.12–0.25 s"], ["Energy hit", ["#FF3D6E"], 0.3, "expanding light ring"],
          ["Explosion", ["#FFFFFF", "#3FD8FF", "#A98BFF"], 0.6, "implosion; small 0.6, large 1.8, reactor 2.5 s"],
          ["Beam", ["#FFFFFF", "#FF3D6E"], 0.05, "on 0.05 s, off 0.12 s"], ["Shield ripple", ["#3FD8FF"], 0.4, "always faintly visible"],
          ["Thruster plume", ["#A98BFF"], 0.6, "ion trail fade 0.6 s"], ["Lane jump", ["#000000", "#A6EEFF"], 0.5, "light tunnel, then black-out"]],
    damageTypes: [["Kinetic", "#FFFFFF"], ["Energy", "#FF3D6E"], ["Explosive", "#FFB84D"], ["Ram", "#9FE8FF"]],
    ui: "Thin holographic lines, cyan. Translucent panels with corner brackets when focused; ghost buttons; thin bars with numbers on hover or below 30 %.",
    motion: "Reveal = fade with a 6 px upward move; alerts pulse their glow at 1 Hz.",
  },
  "pulp-horizon": {
    name: "PULP HORIZON", letter: "C", section: 7,
    tagline: "Graphic colour blocks, crisp ink edges, painterly gradient skies with a three-colour key per star system. A paperback cover you can blow apart.",
    fonts: { display: "Archivo Expanded ExtraBold", body: "Atkinson Hyperlegible Regular / Bold", mono: "Space Mono Regular" },
    displaySizes: ["l", "xl", "xxl"], easing: "cubic-bezier(0.34, 1.56, 0.64, 1)", easingName: "ease-out-back",
    hull: [["Cream", "#EFE4C8", "Default plates; ARK primary"], ["Sand", "#D9C9A3", "Secondary plates"], ["Teal-grey", "#5F7F86", "Frames; ARK secondary"],
           ["Ink", "#2A3140", "Lines, girders, trims"], ["Bare", "#9FA8B0", "Exposed metal"], ["Cavity", "#161B26", "Interior background"]],
    functions: [["Thruster", "#FF8A2B", "nozzle 1.5"], ["Weapon", "#E2483D", "0"], ["Power", "#FFC233", "0"], ["Shield", "#2F9BFF", "0"], ["Core", "#3FB950", "beacon 1.5"], ["Utility", "#8A5CD6", "0"]],
    team: [["ARK expedition", "#EFE4C8", "#5F7F86", "#FFC233"], ["Vestari", "#F6F3EE", "#2F6FD6", "#8FD3FF"], ["Khuchad Clan", "#B5482E", "#2A3140", "#FF8A2B"],
           ["Tlaxcala", "#3FA34D", "#EFE4C8", "#C5E84A"], ["Scavengers", "#C9A13A", "#8E3B2E", "#F27BAA"], ["Vanguard", "#E3C38E", "#FBF7EC", "#FFE08A"],
           ["Silence", "#07080C", "#07080C", "#F6EEDC"], ["Xeno probes", "#6E8040", "#3A2547", "#D7FF3A"]],
    space: [["Teal Dusk · top", "#0E1C2E", "sky key"], ["Teal Dusk · horizon", "#163049", ""], ["Teal Dusk · band", "#1F4E5F", ""],
            ["Plum Night · band", "#3A2547", ""], ["Rust Haze · band", "#5A2E22", ""], ["Ink Blue · band", "#24406A", ""],
            ["Moss Dark · band", "#2F4A32", ""], ["Stars, dust", "#F6EEDC", "cream dots"]],
    spaceNote: "Painted gradients; each star system picks a three-colour sky key; band luminance ≤ 0.08. Toon ramp and 1.5 px ink outlines.",
    vfx: [["Cell debris", ["#F6EEDC"], 3.0, "confetti in cell colours; life 3–6 s"], ["Chunk dissolve", ["#FFC233"], 1.2, "cells pop into confetti"],
          ["Hits", ["#FFE08A", "#F6EEDC"], 0.33, "2-tone starburst, 2–4 frames at 12 fps"],
          ["Explosion", ["#FF8A2B", "#FFC233", "#F6EEDC", "#2A3140"], 0.6, "flat starburst, ink ring, ink smoke"],
          ["Beam", ["#FF6F59", "#F6EEDC"], 0.05, "flat bar, cream core, ink edge"], ["Shield", ["#2F9BFF"], 0.4, "2 px outline and dent ring"],
          ["Thruster plume", ["#F6EEDC", "#FF8A2B"], 0.083, "stepped flicker at 12 fps"], ["Lane jump", ["#1F4E5F"], 0.5, "speed lines, colour wipe"]],
    damageTypes: [["Kinetic", "#FFE08A"], ["Energy", "#FF6F59"], ["Explosive", "#FF8A2B"], ["Ram", "#F6EEDC"]],
    ui: "Bold print cards, sunflower accent. Flat cards with a print shadow 4 px right and down; buttons press onto their shadow; chunky outlined bars.",
    motion: "Appear with overshoot (ease-out-back), cards drop 12 px, stamps rotate from −3° to 0°.",
  },
};

// Public-domain mood-board images (ART-003). Paths relative to docs/design/board/.
const MOOD = {
  "drydock": [["iss-truss", "ISS truss (NASA)"], ["apollo15-service-module", "Apollo 15 service module"], ["mccandless-mmu", "McCandless, STS-41-B"],
              ["earthrise", "Earthrise, Apollo 8"], ["apollo11-cm-heat-shield", "Apollo 11 CM after re-entry"], ["iso7010-warning", "ISO 7010 warning layout"]],
  "nocturne": [["pillars-of-creation", "Pillars of Creation (Hubble)"], ["cosmic-cliffs", "Cosmic Cliffs (Webb)"], ["kamoamoa-fissure", "Kamoamoa fissure (USGS)"]],
  "pulp-horizon": [["stanford-torus-interior", "Stanford torus, Don Davis"], ["stanford-torus-cutaway", "Torus cutaway, Rick Guidice"],
                   ["bernal-sphere-guidice", "Bernal sphere, Rick Guidice"], ["bernal-sphere-exterior", "Bernal sphere exterior"]],
};

const BLOCKS = [["block_hull", "Hull (4 × 4 patch)"], ["block_hull_angled", "Hull angled"], ["block_thruster_small", "Thruster small"],
                ["weapon_plasma_turret_small", "Plasma turret small"], ["block_core_small", "Core small"]];

const COLOR_TOKENS = ["bg", "surface-1", "surface-2", "surface-3", "border", "border-strong", "text", "text-muted", "text-on-accent", "accent", "accent-strong",
  "positive", "warning", "danger", "info", "ally", "enemy", "neutral", "shield", "hull", "energy", "objective", "clue", "overlay", "travel-cover"];
const FACTION_TOKENS = ["ark", "raiders", "xeno", "vestari", "tlaxcala", "khuchad-clan", "neutral"];
const TEXT_ON = ["text", "text-muted", "accent", "positive", "warning", "danger", "info", "ally", "enemy", "neutral", "shield", "hull", "energy", "objective", "clue", "border-strong"];

// HUD zones, DESIGN-SYSTEM §10.4.2 (x, y, w, h at 1920 × 1080).
const ZONES = [["Z-TL", 32, 32, 480, 128], ["Z-TC", 672, 32, 576, 112], ["Z-TR", 1568, 32, 320, 320], ["Z-L", 32, 200, 528, 600],
               ["Z-C", 560, 200, 800, 600], ["Z-R", 1488, 400, 400, 360], ["Z-BL", 32, 856, 528, 192], ["Z-BC", 640, 952, 640, 96], ["Z-BR", 1408, 936, 480, 112]];

// ---------------------------------------------------------------------------------------------------------------- helpers

const h = (tag, attrs = {}, ...children) => {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === null || v === undefined || v === false) continue;
    if (k === "class") el.className = v;
    else if (k === "style") el.style.cssText = v;
    else if (k === "html") el.innerHTML = v;
    else if (k.startsWith("on")) el.addEventListener(k.slice(2), v);
    else el.setAttribute(k, v === true ? "" : v);
  }
  for (const c of children.flat()) if (c !== null && c !== undefined && c !== false) el.append(c.nodeType ? c : document.createTextNode(String(c)));
  return el;
};

const store = {
  get(key, fallback) { try { const v = window.localStorage.getItem("design-board:" + key); return v === null ? fallback : JSON.parse(v); } catch (e) { return fallback; } },
  set(key, value) { try { window.localStorage.setItem("design-board:" + key, JSON.stringify(value)); } catch (e) { /* storage unavailable: keep working */ } },
};

function tokenValue(scope, name) { return getComputedStyle(scope).getPropertyValue(name).trim(); }

function parseColor(value) {
  const v = value.trim();
  let m = /^#([0-9a-f]{6})$/i.exec(v);
  if (m) return [0, 2, 4].map(i => parseInt(m[1].slice(i, i + 2), 16) / 255).concat(1);
  m = /^rgba?\(([^)]+)\)$/i.exec(v);
  if (m) { const p = m[1].split(",").map(s => parseFloat(s)); return [p[0] / 255, p[1] / 255, p[2] / 255, p.length > 3 ? p[3] : 1]; }
  return null;
}
const lin = c => (c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
function luminance(rgb) { return 0.2126 * lin(rgb[0]) + 0.7152 * lin(rgb[1]) + 0.0722 * lin(rgb[2]); }
function contrast(a, b) { const la = luminance(a), lb = luminance(b); return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05); }
function over(fg, bg) { const a = fg[3]; return [fg[0] * a + bg[0] * (1 - a), fg[1] * a + bg[1] * (1 - a), fg[2] * a + bg[2] * (1 - a), 1]; }

function glyph(kind) {
  // §10.6 notice glyphs and function glyphs as small inline SVG (no namespace string needed: parsed as HTML).
  const paths = {
    positive: '<path d="M5 12.5l4.5 4.5L19 7.5" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="square"/>',
    warning: '<path d="M12 3.5L21.5 20h-19z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="miter"/><path d="M12 9.5v5M12 16.5v1.8" stroke="currentColor" stroke-width="2"/>',
    danger: '<path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="2.2" stroke-linecap="square"/>',
    info: '<circle cx="12" cy="12" r="8.5" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 10.5v6M12 7v1.8" stroke="currentColor" stroke-width="2"/>',
    chevron: '<path d="M5 15l7-6 7 6M5 20l7-6 7 6" fill="none" stroke="currentColor" stroke-width="2"/>',
    crosshair: '<circle cx="12" cy="12" r="6" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4" stroke="currentColor" stroke-width="2"/>',
    bolt: '<path d="M13.5 2.5L6 13.5h5L10 21.5l8-11.5h-5z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="miter"/>',
    ring: '<path d="M8 19.5A8 8 0 1 1 16 19.5" fill="none" stroke="currentColor" stroke-width="3"/>',
    diamond: '<path d="M12 3l9 9-9 9-9-9z" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="12" cy="12" r="1.8" fill="currentColor"/>',
    square: '<rect x="4" y="4" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"/><rect x="9" y="9" width="6" height="6" fill="none" stroke="currentColor" stroke-width="2"/>',
  };
  return h("span", { class: "glyph", html: `<svg viewBox="0 0 24 24" width="1em" height="1em" aria-hidden="true">${paths[kind]}</svg>` });
}

function section(id, title, note, ...body) {
  return h("section", { class: "board-section", id },
    h("header", { class: "section-head" }, h("h2", {}, title), note ? h("p", { class: "section-note" }, note) : null),
    ...body);
}

// ---------------------------------------------------------------------------------------------------------------- direction tab

function buildDirection(dir) {
  const d = DATA[dir];
  const root = document.getElementById("tab-" + dir);
  const anchors = [["palette", "Palette"], ["type", "Type"], ["components", "Components"], ["hud", "HUD"], ["blocks", "Block kit"],
                   ["damage", "Damage"], ["vfx", "VFX"], ["space", "Space"], ["mood", "Mood board"]];
  root.append(
    h("div", { class: "dir-hero" },
      h("div", { class: "dir-hero-text" },
        h("span", { class: "dir-letter" }, "Direction " + d.letter + " · DESIGN-SYSTEM §" + d.section),
        h("h2", { class: "dir-name" }, d.name),
        h("p", { class: "dir-tagline" }, d.tagline)),
      h("img", { class: "dir-hero-img", src: `img/blocks/${dir}/block_hull_005.png`, alt: `${d.name} hull patch close-up` })),
    h("nav", { class: "dir-nav" }, anchors.map(([id, label]) => h("a", { href: `#${dir}-${id}` }, label))),
    buildPalette(dir), buildType(dir), buildComponents(dir), buildHud(dir), buildBlocks(dir), buildDamage(dir), buildVfx(dir), buildSpace(dir), buildMood(dir));
}

function swatch(name, value, sub, badges) {
  return h("div", { class: "swatch" },
    h("div", { class: "swatch-chip", style: `background:${value}` }),
    h("div", { class: "swatch-meta" },
      h("span", { class: "swatch-name" }, name),
      h("code", { class: "swatch-hex" }, value),
      sub ? h("span", { class: "swatch-sub" }, sub) : null,
      badges ? h("span", { class: "swatch-badges" }, badges) : null));
}

function badge(ratio, min) {
  const pass = ratio >= min;
  return h("span", { class: "badge " + (pass ? "pass" : "fail"), title: `contrast ${ratio.toFixed(2)}:1 (needs ${min}:1)` }, `${ratio.toFixed(1)}:1`);
}

function buildPalette(dir) {
  const d = DATA[dir];
  const scope = document.getElementById("tab-" + dir);
  const bg = parseColor(tokenValue(scope, "--color-bg"));
  const s1 = parseColor(tokenValue(scope, "--color-surface-1"));
  const tokens = h("div", { class: "swatch-grid" });
  for (const t of COLOR_TOKENS) {
    const value = tokenValue(scope, "--color-" + t);
    const rgb = parseColor(value);
    let badges = null;
    if (rgb && TEXT_ON.includes(t)) {
      const min = t === "border-strong" ? 3 : 4.5;
      badges = [h("span", { class: "badge-label" }, "bg"), badge(contrast(over(rgb, bg), bg), min), h("span", { class: "badge-label" }, "s1"), badge(contrast(over(rgb, s1), s1), min)];
    } else if (rgb && t === "text-on-accent") {
      const acc = parseColor(tokenValue(scope, "--color-accent"));
      badges = [h("span", { class: "badge-label" }, "on accent"), badge(contrast(rgb, acc), 4.5)];
    }
    tokens.append(swatch("--color-" + t, value, null, badges));
  }
  const factions = h("div", { class: "swatch-grid compact" },
    FACTION_TOKENS.map(f => swatch("--color-faction-" + f, tokenValue(scope, "--color-faction-" + f))));
  const hull = h("div", { class: "swatch-grid compact" }, d.hull.map(([n, v, u]) => swatch(n, v, u)));
  const fn = h("div", { class: "swatch-grid compact" }, d.functions.map(([n, v, e]) => swatch(n, v, "emissive " + e)));
  const team = h("table", { class: "team-table" },
    h("thead", {}, h("tr", {}, h("th", {}, "Faction (§" + d.section + ".3)"), h("th", {}, "Primary 17"), h("th", {}, "Secondary 18"), h("th", {}, "Accent 19"))),
    h("tbody", {}, d.team.map(([n, p, s, a]) => h("tr", {}, h("td", {}, n), [p, s, a].map(v => h("td", {}, h("span", { class: "mini-chip", style: `background:${v}` }), h("code", {}, v)))))));
  return section(`${dir}-palette`, "Palette",
    "UI tokens are read from the generated token sheet (equal to the .uss). Badges: WCAG contrast on --color-bg and --color-surface-1 (T1 4.5:1; border-strong T2 3:1).",
    h("h3", {}, "UI colour tokens"), tokens,
    h("h3", {}, "Faction UI tokens"), factions,
    h("div", { class: "two-col" },
      h("div", {}, h("h3", {}, "Hull neutrals (albedo)"), hull),
      h("div", {}, h("h3", {}, "Function colours"), fn)),
    h("h3", {}, "Team colour slots"), team);
}

function buildType(dir) {
  const d = DATA[dir];
  const scope = document.getElementById("tab-" + dir);
  const rows = ["xxl", "xl", "l", "m", "s", "xs"].map(size => {
    const display = d.displaySizes.includes(size);
    const px = tokenValue(scope, "--text-" + size);
    return h("div", { class: "type-row" },
      h("div", { class: "type-meta" }, h("code", {}, "--text-" + size), h("span", {}, px + " · " + (display ? "display" : "body"))),
      h("div", { class: "type-sample " + (display ? "font-display" : "font-body"), style: `font-size:var(--text-${size})` },
        display ? (size === "xxl" ? "Kestrel Reach" : size === "xl" ? "Cargo manifest" : "Weapon group 2") : "The relay hums as the drydock clamps release the hull. 0123456789"));
  });
  const extra = [
    h("div", { class: "type-row" }, h("div", { class: "type-meta" }, h("code", {}, "--font-body-strong"), h("span", {}, "--text-m")),
      h("div", { class: "type-sample font-body-strong", style: "font-size:var(--text-m)" }, "Hull integrity falling — reroute power to the shield emitter.")),
    h("div", { class: "type-row" }, h("div", { class: "type-meta" }, h("code", {}, "--font-mono"), h("span", {}, "--text-s")),
      h("div", { class: "type-sample font-mono", style: "font-size:var(--text-s)" }, "INV-01  THR-S ×4  12 450 cr  98.6 %  −4.2 m/s")),
  ];
  return section(`${dir}-type`, "Type", `Display ${d.fonts.display}; body ${d.fonts.body}; mono ${d.fonts.mono}. All SIL OFL 1.1.`,
    h("div", { class: "type-specimen" }, rows, extra));
}

function bar(kind, pct, label) {
  const segs = h("div", { class: "bar-track" });
  for (let i = 0; i < 10; i++) {
    const filled = i < Math.round(pct / 10);
    const low = pct < 20 && filled;
    segs.append(h("span", { class: "bar-seg" + (filled ? " on" : "") + (low ? " low" : "") }));
  }
  return h("div", { class: `bar bar-${kind}` + (pct < 30 ? " is-low" : ""), style: `--pct:${pct}%` },
    h("span", { class: "bar-label" }, label), segs, h("span", { class: "bar-fill" }), h("span", { class: "bar-value" }, pct + " %"));
}

function buildComponents(dir) {
  const btn = (label, extra = "", attrs = {}) => h("button", { class: "ui-btn " + extra, type: "button", ...attrs }, label);
  const buttons = h("div", { class: "comp-row" },
    h("figure", {}, btn("Default"), h("figcaption", {}, "default")),
    h("figure", {}, btn("Hover", "is-hover"), h("figcaption", {}, "hover")),
    h("figure", {}, btn("Pressed", "is-pressed"), h("figcaption", {}, "pressed")),
    h("figure", {}, btn("Disabled", "", { disabled: true }), h("figcaption", {}, "disabled")),
    h("figure", {}, btn("Launch", "primary"), h("figcaption", {}, "primary")),
    h("figure", {}, btn("Undock", "size-l primary"), h("figcaption", {}, "primary, size l")));
  const tabs = h("div", { class: "ui-tabs", role: "tablist" },
    ["Cargo", "Modules", "Crew"].map((t, i) => h("button", { class: "ui-tab" + (i === 0 ? " is-active" : ""), type: "button" }, t)));
  const panel = (title, code, active) => h("div", { class: "ui-panel" + (active ? " is-active" : "") },
    h("div", { class: "ui-panel-head" }, h("span", { class: "ui-panel-title" }, title), h("span", { class: "ui-panel-code" }, code)),
    h("div", { class: "ui-panel-body" }, "Fuel 62 % · Range 14 ly · 3 jumps queued."));
  const slots = h("div", { class: "ui-slots" },
    h("div", { class: "ui-slot", title: "empty" }),
    h("div", { class: "ui-slot is-filled", title: "filled" }, glyph("chevron"), h("span", { class: "ui-slot-count" }, "4")),
    h("div", { class: "ui-slot is-filled is-selected", title: "selected" }, glyph("crosshair"), h("span", { class: "ui-slot-count" }, "2")),
    h("div", { class: "ui-slot is-filled", title: "filled" }, glyph("bolt")),
    h("div", { class: "ui-slot is-filled", title: "filled" }, glyph("ring")));
  const tooltip = h("div", { class: "ui-tooltip" },
    h("div", { class: "ui-tooltip-title" }, "Plasma turret, small"),
    h("div", { class: "ui-tooltip-body" }, "Fires plasma bolts toward +Z. 30 kg per cell. Function HP 200."),
    h("div", { class: "ui-tooltip-keys" }, h("kbd", {}, "R"), " rotate  ", h("kbd", {}, "F"), " mirror"));
  const toasts = h("div", { class: "ui-toasts" },
    [["positive", "Docking clamps released."], ["warning", "Heat at 85 % — weapons throttled."], ["danger", "Hull breach on deck 3."], ["info", "NORA: new contract available."]]
      .map(([k, text]) => h("div", { class: "ui-toast toast-" + k }, glyph(k), h("span", {}, text))));
  const bars = h("div", { class: "ui-bars" },
    [100, 50, 15].map(p => h("div", { class: "ui-bar-set" }, bar("hull", p, "Hull"), bar("shield", p, "Shield"), bar("energy", p, "Energy"))));
  const dialog = h("ol", { class: "ui-choices" },
    ["We came for the salvage, not a fight.", "Tell me about the beacon.", "[Leave] Undock and burn for the lane."].map((t, i) =>
      h("li", { class: "ui-choice" + (i === 1 ? " is-focused" : "") }, h("span", { class: "ui-choice-key" }, `[${i + 1}]`), h("span", {}, t))));
  const field = h("label", { class: "ui-field" }, h("span", { class: "ui-field-label" }, "Blueprint name"), h("input", { type: "text", value: "Kestrel Mk II", spellcheck: "false" }));
  const toggle = h("label", { class: "ui-toggle" }, h("input", { type: "checkbox", checked: true }), h("span", { class: "ui-toggle-track" }), h("span", {}, "Mirror placement"));
  const slider = h("label", { class: "ui-slider" }, h("span", {}, "Throttle"), h("input", { type: "range", min: "0", max: "100", value: "65" }));
  const fglyphs = h("div", { class: "fn-glyphs" },
    [["chevron", "Propulsion"], ["crosshair", "Weapon"], ["bolt", "Power"], ["ring", "Shield"], ["diamond", "Core"], ["square", "Utility"]].map(([k, n]) =>
      h("figure", {}, glyph(k), h("figcaption", {}, n))));
  return section(`${dir}-components`, "Components", DATA[dir].ui + " " + DATA[dir].motion + " Live: hover, press and focus them.",
    h("div", { class: "comp-grid" },
      h("div", { class: "comp-cell wide" }, h("h3", {}, "Buttons"), buttons),
      h("div", { class: "comp-cell" }, h("h3", {}, "Tabs"), tabs),
      h("div", { class: "comp-cell" }, h("h3", {}, "Panel with header"), h("div", { class: "comp-row" }, panel("Navigation", "NAV-02", true), panel("Inventory", "INV-01", false))),
      h("div", { class: "comp-cell" }, h("h3", {}, "Inventory slots"), slots, h("p", { class: "comp-legend" }, "empty · filled · selected · filled · filled")),
      h("div", { class: "comp-cell" }, h("h3", {}, "Tooltip"), tooltip),
      h("div", { class: "comp-cell wide" }, h("h3", {}, "Notice toasts"), toasts),
      h("div", { class: "comp-cell wide" }, h("h3", {}, "Hull, shield and energy at 100 / 50 / 15 %"), bars),
      h("div", { class: "comp-cell" }, h("h3", {}, "Dialog choices"), dialog),
      h("div", { class: "comp-cell" }, h("h3", {}, "Text field, toggle, slider"), field, toggle, slider),
      h("div", { class: "comp-cell" }, h("h3", {}, "Function glyphs (§10.6)"), fglyphs)));
}

function buildHud(dir) {
  const hud = h("div", { class: "hud-frame" });
  const stage = h("div", { class: "hud-stage", style: `background-image:url("img/blocks/${dir}/block_hull_005.png")` });
  const zone = (id, x, y, w, hh, ...content) => h("div", { class: "hud-zone zone-" + id.toLowerCase(), "data-zone": id, style: `left:${x}px;top:${y}px;width:${w}px;height:${hh}px` },
    h("span", { class: "hud-zone-tag" }, `${id} ${x},${y} ${w}×${hh}`), ...content);
  const z = Object.fromEntries(ZONES.map(r => [r[0], r]));
  const pulp = dir === "pulp-horizon", noct = dir === "nocturne";
  const radar = h("div", { class: "hud-radar" + (dir === "drydock" ? " square" : " round") },
    h("span", { class: "ring r25" }), h("span", { class: "ring r50" }), h("span", { class: "ring r100" }),
    h("span", { class: "blip ally", style: "left:38%;top:44%" }), h("span", { class: "blip enemy", style: "left:70%;top:28%" }), h("span", { class: "blip enemy", style: "left:63%;top:66%" }),
    h("span", { class: "blip self" }));
  const status = noct
    ? h("div", { class: "hud-status-numbers" }, h("div", {}, h("b", {}, "HULL"), " 82 %"), h("div", {}, h("b", {}, "SHIELD"), " 47 %"), h("div", {}, h("b", {}, "ENERGY"), " 15 %"), h("div", {}, h("b", {}, "SPD"), " 128 m/s · HEAT 41 %"))
    : h("div", { class: "hud-status" }, bar("hull", 80, "Hull"), bar("shield", 50, "Shield"), bar("energy", 15, "Energy"), h("div", { class: "hud-speed" }, "128 m/s · heat 41 %"));
  const weapons = h("div", { class: "hud-weapons" }, [1, 2, 3, 4, 5].map(n => h("div", { class: "hud-wg" + (n === 1 ? " is-active" : "") + (n === 4 ? " is-cooling" : "") },
    h("span", { class: "hud-wg-n" }, String(n)), h("span", { class: "hud-wg-name" }, ["PLASMA", "RAIL", "FLAK", "MISSILE", "EMP"][n - 1]))));
  stage.append(
    zone(...z["Z-TL"], h("div", { class: "hud-location" }, h("div", { class: "hud-system" }, "KESTREL REACH"), h("div", { class: "hud-sub" }, "Outer belt · 0.8 AU"),
      h("ul", { class: "hud-objectives" }, h("li", {}, "Salvage the derelict hauler"), h("li", {}, "Return the beacon to NORA")))),
    zone(...z["Z-TC"], h("div", { class: "ui-toast toast-warning" }, glyph("warning"), h("span", {}, "Raider wing inbound — 3 contacts."))),
    zone(...z["Z-TR"], radar),
    zone(...z["Z-L"], h("div", { class: "ui-panel is-active hud-comms" },
      h("div", { class: "ui-panel-head" }, h("span", { class: "ui-panel-title" }, "Comms · NORA"), h("span", { class: "ui-panel-code" }, "COM-01")),
      h("div", { class: "ui-panel-body" }, h("div", { class: "nora-avatar" }), h("p", {}, "That hauler went dark eleven days ago. Its beacon is still singing."),
        h("ol", { class: "ui-choices" }, h("li", { class: "ui-choice is-focused" }, h("span", { class: "ui-choice-key" }, "[1]"), h("span", {}, "Singing how?")),
          h("li", { class: "ui-choice" }, h("span", { class: "ui-choice-key" }, "[2]"), h("span", {}, "Mark it and close the channel.")))))),
    zone(...z["Z-C"], h("div", { class: "hud-reticle" }, glyph("crosshair")), h("div", { class: "hud-marker enemy", style: "left:600px;top:120px" }, "RAIDER 1.2 km"),
      h("div", { class: "hud-marker ally", style: "left:120px;top:360px" }, "ESCORT 640 m"), h("div", { class: "hud-prompt", style: "top:540px" }, h("kbd", {}, "E"), " Dock with the hauler")),
    zone(...z["Z-R"], h("div", { class: "ui-panel hud-target" },
      h("div", { class: "ui-panel-head" }, h("span", { class: "ui-panel-title" }, "Target"), h("span", { class: "ui-panel-code" }, "TGT-03")),
      h("div", { class: "ui-panel-body" }, h("div", { class: "hud-target-name" }, "Raider cutter"), h("div", { class: "hud-sub" }, "Scavengers · 1.2 km · closing"),
        bar("hull", 50, "Hull"), bar("shield", 15, "Shield")))),
    pulp ? null : zone(...z["Z-BL"], status),
    pulp ? null : zone(...z["Z-BC"], weapons),
    zone(...z["Z-BR"], h("div", { class: "hud-cargo" }, h("span", {}, "CARGO 34 / 60 t"), h("span", {}, "12 450 cr"), h("span", {}, "FUEL 62 %"))));
  if (pulp) {
    // §7.8: Z-BL and Z-BC merge into one dashboard strip 112 px tall at the bottom.
    stage.append(h("div", { class: "hud-zone hud-dashboard", "data-zone": "Z-BL+Z-BC", style: "left:32px;top:936px;width:1248px;height:112px" },
      h("span", { class: "hud-zone-tag" }, "dashboard 32,936 1248×112 (§7.8)"),
      h("div", { class: "dash-card" }, bar("hull", 80, "Hull")), h("div", { class: "dash-card" }, bar("shield", 50, "Shield")), h("div", { class: "dash-card" }, bar("energy", 15, "Energy")), weapons));
  }
  if (noct) {
    // §6.8: hull and shield arcs around the player ship (radius 150 px, 6 px thick), energy a thin arc below.
    stage.append(h("div", { class: "hud-arcs", html:
      '<svg viewBox="0 0 340 340" width="340" height="340" aria-hidden="true">' +
      '<path d="M101 300 A150 150 0 0 1 101 40" fill="none" stroke="var(--color-hull)" stroke-width="6" stroke-opacity="0.25"/>' +
      '<path d="M101 300 A150 150 0 0 1 32 160" fill="none" stroke="var(--color-hull)" stroke-width="6"/>' +
      '<path d="M239 40 A150 150 0 0 1 239 300" fill="none" stroke="var(--color-shield)" stroke-width="6" stroke-opacity="0.25"/>' +
      '<path d="M239 40 A150 150 0 0 1 308 160" fill="none" stroke="var(--color-shield)" stroke-width="6"/>' +
      '<path d="M120 318 A150 150 0 0 0 220 318" fill="none" stroke="var(--color-energy)" stroke-width="2"/></svg>' }));
  }
  hud.append(stage);
  const toggleZones = h("label", { class: "chrome-check" }, h("input", { type: "checkbox", onchange: e => hud.classList.toggle("show-zones", e.target.checked) }), " Show zone rectangles (§10.4.2, 1:1 in a 1920 × 1080 frame)");
  return section(`${dir}-hud`, "HUD mock", "A 1920 × 1080 frame scaled to fit, laid out on the shared zones with this direction's variant.", toggleZones, hud);
}

function buildBlocks(dir) {
  const grid = h("div", { class: "block-grid" }, BLOCKS.map(([id, label]) => h("figure", { class: "block-card" },
    h("img", { src: `img/blocks/${dir}/${id}_005.png`, alt: label + " close-up", loading: "lazy" }),
    h("div", { class: "block-true" }, ["050", "150", "300"].map(dist => h("div", { class: "block-true-cell" },
      h("img", { class: "pixelated", src: `img/blocks/${dir}/${id}_${dist}.png`, alt: `${label} at ${dist} m`, loading: "lazy", "data-scale": dist === "050" ? 2 : dist === "150" ? 4 : 6 }),
      h("span", {}, `${parseInt(dist, 10)} m`)))),
    h("figcaption", {}, label))));
  return section(`${dir}-blocks`, "Block kit (ART-004)",
    "Close-up at 148 px per cell (5 m), then each block at its true pixel size at 50, 150 and 300 m (14.8 / 4.9 / 2.5 px per cell), enlarged with hard pixels. Procedural layers; not final art.",
    grid, h("details", { class: "composite" }, h("summary", {}, "Full composite (1920 × 1080)"), h("img", { src: `img/blocks/${dir}/composite.png`, alt: "composite", loading: "lazy" })));
}

function buildDamage(dir) {
  return section(`${dir}-damage`, "Damage strip", "Stages 0 → 3 on the hull patch (hull_plain left half, hull_heavy right half): close-up, then at 50 m.",
    h("div", { class: "damage-strips" },
      h("img", { src: `img/blocks/${dir}/damage_strip_005.png`, alt: "damage stages close-up", loading: "lazy" }),
      h("img", { class: "pixelated", src: `img/blocks/${dir}/damage_strip.png`, alt: "damage stages at 50 m", loading: "lazy", "data-scale": 3 })));
}

function buildVfx(dir) {
  const d = DATA[dir];
  const rows = d.vfx.map(([name, colors, seconds, note]) => {
    const pulse = h("span", { class: "vfx-pulse", style: `--c0:${colors[0]};--c1:${colors[colors.length - 1]}`, "data-seconds": seconds });
    return h("div", { class: "vfx-row" },
      h("span", { class: "vfx-name" }, name),
      h("span", { class: "vfx-colors" }, colors.map(c => h("span", { class: "mini-chip", style: `background:${c}`, title: c }))),
      h("span", { class: "vfx-stage" }, pulse),
      h("code", { class: "vfx-time" }, seconds >= 1 ? seconds.toFixed(1) + " s" : Math.round(seconds * 1000) + " ms"),
      h("span", { class: "vfx-note" }, note));
  });
  const dmg = h("div", { class: "swatch-grid compact" }, d.damageTypes.map(([n, v]) => swatch(n, v, "FxEvent.Color")));
  return section(`${dir}-vfx`, "VFX colour and timing", `§${d.section}.7. Each pulse plays at the effect's duration, then rests 0.7 s. Reduced motion stops them.`,
    h("div", { class: "vfx-list" }, rows), h("h3", {}, "Damage-type colours"), dmg);
}

function buildSpace(dir) {
  const d = DATA[dir];
  return section(`${dir}-space`, "Space", d.spaceNote,
    h("div", { class: "swatch-grid compact" }, d.space.map(([n, v, sub]) => swatch(n, v, sub))));
}

function buildMood(dir) {
  return section(`${dir}-mood`, "Mood board (ART-003)", "Public-domain images only; every other reference is a link in the board file.",
    h("div", { class: "mood-grid" }, MOOD[dir].map(([file, caption]) => h("figure", {},
      h("img", { src: `../moodboards/img/${dir}/${file}.jpg`, alt: caption, loading: "lazy" }), h("figcaption", {}, caption)))),
    h("p", { class: "section-note" }, h("a", { href: `../moodboards/${dir}.md` }, `Open the full ${DATA[dir].name} mood board`), " (references, takes, audio playlist, anti-references)."));
}

// ---------------------------------------------------------------------------------------------------------------- compare and scorecard

const CRITERIA = [["Destruction readability at 50–300 m (pillar 1)", 3, [5, 3, 5]], ["Block function legibility (pillar 2)", 3, [4, 5, 5]],
  ["Massive-scale cues (pillar 3)", 2, [5, 4, 3]], ["Mystery mood (pillar 4)", 2, [4, 5, 3]], ["Production cost", 2, [4, 3, 5]],
  ["Technical risk", 2, [5, 3, 2]], ["Uniqueness", 1, [2, 3, 5]]];

function buildCompare() {
  const root = document.getElementById("tab-compare");
  const cols = DIRECTIONS.map(dir => {
    const d = DATA[dir];
    const col = h("div", { class: "compare-col direction-mini", "data-direction": dir },
      h("h3", { class: "compare-name" }, d.name),
      h("img", { src: `img/blocks/${dir}/block_hull_005.png`, alt: d.name + " hull", loading: "lazy" }),
      h("div", { class: "compare-chips" }, ["bg", "surface-1", "surface-2", "accent", "text", "positive", "warning", "danger", "info", "hull", "shield", "energy"].map(t =>
        h("span", { class: "mini-chip", style: `background:var(--color-${t})`, title: "--color-" + t }))),
      h("div", { class: "font-display compare-display" }, d.name),
      h("p", { class: "font-body compare-body" }, d.tagline),
      h("div", { class: "comp-row" }, h("button", { class: "ui-btn", type: "button" }, "Default"), h("button", { class: "ui-btn primary", type: "button" }, "Launch")),
      bar("hull", 50, "Hull"), bar("shield", 15, "Shield"),
      h("div", { class: "ui-toast toast-danger" }, glyph("danger"), h("span", {}, "Hull breach on deck 3.")),
      h("img", { class: "compare-damage", src: `img/blocks/${dir}/damage_strip_005.png`, alt: d.name + " damage", loading: "lazy" }));
    return col;
  });
  root.append(
    h("div", { class: "dir-hero compare-hero" }, h("div", { class: "dir-hero-text" }, h("span", { class: "dir-letter" }, "DESIGN-SYSTEM §8 · §9"),
      h("h2", { class: "dir-name" }, "Compare & score"),
      h("p", { class: "dir-tagline" }, "The author recommends DRYDOCK, with PULP HORIZON as the second finalist (§9). Your scores below are yours; they start from the author's estimates in §8.3."))),
    section("compare-side", "Side by side", "Same components, each in its own tokens.", h("div", { class: "compare-grid" }, cols)),
    buildScorecard());
}

function buildScorecard() {
  const saved = store.get("scorecard", {});
  const totals = DIRECTIONS.map(() => h("td", { class: "score-total" }));
  const inputs = [];
  const recompute = () => {
    DIRECTIONS.forEach((_, di) => {
      let sum = 0;
      CRITERIA.forEach((c, ci) => { sum += c[1] * (parseInt(inputs[ci][di].value, 10) || 0); });
      totals[di].textContent = sum + " / 75";
    });
    persist();
  };
  const table = h("table", { class: "score-table" },
    h("thead", {}, h("tr", {}, h("th", {}, "Criterion"), h("th", {}, "Weight"), DIRECTIONS.map(d => h("th", {}, DATA[d].name)))),
    h("tbody", {}, CRITERIA.map(([name, weight, author], ci) => {
      inputs[ci] = DIRECTIONS.map((dir, di) => h("input", { type: "number", min: "1", max: "5", step: "1", value: String((saved.scores && saved.scores[ci] && saved.scores[ci][di]) || author[di]), oninput: recompute, "aria-label": `${name}, ${DATA[dir].name}` }));
      return h("tr", {}, h("td", {}, name), h("td", { class: "num" }, String(weight)), inputs[ci].map(i => h("td", { class: "num" }, i)));
    }), h("tr", {}, h("td", {}, h("b", {}, "Weighted total")), h("td", {}), totals)));

  const field = (id, label, type = "text", attrs = {}) => {
    const el = h(type === "textarea" ? "textarea" : "input", { id: "sc-" + id, type: type === "textarea" ? null : type, rows: type === "textarea" ? "3" : null, oninput: persist, onchange: persist, ...attrs });
    if (saved[id] !== undefined) { if (type === "checkbox") el.checked = !!saved[id]; else el.value = saved[id]; }
    return h("label", { class: "sc-field" + (type === "checkbox" ? " inline" : "") }, type === "checkbox" ? el : null, h("span", {}, label), type === "checkbox" ? null : el);
  };
  const form = h("form", { class: "scorecard", onsubmit: e => e.preventDefault() },
    h("fieldset", {}, h("legend", {}, "SHORTLIST (ART-006)"),
      h("div", { class: "sc-row" }, field("f-drydock", "DRYDOCK", "checkbox"), field("f-nocturne", "NOCTURNE", "checkbox"), field("f-pulp", "PULP HORIZON", "checkbox"), field("f-other", "other", "text", { placeholder: "a fourth direction…" })),
      field("shortlist-date", "Date", "date")),
    h("fieldset", {}, h("legend", {}, "APPROVAL (ART-013)"),
      field("chosen", "Chosen direction"),
      field("borrowed", "Elements borrowed from other directions (section numbers, e.g. “§6.7 shield ripple”)", "textarea"),
      field("change1", "Change 1"), field("change2", "Change 2"), field("change3", "Change 3"),
      h("div", { class: "sc-row" }, field("font-display", "Font, display"), field("font-body", "Font, body"), field("font-mono", "Font, mono")),
      field("approved-on", "Approved on", "date")),
    h("fieldset", {}, h("legend", {}, "Notes per direction"), DIRECTIONS.map(d => field("notes-" + d, DATA[d].name, "textarea"))));
  const output = h("textarea", { class: "sc-output", readonly: true, rows: "18", "aria-label": "Markdown output" });
  const status = h("span", { class: "sc-status", role: "status" });
  const copy = h("button", { class: "chrome-button", type: "button", onclick: async () => {
    output.value = markdown();
    let ok = false;
    try { if (navigator.clipboard && window.isSecureContext !== false) { await navigator.clipboard.writeText(output.value); ok = true; } } catch (e) { ok = false; }
    if (!ok) { output.focus(); output.select(); try { ok = document.execCommand("copy"); } catch (e) { ok = false; } }
    status.textContent = ok ? "Copied." : "Select the text below and copy it.";
  } }, "Copy as Markdown");

  function values() {
    const v = {};
    for (const el of form.querySelectorAll("input, textarea")) {
      if (!el.id) continue;
      v[el.id.slice(3)] = el.type === "checkbox" ? el.checked : el.value;
    }
    v.scores = inputs.map(row => row.map(i => parseInt(i.value, 10) || 0));
    return v;
  }
  function persist() { if (inputs.length === CRITERIA.length) store.set("scorecard", values()); }
  function blank(s, n) { return s && s.trim() ? s.trim() : "_".repeat(n); }
  function markdown() {
    const v = values();
    const box = b => (b ? "[x]" : "[ ]");
    const lines = [
      "```",
      "SHORTLIST (ART-006)",
      `  Finalists:      ${box(v["f-drydock"])} DRYDOCK   ${box(v["f-nocturne"])} NOCTURNE   ${box(v["f-pulp"])} PULP HORIZON   ${box(!!(v["f-other"] || "").trim())} other: ${blank(v["f-other"], 10)}`,
      `  Date:           ${blank(v["shortlist-date"], 10)}`,
      "",
      "APPROVAL (ART-013)",
      `  Chosen direction:                         ${blank(v.chosen, 22)}`,
      "  Elements borrowed from other directions   (section numbers, e.g. \"§6.7 shield ripple\"):",
      `    ${blank(v.borrowed, 58).replace(/\n/g, "\n    ")}`,
      "  Changes requested (numbered; each becomes a Decision log row in §12):",
      `    1. ${blank(v.change1, 54)}`,
      `    2. ${blank(v.change2, 54)}`,
      `    3. ${blank(v.change3, 54)}`,
      `  Fonts approved:  display ${blank(v["font-display"], 14)}  body ${blank(v["font-body"], 14)}  mono ${blank(v["font-mono"], 14)}`,
      `  Approved on:     ${blank(v["approved-on"], 10)}`,
      "  Signature:       Frank ______________________",
      "```",
      "",
      "| Criterion | Weight | " + DIRECTIONS.map(d => DATA[d].name).join(" | ") + " |",
      "|---|---|" + DIRECTIONS.map(() => "---").join("|") + "|",
      ...CRITERIA.map(([name, w], ci) => `| ${name} | ${w} | ` + v.scores[ci].join(" | ") + " |"),
      "| **Weighted total (max 75)** | | " + DIRECTIONS.map((_, di) => "**" + CRITERIA.reduce((s, c, ci) => s + c[1] * v.scores[ci][di], 0) + "**").join(" | ") + " |",
    ];
    const notes = DIRECTIONS.filter(d => (v["notes-" + d] || "").trim()).map(d => `- **${DATA[d].name}:** ${v["notes-" + d].trim().replace(/\n/g, " ")}`);
    if (notes.length) lines.push("", "Notes:", ...notes);
    return lines.join("\n");
  }
  setTimeout(recompute, 0);
  return section("compare-score", "Scorecard", "Fills the DESIGN-SYSTEM §1.3 approval block. Saved in this browser only (localStorage); “Copy as Markdown” gives the block to paste into the ledger or a review file.",
    table, form, h("div", { class: "sc-actions" }, copy, status), output);
}

// ---------------------------------------------------------------------------------------------------------------- global behaviour

function selectTab(tab) {
  for (const b of document.querySelectorAll(".chrome-tabs [role=tab]")) b.setAttribute("aria-selected", String(b.dataset.tab === tab));
  for (const p of document.querySelectorAll("main > [role=tabpanel]")) p.hidden = p.id !== "tab-" + tab;
  store.set("tab", tab);
}

function applyCvd(kind) {
  document.getElementById("page").style.filter = kind === "none" ? "" : `url(#cvd-${kind})`;
  store.set("cvd", kind);
}

let vfxAnimations = [];
function startVfx() {
  stopVfx();
  if (document.documentElement.classList.contains("reduced-motion") || typeof Element.prototype.animate !== "function") return;
  for (const el of document.querySelectorAll(".vfx-pulse")) {
    const d = Math.max(0.05, parseFloat(el.dataset.seconds)), rest = 0.7, total = d + rest, end = d / total;
    vfxAnimations.push(el.animate([
      { opacity: 1, transform: "scaleX(0.05)", background: "var(--c0)", offset: 0 },
      { opacity: 1, transform: "scaleX(1)", background: "var(--c1)", offset: end * 0.999 },
      { opacity: 0, transform: "scaleX(1)", background: "var(--c1)", offset: end },
      { opacity: 0, transform: "scaleX(0.05)", background: "var(--c1)", offset: 1 },
    ], { duration: total * 1000, iterations: Infinity }));
  }
}
function stopVfx() { for (const a of vfxAnimations) a.cancel(); vfxAnimations = []; }

function applyReducedMotion(on) {
  document.documentElement.classList.toggle("reduced-motion", on);
  store.set("reduced-motion", on);
  startVfx();
}

function scalePixelImages() {
  for (const img of document.querySelectorAll("img.pixelated[data-scale]")) {
    const fit = () => { const s = parseFloat(img.dataset.scale); img.style.width = img.naturalWidth * s + "px"; img.style.height = img.naturalHeight * s + "px"; };
    if (img.complete && img.naturalWidth) fit(); else img.addEventListener("load", fit, { once: true });
  }
}

function fitHud() {
  for (const frame of document.querySelectorAll(".hud-frame")) {
    const stage = frame.firstElementChild;
    const width = frame.clientWidth;
    if (!width) continue;
    const s = width / 1920;
    stage.style.transform = `scale(${s})`;
    frame.style.height = Math.round(1080 * s) + "px";
  }
}

async function checkFonts() {
  if (!document.fonts || !document.fonts.ready) return;
  const families = ["Barlow Condensed", "Barlow", "IBM Plex Mono", "Michroma", "Inter", "JetBrains Mono", "Archivo", "Atkinson Hyperlegible", "Space Mono"];
  try {
    await Promise.all(families.map(f => document.fonts.load(`16px "${f}"`)));
    await document.fonts.ready;
  } catch (e) { /* treated as not loaded below */ }
  const loaded = new Set();
  document.fonts.forEach(face => { if (face.status === "loaded") loaded.add(face.family.replace(/["']/g, "")); });
  const missing = families.filter(f => !loaded.has(f));
  document.getElementById("font-banner").hidden = missing.length === 0;
  if (missing.length) document.getElementById("font-banner").textContent =
    `Fonts not loaded — type specimens are not accurate (missing: ${missing.join(", ")}). The board needs fonts.googleapis.com.`;
}

function init() {
  for (const dir of DIRECTIONS) buildDirection(dir);
  buildCompare();
  for (const b of document.querySelectorAll(".chrome-tabs [role=tab]")) b.addEventListener("click", () => { selectTab(b.dataset.tab); fitHud(); });
  const cvd = document.getElementById("cvd");
  cvd.value = store.get("cvd", "none");
  cvd.addEventListener("change", () => applyCvd(cvd.value));
  applyCvd(cvd.value);
  const rm = document.getElementById("reduced-motion");
  const prefers = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  rm.checked = store.get("reduced-motion", prefers);
  rm.addEventListener("change", () => applyReducedMotion(rm.checked));
  applyReducedMotion(rm.checked);
  const tab = store.get("tab", "drydock");
  selectTab(DIRECTIONS.includes(tab) || tab === "compare" ? tab : "drydock");
  scalePixelImages();
  fitHud();
  window.addEventListener("resize", fitHud);
  checkFonts();
}

document.addEventListener("DOMContentLoaded", init);
