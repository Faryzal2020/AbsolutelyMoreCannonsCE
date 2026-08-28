/* ==========================================================================
   AMC-CE Web Wiki - Ammunition & Shell Breakdown Component
   ========================================================================== */

/**
 * Render Tab 4: Ammunition Cards & Comparative Matrix Table
 */
export function renderAmmoTab(turret, activeModeKey) {
    const container = document.getElementById('tab-ammo');
    const ammunitions = turret.ammunition || [];

    if (ammunitions.length === 0) {
        container.innerHTML = `<div class="empty-state"><p>No ammunition definitions linked to this turret.</p></div>`;
        return;
    }

    // 1. Render Ammunition Cards Grid
    const cardsHtml = ammunitions.map(ammo => createAmmoCardHtml(ammo)).join('');

    // 2. Render Comparative Shell Matrix Table
    const tableHtml = createAmmoMatrixTableHtml(ammunitions);

    container.innerHTML = `
        <div class="ammo-grid">
            ${cardsHtml}
        </div>

        <div style="margin-top: 2rem;">
            <h3 style="font-size: 1.1rem; color: var(--accent-cyan); margin-bottom: 0.75rem; font-family: var(--font-heading);">
                📊 Comparative Shell Matrix Table
            </h3>
            ${tableHtml}
        </div>
    `;
}

/**
 * Generate HTML string for an individual ammo card
 */
function createAmmoCardHtml(ammo) {
    const p = ammo.projectile || {};
    const recipe = ammo.recipe || {};

    // AP Badges
    const sharpApStr = p.armorPenetrationSharp ? `${p.armorPenetrationSharp} mm RHA` : null;
    const bluntApStr = p.armorPenetrationBlunt ? `${p.armorPenetrationBlunt.toFixed(0)} MPa` : null;

    // Secondary Explosive Details
    let secExplosiveHtml = '';
    if (p.secondaryExplosive) {
        secExplosiveHtml = `
            <div class="data-row"><span class="data-label">Secondary Explosive:</span><span class="data-val" style="color: var(--accent-rose);">${p.secondaryExplosive.damage} ${p.secondaryExplosive.damageDef} (R: ${p.secondaryExplosive.radius}m)</span></div>
        `;
    }

    // Fragment Details
    let fragHtml = '';
    if (p.fragments && p.fragments.fragmentList) {
        const fragsStr = p.fragments.fragmentList.map(f => `${f.count}x ${f.type.replace('Fragment_', '')}`).join(', ');
        fragHtml = `
            <div class="data-row"><span class="data-label">Shrapnel Fragments:</span><span class="data-val">${fragsStr}</span></div>
        `;
    }

    // Airburst Details
    let airburstHtml = '';
    if (p.airburst) {
        airburstHtml = `
            <div class="data-row"><span class="data-label">Airburst System:</span><span class="data-val" style="color: var(--accent-amber);">${p.airburst.type || 'Proximity'} (Radius: ${p.airburst.proximityRadius || 0}m, Arm: ${p.airburst.armingTicks || 0}t)</span></div>
        `;
    }

    // Guided Details
    let guidedHtml = '';
    if (p.guided) {
        guidedHtml = `
            <div class="data-row"><span class="data-label">Smart Trajectory:</span><span class="data-val" style="color: var(--accent-cyan);">Guided (Accel: ${p.guided.homingAcceleration}, Retarget: ${p.guided.retargetRadius}m)</span></div>
        `;
    }

    // Shelling Details
    let shellingHtml = '';
    if (p.shellingProps) {
        shellingHtml = `
            <div class="data-row"><span class="data-label">Cross-Map Shelling:</span><span class="data-val" style="color: var(--accent-rose);">Max ${p.shellingProps.range} tiles (${p.shellingProps.tilesPerTick} t/tick)</span></div>
        `;
    }

    // Recipe Ingredients
    let ingredientsHtml = 'None';
    if (recipe.ingredients && recipe.ingredients.length > 0) {
        ingredientsHtml = recipe.ingredients.map(ing => `${ing.count}x ${ing.item}`).join(', ');
    }
    const yieldCount = recipe.products ? Object.values(recipe.products)[0] || 1 : 1;

    return `
        <article class="ammo-card">
            <header class="ammo-card-header">
                <div class="ammo-title-group">
                    <h4 class="ammo-name">${ammo.label}</h4>
                    <span class="ammo-class-tag">${ammo.ammoClass}</span>
                </div>
                <div class="ap-badges">
                    ${sharpApStr ? `<span class="ap-tag ap-sharp">Sharp: ${sharpApStr}</span>` : ''}
                    ${bluntApStr ? `<span class="ap-tag ap-blunt">Blunt: ${bluntApStr}</span>` : ''}
                </div>
            </header>

            <div class="ammo-stats-box">
                <div class="stat-item">
                    <span class="stat-label">Velocity</span>
                    <span class="stat-value" style="color: var(--accent-cyan);">${p.speed || 0} m/s</span>
                </div>
                <div class="stat-item">
                    <span class="stat-label">Direct Damage</span>
                    <span class="stat-value">${p.damageAmountBase || 0} ${p.damageDef || ''}</span>
                </div>
                <div class="stat-item">
                    <span class="stat-label">Blast Radius</span>
                    <span class="stat-value">${p.explosionRadius ? p.explosionRadius + ' m' : 'Direct'}</span>
                </div>
            </div>

            <div style="font-size: 0.85rem;">
                ${secExplosiveHtml}
                ${fragHtml}
                ${airburstHtml}
                ${guidedHtml}
                ${shellingHtml}
                <div class="data-row"><span class="data-label">Market Value:</span><span class="data-val">$${ammo.marketValue || 0} / round</span></div>
                <div class="data-row"><span class="data-label">Mass & Bulk:</span><span class="data-val">${ammo.mass} kg | ${ammo.bulk} bulk</span></div>
            </div>

            <div class="ammo-recipe-box">
                <div class="recipe-title">🔨 Crafting Economics (Batch Yield: x${yieldCount})</div>
                <div style="color: var(--text-secondary); margin-bottom: 0.25rem;">
                    <strong>Work Amount:</strong> ${recipe.workAmount ? recipe.workAmount.toLocaleString() : 'N/A'} ticks
                </div>
                <div style="color: var(--text-secondary);">
                    <strong>Ingredients:</strong> ${ingredientsHtml}
                </div>
            </div>
        </article>
    `;
}

