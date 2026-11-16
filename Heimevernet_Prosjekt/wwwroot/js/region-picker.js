// The area picker (help with AI), shared by Oversikt, Ressurser and Behov (Views/Shared/_RegionPicker.cshtml).
// The area (kommune) is chosen once and remembered in the browser, like the address at a pizza place, so it follows you
// between the pages. If the browser already may use the position and you are in another kommune, Oversikt asks
// (it never changes by itself). Moved here from overview.js
// Docs: Window.localStorage (MDN) - https://developer.mozilla.org/docs/Web/API/Window/localStorage
// Docs: Permissions API (MDN) - https://developer.mozilla.org/docs/Web/API/Permissions_API
// Docs: Kartverket Adresse-API (punktsok) - https://ws.geonorge.no/adresser/v1/  and  Kommuneinfo - https://ws.geonorge.no/kommuneinfo/v1/
(function () {
    const page = document.getElementById('regionPage');
    if (!page) return;

    const KEY = 'hvRegion';           // the chosen area, the same on all three pages
    const RECENT = 'hvRecentRegions'; // the last chosen areas, newest first
    const region = page.dataset.region || '';
    const required = page.dataset.required === 'true'; // Oversikt before an area is chosen: the picker can't be closed
    let regions = [];
    try { regions = JSON.parse(document.getElementById('regionList').textContent) || []; } catch { regions = []; }

    function get(key) { try { return localStorage.getItem(key); } catch { return null; } }
    function set(key, value) { try { localStorage.setItem(key, value); } catch { /* storage is off */ } }

    // The area is also kept in a cookie, so the server shows the right area at once (RegionList.Chosen) - no flicker
    // Docs: Document.cookie (MDN) - https://developer.mozilla.org/docs/Web/API/Document/cookie
    function remember(name) {
        set(KEY, name);
        document.cookie = KEY + '=' + encodeURIComponent(name) + '; path=/; max-age=31536000; SameSite=Lax';
    }
    function recent() { try { return JSON.parse(get(RECENT) || '[]'); } catch { return []; } }

    // "KRISTIANSAND" -> "Kristiansand"
    function titleCase(text) {
        return (text || '').toLowerCase().replace(/(^|[\s-])\p{L}/gu, letter => letter.toUpperCase());
    }

    function countsFor(name) {
        return regions.find(r => r.name.toLowerCase() === name.toLowerCase()) || { needs: 0, resources: 0 };
    }

    // The same page with ?region=…; the rest of the address (search, «Mine» …) is kept
    function urlFor(name) {
        const target = new URL(page.dataset.url, window.location.origin);
        new URLSearchParams(window.location.search).forEach((value, key) => { if (key !== 'region') target.searchParams.set(key, value); });
        target.searchParams.set('region', name);
        return target.pathname + target.search;
    }

    // Choose an area: remember it (and in «Dine områder»), and show the page for it
    function choose(name) {
        remember(name);
        if (name !== 'alle') set(RECENT, JSON.stringify([name, ...recent().filter(r => r !== name)].slice(0, 3)));
        window.location.href = urlFor(name);
    }

    // The saved area is the one truth for all three pages (help with AI): it wins over an old ?region= in a link,
    // in the menu or in the browser history. Only choose() changes it. The first time, the one in the address is saved
    const saved = get(KEY);
    if (saved) remember(saved); // the cookie is always the same as the saved area (also for areas chosen before the cookie)
    if (saved && saved !== region) { window.location.replace(urlFor(saved)); return; }
    if (!saved && region) remember(region);

    // «Tilbake» in the browser can show the page from its cache without running this file again: check once more
    // Docs: pageshow event (MDN) - https://developer.mozilla.org/docs/Web/API/Window/pageshow_event
    window.addEventListener('pageshow', e => {
        const now = get(KEY);
        if (e.persisted && now && now !== region) window.location.replace(urlFor(now));
    });

    const picker = document.getElementById('regionPicker');
    const changeButton = document.getElementById('regionChange');
    const status = document.getElementById('regionStatus');
    const search = document.getElementById('regionSearch');
    const suggest = document.getElementById('regionSuggest');

    // «Dine områder»: big cards for the last areas (✓ on the one shown now)
    const recentList = recent();
    if (recentList.length > 0) {
        const box = document.getElementById('regionRecent');
        recentList.forEach(name => {
            const c = countsFor(name);
            const card = document.createElement('button');
            card.type = 'button';
            card.className = 'rg-card' + (name === region ? ' is-current' : '');
            const title = document.createElement('b');
            title.textContent = (name === region ? '✓ ' : '') + name; // textContent: a name can't run as HTML
            const small = document.createElement('span');
            small.className = 'small text-body-secondary';
            small.textContent = `${c.needs} behov · ${c.resources} ressurser` + (name === region ? ' · valgt nå' : '');
            card.append(title, small);
            card.addEventListener('click', () => choose(name));
            box.appendChild(card);
        });
        document.getElementById('regionRecentBox').hidden = false;
    }

    // The buttons with data-pick: «Hele Norge» and «Mest aktive nå»
    document.querySelectorAll('[data-pick]').forEach(b => b.addEventListener('click', () => choose(b.dataset.pick)));

    // «Bytt område» opens and closes the picker; ✕, Esc or a click outside closes it (not while it must be used first)
    function openPicker(open) {
        picker.hidden = !open;
        changeButton?.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) {
            picker.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            search.focus();
        }
    }
    changeButton?.addEventListener('click', () => openPicker(picker.hidden));
    document.getElementById('regionClose')?.addEventListener('click', () => { openPicker(false); changeButton?.focus(); });
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && !required && !picker.hidden && suggest.hidden) { openPicker(false); changeButton?.focus(); }
    });
    document.addEventListener('click', e => {
        if (!required && !picker.hidden && !e.target.closest('.rg-wrap')) openPicker(false);
    });

    // The kommune at a position, from Kartverket (the same as in "Ny ressurs")
    async function kommuneAt(lat, lng) {
        const response = await fetch(`https://ws.geonorge.no/adresser/v1/punktsok?lat=${lat}&lon=${lng}&radius=1000&treffPerSide=1&utkoordsys=4258`);
        const a = (await response.json()).adresser?.[0];
        return a?.kommunenavn ? titleCase(a.kommunenavn) : null;
    }

    // «Der jeg er nå»: only when the user says yes
    document.getElementById('regionHere').addEventListener('click', () => {
        if (!navigator.geolocation) { status.textContent = 'Nettleseren din støtter ikke posisjon.'; return; }
        status.textContent = 'Henter posisjonen din …';
        navigator.geolocation.getCurrentPosition(async pos => {
            try {
                const name = await kommuneAt(pos.coords.latitude, pos.coords.longitude);
                if (name) choose(name); else status.textContent = 'Fant ikke kommunen. Søk i stedet.';
            } catch { status.textContent = 'Fant ikke kommunen. Søk i stedet.'; }
        }, () => { status.textContent = 'Fikk ikke tilgang til posisjonen din.'; });
    });

    // The search: our kommuner first (with the number of needs), then all kommuner in Norway from Kartverket
    let timer = null;

    function suggestRow(name, note) {
        const b = document.createElement('button');
        b.type = 'button';
        const icon = document.createElement('i');
        icon.className = 'ti ti-map-pin';
        icon.setAttribute('aria-hidden', 'true');
        const text = document.createElement('span');
        text.textContent = name;
        const small = document.createElement('span');
        small.className = 'ms-auto small text-body-secondary';
        small.textContent = note;
        b.append(icon, text, small);
        b.addEventListener('click', () => choose(name));
        suggest.appendChild(b);
    }

    search.addEventListener('input', () => {
        clearTimeout(timer);
        const text = search.value.trim().toLowerCase();
        if (text.length < 2) { suggest.hidden = true; return; }
        timer = setTimeout(async () => {
            suggest.replaceChildren();
            const ours = regions.filter(r => r.name.toLowerCase().includes(text));
            ours.forEach(r => suggestRow(r.name, `${r.needs} behov · ${r.resources} ressurser`));
            try {
                const response = await fetch('https://ws.geonorge.no/kommuneinfo/v1/sok?knavn=' + encodeURIComponent(text));
                const data = await response.json();
                (data.kommuner || []).forEach(k => {
                    const name = titleCase(k.kommunenavnNorsk || k.kommunenavn);
                    if (!ours.some(r => r.name.toLowerCase() === name.toLowerCase())) suggestRow(name, k.fylkesnavn || '0 behov');
                });
            } catch { /* Kartverket not reachable: our kommuner are enough */ }
            if (!suggest.children.length) {
                const empty = document.createElement('div');
                empty.className = 'small text-body-secondary px-3 py-2';
                empty.textContent = 'Fant ingen kommune';
                suggest.appendChild(empty);
            }
            suggest.hidden = false;
        }, 250);
    });
    search.addEventListener('keydown', e => {
        if (e.key === 'Escape') suggest.hidden = true;
        if (e.key === 'Enter') { e.preventDefault(); suggest.querySelector('button')?.click(); }
    });
    document.addEventListener('click', e => { if (!e.target.closest('.rg-search')) suggest.hidden = true; });

    // «Du ser ut til å være i …» (Oversikt): only if the position is already allowed, only once per visit, never changes by itself
    async function askIfElsewhere() {
        const ask = document.getElementById('regionAsk');
        if (!ask || !region || region === 'alle' || !navigator.permissions || !navigator.geolocation) return;
        try { if (sessionStorage.getItem('hvRegionAsked')) return; } catch { return; }
        const permission = await navigator.permissions.query({ name: 'geolocation' });
        if (permission.state !== 'granted') return;
        navigator.geolocation.getCurrentPosition(async pos => {
            try {
                const name = await kommuneAt(pos.coords.latitude, pos.coords.longitude);
                if (!name || name.toLowerCase() === region.toLowerCase()) return;
                document.getElementById('regionAskName').textContent = name;
                ask.hidden = false;
                document.getElementById('regionAskYes').onclick = () => choose(name);
            } catch { /* no answer from Kartverket: don't ask */ }
        });
        document.getElementById('regionAskNo').addEventListener('click', () => { ask.hidden = true; });
        try { sessionStorage.setItem('hvRegionAsked', '1'); } catch { /* storage is off */ }
    }
    askIfElsewhere();
})();
