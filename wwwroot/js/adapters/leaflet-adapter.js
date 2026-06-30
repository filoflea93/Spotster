'use strict';

const DEFAULT_LAYER_GROUPS = ['requests', 'parkings', 'searchPreview', 'chatLocation'];

/**
 * Implementazione Leaflet dell'interfaccia map-engine.
 * Per migrare a Mapbox/Google/Azure: creare un adapter con la stessa API pubblica.
 */
export function createLeafletMapEngine() {
    let map = null;
    let tileLayer = null;
    const layerGroups = {};

    function ensureLayerGroup(name) {
        if (!map) return null;
        if (!layerGroups[name]) {
            layerGroups[name] = L.layerGroup().addTo(map);
        }
        return layerGroups[name];
    }

    return {
        isInitialized() {
            return !!map;
        },

        init(containerId, { onViewChange, onClick } = {}) {
            if (map) return;

            map = L.map(containerId, {
                zoomControl: false,
                minZoom: 3,
                maxZoom: 19,
                worldCopyJump: false
            }).setView([45.4642, 9.1900], 16);

            L.control.zoom({ position: 'bottomright' }).addTo(map);

            tileLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap',
                maxZoom: 19,
                maxNativeZoom: 19
            }).addTo(map);

            if (onViewChange) {
                map.on('zoomstart zoomend moveend', onViewChange);
            }
            if (onClick) {
                map.on('click', (e) => onClick({ lat: e.latlng.lat, lng: e.latlng.lng }));
            }

            DEFAULT_LAYER_GROUPS.forEach(ensureLayerGroup);
        },

        ensureLayerGroup,
        clearLayerGroup(name) {
            if (layerGroups[name]) {
                layerGroups[name].clearLayers();
            }
        },

        setView(lat, lng, zoom, options = {}) {
            if (!map) return;
            map.setView([lat, lng], zoom, options);
        },

        getZoom() {
            return map ? map.getZoom() : 16;
        },

        flyTo(lat, lng, zoom, options = {}) {
            if (!map) return;
            map.flyTo([lat, lng], zoom, options);
        },

        fitBoundsFromCoords(coordsList, options = {}) {
            if (!map || !coordsList.length) return;
            const padding = options.padding || [48, 48];
            const maxZoom = options.maxZoom || 17;
            const animate = options.animate !== false;

            if (coordsList.length === 1) {
                const c = coordsList[0];
                map.setView([c.lat, c.lng], options.zoom ?? 16, { animate });
                return;
            }

            const bounds = L.latLngBounds(coordsList.map(c => [c.lat, c.lng]));
            map.fitBounds(bounds, { padding, maxZoom, animate });
        },

        latLngBoundsFromCenter(lat, lng, radiusMeters) {
            const latDelta = (radiusMeters / 6371000) * (180 / Math.PI);
            const lngDelta = latDelta / Math.cos(lat * Math.PI / 180);
            return L.latLngBounds(
                [lat - latDelta, lng - lngDelta],
                [lat + latDelta, lng + lngDelta]
            );
        },

        invalidateSize() {
            if (!map) return;
            map.invalidateSize({ animate: false });
            if (tileLayer) tileLayer.redraw();
        },

        getContainer() {
            return map ? map.getContainer() : null;
        },

        removeLayer(layer) {
            if (map && layer) {
                map.removeLayer(layer);
            }
        },

        hasLayer(layer) {
            return !!(map && layer && map.hasLayer(layer));
        },

        addCircle(lat, lng, style, { group = null, addToMap = false } = {}) {
            if (!map) return null;
            const circle = L.circle([lat, lng], style);
            if (group) {
                ensureLayerGroup(group).addLayer(circle);
            } else if (addToMap) {
                circle.addTo(map);
            }
            return circle;
        },

        addHtmlMarker(lat, lng, { html, className = '', iconSize, iconAnchor, zIndexOffset = 0, group = null, addToMap = false }) {
            if (!map) return null;
            const icon = L.divIcon({ className, html, iconSize, iconAnchor });
            const marker = L.marker([lat, lng], { icon, zIndexOffset });
            if (group) {
                ensureLayerGroup(group).addLayer(marker);
            } else if (addToMap) {
                marker.addTo(map);
            }
            return marker;
        },

        bindPopup(marker, html) {
            if (marker) marker.bindPopup(html);
            return marker;
        },

        openPopup(marker) {
            if (marker) marker.openPopup();
        },

        setPopupContent(marker, html) {
            if (marker) marker.setPopupContent(html);
        },

        isPopupOpen(marker) {
            return marker ? marker.isPopupOpen() : false;
        },

        onMarkerClick(marker, handler) {
            if (marker) marker.on('click', handler);
            return marker;
        },

        addLayerToGroup(groupName, layer) {
            if (layer) ensureLayerGroup(groupName).addLayer(layer);
        }
    };
}
