// Process-log ring buffer shared between the main process stream and whatever
// page is currently showing the console.
const MAX = 1200;
export const logBuffer = [];
const subs = new Set();

export function pushLog(entry) {
  logBuffer.push(entry);
  if (logBuffer.length > MAX) logBuffer.splice(0, logBuffer.length - MAX);
  subs.forEach((f) => f(entry));
}
export function onLog(f) { subs.add(f); return () => subs.delete(f); }
export function clearLog() { logBuffer.length = 0; subs.forEach((f) => f(null)); }
