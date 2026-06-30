import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';

export function registerVote(hub, t) {
// --- Vote / parking detail ---
function openParkingDetailModal(p) {
    if (!p) return;

    const reportId = hub.prop(p, 'id', 'Id');
    state.selectedParkingId = reportId;
    const coords = hub.getParkingCoords(p);
    const expiresAt = hub.prop(p, 'expiresAt', 'ExpiresAt');
    const photoUrl = hub.prop(p, 'photoUrl', 'PhotoUrl');
    const canVote = hub.canVoteOnParking(p);
    const ownReport = hub.isMyReport(p);
    const alreadyVoted = hub.hasVotedOnReport(p);

    if (photoUrl) {
        $('#parking-detail-photo').attr('src', photoUrl).attr('alt', t('List_VotePhotoAlt'));
        $('#parking-detail-photo-wrap').removeClass('d-none');
        $('#parking-detail-no-photo').addClass('d-none');
    } else {
        $('#parking-detail-photo').attr('src', '');
        $('#parking-detail-photo-wrap').addClass('d-none');
        $('#parking-detail-no-photo').removeClass('d-none');
    }

    $('#parking-detail-meta').html(`
        <div class="parking-detail-user">${hub.userProfileLink(hub.prop(p, 'createdByUserId', 'CreatedByUserId'), hub.prop(p, 'createdByUsername', 'CreatedByUsername'))}</div>
        <div class="parking-detail-stats">
            <span><i class="bi bi-hand-thumbs-up"></i> ${p.validVotes ?? 0}</span>
            <span><i class="bi bi-hand-thumbs-down"></i> ${p.invalidVotes ?? 0}</span>
            <span><i class="bi bi-shield-check"></i> ${Math.round(p.confidenceScore ?? 0)}%</span>
        </div>
        <div class="parking-detail-expires text-muted">
            <i class="bi bi-clock"></i> ${t('List_PopupRemaining', hub.getTimeLeft(expiresAt))}
        </div>
        ${coords ? `<div class="parking-detail-coords text-muted small mt-1">Lat ${coords.lat.toFixed(5)}, Lng ${coords.lng.toFixed(5)}</div>` : ''}
    `);

    const $info = $('#parking-detail-info-section').empty().addClass('d-none');
    if (ownReport) {
        $info.removeClass('d-none').html(`<p class="mb-0 text-muted">${hub.escapeHtml(t('Modal_ParkingDetail_OwnReport'))}</p>`);
    }

    $('#parking-detail-vote-section').toggleClass('d-none', !canVote);
    $('#parking-detail-voted-section').toggleClass('d-none', !alreadyVoted || ownReport);
    $('#parking-detail-voted-text').text(t('Modal_Vote_AlreadyDone'));

    modals.parkingDetailModal.show();
}

$('#btn-detail-vote-valid').on('click', () => submitVote(true));
$('#btn-detail-vote-invalid').on('click', () => submitVote(false));

async function submitVote(isValid) {
    if (!state.selectedParkingId) return;
    try {
        const p = await hub.apiPost('/api/parking/vote', {
            parkingReportId: state.selectedParkingId,
            isValid
        });
        hub.upsertParking({ ...p, hasVotedByMe: true, HasVotedByMe: true });
        modals.parkingDetailModal.hide();
        hub.showToast(isValid ? t('Toast_VoteYes') : t('Toast_VoteNo'));
    } catch (err) {
        hub.showToast(err.message);
    }
}


  hub.openParkingDetailModal = openParkingDetailModal;
  hub.submitVote = submitVote;
}
