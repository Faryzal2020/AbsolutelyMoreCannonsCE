import React from 'react';
import { Modal, Button, Pill } from './ui.jsx';
import { api } from '../api.js';
import { downloadUrl } from '../util.js';

const RULE_NAMES = {
  hp: 'Hit points', work: 'Work to build', range: 'Range bounds', parent: 'Parent definition',
  weapon: 'Weapon definition', modeswap: 'Mode swap target', cost: 'Cost definitions',
  texture: 'Textures', unmanned: 'Unmanned turret rules', rotary: 'Rotary animation',
};

export default function AuditModal({ onClose, onEdit }) {
  const [report, setReport] = React.useState(null);
  const [error, setError] = React.useState('');

  React.useEffect(() => {
    api.audit().then(setReport).catch((e) => setError(e.message));
  }, []);

  return (
    <Modal
      title="Audit & Validation Report"
      subtitle={report ? `${report.scanned} turrets checked` : ''}
      onClose={onClose}
      footer={
        <>
          <span className="muted">
            Rules: min range above max range, non-positive hit points, missing parent / weapon / mode-swap
            definitions, invalid cost rows, missing textures and unmanned turret requirements.
          </span>
          <span className="spacer" />
          <Button icon="download" disabled={!report}
            onClick={() => downloadUrl(api.exportUrl({ format: 'csv', dataset: 'audit', scope: 'all' }))}>
            Export findings (CSV)
          </Button>
          <Button onClick={onClose}>Close</Button>
        </>
      }
    >
      <div className="modal-body">
        {error && <div className="note danger">{error}</div>}
        {!report && !error && <div className="empty">Running rule checks…</div>}

        {report && (
          <>
            <div className="row" style={{ marginBottom: 13 }}>
              <Pill tone={report.totals.errors ? 'rose' : 'mint'}>{report.totals.errors} error(s)</Pill>
              <Pill tone={report.totals.warnings ? 'butter' : 'mint'}>{report.totals.warnings} warning(s)</Pill>
              <Pill tone="blue">{report.affected} of {report.scanned} turrets affected</Pill>
            </div>

            {report.byRule.length > 0 && (
              <div className="section slate">
                <h3>By rule</h3>
                <div className="row">
                  {report.byRule.map((r) => (
                    <Pill key={r.rule} tone={r.errors ? 'rose' : 'butter'}>
                      {RULE_NAMES[r.rule] || r.rule}: {r.errors} error / {r.warnings} warn
                    </Pill>
                  ))}
                </div>
              </div>
            )}

            {report.entries.length === 0 ? (
              <div className="note good">Every turret passes all validation rules.</div>
            ) : (
              <ul className="list-plain">
                {report.entries.map((e) => (
                  <li key={e.defName} className="record">
                    <header>
                      <strong>{e.label}</strong>
                      <Pill tone="blue">{e.category}</Pill>
                      {e.errors > 0 && <Pill tone="rose">{e.errors} error(s)</Pill>}
                      {e.warnings > 0 && <Pill tone="butter">{e.warnings} warning(s)</Pill>}
                      {e.modified && <Pill tone="lilac">Edited</Pill>}
                      <span style={{ flex: 1 }} />
                      <Button icon="edit" small onClick={() => onEdit(e.defName)}>Open</Button>
                    </header>
                    <div className="cell-sub">{e.defName}</div>
                    <ul style={{ margin: '7px 0 0', paddingLeft: 18 }}>
                      {e.issues.map((issue, i) => (
                        <li key={i} style={{ color: issue.level === 'error' ? 'var(--danger)' : 'var(--warn)', fontSize: 12.5 }}>
                          {issue.message}
                        </li>
                      ))}
                    </ul>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}
