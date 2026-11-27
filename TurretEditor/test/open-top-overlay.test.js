import test from 'node:test';
import assert from 'node:assert/strict';
import { openTopOverlay, rotateVecXZ } from '../ui/src/openTopOverlay.js';

test('open-top seat & shield overlay for the Texture Inspector', async (t) => {
  await t.test('seat rotates with the turret top and the shield wedge is centred on the barrel', () => {
    const turret = {
      comps: { enclosed: { enabled: true, seatOffset: '(0,0,-0.9)', shieldArc: 120, shieldHeight: 0.85, shieldArmor: 6 } },
      barrel: { barrelHeight: 1.1 },
    };
    const o = openTopOverlay(turret, 90);
    // Facing East, a seat 0.9 behind the gun sits 0.9 to the West.
    assert.ok(Math.abs(o.seat.x - -0.9) < 1e-6 && Math.abs(o.seat.z) < 1e-6, JSON.stringify(o.seat));
    assert.deepEqual([o.shield.fromDeg, o.shield.toDeg, o.shield.full], [30, 150, false]);
    assert.equal(o.label, 'shield 0.85 / head 1.1 · 6mm');
  });

  await t.test('a 360 shield is a full ring and the head falls back to 1.45 without barrelHeight', () => {
    const o = openTopOverlay({ comps: { enclosed: { enabled: true, seatOffset: '(0,0,0)', shieldArc: 360 } }, barrel: {} }, 0);
    assert.equal(o.shield.full, true);
    assert.ok(o.label.includes('head 1.45'), o.label);
  });

  await t.test('no seat, no overlay', () => {
    assert.equal(openTopOverlay({ comps: { enclosed: { enabled: true, seatOffset: null } } }, 0), null);
    assert.equal(openTopOverlay({ comps: { enclosed: { enabled: false, seatOffset: '(0,0,-1)' } } }, 0), null);
    assert.equal(openTopOverlay({}, 0), null);
  });

  await t.test('seat without a shield has no wedge', () => {
    const o = openTopOverlay({ comps: { enclosed: { enabled: true, seatOffset: '(0.5,0,0)', shieldArc: 0 } } }, 0);
    assert.equal(o.shield, null);
    assert.ok(Math.abs(o.seat.x - 0.5) < 1e-6);
  });

  await t.test('rotateVecXZ matches the RimWorld clockwise convention', () => {
    const r = rotateVecXZ(0, 1, 90);
    assert.ok(Math.abs(r.x - 1) < 1e-9 && Math.abs(r.z) < 1e-9);
  });
});
