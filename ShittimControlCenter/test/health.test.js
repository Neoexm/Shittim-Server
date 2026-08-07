'use strict';

const test = require('node:test');
const assert = require('node:assert');

// health.js is an ES module and the rest of these tests are CommonJS, so pull it in per test.
const health = () => import('../src/js/health.js');

const T = 1_700_000_000_000;
const GRACE = 90_000;

test('a server the probe can reach is online whether or not we spawned it', async () => {
  const { serverHealth } = await health();
  assert.equal(serverHealth({ proc: 'stopped', startedAt: null, live: true, ready: true, now: T, grace: null }), 'online');
  assert.equal(serverHealth({ proc: 'running', startedAt: T - 5000, live: true, ready: true, now: T, grace: GRACE }), 'online');
});

test('a port that answers outranks a child we never spawned', async () => {
  const { serverHealth } = await health();
  assert.equal(serverHealth({ proc: 'stopped', startedAt: null, live: true, ready: false, now: T, grace: null }), 'starting');
});

test('a child that is up with nothing on the port stops claiming to be starting', async () => {
  const { serverHealth } = await health();
  const args = { proc: 'running', startedAt: T - GRACE - 1, live: false, ready: false, grace: GRACE };
  assert.equal(serverHealth({ ...args, now: T }), 'unresponsive');
  assert.equal(serverHealth({ ...args, startedAt: T - 1000, now: T }), 'starting');
});

test('a host that answers health but never becomes ready is not ready rather than starting', async () => {
  const { serverHealth } = await health();
  const args = { proc: 'running', live: true, ready: false, grace: GRACE, now: T };
  assert.equal(serverHealth({ ...args, startedAt: T - GRACE - 1 }), 'unhealthy');
  assert.equal(serverHealth({ ...args, startedAt: T - 1000 }), 'starting');
});

test('a source run gets no deadline because a long build looks the same as a hang', async () => {
  const { serverHealth } = await health();
  assert.equal(serverHealth({ proc: 'running', startedAt: T - 600_000, live: false, ready: false, now: T, grace: null }), 'starting');
});

test('a spawn that failed is not reported as merely stopped', async () => {
  const { serverHealth } = await health();
  assert.equal(serverHealth({ proc: 'failed', startedAt: null, live: false, ready: false, now: T, grace: GRACE }), 'failed');
  assert.equal(serverHealth({ proc: 'stopped', startedAt: null, live: false, ready: false, now: T, grace: GRACE }), 'stopped');
});
