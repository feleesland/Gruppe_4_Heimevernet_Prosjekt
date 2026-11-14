// The map pictures for every map on the site: Kartverket topographic (default), Kartverket grey or OpenStreetMap.
// Chosen in the account menu and saved in the browser (help with AI) - Docs: Leaflet TileLayer https://leafletjs.com/reference.html#tilelayer
var mapStyles = {
    topo: { url: 'https://cache.kartverket.no/v1/wmts/1.0.0/topo/default/webmercator/{z}/{y}/{x}.png', attribution: '&copy; <a href="https://www.kartverket.no/">Kartverket</a>' },
    gray: { url: 'https://cache.kartverket.no/v1/wmts/1.0.0/topograatone/default/webmercator/{z}/{y}/{x}.png', attribution: '&copy; <a href="https://www.kartverket.no/">Kartverket</a>' },
    osm: { url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' }
};

function addMapPictures(map) {
    var layer = null;
    function use(name) {
        var style = mapStyles[name] || mapStyles.topo;
        if (layer) map.removeLayer(layer);
        layer = L.tileLayer(style.url, { attribution: style.attribution, maxZoom: 18, keepBuffer: 4 }).addTo(map);
        layer.bringToBack();
        // A picture that fails is tried again (up to 3 times) instead of staying grey
        layer.on('tileerror', function (e) {
            var tries = (e.tile.dataset.tries || 0) * 1 + 1;
            if (tries > 3) return;
            e.tile.dataset.tries = tries;
            setTimeout(function () { e.tile.src = e.tile.src.split('#')[0] + '#' + tries; }, 400 * tries);
        });
    }
    var saved = 'topo';
    try { saved = localStorage.getItem('mapStyle') || 'topo'; } catch (e) { }
    use(saved);
    window.addEventListener('mapstylechange', function (e) { use(e.detail); });
}