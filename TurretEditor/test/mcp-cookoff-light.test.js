import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_127mmMark16_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';

/** The XML value of a tag inside the def block of `defName`. */
function tagInDef(xml, defName, tag) {
  const start = xml.indexOf(`<defName>${defName}</defName>`);
  const end = xml.indexOf('</ThingDef>', start);
  const m = xml.slice(start, end).match(new RegExp(`<${tag}>([^<]+)</${tag}>`));
  return m ? m[1] : null;
}

/**
 * Ammo cook-off, light penalty, barrel actuator and gun mass fields: used by
 * most turrets, previously invisible to the editor.
 */
test('MCP cook-off, light-penalty and actuator fields', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const pristine = fixture.read(FILE);

  await t.test('values are extracted from the XML', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    const turret = body.turret;
    assert.equal(turret.cookOff.enabled, true);
    assert.equal(turret.cookOff.maxDamage, Number(tagInDef(pristine, TURRET, 'maxDamage')));
    assert.equal(turret.cookOff.maxExplosiveRadius, Number(tagInDef(pristine, TURRET, 'maxExplosiveRadius')));
    assert.equal(turret.barrel.barrelHeight, Number(tagInDef(pristine, TURRET, 'barrelHeight')));
    assert.equal(turret.ballistics.weaponMass, Number(tagInDef(pristine, turret.weaponDefName, 'Mass')));
  });

  await t.test('the catalogue lists the new keys and toggles', async () => {
    const { body } = await mcp.call('get_field_catalogue');
    const keys = body.fields.map((f) => f.key);
    for (const key of ['cookOff.maxDamage', 'lightPenalty.litPenaltyMultiplier', 'barrel.barrelHeight', 'ballistics.weaponMass']) {
      assert.ok(keys.includes(key), key);
    }
    assert.ok(body.toggles.some((x) => x.key === 'cookOff.enabled'));
    assert.ok(body.toggles.some((x) => x.key === 'lightPenalty.enabled'));
  });

  await t.test('a cook-off value round trips through injection', async () => {
    const { body } = await mcp.call('update_turret', {
      defName: TURRET, changes: { 'cookOff.maxDamage': 333, 'barrel.barrelHeight': 2.6 },
    });
    assert.equal(body.ok, true, JSON.stringify(body.rejected));
    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const xml = fixture.read(FILE);
    assert.equal(tagInDef(xml, TURRET, 'maxDamage'), '333');
    assert.equal(tagInDef(xml, TURRET, 'barrelHeight'), '2.6');
  });

  await t.test('the light-penalty extension is added to the gun def', async () => {
    const { body: list } = await mcp.call('list_turrets');
    let target = null;
    for (const row of list.turrets) {
      const { body } = await mcp.call('get_turret', { defName: row.defName });
      if (body.turret.weaponDefName && !body.turret.lightPenalty.enabled) { target = body.turret; break; }
    }
    assert.ok(target, 'some turret has no light penalty yet');

    const { body } = await mcp.call('update_turret', {
      defName: target.defName,
      changes: { 'lightPenalty.enabled': true, 'lightPenalty.litPenaltyMultiplier': 0.4 },
    });
    assert.equal(body.ok, true, JSON.stringify(body.rejected));
    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [target.defName], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const xml = fixture.read(target.weaponFilePath || target.filePath);
    assert.equal(tagInDef(xml, target.weaponDefName, 'litPenaltyMultiplier'), '0.4', 'written into the gun def');
    const buildingXml = fixture.read(target.filePath);
    const start = buildingXml.indexOf(`<defName>${target.defName}</defName>`);
    const block = buildingXml.slice(start, buildingXml.indexOf('</ThingDef>', start));
    assert.equal(countOccurrences(block, 'TurretLightPenaltyExtension'), 0, 'not added to the building def');

    const { body: reread } = await mcp.call('get_turret', { defName: target.defName });
    assert.equal(reread.turret.lightPenalty.enabled, true);
    assert.equal(reread.turret.lightPenalty.litPenaltyMultiplier, 0.4);
  });

  await t.test('rollback restores the file', async () => {
    await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(fixture.read(FILE), pristine);
  });
});
