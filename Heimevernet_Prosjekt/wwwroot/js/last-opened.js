// «Sist åpnet» (help with AI): the need or resource you opened last, so it is easy to find again after a misclick.
// The need and resource pages remember it (data-last-* on #lastOpened). The lists (Oversikt, Behov, Ressurser) then
// mark that row blue with «Sist åpnet», and scroll to it when you come back from it
// Kept in the browser only. Docs: Window.localStorage (MDN) - https://developer.mozilla.org/docs/Web/API/Window/localStorage
(function () {
    const KEY = 'hvLastOpened-';      // + "need" or "resource"

    function read(kind) { try { return JSON.parse(localStorage.getItem(KEY + kind) || 'null'); } catch { return null; } }
    function write(kind, value) { try { localStorage.setItem(KEY + kind, JSON.stringify(value)); } catch { /* storage is off */ } }

    // ---- A need or resource page: remember it ----
    const page = document.getElementById('lastOpened');
    if (page) {
        write(page.dataset.kind, { id: page.dataset.id, name: page.dataset.name, url: page.dataset.url, at: Date.now() });
    }

    // ---- A list: mark the row (data-last-list on the list says which kind) ----
    const list = document.querySelector('[data-last-list]');
    if (!list) return;
    const kind = list.dataset.lastList;
    const last = read(kind);
    if (!last) return;

    // Came straight back from that page («← Alle behov», «← Oversikt» or the browser's back button)?
    const cameBack = document.referrer && new URL(document.referrer, location.href).pathname.toLowerCase() === new URL(last.url, location.href).pathname.toLowerCase();

    // The row: blue, with «Sist åpnet» after the name (textContent, so a name can't run as HTML)
    const row = document.querySelector(`[data-last-id="${CSS.escape(String(last.id))}"]`);
    if (row) {
        row.classList.add('is-last');
        const name = row.querySelector('.list-name, .ov-name a');
        if (name && !row.querySelector('.last-tag')) {
            const tag = document.createElement('span');
            tag.className = 'last-tag';
            tag.innerHTML = '<i class="ti ti-history" aria-hidden="true"></i> ';
            tag.append('Sist åpnet');
            name.after(' ', tag);
        }
        if (cameBack && !row.hidden) {
            // Open what hides it: a closed box (Løst) or «Vis alle» on Oversikt
            const details = row.closest('details');
            if (details) details.open = true;
            const collapse = row.closest('.collapse');
            if (collapse) collapse.classList.add('show');
            row.scrollIntoView({ block: 'center' });
        }
    }
})();