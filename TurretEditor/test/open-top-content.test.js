// Content rules for open-top turrets (CompProperties_EnclosedTurret with <seatOffset>) in the real mod defs.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'Common', 'Defs', 'ThingDefs_Buildings');

function* xmlFiles(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) yield* xmlFiles(p);
    else if (e.name.endsWith('.xml')) yield p;
  }
}

function openTopDefs() {
  const out = [];
  for (const file of xmlFiles(ROOT)) {
    const src = fs.readFileSync(file, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
    for (const m of src.matchAll(/<ThingDef\b[\s\S]*?<\/ThingDef>/g)) {
      const def = m[0];
      const comp = def.match(/<li Class="AbsolutelyMoreCannons\.CompProperties_EnclosedTurret">([\s\S]*?)<\/li>/);
      if (!comp || !comp[1].includes('<seatOffset>')) continue;
      const tag = (t) => comp[1].match(new RegExp(`<${t}>([^<]*)</${t}>`))?.[1];
      out.push({ file: path.basename(file), defName: def.match(/<defName>([^<]+)<\/defName>/)?.[1], def, tag });
    }
  }
  return out;
}

test('open-top turret content', async (t) => {
  const defs = openTopDefs();

  await t.test('there are open-top turrets to check', () => {
    assert.ok(defs.length >= 8, `found ${defs.length}`);
  });

  await t.test('open-top gunners are not immune to melee, unclassified, fire or temperature damage', () => {
    for (const d of defs) {
      for (const field of ['meleeProtection', 'generalProtection', 'flameProtection', 'temperatureProtection']) {
        // Unset means the C# default of 1.0 (total immunity), which is wrong for an open mount.
        assert.equal(d.tag(field), '0', `${d.defName} (${d.file}) ${field} must be 0, got ${d.tag(field) ?? 'unset (1.0)'}`);
      }
    }
  });

  await t.test('front-shield guns do not also grant blanket operator suppression immunity', () => {
    for (const d of defs) {
      if (Number(d.tag('shieldArc')) >= 360) continue;
      const blanket = /TurretSuppressionImmunityExtension[\s\S]*?<preventOperatorSuppression>\s*true/.test(d.def);
      assert.ok(!blanket, `${d.defName} (${d.file}) keeps TurretSuppressionImmunityExtension preventOperatorSuppression, which overrides directional suppression`);
    }
  });
});
