import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';

export function registerProfile(hub, t) {
// --- Profile & Reviews ---
function userProfileLink(userId, username) {
    if (!userId || !username) return hub.escapeHtml(username || '');
    const safeName = hub.escapeHtml(username);
    return `<span role="button" tabindex="0" class="user-profile-link" data-user-id="${userId}" data-username="${hub.escapeAttr(username)}">${safeName}</span>`;
}

function renderStarIcons(rating, interactive = false) {
    let html = `<div class="profile-stars${interactive ? '' : ' readonly'}">`;
    for (let i = 1; i <= 5; i++) {
        const filled = i <= rating;
        html += `<span class="profile-star-btn${filled ? ' active' : ''}"><i class="bi bi-star${filled ? '-fill' : ''}"></i></span>`;
    }
    html += '</div>';
    return html;
}

function setProfileSelectedRating(rating) {
    state.profileSelectedRating = rating;
    $('#profile-review-stars .profile-star-btn').each(function () {
        const value = parseInt($(this).data('rating'), 10);
        const active = value <= rating;
        $(this).toggleClass('active', active);
        $(this).find('i').attr('class', `bi bi-star${active ? '-fill' : ''}`);
    });
}

function isProfileSelf() {
    return !!(state.currentUser && state.profileTargetUserId && hub.sameUserId(state.profileTargetUserId, state.currentUser.userId));
}

function switchProfileTab(tab) {
    state.profileActiveTab = tab;
    $('#profile-tabs .nav-link').removeClass('active');
    $(`#profile-tabs .nav-link[data-profile-tab="${tab}"]`).addClass('active');
    $('#profile-tab-password').toggleClass('d-none', tab !== 'password');
    $('#profile-tab-ratings').toggleClass('d-none', tab !== 'ratings');
}

function updateProfileTabsVisibility(self) {
    if (self) {
        $('#profile-tabs').removeClass('d-none');
        switchProfileTab(state.profileActiveTab);
    } else {
        $('#profile-tabs').addClass('d-none');
        switchProfileTab('ratings');
    }
}

async function openProfileModal(userId, username) {
    if (!state.currentUser) return;

    state.profileTargetUserId = userId || state.currentUser.userId;
    state.profileTargetUsername = username || state.currentUser.username;
    state.profileTargetPhotoUrl = getUserPhotoUrl(state.currentUser);
    state.profileSelectedRating = 0;
    state.profileActiveTab = 'ratings';

    $('#top-user-search').val('');
    $('#top-user-suggestions').addClass('d-none').empty();
    $('#profile-current-password, #profile-new-password, #profile-confirm-password').val('');
    $('#profile-review-comment').val('');
    setProfileSelectedRating(0);

    await refreshProfileModal();
    modals.profileModal.show();
}

async function refreshProfileModal() {
    if (!state.profileTargetUserId) return;

    const self = isProfileSelf();
    $('#profile-modal-title').text(
        self ? t('Profile_MyTitle') : t('Profile_UserTitle', state.profileTargetUsername)
    );
    updateProfileTabsVisibility(self);
    $('#profile-add-review-section').toggleClass('d-none', self);

    try {
        const [summary, reviews] = await Promise.all([
            hub.apiGet(`/api/users/${state.profileTargetUserId}/reviews/summary`),
            hub.apiGet(`/api/users/${state.profileTargetUserId}/reviews?page=1&pageSize=30`)
        ]);

        renderProfileSummary(summary);
        state.profileTargetPhotoUrl = getUserPhotoUrl(summary) || state.profileTargetPhotoUrl;
        renderProfileReviews(reviews.items || []);

        if (!self) {
            await syncReviewSubmitState();
        } else {
            $('#profile-eligible-hint').text('');
            $('#btn-submit-review').prop('disabled', true);
        }
    } catch (err) {
        hub.showToast(err.message);
    }
}

async function refreshReviewStarsDisplay(userId) {
    const id = userId || state.currentUser?.userId;
    if (!id) return 0;

    try {
        const summary = await hub.apiGet(`/api/users/${id}/reviews/summary`);
        const totalStars = summary.totalStars ?? summary.TotalStars ?? 0;
        if (state.currentUser && hub.sameUserId(id, state.currentUser.userId)) {
            $('#display-review-stars').text(totalStars);
        }
        return totalStars;
    } catch (_) {
        if (state.currentUser && hub.sameUserId(id, state.currentUser.userId)) {
            $('#display-review-stars').text('0');
        }
        return 0;
    }
}

function renderReviewStarsBadge(totalStars) {
    const value = Number(totalStars) || 0;
    return `<i class="bi bi-star-fill"></i><span>${value}</span>`;
}

function getUserPhotoUrl(userOrSummary) {
    if (!userOrSummary) return null;
    return userOrSummary.profilePhotoUrl || userOrSummary.ProfilePhotoUrl || null;
}

function renderAvatarContent(username, photoUrl, size = 'md') {
    const initial = hub.escapeHtml((username || '?')[0].toUpperCase());
    const alt = hub.escapeHtml(username || t('Profile_PhotoAlt'));
    if (photoUrl) {
        const url = `${photoUrl}${photoUrl.includes('?') ? '&' : '?'}v=${state.profilePhotoVersion}`;
        return `<img src="${hub.escapeHtml(url)}" alt="${alt}">`;
    }
    return size === 'lg' ? initial : `<span class="user-avatar-initial">${initial}</span>`;
}

function updateTopBarAvatar() {
    const $avatar = $('#user-avatar').empty();
    if (!state.currentUser) return;

    const photoUrl = getUserPhotoUrl(state.currentUser);
    if (photoUrl) {
        $avatar.append(renderAvatarContent(state.currentUser.username, photoUrl));
    } else {
        $avatar.text((state.currentUser.username || '?')[0].toUpperCase());
    }
}

function renderProfileSummary(summary) {
    const totalStars = summary.totalStars ?? summary.TotalStars ?? 0;
    const photoUrl = getUserPhotoUrl(summary) || state.profileTargetPhotoUrl;
    const self = isProfileSelf();
    const avatarHtml = renderAvatarContent(state.profileTargetUsername, photoUrl, 'lg');
    const photoActions = self
        ? `<div class="profile-avatar-actions">
                <button type="button" class="btn btn-outline-primary btn-sm" id="btn-profile-change-photo">
                    <i class="bi bi-camera"></i> ${hub.escapeHtml(t('Profile_ChangePhoto'))}
                </button>
                ${photoUrl ? `<button type="button" class="btn btn-outline-secondary btn-sm" id="btn-profile-remove-photo">
                    <i class="bi bi-trash"></i> ${hub.escapeHtml(t('Profile_RemovePhoto'))}
                </button>` : ''}
           </div>`
        : '';

    $('#profile-summary').html(`
        <div class="profile-summary-layout">
            <div class="profile-avatar-wrap">
                <div class="profile-avatar" id="profile-avatar-display">${avatarHtml}</div>
                ${photoActions}
            </div>
            <div class="profile-summary-content">
                <div class="profile-summary-name">${userProfileLink(state.profileTargetUserId, state.profileTargetUsername)}</div>
                <div class="profile-summary-rating d-flex align-items-center gap-1 mt-1">${renderReviewStarsBadge(totalStars)}</div>
            </div>
        </div>
    `);
}

async function uploadProfilePhoto(file) {
    if (!file || !isProfileSelf()) return;

    const formData = new FormData();
    formData.append('photo', file);
    const result = await hub.apiPostForm('/api/users/me/profile-photo', formData);
    const photoUrl = result.profilePhotoUrl || result.ProfilePhotoUrl;
    state.profilePhotoVersion++;
    state.profileTargetPhotoUrl = photoUrl;
    if (state.currentUser) {
        state.currentUser.profilePhotoUrl = photoUrl;
        localStorage.setItem(C.USER_STORAGE_KEY, JSON.stringify(state.currentUser));
    }
    updateTopBarAvatar();
    await refreshProfileModal();
    hub.showToast(t('Profile_PhotoUpdated'));
}

async function removeProfilePhoto() {
    if (!isProfileSelf()) return;
    await hub.apiDelete('/api/users/me/profile-photo');
    state.profilePhotoVersion++;
    state.profileTargetPhotoUrl = null;
    if (state.currentUser) {
        state.currentUser.profilePhotoUrl = null;
        localStorage.setItem(C.USER_STORAGE_KEY, JSON.stringify(state.currentUser));
    }
    updateTopBarAvatar();
    await refreshProfileModal();
    hub.showToast(t('Profile_PhotoUpdated'));
}

function renderProfileReviews(items) {
    const $list = $('#profile-reviews-list').empty();
    if (!items.length) {
        return;
    }

    items.forEach(review => {
        const authorId = review.reviewerUserId || review.ReviewerUserId;
        const author = review.reviewerUsername || review.ReviewerUsername || 'user';
        const rating = review.rating ?? review.Rating ?? 0;
        const comment = review.comment || review.Comment || '';
        const address = review.parkingRequestAddress || review.ParkingRequestAddress || '';
        const createdAt = review.createdAt || review.CreatedAt;
        const dateText = createdAt
            ? hub.parseUtcDate(createdAt).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' })
            : '';
        const requestHtml = address
            ? `<div class="profile-review-card-request">${hub.escapeHtml(t('Profile_ReviewOnRequest', address))}</div>`
            : '';

        $list.append(`
            <div class="profile-review-card">
                <div class="profile-review-card-header">
                    <div>
                        <div class="profile-review-card-author">${userProfileLink(authorId, author)}</div>
                        <div class="profile-review-card-meta">${hub.escapeHtml(dateText)}</div>
                    </div>
                    ${renderStarIcons(rating)}
                </div>
                ${requestHtml}
                ${comment ? `<p class="profile-review-card-comment">${hub.escapeHtml(comment)}</p>` : ''}
            </div>
        `);
    });
}

async function syncReviewSubmitState() {
    try {
        const status = await hub.apiGet(`/api/users/${state.profileTargetUserId}/reviews/status`);
        const alreadyRated = status.alreadyRated ?? status.AlreadyRated;
        const canRate = status.canRate ?? status.CanRate;

        if (alreadyRated) {
            $('#profile-eligible-hint').text(t('Profile_AlreadyReviewed'));
            $('#btn-submit-review').prop('disabled', true);
        } else if (!canRate) {
            $('#profile-eligible-hint').text(t('Profile_NoEligibleRequests'));
            $('#btn-submit-review').prop('disabled', true);
        } else {
            $('#profile-eligible-hint').text('');
            $('#btn-submit-review').prop('disabled', false);
        }
    } catch (err) {
        $('#profile-eligible-hint').text(err.message);
        $('#btn-submit-review').prop('disabled', true);
    }
}

async function searchTopBarUsers(query) {
    const $box = $('#top-user-suggestions');
    if (query.length < 2) {
        $box.addClass('d-none').empty();
        return;
    }

    try {
        const users = await hub.apiGet(`/api/users/search?q=${encodeURIComponent(query)}&limit=8`);
        $box.empty();
        if (!users.length) {
            $box.html(`<div class="profile-empty">${t('Profile_NoUsersFound')}</div>`).removeClass('d-none');
            return;
        }

        users.forEach(u => {
            const id = u.userId || u.UserId;
            const name = u.username || u.Username;
            $box.append(`<button type="button" class="top-user-suggestion" data-user-id="${id}" data-username="${hub.escapeAttr(name)}">${hub.escapeHtml(name)}</button>`);
        });
        $box.removeClass('d-none');
    } catch (err) {
        $box.html(`<div class="profile-empty">${hub.escapeHtml(err.message)}</div>`).removeClass('d-none');
    }
}

function hideTopUserSuggestions() {
    $('#top-user-suggestions').addClass('d-none').empty();
}

$('#profile-tabs .nav-link').on('click', function () {
    switchProfileTab($(this).data('profile-tab'));
});

$('#btn-open-profile').on('click keypress', function (e) {
    if (e.type === 'keypress' && e.which !== 13) return;
    e.preventDefault();
    openProfileModal(state.currentUser?.userId, state.currentUser?.username);
});

$(document).on('click', '.user-profile-link', function (e) {
    e.preventDefault();
    e.stopPropagation();
    openProfileModal($(this).data('user-id'), $(this).data('username'));
});

$(document).on('keydown', '.user-profile-link', function (e) {
    if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        e.stopPropagation();
        openProfileModal($(this).data('user-id'), $(this).data('username'));
    }
});

