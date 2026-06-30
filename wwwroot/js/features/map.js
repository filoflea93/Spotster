import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';
import { mapEngine } from '../core/map-engine.js';

export function registerMap(hub, t) {
// --- Dashboard ---
async function showDashboard() {
    $('#auth-screen').addClass('d-none');
    $('#dashboard').removeClass('d-none');
    $('#display-username').html(hub.userProfileLink(state.currentUser.userId, state.currentUser.username));
    $('#display-review-stars').text('0');
    hub.updateTopBarAvatar();
    initMap();
    initViewRadiusControl();
    hub.connectSignalR();
    await refreshGeoPermissionState();
    restoreLastGeoIfAvailable();
    if (!state.userLocation && state.geoPermissionState === 'granted') {
        requestUserLocation();
    }
    updateGeoPermissionUI();
    await hub.refreshUserProfile();
    await hub.refreshReviewStarsDisplay();
    hub.showSuspendedNotice(false);
    hub.initDashboardChromeLayout();
    hub.initSidebarToggle();
    hub.startCountdownTimer();
    window.setTimeout(refreshMapView, 100);
}

function getBoundsFromCenter(lat, lng, radiusMeters) {
    return mapEngine.latLngBoundsFromCenter(lat, lng, radiusMeters);
}

function distanceMeters(lat1, lng1, lat2, lng2) {
    const toRad = d => d * Math.PI / 180;
    const dLat = toRad(lat2 - lat1);
    const dLng = toRad(lng2 - lng1);
    const a = Math.sin(dLat / 2) ** 2 +
        Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLng / 2) ** 2;
    return 6371000 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function getItemCoords(item) {
    if (!item) return null;
    const lat = item.latitude ?? item.Latitude ?? item.lat ?? item.Lat;
    const lng = item.longitude ?? item.Longitude ?? item.lng ?? item.Lng;
    if (lat == null || lng == null || Number.isNaN(Number(lat)) || Number.isNaN(Number(lng))) {
        return null;
    }
    const coords = { lat: Number(lat), lng: Number(lng) };
    if (coords.lat === 0 && coords.lng === 0) return null;
    return coords;
}

function applyReportCoords(p, coords) {
    if (!p || !coords) return p;
    p.latitude = coords.lat;
    p.longitude = coords.lng;
    p.Latitude = coords.lat;
    p.Longitude = coords.lng;
    return p;
}

function loadOwnReportCoordOverrides() {
    try {
        return JSON.parse(sessionStorage.getItem(C.OWN_REPORT_COORDS_KEY) || '{}');
    } catch {
        return {};
    }
}

function saveOwnReportCoordOverride(reportId, coords) {
    if (!reportId || !coords) return;
    const overrides = loadOwnReportCoordOverrides();
    overrides[String(reportId)] = { lat: coords.lat, lng: coords.lng };
    sessionStorage.setItem(C.OWN_REPORT_COORDS_KEY, JSON.stringify(overrides));
}

function patchOwnReportCoords(p) {
    if (!p) return p;
    const overrides = loadOwnReportCoordOverrides();
    const saved = overrides[String(p.id || p.Id)];
    if (saved) applyReportCoords(p, saved);
    return p;
}

function getParkingCoords(p) {
    if (!p) return null;
    const overrides = loadOwnReportCoordOverrides();
    const saved = overrides[String(p.id || p.Id)];
    if (saved) return saved;
    return getItemCoords(p);
}

function getVirtualZoneKey(lat, lng) {
    const latCell = Math.round(lat / C.VIRTUAL_ZONE_CELL_DEGREES);
    const lngCell = Math.round(lng / C.VIRTUAL_ZONE_CELL_DEGREES);
    return `${latCell}:${lngCell}`;
}

function isSameVirtualZone(lat1, lng1, lat2, lng2) {
    return getVirtualZoneKey(lat1, lng1) === getVirtualZoneKey(lat2, lng2);
}

function isWithinViewRange(lat, lng) {
    if (!state.userLocation) return false;
    return distanceMeters(state.userLocation.lat, state.userLocation.lng, lat, lng) <= state.viewRadiusMeters;
}

function isOwnListingOffZone(item) {
    if (!state.userLocation) return false;
    const coords = getItemCoords(item);
    if (!coords) return false;
    if (isSameVirtualZone(state.userLocation.lat, state.userLocation.lng, coords.lat, coords.lng)) {
        return false;
    }
    return !isWithinViewRange(coords.lat, coords.lng);
}

function isOutsideViewRange(lat, lng) {
    return state.userLocation && !isWithinViewRange(lat, lng);
}

function refreshUserLocation() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject(new Error(t('Toast_GeoUnsupported')));
            return;
        }

        navigator.geolocation.getCurrentPosition(
            function (pos) {
                updateUserPosition(pos.coords.latitude, pos.coords.longitude);
                resolve(state.userLocation);
            },
            function () {
                reject(new Error(t('Toast_GeoDisabled')));
            },
            { enableHighAccuracy: false, timeout: 12000, maximumAge: 300000 }
        );
    });
}

