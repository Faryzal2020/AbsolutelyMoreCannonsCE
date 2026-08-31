import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_127mmMark16_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';

/**
 * The MCP surface driven exactly as a client drives it: JSON-RPC over an
 * in-memory transport, against a copy of the real mod XML.
 */
test('MCP tool surface', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  const pristine = fixture.read(FILE);

  await t.test('the catalogue is advertised with schemas', async () => {
    const tools = await mcp.listTools();
    const names = tools.map((x) => x.name);
    for (const expected of [
      'list_turrets', 'get_turret', 'get_field_catalogue', 'update_turret',
      'batch_update_turrets', 'get_diffs', 'inject_xml_changes', 'rollback_xml',
      'revert_all', 'extract_defs',
    ]) {
      assert.ok(names.includes(expected), `${expected} is registered`);
    }
    assert.ok(tools.every((x) => x.inputSchema?.type === 'object'), 'every tool declares an object schema');
  });

  await t.test('extract_defs populates the database', async () => {
    const { body } = await mcp.call('extract_defs');
    assert.equal(body.ok, true);
    assert.equal(body.malformed.length, 0);
    assert.ok(body.counts.turrets > 40, `expected many turrets, got ${body.counts.turrets}`);
  });

  await t.test('list_turrets returns compact rows, not whole records', async () => {
    const { body } = await mcp.call('list_turrets');
    assert.ok(body.count > 40);
    const row = body.turrets.find((x) => x.defName === TURRET);
    assert.equal(row.maxHitPoints, 1792);
    assert.equal(row.modified, false);
    assert.equal(row.stats, undefined, 'nested blobs are not included');

    // The whole point of the projection: the full records are ~165 KB.
    const bytes = JSON.stringify(body).length;
    assert.ok(bytes < 20_000, `listing should stay small, was ${bytes} bytes`);
  });

  await t.test('list_turrets filters are ANDed', async () => {
    const { body } = await mcp.call('list_turrets', { search: '127mmMark16' });
    assert.ok(body.count >= 1);
    assert.ok(body.turrets.every((x) => /127mmmark16/i.test(x.defName + x.label + x.weapon)));

    const { body: none } = await mcp.call('list_turrets', { modified: true });
    assert.equal(none.count, 0, 'nothing is modified yet');
  });

  await t.test('get_turret returns the full record', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(body.turret.stats.maxHitPoints, 1792);
    assert.equal(body.turret.comps.powerWatts, 500);
    assert.equal(body.turret.ammo.magazineSize, 50);
  });

  await t.test('get_turret reports an unknown def as a tool error', async () => {
    const { isError, body } = await mcp.call('get_turret', { defName: 'Nope' });
    assert.equal(isError, true);
    assert.equal(body.ok, false);
    assert.match(body.error, /Unknown turret def/);
  });

  await t.test('get_field_catalogue lists the dotted keys', async () => {
    const { body } = await mcp.call('get_field_catalogue');
    const hp = body.fields.find((f) => f.key === 'stats.maxHitPoints');
    assert.equal(hp.type, 'int');
    const watts = body.fields.find((f) => f.key === 'comps.powerWatts');
    assert.equal(watts.gate, 'comps.isPowered');
    assert.ok(body.toggles.some((x) => x.key === 'barrel.enabled'));
  });

  await t.test('update_turret applies a dotted key', async () => {
    const { body } = await mcp.call('update_turret', {
      defName: TURRET,
      changes: { 'stats.maxHitPoints': 2500 },
    });
    assert.equal(body.ok, true);
    assert.equal(body.modified, true);
    assert.deepEqual(body.applied, [{ key: 'stats.maxHitPoints', from: 1792, to: 2500 }]);

    const row = mcp.db.get('SELECT max_hp, modified FROM turrets WHERE def_name = ?', TURRET);
    assert.equal(row.max_hp, 2500, 'nested patch reached the projected column');
    assert.equal(row.modified, 1);
    assert.equal(fixture.read(FILE), pristine, 'no file was touched');
  });

  await t.test('an unknown field key is rejected, not merged', async () => {
    // The regression that motivates fieldPatch.js: a bare "maxHitPoints" would
    // otherwise be stored verbatim, flag the record modified, and inject nothing.
    const { isError, body } = await mcp.call('update_turret', {
      defName: 'Turret_127mmMark16_indirect_Base',
      changes: { maxHitPoints: 999, 'weapon.cooldown': 3 },
    });
    assert.equal(isError, true);
    assert.equal(body.ok, false);
    assert.deepEqual(body.rejected.map((r) => r.key).sort(), ['maxHitPoints', 'weapon.cooldown']);

    const row = mcp.db.get('SELECT modified, data_json FROM turrets WHERE def_name = ?',
      'Turret_127mmMark16_indirect_Base');
    assert.equal(row.modified, 0, 'the record was left alone');
    assert.equal(JSON.parse(row.data_json).maxHitPoints, undefined, 'no junk key stored');
  });

  await t.test('a field behind a disabled comp is rejected', async () => {
    const unpowered = mcp.db.get(
      'SELECT def_name FROM turrets WHERE is_powered = 0 AND modified = 0 LIMIT 1');
    if (!unpowered) return; // every turret in the fixture is powered
    const { isError, body } = await mcp.call('update_turret', {
      defName: unpowered.def_name,
      changes: { 'comps.powerWatts': 300 },
    });
    assert.equal(isError, true);
    assert.equal(body.rejected[0].reason, 'gate-disabled');
  });

  await t.test('the new shoot-verb fields are extracted', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    const b = body.turret.ballistics;
    assert.equal(b.defaultProjectile, 'Bullet_127mmMark16_HE');
    assert.equal(b.recoilAmount, 0.2);
    assert.equal(b.recoilPattern, 'Mounted');
    assert.equal(b.soundCastTail, 'GunTail_Heavy');
    assert.equal(b.ignorePartialLoSBlocker, true);
    assert.equal(b.canTargetLocations, true);
  });

  await t.test('get_diffs reports the pending change without XML bodies', async () => {
    const { body } = await mcp.call('get_diffs');
    assert.equal(body.defCount, 1);
    const change = body.diffs[0].changes.find((c) => c.key === 'stats.maxHitPoints');
    assert.equal(change.from, 1792);
    assert.equal(change.to, 2500);
    assert.equal(body.diffs[0].panes, undefined);
  });

  await t.test('preview_turret_xml shows both sides without writing', async () => {
    const { body } = await mcp.call('preview_turret_xml', { defName: TURRET });
    assert.ok(body.panes.length >= 1);
    const building = body.panes.find((p) => p.role === 'building');
    assert.ok(building.original.includes('<MaxHitPoints>1792</MaxHitPoints>'));
    assert.ok(building.updated.includes('<MaxHitPoints>2500</MaxHitPoints>'));
    assert.equal(fixture.read(FILE), pristine);
  });

  await t.test('extract_defs refuses while an edit is pending', async () => {
    const { isError, body } = await mcp.call('extract_defs');
    assert.equal(isError, true);
    assert.equal(body.pending, 1);
    assert.match(body.error, /pending edits/);

    const row = mcp.db.get('SELECT max_hp FROM turrets WHERE def_name = ?', TURRET);
    assert.equal(row.max_hp, 2500, 'the edit survived the refusal');
  });

  await t.test('inject_xml_changes previews by default', async () => {
    const { body } = await mcp.call('inject_xml_changes');
    assert.equal(body.dryRun, true);
    assert.equal(body.appliedCount, 1);
    assert.equal(fixture.read(FILE), pristine, 'dry run wrote nothing');
    assert.equal(fixture.exists(`${FILE}.bak`), false, 'dry run made no backup');
  });

  await t.test('inject_xml_changes writes the tag when dryRun is off', async () => {
    const { body } = await mcp.call('inject_xml_changes', { dryRun: false });
    assert.equal(body.filesWritten, 1);
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));
    assert.deepEqual(body.backups, [`${FILE}.bak`]);
    assert.deepEqual(body.ammoDefsTouched, []);

    const after = fixture.read(FILE);
    assert.equal(
      after.replace('<MaxHitPoints>2500</MaxHitPoints>', '<MaxHitPoints>1792</MaxHitPoints>'),
      pristine,
      'the file differs from the original by exactly one tag value',
    );
    assert.equal(countOccurrences(after, '<MaxHitPoints>1792</MaxHitPoints>'), 1,
      'the indirect variant in the same file kept its own value');
    assert.equal(fixture.read(`${FILE}.bak`), pristine, 'backup holds the pristine content');
  });

  await t.test('rollback_xml restores the original XML', async () => {
    const { body: preview } = await mcp.call('rollback_xml');
    assert.equal(preview.dryRun, true);
    assert.equal(fixture.exists(`${FILE}.bak`), true, 'dry run consumed nothing');

    const { body } = await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(body.restoredCount, 1);
    assert.equal(fixture.read(FILE), pristine, 'file restored byte for byte');
    assert.equal(fixture.exists(`${FILE}.bak`), false, 'backup consumed');

    const { body: turret } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(turret.turret.stats.maxHitPoints, 1792, 'database re-extracted from restored XML');
  });
});
