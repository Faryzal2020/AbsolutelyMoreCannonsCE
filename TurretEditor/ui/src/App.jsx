import React from 'react';
import { api } from './api.js';
import { Button, Icon, Modal } from './components/ui.jsx';
import Metrics from './components/Metrics.jsx';
import Filters from './components/Filters.jsx';
import Matrix from './components/Matrix.jsx';
import EditorModal from './components/EditorModal.jsx';
import DiffModal from './components/DiffModal.jsx';
import SnapshotsModal from './components/SnapshotsModal.jsx';
import ExportModal from './components/ExportModal.jsx';
import AuditModal from './components/AuditModal.jsx';
import InjectModal from './components/InjectModal.jsx';

const CATEGORY_FILTERS = [
  { id: 'all', label: 'All' },
  { id: 'Cannons', label: 'Cannons' },
  { id: 'Howitzers', label: 'Howitzers' },
  { id: 'Naval Guns', label: 'Naval Guns' },
  { id: 'Autocannons', label: 'Autocannons' },
  { id: 'RotaryCannons', label: 'Rotary' },
  { id: 'Unmanned', label: 'Unmanned' },
  { id: 'modified', label: 'Modified Only' },
  { id: 'warnings', label: 'Warnings Only' },
];

export default function App() {
  const [turrets, setTurrets] = React.useState([]);
  const [metrics, setMetrics] = React.useState(null);
  const [busy, setBusy] = React.useState('');
  const [toasts, setToasts] = React.useState([]);
  const [ready, setReady] = React.useState(false);

  const [search, setSearch] = React.useState('');
  const [filter, setFilter] = React.useState('all');
  const [live, setLive] = React.useState(true);

  const [undoStack, setUndoStack] = React.useState([]);
  const [redoStack, setRedoStack] = React.useState([]);

  const [editing, setEditing] = React.useState(null);
  const [diffing, setDiffing] = React.useState(null);
  const [modal, setModal] = React.useState(null); // snapshots | export | audit | inject

  const toast = React.useCallback((message, tone = '') => {
    const id = Math.random().toString(36).slice(2);
    setToasts((prev) => [...prev, { id, message, tone }]);
    setTimeout(() => setToasts((prev) => prev.filter((x) => x.id !== id)), 4200);
  }, []);

  const refresh = React.useCallback(async () => {
    const [list, m] = await Promise.all([api.turrets(), api.metrics()]);
    setTurrets(list.turrets);
    setMetrics(m);
    return list.turrets;
  }, []);

  React.useEffect(() => {
    (async () => {
      try {
        const health = await api.health();
        if (health.turrets === 0) {
          setBusy('Running first extraction...');
          await api.extract();
        }
        await refresh();
      } catch (err) {
        toast(err.message, 'bad');
      } finally {
        setBusy('');
        setReady(true);
      }
    })();
  }, [refresh, toast]);

  const withBusy = async (label, fn) => {
    setBusy(label);
    try { return await fn(); }
    catch (err) { toast(err.message, 'bad'); return null; }
    finally { setBusy(''); }
  };

  /** Apply an edit and record it on the undo stack. */
  const applyEdit = React.useCallback(async (defName, patch) => {
    const before = turrets.find((x) => x.defName === defName);
    if (!before) return null;
    try {
      const { turret } = await api.updateTurret(defName, patch);
      setTurrets((prev) => prev.map((x) => (x.defName === defName ? turret : x)));
      setUndoStack((prev) => [...prev.slice(-49), { defName, before, after: turret }]);
      setRedoStack([]);
      api.metrics().then(setMetrics).catch(() => {});
      return turret;
    } catch (err) {
      toast(err.message, 'bad');
      return null;
    }
  }, [turrets, toast]);

  const replaceWhole = async (defName, snapshot) => {
    const { turret } = await api.updateTurret(defName, snapshot);
    setTurrets((prev) => prev.map((x) => (x.defName === defName ? turret : x)));
    api.metrics().then(setMetrics).catch(() => {});
    return turret;
  };

  const undo = async () => {
    const op = undoStack[undoStack.length - 1];
    if (!op) return;
    await withBusy('Undoing...', async () => {
      await replaceWhole(op.defName, op.before);
      setUndoStack((prev) => prev.slice(0, -1));
      setRedoStack((prev) => [...prev, op]);
    });
  };

  const redo = async () => {
    const op = redoStack[redoStack.length - 1];
    if (!op) return;
    await withBusy('Redoing...', async () => {
      await replaceWhole(op.defName, op.after);
      setRedoStack((prev) => prev.slice(0, -1));
      setUndoStack((prev) => [...prev, op]);
    });
  };

  const runExtract = () => withBusy('Extracting XML into SQLite...', async () => {
    const result = await api.extract();
    await refresh();
    setUndoStack([]); setRedoStack([]);
    toast(`Extracted ${result.counts.turrets} turrets from ${result.counts.files} files in ${result.durationMs} ms.`, 'good');
  });

  const revertAll = () => withBusy('Reverting all edits...', async () => {
    const { reverted } = await api.revertAll();
    await refresh();
    setUndoStack([]); setRedoStack([]);
    toast(reverted ? `Reverted ${reverted} turret(s).` : 'Nothing to revert.', 'good');
  });

  const revertOne = (defName) => withBusy('Reverting...', async () => {
    await api.revertTurret(defName);
    await refresh();
    toast(`Reverted ${defName}.`, 'good');
  });

  const restoreSnapshot = (snapshot) => withBusy('Restoring snapshot...', async () => {
    const { restored } = await api.restore(snapshot.turrets);
    await refresh();
    setUndoStack([]); setRedoStack([]);
    toast(`Restored "${snapshot.label}" across ${restored} turret(s).`, 'good');
  });

  const modifiedCount = turrets.filter((t) => t.modified).length;

  const visible = React.useMemo(() => {
    const q = search.trim().toLowerCase();
    return turrets.filter((t) => {
      if (filter === 'modified' && !t.modified) return false;
      if (filter === 'warnings' && t.warnings.length === 0) return false;
      if (filter !== 'all' && filter !== 'modified' && filter !== 'warnings'
        && t.category.toLowerCase() !== filter.toLowerCase()) return false;
      if (!q) return true;
      return [
        t.defName, t.label, t.category, t.parentName, t.weaponDefName,
        t.ammo.ammoSet, t.ballistics.verbClass, t.comps.swapAltDef, t.filePath,
      ].some((v) => String(v || '').toLowerCase().includes(q));
    });
  }, [turrets, search, filter]);

  const editingTurret = editing ? turrets.find((t) => t.defName === editing) : null;

  return (
    <div className="app">
      <header className="masthead">
        <div>
          <h1>AMC Turret Editor</h1>
          <div className="sub">RimWorld ThingDef matrix — Common/Defs/ThingDefs_Buildings</div>
        </div>
        <span className="spacer" />

        <div className="mode-switch" role="group" aria-label="Injection mode">
          <button className="live" aria-pressed={live} onClick={() => setLive(true)}>Live</button>
          <button className="sandbox" aria-pressed={!live} onClick={() => setLive(false)}>Sandbox</button>
        </div>

        <Button icon="play" variant="primary" onClick={runExtract}>Extract XML</Button>
        <Button
          icon="inject"
          variant="success"
          disabled={!live || modifiedCount === 0}
          title={!live ? 'Sandbox mode: injection is disabled' : modifiedCount === 0 ? 'No pending changes' : 'Write pending changes to XML'}
          onClick={() => setModal('inject')}
        >
          Inject Diffs{modifiedCount ? ` (${modifiedCount})` : ''}
        </Button>
      </header>

      <div className="page">
        {!live && (
          <div className="note">
            Sandbox mode is on. Edits are still saved to the SQLite database, but nothing can be written to your
            XML files until you switch back to Live.
          </div>
        )}

        <Metrics metrics={metrics} modifiedCount={modifiedCount} />

        <div className="panel">
          <div className="panel-head">
            <h2>Turret Matrix</h2>
            <span className="muted">{visible.length} of {turrets.length} shown</span>
            <span className="spacer" />
            <Button icon="undo" small disabled={!undoStack.length} onClick={undo}>Undo</Button>
            <Button icon="redo" small disabled={!redoStack.length} onClick={redo}>Redo</Button>
            <span className="divider" />
            <Button icon="camera" small onClick={() => setModal('snapshots')}>Snapshots</Button>
            <Button icon="warn" small onClick={() => setModal('audit')}>
              Audit{metrics?.errors ? ` (${metrics.errors})` : ''}
            </Button>
            <Button icon="download" small onClick={() => setModal('export')}>Export Matrix</Button>
            <Button icon="revert" small variant="danger" disabled={!modifiedCount} onClick={revertAll}>Revert All</Button>
          </div>

          <div className="panel-body" style={{ borderBottom: '1px solid var(--line)' }}>
            <Filters
              search={search} onSearch={setSearch}
              filter={filter} onFilter={setFilter}
              options={CATEGORY_FILTERS}
            />
          </div>

          <Matrix
            turrets={visible}
            ready={ready}
            onEdit={setEditing}
            onDiff={setDiffing}
            onRevert={revertOne}
          />
        </div>
      </div>

      {editingTurret && (
        <EditorModal
          turret={editingTurret}
          allTurrets={turrets}
          onApply={applyEdit}
          onClose={() => setEditing(null)}
          onDiff={(defName) => { setEditing(null); setDiffing(defName); }}
        />
      )}
      {diffing && <DiffModal defName={diffing} turrets={turrets} onClose={() => setDiffing(null)} onSelect={setDiffing} />}
      {modal === 'snapshots' && (
        <SnapshotsModal turrets={turrets} onRestore={restoreSnapshot} onClose={() => setModal(null)} toast={toast} />
      )}
      {modal === 'export' && <ExportModal turrets={turrets} onClose={() => setModal(null)} toast={toast} />}
      {modal === 'audit' && <AuditModal onClose={() => setModal(null)} onEdit={(d) => { setModal(null); setEditing(d); }} />}
      {modal === 'inject' && (
        <InjectModal
          onClose={() => setModal(null)}
          onDone={async (message) => { await refresh(); setUndoStack([]); setRedoStack([]); toast(message, 'good'); }}
          toast={toast}
        />
      )}

      {busy && <div className="busy"><div className="card">{busy}</div></div>}

      <div className="toasts">
        {toasts.map((t) => (
          <div key={t.id} className={`toast ${t.tone}`}>
            <Icon name={t.tone === 'bad' ? 'warn' : 'check'} /> {t.message}
          </div>
        ))}
      </div>
    </div>
  );
}
