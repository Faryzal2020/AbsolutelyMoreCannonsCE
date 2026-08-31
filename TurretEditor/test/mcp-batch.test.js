import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp } from './helpers.js';

const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';

/**
 * Bulk balancing: one fixture per file, because paths.js resolves the mod root
 * once at import time.
 */
test('MCP batch updates', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const pristine = fixture.read(FILE);

  await t.test('a multiplier scales every match and rounds int fields', async () => {
    const { body: before } = await mcp.call('list_turrets', { search: 'Mark16' });
    assert.ok(before.count >= 2, 'more than one turret matches');
    const expected = new Map(before.turrets.map((x) => [x.defName, Math.round(x.maxHitPoints * 1.1)]));

    const { body } = await mcp.call('batch_update_turrets', {
      filter: { search: 'Mark16' },
      ops: [{ key: 'stats.maxHitPoints', op: 'mul', value: 1.1 }],
    });
    assert.equal(body.ok, true);
    assert.equal(body.updated, before.count);

    const { body: after } = await mcp.call('list_turrets', { search: 'Mark16' });
    for (const row of after.turrets) {
      assert.equal(row.maxHitPoints, expected.get(row.defName), `${row.defName} scaled and rounded`);
      assert.equal(row.modified, true);
    }
    assert.equal(fixture.read(FILE), pristine, 'batch edits stay in the database');
  });

  await t.test('dryRun computes without writing', async () => {
    const { body: state } = await mcp.call('list_turrets', { search: 'Mark16' });
    const hp = state.turrets[0].maxHitPoints;

    const { body } = await mcp.call('batch_update_turrets', {
      defNames: [state.turrets[0].defName],
      ops: [{ key: 'stats.maxHitPoints', op: 'add', value: 100 }],
      dryRun: true,
    });
    assert.equal(body.dryRun, true);
    assert.equal(body.results[0].changes[0].to, hp + 100);

    const row = mcp.db.get('SELECT max_hp FROM turrets WHERE def_name = ?', state.turrets[0].defName);
    assert.equal(row.max_hp, hp, 'nothing was committed');
  });

  await t.test('an invalid op changes nothing at all', async () => {
    const modifiedBefore = mcp.db.get('SELECT COUNT(*) AS n FROM turrets WHERE modified = 1').n;

    const { isError, body } = await mcp.call('batch_update_turrets', {
      filter: { search: 'Mark16' },
      ops: [{ key: 'stats.notAField', op: 'mul', value: 2 }],
    });
    assert.equal(isError, true);
    assert.equal(body.rejected[0].reason, 'unknown-field');

    assert.equal(mcp.db.get('SELECT COUNT(*) AS n FROM turrets WHERE modified = 1').n,
      modifiedBefore, 'the transaction never ran');
  });

  await t.test('relative ops are refused on non-numeric fields', async () => {
    const { isError, body } = await mcp.call('batch_update_turrets', {
      filter: { search: 'Mark16' },
      ops: [{ key: 'label', op: 'mul', value: 2 }],
    });
    assert.equal(isError, true);
    assert.equal(body.rejected[0].reason, 'not-numeric');
  });

  await t.test('revert_all clears every pending edit without touching disk', async () => {
    const { body } = await mcp.call('revert_all');
    assert.ok(body.reverted >= 2);
    assert.equal(mcp.db.get('SELECT COUNT(*) AS n FROM turrets WHERE modified = 1').n, 0);
    assert.equal(fixture.read(FILE), pristine);
  });
});
