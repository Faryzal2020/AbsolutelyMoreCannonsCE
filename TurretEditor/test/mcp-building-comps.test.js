import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startMcp, countOccurrences } from './helpers.js';

const TURRET = 'Turret_100mmModel1953_Base';
const INDIRECT = 'Turret_100mmModel1953_indirect_Base';
const FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/100mmModel1953.xml';
const OTOMELARA = 'Turret_76mmOtomelaraC_Base';

/**
 * Building-side comps and modExtensions that were previously invisible to the
 * editor, plus the identity and placement fields.
 */
test('MCP building-side comps and identity fields', async (t) => {
  const fixture = makeFixture();
  const mcp = await startMcp();
  t.after(async () => { await mcp.close(); fixture.cleanup(); });

  await mcp.call('extract_defs');
  const pristine = fixture.read(FILE);

  await t.test('previously invisible extensions are extracted', async () => {
    const { body } = await mcp.call('get_turret', { defName: OTOMELARA });
    const turret = body.turret;
    assert.equal(turret.nonSnap.enabled, true);
    assert.equal(turret.nonSnap.speed, 0.4);
    assert.equal(turret.nonSnap.preferedAngleRange, 30);
    assert.equal(turret.nonSnap.angleWeightMultiplier, 1.5);
    assert.equal(turret.nonSnap.minAngleWeight, 0.1);
    assert.equal(turret.tracer.enabled, true);
    assert.equal(turret.tracer.lineOffset, '(0, 0, 1.8)', 'a tuple stays one string');
    assert.equal(turret.viewTransfer.enabled, true);
  });

  await t.test('identity and placement fields are extracted', async () => {
    const { body } = await mcp.call('get_turret', { defName: TURRET });
    const turret = body.turret;
    assert.match(turret.size, /^\d+,\d+$/, 'size is an x,y string, not a split list');
    assert.ok(turret.researchPrerequisites.length > 0);
    assert.ok(turret.textures.building.length > 0);
    assert.ok(turret.textures.icon.length > 0);
    assert.equal(turret.warnings.filter((w) => w.rule === 'texture').length, 0,
      'texture audit still resolves after the field map took the paths over');
  });

  await t.test('a field behind a missing extension is rejected', async () => {
    const { isError, body } = await mcp.call('update_turret', {
      defName: TURRET, changes: { 'nonSnap.speed': 0.8 },
    });
    assert.equal(isError, true);
    assert.equal(body.rejected[0].reason, 'gate-disabled');
    assert.equal(body.rejected[0].gate, 'nonSnap.enabled');
  });

  await t.test('adding NonSnapTurretExtension with its own speed works in one call', async () => {
    const { body } = await mcp.call('update_turret', {
      defName: TURRET,
      changes: {
        'nonSnap.enabled': true,
        'nonSnap.speed': 0.8,
        'nonSnap.preferedAngleRange': 45,
      },
    });
    assert.equal(body.ok, true);
    assert.deepEqual(body.rejected, []);

    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const after = fixture.read(FILE);
    assert.equal(countOccurrences(after, 'CombatExtended.NonSnapTurretExtension'), 1,
      'added to the target def only');
    assert.equal(countOccurrences(after, '<speed>0.8</speed>'), 1);
    assert.equal(countOccurrences(after, '<preferedAngleRange>45</preferedAngleRange>'), 1);

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.nonSnap.speed, 0.8, 're-extracted from disk');
    assert.equal(reread.turret.nonSnap.preferedAngleRange, 45);

    const { body: sibling } = await mcp.call('get_turret', { defName: INDIRECT });
    assert.equal(sibling.turret.nonSnap.enabled, false, 'the variant in the same file is untouched');
  });

  await t.test('per-turret values need one call each, and they compose', async () => {
    // batch_update_turrets applies uniform ops, so distinct values per turret
    // are separate calls. This is the shape a client has to use today.
    const targets = [[TURRET, 1.2], [INDIRECT, 0.3]];
    for (const [defName, speed] of targets) {
      const { body } = await mcp.call('update_turret', {
        defName, changes: { 'nonSnap.enabled': true, 'nonSnap.speed': speed },
      });
      assert.equal(body.ok, true, defName);
    }
    const { body } = await mcp.call('inject_xml_changes', { dryRun: false });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    for (const [defName, speed] of targets) {
      const { body: check } = await mcp.call('get_turret', { defName });
      assert.equal(check.turret.nonSnap.speed, speed, `${defName} kept its own speed`);
    }
    assert.equal(countOccurrences(fixture.read(FILE), 'CombatExtended.NonSnapTurretExtension'), 2);
  });

  await t.test('a nested container is created on demand', async () => {
    // comps.fireArc.spanMin lives at comps > li[FireArc] > spanRange > min, and
    // the <spanRange> wrapper has to be written along with the value.
    const { body } = await mcp.call('update_turret', {
      defName: TURRET,
      changes: { 'comps.hasFireArc': true, 'comps.fireArc.spanMin': 45, 'comps.fireArc.spanMax': 200 },
    });
    assert.equal(body.ok, true);

    const { body: inject } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.comps.fireArc.spanMin, 45);
    assert.equal(reread.turret.comps.fireArc.spanMax, 200);
  });

  await t.test('a marker comp toggles with no fields of its own', async () => {
    const { body: before } = await mcp.call('get_turret', { defName: TURRET });
    const was = before.turret.comps.hasPreserveAmmo;

    await mcp.call('update_turret', { defName: TURRET, changes: { 'comps.hasPreserveAmmo': !was } });
    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.comps.hasPreserveAmmo, !was, 'marker comp flipped on disk');
  });

  await t.test('an identity field round trips', async () => {
    await mcp.call('update_turret', {
      defName: TURRET, changes: { 'terrainAffordanceNeeded': 'Medium', 'pathCost': 60 },
    });
    const { body } = await mcp.call('inject_xml_changes', { defNames: [TURRET], dryRun: false });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const { body: reread } = await mcp.call('get_turret', { defName: TURRET });
    assert.equal(reread.turret.terrainAffordanceNeeded, 'Medium');
    assert.equal(reread.turret.pathCost, 60);
  });

  await t.test('rollback restores every touched file', async () => {
    await mcp.call('rollback_xml', { dryRun: false });
    assert.equal(fixture.read(FILE), pristine, 'file restored byte for byte');
  });
});
