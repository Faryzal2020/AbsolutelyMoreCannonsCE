/** Thin fetch wrapper — every call returns parsed JSON or throws with the server message. */
async function request(url, options = {}) {
  const res = await fetch(url, {
    headers: options.body ? { 'Content-Type': 'application/json' } : undefined,
    ...options,
  });
  const text = await res.text();
  let body;
  try { body = JSON.parse(text); } catch { body = { ok: false, error: text }; }
  if (!res.ok || body.ok === false) throw new Error(body.error || `${res.status} ${res.statusText}`);
  return body;
}

export const api = {
  health: () => request('/api/health'),
  extract: () => request('/api/extract', { method: 'POST', body: '{}' }),
  turrets: () => request('/api/turrets'),
  turret: (defName) => request(`/api/turrets/${encodeURIComponent(defName)}`),
  updateTurret: (defName, patch) =>
    request(`/api/turrets/${encodeURIComponent(defName)}`, { method: 'PUT', body: JSON.stringify(patch) }),
  revertTurret: (defName) =>
    request(`/api/turrets/${encodeURIComponent(defName)}/revert`, { method: 'POST', body: '{}' }),
  revertAll: () => request('/api/revert-all', { method: 'POST', body: '{}' }),
  restore: (turrets) => request('/api/restore', { method: 'POST', body: JSON.stringify({ turrets }) }),
  diffs: () => request('/api/diffs'),
  diffXml: (defName) => request(`/api/diffs/${encodeURIComponent(defName)}/xml`),
  inject: (options = {}) => request('/api/inject', { method: 'POST', body: JSON.stringify(options) }),
  rollback: (options = {}) => request('/api/rollback', { method: 'POST', body: JSON.stringify(options) }),
  backups: () => request('/api/backups'),
  metrics: () => request('/api/metrics'),
  audit: () => request('/api/audit'),
  exportColumns: (dataset) => request(`/api/export/columns?dataset=${encodeURIComponent(dataset)}`),
  /** Download URLs are hit directly by the browser so Content-Disposition applies. */
  exportUrl: ({ format, dataset, scope }) => {
    const params = new URLSearchParams({ format, scope });
    if (format === 'csv') params.set('dataset', dataset);
    return `/api/export?${params.toString()}`;
  },
};
