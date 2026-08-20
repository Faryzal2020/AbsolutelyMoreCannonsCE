import React from 'react';
import { Modal, Button, Icon, Pill } from './ui.jsx';
import { TABS } from './TabPanels.jsx';
import ParallelView, { DEFAULT_LINKED } from './ParallelView.jsx';
import { mergeDeep, patchFor, getPath } from '../util.js';

/** Every dotted key that differs between two turret drafts. */
function dirtyKeys(draft, original, prefix = '', out = new Set()) {
  for (const [k, v] of Object.entries(draft)) {
    const key = prefix ? `${prefix}.${k}` : k;
    const o = original?.[k];
    if (v && typeof v === 'object' && !Array.isArray(v)) dirtyKeys(v, o || {}, key, out);
    else if (JSON.stringify(v) !== JSON.stringify(o)) out.add(key);
  }
  return out;
}

export default function EditorModal({ turret, allTurrets, onApply, onClose, onDiff }) {
  const partner = turret.linkedModeDef
    ? allTurrets.find((t) => t.defName === turret.linkedModeDef)
    : null;

  // In a dual-mode pair, always present the direct def on the left.
  const [primary, secondary] = React.useMemo(() => {
    if (!partner) return [turret, null];
    return turret.mode === 'indirect' ? [partner, turret] : [turret, partner];
  }, [turret, partner]);

  const [view, setView] = React.useState(partner ? (turret.mode === 'indirect' ? 'indirect' : 'direct') : 'direct');
  const [tab, setTab] = React.useState('basic');
  const [directDraft, setDirectDraft] = React.useState(primary);
  const [indirectDraft, setIndirectDraft] = React.useState(secondary);
  const [linked, setLinked] = React.useState(new Set(DEFAULT_LINKED));
  const [saving, setSaving] = React.useState(false);

  const setDirect = (key, value) => setDirectDraft((d) => mergeDeep(d, patchFor(key, value)));
  const setIndirect = (key, value) => setIndirectDraft((d) => (d ? mergeDeep(d, patchFor(key, value)) : d));

  const dirtyDirect = dirtyKeys(directDraft, primary);
  const dirtyIndirect = indirectDraft ? dirtyKeys(indirectDraft, secondary) : new Set();
  const isDirty = dirtyDirect.size > 0 || dirtyIndirect.size > 0;

  const toggleLink = (key) => setLinked((prev) => {
    const next = new Set(prev);
    if (next.has(key)) next.delete(key); else next.add(key);
    return next;
  });

  const syncBuildingStats = () => {
    if (!indirectDraft) return;
    let next = indirectDraft;
    for (const key of DEFAULT_LINKED) {
      next = mergeDeep(next, patchFor(key, getPath(directDraft, key)));
    }
    setIndirectDraft(next);
  };

  const apply = async () => {
    setSaving(true);
    try {
      if (dirtyDirect.size) await onApply(primary.defName, directDraft);
      if (indirectDraft && dirtyIndirect.size) await onApply(secondary.defName, indirectDraft);
      onClose();
    } finally {
      setSaving(false);
    }
  };

  const close = () => {
    if (!isDirty || window.confirm('Discard unsaved changes to this turret?')) onClose();
  };

  const active = view === 'indirect' ? indirectDraft : directDraft;
  const setActive = view === 'indirect' ? setIndirect : setDirect;
  const activeDirty = view === 'indirect' ? dirtyIndirect : dirtyDirect;
  const ActivePanel = TABS.find((t) => t.id === tab)?.Panel;

  return (
    <Modal
      title={`Edit ${turret.label}`}
      subtitle={`${turret.defName} · ${turret.filePath}`}
      onClose={close}
      headExtra={
        <>
          {isDirty && <Pill tone="butter">Unsaved changes</Pill>}
          <Button icon="diff" small onClick={() => onDiff(turret.defName)}>XML Diff</Button>
        </>
      }
      footer={
        <>
          <span className="muted">
            {isDirty
              ? `${dirtyDirect.size + dirtyIndirect.size} field(s) changed — not written to XML until you inject.`
              : 'No changes yet.'}
          </span>
          <span className="spacer" />
          <Button onClick={close}>Cancel</Button>
          <Button variant="primary" icon="check" disabled={!isDirty || saving} onClick={apply}>
            {saving ? 'Applying…' : 'Apply Changes'}
          </Button>
        </>
      }
    >
      {partner && (
        <div className="tabs" style={{ paddingTop: 8 }}>
          <button aria-selected={view === 'direct'} onClick={() => setView('direct')}>
            <Icon name="target" /> Direct Mode
          </button>
          <button aria-selected={view === 'indirect'} onClick={() => setView('indirect')}>
            <Icon name="globe" /> Indirect Mode
          </button>
          <button aria-selected={view === 'dual'} onClick={() => setView('dual')}>
            <Icon name="balance" /> Dual-Mode Parallel &amp; Sync
          </button>
        </div>
      )}

      {view !== 'dual' && (
        <div className="tabs">
          {TABS.map((t) => (
            <button key={t.id} aria-selected={tab === t.id} onClick={() => setTab(t.id)}>
              <Icon name={t.icon} /> {t.label}
            </button>
          ))}
        </div>
      )}

      <div className="modal-body">
        {view === 'dual' && indirectDraft ? (
          <ParallelView
            direct={directDraft}
            indirect={indirectDraft}
            setDirect={setDirect}
            setIndirect={setIndirect}
            linked={linked}
            toggleLink={toggleLink}
            onSyncBuildingStats={syncBuildingStats}
          />
        ) : (
          ActivePanel && (
            <ActivePanel draft={active} set={setActive} dirty={activeDirty} allTurrets={allTurrets} />
          )
        )}
      </div>
    </Modal>
  );
}
