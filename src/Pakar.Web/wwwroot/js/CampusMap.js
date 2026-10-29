// Leaflet rendering for the campus parking map. Called from CampusMap.razor through JS interop.
window.campusMap = (() => {
    // Faculty of Engineering, Universitas Gadjah Mada; used until spot coordinates are available.
    const defaultCenter = [-7.7685, 110.3760];
    const defaultZoom = 18;
    const colors = { available: "#2e7d32", occupied: "#c62828" };

    let map = null;
    let spotLayer = null;

    function init(elementId, zones) {
        if (map) {
            map.remove();
        }

        map = L.map(elementId).setView(defaultCenter, defaultZoom);
        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 20,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        spotLayer = L.layerGroup().addTo(map);
        renderZones(zones);

        const bounds = spotLayer.getLayers().map(marker => marker.getLatLng());
        if (bounds.length > 0) {
            map.fitBounds(bounds, { padding: [30, 30], maxZoom: defaultZoom + 1 });
        }
    }

    function renderZones(zones) {
        if (!spotLayer) {
            return;
        }

        spotLayer.clearLayers();
        for (const zone of zones) {
            for (const spot of zone.spots) {
                const status = spot.isOccupied ? "occupied" : "available";
                L.circleMarker([spot.latitude, spot.longitude], {
                    radius: 7,
                    weight: 1,
                    color: "#ffffff",
                    fillColor: colors[status],
                    fillOpacity: 0.9
                })
                    .bindTooltip(`${spot.name} (${zone.name}): ${status}`)
                    .addTo(spotLayer);
            }
        }
    }

    function dispose() {
        if (map) {
            map.remove();
            map = null;
            spotLayer = null;
        }
    }

    return { init, renderZones, dispose };
})();
