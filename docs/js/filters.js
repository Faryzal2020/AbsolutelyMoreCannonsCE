/* ==========================================================================
   AMC-CE Web Wiki - Filters & Search State Module
   ========================================================================== */

export const DEFAULT_SORT = 'caliber-asc';

let activeCategory = 'all';
let activeFeatures = new Set();
let searchQuery = '';
let activeSort = DEFAULT_SORT;

/**
 * Longest range the turret can reach in any fire mode. Dual-mode turrets are
 * frequently short-ranged in direct fire and cross-map capable in indirect,
 * so a single-mode lookup would rank them far below their real reach.
 */
function getMaxRange(turret) {
    return [turret.modes?.direct, turret.modes?.indirect]
        .reduce((max, mode) => Math.max(max, mode?.verb?.range || 0), 0);
}

/**
 * Bore diameter in millimetres. The dataset is uniformly "<n>mm" today, but
 * centimetre and inch spellings are accepted so a future entry cannot be
 * silently sorted by a raw number in the wrong unit.
 */
const CALIBER_UNIT_TO_MM = { mm: 1, cm: 10, in: 25.4, '"': 25.4 };

function getCaliberMm(turret) {
    const match = /([\d.]+)\s*(mm|cm|in|")?/i.exec(turret.caliber || '');
    if (!match) {
        return 0;
    }

    const value = parseFloat(match[1]);
    if (!Number.isFinite(value)) {
        return 0;
    }

    return value * (CALIBER_UNIT_TO_MM[(match[2] || 'mm').toLowerCase()] || 1);
}

const byName = (a, b) => a.label.localeCompare(b.label);

const SORTERS = {
    'caliber-asc': (a, b) => getCaliberMm(a) - getCaliberMm(b) || byName(a, b),
    'caliber-desc': (a, b) => getCaliberMm(b) - getCaliberMm(a) || byName(a, b),
    'name': byName,
    'category': (a, b) => a.category.localeCompare(b.category) || byName(a, b),
    'hp-desc': (a, b) => b.common.maxHitPoints - a.common.maxHitPoints || byName(a, b),
    'hp-asc': (a, b) => a.common.maxHitPoints - b.common.maxHitPoints || byName(a, b),
    'range-desc': (a, b) => getMaxRange(b) - getMaxRange(a) || byName(a, b),
    'range-asc': (a, b) => getMaxRange(a) - getMaxRange(b) || byName(a, b),
    'cost-desc': (a, b) => b.common.workToBuild - a.common.workToBuild || byName(a, b),
    'cost-asc': (a, b) => a.common.workToBuild - b.common.workToBuild || byName(a, b)
};

export function setCategory(category) {
    activeCategory = category || 'all';
}

export function getCategory() {
    return activeCategory;
}

export function toggleFeature(feature) {
    if (activeFeatures.has(feature)) {
        activeFeatures.delete(feature);
    } else {
        activeFeatures.add(feature);
    }
    return activeFeatures.has(feature);
}

export function setFeatures(features) {
    activeFeatures = new Set((features || []).filter(Boolean));
}

export function getFeatures() {
    return [...activeFeatures];
}

export function setSearchQuery(query) {
    searchQuery = (query || '').trim().toLowerCase();
}

export function getSearchQuery() {
    return searchQuery;
}

export function setSort(sortKey) {
    activeSort = SORTERS[sortKey] ? sortKey : DEFAULT_SORT;
}

export function getSort() {
    return activeSort;
}

export function resetFilters() {
    activeCategory = 'all';
    activeFeatures.clear();
    searchQuery = '';
}

/**
 * Number of narrowing filters currently applied (sort is not a filter).
 */
export function activeFilterCount() {
    return (activeCategory !== 'all' ? 1 : 0) + activeFeatures.size + (searchQuery ? 1 : 0);
}

/**
 * Filter turrets array according to active search, category, features, and sort.
 */
export function filterTurrets(turrets) {
    const result = turrets.filter(turret => {
        // 1. Category Filter
        if (activeCategory !== 'all' && turret.category !== activeCategory) {
            return false;
        }

        // 2. Feature Pills Filter (All selected features must be present)
        if (activeFeatures.size > 0) {
            for (const feature of activeFeatures) {
                if (!turret.badges || !turret.badges.includes(feature)) {
                    return false;
                }
            }
        }

        // 3. Search Query Filter (Matches Name, Caliber, Subcategory, Description, Ammo names)
        if (searchQuery) {
            const nameMatch = turret.label.toLowerCase().includes(searchQuery);
            const caliberMatch = turret.caliber.toLowerCase().includes(searchQuery);
            const categoryMatch = turret.category.toLowerCase().includes(searchQuery);
            const descMatch = turret.description.toLowerCase().includes(searchQuery);

            const ammoMatch = turret.ammunition && turret.ammunition.some(a =>
                a.label.toLowerCase().includes(searchQuery) ||
                a.ammoClass.toLowerCase().includes(searchQuery)
            );

            if (!nameMatch && !caliberMatch && !categoryMatch && !descMatch && !ammoMatch) {
                return false;
            }
        }

        return true;
    });

    // 4. Sort Output
    result.sort(SORTERS[activeSort] || SORTERS[DEFAULT_SORT]);

    return result;
}
