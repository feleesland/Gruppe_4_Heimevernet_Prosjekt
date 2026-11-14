// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Search in the menu (_Layout) like YouTube/Google Maps: suggestions while typing (our resources + places from Kartverket),
// Enter goes to Kart, ✕ clears. The filter panel (resource-filter.js) uses the same search and filter, kept in the URL, so Kart and Ressursoversikt show the same (help with AI)
(function () {
    const form = document.getElementById('globalSearchForm');
    if (!form) {
        return; // not logged in: the info search below is used instead
    }
    const input = document.getElementById('globalSearchWhat');
    const clearButton = document.getElementById('globalSearchClear');
    const list = document.getElementById('globalSearchSuggestions');
    const status = document.getElementById('globalSearchStatus');

    const defaultRadius = 2; // km
    const path = window.location.pathname.toLowerCase();
    const onListPage = path.startsWith('/resource/list');
    const onMapPage = path.startsWith('/home/map');

    // Remember the search/filter in this browser tab, so it follows between Kart and Ressursoversikt until Nullstill (help with AI)
    // Docs: Window.sessionStorage (MDN) - https://developer.mozilla.org/docs/Web/API/Window/sessionStorage
    const storageKey = 'resourceFilter';
    function saveFilter(params) {
        try { sessionStorage.setItem(storageKey, JSON.stringify(params)); } catch { /* storage blocked: the filter just isn't remembered */ }
    }
    function loadFilter() {
        try { return JSON.parse(sessionStorage.getItem(storageKey) || '{}'); } catch { return {}; }
    }

    // The menu links to Kart and Ressursoversikt get the saved filter added
    function updateMenuLinks() {
        const query = new URLSearchParams(loadFilter()).toString();
        document.querySelectorAll('#mainNavigation a.nav-link').forEach(a => {
            const linkPath = a.pathname.toLowerCase();
            if (linkPath === '/resource/list' || linkPath === '/home/map') {
                a.search = query === '' ? '' : '?' + query;
            }
        });
    }

    // The current search and filter: from the URL on Kart/Ressursoversikt, otherwise the saved one
    let current = (onListPage || onMapPage)
        ? Object.fromEntries(new URLSearchParams(window.location.search))
        : loadFilter();
    delete current.mine; // "Mine" is only a choice on Ressursoversikt, not part of the saved filter
    if (onListPage || onMapPage) {
        saveFilter(current);
    }

    let timer = null;
    let controller = null;
    let items = [];
    let active = -1;
    let lastData = null; // the last suggestions, so Enter can use them

    // ---------- Search ----------

    // ✕ only shows when there is text in the field
    function showClear() {
        clearButton.classList.toggle('d-none', input.value === '');
    }

    function closeList() {
        list.classList.add('d-none');
        list.replaceChildren();
        items = [];
        active = -1;
    }

    // Removes empty values, updates the map in place on Kart, otherwise goes to the page
    function goTo(params, page) {
        Object.keys(params).forEach(k => { if (params[k] === '' || params[k] == null) delete params[k]; });
        current = params;
        saveFilter(params);
        updateMenuLinks();
        const query = new URLSearchParams(params).toString();
        if (typeof window.onResourceSearch === 'function' && page !== form.action) {
            window.onResourceSearch(params);
            history.replaceState(null, '', window.location.pathname + (query === '' ? '' : '?' + query));
        } else {
            window.location.href = page + (query === '' ? '' : '?' + query);
        }
    }

    // The filter panel (resource-filter.js) reads and changes the same search and filter as the menu
    window.getResourceParams = function () { return { ...current }; };
    window.applyResourceParams = function (params) { goTo(params, window.location.pathname); };

    function addSection(title) {
        const div = document.createElement('div');
        div.className = 'search-section';
        div.textContent = title;
        list.appendChild(div);
    }

    // One row in the list; textContent is used so text from users can't run as HTML
    function addItem(icon, title, subtitle, onSelect) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'search-item';

        const iconSpan = document.createElement('span');
        iconSpan.className = 'search-item-icon';
        iconSpan.textContent = icon;

        const text = document.createElement('span');
        const titleSpan = document.createElement('span');
        titleSpan.textContent = title;
        const small = document.createElement('small');
        small.textContent = subtitle;
        text.append(titleSpan, document.createElement('br'), small);

        button.append(iconSpan, text);
        button.addEventListener('click', function () {
            closeList();
            onSelect();
        });
        list.appendChild(button);
        items.push(button);
    }

    // A resource: on Kart the map flies to it, otherwise open the resource page
    function selectResource(resource) {
        input.value = resource.name;
        showClear();
        if (typeof window.onResourceSelected === 'function') {
            window.onResourceSelected(resource);
        } else {
            window.location.href = form.dataset.detailUrl + resource.id;
        }
    }

    // A place: the centre of the circle; the rest of the filter (types, status ...) is kept, the text search is not
    function selectPlace(place) {
        input.value = '';
        showClear();
        const params = { ...current, where: place.name, lat: place.latitude, lng: place.longitude, radius: current.radius || defaultRadius };
        delete params.search;
        goTo(params, form.dataset.mapUrl);
    }

    // Asks our server (SuggestionsController) for resources and places, and builds the list
    async function loadSuggestions() {
        const text = input.value.trim();
        if (text.length < 2) {
            closeList();
            return;
        }
        if (controller) {
            controller.abort(); // stop the previous request if the user is still typing
        }
        controller = new AbortController();
        try {
            const response = await fetch(form.dataset.suggestUrl + '?q=' + encodeURIComponent(text), { signal: controller.signal });
            const data = await response.json();
            lastData = data;
            list.replaceChildren();
            items = [];
            active = -1;

            if (data.resources.length > 0) {
                addSection('Ressurser');
                data.resources.forEach(r => addItem('📦', r.name, [r.type, r.address].filter(x => x).join(' · '), () => selectResource(r)));
            }
            if (data.places.length > 0) {
                addSection('Steder');
                data.places.forEach(p => addItem('📍', p.name, p.description, () => selectPlace(p)));
            }
            if (items.length === 0) {
                addSection('Ingen forslag – trykk Enter for å søke');
            }
            list.classList.remove('d-none');
        } catch (error) {
            if (error.name !== 'AbortError') {
                closeList();
            }
        }
    }

    // Wait until the user pauses typing (250 ms), so we don't ask the server for every letter
    input.addEventListener('input', function () {
        showClear();
        clearTimeout(timer);
        timer = setTimeout(loadSuggestions, 250);
    });

    // Arrow keys move in the list, Enter picks, Esc closes
    input.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeList();
            return;
        }
        if (items.length === 0) {
            return;
        }
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            active = (active + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length;
            items.forEach((b, i) => b.classList.toggle('active', i === active));
        } else if (event.key === 'Enter' && active >= 0) {
            event.preventDefault();
            items[active].click();
        }
    });

    // Enter without picking a suggestion, like Google Maps: a place (and no resource) -> fly there with the radius,
    // otherwise show the matching resources on the map (the filter is kept)
    form.addEventListener('submit', function (event) {
        event.preventDefault();
        const text = input.value.trim();
        const data = lastData;
        closeList();
        if (text !== '' && data && data.resources.length === 0 && data.places.length > 0) {
            selectPlace(data.places[0]);
        } else {
            const params = { ...current, search: text };
            goTo(params, onListPage ? form.action : form.dataset.mapUrl);
        }
    });

    // ✕ empties the field and removes the text search (the filter is kept)
    clearButton.addEventListener('click', function () {
        input.value = '';
        showClear();
        closeList();
        status.textContent = '';
        input.focus();
        if (current.search) {
            const params = { ...current };
            delete params.search;
            goTo(params, window.location.pathname);
        }
    });

    // Clicking outside the search closes the list
    document.addEventListener('click', function (event) {
        if (!form.contains(event.target)) {
            closeList();
        }
    });
 
        showClear();
        updateMenuLinks();
    })();

