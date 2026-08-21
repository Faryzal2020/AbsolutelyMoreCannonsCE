import React from 'react';
import { Button, Pill } from './ui.jsx';
import { fmt, fmtNum } from '../util.js';

function Badges({ t }) {
  const pills = [];
  if (t.comps.isManned) pills.push(<Pill key="man" tone="blue">Manned</Pill>);
  if (t.comps.isPowered) pills.push(<Pill key="pow" tone="lilac">{fmtNum(t.comps.powerWatts, '?')} W</Pill>);
  if (t.comps.hasFcs) pills.push(<Pill key="fcs" tone="mint">FCS</Pill>);
  if (t.comps.accuracy.enabled) pills.push(<Pill key="acc" tone="mint">Accuracy Override</Pill>);
  if (t.comps.enclosed?.enabled) pills.push(<Pill key="enc" tone="butter">Enclosed</Pill>);
  if (t.smoker.muzzle.enabled) pills.push(<Pill key="mz" tone="peach">Muzzle Smoke</Pill>);
  if (t.smoker.heat.enabled) pills.push(<Pill key="ht" tone="peach">Heat Smoke</Pill>);
  if (t.smoker.shockwave.enabled) pills.push(<Pill key="sw" tone="peach">Shockwave</Pill>);
  if (t.barrel.recoil.enabled) pills.push(<Pill key="rc" tone="butter">Recoil Anim</Pill>);
  if (t.barrel.spinning.enabled) pills.push(<Pill key="sp" tone="butter">Rotary Anim</Pill>);
  if (t.barrel.selectableBursts.enabled) pills.push(<Pill key="sb" tone="butter">Bursts {t.barrel.selectableBursts.counts}</Pill>);
  if (t.comps.hasPreserveAmmo) pills.push(<Pill key="pa">Preserve Ammo</Pill>);
  if (t.comps.hasFireArc) pills.push(<Pill key="fa">Fire Arc</Pill>);
  if (t.modified) pills.push(<Pill key="ed" tone="rose">Edited</Pill>);
  return <>{pills}</>;
}

function Validation({ warnings }) {
  if (!warnings.length) return <Pill tone="mint">Valid</Pill>;
  const errors = warnings.filter((w) => w.level === 'error').length;
  const title = warnings.map((w) => `${w.level}: ${w.message}`).join('\n');
  return (
    <>
      {errors > 0 && <Pill tone="rose" title={title}>{errors} error{errors > 1 ? 's' : ''}</Pill>}
      {warnings.length - errors > 0 && (
        <Pill tone="butter" title={title}>{warnings.length - errors} warning{warnings.length - errors > 1 ? 's' : ''}</Pill>
      )}
    </>
  );
}

export default function Matrix({ turrets, ready, onEdit, onDiff, onRevert }) {
  if (!ready) return <div className="empty">Loading turret matrix…</div>;
  if (!turrets.length) {
    return <div className="empty">No turrets match the current search and filters.</div>;
  }

  return (
    <div className="table-wrap">
      <table className="matrix">
        <thead>
          <tr>
            <th>Turret System &amp; Defs</th>
            <th>Gameplay Stats &amp; Costs</th>
            <th>Firing &amp; Ballistics</th>
            <th>Features &amp; Comps</th>
            <th>AmmoSet</th>
            <th>Actions &amp; Audit</th>
          </tr>
        </thead>
        <tbody>
          {turrets.map((t) => (
            <tr key={t.defName} className={t.modified ? 'is-modified' : ''}>
              <td style={{ minWidth: 230 }}>
                <div className="cell-title">{t.label}</div>
                <div className="cell-sub">{t.defName}</div>
                <div style={{ marginTop: 5 }}>
                  <Pill tone="blue">{t.category}</Pill>
                  <Pill>{t.mode === 'indirect' ? 'Indirect' : 'Direct'}</Pill>
                </div>
                <dl className="kv" style={{ marginTop: 6 }}>
                  <dt>Parent</dt><dd className="mono">{fmt(t.parentName)}</dd>
                  {t.comps.swapAltDef && (<><dt>Swaps to</dt><dd className="mono">{t.comps.swapAltDef}</dd></>)}
                </dl>
              </td>

              <td style={{ minWidth: 180 }}>
                <dl className="kv">
                  <dt>Max HP</dt><dd>{fmtNum(t.stats.maxHitPoints)}</dd>
                  <dt>Work</dt><dd>{fmtNum(t.stats.workToBuild)}</dd>
                  <dt>Mass</dt><dd>{fmtNum(t.stats.mass)} kg</dd>
                  <dt>Bulk</dt><dd>{fmtNum(t.stats.bulk)}</dd>
                </dl>
                <div style={{ marginTop: 5 }}>
                  {t.costs.length
                    ? t.costs.map((c) => <Pill key={c.thingDef}>{c.thingDef} ×{c.count}</Pill>)
                    : <span className="muted">no costList</span>}
                </div>
              </td>

              <td style={{ minWidth: 190 }}>
                <dl className="kv">
                  <dt>Verb</dt><dd className="mono">{fmt(t.ballistics.verbClass).split('.').pop()}</dd>
                  <dt>Range</dt><dd>{fmtNum(t.ballistics.minRange, '0')} – {fmtNum(t.ballistics.maxRange)}</dd>
                  <dt>Burst</dt><dd>{fmtNum(t.ballistics.burstShotCount, '1')}</dd>
                  <dt>Cooldown</dt><dd>{fmtNum(t.stats.cooldownTime)} s</dd>
                  <dt>Warmup</dt><dd>{fmtNum(t.ballistics.warmupTime)} s</dd>
                </dl>
              </td>

              <td style={{ minWidth: 220, maxWidth: 300 }}><Badges t={t} /></td>

              <td style={{ minWidth: 165 }}>
                <div className="cell-sub">{fmt(t.ammo.ammoSet)}</div>
                <dl className="kv" style={{ marginTop: 4 }}>
                  <dt>Magazine</dt><dd>{fmtNum(t.ammo.magazineSize)}</dd>
                  <dt>Reload</dt><dd>{fmtNum(t.ammo.reloadTime)} s</dd>
                </dl>
              </td>

              <td style={{ minWidth: 175 }}>
                <div className="row-actions">
                  <Button icon="edit" small variant="primary" onClick={() => onEdit(t.defName)}>Edit</Button>
                  <Button icon="diff" small onClick={() => onDiff(t.defName)}>XML</Button>
                  <Button icon="revert" small variant="danger" disabled={!t.modified} onClick={() => onRevert(t.defName)}>
                    Revert
                  </Button>
                </div>
                <div style={{ marginTop: 6 }}><Validation warnings={t.warnings} /></div>
                <div className="muted mono" style={{ marginTop: 4, fontSize: 10.5 }}>{t.filePath.split('/').slice(-2).join('/')}</div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
