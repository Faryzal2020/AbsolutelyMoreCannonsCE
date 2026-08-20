import React from 'react';
import { Field, Toggle, Section, Button, Icon } from './ui.jsx';
import { getPath } from '../util.js';

/** Bind a Field to a dotted path on the draft. */
export function bind(draft, set, dirty) {
  return (key, label, opts = {}) => (
    <Field
      key={key}
      label={label}
      value={getPath(draft, key)}
      dirty={dirty?.has(key)}
      onChange={(v) => set(key, v)}
      {...opts}
    />
  );
}

export function BasicTab({ draft, set, dirty }) {
  const f = bind(draft, set, dirty);
  return (
    <>
      <Section title="Identity" tone="blue">
        <div className="grid">
          {f('label', 'Label')}
          <Field label="DefName" value={draft.defName} disabled onChange={() => {}} />
          <Field label="Parent Name" value={draft.parentName} disabled onChange={() => {}}
            hint="ParentName is an XML attribute and is not editable here." />
          <Field label="Category Folder" value={draft.category} disabled onChange={() => {}} />
        </div>
      </Section>

      <Section title="Building Stats" tone="mint">
        <div className="grid">
          {f('stats.maxHitPoints', 'Max Hit Points (HP)', { type: 'number' })}
          {f('stats.workToBuild', 'Work To Build', { type: 'number' })}
          {f('stats.mass', 'Mass (kg)', { type: 'number', step: '0.01' })}
          {f('stats.bulk', 'Bulk', { type: 'number', step: '0.01' })}
          {f('stats.cooldownTime', 'Turret Cooldown Time (s)', { type: 'number', step: '0.1' })}
          {f('stats.topDrawSize', 'Turret Top Draw Size', { type: 'number', step: '0.1' })}
          {f('stats.constructionSkill', 'Construction Skill Required', { type: 'number' })}
        </div>
      </Section>

      <Section title="Files" tone="slate">
        <div className="grid">
          <Field label="Building Def File" value={draft.filePath} disabled onChange={() => {}} />
          <Field label="Weapon Def" value={draft.weaponDefName} disabled onChange={() => {}} />
          <Field label="Weapon Def File" value={draft.weaponFilePath} disabled onChange={() => {}} />
        </div>
      </Section>
    </>
  );
}

