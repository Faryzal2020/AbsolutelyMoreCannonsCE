import React from 'react';
import { Field, Section, Button } from './ui.jsx';
import { getPath } from '../util.js';

/**
 * Physical building stats describe the same emplacement in both modes, so they
 * start linked. Firing behaviour is what makes the modes differ, so it starts
 * independent.
 */
export const DEFAULT_LINKED = [
  'stats.maxHitPoints', 'stats.workToBuild', 'stats.mass', 'stats.bulk',
  'stats.cooldownTime', 'costs',
];

const ROWS = [
  { group: 'Building Stats', tone: 'mint', fields: [
    { key: 'stats.maxHitPoints', label: 'Max Hit Points', type: 'number' },
    { key: 'stats.workToBuild', label: 'Work To Build', type: 'number' },
    { key: 'stats.mass', label: 'Mass (kg)', type: 'number', step: '0.01' },
    { key: 'stats.bulk', label: 'Bulk', type: 'number', step: '0.01' },
    { key: 'stats.cooldownTime', label: 'Cooldown Time (s)', type: 'number', step: '0.1' },
  ] },
  { group: 'Firing & Ballistics', tone: 'blue', fields: [
    { key: 'ballistics.minRange', label: 'Min Range', type: 'number', step: '0.1' },
    { key: 'ballistics.maxRange', label: 'Max Range', type: 'number', step: '0.1' },
    { key: 'ballistics.burstShotCount', label: 'Burst Shot Count', type: 'number' },
    { key: 'ballistics.warmupTime', label: 'Warmup Time (s)', type: 'number', step: '0.05' },
    { key: 'ballistics.sightsEfficiency', label: 'Sights Efficiency', type: 'number', step: '0.05' },
    { key: 'ballistics.shotSpread', label: 'Shot Spread', type: 'number', step: '0.01' },
  ] },
  { group: 'Power & Ammo', tone: 'lilac', fields: [
    { key: 'comps.powerWatts', label: 'Power (W)', type: 'number' },
    { key: 'ammo.magazineSize', label: 'Magazine Size', type: 'number' },
    { key: 'ammo.reloadTime', label: 'Reload Time (s)', type: 'number', step: '0.1' },
  ] },
];

function CostSummary({ turret }) {
  if (!turret.costs.length) return <span className="muted">no costList</span>;
  return <span className="mono">{turret.costs.map((c) => `${c.thingDef} ×${c.count}`).join(', ')}</span>;
}

export default function ParallelView({ direct, indirect, setDirect, setIndirect, linked, toggleLink, onSyncBuildingStats }) {
  const setBoth = (side) => (key, value) => {
    if (side === 'direct') setDirect(key, value); else setIndirect(key, value);
    if (linked.has(key)) {
      if (side === 'direct') setIndirect(key, value); else setDirect(key, value);
    }
  };

  return (
    <>
      <div className="note info" style={{ marginBottom: 13 }}>
        Linked fields mirror edits between both modes. Building stats start linked because they describe the
        same emplacement; firing stats start independent because they are what makes the two modes differ.
        <div style={{ marginTop: 9 }}>
          <Button icon="link" small onClick={onSyncBuildingStats}>Sync Building Stats (Direct → Indirect)</Button>
        </div>
      </div>

      <div className="row" style={{ marginBottom: 11 }}>
        <div style={{ flex: 1 }}>
          <div className="cell-title">{direct.label}</div>
          <div className="cell-sub">{direct.defName}</div>
        </div>
        <div style={{ flex: 1 }}>
          <div className="cell-title">{indirect.label}</div>
          <div className="cell-sub">{indirect.defName}</div>
        </div>
      </div>

      {ROWS.map((group) => (
        <Section key={group.group} title={group.group} tone={group.tone}>
          {group.fields.map((f) => (
            <div key={f.key} className="row" style={{ alignItems: 'flex-end', gap: 11, marginBottom: 9 }}>
              <div style={{ flex: 1 }}>
                <Field
                  label={f.label} type={f.type} step={f.step}
                  value={getPath(direct, f.key)}
                  onChange={(v) => setBoth('direct')(f.key, v)}
                />
              </div>
              <button
                className="link-toggle"
                aria-pressed={linked.has(f.key)}
                onClick={() => toggleLink(f.key)}
                title={linked.has(f.key) ? 'Linked — edits mirror to both modes' : 'Independent — edit each mode separately'}
                style={{ marginBottom: 6 }}
              >
                {linked.has(f.key) ? 'Synced' : 'Independent'}
              </button>
              <div style={{ flex: 1 }}>
                <Field
                  label={f.label} type={f.type} step={f.step}
                  value={getPath(indirect, f.key)}
                  onChange={(v) => setBoth('indirect')(f.key, v)}
                />
              </div>
            </div>
          ))}
        </Section>
      ))}

      <Section title="Resource Costs" tone="butter"
        aside={
          <button className="link-toggle" aria-pressed={linked.has('costs')} onClick={() => toggleLink('costs')}>
            {linked.has('costs') ? 'Synced' : 'Independent'}
          </button>
        }>
        <div className="row" style={{ alignItems: 'flex-start' }}>
          <div style={{ flex: 1 }}><CostSummary turret={direct} /></div>
          <div style={{ flex: 1 }}><CostSummary turret={indirect} /></div>
        </div>
        <div className="muted" style={{ fontSize: 11.5, marginTop: 8 }}>
          Edit individual cost rows on the Costs tab of either single-mode view.
          {linked.has('costs') && ' While synced, applying changes copies the direct cost list to the indirect def.'}
        </div>
      </Section>
    </>
  );
}
