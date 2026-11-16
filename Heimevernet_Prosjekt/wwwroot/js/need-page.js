// The need page (help with AI): the overview of the lines, one line at a time («Finn ▸» / «Vis ▸»), «+ Spør» / «✓ Valgt»,
// the send button, the small map, "Info til innbyggere" quick texts, the back button, and the side panel on the main map.
// Moved here from the bottom of Views/ResourceNeed/Index.cshtml. The page gives the data in data-* attributes.
// Docs: Leaflet API reference (Circle, DivIcon, Polyline, Tooltip) - https://leafletjs.com/reference.html
// Docs: Window.postMessage (MDN) - https://developer.mozilla.org/docs/Web/API/Window/postMessage
(function () {
    const page = document.getElementById('needPage');
    if (!page) return;

    const form = document.getElementById('requestForm');
    const overview = document.getElementById('npOverview');
    const sendBar = document.getElementById('npSendBar');
    const sendButton = document.getElementById('sendButton');
    const storeKey = 'need-open-line-' + page.dataset.needId;
    const picks = () => [...document.querySelectorAll('#requestForm .pick')];
    let openLine = null; // the section of the open line, or null = the overview

    // ---- «← Oversikt» / «← Alle behov» (help with AI): from Oversikt (?from=overview, or the page came from there) it goes back
    // to Oversikt, and that is remembered for this need while forms are sent. From the Behov list it goes back with the same filter
    // Docs: Window.sessionStorage (MDN) - https://developer.mozilla.org/docs/Web/API/Window/sessionStorage
    const back = document.getElementById('npBack');
    if (back) {
        const fromKey = 'need-from-' + page.dataset.needId;
        const fromQuery = new URLSearchParams(location.search).get('from');
        const fromList = document.referrer.includes('/ResourceNeed/List');
        let from = null;
        try {
            if (fromQuery === 'overview' || document.referrer.includes('/ResourceNeed/Overview')) sessionStorage.setItem(fromKey, 'overview');
            else if (fromList) sessionStorage.removeItem(fromKey);
            from = sessionStorage.getItem(fromKey);
        } catch { from = fromQuery; }
        if (from === 'overview') {
            back.href = back.dataset.overview;
            back.querySelector('span').textContent = 'Oversikt';
        }
        back.addEventListener('click', e => {
            if (from !== 'overview' && fromList) { e.preventDefault(); history.back(); }
        });
    }

    // "Er du sikker?" on the buttons that change much (data-confirm)
    document.addEventListener('click', e => {
        const button = e.target.closest('[data-confirm]');
        if (button && !confirm(button.dataset.confirm)) e.preventDefault();
    });

    // ---- Eksporter (help with AI): Skriv ut / PDF, or the data from <script id="needExport"> as CSV (the rows) or JSON (everything)
    document.querySelectorAll('[data-need-export]').forEach(button => button.addEventListener('click', () => {
        const kind = button.dataset.needExport;
        if (kind === 'print') { window.print(); return; }
        let data;
        try { data = JSON.parse(document.getElementById('needExport').textContent); } catch { return; }
        // "Flom Lund" -> behov-flom-lund-2026-11-15.csv
        const file = 'behov-' + (page.dataset.needName || '').toLowerCase()
            .replace(/[^a-z0-9æøå]+/g, '-').replace(/^-+|-+$/g, '');
        ResourceExport.download(kind === 'json' ? data : data.Linjer, file, kind);
    }));

    // ---- "Info til innbyggere": a quick text fills the field (the cursor goes to the end), and the counter "42 / 300" ----
    const updateField = document.getElementById('updateText');
    const updateCount = document.getElementById('updateCount');
    const countUpdate = () => { if (updateField && updateCount) updateCount.textContent = `${updateField.value.length} / 300`; };
    updateField?.addEventListener('input', countUpdate);
    document.querySelectorAll('[data-quick]').forEach(button => button.addEventListener('click', () => {
        updateField.value = button.dataset.quick;
        countUpdate();
        updateField.focus();
        updateField.setSelectionRange(updateField.value.length, updateField.value.length);
    }));

    // ---- The send button counts what is chosen on all lines, and is off until something is chosen ----
    function updateSend() {
        if (!sendButton) return;
        const n = picks().filter(b => b.checked).length;
        sendButton.disabled = n === 0;
        sendButton.textContent = n === 0 ? 'Velg ressurser' : n === 1 ? 'Send 1 forespørsel' : `Send ${n} forespørsler`;
        // Shown in a line with free resources, or as long as something is chosen
        sendBar.hidden = n === 0 && !(openLine && openLine.querySelector('.pick'));
    }

    // ---- One line at a time. The open line is remembered when a form is sent (⋯, «Send», Publiser),
    // so the page opens on it again when it comes back ----
    function showLine(id, focus) {
        openLine = id ? document.getElementById('np-line-' + id) : null;
        overview.hidden = !!openLine;
        document.querySelectorAll('.np-line').forEach(section => { section.hidden = section !== openLine; });

        if (focus) {
            const target = openLine ? openLine.querySelector('h2') : document.querySelector(`[data-open-line="${focus}"]`);
            target?.setAttribute('tabindex', '-1');
            target?.focus({ preventScroll: true });
            (openLine || overview).scrollIntoView({ block: 'nearest' });
        }
        updateSend();
        drawDots(true);
        tellMap();
    }

    // The rows in «Hva trengs» and the «Finn …» buttons in «Neste steg» open a line
    document.querySelectorAll('[data-open-line]').forEach(row =>
        row.addEventListener('click', () => showLine(row.dataset.openLine, true)));
    document.querySelectorAll('[data-close-line]').forEach(button =>
        button.addEventListener('click', () => showLine(null, openLine?.dataset.line)));

    // «Bare innenfor sirkelen»: hides the free resources outside the circle (in the list and on the map)
    document.querySelectorAll('[data-inside-only]').forEach(chip => chip.addEventListener('click', () => {
        const on = chip.getAttribute('aria-pressed') !== 'true';
        chip.setAttribute('aria-pressed', on);
        chip.closest('.np-line').classList.toggle('inside-only', on);
        drawDots();
    }));

    form?.addEventListener('change', () => { updateSend(); drawDots(); tellMap(); });

    // ---- The small map: the need, the circle, ⚑ kontaktpunkt with a dashed line, and the free resources ----
    const mapDiv = document.getElementById('detailMap');
    let detailMap = null;
    let circle = null;
    let meetingMarker = null;
    let dotLayer = null;

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    // Blue with ✓ = chosen, green = free inside the circle, grey = outside the circle
    function dotIcon(box) {
        const css = box.checked ? 'is-picked' : box.dataset.inside === 'true' ? 'is-free' : 'is-outside';
        return L.divIcon({ className: '', iconSize: [22, 22], iconAnchor: [11, 11], html: `<div class="np-dot np-dot-map ${css}">${box.checked ? '✓' : ''}</div>` });
    }

    if (mapDiv) {
        const lat = parseFloat(mapDiv.dataset.lat);
        const lng = parseFloat(mapDiv.dataset.lng);
        const planned = mapDiv.dataset.planned === 'true';
        detailMap = L.map('detailMap').setView([lat, lng], 13);
        addMapPictures(detailMap);
        L.marker([lat, lng], { title: page.dataset.needName }).addTo(detailMap);
        circle = L.circle([lat, lng], {
            radius: parseFloat(mapDiv.dataset.radius) * 1000,
            color: planned ? '#E69500' : '#E24B4A',
            dashArray: '8 6',
            fillOpacity: 0.08
        }).addTo(detailMap);

        const meetingLat = parseFloat(mapDiv.dataset.meetingLat);
        const meetingLng = parseFloat(mapDiv.dataset.meetingLng);
        if (!isNaN(meetingLat) && !isNaN(meetingLng)) {
            meetingMarker = L.marker([meetingLat, meetingLng], {
                title: mapDiv.dataset.meetingName,
                icon: L.divIcon({ className: '', html: '<div class="meeting-flag"><i class="ti ti-flag" aria-hidden="true"></i></div>', iconSize: [26, 26], iconAnchor: [13, 13] })
            }).bindTooltip('⚑ ' + escapeHtml(mapDiv.dataset.meetingName)).addTo(detailMap);
            L.polyline([[lat, lng], [meetingLat, meetingLng]], { color: '#7A5AF8', weight: 2, dashArray: '6 6', interactive: false }).addTo(detailMap);
        }
        dotLayer = L.layerGroup().addTo(detailMap);

        // «Vis på kartet» under Kontaktpunkt
        document.getElementById('showMeeting')?.addEventListener('click', () => {
            mapDiv.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            detailMap.setView(meetingMarker.getLatLng(), 16);
            meetingMarker.openTooltip();
        });

        // The map column changes size with the window (Leaflet must know)
        // Big screens: the map fills the screen down to the footer, so the map doesn't make the page scroll (help with AI)
        // Docs: Element.getBoundingClientRect (MDN) - https://developer.mozilla.org/docs/Web/API/Element/getBoundingClientRect
        const fitMap = () => {
            if (window.innerWidth < 992) {
                mapDiv.style.height = '';
            } else {
                const top = mapDiv.getBoundingClientRect().top + window.scrollY;
                const legend = mapDiv.nextElementSibling ? mapDiv.nextElementSibling.offsetHeight : 0;
                const footer = document.querySelector('footer');
                const below = legend + (footer ? footer.offsetHeight : 0) + 40;
                mapDiv.style.height = Math.max(380, window.innerHeight - top - below) + 'px';
            }
            detailMap.invalidateSize();
        };
        window.addEventListener('resize', fitMap);
        fitMap();
    }

    if (detailMap) drawDots(true);

    // The dots (help with AI). A line is open: its free resources, and a click chooses or unchooses, same as «+ Spør».
    // The overview: the free resources of every line that misses something (each resource once), and a click opens its line.
    // fit: move the map (only when a line is opened or closed, not on every choice). It shows the circle, the flag,
    // the free ones inside the circle and the chosen ones - or, in an open line with none inside, the 3 nearest
    function drawDots(fit) {
        if (!detailMap) return;
        dotLayer.clearLayers();
        const bounds = circle.getBounds();
        if (meetingMarker) bounds.extend(meetingMarker.getLatLng());
        const insideOnly = openLine ? openLine.classList.contains('inside-only') : false;
        const seen = new Set();
        const boxes = (openLine ? [...openLine.querySelectorAll('.pick')] : picks())
            .filter(box => box.dataset.lat)
            .filter(box => {
                const id = box.value.split(':')[1];
                if (seen.has(id)) return false;
                seen.add(id);
                return true;
            });
        const anyInside = boxes.some(box => box.dataset.inside === 'true');
        boxes.forEach((box, i) => {
            if (insideOnly && box.dataset.inside !== 'true') return;
            const pos = [parseFloat(box.dataset.lat), parseFloat(box.dataset.lng)];
            const label = escapeHtml(box.dataset.name) + (openLine ? '' : ' · ' + escapeHtml(box.dataset.type)) + (box.checked ? ' · valgt' : '');
            const marker = L.marker(pos, { icon: dotIcon(box), title: box.dataset.name, keyboard: true }).bindTooltip(label);
            marker.on('click', () => {
                if (!openLine) { showLine(box.closest('.np-line').dataset.line, true); return; }
                box.checked = !box.checked;
                box.dispatchEvent(new Event('change', { bubbles: true }));
            });
            dotLayer.addLayer(marker);
            // The list is nearest first, so i < 3 = the 3 nearest
            if (box.checked || box.dataset.inside === 'true' || (openLine && !anyInside && i < 3)) bounds.extend(pos);
        });
        if (fit) detailMap.fitBounds(bounds, { padding: [24, 24], maxZoom: 15 });
    }

    // ---- In the side panel on the main map: tell the map which resources can be chosen (the open line's, or all) and which
    // are chosen, and choose a resource when the map asks ----
    const inPanel = window.parent !== window;
    function tellMap() {
        if (!inPanel) return;
        const idOf = box => parseInt(box.value.split(':')[1], 10);
        const shown = openLine ? [...openLine.querySelectorAll('.pick')] : picks();
        parent.postMessage({
            type: 'need-open',
            name: page.dataset.needName,
            suggestions: shown.map(idOf),
            picked: picks().filter(b => b.checked).map(idOf),
            // The kontaktpunkt, so the main map can show the purple flag (help with AI)
            meeting: page.dataset.meetingLat ? { name: page.dataset.meetingName, lat: parseFloat(page.dataset.meetingLat), lng: parseFloat(page.dataset.meetingLng) } : null
        }, location.origin);
    }
    if (inPanel) {
        window.addEventListener('message', e => {
            if (e.origin !== location.origin || !e.data || e.data.type !== 'need-pick') return;
            const box = picks().find(b => parseInt(b.value.split(':')[1], 10) === e.data.resourceId);
            if (!box) return;
            const section = box.closest('.np-line');
            if (section !== openLine) showLine(section.dataset.line, false);
            box.checked = !box.checked;
            box.dispatchEvent(new Event('change', { bubbles: true }));
            box.closest('.np-sug')?.scrollIntoView({ block: 'center', behavior: 'smooth' });
        });
    }

    document.addEventListener('submit', () => {
        try { if (openLine) sessionStorage.setItem(storeKey, openLine.dataset.line); } catch { /* storage is off */ }
    });

    // Start: on the line that was open when a form was sent (if it is still here), otherwise the overview
    let start = null;
    try { start = sessionStorage.getItem(storeKey); sessionStorage.removeItem(storeKey); } catch { /* storage is off */ }
    showLine(start && document.getElementById('np-line-' + start) ? start : null, false);
})();