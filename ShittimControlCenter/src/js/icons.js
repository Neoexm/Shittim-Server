// Clean stroke icon set. Each entry is the inner markup of an <svg>. Most live
// on a 24×24 grid; the window-caption glyphs use their own 10×10 grid (see VB)
// so they render crisp and centered at the small caption-button size instead of
// shrinking into a corner of a 24-unit box.
const P = {
  dashboard: '<rect x="3" y="3" width="7" height="9" rx="1.5"/><rect x="14" y="3" width="7" height="5" rx="1.5"/><rect x="14" y="12" width="7" height="9" rx="1.5"/><rect x="3" y="16" width="7" height="5" rx="1.5"/>',
  server: '<rect x="3" y="4" width="18" height="7" rx="2"/><rect x="3" y="13" width="18" height="7" rx="2"/><circle cx="7" cy="7.5" r="0.9" fill="currentColor" stroke="none"/><circle cx="7" cy="16.5" r="0.9" fill="currentColor" stroke="none"/>',
  config: '<path d="M4 6h10"/><path d="M18 6h2"/><circle cx="16" cy="6" r="2.2"/><path d="M4 12h2"/><path d="M10 12h10"/><circle cx="8" cy="12" r="2.2"/><path d="M4 18h10"/><path d="M18 18h2"/><circle cx="16" cy="18" r="2.2"/>',
  users: '<circle cx="9" cy="8" r="3.2"/><path d="M3.5 19a5.5 5.5 0 0 1 11 0"/><path d="M16 5.2a3 3 0 0 1 0 5.8"/><path d="M17.5 19a5.2 5.2 0 0 0-3-4.7"/>',
  inventory: '<path d="M3 8l9-4 9 4-9 4-9-4z"/><path d="M3 8v8l9 4 9-4V8"/><path d="M12 12v8"/>',
  mail: '<rect x="3" y="5" width="18" height="14" rx="2.2"/><path d="M3.5 7l8.5 6 8.5-6"/>',
  events: '<rect x="3" y="4.5" width="18" height="16" rx="2.2"/><path d="M3 9h18"/><path d="M8 2.5v4"/><path d="M16 2.5v4"/><path d="M8 13l2.5 2.5L16 12"/>',
  gacha: '<path d="M12 3l2.1 4.6L19 8.3l-3.5 3.4.9 4.9L12 14.3 7.6 16.6l.9-4.9L5 8.3l4.9-.7L12 3z"/>',
  rates: '<path d="M19 5L5 19"/><circle cx="7.5" cy="7.5" r="2.5"/><circle cx="16.5" cy="16.5" r="2.5"/>',
  play: '<path d="M7 5.5v13l11-6.5-11-6.5z" fill="currentColor" stroke="none"/>',
  stop: '<rect x="6" y="6" width="12" height="12" rx="2" fill="currentColor" stroke="none"/>',
  refresh: '<path d="M20 11a8 8 0 0 0-14-4.5L4 8"/><path d="M4 4v4h4"/><path d="M4 13a8 8 0 0 0 14 4.5L20 16"/><path d="M20 20v-4h-4"/>',
  download: '<path d="M12 4v10"/><path d="M8 11l4 4 4-4"/><path d="M5 19.5h14"/>',
  plus: '<path d="M12 5v14"/><path d="M5 12h14"/>',
  minus: '<path d="M5 12h14"/>',
  trash: '<path d="M4 7h16"/><path d="M9 7V4.8A1 1 0 0 1 10 4h4a1 1 0 0 1 1 .8V7"/><path d="M6 7l1 12.2A1.8 1.8 0 0 0 8.8 21h6.4A1.8 1.8 0 0 0 17 19.2L18 7"/>',
  search: '<circle cx="11" cy="11" r="6.5"/><path d="M20 20l-3.8-3.8"/>',
  save: '<path d="M5 4h11l3 3v13H5z"/><path d="M8 4v5h7V4"/><rect x="8" y="13" width="8" height="6"/>',
  folder: '<path d="M3 7a2 2 0 0 1 2-2h4l2 2.5h8a1.5 1.5 0 0 1 1.5 1.5v8A2 2 0 0 1 18.5 19H5a2 2 0 0 1-2-2V7z"/>',
  external: '<path d="M14 5h5v5"/><path d="M19 5l-8 8"/><path d="M18 14v4a1.5 1.5 0 0 1-1.5 1.5H6A1.5 1.5 0 0 1 4.5 18V8A1.5 1.5 0 0 1 6 6.5h4"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
  x: '<path d="M6 6l12 12"/><path d="M18 6L6 18"/>',
  send: '<path d="M21 4L3 11l7 2.5L13 21l8-17z"/><path d="M10 13.5L21 4"/>',
  coin: '<ellipse cx="12" cy="7" rx="7" ry="3"/><path d="M5 7v6c0 1.7 3.1 3 7 3s7-1.3 7-3V7"/><path d="M5 13c0 1.7 3.1 3 7 3s7-1.3 7-3"/>',
  star: '<path d="M12 4l2.3 4.7 5.2.8-3.8 3.7.9 5.2L12 16.6 7.4 18.1l.9-5.2L4.5 9.5l5.2-.8L12 4z" fill="currentColor" stroke="none"/>',
  bolt: '<path d="M13 3L5 13h5l-1 8 8-11h-5l1-7z" fill="currentColor" stroke="none"/>',
  shield: '<path d="M12 3l7 3v5c0 4.5-3 8-7 10-4-2-7-5.5-7-10V6l7-3z"/>',
  clock: '<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/>',
  edit: '<path d="M5 19h3l9.5-9.5a1.8 1.8 0 0 0 0-2.6l-.4-.4a1.8 1.8 0 0 0-2.6 0L5 16v3z"/><path d="M13.5 6.5l3 3"/>',
  power: '<path d="M12 4v8"/><path d="M7.5 7a7 7 0 1 0 9 0"/>',
  box: '<rect x="4" y="4" width="16" height="16" rx="2.5"/><path d="M4 9h16"/><path d="M9 4v5"/>',
  ticket: '<path d="M4 8a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2 2 2 0 0 0 0 8 2 2 0 0 1-2 2H6a2 2 0 0 1-2-2 2 2 0 0 0 0-8z"/><path d="M14 6v12"/>',
  flask: '<path d="M9 3h6"/><path d="M10 3v6l-5 8.5A1.6 1.6 0 0 0 6.4 20h11.2a1.6 1.6 0 0 0 1.4-2.5L14 9V3"/><path d="M7.5 14h9"/>',
  halo: '<ellipse cx="12" cy="7.5" rx="7" ry="2.6"/>',
  info: '<circle cx="12" cy="12" r="8.5"/><path d="M12 11v5"/><circle cx="12" cy="8" r="1" fill="currentColor" stroke="none"/>',
  copy: '<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V5a1.5 1.5 0 0 0-1.5-1.5H5A1.5 1.5 0 0 0 3.5 5v9.5A1.5 1.5 0 0 0 5 16h3"/>',
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2.5v2.5M12 19v2.5M4.4 4.4l1.8 1.8M17.8 17.8l1.8 1.8M2.5 12H5M19 12h2.5M4.4 19.6l1.8-1.8M17.8 6.2l1.8-1.8"/>',
  moon: '<path d="M20.5 14.8A8.5 8.5 0 1 1 9.2 3.5a6.6 6.6 0 0 0 11.3 11.3z"/>',
  // window-caption glyphs (10×10 grid)
  win_min: '<path d="M1 5h8"/>',
  win_max: '<rect x="1.3" y="1.3" width="7.4" height="7.4" rx="0.8"/>',
  win_close: '<path d="M1.4 1.4l7.2 7.2"/><path d="M8.6 1.4L1.4 8.6"/>',
};

