// Custom visuals for the control sheet (SPEC-70 DEV-029).
//
// A visual is a function (slot, cfg) => update. It builds its elements inside `slot` (the space under one control) and
// returns update(frame), called on every poll with
//   frame.action  this control's live state: { pressed, value }  (value: number, [x, y] or undefined)
//   frame.probes  every requested probe value by name (number, bool, string or null)
//   frame.state   the whole /state document
// Attach one to a control in sheet.json:  "Flight/Thrust": { "visual": "sparkline", "visualProbe": "player.speed" }
// Probes named in "visualProbe" are requested from the game automatically.
window.ControlSheet = { visuals: {} };

// The last 120 samples (about 12 s) of a probe, or of the control's own value when no probe is named.
ControlSheet.visuals.sparkline = (slot, cfg) => {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('class', 'spark'); svg.setAttribute('viewBox', '0 0 120 28'); svg.setAttribute('preserveAspectRatio', 'none');
  const line = document.createElementNS(ns, 'polyline');
  svg.appendChild(line); slot.appendChild(svg);
  const samples = [];
  return ({ action, probes }) => {
    let v = cfg.visualProbe ? probes[cfg.visualProbe] : action.value;
    if (Array.isArray(v)) v = Math.hypot(v[0], v[1]);
    if (typeof v === 'boolean') v = v ? 1 : 0;
    samples.push(typeof v === 'number' ? v : 0);
    if (samples.length > 120) samples.shift();
    const max = Math.max(cfg.max ?? 0, ...samples.map(Math.abs)) || 1;
    line.setAttribute('points', samples.map((s, i) => `${i},${(27 - (s / max) * 26).toFixed(1)}`).join(' '));
  };
};
