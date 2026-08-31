import React from 'react';
import { Modal, Button, Pill } from './ui.jsx';
import { api } from '../api.js';

const REASONS = {
  'component-not-declared':
    'The component or extension is only inherited from a parent def. Adding a duplicate here would stack '
    + 'a second component instead of overriding one, so this edit was left alone — enable the component on '
    + 'the Comps tab first.',
  'def-not-found': 'The def could not be located in its file.',
  'file-missing': 'The target XML file no longer exists on disk.',
};

/** Preview the pending changes, then commit them with a dry run first. */
export default function InjectModal({ onClose, onDone, toast }) {
  const [preview, setPreview] = React.useState(null);
  const [result, setResult] = React.useState(null);
  const [error, setError] = React.useState('');
  const [running, setRunning] = React.useState(false);

  React.useEffect(() => {
    api.inject({ dryRun: true }).then(setPreview).catch((e) => setError(e.message));
  }, []);

  const commit = async () => {
    setRunning(true);
    try {
      const res = await api.inject({});
      setResult(res);
      await onDone(`Injected ${res.appliedCount} change(s) into ${res.filesWritten} file(s).`);
    } catch (e) {
      setError(e.message);
    } finally {
      setRunning(false);
    }
  };

  const rollback = async () => {
    setRunning(true);
    try {
      const res = await api.rollback({});
      await onDone(`Restored ${res.restoredCount} file(s) from backups.`);
      onClose();
    } catch (e) {
      setError(e.message);
    } finally {
      setRunning(false);
    }
  };

  const data = result || preview;

  return (
    <Modal
      title={result ? 'Injection complete' : 'Inject diffs into XML'}
      subtitle={result ? `${result.filesWritten} file(s) written` : 'Dry run preview'}
      onClose={onClose}
      footer={
        result ? (
          <>
            <span className="muted">Backups were written next to each file as .xml.bak.</span>
            <span className="spacer" />
            <Button icon="revert" variant="danger" disabled={running} onClick={rollback}>Roll back all backups</Button>
            <Button variant="primary" onClick={onClose}>Done</Button>
          </>
        ) : (
          <>
            <span className="muted">Only the listed tags are rewritten; the rest of each file is untouched.</span>
            <span className="spacer" />
            <Button onClick={onClose}>Cancel</Button>
            <Button icon="inject" variant="success" disabled={!preview || running || preview.appliedCount === 0} onClick={commit}>
              {running ? 'Writing…' : 'Write to XML'}
            </Button>
          </>
        )
      }
    >
      <div className="modal-body">
        {error && <div className="note danger" style={{ marginBottom: 12 }}>{error}</div>}
        {!data && !error && <div className="empty">Calculating diffs…</div>}

        {data && (
          <>
            <div className="row" style={{ marginBottom: 13 }}>
              <Pill tone="blue">{data.defCount} def(s)</Pill>
              <Pill tone="mint">{data.appliedCount} change(s) {result ? 'applied' : 'ready'}</Pill>
              {data.skippedCount > 0 && <Pill tone="rose">{data.skippedCount} skipped</Pill>}
              {result && <Pill tone="butter">{result.backups.length} backup(s) created</Pill>}
            </div>

            {data.appliedCount === 0 && !result && (
              <div className="note">There is nothing to inject. Edit a turret first.</div>
            )}

            {data.applied.length > 0 && (
              <div className="section mint">
                <h3>{result ? 'Applied' : 'Will be written'}</h3>
                <table className="matrix" style={{ fontSize: 12 }}>
                  <thead>
                    <tr><th>Def</th><th>Field</th><th>XML path</th><th>From</th><th>To</th></tr>
                  </thead>
                  <tbody>
                    {data.applied.map((c, i) => (
                      <tr key={i}>
                        <td className="mono">{c.defName}</td>
                        <td>{c.label}</td>
                        <td className="mono muted">{c.xmlPath}</td>
                        <td>{String(c.from ?? '—')}</td>
                        <td><strong>{String(c.to ?? '—')}</strong></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {data.skipped.length > 0 && (
              <div className="section peach">
                <h3>Skipped</h3>
                <ul className="list-plain">
                  {data.skipped.map((c, i) => (
                    <li key={i} className="record">
                      <header>
                        <strong>{c.label}</strong>
                        <span className="mono muted">{c.defName}</span>
                      </header>
                      <div style={{ fontSize: 12.5 }}>{REASONS[c.reason] || c.reason}</div>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {result && result.files.length > 0 && (
              <div className="section slate">
                <h3>Files written</h3>
                {result.files.map((f) => <div key={f} className="mono" style={{ fontSize: 11.5 }}>{f}</div>)}
              </div>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}
