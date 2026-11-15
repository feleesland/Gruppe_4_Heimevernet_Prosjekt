// The resource page (help with AI): «Endre» on Tilgjengelig (the kinds, the quick choices and the day bar), Eksporter,
// "Er du sikker?" and the small map. The page gives the data in data-* attributes and <script id="resourceExport">
// Docs: Bootstrap Dropdowns - https://getbootstrap.com/docs/5.3/components/dropdowns/
// Docs: Leaflet API reference - https://leafletjs.com/reference.html
(function () {
    // "Er du sikker?" on the buttons that change much (data-confirm)
    document.addEventListener('click', e => {
        const button = e.target.closest('[data-confirm]');
        if (button && !confirm(button.dataset.confirm)) e.preventDefault();
    });

    // ---- «Endre» on Tilgjengelig ----
    const form = document.getElementById('availForm');
    if (form) {
        const kinds = [...form.querySelectorAll('input[name="kind"]')];
        const fromField = document.getElementById('fromTime');
        const toField = document.getElementById('toTime');
        const setUrl = form.action;

        // Only the fields for the chosen kind. "Ikke tilgjengelig" is saved by SetUnavailable
        function showKind() {
            const kind = (kinds.find(k => k.checked) || {}).value || 'Always';
            form.querySelectorAll('[data-kind-box]').forEach(box => { box.hidden = box.dataset.kindBox !== kind; });
            form.action = kind === 'unavailable' ? form.dataset.unavailableUrl : setUrl;
        }
        kinds.forEach(k => k.addEventListener('change', showKind));

        // The day bar: green where it can be asked. 16:00–08:00 = two pieces (evening and early morning)
        function minutes(value) {
            const [h, m] = (value || '00:00').split(':').map(Number);
            return h * 60 + (m || 0);
        }
        function drawBar() {
            const bar = document.getElementById('dayBar');
            const a = minutes(fromField.value);
            const b = minutes(toField.value);
            const parts = a === b ? [[0, 1440]] : a < b ? [[a, b]] : [[0, b], [a, 1440]];
            bar.replaceChildren(...parts.map(([start, end]) => {
                const piece = document.createElement('div');
                piece.style.left = (start / 14.4) + '%';
                piece.style.width = ((end - start) / 14.4) + '%';
                return piece;
            }));
            document.getElementById('overnightHint').hidden = !(a > b);
        }
        fromField.addEventListener('input', drawBar);
        toField.addEventListener('input', drawBar);

        // Quick choices: "16:00|08:00|127" = from, to and the days (1 = Monday ... 64 = Sunday, added together)
        form.querySelectorAll('[data-preset]').forEach(button => button.addEventListener('click', () => {
            const [from, to, bits] = button.dataset.preset.split('|');
            fromField.value = from;
            toField.value = to;
            form.querySelectorAll('input[name="days"]').forEach(day => { day.checked = (Number(bits) & Number(day.value)) !== 0; });
            drawBar();
        }));

        const unknown = document.getElementById('unavailableUnknown');
        unknown.addEventListener('change', () => { document.getElementById('unavailableUntil').disabled = unknown.checked; });

        document.getElementById('availCancel').addEventListener('click', () =>
            bootstrap.Dropdown.getOrCreateInstance(document.getElementById('availToggle')).hide());

        showKind();
        drawBar();
    }

    // ---- Eksporter: Skriv ut / PDF, or the data from <script id="resourceExport"> ----
    document.querySelectorAll('[data-res-export]').forEach(button => button.addEventListener('click', () => {
        const kind = button.dataset.resExport;
        if (kind === 'print') { window.print(); return; }
        let data;
        try { data = JSON.parse(document.getElementById('resourceExport').textContent); } catch { return; }
        const file = 'ressurs-' + (data.Ressurs || '').toLowerCase().replace(/[^a-z0-9æøå]+/g, '-').replace(/^-+|-+$/g, '');
        // CSV: one row per loan, or the resource alone when it has never been lent out
        const rows = kind === 'json' ? data
            : data.Utlån.length > 0 ? data.Utlån.map(loan => Object.assign({ Ressurs: data.Ressurs }, loan))
                : [{ Ressurs: data.Ressurs, Type: data.Type, Status: data.Status, Tilgjengelig: data.Tilgjengelig, Eier: data.Eier, Adresse: data.Adresse }];
        ResourceExport.download(rows, file, kind);
    }));

    // ---- The small map: the resource. Big screens: it fills the screen down to the footer ----
    const mapDiv = document.getElementById('detailMap');
    if (mapDiv) {
        const lat = parseFloat(mapDiv.dataset.lat);
        const lng = parseFloat(mapDiv.dataset.lng);
        const detailMap = L.map('detailMap').setView([lat, lng], 15);
        addMapPictures(detailMap);
        L.marker([lat, lng], { title: mapDiv.dataset.name }).addTo(detailMap);

        const fitMap = () => {
            if (window.innerWidth < 992) {
                mapDiv.style.height = '';
            } else {
                const top = mapDiv.getBoundingClientRect().top + window.scrollY;
                const legend = mapDiv.nextElementSibling ? mapDiv.nextElementSibling.offsetHeight : 0;
                const footer = document.querySelector('footer');
                mapDiv.style.height = Math.max(380, window.innerHeight - top - legend - (footer ? footer.offsetHeight : 0) - 40) + 'px';
            }
            detailMap.invalidateSize();
        };
        window.addEventListener('resize', fitMap);
        fitMap();
    }
})();