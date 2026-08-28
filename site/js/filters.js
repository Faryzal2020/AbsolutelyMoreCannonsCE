/* ==========================================================================
   AMC-CE Web Wiki - Filters & Search State Module
   ========================================================================== */

let activeCategory = 'all';
let activeFeatures = new Set();
let searchQuery = '';
let activeSort = 'name';

export function setCategory(category) {
    activeCategory = category;
}

export function toggleFeature(feature) {
    if (activeFeatures.has(feature)) {
        activeFeatures.delete(feature);
    } else {
        activeFeatures.add(feature);
    }
    return activeFeatures.has(feature);
}

export function setSearchQuery(query) {
    searchQuery = query.trim().toLowerCase();
}

export function setSort(sortKey) {
    activeSort = sortKey;
}

/**
 * Filter turrets array according to active search, category, features, and sort.
 */
export function filterTurrets(turrets) {
    let result = turrets.filter(turret => {
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
    result.sort((a, b) => {
        if (activeSort === 'name') {
            return a.label.localeCompare(b.label);
        } else if (activeSort === 'category') {
            return a.category.localeCompare(b.category) || a.label.localeCompare(b.label);
        } else if (activeSort === 'hp') {
            return b.common.maxHitPoints - a.common.maxHitPoints;
        } else if (activeSort === 'range') {
            const rangeA = (a.modes.direct || a.modes.indirect)?.verb?.range || 0;
            const rangeB = (b.modes.direct || b.modes.indirect)?.verb?.range || 0;
            return rangeB - rangeA;
        } else if (activeSort === 'cost') {
            return b.common.workToBuild - a.common.workToBuild;
        }
        return 0;
    });

    return result;
}
