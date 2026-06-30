import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';

export function registerFab(hub, t) {
// --- FAB & actions ---
function setFabOpen(isOpen) {
    $('#btn-action').toggleClass('open', isOpen);
}

$('#btn-action').on('click', function () {
    if (hub.blockIfSuspended()) return;
    const willOpen = $('#fab-menu').hasClass('d-none');
    $('#fab-menu').toggleClass('d-none');
    setFabOpen(willOpen);
});

$(document).on('click', '#btn-enable-geo', function (e) {
    e.preventDefault();
    e.stopPropagation();
    hub.requestUserLocation();
});

$(document).on('click', '#btn-pick-geo-map', function (e) {
    e.preventDefault();
    e.stopPropagation();
    hub.startMapPickMode();
});

$('#btn-locate-me').on('click', function () {
    if (hub.blockIfSuspended()) return;
    hub.centerMapOnUser();
});

$(document).on('click', function (e) {
    if (!$(e.target).closest('#map-fab-stack').length) {
        $('#fab-menu').addClass('d-none');
        setFabOpen(false);
    }
});

$('#btn-report').on('click', function () {
    $('#fab-menu').addClass('d-none');
    setFabOpen(false);
    if (hub.blockIfSuspended()) return;
    if (!state.userLocation) {
        hub.showToast(t('Toast_WaitGps'));
        hub.locateUser();
        return;
    }
    state.reportCaptureLocation = { lat: state.userLocation.lat, lng: state.userLocation.lng };
    hub.resetPhotoSelection();
    $('#report-location-text').text(
        `Lat ${state.reportCaptureLocation.lat.toFixed(5)}, Lng ${state.reportCaptureLocation.lng.toFixed(5)}`
    );
    modals.reportModal.show();
});

$('#btn-search-parking').on('click', async function () {
    $('#fab-menu').addClass('d-none');
    setFabOpen(false);
    if (hub.blockIfSuspended()) return;
    state.editingRequestId = null;
    hub.resetSearchForm();
    hub.setSearchModalMode('create');
    await hub.loadPaymentMethodOptions();
    modals.searchModal.show();
});

$('#listings-tabs .nav-link').on('click', function () {
    hub.switchListingsSheet($(this).data('sheet'));
});

$('#requests-deal-filter .listings-filter-chip').on('click', function () {
    state.requestsDealFilter = $(this).data('filter');
    $('#requests-deal-filter .listings-filter-chip').removeClass('active');
    $(this).addClass('active');
    hub.renderRequests();
});


  hub.setFabOpen = setFabOpen;
}
