/* ==========================================================================
   AMC-CE Web Wiki - Detail Modal View Renderer Component
   ========================================================================== */

import { getThumbnailHtml } from './dataLoader.js';
import { renderAmmoTab } from './ammoView.js';

let currentTurret = null;
let currentMode = 'direct';

/**
 * Open detail modal for selected turret
 */
export function openDetailModal(turret) {
    currentTurret = turret;
    
    // Default mode setup (Prefer Direct if available, else Indirect)
    if (turret.modes.direct) {
        currentMode = 'direct';
    } else if (turret.modes.indirect) {
        currentMode = 'indirect';
    }

    const modal = document.getElementById('detail-modal');
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');

    // Render Modal Header & Title
    document.getElementById('modal-title').textContent = turret.label;
    document.getElementById('modal-caliber').textContent = `Caliber: ${turret.caliber}`;
    document.getElementById('modal-category-badge').textContent = turret.category;

    // Render Thumbnail
    const thumbContainer = document.getElementById('modal-thumb-container');
    thumbContainer.innerHTML = getThumbnailHtml(turret.imageUrl, turret.caliber, turret.label);

    // Setup Dual-Mode Switcher
    setupModeSwitcher(turret);

    // Update Ammo Badge Count
    document.getElementById('ammo-count-badge').textContent = turret.ammunition ? turret.ammunition.length : 0;

    // Render Active Tab Content
    renderAllTabs();

    // Default to Overview Tab
    setActiveTab('tab-overview');
}

/**
 * Close detail modal
 */
export function closeDetailModal() {
    const modal = document.getElementById('detail-modal');
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    currentTurret = null;
}

/**
 * Configure Direct/Indirect fire mode toggle buttons
 */
function setupModeSwitcher(turret) {
    const container = document.getElementById('mode-switch-container');
    const directBtn = document.getElementById('mode-btn-direct');
    const indirectBtn = document.getElementById('mode-btn-indirect');

    if (turret.modes.direct && turret.modes.indirect) {
        container.style.display = 'flex';
        
        directBtn.style.display = 'inline-block';
        indirectBtn.style.display = 'inline-block';

        directBtn.classList.toggle('active', currentMode === 'direct');
        indirectBtn.classList.toggle('active', currentMode === 'indirect');

        directBtn.onclick = () => switchMode('direct');
        indirectBtn.onclick = () => switchMode('indirect');
    } else {
        container.style.display = 'none';
    }
}

/**
 * Switch active fire mode (Direct <-> Indirect)
 */
function switchMode(mode) {
    currentMode = mode;
    document.getElementById('mode-btn-direct').classList.toggle('active', currentMode === 'direct');
    document.getElementById('mode-btn-indirect').classList.toggle('active', currentMode === 'indirect');
    
    // Re-render ballistics & ammo tabs for selected mode
    renderBallisticsTab();
    renderAmmoTab(currentTurret, currentMode);
}

/**
 * Tab Navigation Handler
 */
export function setActiveTab(tabId) {
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.tab === tabId);
    });
    
    document.querySelectorAll('.tab-panel').forEach(panel => {
        panel.classList.toggle('active', panel.id === tabId);
    });
}

/**
 * Render all 4 tab panels for the current turret
 */
function renderAllTabs() {
    renderOverviewTab();
    renderBallisticsTab();
    renderSpecializedTab();
    renderAmmoTab(currentTurret, currentMode);
}

/* --------------------------------------------------------------------------
   Tab 1: Overview & Building Specs
   -------------------------------------------------------------------------- */
