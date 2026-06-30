'use strict';

export const modals = {
    parkingDetailModal: null,
    reportModal: null,
    searchModal: null,
    chatModal: null,
    suspendedModal: null,
    profileModal: null,
    confirmDeleteModal: null
};

export function initModals() {
    modals.parkingDetailModal = new bootstrap.Modal('#parking-detail-modal');
    modals.reportModal = new bootstrap.Modal('#report-modal');
    modals.searchModal = new bootstrap.Modal('#search-modal');
    modals.chatModal = new bootstrap.Modal('#chat-modal');
    modals.suspendedModal = new bootstrap.Modal('#suspended-modal');
    modals.profileModal = new bootstrap.Modal('#profile-modal');
    modals.confirmDeleteModal = new bootstrap.Modal('#confirm-delete-modal');
}