function loadViewRadius() {
    const stored = parseInt(localStorage.getItem(C.VIEW_RADIUS_KEY), 10);
    if (C.VIEW_RADIUS_OPTIONS.includes(stored)) {
        state.viewRadiusMeters = stored;
    }
}

function formatViewRadiusLabel(meters) {
    const keyByMeters = {
        500: 'Modal_Radius_500',
        1000: 'Modal_Radius_1000',
        2000: 'Modal_Radius_2000',
        3000: 'Modal_Radius_3000',
        5000: 'Modal_Radius_5000'
    };
    const key = keyByMeters[meters];
    return key ? t(key) : `${meters} m`;
}

function initViewRadiusControl() {
    loadViewRadius();
    $('#view-radius').val(String(state.viewRadiusMeters));
    $('#map-radius-control').attr('title', t('Map_ViewRadiusHint'));
}

function applyViewRadius(meters) {
    if (!C.VIEW_RADIUS_OPTIONS.includes(meters)) return;
    state.viewRadiusMeters = meters;
    localStorage.setItem(C.VIEW_RADIUS_KEY, String(meters));
    $('#view-radius').val(String(meters));
    if (state.userLocation) {
        updateNearbyAreaCircle(state.userLocation.lat, state.userLocation.lng);
        hub.loadParkings();
        hub.loadRequests();
    } else {
        hub.renderParkings();
        hub.renderRequests();
    }
    hub.syncSignalRViewport();
    hub.refreshMapView();
}

function getVisibleRequests() {
    return hub.getRequestsForList();
}

function getVisibleParkings() {
    return state.parkings.filter(p => {
        const coords = hub.resolveParkingMapCoords(p);
        if (!coords) return hub.isMyReport(p);
        return hub.isMyReport(p) || (state.userLocation && isWithinViewRange(coords.lat, coords.lng));
    });
}

function updateNearbyAreaCircle(lat, lng) {
    if (!mapEngine.isInitialized()) return;

    if (state.mapLimitCircle) {
        mapEngine.removeLayer(state.mapLimitCircle);
    }

    state.mapLimitCircle = mapEngine.addCircle(lat, lng, {
        radius: state.viewRadiusMeters,
        color: '#3B7DD8',
        weight: 2,
        fillColor: '#3B7DD8',
        fillOpacity: 0.06,
        dashArray: '6 8',
        interactive: false
    }, { addToMap: true });
}