$('#top-user-search').on('input', function () {
    clearTimeout(state.profileSearchTimer);
    const q = $(this).val().trim();
    state.profileSearchTimer = setTimeout(() => searchTopBarUsers(q), 280);
});

$(document).on('click', '.top-user-suggestion', async function () {
    const userId = $(this).data('user-id');
    const username = $(this).data('username');
    $('#top-user-search').val('');
    hideTopUserSuggestions();
    await openProfileModal(userId, username);
});

$(document).on('click', function (e) {
    if (!$(e.target).closest('.top-bar-search-wrap').length) {
        hideTopUserSuggestions();
    }
});

$('#profile-review-stars .profile-star-btn').on('click', function () {
    setProfileSelectedRating(parseInt($(this).data('rating'), 10));
});

$(document).on('click', '#btn-profile-change-photo', function () {
    $('#profile-photo-input').trigger('click');
});

$(document).on('click', '#btn-profile-remove-photo', async function () {
    const $btn = $(this).prop('disabled', true);
    try {
        await removeProfilePhoto();
    } catch (err) {
        hub.showToast(hub.translateError(err.message));
    } finally {
        $btn.prop('disabled', false);
    }
});

$('#profile-photo-input').on('change', async function () {
    const file = this.files && this.files[0];
    this.value = '';
    if (!file) return;

    try {
        await uploadProfilePhoto(file);
    } catch (err) {
        hub.showToast(hub.translateError(err.message));
    }
});

