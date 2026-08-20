/**
 * Labelled state snapshots kept in IndexedDB, so they survive a page reload
 * without ever leaving the machine or touching the mod files.
 */
const DB_NAME = 'amc-turret-editor';
const STORE = 'snapshots';

function open() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(STORE)) {
        db.createObjectStore(STORE, { keyPath: 'id' });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

function tx(mode, run) {
  return open().then((db) => new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE, mode);
    const req = run(transaction.objectStore(STORE));
    transaction.oncomplete = () => { db.close(); resolve(req?.result); };
    transaction.onerror = () => { db.close(); reject(transaction.error); };
  }));
}

export const snapshots = {
  list: async () => {
    const all = (await tx('readonly', (store) => store.getAll())) || [];
    return all.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  },
  create: async (label, turrets) => {
    const record = {
      id: `snap_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`,
      label: label || `Snapshot ${new Date().toLocaleString()}`,
      createdAt: new Date().toISOString(),
      turretCount: turrets.length,
      modifiedCount: turrets.filter((x) => x.modified).length,
      turrets,
    };
    await tx('readwrite', (store) => store.put(record));
    return record;
  },
  remove: (id) => tx('readwrite', (store) => store.delete(id)),
  get: (id) => tx('readonly', (store) => store.get(id)),
};
