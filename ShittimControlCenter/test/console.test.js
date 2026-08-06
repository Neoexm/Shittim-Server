'use strict';

const test = require('node:test');
const assert = require('node:assert');

// ui.js is an ES module and the rest of these tests are CommonJS, so pull it in per test.
const ui = () => import('../src/js/ui.js');

// A frame the caller fires by hand, standing in for requestAnimationFrame.
function frames() {
  const waiting = [];
  return { schedule: (f) => waiting.push(f), tick: () => { const due = waiting.splice(0); due.forEach((f) => f()); } };
}

test('lines arriving between two frames are rendered in one pass', async () => {
  const { batched } = await ui();
  const f = frames();
  const flushes = [];
  const feed = batched((rows) => flushes.push(rows.length), { schedule: f.schedule });

  for (let i = 0; i < 500; i++) feed.push({ source: 'server', line: `line ${i}` });
  assert.deepEqual(flushes, [], 'nothing is touched until the frame');

  f.tick();
  assert.deepEqual(flushes, [500], 'one flush for the whole burst, not one per line');

  f.tick();
  assert.deepEqual(flushes, [500], 'an idle frame does no work');
});

// Without this the console dock appended a node and read scrollHeight back per line, which is a forced layout each time. 20000 lines left the renderer 16.9 seconds behind a child process that had already exited, and for those 16.9 seconds the window could not service the button that stops the server.
test('a burst costs one flush per frame however fast it arrives', async () => {
  const { batched } = await ui();
  const f = frames();
  let flushes = 0;
  const feed = batched(() => flushes++, { schedule: f.schedule });

  for (let frame = 0; frame < 6; frame++) {
    for (let i = 0; i < 4000; i++) feed.push({ source: 'server', line: 'x' });
    f.tick();
  }
  assert.equal(flushes, 6);
});

test('a burst bigger than the console holds does not build the part nobody could see', async () => {
  const { batched } = await ui();
  const f = frames();
  let handed = null;
  const feed = batched((rows) => { handed = rows; }, { cap: 1200, schedule: f.schedule });

  for (let i = 0; i < 50000; i++) feed.push({ source: 'server', line: `line ${i}` });
  f.tick();

  assert.equal(handed.length, 1200);
  assert.equal(handed[handed.length - 1].line, 'line 49999', 'the newest lines are the ones kept');
});

// Switching the source filter repaints from the ring buffer. Anything still queued was selected under the old filter.
test('what is waiting on a frame can be thrown away', async () => {
  const { batched } = await ui();
  const f = frames();
  let flushed = false;
  const feed = batched(() => { flushed = true; }, { schedule: f.schedule });

  feed.push({ source: 'mitm', line: 'proxy line' });
  feed.discard();
  f.tick();

  assert.equal(flushed, false);
});
