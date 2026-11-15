// "Meld behov" / "Rediger behov" (help with AI): the lines, "Legg til ressurs" (the big window), Akutt/Planlagt,
// the circle on the map and "Legg til i ..." when the place is inside an active need.
// Moved here from the bottom of Views/ResourceNeedHandler/Create.cshtml. The page gives the data in <script type="application/json">.
// Docs: Leaflet API reference (Circle) - https://leafletjs.com/reference.html
// Docs: Bootstrap Modal events - https://getbootstrap.com/docs/5.3/components/modal/#events
(function () {
    const form = document.getElementById('needForm');
    if (!form) return;

    const editing = form.dataset.editing === 'true';
    const activeUrl = form.dataset.activeUrl;
    const latField = document.getElementById('Latitude');
    const lngField = document.getElementById('Longitude');
    const existingField = document.getElementById('ExistingNeedId');
    const radiusSelect = document.getElementById('RadiusKm');
    const linesBox = document.getElementById('lines');
    const submitButton = document.getElementById('submitButton');
    const searchField = document.getElementById('typeSearch');
    // In the side panel on the main map: the need is placed on the big map (tellMap at the end of this file)
    const inPanel = window.parent !== window;

    function readJson(id) {
        try { return JSON.parse(document.getElementById(id).textContent) || []; } catch { return []; }
    }

    // ---- The types: categories with icons, and old types without a category under "Annet" ----
    const categories = readJson('needCategories').map(c => ({
        name: c.name,
        icon: (c.icon || 'category').replace(/^ti-/, ''),
        types: (c.types || []).map(t => ({ name: t.name, icon: (t.icon || c.icon || 'map-pin').replace(/^ti-/, '') }))
    }));
    const known = new Set(categories.flatMap(c => c.types.map(t => t.name)));
    const leftover = readJson('needTypes').filter(name => !known.has(name));
    if (leftover.length > 0) {
        let other = categories.find(c => c.name === 'Annet');
        if (!other) { other = { name: 'Annet', icon: 'dots', types: [] }; categories.push(other); }
        leftover.forEach(name => other.types.push({ name: name, icon: 'dots' }));
    }
    const typeInfo = {};
    categories.forEach(c => c.types.forEach(t => { typeInfo[t.name] = { icon: t.icon, category: c.name }; }));

    // ---- The lines: one array, drawn in the form (with hidden inputs) and in the window ----
    // itemId/accepted/hasRequests come from "Rediger": a line with requests can't be removed, and not go below "accepted"
    let lines = readJson('needLines')
        .filter(l => l.type)
        .map(l => ({ itemId: l.itemId, type: l.type, quantity: Math.max(1, l.quantity || 1), accepted: l.accepted || 0, hasRequests: !!l.hasRequests }));
    let lastAdded = null;
    let currentCategory = 0;

    // "Ofte brukt": the types used most in needs (from the server), or a fixed list while there are few needs.
    // Filled up to 6 with the first types, and only types that exist
    const fallback = ['Bil', 'Aggregat', 'Førstehjelp', 'Sandsekker', 'Pumpe', 'Lastebil'];
    const popular = [...new Set([...readJson('needPopular'), ...fallback, ...Object.keys(typeInfo)])]
        .filter(name => typeInfo[name])
        .slice(0, 6);

    function findLine(type) { return lines.find(l => l.type === type); }
    function minimum(line) { return Math.max(1, line.accepted); }

    function addType(type) {
        const line = findLine(type);
        if (line) line.quantity = Math.min(1000, line.quantity + 1);
        else lines.push({ type: type, quantity: 1, accepted: 0, hasRequests: false });
        lastAdded = type;
        drawAll();
    }

    function changeQuantity(type, delta) {
        const line = findLine(type);
        if (!line) return;
        const wanted = line.quantity + delta;
        if (wanted < minimum(line)) {
            if (!line.hasRequests) lines = lines.filter(l => l !== line); // below 1 = remove the line
        } else {
            line.quantity = Math.min(1000, wanted);
        }
        drawAll();
    }

    function removeLine(type) {
        lines = lines.filter(l => l.type !== type || l.hasRequests);
        drawAll();
    }

    function totalCount() { return lines.reduce((sum, l) => sum + l.quantity, 0); }

    // Makes an element with a class and text (textContent, so a type name can't run as HTML)
    function el(tag, className, text) {
        const e = document.createElement(tag);
        if (className) e.className = className;
        if (text != null) e.textContent = text;
        return e;
    }

    function icon(name) {
        const i = el('i', 'ti ti-' + name.replace(/[^a-z0-9-]/gi, ''));
        i.setAttribute('aria-hidden', 'true');
        return i;
    }

    function smallButton(text, label, onClick, disabled) {
        const b = el('button', 'btn btn-sm rf-reset need-step', text);
        b.type = 'button';
        b.setAttribute('aria-label', label);
        b.disabled = !!disabled;
        b.addEventListener('click', onClick);
        return b;
    }

    // − 2 + for one line
    function stepper(line) {
        const box = el('span', 'need-stepper');
        box.append(
            smallButton('−', 'Færre ' + line.type, () => changeQuantity(line.type, -1), line.hasRequests && line.quantity <= minimum(line)),
            el('b', null, String(line.quantity)),
            smallButton('+', 'Flere ' + line.type, () => changeQuantity(line.type, 1)));
        return box;
    }

    function hidden(name, value) {
        const input = el('input');
        input.type = 'hidden';
        input.name = name;
        input.value = value;
        return input;
    }

    // ---- The lines in the form ----
    function drawLines() {
        linesBox.replaceChildren();
        linesBox.hidden = lines.length === 0;
        lines.forEach((line, i) => {
            const info = typeInfo[line.type] || { icon: 'map-pin', category: '' };
            const row = el('div', 'need-line' + (line.type === lastAdded ? ' is-new' : ''));
            const dot = el('span', 'need-line-icon');
            dot.appendChild(icon(info.icon));
            const name = el('span', 'need-line-name');
            name.append(el('b', null, line.type));
            if (info.category) name.append(el('span', 'text-body-secondary', ' · ' + info.category));
            if (line.hasRequests) {
                name.append(el('span', 'd-block small text-body-secondary',
                    '🔒 Forespørsler er sendt' + (line.accepted > 0 ? `, ${line.accepted} har sagt ja` : '')));
            }
            row.append(dot, name, stepper(line));
            if (line.hasRequests) {
                const lock = el('span', 'btn btn-sm rf-reset need-step disabled', '🔒');
                lock.title = 'Kan ikke fjernes: forespørsler er sendt';
                row.append(lock);
            } else {
                row.append(smallButton('✕', 'Fjern ' + line.type, () => removeLine(line.type)));
            }

            // What the server reads (Lines[0].Type, Lines[0].Quantity, …)
            if (line.itemId != null) {
                row.append(hidden(`Lines[${i}].ItemId`, line.itemId), hidden(`Lines[${i}].Accepted`, line.accepted),
                    hidden(`Lines[${i}].HasRequests`, line.hasRequests ? 'true' : 'false'));
            }
            row.append(hidden(`Lines[${i}].Type`, line.type), hidden(`Lines[${i}].Quantity`, line.quantity));
            linesBox.appendChild(row);
        });

        // The new line is scrolled into view inside the card
        linesBox.querySelector('.is-new')?.scrollIntoView({ block: 'nearest' });

        document.getElementById('linesCount').textContent = lines.length === 0 ? ''
            : `${lines.length} ${lines.length === 1 ? 'type' : 'typer'} · ${totalCount()} stk`;
    }

    // ---- Under the lines: "Ofte brukt" and the categories (help with AI) ----
    // Empty: big cards. Some lines: the big cards stay (chosen ones green with ✓ and the number), so nothing disappears.
    // When the lines no longer fit, one jump to small chips, so the page never scrolls (the lines scroll in their card)
    function drawQuick() {
        const empty = lines.length === 0;
        // Small screens: the page scrolls anyway, so keep it short with chips once something is chosen
        if (!empty && window.innerWidth < 992) {
            renderQuick('compact');
            return;
        }
        renderQuick(empty ? 'empty' : 'full');
        // The lines are squeezed (taller than their space): switch to chips
        if (!empty && linesBox.scrollHeight > linesBox.clientHeight + 1) {
            renderQuick('compact');
        }
    }

    // mode: 'empty' (nothing chosen), 'full' (big cards + categories) or 'compact' (one row of chips)
    function renderQuick(mode) {
        const box = document.getElementById('quickBox');
        const big = mode !== 'compact';
        box.closest('.need-lines-card').classList.toggle('is-full', mode === 'full');
        box.replaceChildren();
        box.className = mode === 'empty' ? 'need-quick is-empty' : 'need-quick';

        box.append(el('div', 'need-quick-label', big ? 'Ofte brukt · ett klikk legger til' : 'Ofte brukt'));
        const grid = el('div', big ? 'need-quick-grid' : 'need-quick-chips');
        popular.forEach(name => {
            const line = findLine(name);
            const count = line ? ' · ' + line.quantity : '';
            const b = el('button', (big ? 'need-quick-card' : 'need-chip') + (line ? ' is-on' : ''));
            b.type = 'button';
            b.setAttribute('aria-label', 'Legg til ' + name + (line ? `, ${line.quantity} valgt` : ''));
            if (big) {
                const dot = el('span', 'need-type-icon');
                dot.appendChild(icon(line ? 'check' : typeInfo[name].icon));
                const plus = el('span', 'need-type-plus');
                plus.appendChild(icon('plus'));
                b.append(dot, el('span', 'flex-grow-1', name + count), plus);
            } else {
                b.append(icon(line ? 'check' : 'plus'), document.createTextNode(' ' + name + count));
            }
            b.addEventListener('click', () => addType(name));
            grid.appendChild(b);
        });
        box.appendChild(grid);

        // Shortcuts into the window, which opens on that category. Big screens: all of them
        if (big) {
            box.append(el('div', 'need-quick-label mt-2', 'Eller åpne en kategori'));
            const cats = el('div', 'need-quick-cats');
            (window.innerWidth >= 1400 ? categories : categories.slice(0, 4)).forEach((c, i) => {
                const b = el('button', 'need-cat need-cat-small');
                b.type = 'button';
                b.append(icon(c.icon), el('span', null, c.name));
                b.addEventListener('click', () => {
                    currentCategory = i;
                    bootstrap.Modal.getOrCreateInstance(document.getElementById('addModal')).show();
                });
                cats.appendChild(b);
            });
            box.appendChild(cats);
        }
        document.getElementById('allHint').textContent = mode === 'empty' ? `· ${categories.length} kategorier, søk` : '';
    }
    // ---- The window: categories, types and "Valgt" ----
    function searchHits() {
        const text = searchField.value.trim().toLowerCase();
        if (!text) return null;
        return Object.keys(typeInfo).filter(name => name.toLowerCase().includes(text));
    }

    function drawCategories() {
        const grid = document.getElementById('catGrid');
        grid.replaceChildren();
        categories.forEach((c, i) => {
            const chosen = lines.filter(l => typeInfo[l.type]?.category === c.name).reduce((sum, l) => sum + l.quantity, 0);
            const b = el('button', 'need-cat' + (i === currentCategory && !searchField.value.trim() ? ' is-on' : ''));
            b.type = 'button';
            b.setAttribute('aria-pressed', i === currentCategory);
            b.append(icon(c.icon), el('span', null, c.name));
            if (chosen > 0) b.append(el('span', 'need-badge', String(chosen)));
            b.addEventListener('click', () => { currentCategory = i; searchField.value = ''; drawWindow(); });
            grid.appendChild(b);
        });
    }

    function drawTypes() {
        const hits = searchHits();
        const names = hits ?? (categories[currentCategory]?.types.map(t => t.name) || []);
        document.getElementById('typeTitle').textContent = hits
            ? (hits.length ? 'Treff' : 'Fant ingen type')
            : (categories[currentCategory]?.name || '');
        const list = document.getElementById('typeList');
        list.replaceChildren();
        names.forEach(name => {
            const line = findLine(name);
            const typeIcon = el('span', 'need-type-icon');
            typeIcon.appendChild(icon(typeInfo[name]?.icon || 'map-pin'));

            if (line) {
                // Chosen: the name and − 2 +
                const row = el('div', 'need-type is-on');
                row.append(typeIcon, el('b', 'flex-grow-1', name), stepper(line));
                list.appendChild(row);
                return;
            }

            // Not chosen: the whole card is one button (bigger to hit than a small "+ Legg til")
            const button = el('button', 'need-type need-type-add');
            button.type = 'button';
            button.setAttribute('aria-label', 'Legg til ' + name);
            const plus = el('span', 'need-type-plus');
            plus.appendChild(icon('plus'));
            button.append(typeIcon, el('span', 'flex-grow-1', name), plus);
            button.addEventListener('click', () => {
                addType(name);
                // Keep the keyboard on the same type after it is drawn again
                [...document.querySelectorAll('#typeList .need-type')]
                    .find(r => r.textContent.startsWith(name))?.querySelector('button')?.focus();
            });
            list.appendChild(button);
        });
    }

    // "Valgt": the same list on the right (big screens) and in the bottom bar (small screens)
    function drawCart() {
        ['cartSide', 'cartMobile'].forEach(id => {
            const box = document.getElementById(id);
            box.replaceChildren();
            if (lines.length === 0) box.appendChild(el('p', 'small text-body-secondary mb-0', 'Ingenting valgt ennå.'));
            lines.forEach(line => {
                const row = el('div', 'need-type is-on');
                row.append(el('span', 'flex-grow-1', line.type), stepper(line));
                box.appendChild(row);
            });
        });
        const n = totalCount();
        document.getElementById('cartCount').textContent = n ? n + ' stk' : '';
        document.getElementById('cartCountMobile').textContent = n ? `Valgt · ${n} stk` : 'Valgt';
        document.getElementById('addDone').textContent = n ? `Ferdig · ${n}` : 'Ferdig';
    }

    function drawWindow() {
        drawCategories();
        drawTypes();
        drawCart();
    }

    function drawAll() {
        drawLines();
        drawQuick();
        drawWindow();
        updateSubmit();
    }

    // Enter in the search field adds the first hit, and the field is emptied for the next one
    searchField.addEventListener('input', drawWindow);
    searchField.addEventListener('keydown', e => {
        if (e.key !== 'Enter') return;
        e.preventDefault();
        const first = (searchHits() || [])[0];
        if (first) { addType(first); searchField.value = ''; drawWindow(); }
    });

    const modal = document.getElementById('addModal');
    modal.addEventListener('shown.bs.modal', () => searchField.focus());
    modal.addEventListener('hidden.bs.modal', () => { searchField.value = ''; drawWindow(); });

    // ---- Akutt / Planlagt, and "+ Frist" ----
    function isPlanned() {
        return document.getElementById('urgencyPlanlagt').checked;
    }

    function applyUrgency() {
        const planned = isPlanned();
        const toValue = document.getElementById('NeededTo').value;
        document.getElementById('fromCol').hidden = !planned;
        document.getElementById('toCol').hidden = !planned && !toValue && !deadlineOpen;
        document.getElementById('toLabel').textContent = planned ? 'Til (valgfritt)' : 'Innen';
        document.getElementById('addDeadline').hidden = planned || !!toValue || deadlineOpen;
        drawRadius();
        updateSubmit();
    }
    let deadlineOpen = false;
    document.getElementById('addDeadline').addEventListener('click', () => {
        deadlineOpen = true;
        applyUrgency();
        document.getElementById('NeededTo').focus();
    });
    document.querySelectorAll('input[name="Urgency"]').forEach(r => r.addEventListener('change', applyUrgency));

    // ---- The circle for the need (pickMap comes from location-picker.js) ----
    let radiusCircle = null;
    function drawRadius() {
        if (typeof pickMap === 'undefined') return;
        tellMap();
        if (radiusCircle) {
            pickMap.removeLayer(radiusCircle);
            radiusCircle = null;
        }
        const lat = parseFloat(latField.value);
        const lng = parseFloat(lngField.value);
        if (chosen || isNaN(lat) || isNaN(lng)) return;
        radiusCircle = L.circle([lat, lng], {
            radius: parseFloat(radiusSelect.value) * 1000,
            color: isPlanned() ? '#E69500' : '#E24B4A',
            dashArray: '8 6',
            fillOpacity: 0.08
        }).addTo(pickMap);
    }
    radiusSelect.addEventListener('change', drawRadius);

    // ---- The bar at the bottom: summary and the button ----
    function updateSubmit() {
        const n = lines.length;
        submitButton.textContent = editing ? 'Lagre endringer'
            : chosen ? `Legg til ${n} behov i ${chosen.name}`
                : `Meld ${n} behov`;
        const where = document.getElementById('Address').value.trim() || (latField.value ? 'Valgt punkt' : 'Velg sted');
        const parts = [chosen ? 'Legges til i ' + chosen.name : (isPlanned() ? '📅 Planlagt' : '⏰ Akutt'), '📍 ' + where];
        if (hasMeeting()) parts.push('⚑ ' + (meetingName.value || 'Kontaktpunkt'));
        parts.push(totalCount() + ' stk');
        document.getElementById('needSummary').textContent = parts.join(' · ');
    }
    document.getElementById('Address').addEventListener('input', updateSubmit);

    // ---- "Legg til i ..." or "Nytt behov" ----
    let activeNeeds = [];
    let chosen = null;            // the active need we add to, or null = new need
    let userChoseNew = false;     // the user clicked "Nytt behov": don't pick for them again

    function applyMode() {
        existingField.value = chosen ? chosen.id : '';
        document.getElementById('newNeedFields').hidden = !!chosen;
        document.getElementById('radiusWrap').hidden = !!chosen;
        const info = document.getElementById('addingInfo');
        info.hidden = !chosen;
        info.textContent = chosen
            ? `Behovene legges til i «${chosen.name}» (${chosen.urgency.toLowerCase()}). Navn og tid hentes derfra.`
            : '';
        document.getElementById('linesLegend').textContent = chosen ? 'Hva trengs i tillegg?' : 'Hva trengs?';
        drawRadius();
        updateSubmit();
    }

    function renderBox() {
        const box = document.getElementById('activeBox');
        const list = document.getElementById('activeList');
        list.replaceChildren();
        box.hidden = activeNeeds.length === 0;

        // One button per active need, with the distance on it, and "Nytt behov" (textContent: names from users can't run as code)
        const buttons = el('div', 'd-flex flex-wrap gap-2');
        activeNeeds.forEach(need => {
            const button = el('button', 'btn btn-sm ' + (chosen && chosen.id === need.id ? 'btn-warning' : 'btn-outline-warning'),
                (chosen && chosen.id === need.id ? '✓ ' : '') + `Legg til i ${need.name} · ${String(need.distanceKm).replace('.', ',')} km`);
            button.type = 'button';
            button.addEventListener('click', () => { chosen = need; userChoseNew = false; renderBox(); applyMode(); });
            buttons.appendChild(button);
        });
        const newButton = el('button', 'btn btn-sm ' + (chosen ? 'btn-outline-warning' : 'btn-warning'), (chosen ? '' : '✓ ') + 'Nytt behov');
        newButton.type = 'button';
        newButton.addEventListener('click', () => { chosen = null; userChoseNew = true; renderBox(); applyMode(); });
        buttons.appendChild(newButton);
        list.appendChild(buttons);

        document.getElementById('alreadyReported').textContent = chosen && chosen.items.length
            ? 'Allerede meldt: ' + chosen.items.map(i => `${i.quantity} × ${i.type}`).join(' · ')
            : '';
    }

    // Ask the server: is the chosen place inside an active need? (not when editing: the need would find itself)
    async function checkPlace() {
        updateSubmit();
        if (editing) {
            drawRadius();
            return;
        }
        const lat = parseFloat(latField.value);
        const lng = parseFloat(lngField.value);
        if (isNaN(lat) || isNaN(lng)) return;

        try {
            const response = await fetch(`${activeUrl}?lat=${lat}&lng=${lng}`);
            activeNeeds = response.ok ? await response.json() : [];
        } catch {
            activeNeeds = [];
        }

        // Keep the choice if it is still here; after a failed submit, keep the need from the server
        const fromServer = parseInt(existingField.value);
        if (chosen && !activeNeeds.some(n => n.id === chosen.id)) chosen = null;
        if (!chosen && !isNaN(fromServer)) chosen = activeNeeds.find(n => n.id === fromServer) || null;
        if (!chosen && !userChoseNew && activeNeeds.length > 0) chosen = activeNeeds[0];

        renderBox();
        applyMode();
    }

    latField.addEventListener('change', checkPlace);

    // ---- The description: one line that grows while you write (up to about 5 lines) ----
    const description = document.getElementById('Description');
    function grow() {
        description.style.height = 'auto';
        description.style.height = Math.min(description.scrollHeight + 2, 140) + 'px';
    }
    description.addEventListener('input', grow);

    // ---- Big screens: the form fills the screen exactly, so the page doesn't scroll (the lines scroll in their card) ----
    // Docs: Element.getBoundingClientRect (MDN) - https://developer.mozilla.org/docs/Web/API/Element/getBoundingClientRect
    const layout = document.getElementById('needLayout');
    function fitToScreen() {
        if (window.innerWidth < 992) {
            layout.style.height = '';
        } else {
            const top = layout.getBoundingClientRect().top + window.scrollY;
            const footer = document.querySelector('footer');
            const below = (footer ? footer.offsetHeight : 0) + 24;
            layout.style.height = Math.max(520, window.innerHeight - top - below) + 'px';
        }
        if (typeof pickMap !== 'undefined') pickMap.invalidateSize(); // Leaflet must know the map changed size
    }
    window.addEventListener('resize', fitToScreen);
    window.addEventListener('resize', drawQuick); // big screens show all the categories

    // ---- Kontaktpunkt (help with AI): one short row over the map. «Endre» opens a small menu with «Samme sted som behovet»,
    // «Velg på kartet» and a name/explanation field (e.g. «inngang bak»). The flag is purple, with a dashed line to the need.
    // Docs: Bootstrap Dropdowns - https://getbootstrap.com/docs/5.3/components/dropdowns/
    // Docs: Leaflet DivIcon and Polyline - https://leafletjs.com/reference.html
    // Docs: Kartverket Adresse-API, punktsok - https://ws.geonorge.no/adresser/v1/#/default/get_punktsok
    const meetingName = document.getElementById('MeetingPointName');
    const meetingLat = document.getElementById('MeetingLatitude');
    const meetingLng = document.getElementById('MeetingLongitude');
    const meetingToggle = document.getElementById('meetingToggle');
    const meetingNoteBox = document.getElementById('meetingNoteBox');
    const meetingNameInput = document.getElementById('meetingNameInput');
    const pickBar = document.getElementById('meetingPickBar');
    const meetingMenu = () => bootstrap.Dropdown.getOrCreateInstance(meetingToggle);
    let meetingMarker = null;
    let meetingLine = null;

    function hasMeeting() {
        return meetingLat.value !== '' && meetingLng.value !== '';
    }

    // Distance in km between two points (the haversine formula)
    function distanceKm(lat1, lng1, lat2, lng2) {
        const rad = d => d * Math.PI / 180;
        const a = Math.sin(rad(lat2 - lat1) / 2) ** 2
            + Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(rad(lng2 - lng1) / 2) ** 2;
        return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    }

    function kmText(km) {
        return km < 1 ? Math.round(km * 100) * 10 + ' m' : km.toFixed(1).replace('.', ',') + ' km';
    }

    function drawMeeting() {
        tellMap();
        if (meetingMarker) { pickMap.removeLayer(meetingMarker); meetingMarker = null; }
        if (meetingLine) { pickMap.removeLayer(meetingLine); meetingLine = null; }

        // The row: the name and the distance from the need. The name field only when there is an own place
        const own = hasMeeting();
        const lat = parseFloat(meetingLat.value);
        const lng = parseFloat(meetingLng.value);
        const needLat = parseFloat(latField.value);
        const needLng = parseFloat(lngField.value);
        const hasNeed = !isNaN(needLat) && !isNaN(needLng);
        document.getElementById('meetingText').textContent = own ? (meetingName.value || 'Kontaktpunkt') : 'Samme sted som behovet';
        document.getElementById('meetingDistance').textContent = own && hasNeed ? ' · ' + kmText(distanceKm(needLat, needLng, lat, lng)) : '';
        meetingNoteBox.hidden = !own;
        updateSubmit();
        if (!own) return;

        meetingMarker = L.marker([lat, lng], {
            draggable: true,
            title: meetingName.value || 'Kontaktpunkt',
            icon: L.divIcon({ className: '', html: '<div class="meeting-flag"><i class="ti ti-flag" aria-hidden="true"></i></div>', iconSize: [26, 26], iconAnchor: [13, 13] })
        }).addTo(pickMap);
        meetingMarker.bindTooltip('⚑ ' + (meetingName.value || 'Kontaktpunkt'));
        meetingMarker.on('dragend', () => setMeeting(meetingMarker.getLatLng(), true));
        if (hasNeed) {
            meetingLine = L.polyline([[needLat, needLng], [lat, lng]], { color: '#7A5AF8', weight: 2, dashArray: '6 6', interactive: false }).addTo(pickMap);
        }
    }

    // A click on the map: the nearest address from Kartverket becomes the name (or «Valgt punkt»), then the menu opens
    // so the operator can add an explanation. keepName: the flag was only dragged
    async function setMeeting(latlng, keepName) {
        meetingLat.value = latlng.lat.toFixed(6);
        meetingLng.value = latlng.lng.toFixed(6);
        if (!keepName || !meetingName.value) meetingName.value = 'Valgt punkt';
        meetingNameInput.value = meetingName.value;
        drawMeeting();
        if (keepName) return;

        try {
            const url = `https://ws.geonorge.no/adresser/v1/punktsok?lat=${latlng.lat}&lon=${latlng.lng}&radius=200&treffPerSide=1&utkoordsys=4258`;
            const a = (await (await fetch(url)).json()).adresser?.[0];
            if (a?.adressetekst && meetingName.value === 'Valgt punkt' && meetingLat.value === latlng.lat.toFixed(6)) {
                meetingName.value = a.adressetekst;
                meetingNameInput.value = a.adressetekst;
                drawMeeting();
            }
        } catch {
            // No address: «Valgt punkt» is kept
        }
        meetingMenu().show();
        meetingNameInput.focus();
        meetingNameInput.setSelectionRange(meetingNameInput.value.length, meetingNameInput.value.length);
    }

    // «Velg på kartet»: a purple stripe over the map, and the next click sets the flag.
    // location-picker.js checks window.pickingMeeting, so the need's pin stays where it is
    function stopPicking() {
        window.pickingMeeting = false;
        pickBar.hidden = true;
        pickMap.getContainer().style.cursor = '';
        tellMap();
    }

    document.getElementById('meetingSame').addEventListener('click', () => {
        meetingLat.value = '';
        meetingLng.value = '';
        meetingName.value = '';
        meetingNameInput.value = '';
        stopPicking();
        meetingMenu().hide();
        drawMeeting();
    });

    document.getElementById('meetingPick').addEventListener('click', () => {
        meetingMenu().hide();
        window.pickingMeeting = true;
        pickBar.hidden = false;
        pickMap.getContainer().style.cursor = 'crosshair';
        pickBar.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    });
    document.getElementById('meetingPickCancel').addEventListener('click', stopPicking);
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && window.pickingMeeting) stopPicking(); });

    pickMap.on('click', e => {
        if (!window.pickingMeeting) return;
        stopPicking();
        setMeeting(e.latlng, false);
    });

    // The name or explanation the operators and owners see, e.g. «Lund skole, inngang bak». Enter closes the menu, not the form
    meetingNameInput.value = meetingName.value;
    meetingNameInput.addEventListener('input', () => {
        meetingName.value = meetingNameInput.value.trim();
        drawMeeting();
    });
    meetingNameInput.addEventListener('keydown', e => {
        if (e.key !== 'Enter') return;
        e.preventDefault();
        meetingMenu().hide();
    });

    // The dashed line follows the need when its pin moves
    latField.addEventListener('change', drawMeeting);

    // ---- «+ Info til innbyggere» (help with AI): an optional short text, published on the front page when the need is created ----
    const publicLink = document.getElementById('addPublic');
    if (publicLink) {
        const publicBox = document.getElementById('publicBox');
        const publicField = document.getElementById('PublicText');
        const openPublic = () => { publicBox.hidden = false; publicLink.hidden = true; };
        if (publicField.value) openPublic();
        publicLink.addEventListener('click', () => { openPublic(); publicField.focus(); });
        publicBox.querySelectorAll('[data-quick]').forEach(b => b.addEventListener('click', () => {
            publicField.value = b.dataset.quick;
            publicField.focus();
            publicField.setSelectionRange(publicField.value.length, publicField.value.length);
        }));
    }

    // ---- In the side panel on the main map (help with AI): the small map is hidden, and the need is placed on the big map.
    // The form tells the big map where the need is (pin, circle, kontaktpunkt); a click or a drag there sends the place back
    // Docs: Window.postMessage (MDN) - https://developer.mozilla.org/docs/Web/API/Window/postMessage
    function tellMap() {
        if (!inPanel) return;
        const lat = parseFloat(latField.value);
        const lng = parseFloat(lngField.value);
        parent.postMessage({
            type: 'need-place',
            lat: isNaN(lat) ? null : lat,
            lng: isNaN(lng) ? null : lng,
            radiusKm: chosen ? 0 : parseFloat(radiusSelect.value),
            planned: isPlanned(),
            meeting: hasMeeting() ? { lat: parseFloat(meetingLat.value), lng: parseFloat(meetingLng.value), name: meetingName.value } : null,
            pickingMeeting: !!window.pickingMeeting
        }, location.origin);
    }

    if (inPanel) {
        window.addEventListener('message', e => {
            if (e.origin !== location.origin || !e.data) return;
            if (!['need-map-click', 'need-place-picked', 'need-meeting-moved'].includes(e.data.type)) return;
            const latlng = L.latLng(e.data.lat, e.data.lng);
            if (e.data.type === 'need-map-click' && window.pickingMeeting) {
                // «Velg på kartet» for the kontaktpunkt: the click sets the flag
                stopPicking();
                setMeeting(latlng, false);
            } else if (e.data.type === 'need-meeting-moved') {
                setMeeting(latlng, true);
            } else {
                // The need's place: the same as a click on the small map (location-picker.js)
                setPosition(latlng.lat, latlng.lng);
                fillAddress(latlng.lat, latlng.lng);
                document.getElementById('geoStatus').textContent = 'Plassering valgt på kartet.';
            }
        });
        // «Velg på kartet» for the kontaktpunkt: the big map shows the purple stripe
        document.getElementById('meetingPick').addEventListener('click', tellMap);
        // The form is closed or saved: the big map removes the pin and the circle
        window.addEventListener('pagehide', () => parent.postMessage({ type: 'need-place', off: true }, location.origin));
    }

    drawMeeting();
    drawAll();
    applyUrgency();
    grow();
    fitToScreen();
    checkPlace();   // e.g. when the form opens with a place from the main map
    drawQuick();    // measure again now that the card has its height
})();