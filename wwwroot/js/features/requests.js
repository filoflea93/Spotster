import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';
import { mapEngine } from '../core/map-engine.js';

export function registerRequests(hub, t) {
// --- Requests & Chat ---

function getMsgReadMap() {
    try {
        return JSON.parse(localStorage.getItem(C.MSG_READ_KEY) || '{}');
    } catch {
        return {};
    }
}

function getThreadReadKey(requestId, guestUserId) {
    return `${requestId}_thread_${guestUserId}`;
}

function markThreadRead(messages) {
    if (!state.activeChatRequestId || !state.activeChatGuestUserId || !state.currentUser) return;

    const fromOthers = (messages || []).filter(m => !m.isMine).length;
    const key = getThreadReadKey(state.activeChatRequestId, state.activeChatGuestUserId);
    const readMap = getMsgReadMap();
    readMap[key] = fromOthers;
    localStorage.setItem(C.MSG_READ_KEY, JSON.stringify(readMap));
    updateRequestUnreadInCache(state.activeChatRequestId, state.activeChatGuestUserId, fromOthers);
    renderRequests();
    updateRequestsTabUnreadIndicator();
}

function updateRequestUnreadInCache(requestId, guestUserId, readCount) {
    const threads = state.ownerConversationCache[requestId];
    if (!threads) return;
    const thread = threads.find(t => t.guestUserId === guestUserId);
    if (thread) {
        thread.incomingFromGuestCount = Math.max(thread.incomingFromGuestCount || 0, readCount);
    }
}

function getThreadUnreadCount(requestId, conv) {
    if (!conv || !state.currentUser) return 0;
    const guestId = conv.guestUserId || conv.GuestUserId;
    const key = getThreadReadKey(requestId, guestId);
    const readMap = getMsgReadMap();
    const incoming = conv.incomingFromGuestCount || conv.IncomingFromGuestCount || 0;
    const lastRead = readMap[key] || 0;
    return Math.max(0, incoming - lastRead);
}

function sortOwnerConversations(conversations) {
    return [...conversations].sort((a, b) => {
        const unreadA = getThreadUnreadCount(state.activeChatRequestId, a);
        const unreadB = getThreadUnreadCount(state.activeChatRequestId, b);
        if (unreadA !== unreadB) return unreadB - unreadA;
        const ta = a.lastMessageAt ? hub.parseUtcDate(a.lastMessageAt).getTime() : 0;
        const tb = b.lastMessageAt ? hub.parseUtcDate(b.lastMessageAt).getTime() : 0;
        return tb - ta;
    });
}

function pickDefaultGuestConversation(conversations) {
    const sorted = sortOwnerConversations(conversations);
    const first = sorted[0];
    return first ? (first.guestUserId || first.GuestUserId) : null;
}

function formatChatTime(value) {
    if (!value) return '';
    return hub.parseUtcDate(value).toLocaleString([], {
        day: '2-digit',
        month: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function resetChatThreadPanel() {
    $('#chat-thread-list').addClass('d-none');
    $('#chat-thread-panel-list').empty();
    $('#chat-layout').removeClass('has-threads');
    $('#chat-modal .chat-modal-content').removeClass('chat-has-threads');
}

function sameUserId(a, b) {
    if (a === undefined || a === null || b === undefined || b === null) return false;
    return String(a).toLowerCase() === String(b).toLowerCase();
}

function getSignalRValue(data, camelKey, pascalKey) {
    return hub.prop(data, camelKey, pascalKey);
}

function isMyRequest(r) {
    const ownerId = r.createdByUserId || r.CreatedByUserId;
    return !!(state.currentUser && ownerId && sameUserId(ownerId, state.currentUser.userId));
}

function getParkingMarkerColor(p) {
    const c = p?.markerColor ?? p?.MarkerColor;
    return (c === 'green' || c === 'yellow' || c === 'red') ? c : 'yellow';
}

function resolveParkingMapCoords(p) {
    return hub.getParkingCoords(p) || hub.getItemCoords(p);
}

function ensureParkingMapCoords(p, fallbackCoords) {
    hub.patchOwnReportCoords(p);
    if (resolveParkingMapCoords(p)) return p;
    if (fallbackCoords) {
        hub.applyReportCoords(p, fallbackCoords);
        hub.saveOwnReportCoordOverride(p.id || p.Id, fallbackCoords);
    }
    return p;
}

function displayCoordsForParkingMarker(coords) {
    if (!coords || !state.userLocation) return coords;
    if (hub.distanceMeters(coords.lat, coords.lng, state.userLocation.lat, state.userLocation.lng) > 4) {
        return coords;
    }
    const meters = 14;
    const latOff = (meters / 6371000) * (180 / Math.PI);
    const lngOff = latOff / Math.cos(coords.lat * Math.PI / 180);
    return { lat: coords.lat + latOff, lng: coords.lng + lngOff };
}

function buildParkingMapMarkerHtml(markerColor, isMine) {
    const color = (markerColor === 'green' || markerColor === 'yellow' || markerColor === 'red')
        ? markerColor : 'yellow';
    const mineClass = isMine ? ' parking-marker-mine' : '';
    return `<div class="parking-marker-pin ${color}${mineClass}" aria-hidden="true">
        <div class="parking-marker-head"><i class="bi bi-p-square-fill"></i></div>
        <div class="parking-marker-tip"></div>
    </div>`;
}

function isMyReport(p) {
    const ownerId = p.createdByUserId || p.CreatedByUserId;
    return !!(state.currentUser && ownerId && sameUserId(ownerId, state.currentUser.userId));
}

function getRequestId(r) {
    return r.id || r.Id;
}

function findRequestById(id) {
    return state.parkingRequests.find(x => getRequestId(x) === id);
}

function getRequestsForList() {
    const mine = state.parkingRequests.filter(isMyRequest);
    const others = state.parkingRequests.filter(r =>
        !isMyRequest(r) && state.userLocation && hub.isWithinViewRange(r.latitude, r.longitude)
    );
    return [...mine, ...others];
}

function isRequestReserved(r) {
    return !!r.isReserved || r.status === 3 || r.status === 'Reserved';
}

function passesRequestsDealFilter(r) {
    if (state.requestsDealFilter === 'indeal') return isRequestReserved(r);
    if (state.requestsDealFilter === 'open') return !isRequestReserved(r);
    return true;
}

function getFilteredRequestsForList() {
    return getRequestsForList().filter(passesRequestsDealFilter);
}

function getRequestsForMap() {
    return getFilteredRequestsForList();
}

function getUnreadIncoming(r) {
    if (!state.currentUser) return 0;

    const readMap = getMsgReadMap();

    if (isMyRequest(r)) {
        const threads = state.ownerConversationCache[r.id];
        if (threads && threads.length > 0) {
            return threads.reduce((sum, t) => {
                const key = getThreadReadKey(r.id, t.guestUserId);
                const incoming = t.incomingFromGuestCount || 0;
                const lastRead = readMap[key] || 0;
                return sum + Math.max(0, incoming - lastRead);
            }, 0);
        }
    } else {
        const key = getThreadReadKey(r.id, state.currentUser.userId);
        const incoming = r.incomingMessageCount || 0;
        const lastRead = readMap[key] || 0;
        return Math.max(0, incoming - lastRead);
    }

    const legacyKey = r.id;
    const incoming = r.incomingMessageCount || 0;
    const lastRead = readMap[legacyKey] || 0;
    return Math.max(0, incoming - lastRead);
}

function hasUnreadMessages(r) {
    return getUnreadIncoming(r) > 0;
}

function updateRequestsTabUnreadIndicator() {
    const hasUnread = state.parkingRequests.some(r => hasUnreadMessages(r));
    $('#listings-tabs .nav-link[data-sheet="requests"]').toggleClass('has-unread', hasUnread);
}

function switchListingsSheet(sheet) {
    if (sheet === 'state.parkings') sheet = 'parkings';
    state.activeSheet = sheet;
    $('#listings-tabs .nav-link').removeClass('active').attr('aria-selected', 'false');
    $(`#listings-tabs .nav-link[data-sheet="${sheet}"]`).addClass('active').attr('aria-selected', 'true');
    $('#requests-deal-filter').toggleClass('d-none', sheet !== 'requests');
    if (sheet === 'parkings') {
        $('#parking-list').removeClass('d-none');
        $('#request-list').addClass('d-none').empty();
    } else {
        $('#parking-list').addClass('d-none').empty();
        $('#request-list').removeClass('d-none');
        loadRequests();
    }
    hub.renderParkings();
    renderRequests();
}

async function refreshOwnerConversationCache() {
    if (!state.currentUser) return;

    const mine = state.parkingRequests.filter(r => isMyRequest(r));
    await Promise.all(mine.map(async r => {
        try {
            state.ownerConversationCache[r.id] = await hub.apiGet(`/api/parking/requests/${r.id}/conversations`);
        } catch {
            state.ownerConversationCache[r.id] = state.ownerConversationCache[r.id] || [];
        }
    }));
}

async function loadRequests() {
    const seq = ++state.loadRequestsSeq;
    try {
        const nearbyPromise = state.userLocation
            ? hub.apiGet(`/api/parking/requests/nearby?lat=${state.userLocation.lat}&lng=${state.userLocation.lng}&radius=${state.viewRadiusMeters}`)
            : Promise.resolve([]);
        const minePromise = state.currentUser
            ? hub.apiGet('/api/parking/requests/mine')
            : Promise.resolve([]);

        const [nearbyResult, mineResult] = await Promise.allSettled([nearbyPromise, minePromise]);
        if (seq !== state.loadRequestsSeq) return;

        const nearby = nearbyResult.status === 'fulfilled' ? (nearbyResult.value || []) : [];
        const mine = mineResult.status === 'fulfilled' ? (mineResult.value || []) : [];

        if (nearbyResult.status === 'rejected' && mineResult.status === 'rejected') {
            throw nearbyResult.reason || mineResult.reason;
        }

        const merged = new Map();
        [...nearby, ...mine].forEach(r => {
            if (isRequestStatusActive(r) && isRequestActive(r)) {
                merged.set(r.id, r);
            }
        });
        state.parkingRequests = Array.from(merged.values());
        if (seq !== state.loadRequestsSeq) return;

        await refreshOwnerConversationCache();
        if (seq !== state.loadRequestsSeq) return;

        renderRequests();
    } catch (err) {
        if (seq !== state.loadRequestsSeq) return;
        hub.showToast(t('Toast_LoadRequestsError'));
    }
}

function isRequestStatusActive(r) {
    return r.status === 0 || r.status === 'Active'
        || r.status === 3 || r.status === 'Reserved';
}

function isRequestActive(r) {
    return hub.parseUtcDate(r.expiresAt) > new Date();
}

function upsertRequest(r) {
    if (!r || !isRequestStatusActive(r) || !isRequestActive(r)) return;
    const idx = state.parkingRequests.findIndex(x => x.id === r.id);
    if (idx >= 0) {
        state.parkingRequests[idx] = r;
    } else {
        state.parkingRequests.push(r);
    }
}

function focusMapOnRequest(r) {
    if (!mapEngine.isInitialized() || !r) return;
    mapEngine.setView(r.latitude, r.longitude, Math.max(mapEngine.getZoom(), 15), { animate: true });
}

function parseChatLocationMessage(content) {
    if (!content || !String(content).startsWith(C.CHAT_LOCATION_PREFIX)) return null;
    const payload = String(content).slice(C.CHAT_LOCATION_PREFIX.length);
    const parts = payload.split(',');
    if (parts.length !== 2) return null;
    const lat = parseFloat(parts[0]);
    const lng = parseFloat(parts[1]);
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) return null;
    if (lat < -90 || lat > 90 || lng < -180 || lng > 180) return null;
    return { lat, lng };
}

function formatChatLocationMessage(lat, lng) {
    return `${C.CHAT_LOCATION_PREFIX}${lat.toFixed(6)},${lng.toFixed(6)}`;
}

function parseChatPhotoMessage(content) {
    if (!content || !String(content).startsWith(C.CHAT_PHOTO_PREFIX)) return null;
    const photoUrl = String(content).slice(C.CHAT_PHOTO_PREFIX.length).trim();
    if (!photoUrl.startsWith('/uploads/chat/') || photoUrl.includes('..')) return null;
    return photoUrl;
}

function formatChatMessagePreview(content) {
    if (!content) return '';
    if (content === 'Chat_LocationPreview' || content === 'Chat_PhotoPreview') {
        return t(content);
    }
    if (parseChatPhotoMessage(content)) {
        return t('Chat_PhotoPreview');
    }
    if (parseChatLocationMessage(content)) {
        return t('Chat_LocationPreview');
    }
    return content;
}

function getChatBubbleExtraClass(content) {
    if (parseChatPhotoMessage(content)) return ' chat-bubble-photo';
    if (parseChatLocationMessage(content)) return ' chat-bubble-location';
    return '';
}

function getChatLocateKey(lat, lng) {
    return `${lat.toFixed(6)},${lng.toFixed(6)}`;
}

function clearChatLocationMarker() {
    mapEngine.clearLayerGroup('chatLocation');
    state.activeChatLocateKey = null;
    $('.chat-locate-btn').removeClass('is-active');
}

function syncActiveChatLocateButtons() {
    $('.chat-locate-btn').removeClass('is-active');
    if (!state.activeChatLocateKey) return;
    $(`.chat-locate-btn[data-locate-key="${state.activeChatLocateKey}"]`).addClass('is-active');
}

function focusMapOnCoordinates(lat, lng, $btn) {
    if (!mapEngine.isInitialized()) return;

    const key = getChatLocateKey(lat, lng);
    if (state.activeChatLocateKey === key) {
        mapEngine.setView(lat, lng, Math.max(mapEngine.getZoom(), 17), { animate: true });
        return;
    }

    clearChatLocationMarker();

    const closeLabel = hub.escapeHtml(t('Modal_Chat_RemoveLocationFlag'));
    const marker = mapEngine.addHtmlMarker(lat, lng, {
        className: 'chat-location-flag-icon',
        html: `<div class="chat-location-flag-wrap">
            <button type="button" class="chat-location-flag-close" aria-label="${closeLabel}" title="${closeLabel}">
                <i class="bi bi-x-lg"></i>
            </button>
            <div class="chat-location-flag-marker" aria-hidden="true"><i class="bi bi-flag-fill"></i></div>
        </div>`,
        iconSize: [52, 44],
        iconAnchor: [6, 40],
        zIndexOffset: 1100,
        group: 'chatLocation'
    });
    state.activeChatLocateKey = key;

    if ($btn) {
        $btn.addClass('is-active');
    } else {
        syncActiveChatLocateButtons();
    }

    mapEngine.setView(lat, lng, Math.max(mapEngine.getZoom(), 17), { animate: true });
}

function renderChatMessageBody(content) {
    const photoUrl = parseChatPhotoMessage(content);
    if (photoUrl) {
        return `
            <a href="${hub.escapeHtml(photoUrl)}" target="_blank" rel="noopener noreferrer" class="chat-photo-link">
                <img src="${hub.escapeHtml(photoUrl)}" class="chat-message-photo" alt="${hub.escapeHtml(t('Modal_Chat_PhotoAlt'))}" loading="lazy">
            </a>
        `;
    }

    const location = parseChatLocationMessage(content);
    if (!location) {
        return hub.escapeHtml(content);
    }

    const coordsText = `${location.lat.toFixed(5)}, ${location.lng.toFixed(5)}`;
    const locateKey = getChatLocateKey(location.lat, location.lng);
    return `
        <div class="chat-location-message">
            <div class="chat-location-label">
                <i class="bi bi-geo-alt-fill"></i>
                <span>${hub.escapeHtml(t('Modal_Chat_LocationShared'))}</span>
            </div>
            <div class="chat-location-coords">${hub.escapeHtml(coordsText)}</div>
            <button type="button" class="chat-locate-btn" data-lat="${location.lat}" data-lng="${location.lng}" data-locate-key="${locateKey}">
                <i class="bi bi-bullseye"></i>
                <span>${hub.escapeHtml(t('Modal_Chat_LocateOnMap'))}</span>
            </button>
        </div>
    `;
}

function isReservedForOtherParty(request) {
    if (!request || !isRequestReserved(request)) return false;
    const reservedId = request.reservedByUserId || request.ReservedByUserId;
    if (!reservedId) return false;

    if (state.activeChatIsMine) {
        return !!(state.activeChatGuestUserId && !sameUserId(state.activeChatGuestUserId, reservedId));
    }

    return !sameUserId(state.currentUser.userId, reservedId);
}

function isChatMessagingBlocked(request) {
    if (!request) return false;
    if (!state.activeChatIsMine && (
        request.isBlockedByOwner
        || isGuestBlockedInConversation(state.activeChatRequestId, state.activeChatGuestUserId)
    )) {
        return true;
    }
    return isReservedForOtherParty(request);
}

function canSendChatMessage() {
    if (!state.activeChatRequestId) return false;
    if (state.activeChatIsMine && !state.activeChatGuestUserId) {
        hub.showToast(t('Toast_SelectConversation'));
        return false;
    }

    const request = findRequestById(state.activeChatRequestId);
    if (isChatMessagingBlocked(request)) {
        if (!state.activeChatIsMine && (
            request?.isBlockedByOwner
            || isGuestBlockedInConversation(state.activeChatRequestId, state.activeChatGuestUserId)
        )) {
            hub.showToast(t('Modal_Chat_BlockedCantWrite'));
        } else {
            hub.showToast(t('Toast_RequestInDeal'));
        }
        return false;
    }

    return true;
}

async function postChatMessage(content) {
    const body = { content };
    if (state.activeChatIsMine) {
        body.replyToUserId = state.activeChatGuestUserId;
    }
    await hub.apiPost(`/api/parking/requests/${state.activeChatRequestId}/messages`, body);
    await loadChatMessages();
    await loadRequests();
    syncReserveButton(findRequestById(state.activeChatRequestId));
}

function buildLocateButtonHtml(type, id, extraClass = '') {
    const cls = extraClass ? ` ${extraClass}` : '';
    return `<button type="button" class="btn-locate-item${cls}" data-type="${type}" data-id="${id}" title="${t('List_LocateOnMap')}"><i class="bi bi-crosshair"></i></button>`;
}

function locateListingOnMap(type, id) {
    if (type === 'report') {
        const p = state.parkings.find(x => String(x.id || x.Id) === String(id));
        if (!p) return;
        const coords = resolveParkingMapCoords(p);
        if (!coords) {
            hub.showToast(t('Toast_LocationUnavailable'));
            return;
        }
        if (!isMyReport(p) && !hub.isWithinViewRange(coords.lat, coords.lng)) {
            hub.showToast(t('Toast_OutOfRange'));
            return;
        }
        hub.focusMapOnParking(p, { openPopup: false, openDetail: false });
        return;
    }

    if (type === 'request') {
        const r = findRequestById(id);
        if (!r) return;
        focusMapOnRequest(r);
    }
}

function buildRequestActionButtonsHtml(r, extraClass = '') {
    const mine = isMyRequest(r);
    const requestId = getRequestId(r);
    const cls = extraClass ? ` ${extraClass}` : '';
    const editBtn = mine
        ? `<button type="button" class="btn-edit-request${cls}" data-id="${requestId}" title="${t('List_EditAd')}"><i class="bi bi-pencil"></i></button>`
        : '';
    const renewBtn = mine && r.canRenew
        ? `<button type="button" class="btn-renew-request${cls}" data-id="${requestId}" title="${t('List_Renew')}"><i class="bi bi-arrow-clockwise"></i></button>`
        : '';
    const deleteRequestBtn = mine
        ? `<button type="button" class="btn-delete-item${cls}" data-type="request" data-id="${requestId}" title="${t('List_DelAd')}"><i class="bi bi-trash"></i></button>`
        : '';
    const locateBtn = buildLocateButtonHtml('request', requestId, extraClass);
    return { editBtn, renewBtn, deleteRequestBtn, locateBtn };
}

function buildRequestPopupContent(r) {
    const mine = isMyRequest(r);
    const ownerId = r.createdByUserId || r.CreatedByUserId;
    const ownerName = r.createdByUsername || r.CreatedByUsername || '';
    const requestId = getRequestId(r);
    const reservedBadge = r.isReserved
        ? ` <span class="request-reserved-badge">${hub.escapeHtml(t('Request_InDealBadge'))}</span>`
        : '';
    const rewardHtml = r.rewardAmount
        ? `<div class="request-map-popup-reward"><i class="bi bi-cash-coin"></i> €${Number(r.rewardAmount).toFixed(2)}</div>`
        : '';
    const paymentsHtml = r.paymentMethods && r.paymentMethods.length
        ? `<div class="request-map-popup-payments">${r.paymentMethods.map(c => hub.escapeHtml(paymentMethodDisplayLabel(c))).join(' · ')}</div>`
        : '';
    const { editBtn, renewBtn, deleteRequestBtn, locateBtn } = buildRequestActionButtonsHtml(r, 'map-popup-action');
    const chatBtn = `<button type="button" class="btn-open-request-chat map-popup-action" data-id="${requestId}" title="${t('List_Contact')}"><i class="bi bi-chat-dots"></i></button>`;
    const actionsHtml = [locateBtn, editBtn, renewBtn, deleteRequestBtn, chatBtn].filter(Boolean).join('');

    return `
        <div class="request-map-popup">
            <div class="request-map-popup-header">
                <strong>${hub.userProfileLink(ownerId, ownerName)}</strong>${mine ? ` <small>(${hub.escapeHtml(t('List_MyAd'))})</small>` : ''}${reservedBadge}
            </div>
            <div class="request-map-popup-address">${hub.escapeHtml(r.address)}</div>
            <div class="request-map-popup-meta">
                <small>${hub.escapeHtml(t('Modal_Search_Radius'))}: ${r.radiusMeters}m · ${hub.getTimeLeft(r.expiresAt)}</small>
            </div>
            ${rewardHtml}
            ${paymentsHtml}
            ${actionsHtml ? `<div class="request-map-popup-actions">${actionsHtml}</div>` : ''}
        </div>
    `;
}

function renderRequests() {
    if (!mapEngine.isInitialized()) return;

    hub.clearRequestMapLayers();

    const listRequests = getFilteredRequestsForList();
    const showRequestList = state.activeSheet === 'requests';
    const $list = showRequestList ? $('#request-list').empty() : null;

    if (showRequestList && listRequests.length === 0) {
        const radiusLabel = hub.formatViewRadiusLabel(state.viewRadiusMeters);
        const msg = state.requestsDealFilter === 'all'
            ? t('List_NoRequests', radiusLabel)
            : t('List_NoRequestsFiltered', radiusLabel);
        $list.html(`<div class="empty-list">${msg}</div>`);
    }

    if (state.activeSheet === 'requests') {
        listRequests.forEach(r => {
            const mine = isMyRequest(r);
            const offZone = mine && hub.isOwnListingOffZone(r);
            const reserved = isRequestReserved(r);

            let circle = null;
            if (!reserved) {
                circle = mapEngine.addCircle(r.latitude, r.longitude, {
                    radius: r.radiusMeters,
                    color: mine ? '#3DAA6D' : '#3B7DD8',
                    weight: mine ? 3 : 2,
                    fillColor: mine ? '#3DAA6D' : '#3B7DD8',
                    fillOpacity: mine ? 0.14 : 0.12,
                    className: mine ? 'request-zone-circle request-zone-circle-mine' : 'request-zone-circle',
                    dashArray: offZone ? '5 7' : '8 6'
                }, { group: 'requests' });
            }

            let markerHtml;
            if (mine) {
                markerHtml = reserved
                    ? '<div class="request-marker request-marker-mine request-marker-reserved"><i class="bi bi-handshake"></i></div>'
                    : '<div class="request-marker request-marker-mine"><i class="bi bi-star-fill"></i></div>';
            } else {
                markerHtml = reserved
                    ? '<div class="request-marker request-marker-reserved"><i class="bi bi-handshake"></i></div>'
                    : '<div class="request-marker">?</div>';
            }

            const marker = mapEngine.addHtmlMarker(r.latitude, r.longitude, {
                html: markerHtml,
                iconSize: [28, 28],
                iconAnchor: [14, 14],
                zIndexOffset: mine ? 900 : 800,
                addToMap: true
            });
            mapEngine.bindPopup(marker, buildRequestPopupContent(r));

            state.requestLayers[getRequestId(r)] = { circle, marker };
        });
    }

    if (showRequestList) {
        listRequests.forEach(r => {
            const mine = isMyRequest(r);
            const offZone = mine && hub.isOwnListingOffZone(r);
            const requestId = getRequestId(r);
            const timeLeft = hub.getTimeLeft(r.expiresAt);
            const ownerId = r.createdByUserId || r.CreatedByUserId;
            const ownerName = r.createdByUsername || r.CreatedByUsername || '';
            const rewardHtml = r.rewardAmount
                ? `<div class="request-reward"><i class="bi bi-cash-coin"></i> €${Number(r.rewardAmount).toFixed(2)}</div>`
                : '';
            const paymentsHtml = r.paymentMethods && r.paymentMethods.length
                ? `<div class="request-payments">${r.paymentMethods.map(c => hub.escapeHtml(paymentMethodDisplayLabel(c))).join(' · ')}</div>`
                : '';
            const msgBadge = hasUnreadMessages(r)
                ? `<span class="message-badge message-badge-unread">${getUnreadIncoming(r)}</span>`
                : '';
            const dotHtml = hasUnreadMessages(r)
                ? '<div class="parking-dot parking-dot-notify"></div>'
                : '<div class="parking-dot parking-dot-default"></div>';
            const itemClass = isMyRequest(r) ? 'request-item request-item-mine' : 'request-item';
            const title = hub.userProfileLink(ownerId, ownerName);
            const reservedBadge = r.isReserved
                ? `<span class="request-reserved-badge">${hub.escapeHtml(t('Request_InDealBadge'))}</span>`
                : '';
            const offZoneHtml = offZone ? ` · <i class="bi bi-pin-map"></i> ${t('List_MyOffZone')}` : '';
            const threadCount = (state.ownerConversationCache[requestId] || []).length;
            const metaLine = isMyRequest(r)
                ? (threadCount > 1
                    ? `<i class="bi bi-people"></i> ${t('List_ConversationsCount', threadCount)}${offZoneHtml}`
                    : `<i class="bi bi-chat-dots"></i> ${t('List_MsgsReceived')}${offZoneHtml}`)
                : `<i class="bi bi-bullseye"></i> ${r.radiusMeters}m · <i class="bi bi-chat-dots"></i> ${t('List_Contact')}`;

            const { editBtn, renewBtn, deleteRequestBtn, locateBtn } = buildRequestActionButtonsHtml(r);

            $list.append(`
                <div class="parking-item ${itemClass}" data-id="${requestId}">
                    ${dotHtml}
                    <div class="parking-item-info">
                        <div class="name">${title}${reservedBadge}${msgBadge}</div>
                        <div class="meta text-truncate">${hub.escapeHtml(r.address)}</div>
                        <div class="meta">${metaLine}</div>
                        ${rewardHtml}
                        ${paymentsHtml}
                    </div>
                    <div class="request-item-actions">
                        <div class="item-action-buttons">
                            ${locateBtn}
                            ${editBtn}
                            ${renewBtn}
                            ${deleteRequestBtn}
                        </div>
                        <span class="time-left" data-expires-at="${r.expiresAt}">${timeLeft}</span>
                    </div>
                </div>
            `);
        });
    }

    if (showRequestList && listRequests.length > 0) {
        $('#request-list .request-item, #request-list .request-item-mine').on('click', function (e) {
            if ($(e.target).closest('.btn-delete-item, .btn-renew-request, .btn-edit-request, .btn-locate-item').length) {
                return;
            }
            const id = $(this).data('id');
            const r = findRequestById(id);
            if (r) focusMapOnRequest(r);
            openChat(id);
        });
    }

    Object.values(state.requestLayers).forEach(({ marker }) => {
        if (marker && marker.isPopupOpen()) {
            const id = Object.keys(state.requestLayers).find(k => state.requestLayers[k].marker === marker);
            const r = findRequestById(id);
            if (r) marker.setPopupContent(buildRequestPopupContent(r));
        }
    });

    updateRequestsTabUnreadIndicator();
}

$(document).on('click', '.btn-locate-item', function (e) {
    e.stopPropagation();
    locateListingOnMap($(this).data('type'), $(this).data('id'));
});

$(document).on('click', '.btn-renew-request', function (e) {
    e.stopPropagation();
    renewRequest($(this).data('id'));
});

$(document).on('click', '.btn-edit-request', function (e) {
    e.stopPropagation();
    openEditRequest($(this).data('id'));
});

$(document).on('click', '.request-item .btn-delete-item, .request-item-mine .btn-delete-item, .request-map-popup .btn-delete-item', function (e) {
    e.stopPropagation();
    deleteListing($(this).data('type'), $(this).data('id'));
});

$(document).on('click', '.btn-open-request-chat', function (e) {
    e.stopPropagation();
    const id = $(this).data('id');
    const r = findRequestById(id);
    if (r) focusMapOnRequest(r);
    openChat(id);
});

async function renewRequest(requestId) {
    try {
        await hub.apiPost(`/api/parking/requests/${requestId}/renew`, {});
        hub.showToast(t('Toast_AdRenewed'));
        await loadRequests();
    } catch (err) {
        hub.showToast(err.message);
    }
}

function isGuestBlockedInConversation(requestId, guestUserId) {
    if (!requestId || !guestUserId) return false;
    const conv = (state.ownerConversationCache[requestId] || []).find(c =>
        sameUserId(c.guestUserId || c.GuestUserId, guestUserId));
    return !!(conv?.isBlocked || conv?.IsBlocked);
}

function ownerCanReserveForGuest(request, guestUserId) {
    if (!request || !guestUserId || request.isReserved) return false;
    const conv = (state.ownerConversationCache[getRequestId(request)] || []).find(c =>
        sameUserId(c.guestUserId || c.GuestUserId, guestUserId));
    if (!conv || conv.isBlocked || conv.IsBlocked) return false;
    return (conv.incomingFromGuestCount || conv.IncomingFromGuestCount || 0) > 0;
}

function syncChatInputBar(request) {
    const blocked = isChatMessagingBlocked(request);
    $('#chat-input-bar').toggleClass('d-none', blocked);
    $('#chat-modal .chat-modal-content').toggleClass('chat-input-hidden', blocked);
    if (blocked) {
        $('#chat-input').val('');
    }
}

function syncReserveButton(request) {
    const $bar = $('#chat-reserve-bar');
    if (!request) {
        $bar.addClass('d-none').empty();
        return;
    }

    const requestId = getRequestId(request);
    const parts = [];

    if (state.activeChatIsMine) {
        if (!state.activeChatGuestUserId) {
            $bar.addClass('d-none').empty();
            return;
        }

        const guestBlocked = isGuestBlockedInConversation(requestId, state.activeChatGuestUserId);
        const reservedWithGuest = request.isReserved
            && sameUserId(request.reservedByUserId, state.activeChatGuestUserId);

        if (request.isReserved && request.reservedByUsername) {
            parts.push(`<span class="reserve-status">${hub.escapeHtml(t('Request_InDealBadge'))}: ${hub.userProfileLink(request.reservedByUserId, request.reservedByUsername)}</span>`);
        }

        if (request.canUnreserve) {
            parts.push(`<button type="button" class="btn btn-outline-secondary btn-sm" id="btn-unreserve-request"><i class="bi bi-handshake"></i> ${hub.escapeHtml(t('Request_UnmarkInDeal'))}</button>`);
        } else if (!request.isReserved && ownerCanReserveForGuest(request, state.activeChatGuestUserId)) {
            parts.push(`<button type="button" class="btn btn-outline-warning btn-sm" id="btn-reserve-request"><i class="bi bi-handshake"></i> ${hub.escapeHtml(t('Request_MarkInDeal'))}</button>`);
        }

        if (guestBlocked) {
            parts.push(`<span class="reserve-status reserve-status-blocked"><i class="bi bi-slash-circle"></i> ${hub.escapeHtml(t('Request_UserBlocked'))}</span>`);
            parts.push(`<button type="button" class="btn btn-outline-success btn-sm" id="btn-unblock-guest"><i class="bi bi-person-check"></i> ${hub.escapeHtml(t('Request_UnblockUser'))}</button>`);
        } else if (isReservedForOtherParty(request)) {
            parts.push(`<span class="reserve-status reserve-status-blocked"><i class="bi bi-handshake"></i> ${hub.escapeHtml(t('Request_MessagingClosedInDeal'))}</span>`);
        } else {
            parts.push(`<button type="button" class="btn btn-outline-danger btn-sm" id="btn-block-guest"><i class="bi bi-person-x"></i> ${hub.escapeHtml(t('Request_BlockUser'))}</button>`);
        }
    } else {
        if (request.isBlockedByOwner) {
            parts.push(`<span class="reserve-status reserve-status-blocked"><i class="bi bi-slash-circle"></i> ${hub.escapeHtml(t('Request_UserBlocked'))}</span>`);
        } else if (isRequestReserved(request) && sameUserId(request.reservedByUserId, state.currentUser.userId)) {
            parts.push(`<span class="reserve-status"><i class="bi bi-handshake"></i> ${hub.escapeHtml(t('Request_InDealByYou'))}</span>`);
        } else if (isReservedForOtherParty(request)) {
            parts.push(`<span class="reserve-status reserve-status-blocked"><i class="bi bi-handshake"></i> ${hub.escapeHtml(t('Request_MessagingClosedInDeal'))}</span>`);
        }
    }

    if (parts.length === 0) {
        $bar.addClass('d-none').empty();
        return;
    }

    $bar.removeClass('d-none').html(`<div class="chat-reserve-actions">${parts.join('')}</div>`);
    syncChatInputBar(request);
}

function refreshOwnerThreadPicker(request) {
    if (!state.activeChatIsMine || !state.activeChatRequestId) return;
    const threads = state.ownerConversationCache[state.activeChatRequestId];
    if (!threads || !threads.length) return;
    renderThreadPicker(threads, state.activeChatGuestUserId, request || findRequestById(state.activeChatRequestId));
}

async function reserveRequest(requestId) {
    try {
        const body = {};
        if (state.activeChatIsMine && state.activeChatGuestUserId) {
            body.guestUserId = state.activeChatGuestUserId;
        }
        const updated = await hub.apiPost(`/api/parking/requests/${requestId}/reserve`, body);
        upsertRequest(updated);
        renderRequests();
        const request = findRequestById(requestId);
        syncReserveButton(request);
        refreshOwnerThreadPicker(request);
        hub.showToast(t('Toast_RequestMarkedInDeal'));
    } catch (err) {
        hub.showToast(hub.translateError(err.message));
    }
}

async function unreserveRequest(requestId) {
    try {
        const updated = await hub.apiPost(`/api/parking/requests/${requestId}/unreserve`, {});
        upsertRequest(updated);
        renderRequests();
        const request = findRequestById(requestId);
        syncReserveButton(request);
        refreshOwnerThreadPicker(request);
        hub.showToast(t('Toast_RequestUnmarkedInDeal'));
    } catch (err) {
        hub.showToast(hub.translateError(err.message));
    }
}

async function blockGuestOnRequest(requestId, guestUserId) {
    try {
        await hub.apiPost(`/api/parking/requests/${requestId}/block`, { guestUserId });
        await refreshOwnerConversationCache();
        const threads = state.ownerConversationCache[requestId] || [];
        const thread = threads.find(c => sameUserId(c.guestUserId || c.GuestUserId, guestUserId));
        if (thread) {
            thread.isBlocked = true;
            thread.IsBlocked = true;
        }
        await loadRequests();
        const request = findRequestById(requestId);
        syncReserveButton(request);
        syncChatInputBar(request);
        renderThreadPicker(threads, guestUserId, request);
        await loadChatMessages();
        hub.showToast(t('Toast_UserBlocked'));
    } catch (err) {
        hub.showToast(hub.translateError(err.message));
    }
}

async function unblockGuestOnRequest(requestId, guestUserId) {
    try {
        await hub.apiPost(`/api/parking/requests/${requestId}/unblock`, { guestUserId });
        await refreshOwnerConversationCache();
        const threads = state.ownerConversationCache[requestId] || [];
        const thread = threads.find(c => sameUserId(c.guestUserId || c.GuestUserId, guestUserId));
        if (thread) {
            thread.isBlocked = false;
            thread.IsBlocked = false;
        }
        await loadRequests();
        const request = findRequestById(requestId);
        syncReserveButton(request);
        syncChatInputBar(request);
        refreshOwnerThreadPicker(request);
        await loadChatMessages();
        hub.showToast(t('Toast_UserUnblocked'));
    } catch (err) {
        hub.showToast(hub.translateError(err.message));
    }
}

function setSearchModalMode(mode) {
    const isEdit = mode === 'edit';
    $('#search-modal-title').text(t(isEdit ? 'Modal_Search_EditTitle' : 'Modal_Search_Title'));
    $('#search-modal-desc').text(t(isEdit ? 'Modal_Search_EditDesc' : 'Modal_Search_Desc'));
    $('#search-modal-submit-label').text(t(isEdit ? 'Modal_Search_Save' : 'Modal_Search_Publish'));
    $('#search-modal-submit-icon').attr('class', isEdit ? 'bi bi-check-lg' : 'bi bi-broadcast');
}

function populateSearchFormFromRequest(r) {
    $('#search-address').val(r.address);
    state.geocodedLocation = { lat: r.latitude, lng: r.longitude, address: r.address };
    $('#search-radius').val(String(r.radiusMeters));
    $('#search-reward').val(r.rewardAmount ? Number(r.rewardAmount).toFixed(2) : '');
    $('#geocode-result').removeClass('d-none');
    $('#geocode-address-text').text(r.address);

    $('.payment-method-cb').prop('checked', false);
    $('#payment-other-text').val('');
    $('#payment-other-wrap').addClass('d-none');

    (r.paymentMethods || []).forEach(code => {
        if (code.toLowerCase().startsWith('other:')) {
            $('.payment-method-cb[value="other"]').prop('checked', true);
            $('#payment-other-text').val(code.substring(6));
            $('#payment-other-wrap').removeClass('d-none');
        } else {
            $(`.payment-method-cb[value="${code}"]`).prop('checked', true);
        }
    });

    hub.applyGeocodedLocation(r.latitude, r.longitude, r.address);
}

async function openEditRequest(requestId) {
    const r = findRequestById(requestId);
    if (!r || !isMyRequest(r)) return;

    hub.resetSearchForm();
    state.editingRequestId = requestId;
    await loadPaymentMethodOptions();
    populateSearchFormFromRequest(r);
    setSearchModalMode('edit');
    modals.searchModal.show();
}

function deleteListing(type, id) {
    state.pendingDeleteListing = { type, id };
    $('#confirm-delete-message').text(
        type === 'report' ? t('Confirm_DeleteReport') : t('Confirm_DeleteAd')
    );
    $('#confirm-delete-modal .confirm-delete-title').text(t('Modal_ConfirmDelete_Title'));
    $('#btn-confirm-delete span').text(t('Modal_ConfirmDelete_Btn'));
    modals.confirmDeleteModal.show();
}

async function performDeleteListing(type, id) {
    try {
        const path = type === 'report'
            ? `/api/parking/report/${id}`
            : `/api/parking/requests/${id}`;
        await hub.apiDelete(path);
        if (type === 'request' && state.activeChatRequestId === id) {
            state.activeChatRequestId = null;
            modals.chatModal.hide();
        }
        hub.showToast(type === 'report' ? t('Toast_ReportDeleted') : t('Toast_AdDeleted'));
        if (type === 'report') {
            await hub.loadParkings();
        } else {
            await loadRequests();
        }
    } catch (err) {
        hub.showToast(err.message);
    }
}

$('#btn-confirm-delete').on('click', async function () {
    if (!state.pendingDeleteListing) return;
    const { type, id } = state.pendingDeleteListing;
    state.pendingDeleteListing = null;
    modals.confirmDeleteModal.hide();
    await performDeleteListing(type, id);
});

$('#confirm-delete-modal').on('hidden.bs.modal', function () {
    state.pendingDeleteListing = null;
});

function paymentMethodLabelKey(code) {
    const pascal = code.split('_').map(part => part.charAt(0).toUpperCase() + part.slice(1)).join('');
    return 'Payment_' + pascal;
}

function paymentMethodDisplayLabel(code) {
    if (code.toLowerCase().startsWith('other:')) {
        return code.substring(6);
    }
    return t(paymentMethodLabelKey(code));
}

function syncPaymentOtherField() {
    const checked = $('.payment-method-cb[value="other"]').is(':checked');
    $('#payment-other-wrap').toggleClass('d-none', !checked);
    if (!checked) {
        $('#payment-other-text').val('');
    }
}

function renderPaymentMethodOptions() {
    const $list = $('#payment-methods-list').empty();
    state.paymentMethodOptions.forEach(pm => {
        $list.append(`
            <label class="payment-method-option">
                <input type="checkbox" class="payment-method-cb" value="${hub.escapeHtml(pm.code)}">
                ${hub.escapeHtml(t(paymentMethodLabelKey(pm.code)))}
            </label>
        `);
    });
}

async function loadPaymentMethodOptions() {
    try {
        if (state.paymentMethodOptions.length === 0) {
            state.paymentMethodOptions = await hub.apiGet('/api/parking/payment-methods');
        }
        renderPaymentMethodOptions();
    } catch {
        hub.showToast(t('Toast_LoadPaymentsError'));
    }
}

$(document).on('change', '.payment-method-cb[value="other"]', syncPaymentOtherField);

function getSelectedPaymentMethods() {
    const methods = $('.payment-method-cb:checked').map(function () {
        return $(this).val();
    }).get();

    const otherIndex = methods.indexOf('other');
    if (otherIndex >= 0) {
        const text = $('#payment-other-text').val().trim();
        if (text) {
            methods[otherIndex] = 'other:' + text;
        }
    }

    return methods;
}

async function openChat(requestId, selectGuestUserId = null) {
    state.activeChatRequestId = requestId;
    state.activeChatGuestUserId = null;
    await hub.joinRequestChatSignalR(requestId);
    const r = findRequestById(requestId);
    state.activeChatIsMine = !!(r && isMyRequest(r));

    if (!state.activeChatIsMine && r?.isReserved
        && !sameUserId(r.reservedByUserId, state.currentUser.userId)
        && !r.viewerHasThread) {
        hub.showToast(t('Toast_RequestInDeal'));
        return;
    }

    if (!state.activeChatIsMine && r?.isBlockedByOwner && !r.viewerHasThread) {
        hub.showToast(hub.translateError(t('Error_UserBlockedOnRequest')));
        return;
    }

    let info = r ? `${hub.escapeHtml(r.address)} · ${r.radiusMeters}m` : '';
    if (r && r.rewardAmount) {
        info += ` · Offerta €${Number(r.rewardAmount).toFixed(2)}`;
    }
    if (r && !state.activeChatIsMine && r.createdByUserId && r.createdByUsername) {
        info = `${hub.userProfileLink(r.createdByUserId, r.createdByUsername)} · ${info}`;
    }
    $('#chat-request-info').html(info);
    $('#chat-modal .modal-title').text(
        state.activeChatIsMine ? t('Modal_Chat_TitleMine') : t('Modal_Chat_TitleGuest')
    );
    $('#chat-input').val('');
    resetChatThreadPanel();

    if (state.activeChatIsMine) {
        try {
            const conversations = await hub.apiGet(`/api/parking/requests/${requestId}/conversations`);
            state.ownerConversationCache[requestId] = conversations;
            if (conversations.length === 0) {
                $('#chat-input-bar').addClass('d-none');
                modals.chatModal.show();
                renderChatMessages([], true);
                syncReserveButton(r);
                return;
            }

            const defaultGuestId = selectGuestUserId || pickDefaultGuestConversation(conversations);
            state.activeChatGuestUserId = defaultGuestId;

            if (conversations.length > 1) {
                renderThreadPicker(conversations, defaultGuestId, r);
            } else {
                resetChatThreadPanel();
            }
        } catch (err) {
            hub.showToast(err.message);
            return;
        }
    } else {
        state.activeChatGuestUserId = state.currentUser.userId;
    }

    syncChatInputBar(r);
    modals.chatModal.show();
    await loadChatMessages();
    syncReserveButton(r);
    if (r && hub.isWithinViewRange(r.latitude, r.longitude)) {
        mapEngine.setView(r.latitude, r.longitude, Math.max(mapEngine.getZoom(), 15));
    }
}

function renderThreadPicker(conversations, selectedGuestUserId, request) {
    const sorted = sortOwnerConversations(conversations);
    const $panel = $('#chat-thread-list').removeClass('d-none');
    const $list = $('#chat-thread-panel-list').empty();
    $('#chat-layout').addClass('has-threads');
    $('#chat-modal .chat-modal-content').addClass('chat-has-threads');

    sorted.forEach(c => {
        const guestId = c.guestUserId || c.GuestUserId;
        const guestName = c.guestUsername || c.GuestUsername || '';
        const isActive = selectedGuestUserId && sameUserId(guestId, selectedGuestUserId);
        const unread = getThreadUnreadCount(state.activeChatRequestId, c);
        const isBlocked = c.isBlocked || c.IsBlocked;
        const inDeal = request && isRequestReserved(request) && sameUserId(request.reservedByUserId, guestId);
        const dealClosed = request && isRequestReserved(request) && !sameUserId(request.reservedByUserId, guestId);
        const unreadBadge = unread > 0
            ? `<span class="chat-thread-unread">${unread}</span>`
            : '';
        const statusBits = [];
        if (inDeal) {
            statusBits.push(`<span class="chat-thread-tag chat-thread-tag-deal">${hub.escapeHtml(t('Request_InDealBadge'))}</span>`);
        } else if (dealClosed) {
            const full = t('Request_MessagingClosedInDeal');
            statusBits.push(
                `<span class="chat-thread-tag chat-thread-tag-blocked" title="${hub.escapeAttr(full)}">` +
                `<i class="bi bi-lock-fill"></i> ${hub.escapeHtml(t('Request_ThreadDealBlocked'))}</span>`
            );
        }
        if (isBlocked) {
            statusBits.push(`<span class="chat-thread-tag chat-thread-tag-blocked">${hub.escapeHtml(t('Request_UserBlocked'))}</span>`);
        }

        $list.append(`
            <button type="button" class="chat-thread-item${isActive ? ' active' : ''}${unread > 0 ? ' has-unread' : ''}" data-guest-id="${guestId}">
                <div class="chat-thread-item-top">
                    <strong>${hub.userProfileLink(guestId, guestName)}</strong>
                    ${unreadBadge}
                </div>
                <div class="chat-thread-item-meta">
                    <small class="chat-thread-preview">${hub.escapeHtml(formatChatMessagePreview(c.lastMessagePreview) || t('Modal_Chat_NoMessagesYet'))}</small>
                    <small class="chat-thread-time">${formatChatTime(c.lastMessageAt)}</small>
                </div>
                ${statusBits.length ? `<div class="chat-thread-tags">${statusBits.join('')}</div>` : ''}
            </button>
        `);
    });

    $list.find('.chat-thread-item').on('click', async function () {
        $list.find('.chat-thread-item').removeClass('active');
        $(this).addClass('active');
        state.activeChatGuestUserId = $(this).attr('data-guest-id');
        const request = findRequestById(state.activeChatRequestId);
        syncChatInputBar(request);
        syncReserveButton(request);
        await loadChatMessages();
    });
}

async function loadChatMessages() {
    if (!state.activeChatRequestId) return;
    if (state.activeChatIsMine && !state.activeChatGuestUserId) return;

    try {
        const withParam = state.activeChatIsMine && state.activeChatGuestUserId
            ? `?with=${state.activeChatGuestUserId}`
            : '';
        const messages = await hub.apiGet(`/api/parking/requests/${state.activeChatRequestId}/messages${withParam}`);
        renderChatMessages(messages, state.activeChatIsMine);
        markThreadRead(messages);
        const request = findRequestById(state.activeChatRequestId);
        syncChatInputBar(request);
        syncReserveButton(request);
    } catch (err) {
        hub.showToast(err.message);
    }
}

function renderChatMessages(messages, isOwnerView) {
    const $container = $('#chat-messages').empty();
    if (!messages || messages.length === 0) {
        const emptyText = isOwnerView
            ? t('Modal_Chat_EmptyOwner')
            : t('Modal_Chat_EmptyGuest');
        $container.html(`<div class="chat-empty">${emptyText}</div>`);
        return;
    }

    messages.forEach(m => {
        const time = hub.parseUtcDate(m.createdAt).toLocaleTimeString('it-IT', {
            hour: '2-digit',
            minute: '2-digit'
        });
        $container.append(`
            <div class="chat-bubble ${m.isMine ? 'mine' : 'theirs'}${getChatBubbleExtraClass(m.content)}">
                ${renderChatMessageBody(m.content)}
                <div class="meta">${hub.userProfileLink(m.senderUserId, m.senderUsername)} · ${time}</div>
            </div>
        `);
    });
    $container.scrollTop($container[0].scrollHeight);
    syncActiveChatLocateButtons();
    updateChatInputState(messages, isOwnerView);
}

function updateChatInputState(messages, isOwnerView) {
    const request = findRequestById(state.activeChatRequestId);
    const blocked = isChatMessagingBlocked(request);

    $('#chat-modal .chat-modal-content').toggleClass('chat-input-hidden', blocked);

    if (blocked) {
        $('#chat-input-bar').addClass('d-none');
        return;
    }

    if (!isOwnerView) {
        $('#chat-input-bar').removeClass('d-none');
        return;
    }

    const hasFromOthers = messages && messages.some(m => !m.isMine);
    $('#chat-input-bar').toggleClass('d-none', !hasFromOthers);
    $('#chat-modal .chat-modal-content').toggleClass('chat-input-hidden', !hasFromOthers);
}

async function sendChatMessage() {
    const text = $('#chat-input').val().trim();
    if (!text) return;
    if (!canSendChatMessage()) return;

    try {
        await postChatMessage(text);
        $('#chat-input').val('');
    } catch (err) {
        hub.showToast(err.message);
    }
}

async function sendChatLocation() {
    if (!state.userLocation) {
        hub.showToast(t('Toast_ChatLocationUnavailable'));
        return;
    }
    if (!canSendChatMessage()) return;

    try {
        await postChatMessage(formatChatLocationMessage(state.userLocation.lat, state.userLocation.lng));
    } catch (err) {
        hub.showToast(err.message);
    }
}

async function sendChatPhoto(file) {
    if (!file || !canSendChatMessage()) return;

    const formData = new FormData();
    formData.append('photo', file);
    if (state.activeChatIsMine && state.activeChatGuestUserId) {
        formData.append('replyToUserId', state.activeChatGuestUserId);
    }

    try {
        await hub.apiPostForm(`/api/parking/requests/${state.activeChatRequestId}/messages/photo`, formData);
        await loadChatMessages();
        await loadRequests();
        syncReserveButton(findRequestById(state.activeChatRequestId));
    } catch (err) {
        hub.showToast(hub.translateError(err.message));
    }
}

$('#btn-send-message').on('click', sendChatMessage);
$('#btn-send-location').on('click', sendChatLocation);
$('#btn-send-photo').on('click', function () {
    $('#chat-photo-input').trigger('click');
});
$('#chat-photo-input').on('change', async function () {
    const file = this.files && this.files[0];
    this.value = '';
    if (!file) return;
    await sendChatPhoto(file);
});
$(document).on('click', '.chat-locate-btn', function (e) {
    e.preventDefault();
    e.stopPropagation();
    const lat = parseFloat($(this).attr('data-lat'));
    const lng = parseFloat($(this).attr('data-lng'));
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) return;
    focusMapOnCoordinates(lat, lng, $(this));
});
$(document).on('click', '.chat-location-flag-close', function (e) {
    e.preventDefault();
    e.stopPropagation();
    clearChatLocationMarker();
});
$('#chat-reserve-bar').on('click', '#btn-reserve-request', function () {
    if (state.activeChatRequestId) {
        reserveRequest(state.activeChatRequestId);
    }
});
$('#chat-reserve-bar').on('click', '#btn-unreserve-request', function () {
    if (state.activeChatRequestId) {
        unreserveRequest(state.activeChatRequestId);
    }
});
$('#chat-reserve-bar').on('click', '#btn-block-guest', function () {
    if (state.activeChatRequestId && state.activeChatGuestUserId) {
        blockGuestOnRequest(state.activeChatRequestId, state.activeChatGuestUserId);
    }
});

$('#chat-reserve-bar').on('click', '#btn-unblock-guest', function () {
    if (state.activeChatRequestId && state.activeChatGuestUserId) {
        unblockGuestOnRequest(state.activeChatRequestId, state.activeChatGuestUserId);
    }
});
$('#chat-input').on('keypress', function (e) {
    if (e.which === 13) {
        e.preventDefault();
        sendChatMessage();
    }
});

$('#chat-modal').on('hidden.bs.modal', function () {
    state.activeChatRequestId = null;
    state.activeChatGuestUserId = null;
    resetChatThreadPanel();
    $('#chat-modal .chat-modal-content').removeClass('chat-input-hidden chat-has-threads');
    renderRequests();
    updateRequestsTabUnreadIndicator();
});


  hub.getMsgReadMap = getMsgReadMap;
  hub.getThreadReadKey = getThreadReadKey;
  hub.markThreadRead = markThreadRead;
  hub.updateRequestUnreadInCache = updateRequestUnreadInCache;
  hub.getThreadUnreadCount = getThreadUnreadCount;
  hub.sortOwnerConversations = sortOwnerConversations;
  hub.pickDefaultGuestConversation = pickDefaultGuestConversation;
  hub.formatChatTime = formatChatTime;
  hub.resetChatThreadPanel = resetChatThreadPanel;
  hub.sameUserId = sameUserId;
  hub.getSignalRValue = getSignalRValue;
  hub.isMyRequest = isMyRequest;
  hub.getParkingMarkerColor = getParkingMarkerColor;
  hub.resolveParkingMapCoords = resolveParkingMapCoords;
  hub.ensureParkingMapCoords = ensureParkingMapCoords;
  hub.displayCoordsForParkingMarker = displayCoordsForParkingMarker;
  hub.buildParkingMapMarkerHtml = buildParkingMapMarkerHtml;
  hub.isMyReport = isMyReport;
  hub.getRequestId = getRequestId;
  hub.findRequestById = findRequestById;
  hub.getRequestsForList = getRequestsForList;
  hub.isRequestReserved = isRequestReserved;
  hub.passesRequestsDealFilter = passesRequestsDealFilter;
  hub.getFilteredRequestsForList = getFilteredRequestsForList;
  hub.getRequestsForMap = getRequestsForMap;
  hub.getUnreadIncoming = getUnreadIncoming;
  hub.hasUnreadMessages = hasUnreadMessages;
  hub.updateRequestsTabUnreadIndicator = updateRequestsTabUnreadIndicator;
  hub.switchListingsSheet = switchListingsSheet;
  hub.refreshOwnerConversationCache = refreshOwnerConversationCache;
  hub.loadRequests = loadRequests;
  hub.isRequestStatusActive = isRequestStatusActive;
  hub.isRequestActive = isRequestActive;
  hub.upsertRequest = upsertRequest;
  hub.focusMapOnRequest = focusMapOnRequest;
  hub.parseChatLocationMessage = parseChatLocationMessage;
  hub.formatChatLocationMessage = formatChatLocationMessage;
  hub.parseChatPhotoMessage = parseChatPhotoMessage;
  hub.formatChatMessagePreview = formatChatMessagePreview;
  hub.getChatBubbleExtraClass = getChatBubbleExtraClass;
  hub.getChatLocateKey = getChatLocateKey;
  hub.clearChatLocationMarker = clearChatLocationMarker;
  hub.syncActiveChatLocateButtons = syncActiveChatLocateButtons;
  hub.focusMapOnCoordinates = focusMapOnCoordinates;
  hub.renderChatMessageBody = renderChatMessageBody;
  hub.isReservedForOtherParty = isReservedForOtherParty;
  hub.isChatMessagingBlocked = isChatMessagingBlocked;
  hub.canSendChatMessage = canSendChatMessage;
  hub.postChatMessage = postChatMessage;
  hub.buildLocateButtonHtml = buildLocateButtonHtml;
  hub.locateListingOnMap = locateListingOnMap;
  hub.buildRequestActionButtonsHtml = buildRequestActionButtonsHtml;
  hub.buildRequestPopupContent = buildRequestPopupContent;
  hub.renderRequests = renderRequests;
  hub.renewRequest = renewRequest;
  hub.isGuestBlockedInConversation = isGuestBlockedInConversation;
  hub.ownerCanReserveForGuest = ownerCanReserveForGuest;
  hub.syncChatInputBar = syncChatInputBar;
  hub.syncReserveButton = syncReserveButton;
  hub.refreshOwnerThreadPicker = refreshOwnerThreadPicker;
  hub.reserveRequest = reserveRequest;
  hub.unreserveRequest = unreserveRequest;
  hub.blockGuestOnRequest = blockGuestOnRequest;
  hub.unblockGuestOnRequest = unblockGuestOnRequest;
  hub.setSearchModalMode = setSearchModalMode;
  hub.populateSearchFormFromRequest = populateSearchFormFromRequest;
  hub.openEditRequest = openEditRequest;
  hub.deleteListing = deleteListing;
  hub.performDeleteListing = performDeleteListing;
  hub.paymentMethodLabelKey = paymentMethodLabelKey;
  hub.paymentMethodDisplayLabel = paymentMethodDisplayLabel;
  hub.syncPaymentOtherField = syncPaymentOtherField;
  hub.renderPaymentMethodOptions = renderPaymentMethodOptions;
  hub.loadPaymentMethodOptions = loadPaymentMethodOptions;
  hub.getSelectedPaymentMethods = getSelectedPaymentMethods;
  hub.openChat = openChat;
  hub.renderThreadPicker = renderThreadPicker;
  hub.loadChatMessages = loadChatMessages;
  hub.renderChatMessages = renderChatMessages;
  hub.updateChatInputState = updateChatInputState;
  hub.sendChatMessage = sendChatMessage;
  hub.sendChatLocation = sendChatLocation;
  hub.sendChatPhoto = sendChatPhoto;
}
