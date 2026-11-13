// "Ny ressurs" / "Endre ressurs" as a wizard: one step at a time, and only the questions for the chosen type (help with AI)
// Docs: HTMLElement.hidden - https://developer.mozilla.org/docs/Web/API/HTMLElement/hidden
(() => {
    const form = document.getElementById('resourceWizard');
    if (!form) return;

    const steps = [...form.querySelectorAll('.wizard-step')];
    const labels = [...document.querySelectorAll('.wizard-steps li')];
    const backButton = form.querySelector('[data-wizard-back]');
    const nextButton = form.querySelector('[data-wizard-next]');
    const submitButtons = [...form.querySelectorAll('[data-wizard-submit]')];
    const stepCount = form.querySelector('[data-step-count]');
    const typeError = document.getElementById('typeError');
    const noQuestions = form.querySelector('[data-no-questions]');
    const last = steps.length - 1;
    let current = 0;
    let pickedWithPointer = false;

    function chosenType() {
        return form.querySelector('input[name="Type"]:checked')?.value ?? '';
    }

    // "KRISTIANSAND S" from Kartverket is shown as "Kristiansand S"
    function titleCase(text) {
        return (text ?? '').toLowerCase().replace(/(^|[\s-])\p{L}/gu, letter => letter.toUpperCase());
    }

    // Shows the chosen type's questions. The other types' fields are disabled, so they are not sent
    function showQuestions() {
        const type = chosenType();
        let found = false;
        form.querySelectorAll('[data-questions-for]').forEach(block => {
            const match = block.dataset.questionsFor === type;
            if (match) found = true;
            block.hidden = !match;
            block.querySelectorAll('input, select').forEach(field => field.disabled = !match);
        });
        noQuestions.hidden = found;
    }

    function showStep(index, moveFocus = true) {
        current = Math.max(0, Math.min(index, last));
        steps.forEach((step, i) => step.hidden = i !== current);
        labels.forEach((label, i) => {
            label.classList.toggle('is-current', i === current);
            label.classList.toggle('is-done', i < current);
            if (i === current) label.setAttribute('aria-current', 'step');
            else label.removeAttribute('aria-current');
        });
        backButton.hidden = current === 0;
        nextButton.hidden = current === last;
        submitButtons.forEach(button => button.hidden = current !== last);
        stepCount.textContent = `Steg ${current + 1} av ${steps.length}`;

        // The map was made while its step was hidden, so it must measure itself again - Docs: Leaflet invalidateSize
        // https://leafletjs.com/reference.html#map-invalidatesize
        if (current === last && typeof pickMap !== 'undefined') {
            pickMap.invalidateSize();
            const lat = parseFloat(document.getElementById('Latitude').value);
            const lng = parseFloat(document.getElementById('Longitude').value);
            if (!isNaN(lat) && !isNaN(lng)) pickMap.setView([lat, lng], 16);
        }

        if (moveFocus) {
            window.scrollTo({ top: 0 });
            steps[current].querySelector('h2')?.focus();
        }
        // The summary on the right follows the step
        updateSummary();
    }

    // Checks the fields in the step before going on. Step 1: a type must be chosen.
    // Other steps: jQuery Validation checks the fields with rules (the name in step 3) - Docs: https://jqueryvalidation.org/valid/
    function stepIsValid() {
        if (current === 0) {
            const ok = chosenType() !== '';
            typeError.hidden = ok;
            return ok;
        }
        let ok = true;
        if (window.jQuery && jQuery.fn.valid) {
            steps[current].querySelectorAll('[data-val="true"]').forEach(field => {
                if (!jQuery(field).valid()) ok = false;
            });
        }
        return ok;
    }

    nextButton.addEventListener('click', () => {
        if (stepIsValid()) showStep(current + 1);
    });
    backButton.addEventListener('click', () => showStep(current - 1));

    // Step 1: pressing a category opens its types right under the row, like a tab. Only one is open at a time,
    // and pressing the open one again closes it (help with AI)
    const categoryButtons = [...form.querySelectorAll('[data-category]')];
    const typePanels = [...form.querySelectorAll('[data-types-for]')];

    function openCategory(id) {
        categoryButtons.forEach(button => {
            const open = button.dataset.category === id;
            button.classList.toggle('is-open', open);
            button.setAttribute('aria-expanded', open);
        });
        typePanels.forEach(panel => panel.hidden = panel.dataset.typesFor !== id);
    }

    // The card shows "✓ Bil valgt" when one of its types is chosen, otherwise the types
    function markPicked() {
        categoryButtons.forEach(button => {
            const picked = form.querySelector(`[data-types-for="${button.dataset.category}"] input[name="Type"]:checked`);
            button.querySelector('[data-count]').hidden = !!picked;
            button.querySelector('[data-picked]').hidden = !picked;
            button.querySelector('[data-picked-name]').textContent = picked ? picked.value : '';
        });
    }

    // A category with only one type (Sjø, Annet) picks it straight away, the others open their panel
    categoryButtons.forEach(button => button.addEventListener('click', () => {
        const radios = form.querySelectorAll(`[data-types-for="${button.dataset.category}"] input[name="Type"]`);
        if (radios.length === 1) {
            radios[0].checked = true;
            openCategory(null);
            pickedWithPointer = true;
            radios[0].dispatchEvent(new Event('change'));
            return;
        }
        openCategory(button.classList.contains('is-open') ? null : button.dataset.category);
    }));

    // Clicking a type card goes straight to its questions. With the keyboard (arrow keys) it only selects,
    // so the user can look through the list and press "Neste"
    form.querySelectorAll('.type-card').forEach(card => card.addEventListener('pointerdown', () => pickedWithPointer = true));
    form.querySelectorAll('input[name="Type"]').forEach(radio => radio.addEventListener('change', () => {
        typeError.hidden = true;
        markPicked();
        showQuestions();
        if (pickedWithPointer) showStep(1);
        pickedWithPointer = false;
    }));

    // Enter in a field goes to the next step instead of sending the form too early.
    // In the address fields in the last step, Enter searches on the map
    form.addEventListener('keydown', e => {
        if (e.key !== 'Enter' || e.target.tagName !== 'INPUT' || e.target.type === 'file') return;
        if (current < last) {
            e.preventDefault();
            nextButton.click();
        } else if (['Address', 'ZipCode', 'City'].includes(e.target.id)) {
            e.preventDefault();
            document.getElementById('findOnMap').click();
        }
    });

    // Older resources have no municipality yet: get it from Kartverket, without changing the address
    // Docs: Kartverket Adresse-API, punktsok - https://ws.geonorge.no/adresser/v1/#/default/get_punktsok
    const municipality = document.getElementById('Municipality');
    const startLat = document.getElementById('Latitude').value;
    const startLng = document.getElementById('Longitude').value;
    if (municipality && !municipality.value && startLat && startLng) {
        fetch(`https://ws.geonorge.no/adresser/v1/punktsok?lat=${startLat}&lon=${startLng}&radius=200&treffPerSide=1&utkoordsys=4258`)
            .then(response => response.json())
            .then(result => {
                const name = result.adresser?.[0]?.kommunenavn;
                if (name && !municipality.value) municipality.value = name;
                updateSummary();
            })
            .catch(() => { /* no municipality found, the rest is saved anyway */ });
    }

    // "Din ressurs" on the right: what is chosen so far, with ✓ and "Endre" on the steps that are done (help with AI)
    const summary = form.querySelector('[data-summary]');
    const summaryImg = form.querySelector('[data-summary-img]');
    const summaryIcon = form.querySelector('[data-summary-icon]');
    const stepNames = ['Type', 'Om ressursen', 'Hvor'];
    const narrow = window.matchMedia('(max-width: 991.98px)');

    // The answers to the chosen type's questions in short form: "✓ Hengerfeste · Antall seter: 5"
    function answersText() {
        const block = [...form.querySelectorAll('[data-questions-for]')].find(b => b.dataset.questionsFor === chosenType());
        if (!block) return '';
        const parts = [];
        block.querySelectorAll('.wizard-question').forEach(question => {
            const label = question.querySelector('legend, label')?.textContent.trim() ?? '';
            const field = question.querySelector('input[type="number"], input[type="text"], select');
            const unit = question.querySelector('.input-group-text')?.textContent ?? '';
            if (question.querySelector('input[value="true"]:checked')) parts.push('✓ ' + label);
            else if (field && field.value) parts.push(`${label}: ${field.value} ${unit}`.trim());
        });
        return parts.join(' · ');
    }

    function updateSummary() {
        const typeRadio = form.querySelector('input[name="Type"]:checked');
        const name = document.getElementById('Name').value.trim();
        const cityField = document.getElementById('City');
        // Kartverket fills in the place in capitals: shown as "Kristiansand S" (help with AI)
        if (cityField.value && cityField.value === cityField.value.toUpperCase()) cityField.value = titleCase(cityField.value);
        const place = [document.getElementById('Address').value, cityField.value].filter(Boolean).join(', ');
        const answers = answersText();
        const texts = [
            typeRadio ? typeRadio.value + (typeRadio.dataset.categoryName ? ' · ' + typeRadio.dataset.categoryName : '') : '',
            name ? [name, answers].filter(Boolean).join(' · ') : '',
            document.getElementById('Latitude').value ? (place || 'Valgt på kartet') : ''
        ];

        form.querySelectorAll('[data-summary-step]').forEach((item, i) => {
            const now = i === current;
            const done = !now && texts[i] !== '';
            item.querySelector('[data-summary-text]').textContent = texts[i] || stepNames[i];
            item.querySelector('[data-summary-mark]').className = 'ti ' + (done ? 'ti-circle-check' : now ? 'ti-circle-dot' : 'ti-circle-dashed');
            item.classList.toggle('is-now', now);
            item.classList.toggle('is-done', done);
            if (now) item.setAttribute('aria-current', 'step');
            else item.removeAttribute('aria-current');
            item.querySelector('[data-goto]').hidden = !done;
        });

        // The picture if one is chosen, otherwise the type's icon (also at the top of step 2)
        const icon = typeRadio?.dataset.icon || 'ti-photo';
        const hasPicture = !!summaryImg.getAttribute('src');
        summaryImg.hidden = !hasPicture;
        summaryIcon.hidden = hasPicture;
        summaryIcon.className = 'ti ' + icon;
        form.querySelector('[data-type-icon]').className = 'ti ' + (typeRadio ? icon : 'ti-list-details');
        form.querySelector('[data-summary-short]').textContent = (name || typeRadio?.value) ? ': ' + (name || typeRadio.value) : '';

        // The name field's hint follows the type: "F.eks. Honda EU22i 2,2 kW"
        if (typeRadio?.dataset.example) document.getElementById('Name').placeholder = 'F.eks. ' + typeRadio.dataset.example;

        // "✓ Kristiansand kommune" next to the map buttons when the place is found
        const placeLine = form.querySelector('[data-place]');
        placeLine.hidden = !municipality?.value;
        form.querySelector('[data-place-name]').textContent = titleCase(municipality?.value) + ' kommune';
    }

    // A new picture is shown right away, before it is saved - Docs: URL.createObjectURL
    // https://developer.mozilla.org/docs/Web/API/URL/createObjectURL_static
    document.getElementById('Image')?.addEventListener('change', e => {
        const file = e.target.files?.[0];
        if (file) summaryImg.src = URL.createObjectURL(file);
        updateSummary();
    });

    form.addEventListener('input', updateSummary);
    form.addEventListener('change', updateSummary);
    // The address comes from Kartverket a moment after the map click, so the summary is updated again then
    document.getElementById('Latitude').addEventListener('change', () => setTimeout(updateSummary, 1200));
    form.querySelectorAll('[data-goto]').forEach(button => button.addEventListener('click', () => showStep(Number(button.dataset.goto))));

    // Phones: the summary starts folded as one line. On a PC it is always open
    if (narrow.matches) summary.open = false;
    summary.querySelector('summary').addEventListener('click', e => {
        if (!narrow.matches) e.preventDefault();
    });

    // Start: the chosen type's category is open (Edit), otherwise all are closed.
    // Then the step with a server error (e.g. missing name), otherwise step 1
    const checkedGroup = form.querySelector('input[name="Type"]:checked')?.closest('[data-types-for]');
    openCategory(checkedGroup ? checkedGroup.dataset.typesFor : null);
    markPicked();
    showQuestions();
    const errorStep = steps.findIndex(step => step.querySelector('.field-validation-error'));
    showStep(errorStep >= 0 ? errorStep : 0, errorStep >= 0);
})();