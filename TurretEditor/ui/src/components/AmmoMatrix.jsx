import React from 'react';
import { Button, Icon } from './ui.jsx';

export default function AmmoMatrix({ ammoList, ready, onEdit, onRevert }) {
  const [sortKey, setSortKey] = React.useState('ammoFamily');
  const [sortAsc, setSortAsc] = React.useState(true);

  const handleSort = (key) => {
    if (sortKey === key) setSortAsc(!sortAsc);
    else { setSortKey(key); setSortAsc(true); }
  };

  const sorted = React.useMemo(() => {
    return [...ammoList].sort((a, b) => {
      let va = a[sortKey], vb = b[sortKey];
      if (sortKey === 'marketValue') { va = Number(a.stats?.marketValue || 0); vb = Number(b.stats?.marketValue || 0); }
      else if (sortKey === 'damage') { va = Number(a.payload?.damageAmountBase || 0); vb = Number(b.payload?.damageAmountBase || 0); }
      else if (sortKey === 'speed') { va = Number(a.direct?.speed || 0); vb = Number(b.direct?.speed || 0); }
      if (va === vb) return 0;
      const res = (va ?? '') > (vb ?? '') ? 1 : -1;
      return sortAsc ? res : -res;
    });
  }, [ammoList, sortKey, sortAsc]);

  if (!ready) {
    return <div className="matrix-empty">Loading Ammunition definitions...</div>;
  }
  if (!ammoList.length) {
    return <div className="matrix-empty">No ammunition definitions match the active filters.</div>;
  }

  return (
    <div className="table-wrap">
      <table className="matrix-table">
        <thead>
          <tr>
            <th onClick={() => handleSort('defName')}>Label / DefName {sortKey === 'defName' ? (sortAsc ? '▲' : '▼') : ''}</th>
            <th onClick={() => handleSort('ammoFamily')}>Caliber / Family {sortKey === 'ammoFamily' ? (sortAsc ? '▲' : '▼') : ''}</th>
            <th onClick={() => handleSort('ammoClass')}>Ammo Class</th>
            <th>Fire Modes</th>
            <th onClick={() => handleSort('marketValue')}>Market Value</th>
            <th>Mass / Bulk</th>
            <th>Yield / Work</th>
            <th onClick={() => handleSort('damage')}>Damage / Blast</th>
            <th>Special Payload</th>
            <th onClick={() => handleSort('speed')}>Speed</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((item) => {
            const hasDirect = item.hasDirectMode;
            const hasIndirect = item.hasIndirectMode;

            let modeBadge = <span className="pill badge-neutral">None</span>;
            if (hasDirect && hasIndirect) modeBadge = <span className="pill badge-good">Dual Mode</span>;
            else if (hasDirect) modeBadge = <span className="pill badge-info" style={{ background: 'rgba(56, 139, 253, 0.15)', color: '#58a6ff' }}>Direct Only</span>;
            else if (hasIndirect) modeBadge = <span className="pill badge-warn" style={{ background: 'rgba(210, 153, 34, 0.15)', color: '#d29922' }}>Indirect Only</span>;

            const frags = item.fragments?.enabled && item.fragments?.list?.length
              ? item.fragments.list.map(f => `${f.count}x ${f.thingDef.replace('Fragment_', '')}`).join(', ')
              : null;
            const airburst = item.airburst?.enabled ? `${item.airburst.type || 'Fuse'} (T=${item.airburst.armingTicks || '0'})` : null;
            const guidance = item.guidance?.enabled ? `Guided (Acc=${item.guidance.homingAcceleration || '0.1'})` : null;

            return (
              <tr key={item.defName} className={item.modified ? 'modified-row' : ''}>
                <td>
                  <div className="def-cell">
                    <strong>{item.label || item.defName}</strong>
                    <span className="sub-text">{item.defName}</span>
                  </div>
                </td>
                <td>
                  <span className="tag-pill">{item.ammoFamily}</span>
                </td>
                <td>
                  <span className="code-sm">{item.ammoClass || 'Standard'}</span>
                </td>
                <td>{modeBadge}</td>
                <td>
                  <span className="num font-mono">{item.stats?.marketValue ? `$${item.stats.marketValue}` : '-'}</span>
                </td>
                <td>
                  <span className="num font-mono">{item.stats?.mass ? `${item.stats.mass}kg` : '-'} / {item.stats?.bulk || '-'}</span>
                </td>
                <td>
                  <span className="num font-mono">{item.recipe?.yieldCount ? `x${item.recipe.yieldCount}` : 'x1'} ({item.recipe?.workAmount ? item.recipe.workAmount.toLocaleString() : '-'})</span>
                </td>
                <td>
                  <div className="stat-cell">
                    <span>{item.payload?.damageDef || 'Bullet'} {item.payload?.damageAmountBase ? `(${item.payload.damageAmountBase})` : ''}</span>
                    {item.payload?.explosionRadius ? <span className="sub-text">Blast R={item.payload.explosionRadius}</span> : null}
                  </div>
                </td>
                <td>
                  <div className="payload-cell">
                    {frags && <span className="badge-payload" title={frags}>Frags: {frags}</span>}
                    {airburst && <span className="badge-payload fuse" title={airburst}>Airburst: {airburst}</span>}
                    {guidance && <span className="badge-payload guide" title={guidance}>{guidance}</span>}
                    {!frags && !airburst && !guidance && <span className="muted">-</span>}
                  </div>
                </td>
                <td>
                  <span className="num font-mono">{item.direct?.speed ? `${item.direct.speed} c/s` : (item.hasIndirectMode ? '0 (Artillery)' : '-')}</span>
                </td>
                <td>
                  <div className="actions-cell">
                    <Button small icon="edit" onClick={() => onEdit(item.defName)}>Edit</Button>
                    {item.modified && (
                      <Button small variant="danger" icon="revert" onClick={() => onRevert(item.defName)}>Revert</Button>
                    )}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
