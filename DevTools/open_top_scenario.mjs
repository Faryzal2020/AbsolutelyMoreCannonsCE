#!/usr/bin/env node
// In-game scenario checks for open-top turret seats & gun shields, driven through RimTestHarness.
//
//   node DevTools/open_top_scenario.mjs --port=18573 --turret=Turret_20mmFlak38_Base --case=front
//
// Cases: seat | front | rear | ap | he | ownfire | despawn | reload | enclosed
// Prints one JSON line {case, turret, pass, details}; exit 0 on pass, 1 on fail, 2 on setup error.
// Each case starts from a fresh quicktest game, so cases are independent.
//
// Spec: docs/superpowers/specs/2026-11-27-open-top-turret-seat-shield-design.md

const args = Object.fromEntries(process.argv.slice(2).map((a) => {
  const m = a.match(/^--([^=]+)=?(.*)$/);
  return m ? [m[1], m[2] === '' ? true : m[2]] : [a, true];
}));
const PORT = Number(args.port || 18573);
const TURRET = args.turret || 'Turret_20mmFlak38_Base';
const CASE = args.case || 'front';
const SHOOTER_KIND = args.shooter || 'Mercenary_Gunner';
const SHOOT_TICKS = Number(args.ticks || 900);

// Seat offsets / shield config mirrored from the defs under test (turret-top frame, compass degrees).
const SEATS = {
  Turret_20mmFlak38_Base: { seat: [0, -0.9], arc: 120, size: 3, shield: 0.85 },
  Turret_40mmDCA40_Base: { seat: [0.7, -0.7], arc: 360, size: 4, shield: 0.95 },
  Turret_37mmFlakM42_Base: { seat: [0, -0.9], arc: 120, size: 3, shield: 1.45 },
  Turret_75mmLEIG18_Base: { seat: [0, -0.9], arc: 100, size: 3, shield: 0.75 },
  Turret_150mmSIG33_Base: { seat: [0, -0.9], arc: 100, size: 3, shield: 0.95 },
  Turret_233mmBLMK1_Base: { seat: [0, -1.5], arc: 100, size: 4, shield: 1.6 },
  Turret_57mmDeacon_Base: { seat: [0.7, -0.7], arc: 360, size: 3, shield: 1.2 },
  Turret_20mmM61Vulcan_Base: { seat: [0.7, -0.7], arc: 360, size: 4, shield: 1.75 },
};
// Unmanned player turrets: no gunner for the hostile turret (NPC turrets get free ammo) to kill,
// and they auto-target the only hostile pawn left after "Destroy non-colonists".
const HULL_TOP = 0.7; // TurretBuilding_Base fillPercent

const ATTACKERS = {
  ap: { def: 'Turret_U20mmFlak38_Base', ammo: 'Ammo_20x138mm_AP' },
  he: { def: 'Turret_U40mmDCA40_Base', ammo: 'Ammo_40x365mm_HE' },
};

class SetupError extends Error {}

