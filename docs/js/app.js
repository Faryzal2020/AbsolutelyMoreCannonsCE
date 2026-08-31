/* ==========================================================================
   AMC-CE Web Wiki - Main Application Module
   ========================================================================== */

import { loadTurretsData } from './dataLoader.js';
import {
    setCategory, getCategory,
    toggleFeature, setFeatures, getFeatures,
    setSearchQuery, getSearchQuery,
    setSort, getSort, DEFAULT_SORT,
    resetFilters, activeFilterCount, filterTurrets
} from './filters.js';
import { createTurretCard } from './turretCard.js';
import { openDetailModal, closeDetailModal, isModalOpen, setActiveTab } from './detailView.js';

const SEARCH_DEBOUNCE_MS = 150;
const MOBILE_QUERY = '(max-width: 900px)';

let allTurrets = [];
let searchTimer = null;
let renderedQuery = null;

document.addEventListener('DOMContentLoaded', initApp);

async function initApp() {
    const { turrets } = await loadTurretsData();
    allTurrets = turrets;

    setupSearchListeners();
    setupCategoryFilterListeners();
    setupFeatureFilterListeners();
    setupSortListeners();
    setupResetListener();
    setupFilterToggle();
    setupModalListeners();

    window.addEventListener('popstate', syncFromUrl);

    // Restore filters, sort and any deep-linked turret from the URL.
    readStateFromUrl();
    syncControlsToState();
    renderGrid();
    openTurretFromHash();
}

/* --------------------------------------------------------------------------
   Rendering
   -------------------------------------------------------------------------- */

function renderGrid() {
    const grid = document.getElementById('turret-grid');
    const emptyState = document.getElementById('no-results');
    const counter = document.getElementById('turret-counter');

    const filtered = filterTurrets(allTurrets);

    counter.innerHTML = `Showing <strong>${filtered.length}</strong> of <strong>${allTurrets.length}</strong> turrets`;

    const fragment = document.createDocumentFragment();
    filtered.forEach(turret => {
        fragment.appendChild(createTurretCard(turret, selectTurret));
    });

    grid.replaceChildren(fragment);
    emptyState.hidden = filtered.length > 0;
    renderedQuery = filterKey();

    updateFilterAffordances();
}

/**
 * Identity of the current filter set, used to skip rebuilding the grid when
 * navigation did not actually change it (closing the dialog via Back, say —
 * a needless rebuild would detach the card the dialog must return focus to).
 */
function filterKey() {
    return JSON.stringify([getCategory(), getFeatures(), getSearchQuery(), getSort()]);
}

/**
 * Keep the reset button and the mobile filter toggle in step with filter state.
 */
function updateFilterAffordances() {
    const count = activeFilterCount();

    const resetBtn = document.getElementById('reset-filters');
    resetBtn.hidden = count === 0;

    const toggle = document.getElementById('filter-toggle');
    const label = toggle.querySelector('.filter-toggle-count');
    label.textContent = count > 0 ? ` (${count})` : '';
}

/* --------------------------------------------------------------------------
   URL state — filters and sort live in the query string, the open turret in
   the hash, so every view is linkable and Back closes the dialog.
   -------------------------------------------------------------------------- */

function buildUrl(hash = '') {
    const params = new URLSearchParams();

    if (getCategory() !== 'all') params.set('cat', getCategory());
    if (getFeatures().length) params.set('feat', getFeatures().join('|'));
    if (getSearchQuery()) params.set('q', getSearchQuery());
    if (getSort() !== DEFAULT_SORT) params.set('sort', getSort());

    const query = params.toString();
    return `${location.pathname}${query ? `?${query}` : ''}${hash}`;
}

function readStateFromUrl() {
    const params = new URLSearchParams(location.search);
    setCategory(params.get('cat') || 'all');
    setFeatures((params.get('feat') || '').split('|').filter(Boolean));
    setSearchQuery(params.get('q') || '');
    setSort(params.get('sort'));
}

function turretIdFromHash() {
    return location.hash.startsWith('#turret-') ? location.hash.slice('#turret-'.length) : '';
}

/**
 * Push filter state into the URL without adding a history entry — only opening
 * a turret should create one.
 */
function commitFilterState() {
    history.replaceState(null, '', buildUrl());
    renderGrid();
}

function syncControlsToState() {
    document.querySelectorAll('#category-pills .pill').forEach(pill => {
        const active = pill.dataset.category === getCategory();
        pill.classList.toggle('active', active);
        pill.setAttribute('aria-pressed', String(active));
    });

    const features = getFeatures();
    document.querySelectorAll('#feature-pills .pill').forEach(pill => {
        const active = features.includes(pill.dataset.feature);
        pill.classList.toggle('active', active);
        pill.setAttribute('aria-pressed', String(active));
    });

    const searchInput = document.getElementById('search-input');
    searchInput.value = getSearchQuery();
    document.getElementById('clear-search').hidden = !getSearchQuery();

    document.getElementById('sort-select').value = getSort();
}

