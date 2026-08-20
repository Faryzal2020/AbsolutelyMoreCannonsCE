import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startApp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_127mmMark16_Base';
const WEAPON = 'Turret_127mmMark16_Weapon';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';

/**
 * The full acceptance flow, run against a copy of the real mod XML:
 * extract -> edit -> inject -> verify on disk -> rollback -> verify restored.
 */
test('XML -> SQLite -> XML round trip', async (t) => {
  const fixture = makeFixture();
  const app = await startApp();
  t.after(async () => { await app.close(); fixture.cleanup(); });

  const pristine = fixture.read(FILE);

  await t.test('POST /api/extract ingests the XML files into SQLite', async () => {
    const { status, body } = await app.post('/api/extract');
    assert.equal(status, 200);
    assert.equal(body.ok, true);
    assert.equal(body.malformed.length, 0, 'no malformed XML in the fixture');
    assert.ok(body.counts.turrets > 40, `expected many turrets, got ${body.counts.turrets}`);
    assert.ok(body.counts.files > 20);
    assert.ok(body.counts.weapons > 0);
    assert.ok(body.counts.modExtensions > 0);

    const rows = app.db.all('SELECT COUNT(*) AS n FROM turrets');
    assert.equal(rows[0].n, body.counts.turrets, 'DB row count matches reported count');
  });

  await t.test('GET /api/turrets returns parsed stats, weapons and extensions', async () => {
    const { status, body } = await app.get('/api/turrets');
    assert.equal(status, 200);
    const turret = body.turrets.find((x) => x.defName === TURRET);
    assert.ok(turret, `${TURRET} was extracted`);

    // Own statBases values.
    assert.equal(turret.stats.maxHitPoints, 1792);
    assert.equal(turret.stats.workToBuild, 44000);
    assert.equal(turret.stats.cooldownTime, 3.3);

    // Linked weapon def.
    assert.equal(turret.weaponDefName, WEAPON);
    assert.equal(turret.ballistics.sightsEfficiency, 4);
    assert.equal(turret.ballistics.maxRange, 23700);
    assert.equal(turret.ammo.magazineSize, 50);

    // Comps and mod extensions.
    assert.equal(turret.comps.isPowered, true);
    assert.equal(turret.comps.powerWatts, 500);
    assert.equal(turret.comps.hasModeSwap, true);
    assert.equal(turret.comps.swapAltDef, 'Turret_127mmMark16_indirect_Base');
    assert.equal(turret.smoker.enabled, true);
    assert.equal(turret.smoker.shockwave.radius, 5);
    assert.equal(turret.barrel.enabled, true);
    assert.equal(turret.barrel.recoil.recoilDuration, 15);
    assert.equal(turret.modified, false);

    // Inheritance from AMCTurretBase / AMCTurretMannedBase resolved.
    assert.equal(turret.comps.isManned, true);
    assert.ok(turret.ancestry.includes('AMCTurretMannedBase'));
  });

  await t.test('PUT /api/turrets/:defName updates the SQLite record', async () => {
    const { status, body } = await app.put(`/api/turrets/${TURRET}`, {
      stats: { maxHitPoints: 2500 },
    });
    assert.equal(status, 200);
    assert.equal(body.turret.stats.maxHitPoints, 2500);
    assert.equal(body.turret.modified, true);
    // Untouched sibling fields keep their values.
    assert.equal(body.turret.stats.workToBuild, 44000);

    const row = app.db.get('SELECT max_hp, modified FROM turrets WHERE def_name = ?', TURRET);
    assert.equal(row.max_hp, 2500, 'relational column is projected too');
    assert.equal(row.modified, 1);

    // The file on disk must not have changed yet.
    assert.equal(fixture.read(FILE), pristine, 'PUT does not touch disk');
  });

  await t.test('GET /api/diffs reports the pending change', async () => {
    const { body } = await app.get('/api/diffs');
    assert.equal(body.defCount, 1);
    const diff = body.diffs[0];
    assert.equal(diff.defName, TURRET);
    const change = diff.changes.find((c) => c.key === 'stats.maxHitPoints');
    assert.ok(change, 'MaxHitPoints diff present');
    assert.equal(change.from, 1792);
    assert.equal(change.to, 2500);
    assert.equal(change.xmlPath, 'statBases > MaxHitPoints');
  });

  await t.test('POST /api/inject writes the tag and leaves everything else alone', async () => {
    const { status, body } = await app.post('/api/inject');
    assert.equal(status, 200);
    assert.equal(body.filesWritten, 1);
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));
    assert.deepEqual(body.backups, [FILE + '.bak']);

    const after = fixture.read(FILE);
    assert.ok(after.includes('<MaxHitPoints>2500</MaxHitPoints>'), 'new value written');
    assert.ok(!after.includes('<MaxHitPoints>1792</MaxHitPoints>')
      || countOccurrences(after, '<MaxHitPoints>1792</MaxHitPoints>') === 1,
    'only the targeted def changed');

    // The indirect variant in the same file keeps its own 1792.
    assert.equal(countOccurrences(after, '<MaxHitPoints>2500</MaxHitPoints>'), 1);
    assert.equal(countOccurrences(after, '<MaxHitPoints>1792</MaxHitPoints>'), 1);

    // Unrelated tags, comments and formatting survive byte for byte.
    assert.equal(countOccurrences(after, '<WorkToBuild>44000</WorkToBuild>'), 2);
    assert.ok(after.includes('<!-- INDIRECT -->'), 'comments preserved');
    assert.ok(after.includes('<shockwaveRadius>5</shockwaveRadius> <!-- radius in cells -->'),
      'inline trailing comments preserved');
    assert.equal(
      after.replace('<MaxHitPoints>2500</MaxHitPoints>', '<MaxHitPoints>1792</MaxHitPoints>'),
      pristine,
      'the file differs from the original by exactly one tag value',
    );

    // The backup holds the pristine content.
    assert.equal(fixture.read(FILE + '.bak'), pristine);
  });

  await t.test('the record is no longer modified after injection', async () => {
    const { body } = await app.get(`/api/turrets/${TURRET}`);
    assert.equal(body.turret.stats.maxHitPoints, 2500);
    assert.equal(body.turret.modified, false, 'DB and disk agree again');

    const { body: diffs } = await app.get('/api/diffs');
    assert.equal(diffs.changeCount, 0);
  });

  await t.test('POST /api/rollback restores the original XML', async () => {
    const { status, body } = await app.post('/api/rollback');
    assert.equal(status, 200);
    assert.equal(body.restoredCount, 1);
    assert.deepEqual(body.restored, [FILE]);

    assert.equal(fixture.read(FILE), pristine, 'file restored byte for byte');
    assert.equal(fixture.exists(FILE + '.bak'), false, 'backup consumed');

    const { body: turret } = await app.get(`/api/turrets/${TURRET}`);
    assert.equal(turret.turret.stats.maxHitPoints, 1792, 'DB re-extracted from restored XML');
  });
});