$('#profile-password-form').on('submit', async function (e) {
    e.preventDefault();
    const currentPassword = $('#profile-current-password').val();
    const newPassword = $('#profile-new-password').val();
    const confirmPassword = $('#profile-confirm-password').val();

    if (newPassword !== confirmPassword) {
        hub.showToast(t('Profile_PasswordMismatch'));
        return;
    }

    try {
        await hub.apiPut('/api/users/me/password', { currentPassword, newPassword });
        $('#profile-current-password, #profile-new-password, #profile-confirm-password').val('');
        hub.showToast(t('Profile_PasswordChanged'));
    } catch (err) {
        hub.showToast(err.message);
    }
});

$('#btn-submit-review').on('click', async function () {
    if (!state.profileTargetUserId || isProfileSelf()) return;

    if (state.profileSelectedRating < 1) {
        hub.showToast(t('Error_InvalidReviewRating'));
        return;
    }

    const comment = $('#profile-review-comment').val().trim();
    const $btn = $(this).prop('disabled', true);

    try {
        await hub.apiPost(`/api/users/${state.profileTargetUserId}/reviews`, {
            rating: state.profileSelectedRating,
            comment: comment || null
        });
        hub.showToast(t('Profile_ReviewSubmitted'));
        state.profileSelectedRating = 0;
        setProfileSelectedRating(0);
        $('#profile-review-comment').val('');
        await refreshProfileModal();
        if (state.currentUser && hub.sameUserId(state.profileTargetUserId, state.currentUser.userId)) {
            await refreshReviewStarsDisplay();
        }
    } catch (err) {
        hub.showToast(err.message);
    } finally {
        $btn.prop('disabled', false);
    }
});