// Search in the menu when not logged in: finds and highlights text on the homepage (help with AI)
(function () {
    const form = document.getElementById('infoSearchForm');
    if (!form) {
        return; // logged in: the resource search above is used instead
    }
    const input = document.getElementById('infoSearchText');
    const status = document.getElementById('infoSearchStatus');
    const homePaths = ['/', '/home', '/home/index'];
    const onHomePage = homePaths.includes(window.location.pathname.toLowerCase());

    function searchPage(text) {
        const elements = document.querySelectorAll('main h1, main h2, main h3, main h4, main p, main li');
        elements.forEach(e => e.classList.remove('search-hit'));
        if (text === '') {
            status.textContent = '';
            return;
        }
        const hits = [...elements].filter(e => e.textContent.toLowerCase().includes(text.toLowerCase()));
        hits.forEach(e => e.classList.add('search-hit'));
        status.textContent = hits.length === 0 ? 'Ingen treff' : hits.length + ' treff';
        if (hits.length > 0) {
            hits[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    // On the homepage: search without reloading. On other pages the form goes to the homepage with ?q=
    form.addEventListener('submit', function (event) {
        if (onHomePage) {
            event.preventDefault();
            const text = input.value.trim();
            searchPage(text);
            history.replaceState(null, '', text === '' ? window.location.pathname : '?q=' + encodeURIComponent(text));
        }
    });

    // ✕ empties the field and removes the highlighting
    const clearButton = document.getElementById('infoSearchClear');
    input.addEventListener('input', () => clearButton.classList.toggle('d-none', input.value === ''));
    clearButton.addEventListener('click', function () {
        input.value = '';
        clearButton.classList.add('d-none');
        searchPage('');
        if (onHomePage) {
            history.replaceState(null, '', window.location.pathname);
        }
        input.focus();
    });
    clearButton.classList.toggle('d-none', input.value === '');

    // Coming from another page with ?q=: search straight away
    if (onHomePage) {
        searchPage(input.value.trim());
    }
})();