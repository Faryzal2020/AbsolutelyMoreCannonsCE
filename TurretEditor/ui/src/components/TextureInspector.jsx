import React from 'react';
import { Button, Icon, Pill } from './ui.jsx';
import { openTopOverlay, parseVec3, rotateVecXZ } from '../openTopOverlay.js';

const SUBFOLDER_ORDER = [
  'Autocannons',
  'Cannons',
  'Howitzers',
  'Naval Guns',
  'RotaryCannons',
  'Unmanned',
];

/** Parse "(x,y)" or "x,y" or single number "s" into [w, h] */
function parseVec2(raw, fallback = [1, 1]) {
  if (raw === null || raw === undefined || raw === '') return fallback;
  if (typeof raw === 'number') return [raw, raw];
  const s = String(raw).replace(/[()]/g, '').trim();
  if (!s) return fallback;
  const parts = s.split(',').map((p) => parseFloat(p.trim()));
  if (parts.length === 1 && Number.isFinite(parts[0])) {
    return [parts[0], parts[0]];
  }
  if (parts.length >= 2 && Number.isFinite(parts[0]) && Number.isFinite(parts[1])) {
    return [parts[0], parts[1]];
  }
  return fallback;
}

/** Parse "(r,g,b,a)" or "(r,g,b)" where channels are 0..1 (or 0..255) into { r, g, b, a } in 0..1 */
function parseColorRGBA(raw, fallback = { r: 1, g: 0.75, b: 0.35, a: 1 }) {
  if (!raw) return fallback;
  const s = String(raw).replace(/[()]/g, '').trim();
  const parts = s.split(',').map((p) => parseFloat(p.trim()));
  if (parts.length < 3 || parts.slice(0, 3).some((n) => !Number.isFinite(n))) return fallback;
  const scale = parts[0] > 1 || parts[1] > 1 || parts[2] > 1 ? 1 / 255 : 1;
  return {
    r: parts[0] * scale,
    g: parts[1] * scale,
    b: parts[2] * scale,
    a: Number.isFinite(parts[3]) ? (parts[3] > 1 ? parts[3] / 255 : parts[3]) : 1,
  };
}

/**
 *Exact port of RecoilAnimation.recoilCurve.Evaluate(progress) from TurretBarrelExtension.cs
 */
function evaluateRecoilCurve(progress, recoilDuration, returnDuration, useRecoilCurve, useReturnCurve) {
  const p = Math.max(0, Math.min(1, progress));
  const totalTicks = Math.max(1, recoilDuration + returnDuration);
  const recoilProgress = recoilDuration / totalTicks;

  if (recoilProgress <= 0) return 0;

  if (p <= recoilProgress) {
    // Recoil phase: 0 -> 1
    if (!useRecoilCurve) {
      return p / recoilProgress;
    }
    const fastAccelProgress = recoilProgress * 0.1;
    const fastAccelDistance = 0.7;
    if (p <= fastAccelProgress) {
      return fastAccelProgress > 0 ? (p / fastAccelProgress) * fastAccelDistance : fastAccelDistance;
    }
    const t = (p - fastAccelProgress) / (recoilProgress - fastAccelProgress);
    const easeOut = 1 - Math.pow(1 - t, 3);
    return fastAccelDistance + (1 - fastAccelDistance) * easeOut;
  }

  // Return phase: 1 -> 0
  const returnSpan = 1 - recoilProgress;
  if (returnSpan <= 0) return 0;

  if (!useReturnCurve) {
    return 1 - (p - recoilProgress) / returnSpan;
  }
  const returnFastAccelProgress = recoilProgress + returnSpan * 0.1;
  const returnFastAccelDistance = 0.3;
  if (p <= returnFastAccelProgress) {
    const localT = (p - recoilProgress) / (returnSpan * 0.1);
    return 1 - localT * (1 - returnFastAccelDistance);
  }
  const t = (p - returnFastAccelProgress) / (returnSpan * 0.9);
  const easeOut = 1 - Math.pow(1 - t, 3);
  return returnFastAccelDistance * (1 - easeOut);
}

/**
 * Evaluate firing flash intensity curve from FiringAnimation.flashIntensityCurve
 * Points: (0, 1) -> (0.3, 0.9) -> (0.6, 0.6) -> (1, 0)
 */
function evaluateFlashIntensity(progress) {
  const p = Math.max(0, Math.min(1, progress));
  if (p <= 0.3) return 1 - (p / 0.3) * 0.1;
  if (p <= 0.6) return 0.9 - ((p - 0.3) / 0.3) * 0.3;
  return 0.6 * (1 - (p - 0.6) / 0.4);
}

/**
 * Convert a dark/black-background additive sprite sheet (like MuzzleFlashOne5x5.png)
 * into an alpha-blended canvas so it renders cleanly over any background.
 */
function processMuzzleFlashImage(img) {
  const canvas = document.createElement('canvas');
  canvas.width = img.naturalWidth || img.width;
  canvas.height = img.naturalHeight || img.height;
  const ctx = canvas.getContext('2d');
  ctx.drawImage(img, 0, 0);
  try {
    const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const d = imgData.data;
    // Check top-left corner pixel: MuzzleFlashOne5x5 has opaque black (0,0,0,255) background
    if (d[3] > 240 && d[0] < 15 && d[1] < 15 && d[2] < 15) {
      for (let i = 0; i < d.length; i += 4) {
        const r = d[i], g = d[i + 1], b = d[i + 2];
        const lum = Math.max(r, g, b);
        d[i + 3] = lum;
      }
      ctx.putImageData(imgData, 0, 0);
    }
  } catch {
    // Fallback if tainted
  }
  return canvas;
}

const DEFAULT_LAYERS = {
  grid: true,
  footprint: true,
  canvasBounds: false,
  base: true,
  underBarrel: true,
  barrel: true,
  top: true,
  muzzleFlash: true,
  firingFlash: true,
  interactionCell: true,
  seat: true,
  shield: true,
  projectileSpawn: true,
  smokeMuzzle: true,
  smokeHeat: true,
  smokeShockwave: true,
};

