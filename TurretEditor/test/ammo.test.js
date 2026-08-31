import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startApp } from './helpers.js';

test('Ammunition management suite', async (t) => {
  const fx = makeFixture();
  const app = await startApp();

  t.after(async () => {
    await app.close();
    fx.cleanup();
  });

  await t.test('1. Extracts ammunition items from fixture', async () => {
    const res = await app.post('/api/extract');
    assert.equal(res.status, 200);
    assert.equal(res.body.ok, true);
    assert.ok(res.body.counts.ammo > 0, 'Extracted ammo items');

    const listRes = await app.get('/api/ammo');
    assert.equal(listRes.status, 200);
    assert.ok(listRes.body.count >= 50, 'At least 50 ammo items parsed');
  });

  await t.test('2. Retrieves single ammo definition details', async () => {
    const res = await app.get('/api/ammo/Ammo_76mmOtomelaraC_HE');
    assert.equal(res.status, 200);
    assert.equal(res.body.ammo.defName, 'Ammo_76mmOtomelaraC_HE');
    assert.equal(res.body.ammo.label, '76x636mm (HE)');
    assert.equal(res.body.ammo.hasDirectMode, true);
    assert.equal(res.body.ammo.hasIndirectMode, true);
  });

  await t.test('3. Updates shared payload stats and mode toggles', async () => {
    const patch = {
      payload: { damageAmountBase: 160, explosionRadius: 3.2 },
      stats: { marketValue: 95.0 },
    };

    const updateRes = await app.put('/api/ammo/Ammo_76mmOtomelaraC_HE', patch);
    assert.equal(updateRes.status, 200);
    assert.equal(updateRes.body.ammo.payload.damageAmountBase, 160);
    assert.equal(updateRes.body.ammo.payload.explosionRadius, 3.2);
    assert.equal(updateRes.body.ammo.modified, true);

    const diffRes = await app.get('/api/diffs');
    assert.equal(diffRes.status, 200);
    assert.ok(diffRes.body.changeCount > 0, 'Diff count > 0');
  });

  await t.test('4. Injects diffs into XML and verifies both direct and indirect bullets updated', async () => {
    const injectRes = await app.post('/api/inject', { dryRun: false });
    assert.equal(injectRes.status, 200);
    assert.equal(injectRes.body.ok, true);
    assert.ok(injectRes.body.appliedCount > 0);

    const fileContent = fx.read('Common/Defs/Ammo/76mmOtomelaraC.xml');
    assert.ok(fileContent.includes('<damageAmountBase>160</damageAmountBase>'), 'Updated XML base damage');
    assert.ok(fileContent.includes('<explosionRadius>3.2</explosionRadius>'), 'Updated XML explosion radius');
  });

  await t.test('5. Rollback restores XML files byte-for-byte', async () => {
    const rollbackRes = await app.post('/api/rollback');
    assert.equal(rollbackRes.status, 200);
    assert.ok(rollbackRes.body.restoredCount > 0, 'Restored backup files');

    const fileContent = fx.read('Common/Defs/Ammo/76mmOtomelaraC.xml');
    assert.ok(fileContent.includes('<damageAmountBase>132</damageAmountBase>'), 'Restored original XML base damage');
  });
});