function renderOverviewTab() {
    const container = document.getElementById('tab-overview');
    const t = currentTurret;
    const c = t.common;

    const costsHtml = Object.entries(c.costList || {}).map(([item, count]) => `
        <div class="chip">
            <span class="chip-item">${item}</span>
            <span class="chip-count">x${count}</span>
        </div>
    `).join('');

    const researchHtml = (c.researchPrerequisites || []).map(r => `
        <span class="badge badge-purple">${r}</span>
    `).join(' ') || '<span class="data-val">None</span>';

    container.innerHTML = `
        <div class="detail-card" style="margin-bottom: 1.25rem;">
            <div class="detail-card-title">📖 Description & Overview</div>
            <p style="color: var(--text-secondary); line-height: 1.6; font-size: 0.925rem;">${t.description || 'No detailed description available.'}</p>
        </div>

        <div class="detail-grid">
            <div class="detail-card">
                <div class="detail-card-title">🛠️ Physical & Building Specs</div>
                <div class="data-row"><span class="data-label">Max HP:</span><span class="data-val" style="color: var(--accent-cyan);">${c.maxHitPoints} HP</span></div>
                <div class="data-row"><span class="data-label">Work to Build:</span><span class="data-val">${c.workToBuild.toLocaleString()} ticks</span></div>
                <div class="data-row"><span class="data-label">Mass:</span><span class="data-val">${c.mass} kg</span></div>
                <div class="data-row"><span class="data-label">Footprint Size:</span><span class="data-val">${c.size[0]} x ${c.size[1]} cells</span></div>
                <div class="data-row"><span class="data-label">Base Power Consumption:</span><span class="data-val">${c.basePowerConsumption > 0 ? c.basePowerConsumption + ' W' : 'Unpowered / None'}</span></div>
                <div class="data-row"><span class="data-label">Construction Skill Req:</span><span class="data-val">${c.constructionSkillPrerequisite > 0 ? 'Level ' + c.constructionSkillPrerequisite : 'None'}</span></div>
                <div class="data-row"><span class="data-label">Terrain Required:</span><span class="data-val">${c.terrainAffordanceNeeded}</span></div>
            </div>

            <div class="detail-card">
                <div class="detail-card-title">🧪 Research & Material Costs</div>
                <div style="margin-bottom: 1rem;">
                    <span class="data-label" style="display: block; margin-bottom: 0.4rem; font-size: 0.8rem;">Research Prerequisites:</span>
                    <div>${researchHtml}</div>
                </div>
                <div>
                    <span class="data-label" style="display: block; margin-bottom: 0.4rem; font-size: 0.8rem;">Construction Material Costs:</span>
                    <div class="cost-chips">
                        ${costsHtml || '<span class="data-val">No material costs specified</span>'}
                    </div>
                </div>
            </div>
        </div>
    `;
}

/* --------------------------------------------------------------------------
   Tab 2: Fire Control & Ballistics
   -------------------------------------------------------------------------- */
