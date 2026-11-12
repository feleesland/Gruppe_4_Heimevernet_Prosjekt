// "Ny ressurs" / "Endre ressurs" as a wizard: one step at a time, and only the questions for the chosen type (help with AI)
// Docs: HTMLElement.hidden - https://developer.mozilla.org/docs/Web/API/HTMLElement/hidden
(() => {
    const form = document.getElementById('resourceWizard');
    if (!form) return;

    const steps = [...form.querySelectorAll('.wizard-step')];
    const labels = [...document.querySelectorAll('.wizard-steps li')];
    const backButton = form.querySelector('[data-wizard-back]');
    const nextButton = form.querySelector('[data-wizard-next]');
    const submitButton = form.querySelector('[data-wizard-submit]');
    const typeError = document.getElementById('typeError');
    const noQuestions = form.querySelector('[data-no-questions]');
    const last = steps.length - 1;
    let current = 0;
    let pickedWithPointer = false;

    function chosenType() {
        return form.querySelector('input[name="Type"]:checked')?.value ?? '';
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
        submitButton.hidden = current !== last;

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

    // Step 1 has two views: the six categories, or the types in one category (id = null shows the categories)
    const categoryList = form.querySelector('[data-category-list]');
    const typeGroups = [...form.querySelectorAll('[data-types-for]')];
    const oldType = form.querySelector('[data-old-type]');

    function showCategory(id) {
        categoryList.hidden = id !== null;
        typeGroups.forEach(group => group.hidden = group.dataset.typesFor !== id);
        if (oldType) oldType.hidden = id !== null;
    }

    form.querySelectorAll('[data-category]').forEach(button => button.addEventListener('click', () => {
        showCategory(button.dataset.category);
        form.querySelector(`[data-types-for="${button.dataset.category}"] legend`)?.focus();
    }));
    form.querySelectorAll('[data-category-back]').forEach(button => button.addEventListener('click', () => {
        showCategory(null);
        document.getElementById('stepTitle1').focus();
    }));

    // Clicking a type card goes straight to its questions. With the keyboard (arrow keys) it only selects,
    // so the user can look through the list and press "Neste"
    form.querySelectorAll('.type-card').forEach(card => card.addEventListener('pointerdown', () => pickedWithPointer = true));
    form.querySelectorAll('input[name="Type"]').forEach(radio => radio.addEventListener('change', () => {
        typeError.hidden = true;
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
            })
            .catch(() => { /* no municipality found, the rest is saved anyway */ });
    }

    // Start: the chosen type's category is open (Edit), otherwise the six categories.
    // Then the step with a server error (e.g. missing name), otherwise step 1
    const checkedGroup = form.querySelector('input[name="Type"]:checked')?.closest('[data-types-for]');
    showCategory(checkedGroup ? checkedGroup.dataset.typesFor : null);
    showQuestions();
    const errorStep = steps.findIndex(step => step.querySelector('.field-validation-error'));
    showStep(errorStep >= 0 ? errorStep : 0, errorStep >= 0);
})();