function updateUserPositionMarker(lat, lng) {
    if (!mapEngine.isInitialized()) return;

    if (state.userPositionMarker) {
        mapEngine.removeLayer(state.userPositionMarker);
    }

    state.userPositionMarker = mapEngine.addHtmlMarker(lat, lng, {
        html: `<div class="user-location-marker">
            <span class="user-location-pulse"></span>
            <span class="user-location-pin">
                <span class="user-location-figure"><i class="bi bi-person-fill"></i></span>
                <span class="user-location-pin-tip"></span>
            </span>
        </div>`,
        iconSize: [44, 56],
        iconAnchor: [22, 54],
        zIndexOffset: 1000,
        addToMap: true
    });
    mapEngine.bindPopup(state.userPositionMarker, t('List_YourPosition'));
}

async function syncLocationToServer(lat, lng) {
    if (!state.currentUser) return;
    try {
        const profile = await hub.apiPut('/api/users/me/location', { latitude: lat, longitude: lng });
        hub.applyUserProfile(profile);
        hub.updateAccountStatusUI();
    } catch (_) { /* ignore */ }
}

function scheduleLocationSync(lat, lng) {
    if (state.locationSyncTimerId) {
        window.clearTimeout(state.locationSyncTimerId);
    }
    state.locationSyncTimerId = window.setTimeout(() => {
        state.locationSyncTimerId = null;
        syncLocationToServer(lat, lng);
    }, 1500);
}

function updateUserPosition(lat, lng) {
    if (!mapEngine.isInitialized()) initMap();

    const isFirstFix = !state.userLocation;
    state.userLocation = { lat, lng };
    updateGeoPermissionUI();

    try {
        if (isFirstFix) {
            mapEngine.setView(lat, lng, 16, { animate: false });
        }

        updateUserPositionMarker(lat, lng);
        updateNearbyAreaCircle(lat, lng);
        scheduleLocationSync(lat, lng);
        hub.loadParkings();
        hub.loadRequests();
        hub.syncSignalRViewport();
    } catch (err) {
        console.error('updateUserPosition failed', err);
        hub.showToast(t('Toast_GeoDisabled'));
    }
}

function setGeoRequestLoading(loading) {
    state.geoRequestInFlight = loading;
    const $btn = $('#btn-enable-geo');
    $btn.toggleClass('is-loading', loading).prop('disabled', loading);
    $btn.text(loading ? t('Map_GeoPrompt_Loading') : t('Map_GeoPrompt_Enable'));
}

function setGeoPermissionHint(messageKey, isError = false) {
    const $hint = $('#geo-permission-hint');
    if (!messageKey) {
        $hint.text(t('Map_GeoPrompt_Body')).removeClass('is-error');
        return;
    }
    $hint.text(t(messageKey)).toggleClass('is-error', isError);
}

function restoreLastGeoIfAvailable() {
    if (state.userLocation) return true;
    try {
        const raw = sessionStorage.getItem('spotsterLastGeo');
        if (!raw) return false;
        const parsed = JSON.parse(raw);
        const lat = Number(parsed?.lat);
        const lng = Number(parsed?.lng);
        const at = Number(parsed?.at) || 0;
        if (!Number.isFinite(lat) || !Number.isFinite(lng)) return false;
        if (Date.now() - at > 60 * 60 * 1000) return false;
        state.geoPermissionState = 'granted';
        updateUserPosition(lat, lng);
        return true;
    } catch {
        return false;
    }
}

async function refreshGeoPermissionState() {
    if (!navigator.permissions?.query) return;
    try {
        const status = await navigator.permissions.query({ name: 'geolocation' });
        state.geoPermissionState = status.state;
        status.onchange = () => {
            state.geoPermissionState = status.state;
            updateGeoPermissionUI();
            if (status.state === 'granted' && !state.userLocation && !state.geoRequestInFlight) {
                requestUserLocation();
            }
        };
    } catch {
        state.geoPermissionState = 'unknown';
    }
}

