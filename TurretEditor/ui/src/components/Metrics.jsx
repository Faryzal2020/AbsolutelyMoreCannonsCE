import React from 'react';

const CARDS = [
  { key: 'total', name: 'Total Systems', tone: 'blue' },
  { key: 'modeSwapCapable', name: 'Mode Swap Capable', tone: 'mint' },
  { key: 'powered', name: 'Powered Systems', tone: 'lilac' },
  { key: 'animated', name: 'Recoil / Rotary Animated', tone: 'peach' },
  { key: 'modified', name: 'Modified Systems', tone: 'butter' },
  { key: 'warnings', name: 'Active Audit Warnings', tone: 'rose' },
];

export default function Metrics({ metrics, modifiedCount }) {
  const values = { ...(metrics || {}), modified: modifiedCount };
  return (
    <div className="panel tint-slate">
      <div className="panel-body">
        <div className="metrics">
          {CARDS.map((c) => (
            <div key={c.key} className={`metric ${c.tone}`}>
              <div className="value">{metrics ? (values[c.key] ?? 0) : '—'}</div>
              <div className="name">{c.name}</div>
            </div>
          ))}
        </div>
        {metrics?.lastExtraction && (
          <div className="muted" style={{ fontSize: 11.5, marginTop: 10 }}>
            Last extraction {new Date(metrics.lastExtraction).toLocaleString()}
            {metrics.backups > 0 && ` · ${metrics.backups} backup file(s) on disk`}
          </div>
        )}
      </div>
    </div>
  );
}
