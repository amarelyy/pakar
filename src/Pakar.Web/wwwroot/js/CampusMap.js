// Leaflet is only the geographic renderer; application state and events live in C#/Blazor.
window.campusMap = (() => {
    let map, zonesLayer, spotsLayer, reference, resizeObserver, zones = [], selected;
    const colors = { available: '#0f7a4a', almost_full: '#e39b00', full: '#c42b2b', unknown: '#9aa3ae' };
    const safe = value => { const el = document.createElement('span'); el.textContent = String(value); return el.innerHTML; };
    const points = zone => zone.spots.filter(s => Number.isFinite(s.latitude) && Number.isFinite(s.longitude) && Math.abs(s.latitude) <= 90 && Math.abs(s.longitude) <= 180 && (s.latitude !== 0 || s.longitude !== 0)).map(s => [s.latitude, s.longitude]);
    function hull(points) {
        const sorted = [...new Map(points.map(p => [p.join(','), p])).values()].sort((a,b) => a[0]-b[0] || a[1]-b[1]);
        if (sorted.length < 3) return [];
        const cross = (o,a,b) => (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0]);
        const lower=[], upper=[];
        for(const p of sorted){ while(lower.length>=2 && cross(lower.at(-2),lower.at(-1),p)<=0)lower.pop();lower.push(p); }
        for(const p of [...sorted].reverse()){ while(upper.length>=2 && cross(upper.at(-2),upper.at(-1),p)<=0)upper.pop();upper.push(p); }
        lower.pop();upper.pop();const result=lower.concat(upper);return result.length>=3 ? result : [];
    }
    function init(id, dotnet) {
        dispose(); reference=dotnet;
        if(!window.L) throw new Error('Map library unavailable');
        map=L.map(id,{zoomControl:false}).setView([-7.7685,110.3760],17);
        L.control.zoom({position:'bottomright'}).addTo(map);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19,attribution:'&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'}).addTo(map);
        zonesLayer=L.layerGroup().addTo(map);spotsLayer=L.layerGroup().addTo(map);
        resizeObserver=new ResizeObserver(()=>map?.invalidateSize());resizeObserver.observe(map.getContainer());
        map.on('zoomend',drawSpots);
    }
    function renderZones(next, selectedId) {
        if(!map)return;
        const first=zones.length===0;zones=next;selected=selectedId;zonesLayer.clearLayers();
        for(const z of zones){
            const pts=points(z);if(!pts.length)continue;
            const color=z.stale?colors.unknown:(colors[z.status]||colors.unknown);
            const opts={color:z.id===selected?'#1f4fd8':color,weight:z.id===selected?3:2,fillColor:color,fillOpacity:z.stale?.1:.2,dashArray:z.stale?'4 4':undefined};
            const outline=hull(pts);
            const center=pts.reduce((c,p)=>[c[0]+p[0]/pts.length,c[1]+p[1]/pts.length],[0,0]);
            const shape=outline.length?L.polygon(outline,opts):L.circleMarker(center,{...opts,radius:16});
            const label=document.createElement('span');
            label.innerHTML=`<strong>${safe(z.availableSpots)}</strong><span> kosong</span><small>${safe(z.name)}${z.stale?' · data lama':z.unknownSpots>0?' · '+safe(z.unknownSpots)+' belum diketahui':''}</small>`;
            label.style.setProperty('--badge-color',color);
            shape.bindTooltip(label,{permanent:true,direction:'center',className:'zone-badge',interactive:true,opacity:1});
            shape.on('click',()=>reference?.invokeMethodAsync('SelectFromMap',z.id));shape.addTo(zonesLayer);
        }
        drawSpots();if(first)fit();
    }
    function drawSpots(){
        if(!spotsLayer)return;spotsLayer.clearLayers();
        for(const z of zones){if(z.id!==selected && map.getZoom()<19)continue;
            for(const s of z.spots){if(!points({spots:[s]}).length)continue;
                L.circleMarker([s.latitude,s.longitude],{radius:4,weight:1,color:'#fff',fillColor:z.stale || (z.usesObservations && !s.observedStatus)?colors.unknown:(z.usesObservations?s.observedStatus==='occupied':s.isOccupied)?colors.full:colors.available,fillOpacity:1})
                .bindTooltip(`${safe(s.name)}: ${z.usesObservations ? (s.observedStatus==='occupied'?'Terisi':s.observedStatus==='available'?'Kosong teramati':'Belum diketahui') : s.isOccupied?'Terisi':'Kosong'}`).addTo(spotsLayer);
            }
        }
    }
    function fit(){const pts=zones.flatMap(points);if(pts.length)map.fitBounds(L.latLngBounds(pts),{padding:[65,65],maxZoom:18});}
    function focusZone(id){const zone=zones.find(z=>z.id===id);const pts=zone?points(zone):[];if(pts.length)map.fitBounds(L.latLngBounds(pts),{padding:[90,90],maxZoom:19});}
    function dispose(){resizeObserver?.disconnect();if(map)map.remove();map=null;zonesLayer=null;spotsLayer=null;zones=[];reference=null;}
    return {init,renderZones,fit,focusZone,dispose};
})();
