import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
const source = await readFile(new URL('../src/services/bankQrSession.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
const { MOD_BIL_createQrSession, MOD_BIL_formatQrCountdown } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);
function fixture() {
  let now = 0, lastTick, expired = 0, next = 0;
  const timers = new Map();
  const session = MOD_BIL_createQrSession(seconds => { lastTick = seconds; }, () => { expired++; }, {
    now: () => now, schedule: callback => { timers.set(++next, callback); return next; }, cancel: id => timers.delete(id),
  });
  return { session, timers, advance: value => { now += value; }, tick: () => { for (const callback of [...timers.values()]) callback(); },
    get seconds() { return lastTick; }, get expired() { return expired; } };
}
test('starts at 15:00 and has one countdown timer', () => {
  const f = fixture(); assert.equal(f.session.isActive(), false); f.session.start();
  assert.equal(f.seconds, 900); assert.equal(MOD_BIL_formatQrCountdown(f.seconds), '15:00'); assert.equal(f.timers.size, 1);
});
test('expires exactly at 900 seconds, once, and cancels timer', () => {
  const f = fixture(); f.session.start(); f.advance(899000); f.tick(); assert.equal(f.seconds, 1); assert.equal(f.expired, 0);
  f.advance(1000); f.tick(); assert.equal(f.seconds, 0); assert.equal(f.expired, 1); assert.equal(f.timers.size, 0);
  assert.equal(f.session.isActive(), false); f.tick(); assert.equal(f.expired, 1);
});
test('background tab delayed timer does not extend deadline', () => {
  const f = fixture(); f.session.start(); f.advance(1200000); f.tick(); assert.equal(f.expired, 1); assert.equal(f.seconds, 0);
});
test('action guard expires before a throttled timer tick', () => {
  const f = fixture(); f.session.start(); f.advance(900000); assert.equal(f.session.isActive(), false);
  assert.equal(f.expired, 1); assert.equal(f.timers.size, 0);
});
test('closing/unmounting stops session without firing expiry', () => {
  const f = fixture(); f.session.start(); f.session.stop(); f.session.stop(); f.advance(900000); f.tick();
  assert.equal(f.expired, 0); assert.equal(f.timers.size, 0); assert.equal(f.session.isActive(), false);
});
test('reload replaces countdown and grants a new full 15 minutes', () => {
  const f = fixture(); f.session.start(); f.advance(899000); f.session.start();
  assert.equal(f.seconds, 900); assert.equal(f.timers.size, 1); f.advance(1000); f.tick(); assert.equal(f.seconds, 899);
});
test('expired session can be restarted without leaking timers', () => {
  const f = fixture(); f.session.start(); f.advance(900000); f.tick(); f.session.start();
  assert.equal(f.seconds, 900); assert.equal(f.session.isActive(), true); assert.equal(f.timers.size, 1);
});
test('format countdown includes leading zeroes', () => {
  assert.equal(MOD_BIL_formatQrCountdown(0), '00:00'); assert.equal(MOD_BIL_formatQrCountdown(59), '00:59');
  assert.equal(MOD_BIL_formatQrCountdown(61), '01:01');
});