async function call(cmd, body = {}, timeoutMs = 120000) {
  const res = await fetch(`http://127.0.0.1:${PORT}/${cmd}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ ...body, timeout_ms: timeoutMs }),
  });
  const json = JSON.parse(await res.text());
  if (!json.ok) throw new SetupError(`${cmd} ${JSON.stringify(body)} failed: ${json.error}`);
  return json.result;
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const tick = (n) => call('tick', { n });
const thing = async (id) => (await call('query_things', { id })).things[0];

function rotate(x, z, deg) {
  const r = (deg * Math.PI) / 180;
  return { x: x * Math.cos(r) + z * Math.sin(r), z: -x * Math.sin(r) + z * Math.cos(r) };
}
function dirVec(deg) {
  const r = (deg * Math.PI) / 180;
  return { x: Math.sin(r), z: Math.cos(r) };
}

async function freshGame() {
  await call('new_quicktest_game');
  for (let i = 0; i < 240; i++) {
    await sleep(500);
    try {
      const s = await call('status', {}, 5000);
      if (s.programState === 'Playing' && !s.anyLongEvent) break;
    } catch { /* scene switching */ }
  }
  await call('dev_mode', { on: true });
  await call('set_debug', { field: 'unlimitedPower', value: true });
  await call('set_debug', { field: 'enableStoryteller', value: false }).catch(() => {});
  // Wildlife and raiders soak shots and pull turrets around; remove them.
  await call('debug_action', { path: 'Actions\\Destroy non-colonists' }).catch(() => {});
}

async function logCount(contains, since, alsoContains = null) {
  const r = await call('log', { contains, since, limit: 5000 });
  return alsoContains ? r.entries.filter((e) => e.text.includes(alsoContains)).length : r.entries.length;
}
async function logCursor() {
  return (await call('status')).logCount;
}

async function loadAmmo(turretId, preferred) {
  const t = await thing(turretId);
  const ammo = preferred || t.turret?.ammo?.currentAmmo;
  if (ammo) await call('turret_ammo', { id: turretId, ammoDef: ammo });
}

// Spawns a manned turret at (x,z) and returns {turret, gunner}.
async function mannedTurret(def, x, z, ammo, faction = 'player') {
  const turret = await call('spawn_thing', { defName: def, x, z, faction });
  if (faction === 'player') {
    await call('turret_fcs', { id: turret.id }).catch(() => {}); // only turrets with an FCS comp
    await loadAmmo(turret.id, ammo);
  }
  const gunner = await call('spawn_pawn', { kindDef: 'Colonist', faction, x, z: z - 3 });
  await call('order_job', { pawnId: gunner.id, jobDef: 'ManTurret', targetId: turret.id });
  for (let i = 0; i < 20; i++) {
    await tick(30);
    const gp = await thing(gunner.id);
    if (!gp) throw new SetupError(`gunner of ${def} vanished during setup`);
    if (gp.pawn.curJob === 'ManTurret') return { turret: await thing(turret.id), gunner: await thing(gunner.id) };
  }
  throw new SetupError(`gunner never started ManTurret on ${def}`);
}

// Turn the turret top to face compass `deg` by ordering an attack on a far cell, then clear it.
async function faceTurret(turret, deg) {
  const d = dirVec(deg);
  await call('turret_order_attack', { id: turret.id, x: Math.round(turret.x + d.x * 20), z: Math.round(turret.z + d.z * 20) });
  await tick(150);
  await call('turret_order_attack', { id: turret.id, clear: true }).catch(() => {});
  return (await thing(turret.id)).turret.topRotation;
}

async function shooterAt(turret, fromDeg, targetId, dist = 10) {
  const d = dirVec(fromDeg);
  const s = await call('spawn_pawn', { kindDef: SHOOTER_KIND, faction: 'player', x: Math.round(turret.x + d.x * dist), z: Math.round(turret.z + d.z * dist) });
  await call('draft', { pawnId: s.id, on: true });
  await call('order_job', { pawnId: s.id, jobDef: 'AttackStatic', targetId });
  return s;
}

async function impacts(sinceSeq = 0) {
  const r = await call('events', { type: 'impact', since: sinceSeq, limit: 5000 });
  return r.events;
}

async function setup(def = TURRET) {
  await freshGame();
  let c = null;
  for (const clearRadius of [10, 8, 6, 5]) {
    c = await call('find_cell', { x: 60, z: 190, radius: 120, clearRadius }).catch(() => null);
    if (c) break;
  }
  if (!c) throw new SetupError('no clear cell found');
  const m = await mannedTurret(def, c.x, c.z);
  const top = await faceTurret(m.turret, 0);
  await call('telemetry_stop').catch(() => {}); // a previous failed run may have left one open
  await call('telemetry_start', { runId: `${CASE}-${def}-${Date.now()}` });
  return { ...m, top, cell: c };
}

const CASES = {
  async seat() {
    const { turret, gunner, top } = await setup();
    await shooterAt(turret, 180, gunner.id);
    // The hull correctly takes most low rear shots, so keep firing (up to 4 windows) until the gunner is hit.
    for (let i = 0; i < 4; i++) {
      await tick(SHOOT_TICKS);
      if ((await impacts()).some((e) => e.other_id === gunner.id)) break;
    }
    const cfg = SEATS[TURRET];
    const off = rotate(cfg.seat[0], cfg.seat[1], top);
    // GenThing.TrueCenter (rot North): cell centre, plus 0.5 per even footprint dimension.
    const t = await thing(turret.id);
    const even = (cfg.size ?? 3) % 2 === 0 ? 0.5 : 0;
    const cx = t.x + 0.5 + even;
    const cz = t.z + 0.5 + even;
    const seat = { x: cx + off.x, z: cz + off.z };
    const allImpacts = await impacts();
    const onGunner = allImpacts.filter((e) => e.other_id === gunner.id);
    // A hit counts only if it lands on the seat, i.e. nearer the seat than the turret centre and within 0.5.
    const near = onGunner.filter((e) => {
      const dSeat = Math.hypot(e.x - seat.x, e.z - seat.z);
      return dSeat <= 0.5 && dSeat < Math.hypot(e.x - cx, e.z - cz);
    });
    return { pass: near.length >= 1, details: { top, seat, center: { x: cx, z: cz }, gunnerHits: onGunner.map((e) => [e.x, e.z]).slice(0, 5), near: near.length, totalImpacts: allImpacts.length, sample: allImpacts.slice(0, 8).map((e) => [e.other_id === turret.id ? 'TURRET' : e.other_id, e.detail, +e.x.toFixed(2), +e.y.toFixed(2), +e.z.toFixed(2)]) } };
  },

  async front() {
    const { turret, gunner } = await setup();
    const cur = await logCursor();
    await shooterAt(turret, 0, gunner.id);
    // Only shots between hull height and shield height reach the shield, so keep firing until one is stopped.
    let stops = 0;
    for (let i = 0; i < 8 && stops === 0; i++) {
      await tick(SHOOT_TICKS);
      stops = await logCount('[AMC OpenTop] shield stop', cur);
    }
    const all = await impacts();
    const onTurret = all.filter((e) => e.other_id === turret.id).length;
    // Gunner hits are allowed only above the shield (head/upper body exposed).
    const shieldH = SEATS[TURRET]?.shield ?? 0;
    const rawYs = all.filter((e) => e.other_id === gunner.id).map((e) => e.y);
    const gunnerYs = rawYs.map((y) => +y.toFixed(3));
    const below = rawYs.filter((y) => y <= shieldH); // raw heights: rounding made 0.951 look like 0.95
    // Nothing below the shield may reach the gunner from the front; the shield or hull must have engaged.
    return { pass: below.length === 0 && stops + onTurret >= 1, details: { stops, onTurret, gunnerYs, belowShield: below.length, shieldH, totalImpacts: all.length, others: all.filter((e) => e.other_id !== gunner.id && e.other_id !== turret.id).slice(0, 5).map((e) => [e.detail, +e.y.toFixed(2)]) } };
  },

  async rear() {
    const { turret, gunner } = await setup();
    const cur = await logCursor();
    await shooterAt(turret, 180, gunner.id);
    let gunnerYs = [];
    for (let i = 0; i < 4 && gunnerYs.length === 0; i++) {
      await tick(SHOOT_TICKS);
      gunnerYs = (await impacts()).filter((e) => e.other_id === gunner.id).map((e) => e.y);
    }
    const stops = await logCount('[AMC OpenTop] shield stop', cur);
    const full = (SEATS[TURRET]?.arc ?? 0) >= 360;
    const shieldH = SEATS[TURRET]?.shield ?? 0;
    // Front-only shield: rear shots are never stopped and do reach the gunner.
    // 360 shield: the gunner is hit only above the shield.
    // Hull first: nothing at or below the turret hull (fillPercent 0.7) may reach the gunner from any side.
    const belowHull = gunnerYs.filter((y) => y <= HULL_TOP);
    const pass = belowHull.length === 0 && (full ? gunnerYs.every((y) => y > shieldH) : gunnerYs.length >= 1 && stops === 0);
    return { pass, details: { gunnerYs: gunnerYs.map((y) => +y.toFixed(3)), belowHull: belowHull.length, stops, fullShield: full, shieldH } };
  },

  async ap() { return attackerCase(ATTACKERS.ap, '[AMC OpenTop] shield pen', true); },
  async he() { return attackerCase(ATTACKERS.he, null, true); },

  async ownfire() {
    const { turret, gunner, cell } = await setup();
    const all0 = (await impacts()).length;
    await call('turret_order_attack', { id: turret.id, x: cell.x, z: cell.z + 25 });
    await tick(SHOOT_TICKS * 2);
    const own = (await impacts()).slice(all0).filter((e) => e.other_id === gunner.id || e.other_id === turret.id);
    return { pass: own.length === 0, details: { ownHits: own.length } };
  },

  async despawn() {
    const { turret, gunner, cell } = await setup();
    const errs0 = (await call('status')).errors;
    await shooterAt(turret, 180, gunner.id);
    await tick(Math.floor(SHOOT_TICKS / 2));
    await call('order_job', { pawnId: gunner.id, jobDef: 'Goto', targetX: cell.x - 8, targetZ: cell.z - 8 });
    await tick(SHOOT_TICKS);
    const errs1 = (await call('status')).errors;
    const lines = (await call('log', { level: 'error', limit: 50, since: 0 })).entries
      .filter((e) => /OpenTop|EnclosedTurret|NullReference/.test(JSON.stringify(e)));
    return { pass: errs1 === errs0, details: { newErrors: errs1 - errs0, sample: lines.slice(-3) } };
  },

  async reload() {
    const { turret, gunner } = await setup();
    await call('save_game', { name: 'opentop_reload' });
    await call('load_save', { name: 'opentop_reload' });
    for (let i = 0; i < 240; i++) {
      await sleep(500);
      try { const s = await call('status', {}, 5000); if (s.programState === 'Playing' && !s.anyLongEvent) break; } catch { }
    }
    await call('dev_mode', { on: true });
    await call('order_job', { pawnId: gunner.id, jobDef: 'ManTurret', targetId: turret.id });
    await tick(120);
    const t = await thing(turret.id);
    await faceTurret(t, 0);
    await call('telemetry_start', { runId: `reload2-${Date.now()}` }).catch(() => {});
    const cur = await logCursor();
    await shooterAt(t, 0, gunner.id);
    let stops = 0;
    for (let i = 0; i < 5 && stops === 0; i++) {
      await tick(SHOOT_TICKS);
      stops = await logCount('[AMC OpenTop] shield stop', cur);
    }
    return { pass: stops >= 1, details: { stops } };
  },

  async enclosed() {
    const def = TURRET.includes('BMP2') ? TURRET : 'Turret_30mmBMP2_Base';
    const { turret, gunner } = await setup(def);
    const cur = await logCursor();
    await shooterAt(turret, 180, gunner.id);
    await tick(SHOOT_TICKS);
    const g = await thing(gunner.id);
    const lines = await logCount('[AMC OpenTop]', cur);
    return { pass: g.pawn.health === 1 && lines === 0, details: { health: g.pawn.health, openTopLines: lines } };
  },
};

// The turret under test is HOSTILE (CE turrets will not fire on friendly pawns) with a hostile gunner and no ammo,
// so its top stays facing north (0). A player turret 16 cells north fires the given ammo at that gunner.
async function attackerCase(att, logNeedle, expectGunnerHurt) {
  await freshGame();
  let c = null;
  for (const clearRadius of [10, 8, 6, 5, 4]) {
    c = await call('find_cell', { x: 60, z: 190, radius: 120, clearRadius }).catch(() => null);
    if (c) break;
  }
  if (!c) throw new SetupError('no clear cell found');
  const { turret, gunner } = await mannedTurret(TURRET, c.x, c.z, null, 'hostile');
  await call('telemetry_stop').catch(() => {});
  await call('telemetry_start', { runId: `${CASE}-${TURRET}-${Date.now()}` });
  const cur = await logCursor();
  const ac = await call('find_cell', { x: c.x, z: c.z + 16, radius: 6, clearRadius: 3 }).catch(() => ({ x: c.x, z: c.z + 16 }));
  const a = await call('spawn_thing', { defName: att.def, x: ac.x, z: ac.z, faction: 'player', power: true });
  await call('turret_fcs', { id: a.id }).catch(() => {});
  await loadAmmo(a.id, att.ammo);
  let pen = 0; let blastSeat = 0; let stops = 0;
  for (let i = 0; i < 8; i++) {
    await tick(SHOOT_TICKS);
    // The hostile turret fires back, so count only log lines about the target gunner.
    pen = logNeedle ? await logCount(`${logNeedle}`, cur, `gunner=${gunner.id} `) : 0;
    blastSeat = await logCount('[AMC OpenTop] blast seat', cur, `gunner=${gunner.id}`);
    stops = await logCount('[AMC OpenTop] shield stop', cur, `gunner=${gunner.id} `);
    if (logNeedle ? pen >= 1 : (stops >= 1 && blastSeat >= 1)) break;
  }
  const g = await thing(gunner.id).catch(() => undefined);
  const hurt = !g || g.pawn.health < 1 || g.pawn.dead; // a killed pawn may no longer be queryable
  // ap: a round penetrated the shield and hurt the gunner. he: the blast was judged at the seat cell and hurt him
  // (HE mostly detonates on the hull, so a shield stop is not required).
  const pass = logNeedle
    ? pen >= 1 && (hurt || !expectGunnerHurt)
    : blastSeat >= 1 && hurt;
  const all = await impacts();
  return { pass, details: { gunnerId: gunner.id, health: g?.pawn.health, dead: g ? g.pawn.dead : 'gone', pen, stops, blastSeat, top: (await thing(turret.id).catch(() => undefined))?.turret?.topRotation, impactsTotal: all.length, sample: all.slice(0, 6).map((e) => [e.other_id === turret.id ? 'TURRET' : e.other_id === gunner.id ? 'GUNNER' : e.other_id, e.detail, +e.y.toFixed(2)]) } };
}

try {
  if (!CASES[CASE]) throw new SetupError(`unknown case ${CASE}`);
  const r = await CASES[CASE]();
  await call('telemetry_stop').catch(() => {});
  console.log(JSON.stringify({ case: CASE, turret: TURRET, ...r }));
  process.exit(r.pass ? 0 : 1);
} catch (e) {
  console.log(JSON.stringify({ case: CASE, turret: TURRET, pass: false, error: String(e.message || e) }));
  process.exit(e instanceof SetupError ? 2 : 1);
}
