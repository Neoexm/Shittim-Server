import { icon } from './icons.js';

// ------------------------------------------------------------------ DOM helpers

// Hyperscript: el('div.card', { onclick }, child, child...)
export function el(spec, props = {}, ...children) {
  let tag = 'div', id = null;
  const classes = [];
  spec.replace(/([.#]?[^.#]+)/g, (m) => {
    if (m[0] === '.') classes.push(m.slice(1));
    else if (m[0] === '#') id = m.slice(1);
    else tag = m;
  });
  const node = document.createElement(tag);
  if (id) node.id = id;
  if (classes.length) node.className = classes.join(' ');

  for (const [k, v] of Object.entries(props || {})) {
    if (v == null || v === false) continue;
    if (k === 'class') node.className += ' ' + v;
    else if (k === 'html') node.innerHTML = v;
    else if (k === 'text') node.textContent = v;
    else if (k === 'style' && typeof v === 'object') Object.assign(node.style, v);
    else if (k.startsWith('on') && typeof v === 'function') node.addEventListener(k.slice(2).toLowerCase(), v);
    else if (k === 'dataset') Object.assign(node.dataset, v);
    else if (v === true) node.setAttribute(k, '');
    else node.setAttribute(k, v);
  }
  for (const c of children.flat()) {
    if (c == null || c === false) continue;
    node.appendChild(typeof c === 'string' ? document.createTextNode(c) : c);
  }
  return node;
}

// Parse an HTML string into a single node (or fragment's first element).
export function frag(html) {
  const t = document.createElement('template');
  t.innerHTML = html.trim();
  return t.content.firstElementChild;
}

export function clear(node) { while (node.firstChild) node.removeChild(node.firstChild); return node; }

// ------------------------------------------------------------------ buttons

export function button(label, { variant = '', iconName, onClick, sm, block, disabled } = {}) {
  const cls = ['btn', variant && `btn-${variant}`, sm && 'btn-sm', block && 'btn-block'].filter(Boolean).join(' ');
  const b = frag(`<button class="${cls}">${iconName ? icon(iconName) : ''}<span>${label}</span></button>`);
  if (disabled) b.disabled = true;
  if (onClick) b.addEventListener('click', onClick);
  return b;
}

// ------------------------------------------------------------------ fields

export function field(label, control, hint) {
  return el('div.field', {},
    el('label', {}, label, hint ? el('span.hint', { text: ` — ${hint}` }) : null),
    control);
}

export function input(props = {}) {
  const i = el('input.input', {});
  Object.assign(i, props);
  if (props.value != null) i.value = props.value;
  return i;
}
export function textarea(props = {}) {
  const t = el('textarea.input', {});
  Object.assign(t, props);
  if (props.value != null) t.value = props.value;
  return t;
}
export function select(options, props = {}) {
  const s = el('select.select', {});
  for (const o of options) {
    const opt = document.createElement('option');
    opt.value = o.value;
    opt.textContent = o.label;
    s.appendChild(opt);
  }
  Object.assign(s, props);
  if (props.value != null) s.value = props.value;
  return s;
}
export function toggle(on, onChange) {
  const t = el('div.toggle' + (on ? '.on' : ''), {});
  t.addEventListener('click', () => {
    t.classList.toggle('on');
    onChange && onChange(t.classList.contains('on'));
  });
  return t;
}

// ------------------------------------------------------------------ toast

export function toast(message, kind = '', title) {
  const host = document.getElementById('toasts');
  const ic = kind === 'good' ? 'check' : kind === 'bad' ? 'x' : kind === 'warn' ? 'info' : 'info';
  const t = frag(`<div class="toast ${kind}">${icon(ic)}<div>${title ? `<b>${title}</b><br>` : ''}${escapeHtml(message)}</div></div>`);
  host.appendChild(t);
  setTimeout(() => {
    t.style.transition = 'opacity .25s, transform .25s';
    t.style.opacity = '0';
    t.style.transform = 'translateX(20px)';
    setTimeout(() => t.remove(), 260);
  }, 3400);
}

// ------------------------------------------------------------------ modal

export function modal({ title, body, footer, wide, onClose }) {
  const overlay = document.getElementById('overlay');
  const veil = el('div.modal-veil', {});
  const m = el('div.modal' + (wide ? '.wide' : ''), {});

  const head = frag(`<div class="modal-head"><span class="plus"></span><h3>${escapeHtml(title || '')}</h3><button class="x">✕</button></div>`);
  const close = () => { veil.remove(); onClose && onClose(); };
  head.querySelector('.x').addEventListener('click', close);

  const bodyEl = el('div.modal-body', {});
  if (typeof body === 'string') bodyEl.innerHTML = body;
  else if (body) bodyEl.appendChild(body);

  m.appendChild(head);
  m.appendChild(bodyEl);
  if (footer) {
    const f = el('div.modal-foot', {}, ...(Array.isArray(footer) ? footer : [footer]));
    m.appendChild(f);
  }
  veil.appendChild(m);
  veil.addEventListener('mousedown', (e) => { if (e.target === veil) close(); });
  document.addEventListener('keydown', function esc(e) {
    if (e.key === 'Escape') { close(); document.removeEventListener('keydown', esc); }
  });
  overlay.appendChild(veil);
  return { close, bodyEl };
}

export function confirmDialog({ title = 'Confirm', message, confirmLabel = 'Confirm', danger = false }) {
  return new Promise((resolve) => {
    const yes = button(confirmLabel, { variant: danger ? 'danger' : 'primary', iconName: 'check' });
    const no = button('Cancel', { variant: 'ghost' });
    const ref = modal({
      title,
      body: el('div', { style: { fontSize: '14px', color: 'var(--ink-2)', lineHeight: '1.6' } }, message),
      footer: [no, yes],
      onClose: () => resolve(false),
    });
    yes.addEventListener('click', () => { ref.close(); resolve(true); });
    no.addEventListener('click', () => { ref.close(); resolve(false); });
  });
}

// ------------------------------------------------------------------ picker

// Generic searchable picker backed by an async loader returning [{id,name,sub}].
export function openPicker({ title, loader, onPick }) {
  const list = el('div.picker-list', {});
  const search = input({ placeholder: 'Search by name or ID…', className: 'input picker-search' });
  let timer;

  async function load(q) {
    list.innerHTML = `<div class="empty"><div class="spinner"></div></div>`;
    try {
      const items = await loader(q);
      list.innerHTML = '';
      if (!items.length) { list.innerHTML = `<div class="empty"><b>Nothing found</b></div>`; return; }
      for (const it of items) {
        const row = frag(`<div class="picker-item"><span class="pi-id">${it.id}</span><span class="pi-name">${escapeHtml(it.name)}</span>${it.sub ? `<span class="tag grey">${escapeHtml(it.sub)}</span>` : ''}</div>`);
        row.addEventListener('click', () => { ref.close(); onPick(it); });
        list.appendChild(row);
      }
    } catch (e) {
      list.innerHTML = `<div class="empty"><b>Failed to load</b><span>${escapeHtml(String(e.message || e))}</span></div>`;
    }
  }
  search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => load(search.value.trim()), 220); });

  const ref = modal({ title, wide: true, body: el('div', {}, search, list) });
  load('');
  setTimeout(() => search.focus(), 50);
}

