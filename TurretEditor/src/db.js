import fs from 'node:fs';
import path from 'node:path';
import { openDatabase } from './sqlite.js';
import { DB_PATH } from './paths.js';

const SCHEMA = `
CREATE TABLE IF NOT EXISTS files (
  file_id     INTEGER PRIMARY KEY AUTOINCREMENT,
  file_path   TEXT NOT NULL UNIQUE,   -- mod-root relative, forward slashes
  abs_path    TEXT NOT NULL,
  checksum    TEXT NOT NULL,
  scanned_at  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS turrets (
  def_name             TEXT PRIMARY KEY,
  file_id              INTEGER NOT NULL REFERENCES files(file_id) ON DELETE CASCADE,
  label                TEXT,
  parent_name          TEXT,
  category             TEXT,
  designator_dropdown  TEXT,
  weapon_def_name      TEXT,
  mode                 TEXT,           -- 'direct' | 'indirect'
  max_hp               REAL,
  work_to_build        REAL,
  mass                 REAL,
  bulk                 REAL,
  cooldown_time        REAL,
  top_draw_size        REAL,
  construction_skill   INTEGER,
  power_consumption    REAL,
  is_manned            INTEGER DEFAULT 0,
  is_powered           INTEGER DEFAULT 0,
  has_fcs              INTEGER DEFAULT 0,
  has_mode_swap        INTEGER DEFAULT 0,
  swap_alt_def         TEXT,
  has_accuracy_override INTEGER DEFAULT 0,
  has_barrel_ext       INTEGER DEFAULT 0,
  has_recoil_anim      INTEGER DEFAULT 0,
  has_rotary_anim      INTEGER DEFAULT 0,
  has_smoker           INTEGER DEFAULT 0,
  has_selectable_bursts INTEGER DEFAULT 0,
  size                 TEXT,
  modified             INTEGER NOT NULL DEFAULT 0,
  data_json            TEXT NOT NULL,  -- live, editable state
  original_json        TEXT NOT NULL,  -- pristine state as read from disk
  updated_at           TEXT
);

CREATE TABLE IF NOT EXISTS costs (
  cost_id    INTEGER PRIMARY KEY AUTOINCREMENT,
  def_name   TEXT NOT NULL REFERENCES turrets(def_name) ON DELETE CASCADE,
  thing_def  TEXT NOT NULL,
  count      INTEGER NOT NULL,
  ordinal    INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS weapons (
  weapon_def_name    TEXT PRIMARY KEY,
  def_name           TEXT NOT NULL REFERENCES turrets(def_name) ON DELETE CASCADE,
  file_id            INTEGER REFERENCES files(file_id) ON DELETE SET NULL,
  label              TEXT,
  verb_class         TEXT,
  cooldown           REAL,
  sights_efficiency  REAL,
  shot_spread        REAL,
  sway_factor        REAL,
  warmup_time        REAL,
  burst_count        INTEGER,
  ticks_between      INTEGER,
  min_range          REAL,
  max_range          REAL,
  ammo_set           TEXT,
  magazine_size      INTEGER,
  reload_time        REAL
);

CREATE TABLE IF NOT EXISTS mod_extensions (
  ext_id     INTEGER PRIMARY KEY AUTOINCREMENT,
  def_name   TEXT NOT NULL REFERENCES turrets(def_name) ON DELETE CASCADE,
  kind       TEXT NOT NULL,   -- 'modExtension' | 'comp'
  ext_class  TEXT NOT NULL,
  params_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ammo (
  def_name               TEXT PRIMARY KEY,
  file_id                INTEGER REFERENCES files(file_id) ON DELETE CASCADE,
  label                  TEXT,
  ammo_family            TEXT,
  ammo_set_name          TEXT,
  indirect_ammo_set_name TEXT,
  ammo_class             TEXT,
  has_direct_mode        INTEGER DEFAULT 1,
  has_indirect_mode      INTEGER DEFAULT 1,
  direct_bullet_def      TEXT,
  indirect_bullet_def    TEXT,
  market_value           REAL,
  mass                   REAL,
  bulk                   REAL,
  modified               INTEGER NOT NULL DEFAULT 0,
  data_json              TEXT NOT NULL,
  original_json          TEXT NOT NULL,
  updated_at             TEXT
);

CREATE TABLE IF NOT EXISTS ammo_recipes (
  recipe_id        INTEGER PRIMARY KEY AUTOINCREMENT,
  def_name         TEXT NOT NULL REFERENCES ammo(def_name) ON DELETE CASCADE,
  recipe_def       TEXT NOT NULL,
  work_amount      INTEGER,
  yield_count      INTEGER,
  ingredients_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS meta (
  key   TEXT PRIMARY KEY,
  value TEXT
);

CREATE INDEX IF NOT EXISTS idx_turrets_category ON turrets(category);
CREATE INDEX IF NOT EXISTS idx_turrets_modified ON turrets(modified);
CREATE INDEX IF NOT EXISTS idx_costs_def       ON costs(def_name);
CREATE INDEX IF NOT EXISTS idx_weapons_def     ON weapons(def_name);
CREATE INDEX IF NOT EXISTS idx_ext_def         ON mod_extensions(def_name);
CREATE INDEX IF NOT EXISTS idx_ammo_family     ON ammo(ammo_family);
CREATE INDEX IF NOT EXISTS idx_ammo_modified   ON ammo(modified);
`;

let instance = null;

export async function getDb(file = DB_PATH) {
  if (instance && instance.file === file) return instance.db;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const db = await openDatabase(file);
  db.exec(SCHEMA);
  instance = { file, db };
  return db;
}

export async function closeDb() {
  if (instance) {
    try { instance.db.close(); } catch { /* already closed */ }
    instance = null;
  }
}

/** Wipe every extracted row, keeping the schema. Used at the start of extract. */
export function resetTables(db) {
  db.exec(`
    DELETE FROM ammo_recipes;
    DELETE FROM ammo;
    DELETE FROM mod_extensions;
    DELETE FROM weapons;
    DELETE FROM costs;
    DELETE FROM turrets;
    DELETE FROM files;
  `);
}

export function setMeta(db, key, value) {
  db.run('INSERT INTO meta(key, value) VALUES(?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value', key, String(value));
}

export function getMeta(db, key) {
  return db.get('SELECT value FROM meta WHERE key = ?', key)?.value ?? null;
}
