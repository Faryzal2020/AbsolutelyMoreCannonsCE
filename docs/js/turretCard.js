/* ==========================================================================
   AMC-CE Web Wiki - Turret Card Renderer Component
   ========================================================================== */

import { getThumbnailHtml } from './dataLoader.js';

/**
 * Creates HTML element for a Turret Card
 */
export function createTurretCard(turret, onSelectCard) {
    const card = document.createElement('article');
    card.className = 'turret-card';
    card.dataset.id = turret.id;
    card.id = `turret-card-${turret.id}`;

    // Full-width top hero thumbnail
    const thumbHtml = getThumbnailHtml(turret.imageUrl, turret.caliber, turret.label);

    // Feature Badges HTML
    const badgesHtml = turret.badges && turret.badges.length > 0
        ? turret.badges.map(b => getBadgePillHtml(b)).join('')
        : `<span class="badge badge-cyan">Standard Turret</span>`;

    // Fire Modes & Range Comparison
    const rangePanelHtml = getRangePanelHtml(turret);

    // Core Specs Reference
    const activeMode = turret.modes.direct || turret.modes.indirect || {};
    const verb = activeMode.verb || {};

    const cooldownStr = `${activeMode.turretCooldown || verb.warmupTime || 0}s`;
    const hpStr = `${turret.common.maxHitPoints}`;
    const workStr = `${turret.common.workToBuild.toLocaleString()}`;
    const magSizeStr = activeMode.magazineSize ? `${activeMode.magazineSize}` : 'N/A';
    const reloadStr = activeMode.reloadTime ? `${activeMode.reloadTime}s` : 'N/A';
    const powerStr = turret.common.basePowerConsumption > 0 ? `${turret.common.basePowerConsumption}W` : 'Unpowered';

    // Ammo & Damage Summary
    const ammoSummaryHtml = getAmmoSummaryHtml(turret.ammunition);

    // Specialized Highlights
    const specHighlights = getSpecializedHighlights(turret);

    card.innerHTML = `
        <!-- Full-Width Hero Thumbnail Top Bar -->
        <div class="card-hero-thumb">
            ${thumbHtml}
        </div>

        <!-- Card Title & Meta Header -->
        <header class="card-header-bar">
            <div class="card-title-group">
                <h3 class="card-title" title="${turret.label}">${turret.label}</h3>
                <div class="card-subtitle-bar">
                    <span class="category-tag">${turret.category}</span>
                    <span class="sub-divider">•</span>
                    <span class="badge badge-amber">${turret.caliber}</span>
                </div>
            </div>
        </header>

        <!-- Feature Tags -->
        <div class="card-badges">
            ${badgesHtml}
        </div>

        <!-- Direct vs Indirect Range Comparison Bar -->
        ${rangePanelHtml}

        <!-- Core Stats Grid -->
        <div class="card-stats-grid">
            <div class="stat-item">
                <span class="stat-label">Max HP</span>
                <span class="stat-value highlight">${hpStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Work</span>
                <span class="stat-value">${workStr}</span>
            </div>
                <div class="stat-item">
                <span class="stat-label">Power</span>
                <span class="stat-value">${powerStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Cooldown</span>
                <span class="stat-value">${cooldownStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Mag Size</span>
                <span class="stat-value">${magSizeStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Reload</span>
                <span class="stat-value">${reloadStr}</span>
            </div>
        </div>

        <!-- Ammo & Damage Overview Box -->
        ${ammoSummaryHtml}

        <!-- Specialized Mechanics Highlights -->
        ${specHighlights ? `
            <div class="card-specialized-box">
                <div class="specialized-header">⚙️ Specialized Systems</div>
                <ul class="specialized-list">
                    ${specHighlights}
                </ul>
            </div>
        ` : ''}

        <!-- Action Footer -->
        <footer class="card-footer">
            <button class="view-btn" data-action="open-detail">
                <span>View Specs & Shell Matrix</span>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="5" y1="12" x2="19" y2="12"></line>
                    <polyline points="12 5 19 12 12 19"></polyline>
                </svg>
            </button>
        </footer>
    `;

    // Click Listeners
    card.querySelector('[data-action="open-detail"]').addEventListener('click', (e) => {
        e.stopPropagation();
        onSelectCard(turret);
    });

    card.addEventListener('click', () => {
        onSelectCard(turret);
    });

    return card;
}

/**
 * Format Direct vs Indirect Min & Max Range panel
 */