function renderBallisticsTab() {
    const container = document.getElementById('tab-ballistics');
    const t = currentTurret;
    const modeData = t.modes[currentMode];

    if (!modeData) {
        container.innerHTML = `<div class="empty-state"><p>No ballistics data available for ${currentMode} mode.</p></div>`;
        return;
    }

    const verb = modeData.verb || {};
    const rangeStr = verb.minRange ? `${verb.minRange} - ${verb.range} cells` : `${verb.range || 0} cells`;
    const rpmStr = verb.calculatedRPM ? `${verb.calculatedRPM} RPM` : 'N/A';
    const chargesStr = (modeData.chargeSpeeds || []).join(', ') || 'Standard / Fixed Charge';

    container.innerHTML = `
        <div class="detail-grid">
            <div class="detail-card">
                <div class="detail-card-title">🎯 Fire Control & Timing (${currentMode.toUpperCase()} MODE)</div>
                <div class="data-row"><span class="data-label">Warmup / Aiming Time:</span><span class="data-val">${verb.warmupTime || 0} seconds</span></div>
                <div class="data-row"><span class="data-label">Turret Cooldown:</span><span class="data-val">${modeData.turretCooldown || 0} seconds</span></div>
                <div class="data-row"><span class="data-label">Effective Range:</span><span class="data-val" style="color: var(--accent-cyan);">${rangeStr}</span></div>
                <div class="data-row"><span class="data-label">Magazine Size:</span><span class="data-val">${modeData.magazineSize || 'N/A'} rounds</span></div>
                <div class="data-row"><span class="data-label">Reload Duration:</span><span class="data-val">${modeData.reloadTime || 0} seconds</span></div>
                <div class="data-row"><span class="data-label">Burst Shot Count:</span><span class="data-val">${verb.burstShotCount || 1} shots</span></div>
                <div class="data-row"><span class="data-label">Shot Delay:</span><span class="data-val">${verb.ticksBetweenBurstShots || 0} ticks (${((verb.ticksBetweenBurstShots || 0) / 60).toFixed(2)}s)</span></div>
                <div class="data-row"><span class="data-label">Rate of Fire:</span><span class="data-val" style="color: var(--accent-amber);">${rpmStr}</span></div>
            </div>

            <div class="detail-card">
                <div class="detail-card-title">📐 Accuracy, Dispersion & Optics</div>
                <div class="data-row"><span class="data-label">Sights Efficiency:</span><span class="data-val">${(modeData.sightsEfficiency * 100).toFixed(0)}%</span></div>
                <div class="data-row"><span class="data-label">Shot Spread:</span><span class="data-val">${modeData.shotSpread}</span></div>
                <div class="data-row"><span class="data-label">Sway Factor:</span><span class="data-val">${modeData.swayFactor}</span></div>
                <div class="data-row"><span class="data-label">Recoil Amount:</span><span class="data-val">${verb.recoilAmount || 0}</span></div>
                <div class="data-row"><span class="data-label">Circular Error (CE):</span><span class="data-val">${verb.circularError || 0}</span></div>
                <div class="data-row"><span class="data-label">Indirect Fire Penalty:</span><span class="data-val">${verb.indirectFirePenalty || 0}</span></div>
                <div class="data-row"><span class="data-label">Requires Line of Sight:</span><span class="data-val">${verb.requireLineOfSight ? 'Yes' : 'No (Indirect / High-Arc)'}</span></div>
                <div class="data-row"><span class="data-label">Propellant Charges:</span><span class="data-val">${chargesStr}</span></div>
            </div>
        </div>
    `;
}

/* --------------------------------------------------------------------------
   Tab 3: Specialized Mechanics Breakdown
   -------------------------------------------------------------------------- */