export function CostsTab({ draft, set }) {
  const costs = draft.costs || [];
  const update = (i, key, value) => {
    const next = costs.map((c, idx) => (idx === i ? { ...c, [key]: key === 'count' ? (Number(value) || 0) : value } : c));
    set('costs', next);
  };
  return (
    <Section title="Resource Cost List (costList)" tone="butter"
      aside={<Button icon="plus" small onClick={() => set('costs', [...costs, { thingDef: '', count: 1 }])}>Add row</Button>}>
      {costs.length === 0 && <div className="muted" style={{ marginBottom: 10 }}>This def has no costList.</div>}
      <table className="rows">
        {costs.length > 0 && (
          <thead>
            <tr><th style={{ width: '60%' }}>Resource ThingDef</th><th>Count</th><th style={{ width: 44 }} /></tr>
          </thead>
        )}
        <tbody>
          {costs.map((c, i) => (
            <tr key={i}>
              <td><input value={c.thingDef} placeholder="Steel" onChange={(e) => update(i, 'thingDef', e.target.value)} /></td>
              <td><input type="number" value={c.count} onChange={(e) => update(i, 'count', e.target.value)} /></td>
              <td>
                <Button icon="trash" small variant="danger" aria-label="Delete row"
                  onClick={() => set('costs', costs.filter((_, idx) => idx !== i))} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Section>
  );
}

export function BallisticsTab({ draft, set, dirty }) {
  const f = bind(draft, set, dirty);
  return (
    <>
      <Section title="Verb & Range" tone="blue">
        <div className="grid">
          {f('ballistics.verbClass', 'Verb Class')}
          {f('ballistics.minRange', 'Min Range (cells)', { type: 'number', step: '0.1' })}
          {f('ballistics.maxRange', 'Max Range (cells)', { type: 'number', step: '0.1' })}
          {f('ballistics.burstShotCount', 'Burst Shot Count', { type: 'number' })}
          {f('ballistics.ticksBetween', 'Ticks Between Burst Shots', { type: 'number' })}
          {f('ballistics.warmupTime', 'Warmup Time (s)', { type: 'number', step: '0.05' })}
        </div>
      </Section>

      <Section title="Accuracy Stats" tone="mint">
        <div className="grid">
          {f('ballistics.sightsEfficiency', 'Sights Efficiency', { type: 'number', step: '0.05' })}
          {f('ballistics.shotSpread', 'Shot Spread', { type: 'number', step: '0.01' })}
          {f('ballistics.swayFactor', 'Sway Factor', { type: 'number', step: '0.01' })}
          {f('ballistics.cooldown', 'Weapon Cooldown (s)', { type: 'number', step: '0.1' })}
        </div>
      </Section>

      <Section title="AmmoSet" tone="lilac">
        <div className="grid">
          {f('ammo.ammoSet', 'AmmoSet Def')}
          {f('ammo.magazineSize', 'Magazine Size', { type: 'number' })}
          {f('ammo.reloadTime', 'Reload Time (s)', { type: 'number', step: '0.1' })}
        </div>
      </Section>

      <Section title="Selectable Bursts" tone="butter">
        <Toggle
          label="Enable selectableBurstCounts"
          checked={draft.barrel.selectableBursts.enabled}
          onChange={(v) => set('barrel.selectableBursts.enabled', v)}
        />
        {draft.barrel.selectableBursts.enabled && (
          <div className="grid" style={{ marginTop: 8 }}>
            {f('barrel.selectableBursts.counts', 'Burst counts (comma separated)', { placeholder: '3, 6, 9' })}
          </div>
        )}
        {draft.barrel.selectableBursts.enabled && !draft.barrel.enabled && (
          <div className="note danger" style={{ marginTop: 10 }}>
            Burst counts live inside TurretBarrelExtension. Enable the barrel extension on the
            Barrel &amp; Animations tab before injecting.
          </div>
        )}
      </Section>
    </>
  );
}

export function CompsTab({ draft, set, dirty, allTurrets }) {
  const f = bind(draft, set, dirty);
  const swapTargets = allTurrets
    .filter((t) => t.defName !== draft.defName)
    .map((t) => ({ value: t.defName, label: `${t.label} — ${t.defName}` }));

  return (
    <>
      <Section title="Operation" tone="blue">
        <div className="grid">
          <div>
            <Toggle label="Mannable turret (CompProperties_Mannable)"
              checked={draft.comps.isManned} onChange={(v) => set('comps.isManned', v)} />
            <Toggle label="Electrical power (CompProperties_Power)"
              checked={draft.comps.isPowered} onChange={(v) => set('comps.isPowered', v)} />
          </div>
          {draft.comps.isPowered && f('comps.powerWatts', 'Base Power Consumption (W)', { type: 'number' })}
        </div>
      </Section>

      <Section title="Fire Control System" tone="mint">
        <Toggle label="FCS comp (CompProperties_TurretFCS)"
          checked={draft.comps.hasFcs} onChange={(v) => set('comps.hasFcs', v)} />
      </Section>

      <Section title="Mode Swap" tone="lilac">
        <Toggle label="Mode swap capable (CompProperties_TurretModeSwap)"
          checked={draft.comps.hasModeSwap} onChange={(v) => set('comps.hasModeSwap', v)} />
        {draft.comps.hasModeSwap && (
          <div className="grid" style={{ marginTop: 8 }}>
            <Field
              label="Alternate Mode Def Target"
              value={draft.comps.swapAltDef}
              onChange={(v) => set('comps.swapAltDef', v)}
              options={[{ value: '', label: '— none —' }, ...swapTargets]}
            />
            {f('comps.swapGizmoLabel', 'Gizmo Label')}
          </div>
        )}
      </Section>

      <Section title="Accuracy Override" tone="peach">
        <Toggle label="Accuracy override comp (CompProperties_AccuracyOverride)"
          checked={draft.comps.accuracy.enabled} onChange={(v) => set('comps.accuracy.enabled', v)} />
        {draft.comps.accuracy.enabled && (
          <div className="grid" style={{ marginTop: 8 }}>
            {f('comps.accuracy.swayReduction', 'Sway Reduction', { type: 'number', step: '0.01' })}
            {f('comps.accuracy.recoilReduction', 'Recoil Reduction', { type: 'number', step: '0.01' })}
            {f('comps.accuracy.spreadReduction', 'Spread Reduction', { type: 'number', step: '0.01' })}
          </div>
        )}
      </Section>
    </>
  );
}

export function BarrelTab({ draft, set, dirty }) {
  const f = bind(draft, set, dirty);
  const on = draft.barrel.enabled;
  return (
    <>
      <Section title="Root Barrel Setup (TurretBarrelExtension)" tone="blue">
        <Toggle label="Enable barrel extension" checked={on} onChange={(v) => set('barrel.enabled', v)} />
        {on && (
          <div className="grid" style={{ marginTop: 8 }}>
            {f('barrel.drawSize', 'Barrel Draw Size', { type: 'number', step: '0.1' })}
            {f('barrel.offset', 'Barrel Offset (Vector3)', { placeholder: '(0,0,0.0)' })}
            {f('barrel.barrelAmount', 'Barrel Count', { type: 'number' })}
            {f('barrel.barrelSpacing', 'Barrel Spacing', { type: 'number', step: '0.1' })}
            {f('barrel.maxRPMs', 'Max RPM steps (comma separated)', { placeholder: '1500, 3000' })}
            <div>
              <Toggle label="Draw on top" checked={draft.barrel.drawOnTop} onChange={(v) => set('barrel.drawOnTop', v)} />
              <Toggle label="Sequential alternating fire" checked={draft.barrel.sequentialFiring}
                onChange={(v) => set('barrel.sequentialFiring', v)} />
            </div>
          </div>
        )}
      </Section>

      {on && (
        <>
          <Section title="Recoil Animation" tone="mint">
            <Toggle label="Enable recoil animation" checked={draft.barrel.recoil.enabled}
              onChange={(v) => set('barrel.recoil.enabled', v)} />
            {draft.barrel.recoil.enabled && (
              <div className="grid" style={{ marginTop: 8 }}>
                {f('barrel.recoil.maxDistance', 'Max Distance', { type: 'number', step: '0.05' })}
                {f('barrel.recoil.recoilDuration', 'Recoil Duration (ticks)', { type: 'number' })}
                {f('barrel.recoil.returnDuration', 'Return Duration (ticks)', { type: 'number' })}
                <div>
                  <Toggle label="Use recoil curve" checked={draft.barrel.recoil.useRecoilCurve}
                    onChange={(v) => set('barrel.recoil.useRecoilCurve', v)} />
                  <Toggle label="Use return curve" checked={draft.barrel.recoil.useReturnCurve}
                    onChange={(v) => set('barrel.recoil.useReturnCurve', v)} />
                  <Toggle label="Rotation impact" checked={draft.barrel.recoil.affectsRotation}
                    onChange={(v) => set('barrel.recoil.affectsRotation', v)} />
                </div>
              </div>
            )}
          </Section>

          <Section title="Firing Flash & Sound" tone="peach">
            <Toggle label="Enable firing animation" checked={draft.barrel.firing.enabled}
              onChange={(v) => set('barrel.firing.enabled', v)} />
            {draft.barrel.firing.enabled && (
              <div className="grid" style={{ marginTop: 8 }}>
                {f('barrel.firing.durationTicks', 'Duration (ticks)', { type: 'number' })}
                {f('barrel.firing.flashColor', 'Flash Color (RGBA)', { placeholder: '(1,0.7,0.3,1)' })}
                {f('barrel.firing.flashSize', 'Flash Size', { type: 'number', step: '0.5' })}
                {f('barrel.firing.flashBrightness', 'Flash Brightness', { type: 'number', step: '0.1' })}
                {f('barrel.firing.projectileSpawnOffset', 'Projectile Spawn Offset', { type: 'number', step: '0.1' })}
                {f('barrel.firing.burstSound', 'Sustained Burst SoundDef')}
                <div>
                  <Toggle label="Draw muzzle flash" checked={draft.barrel.firing.drawFlash}
                    onChange={(v) => set('barrel.firing.drawFlash', v)} />
                </div>
              </div>
            )}
          </Section>

          <Section title="Rotary / Spinning Animation" tone="lilac">
            <Toggle label="Enable rotary animation" checked={draft.barrel.spinning.enabled}
              onChange={(v) => set('barrel.spinning.enabled', v)} />
            {draft.barrel.spinning.enabled && (
              <div className="grid" style={{ marginTop: 8 }}>
                {f('barrel.spinning.animationMode', 'Animation Mode', { options: ['RPMBased', 'Cycling'] })}
                {f('barrel.spinning.maxRPM', 'Max RPM', { type: 'number' })}
                {f('barrel.spinning.spindownTime', 'Spindown Time (s)', { type: 'number', step: '0.1' })}
                {f('barrel.spinning.frameCount', 'Frame Count', { type: 'number' })}
                {f('barrel.spinning.barrelCount', 'Barrel Count', { type: 'number' })}
                {f('barrel.spinning.spinUpSound', 'Spin Up SoundDef')}
                {f('barrel.spinning.spinDownSound', 'Spin Down SoundDef')}
              </div>
            )}
          </Section>
        </>
      )}
    </>
  );
}

export function SmokeTab({ draft, set, dirty }) {
  const f = bind(draft, set, dirty);
  const on = draft.smoker.enabled;
  return (
    <>
      <Section title="Smoker Component (CompProperties_TurretSmoker)" tone="blue">
        <Toggle label="Enable smoker component" checked={on} onChange={(v) => set('smoker.enabled', v)} />
      </Section>

      {on && (
        <>
          <Section title="Muzzle Smoke" tone="peach">
            <Toggle label="Enable muzzle smoke" checked={draft.smoker.muzzle.enabled}
              onChange={(v) => set('smoker.muzzle.enabled', v)} />
            {draft.smoker.muzzle.enabled && (
              <div className="grid" style={{ marginTop: 8 }}>
                {f('smoker.muzzle.fleckDef', 'FleckDef')}
                {f('smoker.muzzle.particleCount', 'Particle Count', { type: 'number' })}
                {f('smoker.muzzle.velocity', 'Velocity (cells/s)', { type: 'number', step: '0.5' })}
                {f('smoker.muzzle.particleSize', 'Size Multiplier', {
                  placeholder: '1~2', hint: 'Accepts a number or a RimWorld range such as 1~2.',
                })}
              </div>
            )}
          </Section>

          <Section title="Heat Smoke" tone="butter">
            <Toggle label="Enable heat smoke" checked={draft.smoker.heat.enabled}
              onChange={(v) => set('smoker.heat.enabled', v)} />
            {draft.smoker.heat.enabled && (
              <div className="grid" style={{ marginTop: 8 }}>
                {f('smoker.heat.fleckDef', 'FleckDef')}
                {f('smoker.heat.threshold', 'Heat Threshold (shots)', { type: 'number' })}
                {f('smoker.heat.decayRate', 'Decay Rate (/s)', { type: 'number', step: '0.01' })}
                {f('smoker.heat.emissionRate', 'Emission Rate (/s)', { type: 'number', step: '0.1' })}
              </div>
            )}
          </Section>

          <Section title="Shockwave Smoke" tone="mint">
            <Toggle label="Enable shockwave smoke" checked={draft.smoker.shockwave.enabled}
              onChange={(v) => set('smoker.shockwave.enabled', v)} />
            {draft.smoker.shockwave.enabled && (
              <div className="grid" style={{ marginTop: 8 }}>
                {f('smoker.shockwave.fleckDef', 'FleckDef')}
                {f('smoker.shockwave.radius', 'Radius (cells)', { type: 'number', step: '0.5' })}
                {f('smoker.shockwave.density', 'Particle Density', { type: 'number' })}
              </div>
            )}
          </Section>
        </>
      )}
    </>
  );
}

export const TABS = [
  { id: 'basic', label: 'Basic & Stats', icon: 'stats', Panel: BasicTab },
  { id: 'costs', label: 'Costs', icon: 'coins', Panel: CostsTab },
  { id: 'ballistics', label: 'Ballistics', icon: 'target', Panel: BallisticsTab },
  { id: 'comps', label: 'Comps & FCS', icon: 'gear', Panel: CompsTab },
  { id: 'barrel', label: 'Barrel & Animations', icon: 'barrel', Panel: BarrelTab },
  { id: 'smoke', label: 'Smoke & Particles', icon: 'smoke', Panel: SmokeTab },
];
