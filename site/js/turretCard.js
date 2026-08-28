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

    // Get thumbnail frame
    const thumbHtml = getThumbnailHtml(turret.imageUrl, turret.caliber, turret.label);

    // Feature Badges HTML
    const badgesHtml = turret.badges && turret.badges.length > 0
        ? turret.badges.map(b => getBadgePillHtml(b)).join('')
        : `<span class="badge badge-cyan">Standard Turret</span>`;

    // Mode ballistics reference (prefer Direct, fallback to Indirect)
    const activeMode = turret.modes.direct || turret.modes.indirect || {};
    const verb = activeMode.verb || {};

    const rangeStr = verb.minRange ? `${verb.minRange} - ${verb.range} cells` : `${verb.range || 0} cells`;
    const cooldownStr = `${activeMode.turretCooldown || verb.warmupTime || 0}s`;
    const hpStr = `${turret.common.maxHitPoints}`;
    const workStr = `${turret.common.workToBuild.toLocaleString()}`;
    const magSizeStr = activeMode.magazineSize ? `${activeMode.magazineSize}` : 'N/A';
    const powerStr = turret.common.basePowerConsumption > 0 ? `${turret.common.basePowerConsumption}W` : 'N/A';

    // Specialized Highlights List
    const specHighlights = getSpecializedHighlights(turret);

    card.innerHTML = `
        <header class="card-header">
            <div class="card-thumb-container">
                ${thumbHtml}
            </div>
            <div class="card-meta">
                <h3 class="card-title" title="${turret.label}">${turret.label}</h3>
                <div class="card-subtitle">
                    <span class="category-tag">${turret.category}</span>
                    <span>•</span>
                    <span class="badge badge-amber">${turret.caliber}</span>
                </div>
            </div>
        </header>

        <div class="card-badges">
            ${badgesHtml}
        </div>

        <div class="card-stats-grid">
            <div class="stat-item">
                <span class="stat-label">Max HP</span>
                <span class="stat-value highlight">${hpStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Work to Build</span>
                <span class="stat-value">${workStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Power</span>
                <span class="stat-value">${powerStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Range</span>
                <span class="stat-value">${rangeStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Cooldown</span>
                <span class="stat-value">${cooldownStr}</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Mag Size</span>
                <span class="stat-value">${magSizeStr}</span>
            </div>
        </div>

        ${specHighlights ? `
            <div class="card-specialized-box">
                <div class="specialized-header">⚙️ Specialized Features</div>
                <ul class="specialized-list">
                    ${specHighlights}
                </ul>
            </div>
        ` : ''}

        <footer class="card-footer">
            <button class="view-btn" data-action="open-detail">
                <span>View Full Specs & Ammo</span>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="5" y1="12" x2="19" y2="12"></line>
                    <polyline points="12 5 19 12 12 19"></polyline>
                </svg>
            </button>
        </footer>
    `;

    // Click Handlers
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
 * Returns formatted badge pill HTML for turret card
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
 * Summarize active specialized mechanics into concise list items for card view
 */
function getSpecializedHighlights(turret) {
    const items = [];
    const spec = turret.specialized || {};

    if (spec.enclosed) {
        items.push(`<li class="specialized-item"><strong>Armored Enclosure:</strong> ${spec.enclosed.bulletProtection}% Bullet / ${spec.enclosed.explosiveProtection}% Explosive immunity</li>`);
    }

    if (spec.ciws) {
        items.push(`<li class="specialized-item"><strong>CIWS Air Defense:</strong> Intercepts incoming projectiles up to ${spec.ciws.interceptionRange} cells</li>`);
    }

    if (spec.variableRpm && spec.variableRpm.selectableBurstCounts && spec.variableRpm.selectableBurstCounts.length > 0) {
        items.push(`<li class="specialized-item"><strong>Fire Control:</strong> Selectable burst sizes (${spec.variableRpm.selectableBurstCounts.join(' / ')} rounds)</li>`);
    }

    if (spec.clamping) {
        items.push(`<li class="specialized-item"><strong>Turret Clamping:</strong> Elevation [${spec.clamping.minElevationAngle}°], Max Deviation [${spec.clamping.maxRotationDeviation}°]</li>`);
    }

    if (spec.ammoPreservation && spec.ammoPreservation.preserveAmmo) {
        items.push(`<li class="specialized-item"><strong>Smart Autoloader:</strong> Ammo retention & mid-burst tracking active</li>`);
    }

    const shellingAmmo = turret.ammunition && turret.ammunition.find(a => a.projectile && a.projectile.shellingProps);
    if (shellingAmmo) {
        const sp = shellingAmmo.projectile.shellingProps;
        items.push(`<li class="specialized-item"><strong>Cross-Map Bombardment:</strong> Shelling range up to ${sp.range} map tiles</li>`);
    }

    return items.join('');
}
