/* ==========================================================================
   AMC-CE Web Wiki - Data Loader Module
   ========================================================================== */

let turretsData = [];
let metadata = {};

/**
 * Fetch and load turrets database from data/turrets_data.json
 */
export async function loadTurretsData() {
    try {
        const response = await fetch('data/turrets_data.json');
        if (!response.ok) {
            throw new Error(`Failed to load turrets data (HTTP ${response.status})`);
        }
        const json = await response.json();
        turretsData = json.turrets || [];
        metadata = {
            generatedAt: json.generatedAt,
            totalTurrets: json.totalTurrets
        };
        return { turrets: turretsData, metadata };
    } catch (error) {
        console.error('DataLoader Error:', error);
        return { turrets: [], metadata: { error: error.message } };
    }
}

/**
 * Returns current dataset
 */
export function getTurrets() {
    return turretsData;
}

/**
 * Finds turret entry by ID or defName
 */
export function getTurretById(id) {
    return turretsData.find(t => t.id === id || t.defName === id);
}

/**
 * Escape a value before interpolating it into markup or an attribute.
 */
export function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

/**
 * Thumbnail markup: the image plus a hidden monotone placeholder that the
 * image's own error handler reveals. The handler touches DOM properties only,
 * so no nested quoting is needed inside the attribute.
 */
export function getThumbnailHtml(imageUrl, caliber, title) {
    const placeholder = `
        <div class="placeholder-thumb"${imageUrl ? ' hidden' : ''}>
            <span class="placeholder-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" aria-hidden="true">
                    <circle cx="12" cy="12" r="9"></circle>
                    <path d="M12 3v3M12 18v3M3 12h3M18 12h3"></path>
                </svg>
            </span>
            <span class="placeholder-caliber">${escapeHtml(caliber)}</span>
        </div>
    `;

    if (!imageUrl) {
        return placeholder;
    }

    return `
        <img src="${escapeHtml(imageUrl)}" alt="${escapeHtml(title)}" class="card-thumb-img"
             onerror="this.hidden=true;this.nextElementSibling.hidden=false">
        ${placeholder}
    `;
}