/**
 * Generate Comparative Shell Matrix Table HTML
 */
function createAmmoMatrixTableHtml(ammunitions) {
    const rowsHtml = ammunitions.map(ammo => {
        const p = ammo.projectile || {};
        const recipe = ammo.recipe || {};
        const yieldCount = recipe.products ? Object.values(recipe.products)[0] || 1 : 1;
        const workPerRound = recipe.workAmount ? Math.round(recipe.workAmount / yieldCount) : 0;

        return `
            <tr>
                <td><strong>${ammo.label}</strong></td>
                <td><span class="badge badge-cyan">${ammo.ammoClass}</span></td>
                <td>${p.speed || 0} m/s</td>
                <td><strong>${p.damageAmountBase || 0}</strong> ${p.damageDef || ''}</td>
                <td><span style="color: var(--accent-rose); font-weight: 600;">${p.armorPenetrationSharp ? p.armorPenetrationSharp + ' mm' : '-'}</span></td>
                <td><span style="color: var(--accent-amber);">${p.armorPenetrationBlunt ? p.armorPenetrationBlunt.toFixed(0) + ' MPa' : '-'}</span></td>
                <td>${p.explosionRadius ? p.explosionRadius + ' m' : '-'}</td>
                <td>$${ammo.marketValue || 0}</td>
                <td>${workPerRound ? workPerRound + ' t/rnd' : '-'}</td>
            </tr>
        `;
    }).join('');

    return `
        <div class="ammo-matrix-wrapper">
            <table class="ammo-matrix-table">
                <thead>
                    <tr>
                        <th>Shell Variant</th>
                        <th>Class</th>
                        <th>Muzzle Velocity</th>
                        <th>Direct Damage</th>
                        <th>Sharp AP</th>
                        <th>Blunt AP</th>
                        <th>Blast Radius</th>
                        <th>Market Value</th>
                        <th>Crafting Work</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml}
                </tbody>
            </table>
        </div>
    `;
}
