import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));

/** TurretEditor/ lives inside the mod root, so the mod root is one level up. */
export const APP_ROOT = path.resolve(here, '..');

/**
 * All paths are overridable via env so the test suite can point the whole
 * application at a throwaway fixture tree instead of the real mod.
 */
export const MOD_ROOT = path.resolve(process.env.AMC_MOD_ROOT || path.resolve(APP_ROOT, '..'));
export const DEFS_DIR = path.join(MOD_ROOT, 'Common', 'Defs');
export const BUILDINGS_DIR = path.join(DEFS_DIR, 'ThingDefs_Buildings');
export const TEXTURES_DIR = path.join(MOD_ROOT, 'Common', 'Textures');
export const DB_PATH = path.resolve(
  process.env.AMC_DB_PATH || path.join(MOD_ROOT, 'DevTools', 'turret_editor.db'),
);
export const PUBLIC_DIR = path.join(APP_ROOT, 'public');

export const rel = (abs) => path.relative(MOD_ROOT, abs).split(path.sep).join('/');
export const abs = (relPath) => path.resolve(MOD_ROOT, relPath.split('/').join(path.sep));
