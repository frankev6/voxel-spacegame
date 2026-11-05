// Tabs, and the Flight configs panel (SPEC-70 DEV-030): every FlightConfig field, editable while the game runs.
// Edits go to the game's live FlightConfig only; config_flight is never written, and leaving Play restores it.
(() => {
  const POLL_MS = 400, SEND_MS = 80;
  const panel = document.getElementById('flight'), resetAll = document.getElementById('flightResetAll');
  let text = { groups: [], fields: {} }, rows = new Map(), built = false;

  // Tabs
  const show = name => {
    document.body.dataset.panel = name;
    try { localStorage.setItem('controlsheet.panel', name); } catch (e) { }
    if (name === 'flight') poll();
  };
  document.querySelectorAll('.tabs button').forEach(b => b.addEventListener('click', () => show(b.dataset.panel)));
  let saved = 'controls';
  try { saved = localStorage.getItem('controlsheet.panel') || 'controls'; } catch (e) { }
  document.body.dataset.panel = saved;

  const el = (tag, cls, content) => { const e = document.createElement(tag); if (cls) e.className = cls; if (content != null) e.textContent = content; return e; };
  const format = (v, f) => f.integer ? String(Math.round(v)) : Math.abs(v) >= 10000 ? Math.round(v).toLocaleString('en-US') : String(+v.toPrecision(4));

  function post(path) {
    return fetch(path, { method: 'POST', headers: { 'X-Control-Sheet': '1' } }).then(() => setTimeout(poll, 60), () => { });
  }

  // A slider position (0..1000) to a value and back; "log" sliders spread wide ranges such as 0.01 .. 1,000,000.
  function sliderScale(f, spec) {
    const [lo, hi, mode] = spec.slider || [f.min, f.max];
    if (mode === 'log' && lo > 0) {
      const a = Math.log(lo), b = Math.log(hi);
      return { to: v => 1000 * (Math.log(Math.max(lo, Math.min(hi, v))) - a) / (b - a), from: p => Math.exp(a + (b - a) * p / 1000) };
    }
    return { to: v => 1000 * (Math.max(lo, Math.min(hi, v)) - lo) / (hi - lo), from: p => lo + (hi - lo) * p / 1000 };
  }

  function buildRow(f) {
    const spec = text.fields[f.name] || {};
    const row = el('div', 'field'), title = el('div', 'title');
    title.append(el('span', 'label', spec.label || f.name), el('span', 'code', f.name));
    row.appendChild(title);
    row.appendChild(el('div', 'what', spec.what || 'No description yet: add one to tools/controlsheet/flight.json.'));
    if (spec.lower || spec.higher) {
      const range = el('div', 'range');
      range.append(el('b', null, 'Lower'), el('span', null, spec.lower || '—'), el('b', null, 'Higher'), el('span', null, spec.higher || '—'));
      row.appendChild(range);
    }
    const edit = el('div', 'edit'), slider = el('input'), number = el('input');
    slider.type = 'range'; slider.min = 0; slider.max = 1000; slider.step = 1;
    number.type = 'number'; number.min = f.min; number.max = f.max; number.step = f.integer ? 1 : 'any';
    edit.append(slider, number, el('span', 'unit', f.unit));
    row.appendChild(edit);
    const foot = el('div', 'foot'), boot = el('span'), reset = el('button', 'button', 'Reset');
    reset.type = 'button';
    const left = el('span'); left.appendChild(boot);
    if (f.aiLags) left.appendChild(el('span', 'tag', ' · AI pilots see it after a ship rebuild'));
    foot.append(left, reset);
    row.appendChild(foot);

    const scale = sliderScale(f, spec);
    let timer = 0, pendingValue = null;
    const send = v => {
      pendingValue = v;
      if (timer) return;
      timer = setTimeout(() => { timer = 0; post('flight?name=' + encodeURIComponent(f.name) + '&value=' + encodeURIComponent(pendingValue)); }, SEND_MS);
    };
    slider.addEventListener('input', () => { let v = scale.from(+slider.value); if (f.integer) v = Math.round(v); number.value = format(v, f); send(v); });
    number.addEventListener('change', () => { const v = parseFloat(number.value); if (isFinite(v)) send(v); });
    reset.addEventListener('click', () => post('flight/reset?name=' + encodeURIComponent(f.name)));

    return {
      row, update(field) {
        const changed = field.value !== field.boot;
        row.classList.toggle('field--changed', changed);
        boot.textContent = (changed ? 'changed · ' : '') + 'boot value ' + format(field.boot, f) + (f.unit ? ' ' + f.unit : '');
        reset.disabled = !changed;
        // Never overwrite what the user is dragging or typing.
        if (document.activeElement !== number) number.value = format(field.value, f);
        if (document.activeElement !== slider) slider.value = scale.to(field.value);
      }
    };
  }

  function build(doc) {
    panel.replaceChildren(); rows.clear();
    panel.appendChild(el('div', 'banner', 'Changes apply to the running game at once and are not saved: Reset, or leaving Play, restores config_flight. ' +
      'To keep a value, copy it into Assets/Game/Content/Config/config_flight.asset.'));
    const byName = new Map(doc.fields.map(f => [f.name, f])), placed = new Set();
    const groups = (text.groups || []).map(g => ({ name: g.name, fields: g.fields.filter(n => byName.has(n)) }));
    const rest = doc.fields.map(f => f.name).filter(n => !groups.some(g => g.fields.includes(n)));
    if (rest.length) groups.push({ name: 'Other', fields: rest });     // new FlightConfig fields show up before they get text
    for (const g of groups) {
      if (!g.fields.length) continue;
      const section = el('section', 'map'), h = el('h2'), grid = el('div', 'fields');
      h.append(el('span', null, g.name), el('span', 'count', g.fields.length + ' values'));
      h.addEventListener('click', () => section.classList.toggle('map--collapsed'));
      section.append(h, grid);
      for (const name of g.fields) { if (placed.has(name)) continue; placed.add(name); const r = buildRow(byName.get(name)); rows.set(name, r); grid.appendChild(r.row); }
      panel.appendChild(section);
    }
    built = true;
  }

  function apply(doc) {
    if (!doc.available) {
      built = false;
      panel.replaceChildren(el('div', 'banner', 'No game world with flight values yet. Start a game or the playground and this panel fills in.'));
      resetAll.disabled = true;
      return;
    }
    if (!built) build(doc);
    let anyChanged = false;
    for (const f of doc.fields) { const r = rows.get(f.name); if (r) r.update(f); if (f.value !== f.boot) anyChanged = true; }
    resetAll.disabled = !anyChanged;
    let error = panel.querySelector('.banner--error');
    if (doc.error) {
      if (!error) { error = el('div', 'banner banner--error'); panel.insertBefore(error, panel.children[1] || null); }
      error.textContent = doc.error;
    } else if (error) error.remove();
  }

  let polling = false;
  async function poll() {
    if (polling || document.body.dataset.panel !== 'flight') return;
    polling = true;
    try {
      const doc = await (await fetch('flight', { cache: 'no-store' })).json();
      if (doc.fields) apply(doc);
    } catch (e) { built = false; }   // the game went away: rebuild when it is back
    polling = false;
  }
  resetAll.addEventListener('click', () => post('flight/reset'));

  fetch('flight.json', { cache: 'no-store' }).then(r => r.ok ? r.json() : null).then(j => { if (j) text = j; }, () => { })
    .finally(() => { poll(); setInterval(poll, POLL_MS); });
})();
