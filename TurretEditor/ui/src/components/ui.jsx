import React from 'react';

/* Flat, single-colour glyphs. Icons appear only where a button is too small
   for a text label, or alongside text as a quiet visual anchor. */
const paths = {
  search: 'M11 4a7 7 0 1 0 4.2 12.6l3.6 3.6 1.4-1.4-3.6-3.6A7 7 0 0 0 11 4Zm0 2a5 5 0 1 1 0 10 5 5 0 0 1 0-10Z',
  undo: 'M8 7V4L2 9l6 5v-3h5a4 4 0 0 1 0 8H9v2h4a6 6 0 0 0 0-12H8Z',
  redo: 'M16 7V4l6 5-6 5v-3h-5a4 4 0 0 0 0 8h4v2h-4a6 6 0 0 1 0-12h5Z',
  edit: 'M4 17.5V20h2.5l9.9-9.9-2.5-2.5L4 17.5Zm15.7-9.4a.9.9 0 0 0 0-1.3l-1.5-1.5a.9.9 0 0 0-1.3 0l-1.3 1.3 2.8 2.8 1.3-1.3Z',
  diff: 'M9 3v4H5v2h4v4h2V9h4V7h-4V3H9Zm-4 14h14v2H5v-2Z',
  revert: 'M12 5V2L7 6l5 4V7a5 5 0 1 1-5 5H5a7 7 0 1 0 7-7Z',
  close: 'M18.3 5.7 12 12l6.3 6.3-1.4 1.4L10.6 13.4 4.3 19.7 2.9 18.3 9.2 12 2.9 5.7 4.3 4.3l6.3 6.3 6.3-6.3 1.4 1.4Z',
  download: 'M12 3v10.2l3.6-3.6 1.4 1.4-6 6-6-6 1.4-1.4L10 13.2V3h2ZM4 19h16v2H4v-2Z',
  plus: 'M11 5h2v6h6v2h-6v6h-2v-6H5v-2h6V5Z',
  trash: 'M9 3h6l1 2h4v2H4V5h4l1-2Zm-3 6h12l-1 12H7L6 9Z',
  check: 'M9.6 16.2 5.4 12l-1.4 1.4 5.6 5.6L20 8.6 18.6 7.2 9.6 16.2Z',
  warn: 'M12 3 1.5 21h21L12 3Zm0 5 6.9 11.9H5.1L12 8Zm-1 3v5h2v-5h-2Zm0 6v2h2v-2h-2Z',
  camera: 'M9 4h6l1.5 2H20a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h3.5L9 4Zm3 4.5A4.5 4.5 0 1 0 12 17a4.5 4.5 0 0 0 0-8.5Z',
  clipboard: 'M9 2h6a1 1 0 0 1 1 1v1h2a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h2V3a1 1 0 0 1 1-1Zm0 3v1h6V5H9Z',
  play: 'M6 4l14 8-14 8V4Z',
  inject: 'M4 12h10v-3l6 4-6 4v-3H4v-2Z',
  link: 'M10.6 13.4a4 4 0 0 0 5.7 0l2.8-2.8a4 4 0 1 0-5.7-5.7l-1.4 1.4 1.4 1.4 1.4-1.4a2 2 0 1 1 2.8 2.8l-2.8 2.8a2 2 0 0 1-2.8 0l-1.4 1.5ZM13.4 10.6a4 4 0 0 0-5.7 0l-2.8 2.8a4 4 0 0 0 5.7 5.7l1.4-1.4-1.4-1.4-1.4 1.4a2 2 0 0 1-2.8-2.8l2.8-2.8a2 2 0 0 1 2.8 0l1.4-1.5Z',
  stats: 'M4 20V10h4v10H4Zm6 0V4h4v16h-4Zm6 0v-7h4v7h-4Z',
  coins: 'M12 3c4.4 0 8 1.6 8 3.5S16.4 10 12 10 4 8.4 4 6.5 7.6 3 12 3Zm8 6.4v3.1c0 1.9-3.6 3.5-8 3.5s-8-1.6-8-3.5V9.4C5.7 10.8 8.7 11.6 12 11.6s6.3-.8 8-2.2Zm0 6v2.1c0 1.9-3.6 3.5-8 3.5s-8-1.6-8-3.5v-2.1c1.7 1.4 4.7 2.2 8 2.2s6.3-.8 8-2.2Z',
  target: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 2.5A7.5 7.5 0 1 1 4.5 12 7.5 7.5 0 0 1 12 4.5Zm0 3A4.5 4.5 0 1 0 16.5 12 4.5 4.5 0 0 0 12 7.5Zm0 2.5a2 2 0 1 1-2 2 2 2 0 0 1 2-2Z',
  gear: 'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm9.2 4a7.6 7.6 0 0 0-.1-1.2l2-1.6-2-3.4-2.4 1a7.4 7.4 0 0 0-2-1.2L16.3 3h-4l-.4 2.6a7.4 7.4 0 0 0-2 1.2l-2.4-1-2 3.4 2 1.6a7.6 7.6 0 0 0 0 2.4l-2 1.6 2 3.4 2.4-1a7.4 7.4 0 0 0 2 1.2l.4 2.6h4l.4-2.6a7.4 7.4 0 0 0 2-1.2l2.4 1 2-3.4-2-1.6c.1-.4.1-.8.1-1.2Z',
  barrel: 'M3 10h11V7l7 5-7 5v-3H3v-4Z',
  smoke: 'M7 18a4 4 0 0 1-.6-8A5 5 0 0 1 16 8.6 3.7 3.7 0 0 1 17.5 18H7Zm1 3h9v2H8v-2Z',
  globe: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 2c1.3 0 2.8 2.3 3.3 6H8.7C9.2 6.3 10.7 4 12 4ZM4.3 10h2.4a20 20 0 0 0 0 4H4.3a8 8 0 0 1 0-4Zm0 6h2.6c.4 1.9 1 3.4 1.8 4.3A8 8 0 0 1 4.3 16Zm4.4 0h6.6C14.8 19.7 13.3 22 12 22s-2.8-2.3-3.3-6Zm.1-2a17 17 0 0 1 0-4h6.4a17 17 0 0 1 0 4H8.8Zm6.5 6.3c.8-.9 1.4-2.4 1.8-4.3h2.6a8 8 0 0 1-4.4 4.3ZM17.3 14a20 20 0 0 0 0-4h2.4a8 8 0 0 1 0 4h-2.4Zm1.4-6h-2.6c-.4-1.9-1-3.4-1.8-4.3A8 8 0 0 1 18.7 8ZM9.3 3.7C8.5 4.6 7.9 6.1 7.5 8H4.9a8 8 0 0 1 4.4-4.3Z',
  balance: 'M12 2v2.2l6 1.5V8l-1.4-.3-2.2 6.6A3.8 3.8 0 0 0 18 18a3.8 3.8 0 0 0 3.6-3.7L19.4 7.7 21 8V5.7l-9-2.3-9 2.3V8l1.6-.4-2.2 6.6A3.8 3.8 0 0 0 6 18a3.8 3.8 0 0 0 3.6-3.7L7.4 7.7 11 6.8V20H7v2h10v-2h-4V6.8l-1-.3V2Z',
};

