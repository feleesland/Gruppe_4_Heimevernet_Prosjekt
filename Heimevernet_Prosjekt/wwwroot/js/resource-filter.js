// The resource filter (in the side panel on Kart, later also on Ressursoversikt): type and status with checkboxes
// and the number of hits, and the distance. No "Bruk" button: the result updates as soon as you choose (help with AI)
// The choices are kept in the URL (types, status, lat, lng, radius) through site.js, so Kart and Ressursoversikt show the same
// Docs: URLSearchParams (MDN) - https://developer.mozilla.org/docs/Web/API/URLSearchParams
window.ResourceFilter = (function () {
    var statuses = ['Ledig', 'Opptatt', 'Ikke tilgjengelig', 'Ugyldig'];
    var allTypes = null; // all types from the server, also the ones with 0 hits

    // "a,b" in the URL -> ['a', 'b']
    function list(value) {
        return value ? value.split(',') : [];
    }

    // Does one resource match the chosen types and statuses? Search and distance are already done by the server.
    // skip: leave one group out, so its own numbers show what you get if you tick more in that group
    function matches(resource, params, skip) {
        var types = list(params.types);
        var chosenStatus = list(params.status);
        if (skip !== 'types' && types.length > 0 && !types.includes(resource.type)) return false;
        if (skip !== 'status' && chosenStatus.length > 0 && !chosenStatus.includes(resource.status || 'Ledig')) return false;
        return true;
    }

    function countBy(resources, params, skip, key) {
        var counts = {};
        resources.filter(function (r) { return matches(r, params, skip); }).forEach(function (r) {
            var value = r[key] || (key === 'status' ? 'Ledig' : 'Ukjent');
            counts[value] = (counts[value] || 0) + 1;
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

    // One group of checkboxes with the number after each, e.g. "Kjøretøy 4"
    function renderGroup(boxId, name, values, counts, chosen) {
        var box = document.getElementById(boxId);
        box.className = '';
        box.replaceChildren();
        values.forEach(function (value, i) {
            var div = document.createElement('div');
            div.className = 'form-check';
            var input = document.createElement('input');
            input.type = 'checkbox';
            input.className = 'form-check-input';
            input.id = boxId + i;
            input.checked = chosen.includes(value);
            input.addEventListener('change', function () {
                var now = chosen.filter(function (v) { return v !== value; });
                if (input.checked) now.push(value);
                change(name, now);
            });
            var label = document.createElement('label');
            label.className = 'form-check-label d-flex w-100';
            label.htmlFor = input.id;
            label.textContent = value; // textContent: a name from a user can't run as HTML
            var count = document.createElement('span');
            count.className = 'ms-auto text-body-secondary';
            count.textContent = counts[value] || 0;
            label.appendChild(count);
            div.append(input, label);
            box.appendChild(div);
        });
    }

    // "2" -> "2 km", "0.5" -> "500 m"
    function radiusText(km) {
        return parseFloat(km) < 1 ? (parseFloat(km) * 1000) + ' m' : km + ' km';
    }

    // One chip; a click removes that choice (or all of them for "Nullstill alt")
    function makeChip(text, remove, isReset) {
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'rf-chip' + (isReset ? ' rf-chip-reset' : '');
        button.textContent = isReset ? text : text + ' ✕'; // textContent: a type name can't run as HTML
        button.setAttribute('aria-label', isReset ? text : 'Fjern filteret ' + text);
        button.addEventListener('click', function () {
            var params = window.getResourceParams();
            remove(params);
            window.applyResourceParams(params);
        });
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
        if (params.lat) {
            chips.push(['Innen ' + radiusText(params.radius || 2) + ' fra ' + (params.where || 'valgt punkt'),
            function (p) { delete p.lat; delete p.lng; delete p.where; delete p.radius; }]);
        }
        list(params.hide).forEach(function (h) { chips.push([h === 'needs' ? 'Behov skjult' : 'Ressurser skjult', without('hide', h)]); });

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

    // Draw the filter from the resources the server sent (after search and distance)
    async function render(resources, params) {
        var panel = document.getElementById('rfPanel');
        if (!panel) return;
        if (!allTypes) {
            try { allTypes = await (await fetch(panel.dataset.typesUrl)).json(); } catch (e) { allTypes = []; }
        }
        renderGroup('rfTypes', 'types', allTypes, countBy(resources, params, 'types', 'type'), list(params.types));
        renderGroup('rfStatus', 'status', statuses, countBy(resources, params, 'status', 'status'), list(params.status));

        // "Vis X treff", or "Vis kartet" while the resources are hidden on the map
        var hitCount = resources.filter(function (r) { return matches(r, params); }).length;
        var show = document.getElementById('rfShow');
        if (show) show.textContent = list(params.hide).includes('resources') ? 'Vis kartet' : 'Vis ' + hitCount + ' treff';

        // Distance: the place it is measured from, and the chosen radius
        if (params.radius) document.getElementById('rfRadius').value = params.radius;
        document.getElementById('rfPlace').textContent = params.lat
            ? 'Fra: ' + (params.where || 'valgt punkt')
            : 'Velg et sted i søket, eller et punkt på kartet.';

        // The chips, and the number of choices on the Filter button
        var active = renderChips(params);
        var badge = document.getElementById('rfCount');
        if (badge) {
            badge.textContent = active;
            badge.hidden = active === 0;
        }
    }

    // The buttons in the panel; set up once
    function init() {
        if (!document.getElementById('rfPanel')) return;

        document.getElementById('rfRadius').addEventListener('change', function () {
            var params = window.getResourceParams();
            if (!params.lat) return; // no place chosen yet: the radius is used when you choose one
            params.radius = this.value;
            window.applyResourceParams(params);
        });

        var pick = document.getElementById('rfPick');
        if (pick) {
            pick.addEventListener('click', function () {
                window.startPickCenter(function (lat, lng) {
                    var params = window.getResourceParams();
                    params.where = 'valgt punkt';
                    params.lat = lat.toFixed(5);
                    params.lng = lng.toFixed(5);
                    params.radius = document.getElementById('rfRadius').value;
                    window.applyResourceParams(params);
                });
            });
        }

        // Nullstill alt: removes every choice, but keeps what is written in the search field
        document.getElementById('rfReset').addEventListener('click', function () {
            var params = window.getResourceParams();
            window.applyResourceParams(params.search ? { search: params.search } : {});
        });

        // "Vis X treff": close the panel and look at the map
        var show = document.getElementById('rfShow');
        if (show) show.addEventListener('click', function () { closeSidePanel(); });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();

    return { matches: matches, render: render };
})();