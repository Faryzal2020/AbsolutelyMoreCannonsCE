import React from 'react';
import { Icon } from './ui.jsx';

export default function Filters({ search, onSearch, filter, onFilter, options }) {
  return (
    <div className="toolbar">
      <div className="search">
        <Icon name="search" />
        <input
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          placeholder="Search defName, label, category, verb class, ammo set or file path"
          aria-label="Search turrets"
        />
        {search && (
          <button className="btn ghost small" onClick={() => onSearch('')} aria-label="Clear search">
            <Icon name="close" size={12} />
          </button>
        )}
      </div>
      <div className="chips">
        {options.map((o) => (
          <button
            key={o.id}
            className="chip"
            aria-pressed={filter === o.id}
            onClick={() => onFilter(o.id)}
          >
            {o.label}
          </button>
        ))}
      </div>
    </div>
  );
}
