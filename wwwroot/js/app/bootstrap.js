import { state } from '../core/state.js';
import { modals } from '../core/modals.js';
import { C } from '../core/constants.js';

export function wireEvents(hub, t) {
$('#report-modal').on('hidden.bs.modal', function () {
    state.reportCaptureLocation = null;
});

$('#parking-detail-modal, #report-modal, #search-modal, #chat-modal, #profile-modal').on('shown.bs.modal hidden.bs.modal', function () {
    window.setTimeout(hub.refreshMapView, 50);
});

$(window).on('resize', function () {
    hub.syncDashboardChromeTop();
    hub.refreshMapView();
});

// Init
window.onLanguageChanged = function () {
    hub.renderParkings();
    hub.renderRequests();
    hub.updateAccountStatusUI();
    if (state.paymentMethodOptions.length > 0) hub.renderPaymentMethodOptions();
    $('#payment-other-text').attr('placeholder', t('Modal_Search_PaymentOtherPlaceholder'));
    hub.setSearchModalMode(state.editingRequestId ? 'edit' : 'create');
    $('#map-radius-control').attr('title', t('Map_ViewRadiusHint'));
    $('#view-radius option').each(function () {
        const key = $(this).attr('data-i18n');
        if (key) $(this).text(t(key));
    });
    if (state.userPositionMarker) {
        hub.mapEngine.bindPopup(state.userPositionMarker, t('List_YourPosition'));
    }
    hub.updateTopBarAvatar();
    $('#photo-preview').attr('alt', t('List_PhotoAlt'));
    $('#parking-detail-photo').attr('alt', t('List_VotePhotoAlt'));
    $('#parking-detail-no-photo').text(t('Modal_ParkingDetail_NoPhoto'));
    $('#parking-detail-modal .modal-title').text(t('Modal_ParkingDetail_Title'));
    $('#parking-detail-voted-text').text(t('Modal_Vote_AlreadyDone'));
    const $ownInfo = $('#parking-detail-info-section p');
    if ($ownInfo.length) {
        $ownInfo.text(t('Modal_ParkingDetail_OwnReport'));
    }
    $('#profile-review-comment').attr('placeholder', t('Profile_ReviewCommentPh'));
    $('#top-user-search').attr('placeholder', t('Profile_SearchPlaceholder'));
    $('#profile-tabs .nav-link').each(function () {
        const key = $(this).attr('data-i18n');
        if (key) $(this).text(t(key));
    });
    $('#requests-deal-filter .listings-filter-chip span[data-i18n]').each(function () {
        const key = $(this).attr('data-i18n');
        if (key) $(this).text(t(key));
    });
    $('.chat-thread-panel-header[data-i18n]').each(function () {
        const key = $(this).attr('data-i18n');
        if (key) $(this).text(t(key));
    });
};

$('#view-radius').on('change', function () {
    hub.applyViewRadius(parseInt($(this).val(), 10));
});

$(document).on('click', '.lang-switch .lang-btn', async function () {
    await SpotsterI18n.setLanguage($(this).data('lang'));
});

hub.setRegisterBirthdateLimits();
hub.handleEmailConfirmationRedirect();
hub.loadUser();

}
