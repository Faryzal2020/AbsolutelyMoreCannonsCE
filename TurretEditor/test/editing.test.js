import test from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, startApp, countOccurrences } from './helpers.js';

const NAVAL = 'Turret_127mmMark16_Base';
const NAVAL_FILE = 'Common/Defs/ThingDefs_Buildings/Naval Guns/127mmMark16.xml';
const VULCAN = 'Turret_20mmM61Vulcan_Base';
const VULCAN_FILE = 'Common/Defs/ThingDefs_Buildings/RotaryCannons/20mmM61Vulcan.xml';

test('editing across every field family', async (t) => {
  const fixture = makeFixture();
  const app = await startApp();
  t.after(async () => { await app.close(); fixture.cleanup(); });

  await app.post('/api/extract');
  const pristineNaval = fixture.read(NAVAL_FILE);
  const pristineVulcan = fixture.read(VULCAN_FILE);

  await t.test('weapon-def fields are written to the weapon ThingDef', async () => {
    await app.put(`/api/turrets/${NAVAL}`, {
      ballistics: { maxRange: 19000, sightsEfficiency: 3.5 },
      ammo: { magazineSize: 60 },
    });

    const { body: diffs } = await app.get(`/api/diffs?defName=${NAVAL}`);
    const keys = diffs.diffs[0].changes.map((c) => c.key);
    assert.ok(keys.includes('ballistics.maxRange'));
    assert.ok(diffs.diffs[0].changes.every((c) => c.doc !== 'weapon' || c.defName === 'Turret_127mmMark16_Weapon'));

    const { body: inject } = await app.post('/api/inject', { defNames: [NAVAL] });
    assert.equal(inject.skippedCount, 0, JSON.stringify(inject.skipped));

    const after = fixture.read(NAVAL_FILE);
    assert.ok(after.includes('<range>19000</range>'), 'direct weapon range updated');
    assert.ok(after.includes('<SightsEfficiency>3.5</SightsEfficiency>'));
    assert.ok(after.includes('<magazineSize>60</magazineSize>'));

    // The indirect weapon in the same file is a different def and must be untouched.
    assert.equal(countOccurrences(after, '<range>23700</range>'), 1);
    assert.equal(countOccurrences(after, '<SightsEfficiency>4</SightsEfficiency>'), 1);
    assert.equal(countOccurrences(after, '<magazineSize>50</magazineSize>'), 1);
  });

  await t.test('nested modExtension and comp values are written in place', async () => {
    await app.put(`/api/turrets/${NAVAL}`, {
      barrel: { recoil: { maxDistance: 0.75, recoilDuration: 22 }, firing: { flashSize: 42 } },
      smoker: { heat: { threshold: 33 }, shockwave: { radius: 9 } },
      comps: { powerWatts: 650, accuracy: { swayReduction: 0.25 } },
    });

    const { body } = await app.post('/api/inject', { defNames: [NAVAL] });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(NAVAL_FILE);
    assert.ok(after.includes('<maxDistance>0.75</maxDistance>'));
    assert.ok(after.includes('<recoilDuration>22</recoilDuration>'));
    assert.ok(after.includes('<flashSize>42</flashSize>'));
    assert.ok(after.includes('<heatThreshold>33</heatThreshold>'));
    assert.ok(after.includes('<shockwaveRadius>9</shockwaveRadius>'));
    assert.ok(after.includes('<basePowerConsumption>650</basePowerConsumption>'));
    assert.ok(after.includes('<swayReduction>0.25</swayReduction>'));

    // Only the direct def changed; the indirect def keeps its own values.
    assert.equal(countOccurrences(after, '<recoilDuration>15</recoilDuration>'), 1);
    assert.equal(countOccurrences(after, '<heatThreshold>20</heatThreshold>'), 1);
    assert.ok(after.includes('<!-- Shockwave Smoke -->'), 'section comments preserved');
  });

  await t.test('cost lists are rewritten as a unit', async () => {
    await app.put(`/api/turrets/${NAVAL}`, {
      costs: [
        { thingDef: 'Steel', count: 1200 },
        { thingDef: 'Plasteel', count: 45 },
        { thingDef: 'Uranium', count: 3 },
      ],
    });
    const { body } = await app.post('/api/inject', { defNames: [NAVAL] });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(NAVAL_FILE);
    assert.ok(after.includes('<Steel>1200</Steel>'));
    assert.ok(after.includes('<Uranium>3</Uranium>'), 'new resource row added');
    assert.equal(countOccurrences(after, '<ComponentSpacer>5</ComponentSpacer>'), 1,
      'removed row is gone from the direct def but kept in the indirect def');
  });

  await t.test('list fields such as selectableBurstCounts round trip', async () => {
    await app.put(`/api/turrets/${VULCAN}`, {
      barrel: { selectableBursts: { enabled: true, counts: '15, 45, 90' } },
    });
    const { body } = await app.post('/api/inject', { defNames: [VULCAN] });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));

    const after = fixture.read(VULCAN_FILE);
    assert.ok(/<selectableBurstCounts>\s*<li>15<\/li>\s*<li>45<\/li>\s*<li>90<\/li>\s*<\/selectableBurstCounts>/.test(after),
      'burst list rewritten');
    assert.ok(!after.includes('<li>20</li>'));

    await app.post('/api/extract');
    const { body: reread } = await app.get(`/api/turrets/${VULCAN}`);
    assert.equal(reread.turret.barrel.selectableBursts.counts, '15, 45, 90');
  });

  await t.test('rotary animation settings survive a full round trip', async () => {
    await app.put(`/api/turrets/${VULCAN}`, {
      barrel: { spinning: { spindownTime: 2.75, frameCount: 8, animationMode: 'Cycling' } },
    });
    await app.post('/api/inject', { defNames: [VULCAN] });

    const after = fixture.read(VULCAN_FILE);
    assert.ok(after.includes('<spindownTime>2.75</spindownTime>'));
    assert.ok(after.includes('<animationMode>Cycling</animationMode>'));
    assert.ok(after.includes('<spinUpSound>AMC_Vulcan_SpinUp</spinUpSound>'), 'sibling untouched');

    const { body } = await app.get(`/api/turrets/${VULCAN}`);
    assert.equal(body.turret.barrel.spinning.frameCount, 8);
  });

  await t.test('toggling a component adds and removes the whole <li>', async () => {
    await app.put(`/api/turrets/${VULCAN}`, { comps: { hasFcs: true } });
    let { body } = await app.post('/api/inject', { defNames: [VULCAN] });
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));
    assert.ok(fixture.read(VULCAN_FILE).includes('AbsolutelyMoreCannons.CompProperties_TurretFCS'));

    await app.put(`/api/turrets/${VULCAN}`, { comps: { hasFcs: false } });
    ({ body } = await app.post('/api/inject', { defNames: [VULCAN] }));
    assert.equal(body.skippedCount, 0, JSON.stringify(body.skipped));
    assert.ok(!fixture.read(VULCAN_FILE).includes('CompProperties_TurretFCS'));
  });

  await t.test('a dry run reports changes without touching disk', async () => {
    const before = fixture.read(NAVAL_FILE);
    await app.put(`/api/turrets/${NAVAL}`, { stats: { mass: 99 } });
    const { body } = await app.post('/api/inject', { dryRun: true });
    assert.equal(body.dryRun, true);
    assert.ok(body.appliedCount > 0);
    assert.equal(fixture.read(NAVAL_FILE), before, 'dry run left the file alone');

    await app.post(`/api/turrets/${NAVAL}/revert`);
    const { body: diffs } = await app.get('/api/diffs');
    assert.equal(diffs.changeCount, 0, 'revert cleared the pending edit');
  });

  await t.test('rollback restores both edited files to their pristine state', async () => {
    const { body } = await app.post('/api/rollback');
    assert.equal(body.restoredCount, 2);
    assert.equal(fixture.read(NAVAL_FILE), pristineNaval);
    assert.equal(fixture.read(VULCAN_FILE), pristineVulcan);
  });
});