function updateGeoPermissionUI() {
    const show = !!state.currentUser && !state.userLocation && $('#dashboard').is(':visible');
    $('#geo-permission-banner').toggleClass('d-none', !show);
    if (!show) {
        setGeoRequestLoading(false);
        return;
    }

    if (!window.isSecureContext) {
        setGeoPermissionHint('Map_GeoPrompt_Insecure', true);
        return;
    }

    if (state.geoPermissionState === 'denied') {
        setGeoPermissionHint('Map_GeoPrompt_Denied', true);
        return;
    }

    if (!state.geoRequestInFlight) {
        setGeoPermissionHint(null, false);
    }
}

function stopGeoWatch() {
    if (state.geoWatchId != null) {
        navigator.geolocation.clearWatch(state.geoWatchId);
        state.geoWatchId = null;
    }
}

function cancelGeoRequest() {
    stopGeoWatch();
    if (state.geoWatchdogId != null) {
        window.clearTimeout(state.geoWatchdogId);
        state.geoWatchdogId = null;
    }
    setGeoRequestLoading(false);
}

function applyUserCoords(lat, lng, onSuccess) {
    state.geoPermissionState = 'granted';
    try {
        sessionStorage.setItem('spotsterLastGeo', JSON.stringify({ lat, lng, at: Date.now() }));
    } catch { /* ignore */ }
    updateUserPosition(lat, lng);
    hub.showToast(t('Toast_GeoSuccess'));
    if (typeof onSuccess === 'function') {
        onSuccess(lat, lng);
    }
}

function tryWatchPosition(onSuccess, onFail) {
    stopGeoWatch();
    state.geoWatchId = navigator.geolocation.watchPosition(
        function (pos) {
            cancelGeoRequest();
            try {
                applyUserCoords(pos.coords.latitude, pos.coords.longitude, onSuccess);
            } catch (err) {
                console.error('updateUserPosition failed', err);
                hub.showToast(t('Toast_GeoDisabled'));
                updateGeoPermissionUI();
            }
        },
        function (err) {
            stopGeoWatch();
            if (typeof onFail === 'function') {
                onFail(err);
            }
        },
        { enableHighAccuracy: false, maximumAge: 300000, timeout: 12000 }
    );
}

function onGeoError(err) {
    refreshGeoPermissionState().then(updateGeoPermissionUI);

    if (err && err.code === 1) {
        state.geoPermissionState = 'denied';
        setGeoPermissionHint('Map_GeoPrompt_Denied', true);
        hub.showToast(t('Toast_GeoDenied'), 6000);
        return;
    }
    if (err && err.code === 3) {
        hub.showToast(t('Toast_GeoTimeout'), 6000);
        return;
    }
    hub.showToast(t('Toast_GeoUnavailable'), 6000);
}

function requestUserLocation(onSuccess) {
    if (!window.isSecureContext) {
        setGeoPermissionHint('Map_GeoPrompt_Insecure', true);
        hub.showToast(t('Map_GeoPrompt_Insecure'), 6000);
        return;
    }

    if (!navigator.geolocation) {
        hub.showToast(t('Toast_GeoUnsupported'));
        return;
    }

    if (!mapEngine.isInitialized()) initMap();

    if (state.geoRequestInFlight) {
        return;
    }

    setGeoRequestLoading(true);
    setGeoPermissionHint(null, false);
    hub.showToast(t('Toast_GeoAcquiring'), 2500);

    let settled = false;
    const finish = () => {
        if (settled) return;
        settled = true;
        if (state.geoWatchdogId != null) {
            window.clearTimeout(state.geoWatchdogId);
            state.geoWatchdogId = null;
        }
        setGeoRequestLoading(false);
    };

    state.geoWatchdogId = window.setTimeout(() => {
        if (settled) return;
        stopGeoWatch();
        finish();
        hub.showToast(t('Toast_GeoTimeout'), 6000);
        updateGeoPermissionUI();
    }, 18000);

    const attempt = (highAccuracy) => {
        navigator.geolocation.getCurrentPosition(
            function (pos) {
                finish();
                try {
                    applyUserCoords(pos.coords.latitude, pos.coords.longitude, onSuccess);
                } catch (err) {
                    console.error('updateUserPosition failed', err);
                    hub.showToast(t('Toast_GeoDisabled'));
                    updateGeoPermissionUI();
                }
            },
            function (err) {
                if (!highAccuracy && (err && (err.code === 2 || err.code === 3))) {
                    attempt(true);
                    return;
                }
                if (!highAccuracy) {
                    tryWatchPosition(onSuccess, function (watchErr) {
                        finish();
                        onGeoError(watchErr);
                    });
                    return;
                }
                finish();
                onGeoError(err);
            },
            {
                enableHighAccuracy: highAccuracy,
                timeout: highAccuracy ? 12000 : 8000,
                maximumAge: highAccuracy ? 0 : 300000
            }
        );
    };

    attempt(false);
}

