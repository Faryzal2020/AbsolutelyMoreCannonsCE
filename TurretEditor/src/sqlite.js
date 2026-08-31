/**
 * SQLite adapter.
 *
 * `better-sqlite3` is the preferred driver, but it is a native addon with no
 * prebuilt binary for the newest Node releases. Node >= 22.5 ships `node:sqlite`
 * with the same synchronous, prepared-statement model, so we use whichever is
 * available and expose one small API to the rest of the app.
 */

let driver = null;

async function loadDriver() {
  if (driver) return driver;
  try {
    const mod = await import('better-sqlite3');
    driver = { kind: 'better-sqlite3', Ctor: mod.default };
  } catch {
    const { DatabaseSync } = await import('node:sqlite');
    driver = { kind: 'node:sqlite', Ctor: DatabaseSync };
  }
  return driver;
}

export async function openDatabase(file) {
  const { kind, Ctor } = await loadDriver();
  const raw = new Ctor(file);

  if (kind === 'better-sqlite3') {
    raw.pragma('journal_mode = WAL');
    raw.pragma('foreign_keys = ON');
    // The MCP server and the web editor can hold the same file open at once,
    // so wait for a writer to finish instead of failing the call outright.
    raw.pragma('busy_timeout = 5000');
    return {
      kind,
      raw,
      exec: (sql) => raw.exec(sql),
      run: (sql, ...params) => raw.prepare(sql).run(...params),
      get: (sql, ...params) => raw.prepare(sql).get(...params) ?? null,
      all: (sql, ...params) => raw.prepare(sql).all(...params),
      transaction: (fn) => raw.transaction(fn),
      close: () => raw.close(),
    };
  }

  raw.exec('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;');
  // node:sqlite returns null-prototype rows; normalise them so JSON.stringify
  // and object spread behave the way callers expect.
  const plain = (row) => (row ? { ...row } : null);
  return {
    kind,
    raw,
    exec: (sql) => raw.exec(sql),
    run: (sql, ...params) => raw.prepare(sql).run(...params),
    get: (sql, ...params) => plain(raw.prepare(sql).get(...params)),
    all: (sql, ...params) => raw.prepare(sql).all(...params).map(plain),
    transaction: (fn) => (...args) => {
      raw.exec('BEGIN');
      try {
        const out = fn(...args);
        raw.exec('COMMIT');
        return out;
      } catch (err) {
        raw.exec('ROLLBACK');
        throw err;
      }
    },
    close: () => raw.close(),
  };
}
