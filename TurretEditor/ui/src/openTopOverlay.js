// Pure helpers for the Texture Inspector's open-top seat & gun shield overlay (no React, testable in node).
// Mirrors the mod's OpenTopGeometry: compass degrees clockwise (0 = North/+Z, 90 = East/+X);
// seat offsets are turret-top-local (+Z along the barrel, +X to the barrel's right).

/** Gunner head height used by the mod when the turret has no barrelHeight. */
export const DEFAULT_GUNNER_TOP = 1.45;

/**
 * RimWorld clockwise rotation around Y axis (0° = North/+Z, 90° = East/+X):
 * x' = x*cos(θ) + z*sin(θ)
 * z' = -x*sin(θ) + z*cos(θ)
 */
export function rotateVecXZ(x, z, angleDeg) {
  const rad = (angleDeg * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return {
    x: x * cos + z * sin,
    z: -x * sin + z * cos,
  };
}

/** Parse "(x, y, z)" or "x,y" into [x, y, z] */
export function parseVec3(raw, fallback = [0, 0, 0]) {
  if (raw === null || raw === undefined || raw === '') return fallback;
  const s = String(raw).replace(/[()]/g, '').trim();
  if (!s) return fallback;
  const parts = s.split(',').map((p) => parseFloat(p.trim()));
  if (parts.length === 2 && Number.isFinite(parts[0]) && Number.isFinite(parts[1])) {
    return [parts[0], 0, parts[1]];
  }
  if (parts.length >= 3 && Number.isFinite(parts[0]) && Number.isFinite(parts[1]) && Number.isFinite(parts[2])) {
    return [parts[0], parts[1], parts[2]];
  }
  return fallback;
}

/**
 * Seat & shield overlay for a turret at the given top rotation, or null when the enclosed comp has no seat.
 * Returns { seat: {x, z} (tiles from the turret centre), shield: null | {fromDeg, toDeg, full}, label }.
 */
export function openTopOverlay(turret, rotationDeg) {
  const enc = turret?.comps?.enclosed;
  if (!enc?.enabled) return null;
  const off = parseVec3(enc.seatOffset, null);
  if (!off) return null;

  const seat = rotateVecXZ(off[0], off[2], rotationDeg);
  const arc = Number(enc.shieldArc) || 0;
  const shield = arc > 0
    ? { fromDeg: rotationDeg - Math.min(arc, 360) / 2, toDeg: rotationDeg + Math.min(arc, 360) / 2, full: arc >= 360 }
    : null;

  // Head height: barrelHeight (barrel pivot height), else the mod's 1.45 fallback.
  const barrelHeight = Number(turret?.barrel?.barrelHeight);
  const top = barrelHeight > 0 ? barrelHeight : DEFAULT_GUNNER_TOP;
  const label = `shield ${enc.shieldHeight ?? 0} / head ${top} · ${enc.shieldArmor ?? 0}mm`;
  return { seat, shield, label };
}