// ------------------------------------------------------------------ format

export function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}
export function num(n) { return Number(n ?? 0).toLocaleString('en-US'); }
export function stars(n) { return '★'.repeat(Math.max(0, Math.min(5, n))) + '☆'.repeat(Math.max(0, 5 - n)); }
export function shortDate(s) {
  if (!s) return '—';
  const d = new Date(s);
  if (isNaN(d)) return String(s);
  return d.toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}
export function relTime(secs) {
  secs = Math.max(0, Math.floor(secs));
  const h = Math.floor(secs / 3600), m = Math.floor((secs % 3600) / 60), s = secs % 60;
  return h ? `${h}h ${m}m` : m ? `${m}m ${s}s` : `${s}s`;
}

// section helpers
export function card(title, { sub, actions, body, tight } = {}) {
  const head = el('div.card-head', {}, el('span.tab-mark', {}),
    el('h3', { text: title }),
    sub ? el('span.sub', { text: sub }) : null,
    el('div.spacer', {}),
    ...(actions || []));
  const c = el('div.card', {}, head);
  if (body) c.appendChild(el('div.card-body' + (tight ? '.tight' : ''), {}, body));
  return c;
}

export function emptyState(text, sub) {
  return frag(`<div class="empty"><svg class="halo" viewBox="0 0 48 48" fill="none"><ellipse cx="24" cy="20" rx="15" ry="5.5" stroke="#3db8f5" stroke-width="3"/></svg><b>${escapeHtml(text)}</b>${sub ? `<span>${escapeHtml(sub)}</span>` : ''}</div>`);
}
