import fs from 'node:fs';
import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp } from './helpers.js';

const TURRET = 'Turret_127mmMark16_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';

/** Edit the first occurrence of a tag in a fixture file, as a person would by hand. */
function handEdit(fixture, relPath, tag, value) {
  const body = fixture.read(relPath);
  const next = body.replace(new RegExp(`<${tag}>[^<]*</${tag}>`), `<${tag}>${value}</${tag}>`);
  assert.notEqual(next, body, `${tag} found in ${relPath}`);
  fs.writeFileSync(fixture.file(relPath), next);
}

test('rollback only restores backups the editor can vouch for', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  // A backup left behind by older tooling, months behind the live file.
  const staleBackup = fixture.read(FILE).replace(/<MaxHitPoints>\d+</, '<MaxHitPoints>1<');
  fs.writeFileSync(`${fixture.file(FILE)}.bak`, staleBackup);

  await t.test('an untracked backup is listed as not restorable and skipped', async () => {
    const { body: list } = await mcp.call('list_backups');
    assert.equal(list.backups.length, 1);
    assert.equal(list.backups[0].restorable, false);
    assert.match(list.backups[0].reason, /untracked/);

    const { body } = await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(body.restoredCount, 0);
    assert.equal(body.skippedCount, 1);
    assert.ok(!fixture.read(FILE).includes('<MaxHitPoints>1<'), 'live file untouched');
  });

  await t.test('injection refreshes a stale backup with the real pre-injection file', async () => {
    const before = fixture.read(FILE);
    await mcp.call('update_turret', { defName: TURRET, changes: { 'stats.maxHitPoints': 2222 } });
    const { body } = await mcp.call('inject_xml_changes', { dryRun: false });
    assert.deepEqual(body.refreshedBackups, [`${FILE}.bak`]);
    assert.equal(fs.readFileSync(`${fixture.file(FILE)}.bak`, 'utf8'), before);
  });

  await t.test('a backup is not restored once its file was edited by hand', async () => {
    handEdit(fixture, FILE, 'WorkToBuild', '12345');
    const { body } = await mcp.call('rollback_xml', { dryRun: true });
    assert.equal(body.restoredCount, 0);
    assert.match(body.skipped[0].reason, /changed since/);

    const { body: forced } = await mcp.call('rollback_xml', { dryRun: true, force: true });
    assert.equal(forced.restoredCount, 1, 'force overrides the guard');
  });

  await t.test('a trusted backup restores the pre-injection file', async () => {
    // Re-inject so the editor's last write is the current file again.
    const original = fs.readFileSync(`${fixture.file(FILE)}.bak`, 'utf8');
    handEdit(fixture, FILE, 'WorkToBuild', '44000');
    await mcp.call('update_turret', { defName: TURRET, changes: { 'stats.maxHitPoints': 2223 } });
    await mcp.call('inject_xml_changes', { dryRun: false });

    const { body } = await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(body.restoredCount, 1, JSON.stringify(body.skipped));
    assert.equal(fixture.read(FILE), original);
    assert.equal(fixture.exists(`${FILE}.bak`), false);
  });
});
