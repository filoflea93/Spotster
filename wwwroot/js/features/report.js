import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';

export function registerReport(hub, t) {
// --- Report (photo) ---
$('#photo-placeholder, #photo-preview').on('click', function () {
    $('#report-photo').trigger('click');
});

$('#report-photo').on('change', function () {
    const file = this.files[0];
    if (!file) return;

    if (file.size > 5 * 1024 * 1024) {
        hub.showToast(t('Toast_PhotoTooLarge'));
        this.value = '';
        return;
    }

    state.selectedPhotoFile = file;
    const reader = new FileReader();
    reader.onload = function (e) {
        $('#photo-preview').attr('src', e.target.result).removeClass('d-none');
        $('#photo-placeholder').addClass('d-none');
        $('#btn-remove-photo').removeClass('d-none');
    };
    reader.readAsDataURL(file);
});

$('#btn-remove-photo').on('click', function (e) {
    e.stopPropagation();
    resetPhotoSelection();
});

function resetPhotoSelection() {
    state.selectedPhotoFile = null;
    $('#report-photo').val('');
    $('#photo-preview').addClass('d-none').attr('src', '');
    $('#photo-placeholder').removeClass('d-none');
    $('#btn-remove-photo').addClass('d-none');
}

function resetReportForm() {
    resetPhotoSelection();
    state.reportCaptureLocation = null;
}

$('#btn-confirm-report').on('click', async function () {
    const loc = state.reportCaptureLocation || state.userLocation;
    if (!loc) {
        hub.showToast(t('Toast_LocationUnavailable'));
        return;
    }

    const $btn = $(this).prop('disabled', true);
    try {
        await hub.syncLocationToServer(loc.lat, loc.lng);

        const formData = new FormData();
        formData.append('latitude', loc.lat.toFixed(6));
        formData.append('longitude', loc.lng.toFixed(6));
        if (state.selectedPhotoFile) {
            formData.append('photo', state.selectedPhotoFile);
        }

        const sentCoords = { lat: loc.lat, lng: loc.lng };
        const p = await hub.apiPostForm('/api/parking/report', formData);
        hub.ensureParkingMapCoords(p, sentCoords);
        state.skipParkingReloadUntil = Date.now() + 30000;
        state.loadParkingsSeq++;
        hub.upsertParking(p, sentCoords);
        if (state.activeSheet !== 'state.parkings') {
            hub.switchListingsSheet('state.parkings');
        }
        state.reportCaptureLocation = null;
        modals.reportModal.hide();
        hub.showToast(t('Toast_ReportSent'));
        window.setTimeout(() => {
            hub.centerMapOnCoords(sentCoords, p);
        }, 350);
    } catch (err) {
        hub.showToast(err.message);
    } finally {
        $btn.prop('disabled', false);
    }
});

function buildPopupContent(p) {
    const photoUrl = hub.prop(p, 'photoUrl', 'PhotoUrl');
    const photoHtml = photoUrl
        ? `<img src="${hub.escapeHtml(photoUrl)}" class="popup-photo" alt="${t('List_VotePhotoAlt')}">`
        : '';
    const expiresAt = hub.prop(p, 'expiresAt', 'ExpiresAt');
    return `${photoHtml}<strong>${hub.userProfileLink(hub.prop(p, 'createdByUserId', 'CreatedByUserId'), hub.prop(p, 'createdByUsername', 'CreatedByUsername'))}</strong><br>
        <small>${t('List_PopupRemaining', hub.getTimeLeft(expiresAt))}</small>`;
}

function hasVotedOnReport(p) {
    return !!hub.prop(p, 'hasVotedByMe', 'HasVotedByMe');
}

function canVoteOnParking(p) {
    if (!p || hub.isMyReport(p) || hasVotedOnReport(p)) return false;
    return !!hub.getParkingCoords(p);
}


  hub.resetPhotoSelection = resetPhotoSelection;
  hub.resetReportForm = resetReportForm;
  hub.buildPopupContent = buildPopupContent;
  hub.hasVotedOnReport = hasVotedOnReport;
  hub.canVoteOnParking = canVoteOnParking;
}