/**
 * Re-apply whatever the current URL describes (fired on Back / Forward).
 */
function syncFromUrl() {
    readStateFromUrl();
    syncControlsToState();

    if (filterKey() !== renderedQuery) {
        renderGrid();
    } else {
        updateFilterAffordances();
    }

    const id = turretIdFromHash();
    const turret = id ? allTurrets.find(t => t.id === id) : null;

    if (turret) {
        openDetailModal(turret);
    } else {
        closeDetailModal();
    }
}

function openTurretFromHash() {
    const id = turretIdFromHash();
    const turret = id ? allTurrets.find(t => t.id === id) : null;
    if (turret) {
        openDetailModal(turret);
    }
}

/* --------------------------------------------------------------------------
   Detail dialog
   -------------------------------------------------------------------------- */

function selectTurret(turret) {
    history.pushState({ turretId: turret.id }, '', buildUrl(`#turret-${turret.id}`));
    openDetailModal(turret);
}

function dismissTurret() {
    if (!isModalOpen()) {
        return;
    }

    closeDetailModal();

    if (history.state && history.state.turretId) {
        history.back();
    } else {
        history.replaceState(null, '', buildUrl());
    }
}

/* --------------------------------------------------------------------------
   Listeners
   -------------------------------------------------------------------------- */

function setupSearchListeners() {
    const searchInput = document.getElementById('search-input');
    const clearBtn = document.getElementById('clear-search');

    searchInput.addEventListener('input', (e) => {
        const value = e.target.value;
        clearBtn.hidden = !value;

        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => {
            setSearchQuery(value);
            commitFilterState();
        }, SEARCH_DEBOUNCE_MS);
    });

    // Escape clears the query while the field has focus, rather than falling
    // through to the dialog handler.
    searchInput.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && searchInput.value) {
            e.stopPropagation();
            clearSearch();
        }
    });

    clearBtn.addEventListener('click', clearSearch);
}

function clearSearch() {
    const searchInput = document.getElementById('search-input');
    clearTimeout(searchTimer);
    searchInput.value = '';
    document.getElementById('clear-search').hidden = true;
    setSearchQuery('');
    commitFilterState();
    searchInput.focus();
}

function setupCategoryFilterListeners() {
    document.querySelectorAll('#category-pills .pill').forEach(pill => {
        pill.addEventListener('click', () => {
            setCategory(pill.dataset.category);
            syncControlsToState();
            commitFilterState();
        });
    });
}

function setupFeatureFilterListeners() {
    document.querySelectorAll('#feature-pills .pill').forEach(pill => {
        pill.addEventListener('click', () => {
            const isActive = toggleFeature(pill.dataset.feature);
            pill.classList.toggle('active', isActive);
            pill.setAttribute('aria-pressed', String(isActive));
            commitFilterState();
        });
    });
}

function setupSortListeners() {
    document.getElementById('sort-select').addEventListener('change', (e) => {
        setSort(e.target.value);
        commitFilterState();
    });
}

function setupResetListener() {
    document.getElementById('reset-filters').addEventListener('click', () => {
        resetFilters();
        syncControlsToState();
        commitFilterState();
        document.getElementById('search-input').focus();
    });
}

/**
 * The filter bar is tall enough to swallow most of a phone screen, so it
 * collapses behind a toggle below the tablet breakpoint.
 */
function setupFilterToggle() {
    const toggle = document.getElementById('filter-toggle');
    const bar = document.getElementById('filter-bar');
    const mobile = window.matchMedia(MOBILE_QUERY);

    const setExpanded = (expanded) => {
        toggle.setAttribute('aria-expanded', String(expanded));
        bar.classList.toggle('collapsed', !expanded);
    };

    setExpanded(!mobile.matches);
    mobile.addEventListener('change', (e) => setExpanded(!e.matches));

    toggle.addEventListener('click', () => {
        setExpanded(toggle.getAttribute('aria-expanded') !== 'true');
    });
}

function setupModalListeners() {
    const backdrop = document.getElementById('detail-modal');

    document.getElementById('modal-close').addEventListener('click', dismissTurret);

    backdrop.addEventListener('click', (e) => {
        if (e.target === backdrop) {
            dismissTurret();
        }
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && isModalOpen()) {
            dismissTurret();
        }
    });

    // Tab strip: click plus arrow-key roving focus.
    const tabs = [...document.querySelectorAll('.tab-btn')];
    tabs.forEach((btn, index) => {
        btn.addEventListener('click', () => setActiveTab(btn.dataset.tab));

        btn.addEventListener('keydown', (e) => {
            const offset = e.key === 'ArrowRight' ? 1 : e.key === 'ArrowLeft' ? -1 : 0;
            if (!offset) return;

            e.preventDefault();
            const next = tabs[(index + offset + tabs.length) % tabs.length];
            setActiveTab(next.dataset.tab);
            next.focus();
        });
    });
}
