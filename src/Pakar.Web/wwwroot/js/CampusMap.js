// Leaflet renders the geography. Blazor owns selection, availability and realtime state.
window.campusMap = (() => {
    const colors = { available: '#0f7a4a', almost_full: '#e39b00', full: '#c42b2b', unknown: '#9aa3ae' };
    const campusLimits = [[-7.7705, 110.3694], [-7.7641, 110.3784]];
    let campusBounds;
    let map, zonesLayer, spotsLayer, reference, resizeObserver, selected;
    let zones = [];

    function hasLocation(spot) {
        return Number.isFinite(spot.latitude) && Number.isFinite(spot.longitude) &&
            campusBounds.contains([spot.latitude, spot.longitude]);
    }

    function points(zone) {
        return zone.spots.filter(hasLocation).map(spot => [spot.latitude, spot.longitude]);
    }

    function hull(coordinates) {
        const sorted = [...new Map(coordinates.map(point => [point.join(','), point])).values()]
            .sort((a, b) => a[0] - b[0] || a[1] - b[1]);
        if (sorted.length < 3) return [];

        function side(ordered) {
            const boundary = [];
            for (const point of ordered) {
                while (boundary.length >= 2) {
                    const [x, y] = boundary.at(-2);
                    const [u, v] = boundary.at(-1);
                    const cross = (u - x) * (point[1] - y) - (v - y) * (point[0] - x);
                    if (cross > 0) break;
                    boundary.pop();
                }
                boundary.push(point);
            }
            return boundary.slice(0, -1);
        }

        return side(sorted).concat(side([...sorted].reverse()));
    }

    function init(id, dotnet) {
        dispose();
        if (!window.L) throw new Error('Map library unavailable');
        reference = dotnet;
        campusBounds = L.latLngBounds(campusLimits);
        map = L.map(id, {
            zoomControl: false,
            minZoom: 16,
            maxZoom: 19,
            maxBounds: campusBounds,
            maxBoundsViscosity: 1,
            bounceAtZoomLimits: false,
            zoomSnap: .25
        }).setView(campusBounds.getCenter(), 17);
        L.control.zoom({ position: 'bottomright' }).addTo(map);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(map);
        zonesLayer = L.layerGroup().addTo(map);
        spotsLayer = L.layerGroup().addTo(map);
        resizeObserver = new ResizeObserver(() => {
            if (!map) return;
            map.invalidateSize({ pan: false });
            if (selected) focusZone(selected);
            else fit();
        });
        resizeObserver.observe(map.getContainer());
        map.on('zoomend', drawSpots);
    }

    function zoneLabel(zone, color) {
        const label = document.createElement('span');
        const count = document.createElement('strong');
        const description = document.createElement('span');
        const name = document.createElement('small');
        count.textContent = zone.waitingForSource ? '—' : zone.availableSpots;
        description.textContent = zone.waitingForSource ? '' : ' free';
        const note = zone.waitingForSource ? ' · waiting for first update'
            : zone.stale ? ' · stale data'
            : zone.unknownSpots > 0 ? ' · ' + zone.unknownSpots + ' with unknown status' : '';
        name.textContent = zone.name + note;
        label.append(count, description, name);
        label.style.setProperty('--badge-color', color);
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
                fillOpacity: zone.stale ? .1 : .2,
                dashArray: zone.stale ? '4 4' : undefined
            };
            const outline = hull(coordinates);
            const center = L.latLngBounds(coordinates).getCenter();
            const shape = outline.length >= 3
                ? L.polygon(outline, options)
                : L.circleMarker(center, { ...options, radius: 16 });
            shape.bindTooltip(zoneLabel(zone, color), {
                permanent: true, direction: 'center', className: 'zone-badge', interactive: true, opacity: 1
            });
            const select = () => reference?.invokeMethodAsync('SelectFromMap', zone.id);
            shape.on('click', select);
            shape.getTooltip().on('click', select);
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
        if (!map || !coordinates.length) return;
        const padding = map.getSize().x < 600 ? [45, 28] : [65, 45];
        map.fitBounds(coordinates, { padding, maxZoom: 18, animate: false });
    }

    function focusZone(id) {
        const zone = zones.find(candidate => candidate.id === id);
        if (!zone) return;
        const coordinates = points(zone);
        if (coordinates.length) map.fitBounds(coordinates, { padding: [40, 40], maxZoom: 19, animate: false });
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