export default function TextureInspector({ turrets, onEdit }) {
  const [sidebarSearch, setSidebarSearch] = React.useState('');
  const [collapsedFolders, setCollapsedFolders] = React.useState({});
  const [selectedFilePath, setSelectedFilePath] = React.useState('');
  const [activeMode, setActiveMode] = React.useState('direct'); // 'direct' | 'indirect'
  const [rotation, setRotation] = React.useState(90); // 0..360, default 90° (East)
  const [layers, setLayers] = React.useState(DEFAULT_LAYERS);
  const [markerShape, setMarkerShape] = React.useState('X'); // 'X' | 'O'
  const [zoom, setZoom] = React.useState(44); // pixels per tile
  const [pan, setPan] = React.useState({ x: 0, y: 0 });
  const [darkBg, setDarkBg] = React.useState(true);
  const [burstUiState, setBurstUiState] = React.useState({ active: false, shotsFired: 0, totalShots: 0 });

  const canvasRef = React.useRef(null);
  const imageCacheRef = React.useRef(new Map());
  const [imageTick, setImageTick] = React.useState(0);
  const dragRef = React.useRef(null);

  // Simulation state ref (runs at 60 ticks/sec via requestAnimationFrame when active)
  const simRef = React.useRef({
    active: false,
    shotsRemaining: 0,
    totalShots: 0,
     shotsFired: 0,
    ticksToNextShot: 0,
    ticksBetweenShots: 10,
    nextSequentialBarrel: 0,
    mfAltIndex: 0,
    barrelRecoilTicks: [],
    barrelRecoilDistances: [],
    barrelFiringTicks: [],
    mfTicksRemaining: 0,
    mfDuration: 5,
    mfRow: 0,
    mfActiveIndex: -1,
    spinFrame: 0,
    lastTimestamp: 0,
    tickAccumulator: 0,
  });

  // Group turrets by XML file inside Common/Defs/ThingDefs_Buildings/<Subfolder>/<File>.xml
  const xmlFilesByFolder = React.useMemo(() => {
    const byFile = new Map();
    for (const t of turrets) {
      if (!t.filePath) continue;
      if (!byFile.has(t.filePath)) {
        const parts = t.filePath.split('/');
        const fileName = parts[parts.length - 1];
        const folder = t.category || parts[parts.length - 2] || 'Other';
        byFile.set(t.filePath, {
          filePath: t.filePath,
          fileName,
          baseName: fileName.replace(/\.xml$/i, ''),
          folder,
          direct: null,
          indirect: null,
          turrets: [],
        });
      }
      const entry = byFile.get(t.filePath);
      entry.turrets.push(t);
      if (t.mode === 'indirect') {
        entry.indirect = t;
      } else {
        entry.direct = t;
      }
    }

    const q = sidebarSearch.trim().toLowerCase();
    const groups = new Map();
    for (const folder of SUBFOLDER_ORDER) groups.set(folder, []);

    for (const entry of byFile.values()) {
      const primary = entry.direct || entry.indirect || entry.turrets[0];
      entry.displayLabel = (primary?.label || entry.baseName).replace(/\s*\(indirect\)\s*$/i, '');
      if (q) {
        const hay = `${entry.fileName} ${entry.displayLabel} ${entry.folder} ${entry.turrets.map((x) => x.defName).join(' ')}`.toLowerCase();
        if (!hay.includes(q)) continue;
      }
      if (!groups.has(entry.folder)) groups.set(entry.folder, []);
      groups.get(entry.folder).push(entry);
    }

    for (const list of groups.values()) {
      list.sort((a, b) => a.fileName.localeCompare(b.fileName, undefined, { numeric: true }));
    }

    return [...groups.entries()].filter(([, list]) => list.length > 0);
  }, [turrets, sidebarSearch]);

  // Select first file automatically if none selected
  React.useEffect(() => {
    if (!selectedFilePath && xmlFilesByFolder.length > 0) {
      const firstGroup = xmlFilesByFolder[0][1];
      if (firstGroup.length > 0) {
        setSelectedFilePath(firstGroup[0].filePath);
      }
    }
  }, [xmlFilesByFolder, selectedFilePath]);

  const currentFileEntry = React.useMemo(() => {
    for (const [, list] of xmlFilesByFolder) {
      const found = list.find((f) => f.filePath === selectedFilePath);
      if (found) return found;
    }
    for (const t of turrets) {
      if (t.filePath === selectedFilePath) {
        const sameFile = turrets.filter((x) => x.filePath === selectedFilePath);
        const direct = sameFile.find((x) => x.mode !== 'indirect') || null;
        const indirect = sameFile.find((x) => x.mode === 'indirect') || null;
        const parts = selectedFilePath.split('/');
        const fileName = parts[parts.length - 1];
        return {
          filePath: selectedFilePath,
          fileName,
          baseName: fileName.replace(/\.xml$/i, ''),
          folder: t.category,
          direct,
          indirect,
          turrets: sameFile,
          displayLabel: ((direct || indirect)?.label || fileName).replace(/\s*\(indirect\)\s*$/i, ''),
        };
      }
    }
    return null;
  }, [xmlFilesByFolder, selectedFilePath, turrets]);

  // Ensure activeMode is valid for the selected XML file
  React.useEffect(() => {
    if (!currentFileEntry) return;
    if (activeMode === 'direct' && !currentFileEntry.direct && currentFileEntry.indirect) {
      setActiveMode('indirect');
    } else if (activeMode === 'indirect' && !currentFileEntry.indirect && currentFileEntry.direct) {
      setActiveMode('direct');
    }
  }, [currentFileEntry, activeMode]);

  const activeTurret = React.useMemo(() => {
    if (!currentFileEntry) return null;
    if (activeMode === 'indirect' && currentFileEntry.indirect) return currentFileEntry.indirect;
    if (activeMode === 'direct' && currentFileEntry.direct) return currentFileEntry.direct;
    return currentFileEntry.direct || currentFileEntry.indirect || null;
  }, [currentFileEntry, activeMode]);

  // Reset burst simulation when switching turret
  React.useEffect(() => {
    const sim = simRef.current;
    sim.active = false;
    sim.shotsRemaining = 0;
    sim.barrelRecoilTicks = [];
    sim.barrelRecoilDistances = [];
    sim.barrelFiringTicks = [];
    sim.mfTicksRemaining = 0;
    sim.spinFrame = 0;
    setBurstUiState({ active: false, shotsFired: 0, totalShots: 0 });
  }, [activeTurret]);

  // Helper to load & cache an image by URL
  const getImage = React.useCallback((url, isMuzzleFlash = false) => {
    if (!url) return null;
    const cache = imageCacheRef.current;
    if (cache.has(url)) {
      return cache.get(url);
    }
    const record = { status: 'loading', img: null, width: 0, height: 0 };
    cache.set(url, record);
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      record.status = 'loaded';
      record.img = isMuzzleFlash ? processMuzzleFlashImage(img) : img;
      record.width = img.naturalWidth;
      record.height = img.naturalHeight;
      setImageTick((n) => n + 1);
    };
    img.onerror = () => {
      record.status = 'error';
      setImageTick((n) => n + 1);
    };
    img.src = url;
    return record;
  }, []);

  // Preload textures for the active turret (including all rotary spin frames if spinning.enabled)
  const textureStatus = React.useMemo(() => {
    if (!activeTurret) return {};
    const baseUrl = activeTurret.textures?.building
      ? `/api/texture?texPath=${encodeURIComponent(activeTurret.textures.building)}`
      : null;
    const topUrl = activeTurret.textures?.weapon
      ? `/api/texture?texPath=${encodeURIComponent(activeTurret.textures.weapon)}`
      : null;
    const barrelTexPath = activeTurret.barrel?.enabled && activeTurret.barrel?.graphic?.texPath
      ? activeTurret.barrel.graphic.texPath
      : null;
    const barrelUrl = barrelTexPath
      ? `/api/texture?texPath=${encodeURIComponent(barrelTexPath)}&frame=0`
      : null;
    const spinFrameCount = activeTurret.barrel?.spinning?.enabled
      ? Math.max(1, parseInt(activeTurret.barrel.spinning.frameCount ?? 4, 10) || 4)
      : 1;
    const barrelFrames = [];
    if (barrelTexPath) {
      for (let f = 0; f < spinFrameCount; f++) {
        barrelFrames.push(getImage(`/api/texture?texPath=${encodeURIComponent(barrelTexPath)}&frame=${f}`));
      }
    }
    const underBarrelUrl = activeTurret.barrel?.enabled && activeTurret.barrel?.underGraphic?.texPath
      ? `/api/texture?texPath=${encodeURIComponent(activeTurret.barrel.underGraphic.texPath)}`
      : null;
    const mfUrl = activeTurret.muzzleFlash?.enabled && activeTurret.muzzleFlash?.def
      ? `/api/muzzle-flash-texture/${encodeURIComponent(activeTurret.muzzleFlash.def)}`
      : null;

    return {
      base: getImage(baseUrl),
      top: getImage(topUrl),
      barrel: getImage(barrelUrl),
      barrelFrames,
      underBarrel: getImage(underBarrelUrl),
      muzzleFlash: getImage(mfUrl, true),
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTurret, getImage, imageTick]);

  // Derived dimensions & positions for activeTurret
  const sceneMeta = React.useMemo(() => {
    if (!activeTurret) return null;
    const footprintSize = parseVec2(activeTurret.size, [3, 3]);
    const baseDrawSize = parseVec2(activeTurret.graphics?.drawSize, footprintSize);
    const topDrawSizeVal = Number(activeTurret.stats?.topDrawSize);
    const topDrawSize = Number.isFinite(topDrawSizeVal) && topDrawSizeVal > 0
      ? [topDrawSizeVal, topDrawSizeVal]
      : baseDrawSize;
    const barrelDrawSizeVal = Number(activeTurret.barrel?.drawSize);
    const barrelDrawSize = Number.isFinite(barrelDrawSizeVal) && barrelDrawSizeVal > 0
      ? [barrelDrawSizeVal, barrelDrawSizeVal]
      : topDrawSize;
    const barrelOffset = parseVec3(activeTurret.barrel?.offset, [0, 0, 0]);
    const barrelAmount = Math.max(1, parseInt(activeTurret.barrel?.barrelAmount ?? 1, 10) || 1);
    const barrelSpacing = Number(activeTurret.barrel?.barrelSpacing ?? 1) || 1;
    const drawOnTop = Boolean(activeTurret.barrel?.drawOnTop);

    // Recoil parameters (defaults match C# RecoilAnimation class)
    const recoilCfg = activeTurret.barrel?.recoil || {};
    const recoilEnabled = Boolean(activeTurret.barrel?.enabled && recoilCfg.enabled !== false);
    const recoilMaxDist = Number(recoilCfg.maxDistance ?? 0.2) || 0;
    const recoilDuration = Math.max(1, parseInt(recoilCfg.recoilDuration ?? 5, 10) || 5);
    const returnDuration = Math.max(1, parseInt(recoilCfg.returnDuration ?? 10, 10) || 10);
    const useRecoilCurve = recoilCfg.useRecoilCurve !== undefined ? Boolean(recoilCfg.useRecoilCurve) : true;
    const useReturnCurve = Boolean(recoilCfg.useReturnCurve);

    // Firing animation parameters (defaults match C# FiringAnimation class)
    const firingCfg = activeTurret.barrel?.firing || {};
    const firingEnabled = Boolean(activeTurret.barrel?.enabled && firingCfg.enabled !== false);
    const firingDuration = Math.max(1, parseInt(firingCfg.durationTicks ?? 5, 10) || 5);
    const drawFlash = Boolean(firingCfg.drawFlash);
    const flashColor = parseColorRGBA(firingCfg.flashColor);
    const flashSize = Number(firingCfg.flashSize ?? 1) || 1;
    const flashBrightness = Number(firingCfg.flashBrightness ?? 2) || 2;
    const flashOffset = Number(firingCfg.flashOffset ?? 0) || 0;
    const projectileSpawnOffset = Number(firingCfg.projectileSpawnOffset ?? 0) || 0;

    // Burst count & cadence from weapon verb / selectable bursts / rotary RPM
    let burstCount = parseInt(activeTurret.ballistics?.burstShotCount ?? 0, 10) || 0;
    if (!burstCount && activeTurret.barrel?.selectableBursts?.counts) {
      const firstCount = parseInt(String(activeTurret.barrel.selectableBursts.counts).split(',')[0], 10);
      if (firstCount > 0) burstCount = firstCount;
    }
    if (!burstCount) burstCount = 1;

    let ticksBetween = parseInt(activeTurret.ballistics?.ticksBetween ?? 0, 10) || 0;
    if (activeTurret.barrel?.spinning?.enabled) {
      let rpm = Number(activeTurret.barrel.spinning.maxRPM) || 0;
      if (!rpm && activeTurret.barrel.maxRPMs) {
        const parts = String(activeTurret.barrel.maxRPMs).split(',').map((s) => Number(s.trim())).filter(Boolean);
        if (parts.length) rpm = parts[parts.length - 1];
      }
      if (rpm > 0) ticksBetween = Math.max(1, Math.round(3600 / rpm));
    }
    if (!ticksBetween || ticksBetween <= 0) ticksBetween = 12;

    return {
      footprintSize,
      baseDrawSize,
      topDrawSize,
      barrelDrawSize,
      barrelOffset,
      barrelAmount,
      barrelSpacing,
      drawOnTop,
      recoil: {
        enabled: recoilEnabled,
        maxDistance: recoilMaxDist,
        recoilDuration,
        returnDuration,
        totalDuration: recoilDuration + returnDuration,
        useRecoilCurve,
        useReturnCurve,
      },
      firing: {
        enabled: firingEnabled,
        durationTicks: firingDuration,
        drawFlash,
        flashColor,
        flashSize,
        flashBrightness,
        flashOffset,
        projectileSpawnOffset,
      },
      burstCount,
      ticksBetween,
    };
  }, [activeTurret]);

  // Trigger a burst simulation
  const startBurstSimulation = React.useCallback(() => {
    if (!activeTurret || !sceneMeta) return;
    const sim = simRef.current;
    const nBarrels = sceneMeta.barrelAmount;

    sim.active = true;
    sim.totalShots = sceneMeta.burstCount;
    sim.shotsRemaining = sceneMeta.burstCount;
    sim.shotsFired = 0;
    sim.ticksBetweenShots = sceneMeta.ticksBetween;
    sim.ticksToNextShot = 0; // Fire shot #1 immediately on first tick
    sim.nextSequentialBarrel = 0;
    sim.mfAltIndex = 0;
    sim.barrelRecoilTicks = new Array(nBarrels).fill(0);
    sim.barrelRecoilDistances = new Array(nBarrels).fill(0);
    sim.barrelFiringTicks = new Array(nBarrels).fill(0);
    sim.mfTicksRemaining = 0;
    sim.mfDuration = 6;
    sim.lastTimestamp = performance.now();
    sim.tickAccumulator = 1; // Trigger immediate tick

    setBurstUiState({ active: true, shotsFired: 0, totalShots: sceneMeta.burstCount });
    setImageTick((n) => n + 1);
  }, [activeTurret, sceneMeta]);

  const stopBurstSimulation = React.useCallback(() => {
    const sim = simRef.current;
    sim.active = false;
    sim.shotsRemaining = 0;
    sim.barrelRecoilTicks = [];
    sim.barrelRecoilDistances = [];
    sim.barrelFiringTicks = [];
    sim.mfTicksRemaining = 0;
    sim.spinFrame = 0;
    setBurstUiState({ active: false, shotsFired: 0, totalShots: 0 });
    setImageTick((n) => n + 1);
  }, []);

  // Animation loop running at 60 RimWorld ticks per second while simRef.current.active is true
  React.useEffect(() => {
    if (!burstUiState.active || !activeTurret || !sceneMeta) return undefined;

    let rafId = null;
    const stepOneTick = () => {
      const sim = simRef.current;
      const nBarrels = sceneMeta.barrelAmount;
      const sequential = Boolean(activeTurret.barrel?.sequentialFiring) && nBarrels > 1;

      // 1. Fire next shot if due
      if (sim.shotsRemaining > 0) {
        if (sim.ticksToNextShot <= 0) {
          sim.shotsRemaining--;
          sim.shotsFired++;
          sim.ticksToNextShot = sim.ticksBetweenShots;

          if (sequential) {
            const bIdx = sim.nextSequentialBarrel % nBarrels;
            if (sceneMeta.recoil.enabled) {
              sim.barrelRecoilTicks[bIdx] = sceneMeta.recoil.totalDuration;
            }
            if (sceneMeta.firing.enabled) {
              sim.barrelFiringTicks[bIdx] = sceneMeta.firing.durationTicks;
            }
            sim.nextSequentialBarrel = (bIdx + 1) % nBarrels;
          } else {
            for (let i = 0; i < nBarrels; i++) {
              if (sceneMeta.recoil.enabled) {
                sim.barrelRecoilTicks[i] = sceneMeta.recoil.totalDuration;
              }
              if (sceneMeta.firing.enabled) {
                sim.barrelFiringTicks[i] = sceneMeta.firing.durationTicks;
              }
            }
          }

          // Trigger Muzzle Flash mod sprite animation
          if (activeTurret.muzzleFlash?.enabled) {
            const [, splitsY] = activeTurret.muzzleFlash.splits || [5, 5];
            sim.mfTicksRemaining = sim.mfDuration;
            sim.mfRow = Math.floor(Math.random() * splitsY);
            const mfOffsetsCount = activeTurret.muzzleFlash.offsets?.length || 1;
            if (activeTurret.muzzleFlash.isAlternately && mfOffsetsCount > 1) {
              sim.mfActiveIndex = sim.mfAltIndex % mfOffsetsCount;
              sim.mfAltIndex++;
            } else {
              sim.mfActiveIndex = -1; // all offsets
            }
          }
        } else {
          sim.ticksToNextShot--;
        }
      }

      // 2. Advance per-barrel recoil ticks and evaluate recoil curve
      let anyAnimating = sim.shotsRemaining > 0;
      for (let i = 0; i < nBarrels; i++) {
        if (sim.barrelRecoilTicks[i] > 0) {
          anyAnimating = true;
          const rawProgress = 1 - sim.barrelRecoilTicks[i] / sceneMeta.recoil.totalDuration;
          const mult = evaluateRecoilCurve(
            rawProgress,
            sceneMeta.recoil.recoilDuration,
            sceneMeta.recoil.returnDuration,
            sceneMeta.recoil.useRecoilCurve,
            sceneMeta.recoil.useReturnCurve,
          );
          sim.barrelRecoilDistances[i] = sceneMeta.recoil.maxDistance * mult;
          sim.barrelRecoilTicks[i]--;
        } else {
          sim.barrelRecoilDistances[i] = 0;
        }

        if (sim.barrelFiringTicks[i] > 0) {
          anyAnimating = true;
          sim.barrelFiringTicks[i]--;
        }
      }

      // 3. Advance Muzzle Flash mod sprite ticks
      if (sim.mfTicksRemaining > 0) {
        anyAnimating = true;
        sim.mfTicksRemaining--;
      }

      // 4. Advance rotary spin frame if enabled
      if (activeTurret.barrel?.spinning?.enabled && anyAnimating) {
        const frameCount = Math.max(1, parseInt(activeTurret.barrel.spinning.frameCount ?? 4, 10) || 4);
        sim.spinFrame = (sim.spinFrame + 0.5) % frameCount;
      }

      if (!anyAnimating) {
        sim.active = false;
        sim.spinFrame = 0;
        setBurstUiState({ active: false, shotsFired: sim.shotsFired, totalShots: sim.totalShots });
      } else {
        setBurstUiState((prev) => (prev.shotsFired !== sim.shotsFired
          ? { ...prev, shotsFired: sim.shotsFired }
          : prev));
      }
    };

    const loop = (now) => {
      const sim = simRef.current;
      if (!sim.active) return;
      const dtMs = now - sim.lastTimestamp;
      sim.lastTimestamp = now;
      // 60 ticks per second = 16.6667 ms per tick
      sim.tickAccumulator += dtMs / (1000 / 60);
      const ticksToRun = Math.min(10, Math.floor(sim.tickAccumulator));
      if (ticksToRun > 0) {
        sim.tickAccumulator -= ticksToRun;
        for (let t = 0; t < ticksToRun; t++) {
          if (sim.active) stepOneTick();
        }
        setImageTick((n) => n + 1);
      }
      if (sim.active) {
        rafId = requestAnimationFrame(loop);
      }
    };

    rafId = requestAnimationFrame(loop);
    return () => {
      if (rafId) cancelAnimationFrame(rafId);
    };
  }, [burstUiState.active, activeTurret, sceneMeta]);

  // Draw Canvas whenever activeTurret, rotation, layers, markerShape, zoom, pan, or simulation tick changes
  React.useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !activeTurret || !sceneMeta) return;

    const dpr = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    const width = Math.max(300, Math.floor(rect.width));
    const height = Math.max(300, Math.floor(rect.height));
    if (canvas.width !== width * dpr || canvas.height !== height * dpr) {
      canvas.width = width * dpr;
      canvas.height = height * dpr;
    }

    const ctx = canvas.getContext('2d');
    ctx.save();
    ctx.scale(dpr, dpr);

    // Background fill
    ctx.fillStyle = darkBg ? '#141923' : '#f1f5f9';
    ctx.fillRect(0, 0, width, height);

    const cx = width / 2 + pan.x;
    const cy = height / 2 + pan.y;
    const tilePx = zoom;

    // World (X = East/Right, Z = North/Up) -> Canvas pixel (x = cx + X*tilePx, y = cy - Z*tilePx)
    const toScreen = (wx, wz) => ({
      x: cx + wx * tilePx,
      y: cy - wz * tilePx,
    });

    const {
      footprintSize,
      baseDrawSize,
      topDrawSize,
      barrelDrawSize,
      barrelOffset,
      barrelAmount,
      barrelSpacing,
      drawOnTop,
    } = sceneMeta;

    const sim = simRef.current;

    // 1. Tile Grid
    if (layers.grid) {
      const [fw, fh] = footprintSize;
      const offsetX = (Math.round(fw) % 2 === 1) ? 0.5 : 0;
      const offsetZ = (Math.round(fh) % 2 === 1) ? 0.5 : 0;

      const maxTilesX = Math.ceil(width / tilePx) + 4;
      const maxTilesZ = Math.ceil(height / tilePx) + 4;

      ctx.lineWidth = 1;
      ctx.strokeStyle = darkBg ? 'rgba(148, 163, 184, 0.16)' : 'rgba(100, 116, 139, 0.2)';
      ctx.beginPath();
      for (let ix = -maxTilesX; ix <= maxTilesX; ix++) {
        const wx = ix + offsetX;
        const sx = Math.round(cx + wx * tilePx) + 0.5;
        if (sx >= 0 && sx <= width) {
          ctx.moveTo(sx, 0);
          ctx.lineTo(sx, height);
        }
      }
      for (let iz = -maxTilesZ; iz <= maxTilesZ; iz++) {
        const wz = iz + offsetZ;
        const sy = Math.round(cy - wz * tilePx) + 0.5;
        if (sy >= 0 && sy <= height) {
          ctx.moveTo(0, sy);
          ctx.lineTo(width, sy);
        }
      }
      ctx.stroke();

      // Axis hairlines through turret center (0,0)
      ctx.strokeStyle = darkBg ? 'rgba(56, 189, 248, 0.25)' : 'rgba(2, 132, 199, 0.3)';
      ctx.setLineDash([4, 4]);
      ctx.beginPath();
      ctx.moveTo(cx, 0);
      ctx.lineTo(cx, height);
      ctx.moveTo(0, cy);
      ctx.lineTo(width, cy);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // 2. Building Placement Footprint (<size>)
    if (layers.footprint) {
      const [fw, fh] = footprintSize;
      const x0 = cx - (fw / 2) * tilePx;
      const y0 = cy - (fh / 2) * tilePx;
      const wPx = fw * tilePx;
      const hPx = fh * tilePx;

      ctx.fillStyle = darkBg ? 'rgba(56, 189, 248, 0.08)' : 'rgba(2, 132, 199, 0.08)';
      ctx.fillRect(x0, y0, wPx, hPx);

      ctx.lineWidth = 2;
      ctx.strokeStyle = darkBg ? 'rgba(56, 189, 248, 0.65)' : 'rgba(2, 132, 199, 0.75)';
      ctx.strokeRect(x0, y0, wPx, hPx);

      ctx.fillStyle = darkBg ? '#7dd3fc' : '#0369a1';
      ctx.font = '600 11px Inter, system-ui, sans-serif';
      ctx.fillText(`Footprint <size> ${fw}×${fh}`, x0 + 6, y0 + hPx - 6);
    }

    // Helper to draw a world-scaled texture at world (wx, wz) with canvas size [dw, dh] in tiles and rotationDeg
    const drawTextureLayer = (imgRec, wx, wz, [dw, dh], rotationDeg, boundColor = null, boundLabel = '') => {
      const pos = toScreen(wx, wz);
      const wPx = dw * tilePx;
      const hPx = dh * tilePx;
      const rad = (rotationDeg * Math.PI) / 180;

      ctx.save();
      ctx.translate(pos.x, pos.y);
      ctx.rotate(rad);

      if (imgRec?.status === 'loaded' && imgRec.img) {
        ctx.drawImage(imgRec.img, -wPx / 2, -hPx / 2, wPx, hPx);
      }

      if (layers.canvasBounds && boundColor) {
        ctx.lineWidth = 1.25;
        ctx.strokeStyle = boundColor;
        ctx.setLineDash([5, 4]);
        ctx.strokeRect(-wPx / 2, -hPx / 2, wPx, hPx);
        ctx.setLineDash([]);
        if (boundLabel) {
          ctx.fillStyle = boundColor;
          ctx.font = '500 10px ui-monospace, monospace';
          ctx.fillText(boundLabel, -wPx / 2 + 5, -hPx / 2 + 13);
        }
      }
      ctx.restore();
    };

    const headSpriteRotation = rotation - 90; // Textures face East (90°) by default
    const angleRad = (rotation * Math.PI) / 180;
    const forwardDir = { x: Math.sin(angleRad), z: Math.cos(angleRad) };
    const perpDir = { x: Math.cos(angleRad), z: -Math.sin(angleRad) }; // 90° clockwise from forward
    const rotatedBarrelOffset = rotateVecXZ(barrelOffset[0], barrelOffset[2], rotation);

    // 3. Base Texture (Fixed at 0° North)
    if (layers.base) {
      drawTextureLayer(
        textureStatus.base,
        0,
        0,
        baseDrawSize,
        0,
        '#4ade80',
        `Base drawSize (${baseDrawSize[0]}×${baseDrawSize[1]})`,
      );
    }

    // 4. Under-Barrel Texture (Above Base, Below Barrel & Top; rotates with turret top, no recoil)
    if (layers.underBarrel && activeTurret.barrel?.enabled && activeTurret.barrel?.underGraphic?.texPath) {
      drawTextureLayer(
        textureStatus.underBarrel,
        rotatedBarrelOffset.x,
        rotatedBarrelOffset.z,
        barrelDrawSize,
        headSpriteRotation,
        '#a78bfa',
        `UnderBarrel (${barrelDrawSize[0]}×${barrelDrawSize[1]})`,
      );
    }

    // Helper for drawing all barrels (including live recoil displacement & rotary frame selection)
    const drawBarrels = () => {
      if (!layers.barrel || !activeTurret.barrel?.enabled || !activeTurret.barrel?.graphic?.texPath) return;
      const centerIndex = (barrelAmount - 1) / 2;

      let activeBarrelImg = textureStatus.barrel;
      if (textureStatus.barrelFrames?.length > 1) {
        const frameIdx = Math.floor(sim.spinFrame) % textureStatus.barrelFrames.length;
        activeBarrelImg = textureStatus.barrelFrames[frameIdx] || textureStatus.barrel;
      }

      for (let i = 0; i < barrelAmount; i++) {
        const lateral = (i - centerIndex) * barrelSpacing;
        const recoilDist = (sim.active && sim.barrelRecoilDistances[i]) ? sim.barrelRecoilDistances[i] : 0;
        // Recoil moves backward (-forwardDir * recoilDist)
        const bx = rotatedBarrelOffset.x + perpDir.x * lateral - forwardDir.x * recoilDist;
        const bz = rotatedBarrelOffset.z + perpDir.z * lateral - forwardDir.z * recoilDist;
        drawTextureLayer(
          activeBarrelImg,
          bx,
          bz,
          barrelDrawSize,
          headSpriteRotation,
          '#facc15',
          i === 0 ? `Barrel #${i + 1} (${barrelDrawSize[0]}×${barrelDrawSize[1]})` : `Barrel #${i + 1}`,
        );
      }
    };

    // 5a. Barrel Texture when drawOnTop === false (sandwiched between Under-Barrel and Top)
    if (!drawOnTop) {
      drawBarrels();
    }

    // 6. Turret Top Texture
    if (layers.top) {
      drawTextureLayer(
        textureStatus.top,
        0,
        0,
        topDrawSize,
        headSpriteRotation,
        '#fb923c',
        `Top turretTopDrawSize (${topDrawSize[0]}×${topDrawSize[1]})`,
      );
    }

    // 5b. Barrel Texture when drawOnTop === true (drawn on top of Turret Top)
    if (drawOnTop) {
      drawBarrels();
    }

    // 6b. CompTurretBarrel.DrawFiringFlashForBarrel (3-layer additive glow flash from <firingAnimation>)
    if (layers.firingFlash && sceneMeta.firing.enabled && sceneMeta.firing.drawFlash) {
      const centerIndex = (barrelAmount - 1) / 2;
      const flashDist = barrelDrawSize[0] * 0.5 + sceneMeta.firing.flashOffset;
      const baseCol = sceneMeta.firing.flashColor;
      const brightness = sceneMeta.firing.flashBrightness;

      for (let i = 0; i < barrelAmount; i++) {
        // When burst simulation is running, animate intensity per barrel; when idle, preview at 50% if firingFlash is ticked and burst is running, or show during burst
        const ticksRem = sim.active ? (sim.barrelFiringTicks[i] || 0) : 0;
        if (sim.active && ticksRem <= 0) continue;

        const intensity = sim.active
          ? evaluateFlashIntensity(1 - ticksRem / sceneMeta.firing.durationTicks)
          : 0.75; // Static preview when not running burst

        if (intensity <= 0.01) continue;

        const lateral = (i - centerIndex) * barrelSpacing;
        const fx = rotatedBarrelOffset.x + forwardDir.x * flashDist + perpDir.x * lateral;
        const fz = rotatedBarrelOffset.z + forwardDir.z * flashDist + perpDir.z * lateral;
        const pos = toScreen(fx, fz);

        // ShotFlash in RimWorld is a soft circular radial glow on MeshPool.plane10
        // flashSize in XML is scaled on plane10 (note: in RimWorld plane10 is 1x1, ShotFlash texture has soft transparent padding)
        // We draw the 3 concentric radial gradient layers (3x outer, 1.8x mid, 1x core)
        const coreRadiusPx = Math.max(4, (sceneMeta.firing.flashSize * intensity * 0.12) * tilePx);
        const r255 = Math.min(255, Math.round(baseCol.r * brightness * 255));
        const g255 = Math.min(255, Math.round(baseCol.g * brightness * 255));
        const b255 = Math.min(255, Math.round(baseCol.b * brightness * 255));
        const alpha = Math.min(1, baseCol.a * intensity * (sim.active ? 0.9 : 0.45));

        ctx.save();
        ctx.globalCompositeOperation = 'lighter';
        const grad = ctx.createRadialGradient(pos.x, pos.y, 0, pos.x, pos.y, coreRadiusPx * 2.5);
        grad.addColorStop(0, `rgba(255, 250, 220, ${alpha})`);
        grad.addColorStop(0.25, `rgba(${r255}, ${g255}, ${b255}, ${alpha * 0.75})`);
        grad.addColorStop(0.6, `rgba(${r255}, ${g255}, ${b255}, ${alpha * 0.3})`);
        grad.addColorStop(1, `rgba(${r255}, ${g255}, ${b255}, 0)`);
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(pos.x, pos.y, coreRadiusPx * 2.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }
    }

    // 7. Muzzle Flash Mod Texture (from Patches/Muzzle Flash.xml)
    if (layers.muzzleFlash && activeTurret.muzzleFlash?.enabled) {
      // If burst simulation is active, only draw while mfTicksRemaining > 0
      const shouldDrawMf = !sim.active || sim.mfTicksRemaining > 0;
      if (shouldDrawMf) {
        const mf = activeTurret.muzzleFlash;
        const mfSize = parseVec2(mf.drawSize, [1.5, 1.5]);
        const [splitsX, splitsY] = mf.splits || [5, 5];
        const [multX] = mf.drawOffsetMultiplier || [0.37, 0];
        const normAngle = ((rotation % 360) + 360) % 360;
        const flipped = normAngle > 200 && normAngle < 340 ? -1 : 1;
        // Vector3.Cross(direction, Vector3.up).normalized in Unity left-handed is (-cos(θ), 0, sin(θ))
        const crossDir = { x: -Math.cos(angleRad), z: Math.sin(angleRad) };

        const mfRec = textureStatus.muzzleFlash;
        const offsets = mf.offsets?.length ? mf.offsets : ['(0,0)'];

        // Note: In MuzzleFlashOne5x5 / Two4x5 / Three4x5 / Charged4x5,
        // Column 0 is empty padding! Column 1 is the brightest initial flash frame, and Columns 2..3 are fading frames.
        let colIndex = 1;
        let rowIndex = 0;
        if (sim.active && sim.mfTicksRemaining > 0) {
          const progress = 1 - sim.mfTicksRemaining / sim.mfDuration; // 0..1
          colIndex = Math.min(splitsX - 2, 1 + Math.floor(progress * 3)); // animate cols 1 -> 2 -> 3
          rowIndex = sim.mfRow % splitsY;
        }

        offsets.forEach((rawOff, idx) => {
          if (sim.active && sim.mfActiveIndex !== -1 && sim.mfActiveIndex !== idx) return;

          const [ox, oy] = parseVec2(rawOff, [0, 0]);
          const forwardDist = ox + mfSize[0] * multX;
          const wx = forwardDir.x * forwardDist + crossDir.x * (oy * flipped);
          const wz = forwardDir.z * forwardDist + crossDir.z * (oy * flipped);

          const pos = toScreen(wx, wz);
          const wPx = mfSize[0] * tilePx;
          const hPx = mfSize[1] * tilePx;

          ctx.save();
          ctx.translate(pos.x, pos.y);
          ctx.rotate((headSpriteRotation * Math.PI) / 180);

          if (mfRec?.status === 'loaded' && mfRec.img) {
            const fw = mfRec.width / splitsX;
            const fh = mfRec.height / splitsY;
            const sx = colIndex * fw;
            const sy = rowIndex * fh;
            ctx.drawImage(mfRec.img, sx, sy, fw, fh, -wPx / 2, -hPx / 2, wPx, hPx);
          }

          if (layers.canvasBounds) {
            ctx.lineWidth = 1;
            ctx.strokeStyle = '#fde047';
            ctx.setLineDash([3, 3]);
            ctx.strokeRect(-wPx / 2, -hPx / 2, wPx, hPx);
            ctx.setLineDash([]);
          }
          ctx.restore();

          // Small anchor dot at exact offset origin (before drawOffsetMultiplier shift)
          if (layers.canvasBounds) {
            const anchorPos = toScreen(
              forwardDir.x * ox + crossDir.x * (oy * flipped),
              forwardDir.z * ox + crossDir.z * (oy * flipped),
            );
            ctx.fillStyle = '#fde047';
            ctx.beginPath();
            ctx.arc(anchorPos.x, anchorPos.y, 3, 0, Math.PI * 2);
            ctx.fill();
            if (idx === 0) {
              ctx.font = '500 10px ui-monospace, monospace';
              ctx.fillText(`MF (${ox}, ${oy})`, anchorPos.x + 5, anchorPos.y - 4);
            }
          }
        });
      }
    }

    // Helper to draw a distinct X or O (circle) marker at world (wx, wz)
    const drawMarker = (wx, wz, color, label = '', sizePx = 7) => {
      const p = toScreen(wx, wz);
      ctx.save();
      ctx.lineCap = 'round';

      if (markerShape === 'O') {
        // Outer dark halo circle
        ctx.lineWidth = 4.2;
        ctx.strokeStyle = 'rgba(15, 23, 42, 0.88)';
        ctx.beginPath();
        ctx.arc(p.x, p.y, sizePx, 0, Math.PI * 2);
        ctx.stroke();

        // Inner colored circle + center dot
        ctx.lineWidth = 2.2;
        ctx.strokeStyle = color;
        ctx.beginPath();
        ctx.arc(p.x, p.y, sizePx, 0, Math.PI * 2);
        ctx.stroke();

        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.arc(p.x, p.y, 2, 0, Math.PI * 2);
        ctx.fill();
      } else {
        // Outer dark halo X
        ctx.lineWidth = 4.2;
        ctx.strokeStyle = 'rgba(15, 23, 42, 0.88)';
        ctx.beginPath();
        ctx.moveTo(p.x - sizePx, p.y - sizePx);
        ctx.lineTo(p.x + sizePx, p.y + sizePx);
        ctx.moveTo(p.x + sizePx, p.y - sizePx);
        ctx.lineTo(p.x - sizePx, p.y + sizePx);
        ctx.stroke();

        // Inner colored X
        ctx.lineWidth = 2.2;
        ctx.strokeStyle = color;
        ctx.beginPath();
        ctx.moveTo(p.x - sizePx, p.y - sizePx);
        ctx.lineTo(p.x + sizePx, p.y + sizePx);
        ctx.moveTo(p.x + sizePx, p.y - sizePx);
        ctx.lineTo(p.x - sizePx, p.y + sizePx);
        ctx.stroke();
      }

      if (label) {
        ctx.font = '600 10.5px Inter, system-ui, sans-serif';
        const textW = ctx.measureText(label).width;
        ctx.fillStyle = 'rgba(15, 23, 42, 0.8)';
        ctx.fillRect(p.x + sizePx + 4, p.y - 8, textW + 6, 15);
        ctx.fillStyle = color;
        ctx.fillText(label, p.x + sizePx + 7, p.y + 3);
      }
      ctx.restore();
    };

    // 8. interactionCellOffset Marker (Fixed relative to building North orientation + tile outline)
    // In RimWorld ThingUtility.InteractionCellWhenAt:
    // center Cell = Position. For even sizes, DrawPos is offset by (-0.5, 0, -0.5) from Position cell center!
    // Specially: DrawPos = Position.ToVector3Shifted() + (0.5*(size.x-1), 0, 0.5*(size.z-1))?
    // Wait: GenThing.TrueCenter(IntVec3 loc, Rot4 rot, IntVec2 thingSize, float altitude):
    // TrueCenter = loc.ToVector3ShiftedWithAltitude(alt) + new Vector3((size.x-1)*0.5f, 0, (size.z-1)*0.5f) when size is even?
    // No! In RimWorld, GenAdj.OccupiedRect(center, rot, size):
    // minX = center.x - (size.x - 1) / 2 (integer division!).
    // So for odd size (e.g. 3 or 5), center cell IS the exact geometric center (0, 0).
    // For even size (e.g. 2, 4, 6), integer division (size.x - 1) / 2 = (4-1)/2 = 1, so cells are [center-1 .. center+2],
    // meaning geometric center DrawPos is at (center.x + 0.5, center.z + 0.5), so `center` cell is at (-0.5, -0.5) relative to DrawPos!
    if (layers.interactionCell && activeTurret.hasInteractionCell && activeTurret.interactionCellOffset) {
      const icOff = parseVec3(activeTurret.interactionCellOffset, [0, 0, -1]);
      const [fw, fh] = footprintSize;
      const centerCellOffsetX = (Math.round(fw) % 2 === 0) ? -0.5 : 0;
      const centerCellOffsetZ = (Math.round(fh) % 2 === 0) ? -0.5 : 0;
      const icWorldX = centerCellOffsetX + icOff[0];
      const icWorldZ = centerCellOffsetZ + icOff[2];

      // Draw dashed 1x1 cell square for the interaction spot
      const cellCenter = toScreen(icWorldX, icWorldZ);
      ctx.save();
      ctx.lineWidth = 1.5;
      ctx.strokeStyle = 'rgba(74, 222, 128, 0.75)';
      ctx.fillStyle = 'rgba(74, 222, 128, 0.12)';
      ctx.setLineDash([4, 3]);
      ctx.fillRect(cellCenter.x - tilePx / 2, cellCenter.y - tilePx / 2, tilePx, tilePx);
      ctx.strokeRect(cellCenter.x - tilePx / 2, cellCenter.y - tilePx / 2, tilePx, tilePx);
      ctx.setLineDash([]);
      ctx.restore();

      drawMarker(
        icWorldX,
        icWorldZ,
        '#4ade80',
        `InteractionCell (${icOff[0]},${icOff[2]})`,
        7,
      );
    }

    // 8b. Open-top seat & gun shield (CompProperties_EnclosedTurret.seatOffset / shield*), rotating with the top.
    // Seat offset is relative to the turret DrawPos (world origin here). Canvas angle = compass angle - 90°.
    const openTop = openTopOverlay(activeTurret, rotation);
    if (openTop) {
      const seatScreen = toScreen(openTop.seat.x, openTop.seat.z);
      if (layers.shield && openTop.shield) {
        ctx.save();
        ctx.fillStyle = 'rgba(245, 158, 11, 0.2)';
        ctx.strokeStyle = 'rgba(245, 158, 11, 0.85)';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        if (openTop.shield.full) {
          ctx.arc(seatScreen.x, seatScreen.y, tilePx, 0, Math.PI * 2);
        } else {
          const a1 = ((openTop.shield.fromDeg - 90) * Math.PI) / 180;
          const a2 = ((openTop.shield.toDeg - 90) * Math.PI) / 180;
          ctx.moveTo(seatScreen.x, seatScreen.y);
          ctx.arc(seatScreen.x, seatScreen.y, tilePx, a1, a2, false);
          ctx.closePath();
        }
        ctx.fill();
        ctx.stroke();
        ctx.fillStyle = '#f59e0b';
        ctx.font = '11px sans-serif';
        ctx.fillText(openTop.label, seatScreen.x + tilePx * 0.6, seatScreen.y + tilePx + 12);
        ctx.restore();
      }
      if (layers.seat) {
        ctx.save();
        ctx.strokeStyle = '#f59e0b';
        ctx.fillStyle = 'rgba(245, 158, 11, 0.25)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(seatScreen.x, seatScreen.y, tilePx * 0.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();
        ctx.fillStyle = '#f59e0b';
        ctx.font = 'bold 11px sans-serif';
        ctx.fillText('Seat', seatScreen.x + tilePx * 0.55, seatScreen.y - tilePx * 0.3);
        ctx.restore();
      }
    }

    // 9. projectileSpawnOffset Marker (Rotates with turret head, per-barrel if multi-barrel)
    // Matches HarmonyPatches.Postfix_Verb_LaunchProjectileCE_IncrementBarrelCount:
    // forwardDir * projectileSpawnOffset + rightDir * ((barrelIndex - (N-1)/2) * barrelSpacing)
    if (layers.projectileSpawn && activeTurret.barrel?.enabled) {
      const projOff = sceneMeta.firing.projectileSpawnOffset;
      const centerIndex = (barrelAmount - 1) / 2;
      for (let b = 0; b < barrelAmount; b++) {
        const lateral = barrelAmount > 1 ? (b - centerIndex) * barrelSpacing : 0;
        const wx = forwardDir.x * projOff + perpDir.x * lateral;
        const wz = forwardDir.z * projOff + perpDir.z * lateral;
        const label = b === 0
          ? `ProjectileSpawn (${projOff})`
          : `Proj #${b + 1}`;
        drawMarker(wx, wz, '#facc15', label, 7);
      }
    }

    // 10. CompProperties_TurretSmoker Markers
    if (activeTurret.smoker?.enabled) {
      const centerIndex = (barrelAmount - 1) / 2;

      // 10a. Shockwave Smoke (Magenta / Purple #e879f9)
      if (layers.smokeShockwave && activeTurret.smoker.shockwave?.enabled) {
        const swOff = parseVec3(activeTurret.smoker.shockwave.offset, [0, 0, 0]);
        const rotSw = rotateVecXZ(swOff[0], swOff[2], rotation);
        const radius = Number(activeTurret.smoker.shockwave.radius) || 0;

        if (radius > 0) {
          const p = toScreen(rotSw.x, rotSw.z);
          ctx.save();
          ctx.lineWidth = 1.5;
          ctx.strokeStyle = 'rgba(232, 121, 249, 0.55)';
          ctx.fillStyle = 'rgba(232, 121, 249, 0.06)';
          ctx.setLineDash([6, 4]);
          ctx.beginPath();
          ctx.arc(p.x, p.y, radius * tilePx, 0, Math.PI * 2);
          ctx.fill();
          ctx.stroke();
          ctx.setLineDash([]);
          ctx.restore();
        }
        drawMarker(
          rotSw.x,
          rotSw.z,
          '#e879f9',
          `Shockwave (${swOff[0]},${swOff[2]})${radius ? ` r=${radius}` : ''}`,
          8,
        );
      }

      // 10b. Heat Smoke Emission Points (Orange #fb923c)
      if (layers.smokeHeat && activeTurret.smoker.heat?.enabled) {
        const heatOff = parseVec3(activeTurret.smoker.heat.offset, [0, 0, 0]);
        const rotHeat = rotateVecXZ(heatOff[0], heatOff[2], rotation);
        const points = Math.max(1, parseInt(activeTurret.smoker.heat.emissionPoints ?? 1, 10) || 1);
        const spacing = Number(activeTurret.smoker.heat.emissionSpacing ?? 0.5) || 0.5;

        for (let b = 0; b < barrelAmount; b++) {
          const lateral = barrelAmount > 1 ? (b - centerIndex) * barrelSpacing : 0;
          for (let p = 0; p < points; p++) {
            const wx = rotHeat.x + forwardDir.x * (spacing * p) + perpDir.x * lateral;
            const wz = rotHeat.z + forwardDir.z * (spacing * p) + perpDir.z * lateral;
            const label = (b === 0 && p === 0)
              ? `Heat #1..${points} (${heatOff[0]},${heatOff[2]})`
              : `H${p + 1}`;
            drawMarker(wx, wz, '#fb923c', label, 6);
          }
        }
      }

      // 10c. Muzzle Smoke (Cyan #22d3ee)
      if (layers.smokeMuzzle && activeTurret.smoker.muzzle?.enabled) {
        const muzOff = parseVec3(activeTurret.smoker.muzzle.offset, [0, 0, 0]);
        const rotMuz = rotateVecXZ(muzOff[0], muzOff[2], rotation);
        const barrelMag = Math.hypot(barrelOffset[0], barrelOffset[1], barrelOffset[2]);
        const estimatedTipLen = activeTurret.barrel?.enabled ? barrelMag * 1.5 : 0;

        for (let b = 0; b < barrelAmount; b++) {
          const lateral = barrelAmount > 1 ? (b - centerIndex) * barrelSpacing : 0;
          const wx = forwardDir.x * estimatedTipLen + rotMuz.x + perpDir.x * lateral;
          const wz = forwardDir.z * estimatedTipLen + rotMuz.z + perpDir.z * lateral;
          const label = b === 0 ? `Muzzle Smoke (${muzOff[0]},${muzOff[2]})` : `Muzzle #${b + 1}`;
          drawMarker(wx, wz, '#22d3ee', label, 7);
        }
      }
    }

    // Center origin crosshair (0, 0)
    ctx.save();
    ctx.fillStyle = '#ffffff';
    ctx.strokeStyle = '#0f172a';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(cx, cy, 3.5, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.restore();

    ctx.restore();
  }, [activeTurret, sceneMeta, textureStatus, rotation, layers, markerShape, zoom, pan, darkBg, imageTick]);

  // Re-render on window resize
  React.useEffect(() => {
    const onResize = () => setImageTick((n) => n + 1);
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  // Attach a non-passive wheel listener to the canvas container so wheel zooming
  // never scrolls the browser page.
  const wrapRef = React.useRef(null);
  React.useEffect(() => {
    const el = wrapRef.current;
    if (!el) return undefined;
    const onWheel = (e) => {
      e.preventDefault();
      e.stopPropagation();
      const delta = e.deltaY < 0 ? 1.12 : 0.89;
      setZoom((z) => Math.min(180, Math.max(12, Math.round(z * delta))));
    };
    el.addEventListener('wheel', onWheel, { passive: false });
    return () => el.removeEventListener('wheel', onWheel);
  }, [activeTurret]);

  const toggleFolder = (folder) => {
    setCollapsedFolders((prev) => ({ ...prev, [folder]: !prev[folder] }));
  };

  const toggleLayer = (key) => {
    setLayers((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const handleMouseDown = (e) => {
    if (e.button !== 0) return;
    dragRef.current = { startX: e.clientX, startY: e.clientY, panX: pan.x, panY: pan.y };
  };

  const handleMouseMove = (e) => {
    if (!dragRef.current) return;
    const dx = e.clientX - dragRef.current.startX;
    const dy = e.clientY - dragRef.current.startY;
    setPan({ x: dragRef.current.panX + dx, y: dragRef.current.panY + dy });
  };

  const handleMouseUp = () => {
    dragRef.current = null;
  };

  const hasDualModes = Boolean(currentFileEntry?.direct && currentFileEntry?.indirect);
  const markerGlyph = markerShape === 'O' ? '◯' : '✕';

  return (
    <div className="tex-inspector-layout">
      {/* LEFT SIDEBAR: XML Files grouped by subfolder */}
      <aside className="panel tex-sidebar">
        <div className="panel-head">
          <h2>ThingDefs_Buildings</h2>
          <span className="spacer" />
          <span className="muted" style={{ fontSize: 11.5 }}>
            {xmlFilesByFolder.reduce((acc, [, list]) => acc + list.length, 0)} XMLs
          </span>
        </div>

        <div style={{ padding: '10px 12px', borderBottom: '1px solid var(--line)' }}>
          <div className="search" style={{ minWidth: 0, padding: '5px 10px' }}>
            <Icon name="search" />
            <input
              type="text"
              placeholder="Filter XML files..."
              value={sidebarSearch}
              onChange={(e) => setSidebarSearch(e.target.value)}
            />
          </div>
        </div>

        <div className="tex-file-tree">
          {xmlFilesByFolder.map(([folder, files]) => {
            const collapsed = Boolean(collapsedFolders[folder]);
            return (
              <div key={folder} className="tex-folder-group">
                <button
                  type="button"
                  className="tex-folder-header"
                  onClick={() => toggleFolder(folder)}
                >
                  <span className="tex-folder-caret">{collapsed ? '▸' : '▾'}</span>
                  <strong>{folder}</strong>
                  <span className="spacer" />
                  <span className="pill">{files.length}</span>
                </button>
                {!collapsed && (
                  <ul className="tex-file-list">
                    {files.map((file) => {
                      const active = file.filePath === selectedFilePath;
                      const dual = Boolean(file.direct && file.indirect);
                      const onlyIndirect = Boolean(!file.direct && file.indirect);
                      return (
                        <li key={file.filePath}>
                          <button
                            type="button"
                            className={`tex-file-item${active ? ' active' : ''}`}
                            onClick={() => {
                              setSelectedFilePath(file.filePath);
                              if (file.direct) setActiveMode('direct');
                              else if (file.indirect) setActiveMode('indirect');
                            }}
                          >
                            <div className="tex-file-main">
                              <span className="tex-file-name">{file.fileName}</span>
                              {dual && <Pill tone="lilac" title="Has Direct & Indirect defs">D+I</Pill>}
                              {onlyIndirect && <Pill tone="peach" title="Indirect mode only">Ind</Pill>}
                            </div>
                            <div className="tex-file-sub">{file.displayLabel}</div>
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </div>
            );
          })}
        </div>
      </aside>

      {/* RIGHT WORKSPACE */}
      <section className="panel tex-workspace">
        {activeTurret && currentFileEntry && sceneMeta ? (
          <>
            {/* Top header with Direct / Indirect Mode Tabs */}
            <div className="panel-head tex-workspace-head">
              <div>
                <div className="row" style={{ gap: 8 }}>
                  <h2 style={{ fontSize: 15 }}>{activeTurret.label}</h2>
                  <span className="mono muted">{activeTurret.defName}</span>
                  <Pill tone="blue">{currentFileEntry.folder}</Pill>
                </div>
                <div className="mono muted" style={{ fontSize: 11.5 }}>
                  {currentFileEntry.filePath} · Weapon: {activeTurret.weaponDefName}
                </div>
              </div>

              <span className="spacer" />

              {/* Burst Animation Simulation Controls */}
              <div className="row" style={{ gap: 6 }}>
                {burstUiState.active ? (
                  <Button
                    icon="close"
                    small
                    variant="danger"
                    onClick={stopBurstSimulation}
                    title="Stop active burst animation"
                  >
                    Stop Burst ({burstUiState.shotsFired}/{burstUiState.totalShots})
                  </Button>
                ) : (
                  <Button
                    icon="play"
                    small
                    variant="primary"
                    onClick={startBurstSimulation}
                    title={`Simulate ${sceneMeta.burstCount}-shot burst (recoil dist=${sceneMeta.recoil.maxDistance}, recoil=${sceneMeta.recoil.recoilDuration}t, return=${sceneMeta.recoil.returnDuration}t, interval=${sceneMeta.ticksBetween}t)`}
                  >
                    Start Burst ({sceneMeta.burstCount}×)
                  </Button>
                )}
              </div>

              {/* Mode Switching Tabs */}
              <div className="mode-switch" role="tablist" aria-label="Turret Fire Mode">
                {currentFileEntry.direct && (
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activeMode === 'direct'}
                    aria-pressed={activeMode === 'direct'}
                    className={activeMode === 'direct' ? 'live' : ''}
                    onClick={() => setActiveMode('direct')}
                  >
                    Direct Fire
                  </button>
                )}
                {currentFileEntry.indirect && (
                  <button
                    type="button"
                    role="tab"
                    aria-selected={activeMode === 'indirect'}
                    aria-pressed={activeMode === 'indirect'}
                    className={activeMode === 'indirect' ? 'sandbox' : ''}
                    onClick={() => setActiveMode('indirect')}
                  >
                    Indirect Fire
                  </button>
                )}
              </div>

              {onEdit && (
                <Button icon="edit" small onClick={() => onEdit(activeTurret.defName)}>
                  Edit Def
                </Button>
              )}
            </div>

            {/* Rotation Slider, Marker Shape Toggle & Viewport Toolbar */}
            <div className="tex-controls-bar">
              <div className="tex-rotation-control">
                <label htmlFor="turret-rot-slider">
                  <strong>Head Rotation:</strong>{' '}
                  <span className="mono" style={{ display: 'inline-block', minWidth: 42 }}>{rotation}°</span>
                </label>
                <input
                  id="turret-rot-slider"
                  type="range"
                  min={0}
                  max={360}
                  step={1}
                  value={rotation}
                  onChange={(e) => setRotation(Number(e.target.value))}
                />
                <div className="row" style={{ gap: 4 }}>
                  {[
                    { deg: 0, label: '0° N' },
                    { deg: 90, label: '90° E' },
                    { deg: 180, label: '180° S' },
                    { deg: 270, label: '270° W' },
                  ].map((p) => (
                    <button
                      key={p.deg}
                      type="button"
                      className="chip"
                      style={{ padding: '2px 8px', fontSize: 11.5 }}
                      aria-pressed={rotation === p.deg}
                      onClick={() => setRotation(p.deg)}
                    >
                      {p.label}
                    </button>
                  ))}
                </div>
              </div>

              <span className="spacer" />

              {/* Marker Shape Toggle: X vs O (Circle) */}
              <div className="row" style={{ gap: 6 }}>
                <span className="muted" style={{ fontSize: 12 }}>Markers:</span>
                <div className="mode-switch" role="group" aria-label="Marker shape">
                  <button
                    type="button"
                    aria-pressed={markerShape === 'X'}
                    onClick={() => setMarkerShape('X')}
                    style={{ padding: '2px 9px', fontSize: 11.5 }}
                    title="Draw markers as X crosses"
                  >
                    ✕ Cross
                  </button>
                  <button
                    type="button"
                    aria-pressed={markerShape === 'O'}
                    onClick={() => setMarkerShape('O')}
                    style={{ padding: '2px 9px', fontSize: 11.5 }}
                    title="Draw markers as O circles"
                  >
                    ◯ Circle
                  </button>
                </div>
              </div>

              <div className="row" style={{ gap: 6 }}>
                <span className="muted" style={{ fontSize: 12 }}>Zoom: {zoom}px/tile</span>
                <Button small onClick={() => setZoom((z) => Math.max(12, z - 8))}>−</Button>
                <Button small onClick={() => setZoom((z) => Math.min(180, z + 8))}>+</Button>
                <Button small onClick={() => { setZoom(44); setPan({ x: 0, y: 0 }); }}>Reset View</Button>
                <Button small onClick={() => setDarkBg((d) => !d)}>
                  {darkBg ? 'Light Grid' : 'Dark Grid'}
                </Button>
              </div>
            </div>

            {/* Layer Visibility Tickboxes Bar */}
            <div className="tex-layers-bar">
              <strong style={{ fontSize: 11.5, textTransform: 'uppercase', color: 'var(--ink-soft)' }}>
                Layers:
              </strong>
              <label className="tex-check">
                <input type="checkbox" checked={layers.grid} onChange={() => toggleLayer('grid')} />
                <span>Tile Grid</span>
              </label>
              <label className="tex-check">
                <input type="checkbox" checked={layers.footprint} onChange={() => toggleLayer('footprint')} />
                <span>Footprint ({sceneMeta.footprintSize[0]}×{sceneMeta.footprintSize[1]})</span>
              </label>
              <label className="tex-check">
                <input type="checkbox" checked={layers.canvasBounds} onChange={() => toggleLayer('canvasBounds')} />
                <span>Canvas Outlines</span>
              </label>

              <span className="tex-divider" />

              <label className="tex-check">
                <input type="checkbox" checked={layers.base} onChange={() => toggleLayer('base')} />
                <span>1. Base ({sceneMeta.baseDrawSize[0]}×{sceneMeta.baseDrawSize[1]})</span>
              </label>

              <label
                className={`tex-check${!activeTurret.barrel?.underGraphic?.texPath ? ' disabled' : ''}`}
                title={activeTurret.barrel?.underGraphic?.texPath || 'No underBarrelGraphic configured'}
              >
                <input
                  type="checkbox"
                  checked={layers.underBarrel}
                  disabled={!activeTurret.barrel?.underGraphic?.texPath}
                  onChange={() => toggleLayer('underBarrel')}
                />
                <span>2. Under-Barrel</span>
              </label>

              <label
                className={`tex-check${!activeTurret.barrel?.graphic?.texPath ? ' disabled' : ''}`}
                title={activeTurret.barrel?.graphic?.texPath || 'No barrelGraphic configured'}
              >
                <input
                  type="checkbox"
                  checked={layers.barrel}
                  disabled={!activeTurret.barrel?.graphic?.texPath}
                  onChange={() => toggleLayer('barrel')}
                />
                <span>
                  {sceneMeta.drawOnTop ? '4. Barrel (On Top)' : '3. Barrel'} ({sceneMeta.barrelDrawSize[0]}×{sceneMeta.barrelDrawSize[1]})
                </span>
              </label>

              <label className="tex-check">
                <input type="checkbox" checked={layers.top} onChange={() => toggleLayer('top')} />
                <span>
                  {sceneMeta.drawOnTop ? '3. Top' : '4. Top'} ({sceneMeta.topDrawSize[0]}×{sceneMeta.topDrawSize[1]})
                </span>
              </label>

              <label
                className={`tex-check${!activeTurret.muzzleFlash?.enabled ? ' disabled' : ''}`}
                title={activeTurret.muzzleFlash?.enabled ? `${activeTurret.muzzleFlash.def} ${activeTurret.muzzleFlash.drawSize}` : 'No Muzzle Flash patch'}
              >
                <input
                  type="checkbox"
                  checked={layers.muzzleFlash}
                  disabled={!activeTurret.muzzleFlash?.enabled}
                  onChange={() => toggleLayer('muzzleFlash')}
                />
                <span>5. Muzzle Flash</span>
              </label>

              <label
                className={`tex-check${!(sceneMeta.firing.enabled && sceneMeta.firing.drawFlash) ? ' disabled' : ''}`}
                title="TurretBarrelExtension.firingAnimation glow flash"
              >
                <input
                  type="checkbox"
                  checked={layers.firingFlash}
                  disabled={!(sceneMeta.firing.enabled && sceneMeta.firing.drawFlash)}
                  onChange={() => toggleLayer('firingFlash')}
                />
                <span>6. Firing Glow</span>
              </label>

              <span className="tex-divider" />

              <label
                className={`tex-check${!(activeTurret.hasInteractionCell && activeTurret.interactionCellOffset) ? ' disabled' : ''}`}
                title={`interactionCellOffset: ${activeTurret.interactionCellOffset || 'none'}`}
              >
                <input
                  type="checkbox"
                  checked={layers.interactionCell}
                  disabled={!(activeTurret.hasInteractionCell && activeTurret.interactionCellOffset)}
                  onChange={() => toggleLayer('interactionCell')}
                />
                <span className="tex-marker-swatch" style={{ color: '#16a34a' }}>
                  {markerGlyph} Interaction Cell
                </span>
              </label>

              <label
                className={`tex-check${!openTopOverlay(activeTurret, rotation) ? ' disabled' : ''}`}
                title={`seatOffset: ${activeTurret.comps?.enclosed?.seatOffset || 'none'}`}
              >
                <input
                  type="checkbox"
                  checked={layers.seat}
                  disabled={!openTopOverlay(activeTurret, rotation)}
                  onChange={() => toggleLayer('seat')}
                />
                <span className="tex-marker-swatch" style={{ color: '#f59e0b' }}>
                  ● Gunner Seat
                </span>
              </label>

              <label
                className={`tex-check${!openTopOverlay(activeTurret, rotation)?.shield ? ' disabled' : ''}`}
                title={`shieldArc: ${activeTurret.comps?.enclosed?.shieldArc ?? 'none'}°, shieldHeight: ${activeTurret.comps?.enclosed?.shieldHeight ?? '-'}, shieldArmor: ${activeTurret.comps?.enclosed?.shieldArmor ?? '-'}mm`}
              >
                <input
                  type="checkbox"
                  checked={layers.shield}
                  disabled={!openTopOverlay(activeTurret, rotation)?.shield}
                  onChange={() => toggleLayer('shield')}
                />
                <span className="tex-marker-swatch" style={{ color: '#f59e0b' }}>
                  ◔ Gun Shield
                </span>
              </label>

              <label
                className={`tex-check${!activeTurret.barrel?.enabled ? ' disabled' : ''}`}
                title={`projectileSpawnOffset: ${sceneMeta.firing.projectileSpawnOffset}`}
              >
                <input
                  type="checkbox"
                  checked={layers.projectileSpawn}
                  disabled={!activeTurret.barrel?.enabled}
                  onChange={() => toggleLayer('projectileSpawn')}
                />
                <span className="tex-marker-swatch" style={{ color: '#ca8a04' }}>
                  {markerGlyph} Projectile Spawn
                </span>
              </label>

              <label
                className={`tex-check${!activeTurret.smoker?.muzzle?.enabled ? ' disabled' : ''}`}
                title="CompProperties_TurretSmoker: muzzleOffset"
              >
                <input
                  type="checkbox"
                  checked={layers.smokeMuzzle}
                  disabled={!activeTurret.smoker?.muzzle?.enabled}
                  onChange={() => toggleLayer('smokeMuzzle')}
                />
                <span className="tex-marker-swatch" style={{ color: '#0891b2' }}>
                  {markerGlyph} Muzzle Smoke
                </span>
              </label>

              <label
                className={`tex-check${!activeTurret.smoker?.heat?.enabled ? ' disabled' : ''}`}
                title="CompProperties_TurretSmoker: heatOffset + heatEmissionPoints"
              >
                <input
                  type="checkbox"
                  checked={layers.smokeHeat}
                  disabled={!activeTurret.smoker?.heat?.enabled}
                  onChange={() => toggleLayer('smokeHeat')}
                />
                <span className="tex-marker-swatch" style={{ color: '#ea580c' }}>
                  {markerGlyph} Heat Smoke
                </span>
              </label>

              <label
                className={`tex-check${!activeTurret.smoker?.shockwave?.enabled ? ' disabled' : ''}`}
                title="CompProperties_TurretSmoker: shockwaveOffset & shockwaveRadius"
              >
                <input
                  type="checkbox"
                  checked={layers.smokeShockwave}
                  disabled={!activeTurret.smoker?.shockwave?.enabled}
                  onChange={() => toggleLayer('smokeShockwave')}
                />
                <span className="tex-marker-swatch" style={{ color: '#c026d3' }}>
                  {markerGlyph} Shockwave
                </span>
              </label>
            </div>

            {/* Interactive Canvas Viewport */}
            <div
              ref={wrapRef}
              className="tex-canvas-wrap"
              onMouseDown={handleMouseDown}
              onMouseMove={handleMouseMove}
              onMouseUp={handleMouseUp}
              onMouseLeave={handleMouseUp}
            >
              <canvas ref={canvasRef} className="tex-canvas" />
            </div>

            {/* Metadata & Layer Summary Footer */}
            <div className="tex-footer">
              <div className="tex-meta-item">
                <span className="muted">Placement &lt;size&gt;:</span>{' '}
                <strong className="mono">{sceneMeta.footprintSize[0]}×{sceneMeta.footprintSize[1]} tiles</strong>
              </div>
              <div className="tex-meta-item">
                <span className="muted">Base &lt;drawSize&gt;:</span>{' '}
                <strong className="mono">{sceneMeta.baseDrawSize[0]}×{sceneMeta.baseDrawSize[1]}</strong>{' '}
                <span className="mono muted">({activeTurret.textures?.building || 'none'})</span>
              </div>
              <div className="tex-meta-item">
                <span className="muted">Top &lt;turretTopDrawSize&gt;:</span>{' '}
                <strong className="mono">{sceneMeta.topDrawSize[0]}×{sceneMeta.topDrawSize[1]}</strong>{' '}
                <span className="mono muted">({activeTurret.textures?.weapon || 'none'})</span>
              </div>
              {activeTurret.barrel?.enabled && (
                <div className="tex-meta-item">
                  <span className="muted">Barrel &lt;barrelDrawSize&gt;:</span>{' '}
                  <strong className="mono">
                    {sceneMeta.barrelDrawSize[0]}×{sceneMeta.barrelDrawSize[1]}
                    {sceneMeta.barrelAmount > 1 ? ` (×${sceneMeta.barrelAmount}, spacing ${sceneMeta.barrelSpacing})` : ''}
                  </strong>{' '}
                  <span className="mono muted">
                    recoil={sceneMeta.recoil.maxDistance} ({sceneMeta.recoil.recoilDuration}t/{sceneMeta.recoil.returnDuration}t)
                    · projOffset={sceneMeta.firing.projectileSpawnOffset}
                  </span>
                </div>
              )}
              {activeTurret.muzzleFlash?.enabled && (
                <div className="tex-meta-item">
                  <span className="muted">Muzzle Flash:</span>{' '}
                  <strong className="mono">{activeTurret.muzzleFlash.def}</strong>{' '}
                  <span className="mono muted">
                    size={activeTurret.muzzleFlash.drawSize} offsets=[{activeTurret.muzzleFlash.offsets.join(', ')}]
                  </span>
                </div>
              )}
              {hasDualModes && (
                <div className="tex-meta-item">
                  <Pill tone="lilac">Dual-Mode XML</Pill>
                </div>
              )}
            </div>
          </>
        ) : (
          <div className="empty">Select a turret XML file on the left to inspect its textures and layers.</div>
        )}
      </section>
    </div>
  );
}
