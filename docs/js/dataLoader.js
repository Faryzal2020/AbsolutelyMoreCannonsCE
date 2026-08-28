/* ==========================================================================
   AMC-CE Web Wiki - Data Loader Module
   ========================================================================== */

let turretsData = [];
let metadata = {};

/**
 * Fetch and load turrets database from site/data/turrets_data.json
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
 * Helper to generate thumbnail HTML frame with image fallback
 */
export function getThumbnailHtml(imageUrl, caliber, title) {
    if (imageUrl) {
        return `<img src="${imageUrl}" alt="${title}" class="card-thumb-img" onerror="this.outerHTML='<div class=\\'placeholder-thumb\\'><span class=\\'placeholder-icon\\'>🛡️</span><span class=\\'placeholder-caliber\\'>${caliber}</span></div>'">`;
    }
    return `
        <div class="placeholder-thumb">
            <span class="placeholder-icon">🎯</span>
            <span class="placeholder-caliber">${caliber}</span>
        </div>
    `;
}
