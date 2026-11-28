const editors = new WeakMap();

export function mount(element, status, bounds, initialPoints, maxPoints) {
    const map = campusMap.createMap(element, bounds, { doubleClickZoom: false });
    const area = L.polygon([], { color: '#1f4fd8', interactive: false }).addTo(map);
    const corners = L.layerGroup().addTo(map);
    const points = initialPoints.map(point => L.latLng(point.latitude, point.longitude));
    const editor = { map, status, area, corners, points, maxPoints };
    editors.set(element, editor);

    map.on('click', event => add(editor, event.latlng));
    if (points.length)
        map.fitBounds(L.latLngBounds(points), { padding: [32, 32], maxZoom: 19 });
    render(editor);
}

function add(editor, point) {
    if (editor.points.length >= editor.maxPoints || !editor.map.options.maxBounds.contains(point))
        return;

    editor.points.push(point);
    render(editor);
}

function render(editor) {
    editor.area.setLatLngs(editor.points);
    editor.corners.clearLayers();
    editor.points.forEach((point, index) => {
        const marker = L.marker(point, {
            draggable: true,
            title: 'Boundary point ' + (index + 1),
            alt: 'Boundary point ' + (index + 1),
            icon: L.divIcon({
                className: 'zone-boundary-handle',
                html: '<span style="display:block;width:14px;height:14px;border:2px solid #fff;border-radius:50%;background:#1f4fd8;box-shadow:0 0 0 1px #1f4fd8"></span>',
                iconSize: [14, 14]
            })
        }).addTo(editor.corners);
        marker.on('drag', () => {
            const moved = marker.getLatLng();
            if (!editor.map.options.maxBounds.contains(moved)) {
                marker.setLatLng(editor.points[index]);
                return;
            }
            editor.points[index] = moved;
            editor.area.setLatLngs(editor.points);
        });
    });

    editor.status.textContent = editor.points.length + ' / ' + editor.maxPoints + ' points';
}

export function addAtCenter(element) {
    const editor = editors.get(element);
    add(editor, editor.map.getCenter());
}

export function undo(element) {
    const editor = editors.get(element);
    editor.points.pop();
    render(editor);
}

export function clear(element) {
    const editor = editors.get(element);
    editor.points.length = 0;
    render(editor);
}

export function read(element) {
    return editors.get(element).points.map(point => ({ latitude: point.lat, longitude: point.lng }));
}

export function unmount(element) {
    editors.get(element)?.map.remove();
    editors.delete(element);
}