// Per-name viewBox overrides (default 24×24).
const VB = {
  win_min: '0 0 10 10',
  win_max: '0 0 10 10',
  win_close: '0 0 10 10',
};

export function icon(name, cls = 'ico', stroke = 1.85) {
  const inner = P[name] || P.info;
  const vb = VB[name] || '0 0 24 24';
  return `<svg class="${cls}" viewBox="${vb}" fill="none" stroke="currentColor" stroke-width="${stroke}" stroke-linecap="round" stroke-linejoin="round">${inner}</svg>`;
}

// The Schale halo emblem used as the app mark. Brand-constant colors (BA gold +
// cyan) so it reads identically on either theme; bold enough to stay legible at
// the 16px titlebar size and the 38px rail size.
export function haloMark(cls = 'halo') {
  return `<svg class="${cls}" viewBox="0 0 48 48" fill="none">
    <ellipse cx="24" cy="18" rx="15" ry="5.6" stroke="#ffce4d" stroke-width="3.4"/>
    <ellipse cx="24" cy="18" rx="15" ry="5.6" stroke="#fff6da" stroke-width="1" opacity="0.5"/>
    <path d="M12.5 26.5C14 33 18.6 37.5 24 37.5s10-4.5 11.5-11" stroke="#3db8f5" stroke-width="3.3" stroke-linecap="round"/>
    <circle cx="24" cy="18" r="2.7" fill="#3db8f5"/>
  </svg>`;
}
