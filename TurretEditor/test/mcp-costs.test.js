import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_127mmMark16_Base';
const INDIRECT = 'Turret_127mmMark16_indirect_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';

/**
 * Cost lists: diffable and injectable through the XML pipeline, but previously
 * unreachable from MCP because `costs` is not a field-map key.
 *
 * The fixture file holds both the direct and the indirect variant, so every
 * assertion counts occurrences rather than testing for presence.
 */
test('MCP cost list editing', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const pristine = fixture.read(FILE);

  await t.test('the catalogue advertises costs as a special key', async () => {
    const { body } = await mcp.call('get_field_catalogue');
    const costs = body.special.find((x) => x.key === 'costs');
    assert.equal(costs.type, 'costList');
    assert.match(costs.shape, /thingDef, count/);
  });

  await t.test('a whole cost list can be replaced', async () => {
    const { body: before } = await mcp.call('get_turret', { defName: TURRET });
    assert.deepEqual(before.turret.costs[0], { thingDef: 'Steel', count: 900 });

    const { body } = await mcp.call('update_turret', {
      defName: TURRET,
      changes: { costs: [{ thingDef: 'Steel', count: 1200 }, { thingDef: 'Plasteel', count: 40 }] },
    });
    assert.equal(body.ok, true);
    assert.equal(body.applied[0].key, 'costs');
    assert.deepEqual(body.applied[0].to, [
      { thingDef: 'Steel', count: 1200 }, { thingDef: 'Plasteel', count: 40 },
    ]);
    assert.equal(fixture.read(FILE), pristine, 'no file was touched');
  });

  await t.test('the cost change shows up as a costList diff', async () => {
    const { body } = await mcp.call('get_diffs', { defNames: [TURRET] });
    const change = body.diffs[0].changes.find((c) => c.key === 'costs');
    assert.ok(change, 'costList diff present');
    assert.equal(change.to.length, 2);
  });

  await t.test('injection rewrites one costList and leaves the other alone', async () => {
    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.filesWritten, 1);
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, '<Steel>1200</Steel>'), 1, 'new count written');
    assert.equal(countOccurrences(after, '<Plasteel>40</Plasteel>'), 1, 'new row added');
    assert.equal(countOccurrences(pristine, '<Steel>900</Steel>'), 2, 'both defs started at 900');
    assert.equal(countOccurrences(after, '<Steel>900</Steel>'), 1, 'only the target changed');
    assert.equal(countOccurrences(after, '<ComponentIndustrial>20</ComponentIndustrial>'), 1,
      'the row dropped from the target list is gone, the other def keeps its own');
    assert.equal(countOccurrences(after, '<costList>'), countOccurrences(pristine, '<costList>'));
    assert.ok(after.includes('<!-- INDIRECT -->'), 'comments preserved');
  });

  await t.test('invalid cost lists are rejected', async () => {
    for (const bad of [[], [{ count: 5 }], [{ thingDef: 'Steel' }], 'Steel', {}]) {
      const { isError, body } = await mcp.call('update_turret', {
        defName: INDIRECT, changes: { costs: bad },
      });
      assert.equal(isError, true, `rejected: ${JSON.stringify(bad)}`);
      assert.equal(body.rejected[0].reason, 'invalid-value');
    }
    assert.equal(mcp.db.get('SELECT modified FROM turrets WHERE def_name = ?', INDIRECT).modified, 0,
      'no partial write');
  });

  await t.test('batch mul scales every resource count and floors at 1', async () => {
    const { body: before } = await mcp.call('get_turret', { defName: INDIRECT });
    const expected = before.turret.costs.map((c) => ({
      thingDef: c.thingDef, count: Math.max(1, Math.round(c.count * 0.5)),
    }));

    const { body } = await mcp.call('batch_update_turrets', {
      defNames: [INDIRECT], ops: [{ key: 'costs', op: 'mul', value: 0.5 }],
    });
    assert.equal(body.updated, 1);
    assert.deepEqual(body.results[0].changes[0].to, expected);
    assert.ok(expected.every((c) => c.count >= 1), 'nothing scaled below one unit');
  });
});
