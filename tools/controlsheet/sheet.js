// Control sheet page (SPEC-70 DEV-029). Polls the game's /state about ten times a second and redraws only what changed.
(() => {
  const POLL_MS = 100, RETRY_MS = 1000;
  const $ = id => document.getElementById(id);
  const mapsEl = $('maps');
  let config = { actions: {} }, layout = null, probeQuery = '';
  const cards = new Map();                 // action path -> { card, updates: [fn] }
  const sections = new Map();              // map name -> section element

  const activeOnly = $('activeOnly');
  try { activeOnly.checked = localStorage.getItem('controlsheet.activeOnly') === '1'; } catch (e) { }
  const applyFilter = () => document.body.classList.toggle('active-only', activeOnly.checked);
  activeOnly.addEventListener('change', () => { applyFilter(); try { localStorage.setItem('controlsheet.activeOnly', activeOnly.checked ? '1' : '0'); } catch (e) { } });
  applyFilter();

  const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; };
  // "FirePrimary" -> "Fire primary", "Ability1" -> "Ability 1"
  const human = name => name.replace(/([a-z])([A-Z0-9])/g, '$1 $2').replace(/ ([A-Z])(?=[a-z])/g, (m, c) => ' ' + c.toLowerCase());
  const probeSpec = p => typeof p === 'string' ? { name: p } : p;
  const cfgOf = path => config.actions[path] || {};

  async function loadConfig() {
    try { const r = await fetch('sheet.json', { cache: 'no-store' }); if (r.ok) config = await r.json(); }
    catch (e) { console.warn('sheet.json unreadable; showing controls without notes', e); }
    config.actions = config.actions || {};
    const names = new Set();
    for (const cfg of Object.values(config.actions)) {
      for (const p of cfg.probes || []) names.add(probeSpec(p).name);
      if (cfg.visualProbe) names.add(cfg.visualProbe);
    }
    probeQuery = [...names].join(',');
  }

  function defaultVisual(kind, slot) {
    if (kind === 'vector2') {
      const pad = el('div', 'stick'), dot = el('i'), readout = el('div', 'note', '0, 0'); pad.appendChild(dot); slot.append(pad, readout);
      return ({ action }) => {
        const [x, y] = Array.isArray(action.value) ? action.value : [0, 0];
        dot.style.left = (50 + Math.max(-1, Math.min(1, x)) * 45) + '%';
        dot.style.top = (50 - Math.max(-1, Math.min(1, y)) * 45) + '%';
        readout.textContent = x.toFixed(2) + ', ' + y.toFixed(2);   // pointer actions are pixels, past the pad's ±1
      };
    }
    if (kind === 'axis' || kind === 'value') {
      const bar = el('div', 'axis'), fill = el('i'); bar.appendChild(fill); slot.appendChild(bar);
      return ({ action }) => {
        const v = typeof action.value === 'number' ? Math.max(-1, Math.min(1, action.value)) : 0;
        fill.style.left = (v < 0 ? 50 + v * 50 : 50) + '%';
        fill.style.width = Math.abs(v) * 50 + '%';
      };
    }
    const lamp = el('div', 'lamp'); slot.appendChild(lamp);
    return ({ action }) => lamp.classList.toggle('lamp--on', !!action.pressed);
  }

  function probeRow(spec, slot) {
    const row = el('div', 'probe'), value = el('span', 'value', '—');
    row.append(el('span', 'label', spec.label || spec.name), value);
    let fill = null;
    if (typeof spec.max === 'number') { const meter = el('div', 'meter'); fill = el('i'); meter.appendChild(fill); row.appendChild(meter); }
    slot.appendChild(row);
    return ({ probes }) => {
      const v = probes[spec.name];
      let text = '—';
      if (spec.values && v != null && spec.values[String(v)] != null) text = spec.values[String(v)];
      else if (typeof v === 'number') text = v.toFixed(spec.digits ?? 1) + (spec.unit ? ' ' + spec.unit : '');
      else if (typeof v === 'boolean') text = v ? 'yes' : 'no';
      else if (v != null) text = String(v);
      value.textContent = text;
      if (fill) { const min = spec.min ?? 0; fill.style.width = (typeof v === 'number' ? Math.max(0, Math.min(1, (v - min) / (spec.max - min))) * 100 : 0) + '%'; }
    };
  }

  function buildCard(action) {
    const cfg = cfgOf(action.path);
    const card = el('div', 'card'), head = el('div', 'head'), slot = el('div', 'slot');
    const unbound = !action.binding || action.binding === '?';
    head.append(el('span', 'name', cfg.label || human(action.name)), el('span', unbound ? 'key key--none' : 'key', unbound ? 'unbound' : action.binding));
    card.title = action.path;
    card.append(head, slot);
    const updates = [defaultVisual(action.kind, slot)];
    if (cfg.note) slot.appendChild(el('div', 'note', cfg.note));
    for (const p of cfg.probes || []) updates.push(probeRow(probeSpec(p), slot));
    const custom = cfg.visual && ControlSheet.visuals[cfg.visual];
    if (cfg.visual && !custom) slot.appendChild(el('div', 'note', 'unknown visual "' + cfg.visual + '" (visuals.js)'));
    if (custom) { try { updates.push(custom(slot, cfg)); } catch (e) { console.error(action.path, e); } }
    return { card, updates };
  }

  function render() {
    mapsEl.replaceChildren(); cards.clear(); sections.clear();
    for (const map of layout.maps) {
      const section = el('section', 'map map--idle'), h = el('h2'), grid = el('div', 'cards');
      h.append(el('span', null, map.name), el('span', null, map.actions.length + ' actions'));
      section.append(h, grid);
      for (const action of map.actions) { const c = buildCard(action); grid.appendChild(c.card); cards.set(action.path, c); }
      if (map.actions.length === 0) grid.appendChild(el('div', 'empty', 'no actions'));
      mapsEl.appendChild(section); sections.set(map.name, section);
    }
  }

  function apply(state) {
    $('context').textContent = state.context; $('scheme').textContent = state.scheme;
    const active = new Set(state.active);
    for (const [name, section] of sections) {
      section.classList.toggle('map--active', active.has(name));
      section.classList.toggle('map--idle', !active.has(name));
    }
    const probes = state.probes || {};
    for (const [path, c] of cards) {
      const action = state.actions[path] || { pressed: false };
      c.card.classList.toggle('card--pressed', !!action.pressed);
      const frame = { action, probes, state };
      for (const update of c.updates) { try { update(frame); } catch (e) { } }
    }
  }

  function setLink(live) {
    const link = $('link');
    link.textContent = live ? 'live' : 'game not reachable';
    link.className = 'pill ' + (live ? 'pill--live' : 'pill--down');
  }

  async function poll() {
    let delay = POLL_MS;
    try {
      const r = await fetch('state?probes=' + encodeURIComponent(probeQuery), { cache: 'no-store' });
      const state = await r.json();
      if (!layout || state.layout !== layout.version) {
        layout = await (await fetch('layout', { cache: 'no-store' })).json();
        render();
      }
      apply(state); setLink(true);
    } catch (e) { setLink(false); layout = null; delay = RETRY_MS; }   // the game stopped or reloaded: keep the last sheet, refetch on return
    setTimeout(poll, delay);
  }

  loadConfig().then(poll);
})();
