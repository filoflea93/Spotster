import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';

export function registerSignalr(hub, t) {
// --- SignalR ---
function refreshActiveChatAfterRequestChange() {
    hub.loadRequests().then(() => {
        if (!state.activeChatRequestId) return;
        const request = hub.findRequestById(state.activeChatRequestId);
        hub.syncChatInputBar(request);
        hub.syncReserveButton(request);
        hub.refreshOwnerThreadPicker(request);
    });
}

async function syncSignalRViewport() {
    if (!state.connection || state.connection.state !== signalR.HubConnectionState.Connected) return;
    if (!state.userLocation) return;
    try {
        await state.connection.invoke('SetMapViewport', state.userLocation.lat, state.userLocation.lng, state.viewRadiusMeters);
    } catch (_) { /* ignore */ }
}

async function joinRequestChatSignalR(requestId) {
    if (!state.connection || state.connection.state !== signalR.HubConnectionState.Connected) return;
    if (!requestId) return;
    try {
        await state.connection.invoke('JoinRequestChat', requestId);
    } catch (_) { /* ignore */ }
}

function connectSignalR() {
    if (state.connection) state.connection.stop();
    state.connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/parking', {
            accessTokenFactory: () => hub.getAccessToken() || ''
        })
        .withAutomaticReconnect()
        .build();

    state.connection.on('ParkingCreated', () => {
        if (Date.now() < state.skipParkingReloadUntil) return;
        hub.loadParkings();
        hub.showToast(t('Toast_NewReport'));
    });

    state.connection.on('ParkingUpdated', () => {
        if (Date.now() < state.skipParkingReloadUntil) return;
        hub.loadParkings();
    });

    state.connection.on('AccountRatingUpdated', () => {
        hub.refreshReviewStarsDisplay();
    });

    state.connection.on('ParkingExpired', p => {
        const idx = state.parkings.findIndex(x => x.id === p.id);
        if (idx >= 0) state.parkings.splice(idx, 1);
        hub.renderParkings();
    });

    state.connection.on('ParkingRequestCreated', () => hub.loadRequests());
    state.connection.on('ParkingRequestRenewed', () => hub.loadRequests());
    state.connection.on('ParkingRequestUpdated', () => hub.loadRequests());
    ['ParkingRequestReserved', 'ParkingRequestUnreserved',
        'ParkingRequestGuestBlocked', 'ParkingRequestGuestUnblocked']
        .forEach(evt => state.connection.on(evt, refreshActiveChatAfterRequestChange));
    state.connection.on('ParkingRequestExpired', () => hub.loadRequests());
    state.connection.on('ParkingSpottedNearRequest', data => {
        hub.loadRequests();
        const ownerUserId = hub.getSignalRValue(data, 'ownerUserId', 'OwnerUserId');
        if (state.currentUser && hub.sameUserId(ownerUserId, state.currentUser.userId)) {
            const address = hub.getSignalRValue(data, 'requestAddress', 'RequestAddress') || '';
            hub.showToast(t('Toast_ParkingNearRequest', address));
        }
    });

    state.connection.on('RequestMessageReceived', data => {
        if (!state.currentUser) {
            hub.loadRequests();
            return;
        }

        const message = hub.getSignalRValue(data, 'message', 'Message');
        if (!message) {
            hub.loadRequests();
            return;
        }

        const requestId = hub.getSignalRValue(data, 'requestId', 'RequestId');
        const ownerUserId = hub.getSignalRValue(data, 'ownerUserId', 'OwnerUserId');
        const threadGuestUserId = hub.getSignalRValue(data, 'threadGuestUserId', 'ThreadGuestUserId');
        const recipientUserId = hub.getSignalRValue(data, 'recipientUserId', 'RecipientUserId');
        const senderUserId = hub.getSignalRValue(message, 'senderUserId', 'SenderUserId');
        const senderUsername = hub.getSignalRValue(message, 'senderUsername', 'SenderUsername');
        const content = hub.getSignalRValue(message, 'content', 'Content') || '';

        const isOwner = hub.sameUserId(ownerUserId, state.currentUser.userId);
        const isGuestInThread = hub.sameUserId(threadGuestUserId, state.currentUser.userId);
        const isRecipient = hub.sameUserId(recipientUserId, state.currentUser.userId);
        const fromOther = !hub.sameUserId(senderUserId, state.currentUser.userId);

        if (fromOther && isRecipient) {
            const preview = content.length > 60 ? content.slice(0, 60) + '…' : content;
            hub.showToast(
                `${hub.userProfileLink(senderUserId, senderUsername || t('Modal_Chat_Title'))}:<br>${hub.escapeHtml(preview)}`,
                5000,
                {
                    html: true,
                    onClick: () => hub.openChat(
                        requestId,
                        isOwner ? threadGuestUserId : null
                    )
                }
            );
            hub.updateRequestsTabUnreadIndicator();
        }

        if (fromOther && isGuestInThread) {
            const req = state.parkingRequests.find(x => hub.sameUserId(x.id, requestId));
            if (req) {
                req.incomingMessageCount = (req.incomingMessageCount || 0) + 1;
                hub.renderRequests();
                hub.updateRequestsTabUnreadIndicator();
            }
        } else if (fromOther && isOwner) {
            hub.refreshOwnerConversationCache().then(() => {
                hub.renderRequests();
                hub.updateRequestsTabUnreadIndicator();
            });
        }

        if (hub.sameUserId(state.activeChatRequestId, requestId) &&
            hub.sameUserId(state.activeChatGuestUserId, threadGuestUserId)) {
            hub.loadChatMessages();
        } else if (!fromOther || isRecipient || isOwner || isGuestInThread) {
            hub.loadRequests();
        }
    });

    state.connection.on('UserSuspiciousActivity', data => {
        if (state.currentUser && data.userId === state.currentUser.userId) {
            hub.applyUserProfile({
                status: data.status || state.currentUser.status,
                suspendedUntil: data.suspendedUntil ?? state.currentUser.suspendedUntil,
                suspiciousScore: data.suspiciousScore ?? state.currentUser.suspiciousScore
            });

            if (data.status === 'Suspended') {
                hub.showToast(t('Account_Suspended_Toast'), 6000);
                hub.showSuspendedNotice(true);
            } else {
                hub.showToast(t('Toast_SuspiciousActivity', data.message), 4500);
            }
        }
    });

    state.connection.onreconnected(() => {
        syncSignalRViewport();
        if (state.activeChatRequestId) {
            joinRequestChatSignalR(state.activeChatRequestId);
        }
    });

    state.connection.start()
        .then(() => syncSignalRViewport())
        .catch(() => hub.showToast(t('Toast_RealtimeError')));
}


  hub.refreshActiveChatAfterRequestChange = refreshActiveChatAfterRequestChange;
  hub.syncSignalRViewport = syncSignalRViewport;
  hub.joinRequestChatSignalR = joinRequestChatSignalR;
  hub.connectSignalR = connectSignalR;
}
