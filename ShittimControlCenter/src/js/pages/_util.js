import { el, frag, clear, button } from '../ui.js';
import { store, targetAccount } from '../api.js';

// Gate a page body behind "server must be online" / "an account must be
// selected" preconditions, and auto-repaint when those preconditions flip.
export function gate(root, ctx, opts, renderFn) {
  let prevOnline = store.get().online;
  let prevTarget = store.get().targetId;

  function paint() {
    clear(root);
    const s = store.get();
    if (opts.needServer && !s.online) { root.appendChild(offlinePanel()); return; }
    if (opts.needTarget && !targetAccount()) { root.appendChild(noTargetPanel()); return; }
    renderFn(root, s);
  }

  const unsub = store.subscribe((s) => {
    if (s.online !== prevOnline || s.targetId !== prevTarget) {
      prevOnline = s.online;
      prevTarget = s.targetId;
      paint();
    }
  });
  paint();
  return unsub;
}

export function offlinePanel() {
  const go = button('Go to Server Control', { variant: 'primary', iconName: 'server', onClick: () => (location.hash = '#/server') });
  // soft-navigate by triggering hashchange handler is overkill; reuse nav click
  go.addEventListener('click', () => document.querySelector('.nav-item[data-id="server"]')?.click());
  return el('div.card', { style: { maxWidth: '560px', margin: '40px auto' } },
    el('div.card-body', { style: { textAlign: 'center', padding: '36px' } },
      frag(`<svg class="halo" viewBox="0 0 48 48" fill="none" style="width:54px;height:54px;margin-bottom:6px"><ellipse cx="24" cy="18" rx="15" ry="5.6" stroke="#ffce4d" stroke-width="3.2"/><path d="M12.5 26.5C14 33 18.6 37.5 24 37.5s10-4.5 11.5-11" stroke="#3db8f5" stroke-width="3.2" stroke-linecap="round"/></svg>`),
      el('h3', { text: 'Server is offline', style: { marginBottom: '6px' } }),
      el('p', { text: 'This view talks to the running server. Start it from Server Control to manage live data.', style: { color: 'var(--ink-2)', fontSize: '13.5px', margin: '0 auto 18px', maxWidth: '380px', lineHeight: '1.6' } }),
      go));
}

export function noTargetPanel() {
  return el('div.card', { style: { maxWidth: '520px', margin: '40px auto' } },
    el('div.card-body', { style: { textAlign: 'center', padding: '34px' } },
      el('h3', { text: 'No account selected', style: { marginBottom: '6px' } }),
      el('p', { text: 'Create an account in the Accounts view, then pick it from the selector in the header.', style: { color: 'var(--ink-2)', fontSize: '13.5px', lineHeight: '1.6' } })));
}

// Async list loader: show spinner, then rows or empty/error state.
export async function loadInto(container, loader, render) {
  container.innerHTML = `<div class="empty"><div class="spinner"></div></div>`;
  try {
    const data = await loader();
    clear(container);
    render(container, data);
  } catch (e) {
    container.innerHTML = `<div class="empty"><b>Couldn't load</b><span>${String(e.message || e)}</span></div>`;
  }
}