function getRangePanelHtml(turret) {
    const direct = turret.modes.direct;
    const indirect = turret.modes.indirect;

    if (direct && indirect) {
        const dVerb = direct.verb || {};
        const iVerb = indirect.verb || {};

        const dMin = dVerb.minRange || 0;
        const dMax = dVerb.range || 0;
        const iMin = iVerb.minRange || 0;
        const iMax = iVerb.range || 0;

        return `
            <div class="card-range-panel dual-mode">
                <div class="range-mode-col direct">
                    <span class="range-mode-tag">Direct Range</span>
                    <span class="range-val">${dMin > 0 ? dMin + ' - ' : ''}${dMax} cells</span>
                </div>
                <div class="range-mode-divider"></div>
                <div class="range-mode-col indirect">
                    <span class="range-mode-tag">Indirect Range</span>
                    <span class="range-val">${iMin > 0 ? iMin + ' - ' : ''}${iMax} cells</span>
                </div>
            </div>
        `;
    } else {
        const active = direct || indirect;
        const verb = active?.verb || {};
        const min = verb.minRange || 0;
        const max = verb.range || 0;
        const modeLabel = direct ? 'Direct Range' : 'Indirect Range';

        return `
            <div class="card-range-panel single-mode">
                <span class="range-mode-tag">${modeLabel}</span>
                <span class="range-val">${min > 0 ? min + ' - ' : ''}${max} cells</span>
            </div>
        `;
    }
}

/**
 * Format Ammo & Damage summary rows for the card view
 */
function getAmmoSummaryHtml(ammunitions) {
    if (!ammunitions || ammunitions.length === 0) {
        return '';
    }

    const rows = ammunitions.slice(0, 3).map(ammo => {
        const p = ammo.projectile || {};
        const damage = p.damageAmountBase || 0;
        const damageDef = p.damageDef || 'Damage';
        const ap = p.armorPenetrationSharp ? `${p.armorPenetrationSharp}mm AP` : null;
        const radius = p.explosionRadius ? `R: ${p.explosionRadius}m` : null;

        const extraInfo = [ap, radius].filter(Boolean).join(' | ');

        return `
            <div class="card-ammo-row">
                <span class="ammo-row-label" title="${ammo.label}">${ammo.label}</span>
                <span class="ammo-row-val">
                    <strong>${damage}</strong> ${damageDef}
                    ${extraInfo ? `<small>(${extraInfo})</small>` : ''}
                </span>
            </div>
        `;
    }).join('');

    const remaining = ammunitions.length > 3 ? `<div class="ammo-more-count">+${ammunitions.length - 3} more shell types</div>` : '';

    return `
        <div class="card-ammo-box">
            <div class="ammo-box-header">💣 Compatible Ammo & Damage Overview</div>
            <div class="ammo-box-list">
                ${rows}
                ${remaining}
            </div>
        </div>
    `;
}

/**
 * Returns formatted badge pill HTML
 */
function getBadgePillHtml(badge) {
    switch (badge) {
        case 'Dual Mode':
            return `<span class="badge badge-purple">🔄 Dual Mode</span>`;
        case 'Enclosed Protection':
            return `<span class="badge badge-emerald">🛡️ Enclosed</span>`;
        case 'CIWS Air Defense':
            return `<span class="badge badge-rose">🎯 CIWS</span>`;
        case 'Variable RPM':
            return `<span class="badge badge-amber">⚡ Variable RPM</span>`;
        case 'Turret Clamping':
            return `<span class="badge badge-cyan">🧭 Clamping</span>`;
        case 'Smart Autoloader':
            return `<span class="badge badge-purple">🔄 Autoloader</span>`;
        case 'Cross-Map Shelling':
            return `<span class="badge badge-rose">🌐 Shelling</span>`;
        case 'Airburst / Flak':
            return `<span class="badge badge-amber">💥 Airburst</span>`;
        default:
            return `<span class="badge badge-cyan">${badge}</span>`;
    }
}

/**
 * Summarize active specialized mechanics into concise list items
 */
function getSpecializedHighlights(turret) {
    const items = [];
    const spec = turret.specialized || {};

    if (spec.shellingProps) {
        const range = spec.shellingProps.range || 0;
        items.push(`<li class="specialized-item"><strong>Cross-Map Bombardment:</strong> Shelling range up to <span style="color: var(--accent-rose); font-weight:700;">${range} map tiles</span></li>`);
    }

    if (spec.enclosed) {
        items.push(`<li class="specialized-item"><strong>Armored Enclosure:</strong> ${spec.enclosed.bulletProtection}% Bullet / ${spec.enclosed.explosiveProtection}% Explosive immunity</li>`);
    }

    if (spec.ciws) {
        items.push(`<li class="specialized-item"><strong>CIWS Air Defense:</strong> Intercepts incoming rounds up to ${spec.ciws.interceptionRange} cells</li>`);
    }

    if (spec.variableRpm && spec.variableRpm.selectableBurstCounts && spec.variableRpm.selectableBurstCounts.length > 0) {
        items.push(`<li class="specialized-item"><strong>Fire Control:</strong> Selectable burst sizes (${spec.variableRpm.selectableBurstCounts.join(' / ')} rounds)</li>`);
    }

    if (spec.clamping) {
        items.push(`<li class="specialized-item"><strong>Turret Clamping:</strong> Elevation [${spec.clamping.minElevationAngle}°], Max Dev [${spec.clamping.maxRotationDeviation}°]</li>`);
    }

    return items.join('');
}
