import React from 'react';
import { Modal, Button, Pill } from './ui.jsx';
import { api } from '../api.js';
import { diffLines, collapseContext } from '../util.js';

function Pane({ side, rows, title, subtitle }) {
  return (
    <div className="diff-pane">
      <header>{title}<div className="muted mono" style={{ fontWeight: 400 }}>{subtitle}</div></header>
      <pre className="diff-code">
        {rows.map((r, i) => {
          if (r.type === 'gap') {
            return <div key={i} className="diff-line gap"><span className="no">⋯</span><span>{r.count} unchanged line{r.count > 1 ? 's' : ''}</span></div>;
          }
          const text = side === 'left' ? r.left : r.right;
          const no = side === 'left' ? r.ln : r.rn;
          const cls = r.type === 'same' ? '' : (side === 'left' ? (r.type === 'del' ? 'del' : '') : (r.type === 'add' ? 'add' : ''));
          return (
            <div key={i} className={`diff-line ${cls}`}>
              <span className="no">{no ?? ''}</span>
              <span>{text ?? ''}</span>
            </div>
          );
        })}
      </pre>
    </div>
  );
}

export default function DiffModal({ defName, turrets, onClose, onSelect }) {
  const [data, setData] = React.useState(null);
  const [error, setError] = React.useState('');

  React.useEffect(() => {
    let cancelled = false;
    setData(null); setError('');
    api.diffXml(defName)
      .then((d) => { if (!cancelled) setData(d); })
      .catch((e) => { if (!cancelled) setError(e.message); });
    return () => { cancelled = true; };
  }, [defName]);

  const turret = turrets.find((t) => t.defName === defName);

  return (
    <Modal
      title="Live XML Diff"
      subtitle={defName}
      onClose={onClose}
      headExtra={
        <select
          value={defName}
          onChange={(e) => onSelect(e.target.value)}
          style={{ padding: '5px 9px', borderRadius: 7, border: '1px solid var(--line-strong)', maxWidth: 320 }}
          aria-label="Choose a def to compare"
        >
          {turrets.map((t) => (
            <option key={t.defName} value={t.defName}>
              {t.modified ? '● ' : ''}{t.label} — {t.defName}
            </option>
          ))}
        </select>
      }
      footer={<><span className="muted">Left: raw XML on disk. Right: the same block with pending database edits applied in memory.</span><span className="spacer" /><Button onClick={onClose}>Close</Button></>}
    >
      <div className="modal-body">
        {error && <div className="note danger">{error}</div>}
        {!data && !error && <div className="empty">Building diff…</div>}
        {data && (
          <>
            <div className="row" style={{ marginBottom: 12 }}>
              <Pill tone={data.changeCount ? 'butter' : 'mint'}>
                {data.changeCount ? `${data.changeCount} pending change(s)` : 'No pending changes'}
              </Pill>
              {turret?.modified && <Pill tone="rose">Edited</Pill>}
              {data.changes.map((c, i) => (
                <Pill key={i} tone="blue" title={c.xmlPath}>
                  {c.label}: {String(c.from ?? '—')} → {String(c.to ?? '—')}
                </Pill>
              ))}
            </div>

            {data.panes.map((p) => {
              const unchanged = p.original === p.updated;
              const rows = collapseContext(diffLines(p.original, p.updated));
              return (
                <div key={p.defName} style={{ marginBottom: 16 }}>
                  <div className="row" style={{ marginBottom: 7 }}>
                    <strong style={{ fontSize: 13 }}>{p.role === 'weapon' ? 'Weapon ThingDef' : 'Building ThingDef'}</strong>
                    <span className="muted mono">{p.filePath}</span>
                    {unchanged && <Pill tone="mint">Unchanged</Pill>}
                  </div>
                  <div className="diff-grid">
                    <Pane side="left" rows={rows} title="Original (on disk)" subtitle={p.defName} />
                    <Pane side="right" rows={rows} title="Generated (pending)" subtitle={p.defName} />
                  </div>
                </div>
              );
            })}
          </>
        )}
      </div>
    </Modal>
  );
}