export function Icon({ name, size = 14 }) {
  const d = paths[name];
  if (!d) return null;
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false">
      <path d={d} />
    </svg>
  );
}

export function Button({ icon, children, variant, small, ...rest }) {
  const cls = ['btn', variant, small ? 'small' : ''].filter(Boolean).join(' ');
  return (
    <button type="button" className={cls} {...rest}>
      {icon && <Icon name={icon} size={small ? 12 : 14} />}
      {children}
    </button>
  );
}

export function Pill({ tone, children, title }) {
  return <span className={`pill ${tone || ''}`} title={title}>{children}</span>;
}

export function Field({ label, hint, value, onChange, type = 'text', options, disabled, dirty, placeholder, step }) {
  const id = React.useId();
  return (
    <div className={`field${dirty ? ' dirty' : ''}`}>
      <label htmlFor={id}>{label}</label>
      {options ? (
        <select id={id} value={value ?? ''} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
          {options.map((o) => <option key={o.value ?? o} value={o.value ?? o}>{o.label ?? o}</option>)}
        </select>
      ) : (
        <input
          id={id}
          type={type}
          step={step}
          disabled={disabled}
          placeholder={placeholder ?? 'inherited'}
          value={value ?? ''}
          onChange={(e) => {
            const raw = e.target.value;
            if (type !== 'number') return onChange(raw);
            onChange(raw === '' ? null : Number(raw));
          }}
        />
      )}
      {hint && <span className="hint">{hint}</span>}
    </div>
  );
}

export function Toggle({ label, checked, onChange, disabled }) {
  return (
    <label className="toggle">
      <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <span className="track" />
      <span className="label">{label}</span>
    </label>
  );
}

export function Section({ title, tone, children, aside }) {
  return (
    <div className={`section ${tone || ''}`}>
      {(title || aside) && (
        <h3 style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span>{title}</span>
          <span style={{ flex: 1 }} />
          {aside}
        </h3>
      )}
      {children}
    </div>
  );
}

export function Modal({ title, subtitle, onClose, children, footer, narrow, headExtra }) {
  React.useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <div className="overlay" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className={`modal${narrow ? ' narrow' : ''}`} role="dialog" aria-modal="true" aria-label={title}>
        <div className="modal-head">
          <div>
            <h2>{title}</h2>
            {subtitle && <div className="muted mono" style={{ fontSize: 11.5 }}>{subtitle}</div>}
          </div>
          <span className="spacer" />
          {headExtra}
          <Button icon="close" variant="ghost" small onClick={onClose} aria-label="Close" />
        </div>
        {children}
        {footer && <div className="modal-foot">{footer}</div>}
      </div>
    </div>
  );
}