function startMapPickMode() {
    if (!mapEngine.isInitialized()) initMap();
    state.mapPickMode = true;
    cancelGeoRequest();
    $('#geo-permission-banner').addClass('d-none');
    const container = mapEngine.getContainer();
    if (container) container.style.cursor = 'crosshair';
    hub.showToast(t('Toast_GeoPickHint'), 5000);
}

function onMapPickClick(coords) {
    if (!state.mapPickMode) return;
    state.mapPickMode = false;
    const container = mapEngine.getContainer();
    if (container) container.style.cursor = '';
    try {
        applyUserCoords(coords.lat, coords.lng);
    } catch (err) {
        console.error('map pick failed', err);
        hub.showToast(t('Toast_GeoDisabled'));
    }
}

function locateUser(onSuccess) {
    requestUserLocation(onSuccess);
}

function initMap() {
    if (!mapEngine.isInitialized()) {
        mapEngine.init('map', {
            onViewChange: () => window.requestAnimationFrame(refreshMapView),
            onClick: onMapPickClick
        });
    }
    refreshMapView();
}

function fitMapToCoords(coordsList, options = {}) {
    if (!mapEngine.isInitialized() || !coordsList.length) return;
    mapEngine.fitBoundsFromCoords(coordsList, {
        padding: options.padding || [48, 48],
        maxZoom: options.maxZoom || 17,
        zoom: clampMapZoom(16),
        animate: options.animate !== false
    });
    window.setTimeout(refreshMapView, 350);
}

function openParkingMarkerPopup(p) {
    const reportId = p.id || p.Id;
    const marker = state.markers[reportId];
    if (!marker) return;
    refreshMapView();
    mapEngine.openPopup(marker);
}

function clampMapZoom(minZoom = 15) {
    if (!mapEngine.isInitialized()) return Math.min(minZoom, 18);
    return Math.min(Math.max(mapEngine.getZoom(), minZoom), 18);
}

function refreshMapView() {
    mapEngine.invalidateSize();
}

function centerMapOnCoords(coords, p, options = {}) {
    if (!mapEngine.isInitialized() || !coords) return;
    const { openPopup = true } = options;

    refreshMapView();
    mapEngine.setView(coords.lat, coords.lng, clampMapZoom(16), { animate: false });
    window.requestAnimationFrame(() => {
        refreshMapView();
        if (openPopup && p) {
            openParkingMarkerPopup(p);
        }
    });
}

function centerMapOnReport(p, options = {}) {
    if (!mapEngine.isInitialized() || !p) return;
    const coords = getParkingCoords(p);
    if (!coords) return;
    centerMapOnCoords(coords, p, options);
}

function focusMapOnParking(p, options = {}) {
    if (!mapEngine.isInitialized() || !p) return;
    const { openPopup = false, openDetail = true } = options;

    centerMapOnReport(p, { openPopup });

    if (openDetail) {
        hub.openParkingDetailModal(p);
    }
}

function clearSearchPreviewLayer() {
    mapEngine.clearLayerGroup('searchPreview');
    state.searchPreviewLayer = null;
}

