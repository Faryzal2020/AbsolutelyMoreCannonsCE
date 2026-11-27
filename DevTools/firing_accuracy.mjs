#!/usr/bin/env node
// Firing-solution accuracy probe, driven through RimTestHarness (telemetry on).
// A manned turret fires at a ground cell; reports along-track error (impact past (+) / short of (-) the target,
// measured along the turret-centre -> target line) and cross-track error. A firing solution computed from the
// wrong spawn point shows up as a systematic along-track bias (overshoot/undershoot), not as random spread.
//
//   node DevTools/firing_accuracy.mjs --port=18573 --turret=Turret_152mmMsta_indirect_Base --range=60 --ticks=6000
//
// Prints one JSON line: { turret, range, shots, meanAlong, medianAlong, meanCross, sdAlong, impacts[] }.

const args = Object.fromEntries(process.argv.slice(2).map((a) => {
  const m = a.match(/^--([^=]+)=?(.*)$/);
  return m ? [m[1], m[2] === '' ? true : m[2]] : [a, true];
}));
const PORT = Number(args.port || 18573);
const TURRET = args.turret;
const RANGE = Number(args.range || 50);
const TICKS = Number(args.ticks || 6000);
const DIR = Number(args.dir || 90); // compass degrees from the turret to the target
const FRESH = args.fresh !== 'false';

async function call(cmd, body = {}, timeoutMs = 180000) {
  const res = await fetch(`http://127.0.0.1:${PORT}/${cmd}`, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ ...body, timeout_ms: timeoutMs }),
  });
  const j = JSON.parse(await res.text());
  if (!j.ok) throw new Error(`${cmd} ${JSON.stringify(body)}: ${j.error}`);
  return j.result;
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const thing = async (id) => (await call('query_things', { id })).things[0];

async function freshGame() {
  await call('new_quicktest_game');
  for (let i = 0; i < 240; i++) {
    await sleep(500);
    try { const s = await call('status', {}, 5000); if (s.programState === 'Playing' && !s.anyLongEvent) break; } catch { }
  }
  await call('dev_mode', { on: true });
  await call('set_debug', { field: 'unlimitedPower', value: true });
  await call('debug_action', { path: 'Actions\\Destroy non-colonists' }).catch(() => {});
}

const median = (a) => { const s = [...a].sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : null; };
const mean = (a) => (a.length ? a.reduce((x, y) => x + y, 0) / a.length : null);

try {
  if (FRESH) await freshGame();
  const st = await call('status');
  const r = (DIR * Math.PI) / 180;
  const dx = Math.sin(r); const dz = Math.cos(r);
  // Put the turret so the target cell lies inside the map.
  const cx = Math.round(st.mapSize.x / 2 - dx * RANGE / 2);
  const cz = Math.round(st.mapSize.z / 2 - dz * RANGE / 2);
  const c = await call('find_cell', { x: cx, z: cz, radius: 25, clearRadius: 5 });
  const t = await call('spawn_thing', { defName: TURRET, x: c.x, z: c.z, faction: 'player', power: true });
  await call('turret_fcs', { id: t.id }).catch(() => {});
  const cur = (await thing(t.id)).turret?.ammo?.currentAmmo;
  if (cur) await call('turret_ammo', { id: t.id, ammoDef: cur });
  const g = await call('spawn_pawn', { kindDef: 'Colonist', faction: 'player', x: c.x, z: c.z - 4 });
  await call('order_job', { pawnId: g.id, jobDef: 'ManTurret', targetId: t.id });
  await call('tick', { n: 200 });

  const tt = await thing(t.id);
  const tx = Math.round(tt.x + dx * RANGE); const tz = Math.round(tt.z + dz * RANGE);
  await call('telemetry_stop').catch(() => {});
  await call('telemetry_start', { runId: `acc-${TURRET}-${RANGE}-${Date.now()}` });
  await call('turret_order_attack', { id: t.id, x: tx, z: tz });
  for (let done = 0; done < TICKS; done += 1000) {
    await call('tick', { n: 1000 });
    if ((await thing(t.id)).turret?.ammo?.magCount === 0) await call('turret_ammo', { id: t.id, ammoDef: cur }).catch(() => {});
  }
  const ev = (await call('events', { limit: 5000 })).events;
  await call('telemetry_stop');

  // Projectiles launched by this gun (launcher is the operator pawn for manned turrets).
  const mine = new Set(ev.filter((e) => e.type === 'launch' && (e.other_id === g.id || e.other_id === t.id)).map((e) => e.id));
  const launches = ev.filter((e) => e.type === 'launch' && mine.has(e.id));
  const hits = ev.filter((e) => e.type === 'impact' && mine.has(e.id));
  // Target = centre of the target cell; direction from the turret's true centre.
  const target = { x: tx + 0.5, z: tz + 0.5 };
  const origin = { x: tt.x + 0.5, z: tt.z + 0.5 };
  const len = Math.hypot(target.x - origin.x, target.z - origin.z);
  const ux = (target.x - origin.x) / len; const uz = (target.z - origin.z) / len;
  const along = []; const cross = []; const detail = [];
  for (const h of hits) {
    const ex = h.x - target.x; const ez = h.z - target.z;
    along.push(ex * ux + ez * uz);
    cross.push(ex * uz - ez * ux);
    detail.push([+(ex * ux + ez * uz).toFixed(2), +(ex * uz - ez * ux).toFixed(2), h.detail?.split('|')[0]]);
  }
  const m = mean(along);
  const sd = along.length > 1 ? Math.sqrt(mean(along.map((a) => (a - m) ** 2))) : null;
  const spawn = launches[0] ? { x: +launches[0].x.toFixed(2), y: +launches[0].y.toFixed(2), z: +launches[0].z.toFixed(2) } : null;
  console.log(JSON.stringify({
    turret: TURRET, range: RANGE, shots: launches.length, impacts: hits.length,
    meanAlong: m == null ? null : +m.toFixed(2), medianAlong: median(along) == null ? null : +median(along).toFixed(2),
    sdAlong: sd == null ? null : +sd.toFixed(2), meanCross: mean(cross) == null ? null : +mean(cross).toFixed(2),
    firstSpawn: spawn, turretCentre: origin, detail: detail.slice(0, 12),
    ...(launches.length === 0 ? { whyNoShots: await (async () => {
      const q = (await call('query_things', { id: t.id, deep: true })).things[0];
      const gp = await thing(g.id);
      return { gunnerJob: gp?.pawn?.curJob, turret: q.turret, inspect: (q.inspect || '').slice(0, 300) };
    })() } : {}),
  }));
} catch (e) {
  console.log(JSON.stringify({ turret: TURRET, range: RANGE, error: String(e.message || e) }));
  process.exit(2);
}
