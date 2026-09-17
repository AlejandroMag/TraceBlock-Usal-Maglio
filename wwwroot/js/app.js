// Copiar al portapapeles
window.copyToClipboard = (text) => {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text);
    } else {
        const ta = document.createElement('textarea');
        ta.value = text;
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand('copy'); } catch (e) { }
        document.body.removeChild(ta);
    }
};

// Chart.js bar chart (stacked recaudado / restante)
window.renderBarChart = (canvasId, labels, recaudados, objetivos) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    if (ctx._chart) ctx._chart.destroy();
    ctx._chart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                { label: 'Recaudado', data: recaudados, backgroundColor: '#0d6efd' },
                {
                    label: 'Restante', data: objetivos.map((o, i) =>
                        Math.max(0, o - recaudados[i])), backgroundColor: '#dee2e6'
                }
            ]
        },
        options: { responsive: true, maintainAspectRatio: false, scales: { x: { stacked: true }, y: { stacked: true } } }
    });
};

// Chart.js line chart para donaciones por día
window.renderLineChart = (canvasId, labels, datasets) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    if (ctx._chart) ctx._chart.destroy();
    ctx._chart = new Chart(ctx, {
        type: 'line',
        data: { labels, datasets },
        options: { responsive: true, maintainAspectRatio: false, tension: 0.3 }
    });
};

// Capa base de los mapas.
// Se usan los tiles de CARTO (sirven datos de OpenStreetMap): los servidores de tiles
// de OSM son voluntarios y bloquean el uso que no sigue su política, además de tener
// deprecado el esquema {s}.tile.openstreetmap.org. La atribución sigue siendo obligatoria.
window.capaBase = () => L.tileLayer(
    'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
        subdomains: 'abcd',
        maxZoom: 20,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>'
    });

// Leaflet map init
window.initMap = (containerId, lat, lon, dotnetHelper) => {
    if (window._leafletMap) {
        window._leafletMap.remove();
        window._leafletMap = null;
    }
    const map = L.map(containerId).setView([lat, lon], 13);
    window.capaBase().addTo(map);
    window._leafletMap = map;
    window._leafletMarker = null;
    map.on('click', (e) => {
        if (window._leafletMarker) map.removeLayer(window._leafletMarker);
        window._leafletMarker = L.marker([e.latlng.lat, e.latlng.lng]).addTo(map);
        if (dotnetHelper) {
            dotnetHelper.invokeMethodAsync('OnMapClick', e.latlng.lat, e.latlng.lng);
        } else {
            DotNet.invokeMethodAsync('TrazeaBlock', 'OnMapClick', e.latlng.lat, e.latlng.lng);
        }
    });
    setTimeout(() => map.invalidateSize(), 200);
};

// Mapa de solo lectura para mostrar la ubicación de un proyecto
window._displayMaps = window._displayMaps || {};
window.showLocationMap = (containerId, lat, lon, label) => {
    const el = document.getElementById(containerId);
    if (!el) return;
    if (window._displayMaps[containerId]) {
        window._displayMaps[containerId].remove();
        window._displayMaps[containerId] = null;
    }
    const map = L.map(containerId, { scrollWheelZoom: false }).setView([lat, lon], 14);
    window.capaBase().addTo(map);
    const marker = L.marker([lat, lon]).addTo(map);
    if (label) marker.bindPopup(label).openPopup();
    window._displayMaps[containerId] = map;
    setTimeout(() => map.invalidateSize(), 200);
};

// Dibuja la ubicación de la ONG (punto azul) y el círculo del radio permitido
window.setUserLocation = (lat, lon, radiusKm) => {
    const map = window._leafletMap;
    if (!map) return;
    if (window._userCircle) { map.removeLayer(window._userCircle); window._userCircle = null; }
    if (window._userDot) { map.removeLayer(window._userDot); window._userDot = null; }
    window._userCircle = L.circle([lat, lon], {
        radius: radiusKm * 1000, color: '#0d6efd', weight: 1,
        fillColor: '#0d6efd', fillOpacity: 0.08
    }).addTo(map);
    window._userDot = L.circleMarker([lat, lon], {
        radius: 7, color: '#0d6efd', fillColor: '#0d6efd', fillOpacity: 1
    }).bindPopup('Tu ubicación (ONG)').addTo(map);
    map.fitBounds(window._userCircle.getBounds(), { padding: [20, 20] });
};

// Obtiene la ubicación del navegador, centra el mapa y la reporta a Blazor.
// Reporta OnUserLocation(lat, lon, ok): ok=false si se deniega, falla o no responde.
// Incluye un watchdog para que NUNCA quede colgado esperando al GPS.
window.locateAndInit = (containerId, fallbackLat, fallbackLon, dotnetHelper) => {
    let done = false;
    const report = (lat, lon, ok) => {
        if (done) return;
        done = true;
        try {
            window.initMap(containerId, lat, lon, dotnetHelper);
            if (ok) window.setUserLocation(lat, lon, 10);
        } catch (e) { console.error('locateAndInit map error', e); }
        try {
            if (dotnetHelper) dotnetHelper.invokeMethodAsync('OnUserLocation', lat, lon, ok);
        } catch (e) { console.error('OnUserLocation invoke error', e); }
    };

    // Watchdog: si en 12s no hubo respuesta del GPS, lo tratamos como fallo (habilita "Reintentar").
    setTimeout(() => report(fallbackLat, fallbackLon, false), 12000);

    if (navigator.geolocation) {
        try {
            navigator.geolocation.getCurrentPosition(
                pos => report(pos.coords.latitude, pos.coords.longitude, true),
                () => report(fallbackLat, fallbackLon, false),
                { timeout: 10000, maximumAge: 60000 }
            );
        } catch (e) {
            report(fallbackLat, fallbackLon, false);
        }
    } else {
        report(fallbackLat, fallbackLon, false);
    }
};
