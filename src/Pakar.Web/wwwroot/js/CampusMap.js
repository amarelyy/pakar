// Leaflet rendering for the campus parking map. Called from CampusMap.razor through JS interop.
window.campusMap = (() => {
    // Faculty of Engineering, Universitas Gadjah Mada; used until spot coordinates are available.
    const defaultCenter = [-7.7655, 110.3718];
    const defaultZoom = 18;
    // Box colour by the zone status the API computes (available, almost_full, full).
    const colors = { available: "#2e7d32", almost_full: "#f9a825", full: "#c62828", unknown: "#757575" };
    const statusLabels = { available: "Available", almost_full: "Almost full", full: "Full" };
    // The spots of a zone sit in a thin row, so the box is padded to be easy to see and to hold the label.
    const boxPaddingLat = 0.00008;
    const boxPaddingLng = 0.00004;

    let map = null;
    let zoneLayer = null;

    function init(elementId, zones) {
        if (map) {
            map.remove();
        }

        map = L.map(elementId).setView(defaultCenter, defaultZoom);
        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 20,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        zoneLayer = L.featureGroup().addTo(map);
        renderZones(zones);

        if (zoneLayer.getLayers().length > 0) {
            map.fitBounds(zoneLayer.getBounds(), { padding: [30, 30], maxZoom: defaultZoom + 1 });
        }
    }

    // The smallest rectangle around every mapped spot of the zone, or null when no spot has a position.
    function zoneBounds(zone) {
        const points = (zone.spots ?? [])
            .filter(spot => Number.isFinite(spot.latitude) && Number.isFinite(spot.longitude))
            .map(spot => [spot.latitude, spot.longitude]);
        if (points.length === 0) {
            return null;
        }

        const lats = points.map(point => point[0]);
        const lngs = points.map(point => point[1]);
        return [
            [Math.min(...lats) - boxPaddingLat, Math.min(...lngs) - boxPaddingLng],
            [Math.max(...lats) + boxPaddingLat, Math.max(...lngs) + boxPaddingLng]
        ];
    }

    // Spots taken, so "0 / 10" is an empty zone and "9 / 10" has one spot left. A zone an admin forced to full
    // reports 0 available, so it reads "10 / 10".
    function takenSpots(zone) {
        return Math.min(zone.totalCapacity, Math.max(0, zone.totalCapacity - zone.availableSpots));
    }

    // Builds the label with DOM nodes instead of HTML strings, so a zone name can never inject markup.
    function boxLabel(zone) {
        const label = document.createElement("span");
        // Without a reading the number taken is unknown, not "everything".
        const taken = zone.status === "unknown" ? "—" : takenSpots(zone);
        label.textContent = `${taken} / ${zone.totalCapacity}`;
        return label;
    }

    function popupContent(zone) {
        const wrapper = document.createElement("div");
        const name = document.createElement("strong");
        name.textContent = zone.name;
        const detail = document.createElement("div");
        detail.textContent = zone.status === "unknown"
            ? "No data"
            : `${statusLabels[zone.status] ?? "No data"}: ${takenSpots(zone)} of ${zone.totalCapacity} spots taken`;
        wrapper.append(name, detail);
        return wrapper;
    }

    function renderZones(zones) {
        if (!zoneLayer) {
            return;
        }

        zoneLayer.clearLayers();
        for (const zone of zones) {
            const bounds = zoneBounds(zone);
            if (!bounds) {
                continue;
            }

            const color = colors[zone.status] ?? colors.unknown;
            L.rectangle(bounds, {
                color,
                weight: 2,
                fillColor: color,
                fillOpacity: 0.35
            })
                .bindTooltip(boxLabel(zone), {
                    permanent: true,
                    direction: "center",
                    className: "zone-box-label"
                })
                .bindPopup(popupContent(zone))
                .addTo(zoneLayer);
        }
    }

    function dispose() {
        if (map) {
            map.remove();
            map = null;
            zoneLayer = null;
        }
    }

    return { init, renderZones, dispose };
})();
