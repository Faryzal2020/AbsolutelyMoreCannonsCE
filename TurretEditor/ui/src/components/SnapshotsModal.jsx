import React from 'react';
import { Modal, Button, Pill } from './ui.jsx';
import { snapshots } from '../snapshots.js';

export default function SnapshotsModal({ turrets, onRestore, onClose, toast }) {
  const [items, setItems] = React.useState([]);
  const [label, setLabel] = React.useState('');
  const [error, setError] = React.useState('');

  const reload = React.useCallback(() => {
    snapshots.list().then(setItems).catch((e) => setError(e.message));
  }, []);
  React.useEffect(reload, [reload]);

  const create = async () => {
    try {
      const rec = await snapshots.create(label.trim(), turrets);
      setLabel('');
      reload();
      toast(`Saved snapshot "${rec.label}".`, 'good');
    } catch (e) { setError(e.message); }
  };

  const remove = async (id) => {
    await snapshots.remove(id);
    reload();
  };

  return (
    <Modal
      title="Snapshots & History"
      subtitle="Stored locally in your browser's IndexedDB"
      narrow
      onClose={onClose}
      footer={<><span className="muted">Snapshots capture the database state, not your XML files.</span><span className="spacer" /><Button onClick={onClose}>Close</Button></>}
    >
      <div className="modal-body">
        {error && <div className="note danger" style={{ marginBottom: 12 }}>{error}</div>}

        <div className="section blue">
          <h3>Create snapshot</h3>
          <div className="row">
            <input
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              placeholder="Label (optional)"
              style={{ flex: 1, padding: '7px 11px', borderRadius: 7, border: '1px solid var(--line-strong)' }}
            />
            <Button icon="camera" variant="primary" onClick={create}>Capture {turrets.length} turrets</Button>
          </div>
        </div>

        {items.length === 0 ? (
          <div className="empty">No snapshots saved yet.</div>
        ) : (
          <ul className="list-plain">
            {items.map((s) => (
              <li key={s.id} className="record">
                <header>
                  <strong>{s.label}</strong>
                  <Pill tone="blue">{s.turretCount} turrets</Pill>
                  {s.modifiedCount > 0 && <Pill tone="butter">{s.modifiedCount} modified</Pill>}
                  <span className="spacer" style={{ flex: 1 }} />
                  <Button icon="revert" small variant="primary"
                    onClick={() => { onRestore(s); onClose(); }}>Restore</Button>
                  <Button icon="trash" small variant="danger" aria-label="Delete snapshot"
                    onClick={() => remove(s.id)} />
                </header>
                <div className="muted" style={{ fontSize: 11.5 }}>{new Date(s.createdAt).toLocaleString()}</div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
