// Leaflet renders the geography. Blazor owns selection, availability and realtime state.
window.campusMap = (() => {
    const colors = { available: '#0f7a4a', almost_full: '#e39b00', full: '#c42b2b', unknown: '#9aa3ae' };
    // Faculty of Engineering, Universitas Gadjah Mada; used until spot coordinates are available.
    const defaultCenter = [-7.7655, 110.3718];
    const defaultZoom = 18;
    // The spots of a zone sit in a thin row, so the box is padded to be easy to see and to hold the label.
    const boxPaddingLat = 0.00008;
    const boxPaddingLng = 0.00004;
    let map, zonesLayer, spotsLayer, reference, resizeObserver, selected;
    let zones = [];

    function hasLocation(spot) {
        return Number.isFinite(spot.latitude) && Number.isFinite(spot.longitude) &&
            Math.abs(spot.latitude) <= 90 && Math.abs(spot.longitude) <= 180 &&
            (spot.latitude !== 0 || spot.longitude !== 0);
    }

    function points(zone) {
        return zone.spots.filter(hasLocation).map(spot => [spot.latitude, spot.longitude]);
    }

    // The smallest padded rectangle around the mapped spots of a zone.
    function boxBounds(coordinates) {
        const lats = coordinates.map(point => point[0]);
        const lngs = coordinates.map(point => point[1]);
        return [
            [Math.min(...lats) - boxPaddingLat, Math.min(...lngs) - boxPaddingLng],
            [Math.max(...lats) + boxPaddingLat, Math.max(...lngs) + boxPaddingLng]
        ];
    }

    // A zone an admin forced to full reads as completely taken.
    function takenSpots(zone) {
        return zone.isOverridden && zone.status === 'full' ? zone.totalCapacity : zone.occupiedSpots;
    }

    function init(id, dotnet) {
        dispose();
        if (!window.L) throw new Error('Map library unavailable');
        reference = dotnet;
        map = L.map(id, { zoomControl: false }).setView(defaultCenter, defaultZoom);
        L.control.zoom({ position: 'bottomright' }).addTo(map);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(map);
        zonesLayer = L.layerGroup().addTo(map);
        spotsLayer = L.layerGroup().addTo(map);
        resizeObserver = new ResizeObserver(() => map?.invalidateSize());
        resizeObserver.observe(map.getContainer());
        map.on('zoomend', drawSpots);
    }

    // Builds the label with DOM nodes instead of HTML strings, so a zone name can never inject markup.
    function zoneLabel(zone) {
        const label = document.createElement('span');
        const count = document.createElement('strong');
        const name = document.createElement('small');
        // Without any current reading the count is unknown, not zero, unless an admin forced the zone to full.
        const forcedFull = zone.isOverridden && zone.status === 'full';
        const noReading = zone.waitingForSource || (!forcedFull && zone.unknownSpots >= zone.totalCapacity);
        count.textContent = (noReading ? '—' : takenSpots(zone)) + ' / ' + zone.totalCapacity;
        const note = zone.waitingForSource ? ' · waiting for first update'
            : zone.stale ? ' · stale data'
            : zone.unknownSpots > 0 ? ' · ' + zone.unknownSpots + ' with unknown status' : '';
        name.textContent = zone.name + note;
        label.append(count, name);
        return label;
    }

    function renderZones(next, selectedId) {
        if (!map) return;
        const first = zones.length === 0;
        zones = next;
        selected = selectedId;
        zonesLayer.clearLayers();

        for (const zone of zones) {
            const coordinates = points(zone);
            if (coordinates.length === 0) continue;

            const color = zone.stale ? colors.unknown : (colors[zone.status] || colors.unknown);
            const isSelected = zone.id === selected;
            const options = {
                color: isSelected ? '#1f4fd8' : color,
                weight: isSelected ? 3 : 2,
                fillColor: color,
                fillOpacity: zone.stale ? .1 : .35,
                dashArray: zone.stale ? '4 4' : undefined
            };
            const shape = L.rectangle(boxBounds(coordinates), options);
            shape.bindTooltip(zoneLabel(zone), {
                permanent: true, direction: 'center', className: 'zone-box-label', interactive: true, opacity: 1
            });
            shape.on('click', () => reference?.invokeMethodAsync('SelectFromMap', zone.id));
            shape.addTo(zonesLayer);
        }

        drawSpots();
        if (first) fit();
    }

    function drawSpots() {
        if (!spotsLayer) return;
        spotsLayer.clearLayers();
        for (const zone of zones) {
            if (zone.id !== selected && map.getZoom() < 19) continue;

            for (const spot of zone.spots.filter(hasLocation)) {
                const spotColor = { occupied: colors.full, available: colors.available };
                const fillColor = zone.stale ? colors.unknown : (spotColor[spot.observedStatus] || colors.unknown);
                const label = document.createElement('span');
                const labels = { occupied: 'Occupied', available: 'Free' };
                const status = zone.waitingForSource ? 'Unknown' : (labels[spot.observedStatus] || 'Unknown');
                label.textContent = spot.name + ': ' + status;
                L.circleMarker([spot.latitude, spot.longitude], {
                    radius: 4, weight: 1, color: '#fff', fillColor, fillOpacity: 1
                }).bindTooltip(label).addTo(spotsLayer);
            }
        }
    }

    function fit() {
        const coordinates = zones.flatMap(points);
        if (coordinates.length) map.fitBounds(coordinates, { padding: [65, 65], maxZoom: 18 });
    }

    function focusZone(id) {
        const zone = zones.find(candidate => candidate.id === id);
        if (!zone) return;
        const coordinates = points(zone);
        if (coordinates.length) map.fitBounds(coordinates, { padding: [90, 90], maxZoom: 19 });
    }

    function dispose() {
        resizeObserver?.disconnect();
        map?.remove();
        map = zonesLayer = spotsLayer = reference = null;
        zones = [];
        selected = null;
    }

    return { init, renderZones, fit, focusZone, dispose };
})();