function renderSpecializedTab() {
    const container = document.getElementById('tab-specialized');
    const spec = currentTurret.specialized || {};
    const panels = [];

    // 1. Enclosed Turret Protection
    if (spec.enclosed) {
        panels.push(`
            <div class="specialized-panel">
                <div class="specialized-panel-header">🛡️ Armored Enclosure & Pawn Protection</div>
                <div class="detail-grid" style="margin-bottom: 0;">
                    <div class="data-row"><span class="data-label">Bullet Protection:</span><span class="data-val" style="color: var(--accent-emerald);">${spec.enclosed.bulletProtection}%</span></div>
                    <div class="data-row"><span class="data-label">Explosive Protection:</span><span class="data-val" style="color: var(--accent-emerald);">${spec.enclosed.explosiveProtection}%</span></div>
                    <div class="data-row"><span class="data-label">Temperature Protection:</span><span class="data-val">${spec.enclosed.temperatureProtection}%</span></div>
                    <div class="data-row"><span class="data-label">Operator Status:</span><span class="data-val">${spec.enclosed.hidePawnGraphics ? 'Concealed Inside Turret' : 'Exposed Graphics'}</span></div>
                </div>
            </div>
        `);
    }

    // 2. CIWS Air Defense
    if (spec.ciws) {
        panels.push(`
            <div class="specialized-panel" style="border-color: rgba(244, 63, 94, 0.4);">
                <div class="specialized-panel-header" style="color: var(--accent-rose);">🎯 CIWS Air Defense & Interception System</div>
                <div class="detail-grid" style="margin-bottom: 0;">
                    <div class="data-row"><span class="data-label">Air Defense Capability:</span><span class="data-val" style="color: var(--accent-rose);">Active Anti-Projectile & Drop Pod Interceptor</span></div>
                    <div class="data-row"><span class="data-label">Interception Range:</span><span class="data-val" style="color: var(--accent-cyan);">${spec.ciws.interceptionRange} cells</span></div>
                    <div class="data-row"><span class="data-label">Interception Burst Count:</span><span class="data-val">${spec.ciws.burstCount} rounds</span></div>
                    <div class="data-row"><span class="data-label">Interception Firing Speed:</span><span class="data-val">${spec.ciws.ticksBetweenShots} tick/shot</span></div>
                </div>
            </div>
        `);
    }

    // 3. Variable RPM & Rotary Fire Control
    if (spec.variableRpm) {
        const burstsStr = (spec.variableRpm.selectableBurstCounts || []).join(' / ') || 'Standard';
        const rpmsStr = (spec.variableRpm.maxRPMs || []).join(' / ') || 'Dynamic XML Governed';
        panels.push(`
            <div class="specialized-panel">
                <div class="specialized-panel-header">⚡ Rotary Drive & Variable Fire Control</div>
                <div class="detail-grid" style="margin-bottom: 0;">
                    <div class="data-row"><span class="data-label">Selectable Burst Sizes:</span><span class="data-val" style="color: var(--accent-amber);">${burstsStr} rounds</span></div>
                    <div class="data-row"><span class="data-label">Selectable RPM Tiers:</span><span class="data-val">${rpmsStr} RPM</span></div>
                    <div class="data-row"><span class="data-label">Spin-Down Animation Duration:</span><span class="data-val">${spec.variableRpm.spinDownTime || 0} seconds</span></div>
                </div>
            </div>
        `);
    }

    // 4. Turret Clamping & Non-Snap Rotation
    if (spec.clamping || spec.nonSnapRotation) {
        const clamp = spec.clamping || {};
        const rot = spec.nonSnapRotation || {};
        panels.push(`
            <div class="specialized-panel">
                <div class="specialized-panel-header">🧭 Turret Clamping & Rotation Constraints</div>
                <div class="detail-grid" style="margin-bottom: 0;">
                    <div class="data-row"><span class="data-label">Min Elevation Angle:</span><span class="data-val">${clamp.minElevationAngle || 0}°</span></div>
                    <div class="data-row"><span class="data-label">Max Vertical Deviation:</span><span class="data-val">${clamp.maxVerticalDeviation || 0}°</span></div>
                    <div class="data-row"><span class="data-label">Max Rotation Deviation:</span><span class="data-val">${clamp.maxRotationDeviation || 0}°</span></div>
                    <div class="data-row"><span class="data-label">Smooth Turning Speed:</span><span class="data-val">${rot.turnSpeedDegreesPerSec ? rot.turnSpeedDegreesPerSec.toFixed(1) + '°/sec' : 'Instant / Standard'}</span></div>
                </div>
            </div>
        `);
    }

    // 5. Smart Autoloader & Spray Discipline
    if (spec.ammoPreservation) {
        const ap = spec.ammoPreservation;
        panels.push(`
            <div class="specialized-panel">
                <div class="specialized-panel-header">🔄 Smart Autoloader & Targeting Discipline</div>
                <div class="detail-grid" style="margin-bottom: 0;">
                    <div class="data-row"><span class="data-label">Ammo Retention:</span><span class="data-val" style="color: var(--accent-emerald);">${ap.preserveAmmo ? 'Active (Retains unfired rounds when target dies)' : 'Disabled'}</span></div>
                    <div class="data-row"><span class="data-label">Shots per Target Cycle:</span><span class="data-val">${ap.shotsPerTarget || 'Standard'}</span></div>
                    <div class="data-row"><span class="data-label">Mid-Burst Tracking:</span><span class="data-val">${ap.enableMidBurstTracking ? 'Enabled' : 'Disabled'}</span></div>
                </div>
            </div>
        `);
    }

    if (panels.length === 0) {
        container.innerHTML = `<div class="empty-state"><p>This turret uses standard firing mechanics without specialized sub-systems.</p></div>`;
    } else {
        container.innerHTML = panels.join('');
    }
}
