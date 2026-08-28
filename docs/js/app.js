/* ==========================================================================
   AMC-CE Web Wiki - Main Application Module
   ========================================================================== */

import { loadTurretsData, getTurrets } from './dataLoader.js';
import { setCategory, toggleFeature, setSearchQuery, setSort, filterTurrets } from './filters.js';
import { createTurretCard } from './turretCard.js';
import { openDetailModal, closeDetailModal, setActiveTab } from './detailView.js';

let allTurrets = [];

// Initialize Application on DOM Ready
document.addEventListener('DOMContentLoaded', async () => {
    initApp();
});

async function initApp() {
    const { turrets, metadata } = await loadTurretsData();
    allTurrets = turrets;

    // Attach Event Listeners
    setupSearchListeners();
    setupCategoryFilterListeners();
    setupFeatureFilterListeners();
    setupSortListeners();
    setupModalListeners();

    // Initial Render
    renderGrid();
}

/**
 * Filter dataset and render turret cards in #turret-grid
 */
function renderGrid() {
    const grid = document.getElementById('turret-grid');
    const emptyState = document.getElementById('no-results');
    const counter = document.getElementById('turret-counter');

    const filtered = filterTurrets(allTurrets);

    // Update Counter
    counter.innerHTML = `Showing <strong>${filtered.length}</strong> of <strong>${allTurrets.length}</strong> turrets`;

    grid.innerHTML = '';

    if (filtered.length === 0) {
        emptyState.style.display = 'block';
    } else {
        emptyState.style.display = 'none';
        filtered.forEach(turret => {
            const cardElem = createTurretCard(turret, (selectedTurret) => {
                openDetailModal(selectedTurret);
            });
            grid.appendChild(cardElem);
        });
    }
}

/**
 * Search input listeners
 */
function setupSearchListeners() {
    const searchInput = document.getElementById('search-input');
    const clearBtn = document.getElementById('clear-search');

    searchInput.addEventListener('input', (e) => {
        const val = e.target.value;
        clearBtn.style.display = val ? 'block' : 'none';
        setSearchQuery(val);
        renderGrid();
    });

    clearBtn.addEventListener('click', () => {
        searchInput.value = '';
        clearBtn.style.display = 'none';
        setSearchQuery('');
        renderGrid();
    });
}

/**
 * Sub-category pill button listeners
 */
function setupCategoryFilterListeners() {
    const pills = document.querySelectorAll('#category-pills .pill');
    pills.forEach(pill => {
        pill.addEventListener('click', () => {
            pills.forEach(p => p.classList.remove('active'));
            pill.classList.add('active');
            setCategory(pill.dataset.category);
            renderGrid();
        });
    });
}

/**
 * Feature pill button listeners
 */
function setupFeatureFilterListeners() {
    const pills = document.querySelectorAll('#feature-pills .pill');
    pills.forEach(pill => {
        pill.addEventListener('click', () => {
            const isActive = toggleFeature(pill.dataset.feature);
            pill.classList.toggle('active', isActive);
            renderGrid();
        });
    });
}

/**
 * Sort dropdown listener
 */
function setupSortListeners() {
    const sortSelect = document.getElementById('sort-select');
    sortSelect.addEventListener('change', (e) => {
        setSort(e.target.value);
        renderGrid();
    });
}

/**
 * Modal dialog listeners & keyboard ESC shortcut
 */
function setupModalListeners() {
    const closeBtn = document.getElementById('modal-close');
    const backdrop = document.getElementById('detail-modal');

    closeBtn.addEventListener('click', closeDetailModal);

    backdrop.addEventListener('click', (e) => {
        if (e.target === backdrop) {
            closeDetailModal();
        }
    });

    // Keyboard ESC listener
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeDetailModal();
        }
    });

    // Tab buttons inside modal
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            setActiveTab(btn.dataset.tab);
        });
    });
}
