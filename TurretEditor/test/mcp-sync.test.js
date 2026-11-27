import fs from 'node:fs';
import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp } from './helpers.js';

const TURRET = 'Turret_127mmMark16_Base';
const OTHER = 'Turret_100mmModel1953_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';
const OTHER_FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/100mmModel1953.xml';

/** Edit the first occurrence of a tag in a fixture file, as a person would by hand. */
function handEdit(fixture, relPath, tag, value) {
  const body = fixture.read(relPath);
  const next = body.replace(new RegExp(`<${tag}>[^<]*</${tag}>`), `<${tag}>${value}</${tag}>`);
  assert.notEqual(next, body, `${tag} found in ${relPath}`);
  fs.writeFileSync(fixture.file(relPath), next);
}

/**
 * The database is a snapshot of the XML. These tests change the XML behind the
 * server's back and check that nothing is read or written from stale values.
 */
test('MCP stays in sync with XML edited outside the editor', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');

  await t.test('an unchanged disk does not trigger a re-sync', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(body.diskSync, undefined);
  });

  await t.test('a hand edit is picked up on the next call, pending edits kept', async () => {
    await mcp.call('update_turret', { defName: TURRET, changes: { 'stats.maxHitPoints': 2500 } });
    handEdit(fixture, FILE, 'turretBurstCooldownTime', '9.9');

    const { body } = await mcp.call('get_turret', { defName: TURRET });
    assert.ok(body.diskSync, 'the call reports that it re-synced');
    assert.deepEqual(body.diskSync.files.changed, [FILE]);
    assert.deepEqual(body.diskSync.preserved, { turrets: 1, ammo: 0 });
    assert.equal(body.turret.stats.cooldownTime, 9.9, 'fresh value from disk');
    assert.equal(body.turret.stats.maxHitPoints, 2500, 'pending edit replayed');
    assert.equal(body.turret.modified, true);
  });

  await t.test('injection writes the edit without reverting the hand edit', async () => {
    const { body } = await mcp.call('inject_xml_changes', { dryRun: false });
    assert.equal(body.appliedCount, 1, JSON.stringify(body.applied));
    const xml = fixture.read(FILE);
    assert.ok(xml.includes('<MaxHitPoints>2500</MaxHitPoints>'));
    assert.ok(xml.includes('<turretBurstCooldownTime>9.9</turretBurstCooldownTime>'), 'hand edit survived');
  });

  await t.test('a key edited both on disk and in the database is reported as a conflict', async () => {
    await mcp.call('update_turret', { defName: TURRET, changes: { 'stats.maxHitPoints': 3000 } });
    handEdit(fixture, FILE, 'MaxHitPoints', '1234');

    const { body } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(body.diskSync.conflicts.length, 1);
    const [conflict] = body.diskSync.conflicts;
    assert.equal(conflict.key, 'stats.maxHitPoints');
    assert.equal(conflict.disk, 1234);
    assert.equal(conflict.pending, 3000);
    assert.equal(body.turret.stats.maxHitPoints, 3000, 'the pending edit wins');
    await mcp.call('revert_turret', { defName: TURRET });
  });

  await t.test('a filtered injection leaves other pending edits alone', async () => {
    await mcp.call('update_turret', { defName: TURRET, changes: { 'stats.workToBuild': 50000 } });
    await mcp.call('update_turret', { defName: OTHER, changes: { 'stats.workToBuild': 51000 } });

    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.filesWritten, 1);
    assert.deepEqual(body.stillPending, { turrets: 1, ammo: 0 });

    const { body: other } = await mcp.call('get_turret', { defName: OTHER });
    assert.equal(other.turret.modified, true, 'not wiped by the post-inject re-extract');
    assert.equal(other.turret.stats.workToBuild, 51000);
    await mcp.call('revert_turret', { defName: OTHER });
  });

  await t.test('the diff is three-way even without a sync', async () => {
    // The web editor and REST API do not sync per request, so the diff itself
    // must not revert disk-side changes the user never touched.
    const { computeDiffs } = await import('../src/services/diffService.js');
    await mcp.call('update_turret', { defName: OTHER, changes: { 'stats.maxHitPoints': 4321 } });
    handEdit(fixture, OTHER_FILE, 'turretBurstCooldownTime', '7.7');

    const { diffs } = computeDiffs(mcp.db, [OTHER]);
    assert.deepEqual(diffs[0].changes.map((c) => c.key), ['stats.maxHitPoints']);
    await mcp.call('revert_turret', { defName: OTHER });
  });
});
