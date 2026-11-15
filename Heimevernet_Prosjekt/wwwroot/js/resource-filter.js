// The resource filter (in the side panel on Kart, and on Ressursoversikt): type, status and distance from a point
// with checkboxes and the number of hits. No "Bruk" button: the result updates as soon as you choose (help with AI)
// The choices are kept in the URL (types, status, where, lat, lng, radius) through site.js, so Kart and Ressursoversikt show the same.
// kommune is not chosen here any more (it is the area at the top of the lists), but «Vis på kart» sends it, so it still filters and has a chip
// Docs: URLSearchParams (MDN) - https://developer.mozilla.org/docs/Web/API/URLSearchParams
window.ResourceFilter = (function () {
    var statuses = ['Ledig', 'Ledig senere', 'I bruk', 'Lever tilbake', 'Ikke tilgjengelig', 'Ugyldig']; // as ResourceController.ShownStatus sends them
    var radiusMain = ['1', '2', '5', '10'];   // the chips; other distances under «Annen …»
    var allTypes = null; // the categories with their types from the server
    var openCategories = {}; // which categories are folded out (kept when the filter is drawn again)
    var showCustomRadius = false; // «Annen …» is open
    var lastResources = [];

    // "a,b" in the URL -> ['a', 'b']
    function list(value) {
        return value ? value.split(',') : [];
    }

    // "KRISTIANSAND" from Kartverket -> "Kristiansand"
    function titleCase(text) {
        return (text || '').toLowerCase().replace(/(^|[\s-])\p{L}/gu, function (letter) { return letter.toUpperCase(); });
    }

    // The radius in km, kept between 0.1 and 500 so a strange value in the URL can't break anything
    function radiusKm(params) {
        var km = parseFloat(params.radius);
        return Math.min(Math.max(isNaN(km) ? 2 : km, 0.1), 500);
    }

    // Distance in km between two points (Haversine formula) - the same as DistanceKm in ResourceController
    function distanceKm(lat1, lng1, lat2, lng2) {
        var toRad = Math.PI / 180;
        var dLat = (lat2 - lat1) * toRad;
        var dLng = (lng2 - lng1) * toRad;
        var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
            Math.cos(lat1 * toRad) * Math.cos(lat2 * toRad) * Math.sin(dLng / 2) * Math.sin(dLng / 2);
        return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    }

    function within(resource, params, km) {
        var lat = parseFloat(resource.latitude);
        var lng = parseFloat(resource.longitude);
        if (isNaN(lat) || isNaN(lng)) return false;
        return distanceKm(parseFloat(params.lat), parseFloat(params.lng), lat, lng) <= km;
    }

    // Does one resource match the choices? The text search is done by the server.
    // skip: leave one group out, so its own numbers show what you get if you choose more in that group
    function matches(resource, params, skip) {
        var types = list(params.types);
        var chosenStatus = list(params.status);
        var kommuner = list(params.kommune);
        if (skip !== 'types' && types.length > 0 && !types.includes(resource.type)) return false;
        if (skip !== 'status' && chosenStatus.length > 0 && !chosenStatus.includes(resource.status || 'Ledig')) return false;
        if (skip !== 'kommune' && kommuner.length > 0 && !kommuner.includes(titleCase(resource.municipality))) return false;
        if (skip !== 'radius' && params.lat && params.lng && !within(resource, params, radiusKm(params))) return false;
        if (skip !== 'reports' && params.reports === '1' && !(resource.reports > 0)) return false;
        return true;
    }

    function countBy(resources, params, skip, key) {
        var counts = {};
        resources.filter(function (r) { return matches(r, params, skip); }).forEach(function (r) {
            var value = r[key] || (key === 'status' ? 'Ledig' : 'Ukjent');
            if (value) counts[value] = (counts[value] || 0) + 1;
        });
        return counts;
    }

    // Change one choice; the map (or the list) is updated right away
    function change(name, values) {
        var params = window.getResourceParams();
        params[name] = values.join(',');
        delete params.type; // the old one-type filter from the menu
        window.applyResourceParams(params);
    }

    function setParams(edit) {
        var params = window.getResourceParams();
        edit(params);
        window.applyResourceParams(params);
    }

    // One checkbox row with the number in a pill; a chosen row gets the class is-picked
    function checkRow(id, text, count, checked, onChange) {
        var div = document.createElement('div');
        div.className = 'form-check';
        var input = document.createElement('input');
        input.type = 'checkbox';
        input.className = 'form-check-input';
        input.id = id;
        input.checked = checked;
        if (checked) div.classList.add('is-picked');
        input.addEventListener('change', function () { onChange(input.checked); });
        var label = document.createElement('label');
        label.className = 'form-check-label d-flex w-100';
        label.htmlFor = id;
        label.textContent = text; // textContent: a name from a user can't run as HTML
        var pill = document.createElement('span');
        pill.className = 'rf-pill ms-auto';
        pill.textContent = count;
        label.appendChild(pill);
        div.append(input, label);
        return div;
    }

    // One group of checkboxes with the number after each, e.g. "Ledig 25"
    function renderGroup(boxId, name, values, counts, chosen) {
        var box = document.getElementById(boxId);
        box.className = '';
        box.replaceChildren();
        values.forEach(function (value, i) {
            box.appendChild(checkRow(boxId + i, value, counts[value] || 0, chosen.includes(value), function (checked) {
                var now = chosen.filter(function (v) { return v !== value; });
                if (checked) now.push(value);
                change(name, now);
            }));
        });
    }

    // "Type ressurs" grouped by category: ▸/▾ folds out, the number is the sum, and the category's own checkbox
    // chooses all its types at once (half-ticked when only some are chosen). One type: just a checkbox
    // Docs: indeterminate checkbox (MDN) - https://developer.mozilla.org/docs/Web/HTML/Element/input/checkbox#indeterminate_state_checkboxes
    function renderTypeGroups(categories, counts, chosen) {
        var box = document.getElementById('rfTypes');
        box.className = 'rf-cats';
        box.replaceChildren();
        categories.forEach(function (category, c) {
            var types = category.types;
            var picked = types.filter(function (t) { return chosen.includes(t); });
            var sum = types.reduce(function (n, t) { return n + (counts[t] || 0); }, 0);
            if (openCategories[category.name] === undefined) openCategories[category.name] = picked.length > 0;
            var open = openCategories[category.name] && types.length > 1;

            var row = document.createElement('div');
            row.className = 'rf-cat';
            if (picked.length > 0) row.classList.add('is-picked');
            var input = document.createElement('input');
            input.type = 'checkbox';
            input.className = 'form-check-input';
            input.id = 'rfCat' + c;
            input.checked = picked.length === types.length;
            input.indeterminate = picked.length > 0 && picked.length < types.length;
            input.addEventListener('change', function () {
                var rest = chosen.filter(function (t) { return !types.includes(t); });
                openCategories[category.name] = input.checked; // ticked: fold out, so you see the types
                change('types', input.checked ? rest.concat(types) : rest);
            });
            var label = document.createElement('label');
            label.className = 'form-check-label rf-cat-name';
            label.htmlFor = input.id;
            label.textContent = types.length === 1 ? types[0] : category.name;
            var count = document.createElement('span');
            count.className = 'rf-pill ms-auto';
            count.textContent = sum;
            var icon = document.createElement('i');
            icon.className = 'ti ti-' + (category.icon || 'category').replace(/^ti-/, '') + ' rf-cat-icon';
            icon.setAttribute('aria-hidden', 'true');
            row.append(input, icon, label, count);

            if (types.length > 1) {
                var toggle = document.createElement('button');
                toggle.type = 'button';
                toggle.id = 'rfCatToggle' + c;
                toggle.className = 'rf-cat-toggle';
                toggle.textContent = open ? '▾' : '▸';
                toggle.setAttribute('aria-expanded', open);
                toggle.setAttribute('aria-label', (open ? 'Skjul' : 'Vis') + ' typene i ' + category.name);
                toggle.addEventListener('click', function () {
                    openCategories[category.name] = !open;
                    renderTypeGroups(categories, counts, chosen);
                    document.getElementById('rfCatToggle' + c).focus();
                });
                row.prepend(toggle);
            }
            box.appendChild(row);

            if (!open) return;
            var inner = document.createElement('div');
            inner.className = 'rf-cat-types';
            types.forEach(function (type, i) {
                inner.appendChild(checkRow('rfCat' + c + '_' + i, type, counts[type] || 0, chosen.includes(type), function (checked) {
                    var now = chosen.filter(function (t) { return t !== type; });
                    if (checked) now.push(type);
                    change('types', now);
                }));
            });
            box.appendChild(inner);
        });
    }

    // "Feilmeldinger" (admin and operator, Kart and Ressursoversikt): only resources with open "feil info" messages
    function renderReports(resources, params) {
        var box = document.getElementById('rfReports');
        if (!box) return; // not admin/operator
        var count = resources.filter(function (r) { return matches(r, params, 'reports') && r.reports > 0; }).length;
        box.replaceChildren(checkRow('rfReportsOnly', '⚑ Kun med åpne feilmeldinger', count, params.reports === '1', function (checked) {
            setParams(function (p) { if (checked) p.reports = '1'; else delete p.reports; });
        }));
    }

    // "2" -> "2 km", "0.5" -> "500 m"
    function radiusText(km) {
        return parseFloat(km) < 1 ? Math.round(parseFloat(km) * 1000) + ' m' : String(km).replace('.', ',') + ' km';
    }

    // "Avstand fra et punkt" (help with AI): before a place is chosen only the field (with ⌖ inside) and «Bruk min posisjon».
    // After: the place as a tag with ✕, and the distances as chips with the number of resources within each,
    // like «Ressurser i området» on Kart. «Annen …» opens a field for an own distance, e.g. 7,5 km
    function renderPlace(resources, params) {
        var hasPoint = !!(params.lat && params.lng);
        document.getElementById('rfFromSearch').hidden = hasPoint;
        document.getElementById('rfFromChosen').hidden = !hasPoint;
        if (hasPoint) {
            var where = params.where || 'valgt punkt';
            document.getElementById('rfFromName').textContent = where === 'din posisjon' ? 'Din posisjon' : where;
            document.getElementById('rfFromIcon').className = 'ti ' + (where === 'din posisjon' ? 'ti-current-location' : 'ti-map-pin');
            var mapLink = document.getElementById('rfFromMap');
            if (mapLink) mapLink.href = mapLink.dataset.mapUrl + '?' + new URLSearchParams(params);
            showHint(''); // a place is chosen: an old message is no longer true
        }

        var current = hasPoint ? String(radiusKm(params)) : '';
        var values = radiusMain.slice();
        if (current && !values.includes(current)) values.push(current); // an own distance, e.g. 7.5 km
        values.sort(function (a, b) { return parseFloat(a) - parseFloat(b); });
        var box = document.getElementById('rfRadiusList');
        box.replaceChildren();
        box.hidden = !hasPoint;
        if (!hasPoint) {
            document.getElementById('rfRadiusCustom').hidden = true;
            return;
        }
        values.forEach(function (value) {
            var on = value === current;
            var chip = document.createElement('button');
            chip.type = 'button';
            chip.className = 'rf-km' + (on ? ' is-on' : '');
            chip.setAttribute('aria-pressed', on ? 'true' : 'false');
            var count = resources.filter(function (r) { return matches(r, params, 'radius') && within(r, params, parseFloat(value)); }).length;
            chip.textContent = (on ? '✓ ' : '') + radiusText(value) + ' '; // textContent: never runs as HTML
            var small = document.createElement('small');
            small.textContent = count;
            chip.appendChild(small);
            chip.setAttribute('aria-label', 'Innen ' + radiusText(value) + ', ' + count + (count === 1 ? ' ressurs' : ' ressurser'));
            chip.addEventListener('click', function () { setParams(function (p) { p.radius = value; }); });
            box.appendChild(chip);
        });
        var other = document.createElement('button');
        other.type = 'button';
        other.className = 'rf-km' + (showCustomRadius ? ' is-on' : '');
        other.setAttribute('aria-expanded', showCustomRadius ? 'true' : 'false');
        other.setAttribute('aria-controls', 'rfRadiusCustom');
        other.textContent = 'Annen …';
        other.addEventListener('click', function () {
            showCustomRadius = !showCustomRadius;
            renderPlace(lastResources, window.getResourceParams());
            if (showCustomRadius) document.getElementById('rfRadiusOwn').focus();
        });
        box.appendChild(other);
        document.getElementById('rfRadiusCustom').hidden = !showCustomRadius;
    }

    // A short message under the field: «Henter posisjonen din …», or a warning when the position doesn't work ('' = none)
    function showHint(text, warning) {
        var hint = document.getElementById('rfRadiusHint');
        hint.hidden = !text;
        hint.classList.toggle('rf-warn', !!warning);
        hint.replaceChildren();
        if (!text) return;
        if (warning) {
            var icon = document.createElement('i');
            icon.className = 'ti ti-alert-triangle';
            icon.setAttribute('aria-hidden', 'true');
            hint.append(icon, ' ');
        }
        hint.append(text);
    }

    // One chip; a click removes that choice (or all of them for "Nullstill alt")
    function makeChip(text, remove, isReset) {
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'rf-chip' + (isReset ? ' rf-chip-reset' : '');
        button.textContent = isReset ? text : text + ' ✕'; // textContent: a type name can't run as HTML
        button.setAttribute('aria-label', isReset ? text : 'Fjern filteret ' + text);
        button.addEventListener('click', function () { setParams(remove); });
        return button;
    }

    // Chips over the map (or the list) with what is chosen; ✕ on a chip removes that one choice.
    // Returns the number of choices, for the Filter button
    function renderChips(params) {
        var chips = [];
        function without(name, value) {
            return function (p) { p[name] = list(p[name]).filter(function (x) { return x !== value; }).join(','); };
        }
        list(params.types).forEach(function (t) { chips.push([t, without('types', t)]); });
        list(params.status).forEach(function (s) { chips.push([s, without('status', s)]); });
        list(params.kommune).forEach(function (k) { chips.push([k + ' kommune', without('kommune', k)]); });
        if (params.reports === '1') chips.push(['Kun med feilmeldinger', function (p) { delete p.reports; }]);
        if (params.lat) {
            chips.push(['Innen ' + radiusText(radiusKm(params)) + ' fra ' + (params.where || 'valgt punkt'),
            function (p) { delete p.lat; delete p.lng; delete p.where; delete p.radius; }]);
        }
        list(params.hide).forEach(function (h) { chips.push([h === 'needs' ? 'Behov skjult' : h === 'actors' ? 'Offentlige aktører skjult' : 'Ressurser skjult', without('hide', h)]); });

        var box = document.getElementById('rfChips');
        if (box) {
            box.replaceChildren();
            chips.forEach(function (chip) { box.appendChild(makeChip(chip[0], chip[1])); });
            if (chips.length > 1) {
                box.appendChild(makeChip('Nullstill alt', function (p) {
                    Object.keys(p).forEach(function (k) { if (k !== 'search') delete p[k]; });
                }, true));
            }
            box.hidden = chips.length === 0;
        }
        return chips.length;
    }

    // Draw the filter from the resources the server sent (after the text search)
    async function render(resources, params) {
        var panel = document.getElementById('rfPanel');
        if (!panel) return;
        lastResources = resources;
        if (!allTypes) {
            try { allTypes = await (await fetch(panel.dataset.typesUrl)).json(); } catch (e) { allTypes = []; }
        }
        renderTypeGroups(allTypes, countBy(resources, params, 'types', 'type'), list(params.types));
        renderGroup('rfStatus', 'status', statuses, countBy(resources, params, 'status', 'status'), list(params.status));
        renderPlace(resources, params);
        renderReports(resources, params);

        // "Vis X treff", or "Vis kartet" while the resources are hidden on the map
        var hitCount = resources.filter(function (r) { return matches(r, params); }).length;
        var show = document.getElementById('rfShow');
        if (show) show.textContent = list(params.hide).includes('resources') ? 'Vis kartet' : 'Vis ' + hitCount + ' treff';

        // The chips, and the number of choices on the Filter button
        var active = renderChips(params);
        var badge = document.getElementById('rfCount');
        if (badge) {
            badge.textContent = active;
            badge.hidden = active === 0;
        }
        // The same number on the Filter tab in the side panel on Kart
        var tabBadge = document.getElementById('tabFilterCount');
        if (tabBadge) { tabBadge.textContent = active; tabBadge.hidden = active === 0; }
        // "Nullstill alt" at the top is grey while nothing is chosen
        var topReset = document.getElementById('rfResetTop');
        if (topReset) topReset.disabled = active === 0;
    }

    // The "Fra" field: places from Kartverket while you type (the same search as in the menu) (help with AI)
    function initFromSearch() {
        var input = document.getElementById('rfFrom');
        var listBox = document.getElementById('rfFromList');
        var suggestUrl = document.getElementById('globalSearchForm')?.dataset.suggestUrl;
        if (!input || !suggestUrl) return;
        var timer = null;
        input.addEventListener('input', function () { showHint(''); }); // typing: the warning goes away

        function close() {
            listBox.hidden = true;
            listBox.replaceChildren();
            input.setAttribute('aria-expanded', 'false');
        }

        function choose(place) {
            close();
            input.value = '';
            setParams(function (p) {
                p.where = place.name;
                p.lat = place.latitude;
                p.lng = place.longitude;
                p.radius = p.radius || '2';
            });
        }

        input.addEventListener('input', function () {
            clearTimeout(timer);
            var text = input.value.trim();
            if (text.length < 2) { close(); return; }
            timer = setTimeout(async function () {
                try {
                    var data = await (await fetch(suggestUrl + '?q=' + encodeURIComponent(text))).json();
                    listBox.replaceChildren();
                    data.places.forEach(function (place) {
                        var button = document.createElement('button');
                        button.type = 'button';
                        button.className = 'rf-from-item';
                        var name = document.createElement('span');
                        name.textContent = place.name;
                        var small = document.createElement('small');
                        small.textContent = place.description || '';
                        button.append(name, small);
                        button.addEventListener('click', function () { choose(place); });
                        listBox.appendChild(button);
                    });
                    if (data.places.length === 0) listBox.innerHTML = '<div class="rf-from-empty">Fant ikke stedet</div>';
                    listBox.hidden = false;
                    input.setAttribute('aria-expanded', 'true');
                } catch (e) { close(); }
            }, 250);
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') close();
            if (e.key === 'ArrowDown' && !listBox.hidden) { e.preventDefault(); listBox.querySelector('button')?.focus(); }
        });
        listBox.addEventListener('keydown', function (e) {
            var buttons = Array.from(listBox.querySelectorAll('button'));
            var i = buttons.indexOf(document.activeElement);
            if (e.key === 'ArrowDown' && i < buttons.length - 1) { e.preventDefault(); buttons[i + 1].focus(); }
            if (e.key === 'ArrowUp') { e.preventDefault(); (i > 0 ? buttons[i - 1] : input).focus(); }
            if (e.key === 'Escape') { close(); input.focus(); }
        });
        document.addEventListener('click', function (e) {
            if (!document.getElementById('rfFromSearch').contains(e.target)) close();
        });
    }

    // The buttons in the panel; set up once
    function init() {
        if (!document.getElementById('rfPanel')) return;
        initFromSearch();

        // ✕ on the chosen place: no distance any more
        document.getElementById('rfFromClear').addEventListener('click', function () {
            setParams(function (p) { delete p.lat; delete p.lng; delete p.where; delete p.radius; });
        });

        // «Bruk min posisjon» (the link and ⌖ in the field): only when the user says yes. A radius of 2 km around you to start with
        // Docs: Geolocation API (MDN) - https://developer.mozilla.org/docs/Web/API/Geolocation_API
        function useMyPosition() {
            if (!navigator.geolocation) { showHint('Nettleseren din støtter ikke posisjon. Skriv et sted i stedet.', true); return; }
            showHint('Henter posisjonen din …');
            navigator.geolocation.getCurrentPosition(function (pos) {
                showHint('');
                setParams(function (p) {
                    p.where = 'din posisjon';
                    p.lat = pos.coords.latitude.toFixed(5);
                    p.lng = pos.coords.longitude.toFixed(5);
                    p.radius = p.radius || '2';
                });
            }, function () { showHint('Fikk ikke bruke posisjonen. Skriv et sted i stedet.', true); });
        }
        document.getElementById('rfMyPosition').addEventListener('click', useMyPosition);
        document.getElementById('rfMyPositionIcon').addEventListener('click', useMyPosition);

        // "Velg punkt på kartet" (only on Kart)
        var pick = document.getElementById('rfPick');
        if (pick) {
            pick.addEventListener('click', function () {
                window.startPickCenter(function (lat, lng) {
                    setParams(function (p) {
                        p.where = 'valgt punkt';
                        p.lat = lat.toFixed(5);
                        p.lng = lng.toFixed(5);
                        p.radius = p.radius || '2';
                    });
                });
            });
        }

        // «Annen …»: an own distance, e.g. 7,5 km (kept between 0.1 and 500). «Bruk» or Enter
        function useOwnRadius() {
            var km = parseFloat(document.getElementById('rfRadiusOwn').value.replace(',', '.'));
            if (isNaN(km) || !window.getResourceParams().lat) return;
            showCustomRadius = false;
            setParams(function (p) { p.radius = String(Math.min(Math.max(km, 0.1), 500)); });
        }
        document.getElementById('rfRadiusUse').addEventListener('click', useOwnRadius);
        document.getElementById('rfRadiusOwn').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); useOwnRadius(); }
        });

        // Nullstill alt (at the top and at the bottom): removes every choice, but keeps what is written in the search field
        function resetAll() {
            var params = window.getResourceParams();
            window.applyResourceParams(params.search ? { search: params.search } : {});
        }
        document.getElementById('rfReset').addEventListener('click', resetAll);
        document.getElementById('rfResetTop')?.addEventListener('click', resetAll);

        // "Vis X treff": close the panel and look at the map
        var show = document.getElementById('rfShow');
        if (show) show.addEventListener('click', function () { closeSidePanel(); });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();

    return { matches: matches, render: render, distanceKm: distanceKm };
})();