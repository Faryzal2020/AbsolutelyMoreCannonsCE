import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_127mmMark16_Base';
const WEAPON = 'Turret_127mmMark16_Weapon';
const INDIRECT = 'Turret_127mmMark16_indirect_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';

/** The direct weapon's block, so a write can be proved to land in the right def. */
const weaponBlock = (xml) =>
  xml.slice(xml.indexOf(`<defName>${WEAPON}</defName>`), xml.indexOf(`<defName>${INDIRECT}</defName>`));

/**
 * The shoot-verb fields added to the field map: recoil, sound, targeting and
 * line-of-sight, all inside the <li Class="...VerbPropertiesCE"> element.
 */
test('MCP shoot-verb fields', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const pristine = fixture.read(FILE);

  await t.test('the catalogue lists the new verb keys with types', async () => {
    const { body } = await mcp.call('get_field_catalogue');
    const byKey = new Map(body.fields.map((f) => [f.key, f]));
    for (const key of [
      'ballistics.defaultProjectile', 'ballistics.recoilAmount', 'ballistics.recoilPattern',
      'ballistics.muzzleFlashScale', 'ballistics.soundCast', 'ballistics.soundCastTail',
      'ballistics.circularError', 'ballistics.indirectFirePenalty', 'ballistics.requireLineOfSight',
      'ballistics.stopBurstWithoutLos', 'ballistics.ignorePartialLoSBlocker',
      'ballistics.forceNormalTimeSpeed', 'ballistics.hasStandardCommand',
      'ballistics.canTargetLocations',
    ]) {
      assert.ok(byKey.has(key), `${key} is in the catalogue`);
      assert.equal(byKey.get(key).def, 'weapon');
    }
    assert.equal(byKey.get('ballistics.recoilAmount').type, 'float');
    assert.equal(byKey.get('ballistics.defaultProjectile').type, 'string');
    assert.equal(byKey.get('ballistics.requireLineOfSight').type, 'bool');
  });

  await t.test('they are extracted from the verb element', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    const b = body.turret.ballistics;
    assert.equal(b.defaultProjectile, 'Bullet_127mmMark16_HE');
    assert.equal(b.recoilAmount, 0.2);
    assert.equal(b.recoilPattern, 'Mounted');
    assert.equal(b.soundCast, 'Sound_Single127mm');
    assert.equal(b.soundCastTail, 'GunTail_Heavy');
    assert.equal(b.muzzleFlashScale, 100);
    assert.equal(b.ignorePartialLoSBlocker, true);
    assert.equal(b.canTargetLocations, true);
    assert.equal(b.circularError, null, 'absent tags stay null');
  });

  await t.test('an existing verb tag is replaced in place', async () => {
    const { body } = await mcp.call('update_turret', {
      defName: TURRET,
      changes: { 'ballistics.recoilAmount': 0.5, 'ballistics.soundCastTail': 'GunTail_Light' },
    });
    assert.equal(body.ok, true);
    assert.equal(body.applied.length, 2);

    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, '<recoilAmount>0.5</recoilAmount>'), 1);
    assert.equal(countOccurrences(after, '<recoilAmount>0.2</recoilAmount>'), 0, 'old value replaced');
    assert.equal(countOccurrences(after, '<recoilAmount>0</recoilAmount>'), 1,
      'the indirect variant kept its own value');
    assert.equal(countOccurrences(pristine, '<soundCastTail>GunTail_Heavy</soundCastTail>'), 2);
    assert.equal(countOccurrences(after, '<soundCastTail>GunTail_Heavy</soundCastTail>'), 1,
      'only the targeted weapon changed');
  });

  await t.test('a verb tag absent from disk is created inside the existing verb', async () => {
    assert.ok(!weaponBlock(pristine).includes('<circularError>'),
      'the direct weapon has no circularError tag to start with');
    assert.equal(countOccurrences(pristine, '<circularError>'), 1, 'only the indirect weapon has one');

    await mcp.call('update_turret', { defName: TURRET, changes: { 'ballistics.circularError': 1.5 } });
    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, '<circularError>1.5</circularError>'), 1, 'tag created');
    assert.ok(weaponBlock(after).includes('<circularError>1.5</circularError>'),
      'it landed inside the direct weapon, not elsewhere in the file');
    assert.equal(countOccurrences(after, '<circularError>1</circularError>'), 1,
      'the indirect weapon is untouched');

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.ballistics.circularError, 1.5, 're-extracted from disk');
  });

  await t.test('a boolean verb field round trips', async () => {
    await mcp.call('update_turret', {
      defName: TURRET, changes: { 'ballistics.requireLineOfSight': true },
    });
    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));
    assert.ok(weaponBlock(fixture.read(FILE)).includes('<requireLineOfSight>true</requireLineOfSight>'));
  });

  await t.test('rollback restores the file byte for byte', async () => {
    const { body } = await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(body.restoredCount, 1);
    assert.equal(fixture.read(FILE), pristine);
  });
});