function clearRequestMapLayers() {
    mapEngine.clearLayerGroup('requests');
    if (!mapEngine.isInitialized()) {
        state.requestLayers = {};
        return;
    }
    Object.values(state.requestLayers).forEach(layers => {
        if (layers.marker) mapEngine.removeLayer(layers.marker);
    });
    state.requestLayers = {};
}

function centerMapOnUser() {
    if (!mapEngine.isInitialized()) return;

    const flyToUser = () => {
        if (!state.userLocation) return;
        mapEngine.flyTo(
            state.userLocation.lat,
            state.userLocation.lng,
            Math.max(mapEngine.getZoom(), 16),
            { animate: true, duration: 0.75 }
        );
        if (state.userPositionMarker) {
            window.setTimeout(() => mapEngine.openPopup(state.userPositionMarker), 400);
        }
    };

    if (state.userLocation) {
        flyToUser();
        return;
    }

    hub.showToast(t('Toast_WaitGps'));
    locateUser(flyToUser);
}


  hub.showDashboard = showDashboard;
  hub.getBoundsFromCenter = getBoundsFromCenter;
  hub.distanceMeters = distanceMeters;
  hub.getItemCoords = getItemCoords;
  hub.applyReportCoords = applyReportCoords;
  hub.loadOwnReportCoordOverrides = loadOwnReportCoordOverrides;
  hub.saveOwnReportCoordOverride = saveOwnReportCoordOverride;
  hub.patchOwnReportCoords = patchOwnReportCoords;
  hub.getParkingCoords = getParkingCoords;
  hub.getVirtualZoneKey = getVirtualZoneKey;
  hub.isSameVirtualZone = isSameVirtualZone;
  hub.isWithinViewRange = isWithinViewRange;
  hub.isOwnListingOffZone = isOwnListingOffZone;
  hub.isOutsideViewRange = isOutsideViewRange;
  hub.refreshUserLocation = refreshUserLocation;
  hub.loadViewRadius = loadViewRadius;
  hub.formatViewRadiusLabel = formatViewRadiusLabel;
  hub.initViewRadiusControl = initViewRadiusControl;
  hub.applyViewRadius = applyViewRadius;
  hub.getVisibleRequests = getVisibleRequests;
  hub.getVisibleParkings = getVisibleParkings;
  hub.updateNearbyAreaCircle = updateNearbyAreaCircle;
  hub.updateUserPositionMarker = updateUserPositionMarker;
  hub.syncLocationToServer = syncLocationToServer;
  hub.scheduleLocationSync = scheduleLocationSync;
  hub.updateUserPosition = updateUserPosition;
  hub.setGeoRequestLoading = setGeoRequestLoading;
  hub.setGeoPermissionHint = setGeoPermissionHint;
  hub.refreshGeoPermissionState = refreshGeoPermissionState;
  hub.updateGeoPermissionUI = updateGeoPermissionUI;
  hub.stopGeoWatch = stopGeoWatch;
  hub.cancelGeoRequest = cancelGeoRequest;
  hub.applyUserCoords = applyUserCoords;
  hub.tryWatchPosition = tryWatchPosition;
  hub.onGeoError = onGeoError;
  hub.requestUserLocation = requestUserLocation;
  hub.startMapPickMode = startMapPickMode;
  hub.onMapPickClick = onMapPickClick;
  hub.locateUser = locateUser;
  hub.initMap = initMap;
  hub.fitMapToCoords = fitMapToCoords;
  hub.openParkingMarkerPopup = openParkingMarkerPopup;
  hub.clampMapZoom = clampMapZoom;
  hub.refreshMapView = refreshMapView;
  hub.centerMapOnCoords = centerMapOnCoords;
  hub.centerMapOnReport = centerMapOnReport;
  hub.focusMapOnParking = focusMapOnParking;
  hub.clearSearchPreviewLayer = clearSearchPreviewLayer;
  hub.clearRequestMapLayers = clearRequestMapLayers;
  hub.centerMapOnUser = centerMapOnUser;
}
