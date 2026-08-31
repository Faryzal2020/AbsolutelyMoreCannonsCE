import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_30mmFV510_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Autocannons/30mmFV510.xml';

/**
 * Fields filled in on comps that already had toggles: barrel graphics, muzzle
 * flash, mode-swap gizmo, smoker offsets and the <building> sub-block.
 */
test('MCP field fill-in on existing comps', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const pristine = fixture.read(FILE);

  await t.test('barrel graphics and muzzle flash are extracted', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    const b = body.turret.barrel;
    assert.equal(b.graphic.graphicClass, 'Graphic_Single');
    assert.match(b.graphic.texPath, /Barrel$/);
    assert.equal(b.graphic.drawSize, '(3,3)', 'a tuple stays one string');
    assert.equal(b.firing.muzzleFlashEffect, 'AMC_MuzzleFlashLight');
    assert.equal(typeof b.firing.flashOffset, 'number');
  });

  await t.test('smoker offsets and timings are extracted into the right groups', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    const s = body.turret.smoker;
    assert.equal(s.muzzle.offset, '(0, 0, 4)');
    assert.equal(s.muzzle.spawnDelay, 1);
    assert.equal(s.muzzle.spawnDuration, 5);
    assert.equal(s.heat.offset, '(0, 0, 1.5)');
    assert.equal(s.heat.decayIncrease, 0.08);
    assert.equal(s.heat.emissionIncrease, 1);
    assert.equal(s.heat.emissionPoints, 3);
    assert.equal(s.heat.emissionSpacing, 0.5);
  });

  await t.test('the building sub-block and placement fields are extracted', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(body.turret.building.aiCombatDangerous, true);
    assert.ok(body.turret.building.spawnedConceptLearnOpportunity.length > 0);
    assert.match(body.turret.placeWorkers, /PlaceWorker_TurretTop/);
    assert.match(body.turret.graphics.shadowVolume, /^\(.*\)$/);
  });

  await t.test('a mode-swap gizmo field round trips', async () => {
    const swap = (await mcp.call('list_turrets')).body.turrets
      .map((x) => x.defName)
      .find((d) => d === 'Turret_127mmMark16_Base');
    const { body: before } = await mcp.call('get_turret', { defName: swap });
    assert.ok(before.turret.comps.swapGizmoIcon.length > 0, 'gizmoIcon extracted');
  });

  await t.test('a nested graphic field is written back in place', async () => {
    const { body } = await mcp.call('update_turret', {
      defName: TURRET,
      changes: { 'barrel.graphic.drawSize': '(4,4)', 'barrel.firing.muzzleFlashEffect': 'AMC_MuzzleFlashHeavy' },
    });
    assert.equal(body.ok, true);
    assert.equal(body.applied.length, 2);

    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, '<drawSize>(4,4)</drawSize>'), 1);
    assert.equal(countOccurrences(after, '<muzzleFlashEffect>AMC_MuzzleFlashHeavy</muzzleFlashEffect>'), 1);

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.barrel.graphic.drawSize, '(4,4)', 're-extracted from disk');
  });

  await t.test('a smoker offset round trips without being split on its commas', async () => {
    await mcp.call('update_turret', {
      defName: TURRET, changes: { 'smoker.heat.offset': '(0, 0, 2.5)', 'smoker.heat.emissionPoints': 5 },
    });
    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, '<heatOffset>(0, 0, 2.5)</heatOffset>'), 1, 'written as one tag');
    assert.equal(countOccurrences(after, '<li>(0, 0, 2.5)</li>'), 0, 'not turned into a list');

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.smoker.heat.emissionPoints, 5);
  });

  await t.test('every catalogue key resolves against a real turret record', async () => {
    // A catalogue key with no branch in blankTurret() reads as undefined for
    // every turret, so it would be advertised but never show a value. This
    // catches that drift; it cannot tell whether a field is genuinely used,
    // since an unset bool legitimately reads false.
    const { body: cat } = await mcp.call('get_field_catalogue');
    const { body: list } = await mcp.call('list_turrets');
    const records = [];
    for (const row of list.turrets) {
      records.push((await mcp.call('get_turret', { defName: row.defName })).body.turret);
    }
    const get = (obj, key) => key.split('.').reduce((a, k) => (a == null ? undefined : a[k]), obj);
    const missing = cat.fields.filter((f) => records.every((r) => get(r, f.key) === undefined));
    assert.deepEqual(missing, [], 'every field key exists on the record skeleton');
  });

  await t.test('rollback restores the file', async () => {
    await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(fixture.read(FILE), pristine);
  });
});
