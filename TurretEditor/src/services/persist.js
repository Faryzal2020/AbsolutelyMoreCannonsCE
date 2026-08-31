/** Shared projection of a normalised turret object into the relational tables. */

const COLUMNS = [
  'def_name', 'file_id', 'label', 'parent_name', 'category', 'designator_dropdown',
  'weapon_def_name', 'mode',
  'max_hp', 'work_to_build', 'mass', 'bulk', 'cooldown_time', 'top_draw_size', 'construction_skill',
  'power_consumption',
  'is_manned', 'is_powered', 'has_fcs', 'has_mode_swap', 'swap_alt_def',
  'has_accuracy_override', 'has_barrel_ext', 'has_recoil_anim', 'has_rotary_anim',
  'has_smoker', 'has_selectable_bursts', 'size', 'modified',
  'data_json', 'original_json', 'updated_at',
];

const UPSERT_SQL = `INSERT OR REPLACE INTO turrets (${COLUMNS.join(', ')})
  VALUES (${COLUMNS.map(() => '?').join(', ')})`;

function values(t, { fileId, modified, originalJson }) {
  return [
    t.defName, fileId ?? null, t.label, t.parentName, t.category, t.designatorDropdown,
    t.weaponDefName, t.mode,
    t.stats.maxHitPoints, t.stats.workToBuild, t.stats.mass, t.stats.bulk,
    t.stats.cooldownTime, t.stats.topDrawSize, t.stats.constructionSkill,
    t.comps.powerWatts,
    t.comps.isManned ? 1 : 0, t.comps.isPowered ? 1 : 0, t.comps.hasFcs ? 1 : 0,
    t.comps.hasModeSwap ? 1 : 0, t.comps.swapAltDef,
    t.comps.accuracy.enabled ? 1 : 0,
    t.barrel.enabled ? 1 : 0, t.barrel.recoil.enabled ? 1 : 0, t.barrel.spinning.enabled ? 1 : 0,
    t.smoker.enabled ? 1 : 0, t.barrel.selectableBursts.enabled ? 1 : 0,
    t.size, modified ? 1 : 0,
    JSON.stringify(t), originalJson, new Date().toISOString(),
  ];
}

export function extensionRows(t) {
  const rows = [];
  if (t.barrel.enabled) {
    rows.push(['modExtension', 'AbsolutelyMoreCannons.TurretBarrelExtension', {
      drawSize: t.barrel.drawSize, offset: t.barrel.offset, drawOnTop: t.barrel.drawOnTop,
      barrelAmount: t.barrel.barrelAmount, barrelSpacing: t.barrel.barrelSpacing,
      sequentialFiring: t.barrel.sequentialFiring, maxRPMs: t.barrel.maxRPMs,
      selectableBurstCounts: t.barrel.selectableBursts.counts,
      recoilAnimation: t.barrel.recoil,
      firingAnimation: t.barrel.firing,
      spinningAnimation: t.barrel.spinning,
    }]);
  }
  if (t.smoker.enabled) {
    rows.push(['comp', 'AbsolutelyMoreCannons.CompProperties_TurretSmoker', {
      muzzle: t.smoker.muzzle, heat: t.smoker.heat, shockwave: t.smoker.shockwave,
    }]);
  }
  if (t.comps.accuracy.enabled) rows.push(['comp', 'AbsolutelyMoreCannons.CompProperties_AccuracyOverride', t.comps.accuracy]);
  if (t.comps.hasModeSwap) rows.push(['comp', 'AbsolutelyMoreCannons.CompProperties_TurretModeSwap', { alternateDef: t.comps.swapAltDef, gizmoLabel: t.comps.swapGizmoLabel }]);
  if (t.comps.isPowered) rows.push(['comp', 'CompProperties_Power', { basePowerConsumption: t.comps.powerWatts }]);
  if (t.comps.hasFcs) rows.push(['comp', 'AbsolutelyMoreCannons.CompProperties_TurretFCS', {}]);
  if (t.comps.isManned) rows.push(['comp', 'CompProperties_Mannable', {}]);
  return rows;
}

/** Write the turret plus all of its dependent rows. */
export function upsertTurret(db, t, opts) {
  db.run(UPSERT_SQL, ...values(t, opts));

  db.run('DELETE FROM costs WHERE def_name = ?', t.defName);
  t.costs.forEach((c, i) => {
    db.run('INSERT INTO costs (def_name, thing_def, count, ordinal) VALUES (?, ?, ?, ?)',
      t.defName, c.thingDef, c.count, i);
  });

  db.run('DELETE FROM weapons WHERE def_name = ?', t.defName);
  if (t.weaponDefName) {
    db.run(`INSERT OR REPLACE INTO weapons (
        weapon_def_name, def_name, file_id, label, verb_class, cooldown,
        sights_efficiency, shot_spread, sway_factor, warmup_time, burst_count,
        ticks_between, min_range, max_range, ammo_set, magazine_size, reload_time
      ) VALUES (${new Array(17).fill('?').join(', ')})`,
      t.weaponDefName, t.defName, opts.weaponFileId ?? null, t.weaponLabel,
      t.ballistics.verbClass, t.ballistics.cooldown, t.ballistics.sightsEfficiency,
      t.ballistics.shotSpread, t.ballistics.swayFactor, t.ballistics.warmupTime,
      t.ballistics.burstShotCount, t.ballistics.ticksBetween,
      t.ballistics.minRange, t.ballistics.maxRange,
      t.ammo.ammoSet, t.ammo.magazineSize, t.ammo.reloadTime);
  }

  db.run('DELETE FROM mod_extensions WHERE def_name = ?', t.defName);
  for (const [kind, cls, params] of extensionRows(t)) {
    db.run('INSERT INTO mod_extensions (def_name, kind, ext_class, params_json) VALUES (?, ?, ?, ?)',
      t.defName, kind, cls, JSON.stringify(params));
  }
}
