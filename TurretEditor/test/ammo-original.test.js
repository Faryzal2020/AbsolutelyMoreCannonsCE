import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp } from './helpers.js';

test('ammo edits keep their extracted original', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const { updateAmmo } = await import('../src/services/ammoService.js');
  const { body: list } = await mcp.call('list_ammo');
  const defName = list.ammo[0].defName;
  const { body: before } = await mcp.call('get_ammo', { defName });

  updateAmmo(mcp.db, defName, { stats: { marketValue: 999 } });
  const { body: edited } = await mcp.call('get_ammo', { defName });
  assert.equal(Number(edited.ammo.stats.marketValue), 999);
  assert.equal(edited.ammo.modified, true);

  await mcp.call('revert_ammo', { defName });
  const { body: after } = await mcp.call('get_ammo', { defName });
  assert.equal(after.ammo.stats.marketValue, before.ammo.stats.marketValue, 'revert restores the disk value');
  assert.equal(after.ammo.modified, false);
});
