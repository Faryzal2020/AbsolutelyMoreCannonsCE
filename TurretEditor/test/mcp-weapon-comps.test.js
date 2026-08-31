import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_155mmGCT_Base';
const WEAPON = 'Turret_155mmGCT_Weapon';
const INDIRECT = 'Turret_155mmGCT_indirect_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Howitzers/155mmGCT.xml';

const between = (xml, from, to) => xml.slice(xml.indexOf(from), xml.indexOf(to));
const buildingBlock = (xml) =>
  between(xml, `<defName>${TURRET}</defName>`, `<defName>${WEAPON}</defName>`);
const weaponBlock = (xml) =>
  between(xml, `<defName>${WEAPON}</defName>`, `<defName>${INDIRECT}</defName>`);

/**
 * Weapon-side comps and modExtensions. Every toggle before these lived on the
 * building def, so `diffTurret` hardcoded the building's defName and file; a
 * weapon toggle has to route to the gun def instead. These tests pin that down.
 */
test('MCP weapon-side comps and extensions', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const pristine = fixture.read(FILE);

  await t.test('the catalogue marks the new toggles as weapon-side', async () => {
    const { body } = await mcp.call('get_field_catalogue');
    const toggles = new Map(body.toggles.map((x) => [x.key, x]));
    for (const key of ['clamping.enabled', 'gunDraw.enabled', 'fireModes.enabled', 'charges.enabled']) {
      assert.ok(toggles.has(key), `${key} is a toggle`);
    }
    const byKey = new Map(body.fields.map((f) => [f.key, f]));
    assert.equal(byKey.get('clamping.maxRotationDeviation').gate, 'clamping.enabled');
    assert.equal(byKey.get('fireModes.aimedBurstShotCount').type, 'int');
    assert.equal(byKey.get('charges.speeds').type, 'list');
    // An "x,y" pair must not be typed as a list or the comma would split it.
    assert.equal(byKey.get('gunDraw.casingOffset').type, 'string');
  });

  await t.test('they are extracted from the weapon def', async () => {
    const { body } = await mcp.call('list_turrets');
    assert.ok(body.count > 40);

    const { body: clamped } = await mcp.call('get_turret', { defName: 'Turret_76mmOtomelaraC_Base' });
    assert.equal(clamped.turret.clamping.enabled, true);
    assert.equal(clamped.turret.clamping.maxRotationDeviation, 2);
    assert.equal(clamped.turret.fireModes.enabled, true);

    const { body: plain } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(plain.turret.clamping.enabled, false);
    assert.equal(plain.turret.fireModes.enabled, false);
  });

  await t.test('a field behind a switched-off weapon comp is rejected', async () => {
    const { isError, body } = await mcp.call('update_turret', {
      defName: TURRET, changes: { 'fireModes.aiAimMode': 'AimedShot' },
    });
    assert.equal(isError, true);
    assert.equal(body.rejected[0].reason, 'gate-disabled');
    assert.equal(body.rejected[0].gate, 'fireModes.enabled');
  });

  await t.test('enabling the comp and setting its fields works in one call', async () => {
    const { body } = await mcp.call('update_turret', {
      defName: TURRET,
      changes: {
        'fireModes.enabled': true,
        'fireModes.aiAimMode': 'SuppressFire',
        'fireModes.aimedBurstShotCount': 4,
      },
    });
    assert.equal(body.ok, true);
    assert.equal(body.applied.length, 3);
    assert.deepEqual(body.rejected, [], 'the gate opens within the same patch');
  });

  await t.test('the diff targets the weapon def, not the building', async () => {
    const { body } = await mcp.call('get_diffs', { defNames: [TURRET] });
    const diff = body.diffs[0];
    const toggle = diff.changes.find((c) => c.key === 'fireModes.enabled');
    assert.ok(toggle, 'toggle change present');

    // The projection drops defName per change, so check the raw diff too.
    const raw = mcp.db.get('SELECT data_json FROM turrets WHERE def_name = ?', TURRET);
    assert.equal(JSON.parse(raw.data_json).fireModes.aiAimMode, 'SuppressFire');
  });

  await t.test('injection adds the comp to the weapon def only', async () => {
    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.filesWritten, 1);
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, 'CombatExtended.CompProperties_FireModes'), 1,
      'exactly one comp added');
    assert.ok(weaponBlock(after).includes('CombatExtended.CompProperties_FireModes'),
      'it landed on the weapon def');
    assert.ok(!buildingBlock(after).includes('CombatExtended.CompProperties_FireModes'),
      'the building def was left alone');

    // The values from the same call landed alongside the comp.
    assert.ok(weaponBlock(after).includes('<aiAimMode>SuppressFire</aiAimMode>'));
    assert.ok(weaponBlock(after).includes('<aimedBurstShotCount>4</aimedBurstShotCount>'));

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.fireModes.aiAimMode, 'SuppressFire', 're-extracted from disk');
    assert.equal(reread.turret.fireModes.aimedBurstShotCount, 4);
  });

  await t.test('a modExtension toggle routes to the weapon too', async () => {
    await mcp.call('update_turret', {
      defName: TURRET,
      changes: { 'clamping.enabled': true, 'clamping.maxRotationDeviation': 5 },
    });
    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(FILE);
    assert.ok(weaponBlock(after).includes('AbsolutelyMoreCannons.TurretClampingExtension'));
    assert.ok(!buildingBlock(after).includes('AbsolutelyMoreCannons.TurretClampingExtension'));
    assert.ok(weaponBlock(after).includes('<maxRotationDeviation>5</maxRotationDeviation>'));

    // The indirect variant already had one; it must still be there, untouched.
    assert.equal(countOccurrences(after, 'AbsolutelyMoreCannons.TurretClampingExtension'), 2);
  });

  await t.test('removing a weapon comp deletes it from the weapon def', async () => {
    await mcp.call('update_turret', { defName: TURRET, changes: { 'fireModes.enabled': false } });
    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, 'CombatExtended.CompProperties_FireModes'), 0);
  });

  await t.test('charge speeds round trip as a list', async () => {
    const CHARGED = 'Turret_150mmSIG33_indirect_Base';
    const { body: before } = await mcp.call('get_turret', { defName: CHARGED });
    assert.equal(before.turret.charges.enabled, true);
    assert.equal(before.turret.charges.speeds, '30, 60, 90, 120');

    const { body } = await mcp.call('update_turret', {
      defName: CHARGED, changes: { 'charges.speeds': '25, 50, 75, 100, 125' },
    });
    assert.equal(body.ok, true);

    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [CHARGED], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const { body: reread } = await mcp.call('get_turret', { defName: CHARGED });
    assert.equal(reread.turret.charges.speeds, '25, 50, 75, 100, 125');
  });

  await t.test('rollback restores every touched file', async () => {
    await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(fixture.read(FILE), pristine, 'file restored byte for byte');
  });
});
