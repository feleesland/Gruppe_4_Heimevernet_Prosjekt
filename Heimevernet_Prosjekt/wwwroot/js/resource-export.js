// Eksporter: download a list as CSV (opens in Excel) or JSON (for other systems). Made in the browser,
// from exactly what is shown on the screen, so no server code is needed (help with AI)
// Docs: Blob (MDN) - https://developer.mozilla.org/docs/Web/API/Blob
window.ResourceExport = (function () {
    // One cell: quotes around it, and a quote inside is written twice ("") - Docs: RFC 4180
    function cell(value) {
        var text = value == null ? '' : String(value);
        return '"' + text.replace(/"/g, '""') + '"';
    }

    // Tabs between the columns, saved as UTF-16 with a BOM: then Excel puts each value in its own column and reads æøå,
    // whether Excel is set to Norwegian (;) or English (,)
    function toCsv(rows) {
        if (rows.length === 0) return '';
        var columns = Object.keys(rows[0]);
        var lines = [columns.map(cell).join('\t')];
        rows.forEach(function (row) {
            lines.push(columns.map(function (c) { return cell(row[c]); }).join('\t'));
        });
        return '\uFEFF' + lines.join('\r\n');
    }

    // The text as UTF-16 little endian bytes (what Excel expects together with the BOM)
    function utf16(text) {
        var bytes = new Uint8Array(text.length * 2);
        for (var i = 0; i < text.length; i++) {
            var code = text.charCodeAt(i);
            bytes[i * 2] = code & 255;
            bytes[i * 2 + 1] = code >> 8;
        }
        return bytes;
    }

    // name: the start of the file name, e.g. "ressurser" -> ressurser-2026-11-14.csv. kind: 'csv' or 'json'
    function download(rows, name, kind) {
        var blob = kind === 'json'
            ? new Blob([JSON.stringify(rows, null, 2)], { type: 'application/json;charset=utf-8' })
            : new Blob([utf16(toCsv(rows))], { type: 'text/csv;charset=utf-16le' });
        var link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = name + '-' + new Date().toISOString().slice(0, 10) + '.' + kind;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(link.href); }, 1000);
    }

    return { download: download };
})();