import React from 'react';
import { Modal, Button, Pill, Field } from './ui.jsx';
import { api } from '../api.js';
import { downloadUrl } from '../util.js';

const FORMATS = [
  { id: 'csv', label: 'CSV', hint: 'One table. Opens anywhere — Excel, Numbers, Sheets, pandas.' },
  { id: 'xlsx', label: 'Excel workbook', hint: 'All three tables as sheets, with frozen headers and filters.' },
];

const CSV_DATASETS = [
  { id: 'turrets', label: 'Turret Matrix (full)' },
  { id: 'summary', label: 'Turret Matrix (summary)' },
  { id: 'costs', label: 'Resource Costs' },
  { id: 'audit', label: 'Audit Findings' },
];

const SCOPES = [
  { id: 'all', label: 'All turrets' },
  { id: 'modified', label: 'Modified only' },
];

/** Group the flat column list back into its logical sections for the preview. */
function groupColumns(columns) {
  const groups = [];
  for (const col of columns) {
    const name = col.group || 'Columns';
    let g = groups.find((x) => x.name === name);
    if (!g) groups.push((g = { name, columns: [] }));
    g.columns.push(col);
  }
  return groups;
}

export default function ExportModal({ turrets, onClose, toast }) {
  const [format, setFormat] = React.useState('csv');
  const [dataset, setDataset] = React.useState('turrets');
  const [scope, setScope] = React.useState('all');
  const [info, setInfo] = React.useState(null);
  const [error, setError] = React.useState('');

  // The workbook always contains every dataset, so preview its main sheet.
  const previewDataset = format === 'xlsx' ? 'turrets' : dataset;

  React.useEffect(() => {
    let cancelled = false;
    api.exportColumns(previewDataset)
      .then((d) => { if (!cancelled) setInfo(d); })
      .catch((e) => { if (!cancelled) setError(e.message); });
    return () => { cancelled = true; };
  }, [previewDataset]);

  const modifiedCount = turrets.filter((t) => t.modified).length;
  const rowCount = scope === 'modified' ? modifiedCount : turrets.length;

  const save = () => {
    if (scope === 'modified' && modifiedCount === 0) {
      toast('No turrets are modified — nothing to export in that scope.', 'bad');
      return;
    }
    downloadUrl(api.exportUrl({ format, dataset, scope }));
    toast(format === 'xlsx' ? 'Downloading Excel workbook…' : 'Downloading CSV…', 'good');
  };

  const groups = info ? groupColumns(info.columns) : [];

  return (
    <Modal
      title="Export Matrix"
      subtitle={format === 'xlsx' ? 'amc_turret_matrix.xlsx' : `${CSV_DATASETS.find((d) => d.id === dataset)?.label}.csv`}
      onClose={onClose}
      footer={
        <>
          <span className="muted">
            Exports the live database state, including edits you have not injected yet.
          </span>
          <span className="spacer" />
          <Button onClick={onClose}>Close</Button>
          <Button icon="download" variant="primary" disabled={!info} onClick={save}>
            Download {format === 'xlsx' ? 'Excel' : 'CSV'}
          </Button>
        </>
      }
    >
      <div className="modal-body">
        {error && <div className="note danger" style={{ marginBottom: 12 }}>{error}</div>}

        <div className="section blue">
          <h3>Format</h3>
          <div className="chips">
            {FORMATS.map((f) => (
              <button key={f.id} className="chip" aria-pressed={format === f.id} onClick={() => setFormat(f.id)}>
                {f.label}
              </button>
            ))}
          </div>
          <div className="muted" style={{ fontSize: 11.5, marginTop: 8 }}>
            {FORMATS.find((f) => f.id === format).hint}
          </div>
        </div>

        {format === 'csv' && (
          <div className="section mint">
            <h3>Table</h3>
            <div className="chips">
              {CSV_DATASETS.map((d) => (
                <button key={d.id} className="chip" aria-pressed={dataset === d.id} onClick={() => setDataset(d.id)}>
                  {d.label}
                </button>
              ))}
            </div>
          </div>
        )}

        <div className="section lilac">
          <h3>Scope</h3>
          <div className="chips">
            {SCOPES.map((s) => (
              <button key={s.id} className="chip" aria-pressed={scope === s.id} onClick={() => setScope(s.id)}>
                {s.label}{s.id === 'modified' ? ` (${modifiedCount})` : ` (${turrets.length})`}
              </button>
            ))}
          </div>
        </div>

        <div className="section butter">
          <h3>Contents</h3>
          <div className="row">
            <Pill tone="blue">{rowCount} turret{rowCount === 1 ? '' : 's'}</Pill>
            {info && format === 'csv' && <Pill tone="mint">{info.columns.length} columns</Pill>}
            {format === 'xlsx' && (
              <>
                <Pill tone="mint">Turret Systems — {info ? info.columns.length : '…'} columns</Pill>
                <Pill tone="mint">Resource Costs</Pill>
                <Pill tone="mint">Audit Findings</Pill>
              </>
            )}
          </div>
          {scope === 'modified' && modifiedCount === 0 && (
            <div className="note" style={{ marginTop: 10 }}>
              Nothing is modified right now. Switch the scope to All turrets, or edit something first.
            </div>
          )}
        </div>

        {info && (
          <div className="section slate">
            <h3>Column headers</h3>
            {groups.map((g) => (
              <div key={g.name} style={{ marginBottom: 9 }}>
                <div className="muted" style={{ fontSize: 11, fontWeight: 600, marginBottom: 4 }}>{g.name}</div>
                <div>
                  {g.columns.map((c) => (
                    <Pill key={c.key} tone={c.type === 'number' ? 'blue' : ''}>{c.header}</Pill>
                  ))}
                </div>
              </div>
            ))}
            <div className="muted" style={{ fontSize: 11.5, marginTop: 6 }}>
              Blue headers are written as real numbers so they sort and total correctly in a spreadsheet.
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
