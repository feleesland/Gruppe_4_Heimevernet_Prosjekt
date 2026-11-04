// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Search in the menu (_Layout): finds the place for Hvor? with Nominatim, then goes to Ressurser,
// or filters the map in place when the Kart page has set window.onResourceSearch (help with AI)
// Docs: Nominatim Search API - https://nominatim.org/release-docs/latest/api/Search/
(function () {
    const form = document.getElementById('globalSearchForm');
    if (!form) {
        return; // not logged in: the info search below is used instead
    }
    const status = document.getElementById('globalSearchStatus');

    form.addEventListener('submit', async function (event) {
        event.preventDefault();
        const what = document.getElementById('globalSearchWhat').value.trim();
        const where = document.getElementById('globalSearchWhere').value.trim();
        const radius = document.getElementById('globalSearchRadius').value;

        const params = {};
        if (what !== '') {
            params.search = what;
        }
        if (where !== '') {
            status.textContent = 'Søker etter stedet ...';
            try {
                const response = await fetch('https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=no&q=' + encodeURIComponent(where));
                const places = await response.json();
                if (places.length === 0) {
                    status.textContent = 'Fant ikke stedet.';
                    return;
                }
                params.where = where;
                params.lat = places[0].lat;
                params.lng = places[0].lon;
                params.radius = radius;
            } catch {
                status.textContent = 'Kunne ikke søke etter stedet.';
                return;
            }
        }
        status.textContent = '';

        if (typeof window.onResourceSearch === 'function') {
            // Kart page: filter the map without reloading, and keep the search in the URL
            window.onResourceSearch(params);
            history.replaceState(null, '', '?' + new URLSearchParams(params));
        } else {
            window.location.href = form.action + '?' + new URLSearchParams(params);
        }
    });

    document.getElementById('globalSearchReset').addEventListener('click', function () {
        document.getElementById('globalSearchWhat').value = '';
        document.getElementById('globalSearchWhere').value = '';
        status.textContent = '';
        if (typeof window.onResourceSearch === 'function') {
            window.onResourceSearch({});
            history.replaceState(null, '', window.location.pathname);
        } else {
            window.location.href = form.action;
        }
    });
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

    // Coming from another page with ?q=: search straight away
    if (onHomePage) {
        searchPage(input.value.trim());
    }
})();