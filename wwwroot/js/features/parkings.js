import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';
import { mapEngine } from '../core/map-engine.js';

export function registerParkings(hub, t) {
// --- Parkings ---
async function loadParkings() {
    const seq = ++state.loadParkingsSeq;
    const preserveLocal = Date.now() < state.skipParkingReloadUntil;
    const previousById = new Map(state.parkings.map(p => [String(p.id || p.Id), p]));

    if (!state.userLocation && !state.currentUser) {
        if (seq !== state.loadParkingsSeq) return;
        state.parkings = [];
        renderParkings();
        return;
    }

    try {
        const nearbyPromise = state.userLocation
            ? hub.apiGet(`/api/parking/nearby?lat=${state.userLocation.lat}&lng=${state.userLocation.lng}&radius=${state.viewRadiusMeters}&pageSize=100`)
            : Promise.resolve({ items: [] });
        const minePromise = state.currentUser
            ? hub.apiGet('/api/parking/reports/mine')
            : Promise.resolve([]);

        const [nearbyResult, mineResult] = await Promise.allSettled([nearbyPromise, minePromise]);
        if (seq !== state.loadParkingsSeq) return;

        const nearby = nearbyResult.status === 'fulfilled' ? (nearbyResult.value?.items || []) : [];
        const mine = mineResult.status === 'fulfilled' ? (mineResult.value || []) : [];

        if (nearbyResult.status === 'rejected' && mineResult.status === 'rejected') {
            throw nearbyResult.reason || mineResult.reason;
        }

        const merged = new Map();
        [...nearby, ...mine].forEach(p => {
            if (isParkingActive(p)) {
                merged.set(String(p.id || p.Id), p);
            }
        });

        let nextParkings = Array.from(merged.values()).map(patchOwnReportCoords);
        nextParkings.forEach(p => {
            const prev = previousById.get(String(p.id || p.Id));
            if (!prev) return;
            const prevCoords = hub.resolveParkingMapCoords(prev);
            if (prevCoords && !hub.resolveParkingMapCoords(p)) {
                hub.applyReportCoords(p, prevCoords);
            }
        });

        if (preserveLocal) {
            previousById.forEach((prev, id) => {
                if (merged.has(id) || !isParkingActive(prev)) return;
                nextParkings.push(hub.patchOwnReportCoords({ ...prev }));
            });
        }

        if (seq !== state.loadParkingsSeq) return;
        state.parkings = nextParkings;
        renderParkings();
    } catch (err) {
        if (seq !== state.loadParkingsSeq) return;
        hub.showToast(t('Toast_LoadParkingsError'));
    }
}

function renderParkings() {
    if (!mapEngine.isInitialized()) return;

    mapEngine.clearLayerGroup('parkings');
    Object.values(state.markers).forEach(m => {
        if (mapEngine.hasLayer(m)) mapEngine.removeLayer(m);
    });
    state.markers = {};

    const visibleParkings = hub.getVisibleParkings();
    const $list = $('#parking-list').empty();
    if (visibleParkings.length === 0) {
        $list.html(`<div class="empty-list">${t('List_NoParkings', hub.formatViewRadiusLabel(state.viewRadiusMeters))}</div>`);
        return;
    }

    visibleParkings.forEach(p => {
        hub.patchOwnReportCoords(p);
        const isMine = hub.isMyReport(p);
        const rawCoords = hub.resolveParkingMapCoords(p);
        const coords = rawCoords ? hub.displayCoordsForParkingMarker(rawCoords) : null;
        const reportId = String(p.id || p.Id);

        if (coords) {
            const marker = mapEngine.addHtmlMarker(coords.lat, coords.lng, {
                className: 'parking-leaflet-icon',
                html: hub.buildParkingMapMarkerHtml(hub.getParkingMarkerColor(p), isMine),
                iconSize: [34, 42],
                iconAnchor: [17, 42],
                zIndexOffset: isMine ? 920 : 700,
                group: 'parkings'
            });

            mapEngine.onMarkerClick(marker, () => {
                const markerCoords = hub.resolveParkingMapCoords(p);
                if (!markerCoords) return;

                if (!hub.isMyReport(p) && !hub.isWithinViewRange(markerCoords.lat, markerCoords.lng)) {
                    hub.showToast(t('Toast_OutOfRange'));
                    return;
                }

                hub.focusMapOnParking(p, { openPopup: false, openDetail: true });
            });

            state.markers[reportId] = marker;
        }

        const timeLeft = getTimeLeft(p.expiresAt);
        const locateReportBtn = hub.buildLocateButtonHtml('report', reportId);
        const deleteReportBtn = isMine
            ? `<button type="button" class="btn-delete-item" data-type="report" data-id="${reportId}" title="${t('List_DelReport')}"><i class="bi bi-trash"></i></button>`
            : '';
        const itemClass = isMine ? 'parking-item parking-item-mine' : 'parking-item';
        const thumbHtml = p.photoUrl
            ? `<img src="${hub.escapeHtml(p.photoUrl)}" class="parking-thumb${isMine ? ' parking-thumb-mine' : ''}" alt="${t('List_PhotoAlt')}">`
            : `<div class="parking-dot parking-dot-icon ${p.markerColor}${isMine ? ' parking-dot-mine' : ''}" aria-hidden="true"><i class="bi bi-p-square-fill"></i></div>`;

        $list.append(`
            <div class="${itemClass}" data-id="${reportId}">
                ${thumbHtml}
                <div class="parking-item-info">
                    <div class="name">${hub.userProfileLink(p.createdByUserId, p.createdByUsername)}</div>
                    <div class="meta">
                        <i class="bi bi-hand-thumbs-up"></i> ${p.validVotes}
                        <i class="bi bi-hand-thumbs-down ms-2"></i> ${p.invalidVotes}
                        <span class="ms-2 text-muted">· ${Math.round(p.confidenceScore)}%</span>
                    </div>
                </div>
                <div class="request-item-actions">
                    <div class="item-action-buttons">${locateReportBtn}${deleteReportBtn}</div>
                    <span class="time-left" data-expires-at="${p.expiresAt}">${timeLeft}</span>
                </div>
            </div>
        `);
    });

    $('.parking-item, .parking-item-mine').on('click', function (e) {
        if ($(e.target).closest('.btn-delete-item, .btn-locate-item').length) {
            return;
        }
        const id = $(this).data('id');
        const p = state.parkings.find(x => (x.id || x.Id) === id);
        if (!p) return;
        const coords = hub.resolveParkingMapCoords(p);
        if (!coords) return;

        if (hub.isMyReport(p)) {
            hub.focusMapOnParking(p);
            return;
        }

        if (!hub.isWithinViewRange(coords.lat, coords.lng)) {
            hub.showToast(t('Toast_OutOfRange'));
            return;
        }

        hub.focusMapOnParking(p, { openPopup: false, openDetail: true });
    });

    $('.parking-item .btn-delete-item, .parking-item-mine .btn-delete-item').on('click', function (e) {
        e.stopPropagation();
        hub.deleteListing($(this).data('type'), $(this).data('id'));
    });
}

function upsertParking(p, fallbackCoords) {
    hub.ensureParkingMapCoords(p, fallbackCoords);
    state.loadParkingsSeq++;
    const parkingId = p.id || p.Id;
    const idx = state.parkings.findIndex(x => (x.id || x.Id) === parkingId);
    const keepDespiteRange = hub.isMyReport(p);
    const coords = hub.getParkingCoords(p);

    if (!keepDespiteRange && coords && !hub.isWithinViewRange(coords.lat, coords.lng)) {
        if (idx >= 0) state.parkings.splice(idx, 1);
        renderParkings();
        return;
    }

    if (!isParkingStatusActive(p) || !isParkingActive(p)) {
        if (idx >= 0) state.parkings.splice(idx, 1);
    } else if (idx >= 0) {
        state.parkings[idx] = p;
    } else {
        state.parkings.push(p);
    }
    renderParkings();
}

function parseUtcDate(value) {
    if (!value) return new Date(0);
    const str = String(value).trim();
    if (/[zZ]$/.test(str) || /[+-]\d{2}:\d{2}$/.test(str)) {
        return new Date(str);
    }
    return new Date(str + 'Z');
}

function isParkingStatusActive(p) {
    const status = p?.status ?? p?.Status;
    return status === 0 || status === 'Active';
}

function isParkingActive(p) {
    const expiresAt = p?.expiresAt ?? p?.ExpiresAt;
    return isParkingStatusActive(p) && parseUtcDate(expiresAt) > new Date();
}

function getTimeLeft(expiresAt) {
    const diff = parseUtcDate(expiresAt) - new Date();
    if (diff <= 0) return t('Time_Expired');
    const hours = Math.floor(diff / 3600000);
    const mins = Math.floor((diff % 3600000) / 60000);
    const secs = Math.floor((diff % 60000) / 1000);
    const pad = n => String(n).padStart(2, '0');
    if (hours > 0) return `${hours}:${pad(mins)}:${pad(secs)}`;
    return `${mins}:${pad(secs)}`;
}

function updateCountdowns() {
    let needParkingRefresh = false;
    let needRequestRefresh = false;
    const expiredLabel = t('Time_Expired');

    $('.time-left[data-expires-at]').each(function () {
        const expiresAt = $(this).attr('data-expires-at');
        const diff = parseUtcDate(expiresAt) - new Date();
        const left = getTimeLeft(expiresAt);
        $(this).text(left);
        $(this).toggleClass('is-urgent', diff > 0 && diff < 5 * 60000);
        if (left === expiredLabel) {
            const $item = $(this).closest('.parking-item');
            if ($item.hasClass('request-item') || $item.hasClass('request-item-mine')) {
                needRequestRefresh = true;
            } else if ($item.length) {
                needParkingRefresh = true;
            }
        }
    });

    state.parkings.forEach(p => {
        const marker = state.markers[String(p.id || p.Id)];
        if (marker && mapEngine.isPopupOpen(marker)) {
            mapEngine.setPopupContent(marker, hub.buildPopupContent(p));
        }
    });

    Object.values(state.requestLayers).forEach(layers => {
        const marker = layers?.marker;
        if (marker && mapEngine.isPopupOpen(marker)) {
            const id = Object.keys(state.requestLayers).find(k => state.requestLayers[k].marker === marker);
            const r = hub.findRequestById(id);
            if (r) mapEngine.setPopupContent(marker, hub.buildRequestPopupContent(r));
        }
    });

    if (needParkingRefresh) {
        state.parkings = state.parkings.filter(isParkingActive);
        renderParkings();
    }
    if (needRequestRefresh) {
        state.parkingRequests = state.parkingRequests.filter(isRequestActive);
        hub.renderRequests();
    }

    hub.updateSuspensionCountdown();
}

function startCountdownTimer() {
    stopCountdownTimer();
    updateCountdowns();
    state.countdownTimerId = setInterval(updateCountdowns, 1000);
}

function stopCountdownTimer() {
    if (state.countdownTimerId !== null) {
        clearInterval(state.countdownTimerId);
        state.countdownTimerId = null;
    }
}


  hub.loadParkings = loadParkings;
  hub.renderParkings = renderParkings;
  hub.upsertParking = upsertParking;
  hub.parseUtcDate = parseUtcDate;
  hub.isParkingStatusActive = isParkingStatusActive;
  hub.isParkingActive = isParkingActive;
  hub.getTimeLeft = getTimeLeft;
  hub.updateCountdowns = updateCountdowns;
  hub.startCountdownTimer = startCountdownTimer;
  hub.stopCountdownTimer = stopCountdownTimer;
}
