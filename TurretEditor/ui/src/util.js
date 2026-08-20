/** Read a value from a nested object by dotted path. */
export function getPath(obj, dotted) {
  return dotted.split('.').reduce((acc, k) => (acc == null ? undefined : acc[k]), obj);
}

/** Build the minimal nested patch object for one dotted path. */
export function patchFor(dotted, value) {
  const parts = dotted.split('.');
  const root = {};
  let cur = root;
  for (let i = 0; i < parts.length - 1; i++) cur = (cur[parts[i]] = {});
  cur[parts[parts.length - 1]] = value;
  return root;
}

/** Deep merge used to preview edits locally before the server round trip. */
export function mergeDeep(base, patch) {
  if (Array.isArray(patch) || patch === null || typeof patch !== 'object') return patch;
  const out = { ...base };
  for (const [k, v] of Object.entries(patch)) {
    const b = base?.[k];
    out[k] = (v && typeof v === 'object' && !Array.isArray(v) && b && typeof b === 'object' && !Array.isArray(b))
      ? mergeDeep(b, v) : v;
  }
  return out;
}

export const fmt = (v, dash = '—') =>
  (v === null || v === undefined || v === '' ? dash : String(v));

export const fmtNum = (v, dash = '—') => {
  if (v === null || v === undefined || v === '') return dash;
  const n = Number(v);
  return Number.isFinite(n) ? n.toLocaleString() : String(v);
};

/**
 * Line diff via longest common subsequence — small inputs (one def block), so
 * the quadratic table is fine and gives exact add/delete marks.
 */
export function diffLines(before, after) {
  const a = before.split('\n');
  const b = after.split('\n');
  const n = a.length, m = b.length;

  const lcs = Array.from({ length: n + 1 }, () => new Uint32Array(m + 1));
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      lcs[i][j] = a[i] === b[j] ? lcs[i + 1][j + 1] + 1 : Math.max(lcs[i + 1][j], lcs[i][j + 1]);
    }
  }

  const rows = [];
  let i = 0, j = 0;
  while (i < n && j < m) {
    if (a[i] === b[j]) { rows.push({ type: 'same', left: a[i], right: b[j], ln: i + 1, rn: j + 1 }); i++; j++; }
    else if (lcs[i + 1][j] >= lcs[i][j + 1]) { rows.push({ type: 'del', left: a[i], right: null, ln: i + 1, rn: null }); i++; }
    else { rows.push({ type: 'add', left: null, right: b[j], ln: null, rn: j + 1 }); j++; }
  }
  while (i < n) { rows.push({ type: 'del', left: a[i], right: null, ln: i + 1, rn: null }); i++; }
  while (j < m) { rows.push({ type: 'add', left: null, right: b[j], ln: null, rn: j + 1 }); j++; }
  return rows;
}

/** Collapse long runs of unchanged lines so the changed regions stay visible. */
export function collapseContext(rows, context = 3) {
  const keep = new Set();
  rows.forEach((r, idx) => {
    if (r.type === 'same') return;
    for (let k = idx - context; k <= idx + context; k++) if (k >= 0 && k < rows.length) keep.add(k);
  });
  if (keep.size === 0) return rows.slice(0, 12).map((r) => ({ ...r }));

  const out = [];
  let hidden = 0;
  rows.forEach((r, idx) => {
    if (keep.has(idx)) {
      if (hidden) { out.push({ type: 'gap', count: hidden }); hidden = 0; }
      out.push(r);
    } else hidden++;
  });
  if (hidden) out.push({ type: 'gap', count: hidden });
  return out;
}

/**
 * Trigger a browser download from a server URL. The endpoint sets
 * Content-Disposition, so the browser saves the file rather than navigating.
 */
export function downloadUrl(url) {
  const a = document.createElement('a');
  a.href = url;
  a.rel = 'noopener';
  document.body.appendChild(a);
  a.click();
  a.remove();
}
