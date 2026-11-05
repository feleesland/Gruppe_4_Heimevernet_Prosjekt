// Velg plassering for en ressurs: adressesøk, egen posisjon eller klikk på kartet (help with AI) - Docs: Leaflet API reference
// https://leafletjs.com/reference.html

// 1. Lag kartet (starter i Kristiansand)
const pickMap = L.map('pickMap').setView([58.14671, 7.9956], 12);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
}).addTo(pickMap);

// 2. Feltene i skjemaet vi trenger
const latInput = document.getElementById('Latitude');
const lngInput = document.getElementById('Longitude');
const statusText = document.getElementById('geoStatus');
let marker = null;

// 3. Setter markøren og lagrer koordinatene i de skjulte feltene.
//    Alle tre måtene å velge plassering på bruker denne.
function setPosition(lat, lng) {
    if (marker === null) {
        marker = L.marker([lat, lng], { draggable: true }).addTo(pickMap);
        marker.on('dragend', () => {                                         
            setPosition(marker.getLatLng().lat, marker.getLatLng().lng);
            fillAddress(marker.getLatLng().lat, marker.getLatLng().lng);
        });
    } else {
        marker.setLatLng([lat, lng]);
    }
    latInput.value = lat.toFixed(6);
    lngInput.value = lng.toFixed(6);
}

// 4. Flytter kartet dit og viser en beskjed
function showPosition(lat, lng, message) {
    setPosition(lat, lng);
    pickMap.setView([lat, lng], 16);
    statusText.textContent = message;
}

// 5. Finner adressen til et punkt og fyller inn skjemaet (help with AI) - Docs: Nominatim Reverse API
// https://nominatim.org/release-docs/latest/api/Reverse/
async function fillAddress(lat, lng) {
    try {
        const url = `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}`;
        const result = await (await fetch(url)).json();
        const a = result.address;
        if (!a) return;

        document.getElementById('Address').value = [a.road, a.house_number].filter(x => x).join(' ');
        document.getElementById('ZipCode').value = a.postcode ?? '';
        document.getElementById('City').value = a.city ?? a.town ?? a.village ?? a.municipality ?? '';
    } catch {
        // Ingen adresse funnet, koordinatene lagres uansett
    }
}

// Måte A: klikk på kartet
pickMap.on('click', (e) => {
    setPosition(e.latlng.lat, e.latlng.lng);
    fillAddress(e.latlng.lat, e.latlng.lng);                             
    statusText.textContent = 'Plassering valgt på kartet.';
});

// Måte B: søk etter adressen (help with AI) - Docs: Nominatim Search API
// https://nominatim.org/release-docs/latest/api/Search/
document.getElementById('findOnMap').addEventListener('click', async () => {
    const address = document.getElementById('Address').value;
    const zip = document.getElementById('ZipCode').value;
    const city = document.getElementById('City').value;
    const query = `${address} ${zip} ${city}`.trim();

    if (query === '') {
        statusText.textContent = 'Skriv inn en adresse først.';
        return;
    }

    statusText.textContent = 'Søker...';
    try {
        const url = 'https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=no&q='
            + encodeURIComponent(query);
        const results = await (await fetch(url)).json();

        if (results.length === 0) {
            statusText.textContent = 'Fant ikke adressen. Klikk på kartet i stedet.';
            return;
        }
        showPosition(parseFloat(results[0].lat), parseFloat(results[0].lon),
            'Adressen ble funnet. Sjekk at markøren står riktig.');
    } catch {
        statusText.textContent = 'Klarte ikke å søke nå. Klikk på kartet i stedet.';
    }
});

// Måte C: bruk posisjonen til enheten, bare hvis brukeren sier ja (help with AI) - Docs: Geolocation API (MDN)
// https://developer.mozilla.org/docs/Web/API/Geolocation_API
document.getElementById('useMyLocation').addEventListener('click', () => {
    if (!navigator.geolocation) {
        statusText.textContent = 'Nettleseren din støtter ikke posisjon.';
        return;
    }
    statusText.textContent = 'Henter posisjonen din...';
    navigator.geolocation.getCurrentPosition(
        (pos) => {                                                        
            showPosition(pos.coords.latitude, pos.coords.longitude,
                'Posisjonen din er valgt. Dra markøren hvis den ikke står helt riktig.');
            fillAddress(pos.coords.latitude, pos.coords.longitude);
        },
        () => statusText.textContent = 'Fikk ikke tilgang til posisjonen din.'
    );
});

// Hvis ressursen allerede har en plassering (f.eks. på Rediger-siden), vis den
if (latInput.value && lngInput.value) {
    showPosition(parseFloat(latInput.value), parseFloat(lngInput.value), '');
}