$('#profile-modal').on('hidden.bs.modal', function () {
    state.profileSelectedRating = 0;
    setProfileSelectedRating(0);
});


  hub.userProfileLink = userProfileLink;
  hub.renderStarIcons = renderStarIcons;
  hub.setProfileSelectedRating = setProfileSelectedRating;
  hub.isProfileSelf = isProfileSelf;
  hub.switchProfileTab = switchProfileTab;
  hub.updateProfileTabsVisibility = updateProfileTabsVisibility;
  hub.openProfileModal = openProfileModal;
  hub.refreshProfileModal = refreshProfileModal;
  hub.refreshReviewStarsDisplay = refreshReviewStarsDisplay;
  hub.renderReviewStarsBadge = renderReviewStarsBadge;
  hub.getUserPhotoUrl = getUserPhotoUrl;
  hub.renderAvatarContent = renderAvatarContent;
  hub.updateTopBarAvatar = updateTopBarAvatar;
  hub.renderProfileSummary = renderProfileSummary;
  hub.uploadProfilePhoto = uploadProfilePhoto;
  hub.removeProfilePhoto = removeProfilePhoto;
  hub.renderProfileReviews = renderProfileReviews;
  hub.syncReviewSubmitState = syncReviewSubmitState;
  hub.searchTopBarUsers = searchTopBarUsers;
  hub.hideTopUserSuggestions = hideTopUserSuggestions;
}
