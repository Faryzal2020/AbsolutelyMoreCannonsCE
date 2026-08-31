import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startApp } from './helpers.js';

const NAVAL = 'Turret_127mmMark16_Base';

test('read-only API surface', async (t) => {
  const fixture = makeFixture();
  const app = await startApp();
  t.after(async () => { await app.close(); fixture.cleanup(); });
  await app.post('/api/extract');

  await t.test('health reports the active SQLite driver', async () => {
    const { body } = await app.get('/api/health');
    assert.equal(body.ok, true);
    assert.ok(body.turrets > 0);
    assert.ok(['node:sqlite', 'better-sqlite3'].includes(body.driver));
  });

  await t.test('metrics power the dashboard counters', async () => {
    const { body } = await app.get('/api/metrics');
    assert.ok(body.total > 40);
    assert.ok(body.modeSwapCapable > 0);
    assert.ok(body.powered > 0);
    assert.ok(body.animated > 0);
    assert.equal(body.modified, 0);
    assert.ok(Array.isArray(body.categories));
    assert.ok(body.categories.includes('Naval Guns'));
  });

  await t.test('search and filters narrow the collection', async () => {
    const { body: all } = await app.get('/api/turrets');
    const { body: naval } = await app.get('/api/turrets?category=Naval%20Guns');
    assert.ok(naval.count > 0 && naval.count < all.count);
    assert.ok(naval.turrets.every((x) => x.category === 'Naval Guns'));

    const { body: search } = await app.get('/api/turrets?search=vulcan');
    assert.ok(search.count >= 1);
    assert.ok(search.turrets.some((x) => x.defName === 'Turret_20mmM61Vulcan_Base'));

    const { body: byAmmo } = await app.get('/api/turrets?search=AmmoSet_127mmMark16');
    assert.ok(byAmmo.count >= 1, 'full-text search covers ammo set names');

    const { body: none } = await app.get('/api/turrets?modified=true');
    assert.equal(none.count, 0);
  });

  await t.test('the audit report is clean for the shipped defs', async () => {
    const { body } = await app.get('/api/audit');
    assert.equal(body.ok, true);
    assert.ok(body.scanned > 40);
    assert.equal(body.totals.errors, 0, JSON.stringify(body.entries.slice(0, 3), null, 2));
  });

  await t.test('the audit catches an invalid edit', async () => {
    await app.put(`/api/turrets/${NAVAL}`, {
      stats: { maxHitPoints: 0 },
      ballistics: { minRange: 900, maxRange: 100 },
    });
    const { body } = await app.get('/api/audit');
    const entry = body.entries.find((e) => e.defName === NAVAL);
    assert.ok(entry, 'edited turret appears in the report');
    const rules = entry.issues.map((i) => i.rule);
    assert.ok(rules.includes('hp'), 'zero hit points flagged');
    assert.ok(rules.includes('range'), 'min range above max range flagged');

    await app.post('/api/revert-all');
    const { body: after } = await app.get('/api/audit');
    assert.equal(after.totals.errors, 0);
  });

  await t.test('the XML diff viewer returns both panes', async () => {
    await app.put(`/api/turrets/${NAVAL}`, { stats: { maxHitPoints: 4242 } });
    const { body } = await app.get(`/api/diffs/${NAVAL}/xml`);
    assert.equal(body.ok, true);
    const pane = body.panes.find((p) => p.role === 'building');
    assert.ok(pane.original.includes('<MaxHitPoints>1792</MaxHitPoints>'));
    assert.ok(pane.updated.includes('<MaxHitPoints>4242</MaxHitPoints>'));
    assert.ok(!pane.updated.includes('<MaxHitPoints>1792</MaxHitPoints>'));
    await app.post('/api/revert-all');
  });

  await t.test('the OpenAPI document describes the export endpoints', async () => {
    const { body } = await app.get('/api/openapi.json');
    assert.ok(body.paths['/api/export'].get.parameters.some((p) => p.name === 'format'));
    assert.ok(body.paths['/api/export/columns']);
  });

  await t.test('snapshot restore replaces the live state', async () => {
    const { body: before } = await app.get('/api/turrets');
    const snapshot = before.turrets.map((x) => ({ ...x, stats: { ...x.stats, mass: 7 } }));
    const { body } = await app.post('/api/restore', { turrets: snapshot });
    assert.equal(body.restored, before.count);

    const { body: after } = await app.get(`/api/turrets/${NAVAL}`);
    assert.equal(after.turret.stats.mass, 7);
    assert.equal(after.turret.modified, true);
    await app.post('/api/revert-all');
  });

  await t.test('the schema catalogue exposes every editable path', async () => {
    const { body } = await app.get('/api/schema');
    const keys = body.fields.map((f) => f.key);
    for (const expected of [
      'stats.maxHitPoints', 'ballistics.maxRange', 'ammo.magazineSize',
      'barrel.spinning.maxRPM', 'smoker.heat.threshold', 'comps.powerWatts',
    ]) assert.ok(keys.includes(expected), `${expected} is documented`);

    const hp = body.fields.find((f) => f.key === 'stats.maxHitPoints');
    assert.equal(hp.xmlPath, 'statBases > MaxHitPoints');
    assert.ok(body.componentToggles.some((c) => c.key === 'smoker.enabled'));
  });

  await t.test('the OpenAPI document describes the whole pipeline', async () => {
    const { body } = await app.get('/api/openapi.json');
    assert.equal(body.openapi, '3.1.0');
    for (const p of ['/api/extract', '/api/turrets', '/api/turrets/{defName}', '/api/diffs', '/api/inject', '/api/rollback']) {
      assert.ok(body.paths[p], `${p} is documented`);
    }
    assert.ok(body.paths['/api/turrets/{defName}'].put);
  });

  await t.test('unknown defs and endpoints return structured 404s', async () => {
    const { status, body } = await app.get('/api/turrets/Nope_Does_Not_Exist');
    assert.equal(status, 404);
    assert.equal(body.ok, false);

    const missing = await app.get('/api/not-a-route');
    assert.equal(missing.status, 404);
    assert.equal(missing.body.ok, false);

    const badPut = await app.put(`/api/turrets/${NAVAL}`, ['not', 'an', 'object']);
    assert.equal(badPut.status, 400);
    assert.equal(badPut.body.ok, false);
  });

  await t.test('rollback dry run previews without restoring', async () => {
    const { body } = await app.post('/api/rollback', { dryRun: true });
    assert.equal(body.dryRun, true);
    assert.equal(body.restoredCount, 0, 'nothing was injected in this suite');
  });
});
