// "Oversikt" (help with AI): «Hva betyr kolonnene?» open the first time, and a row opens the need.
// The area picker is in region-picker.js (shared with Ressurser and Behov)
// Docs: Window.localStorage (MDN) - https://developer.mozilla.org/docs/Web/API/Window/localStorage
(function () {
    function get(key) { try { return localStorage.getItem(key); } catch { return null; } }
    function set(key, value) { try { localStorage.setItem(key, value); } catch { /* storage is off */ } }

    // «Hva betyr kolonnene?»: open the first time, then closed
    const help = document.getElementById('ovHelp');
    if (help && !get('hvOverviewHelpSeen')) {
        help.open = true;
        set('hvOverviewHelpSeen', '1');
    }

    // A row opens the need: the active needs and the rows in "Utlånt nå" / "Må følges opp"
    // (not when a link inside the row is pressed - the name, phone, e-mail or the map button)
    document.querySelectorAll('.ov-row[data-href], .ov-loan[data-href]').forEach(row => row.addEventListener('click', e => {
        if (!e.target.closest('a, button')) window.location.href = row.dataset.href;
    }));
})();