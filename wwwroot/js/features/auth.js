import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';

export function registerAuth(hub, t) {
// --- Auth ---
function normalizeUser(user) {
    if (!user) return null;
    return {
        userId: user.userId || user.UserId,
        username: user.username || user.Username || '',
        reputationScore: user.reputationScore ?? user.ReputationScore ?? 0,
        accuracyRate: user.accuracyRate ?? user.AccuracyRate ?? 0,
        status: user.status || user.Status || 'Active',
        suspendedUntil: user.suspendedUntil || user.SuspendedUntil || null,
        suspiciousScore: user.suspiciousScore ?? user.SuspiciousScore ?? 0,
        profilePhotoUrl: user.profilePhotoUrl || user.ProfilePhotoUrl || null
    };
}

function isAccountSuspended() {
    return !!(state.currentUser && state.currentUser.status === 'Suspended');
}

function applyUserProfile(profile) {
    if (!state.currentUser) return;
    state.currentUser = normalizeUser({ ...state.currentUser, ...profile });
    localStorage.setItem(C.USER_STORAGE_KEY, JSON.stringify(state.currentUser));
    hub.updateAccountStatusUI();
    hub.updateTopBarAvatar();
}

function formatSuspendedClock(value) {
    if (!value) return '';
    return hub.parseUtcDate(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function getSuspensionRemainingMs() {
    if (!state.currentUser?.suspendedUntil) return 0;
    return hub.parseUtcDate(state.currentUser.suspendedUntil) - new Date();
}

function formatSuspensionCountdown(ms) {
    if (ms <= 0) return '00:00';
    const totalSeconds = Math.floor(ms / 1000);
    const mins = Math.floor(totalSeconds / 60);
    const secs = totalSeconds % 60;
    return `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
}

function updateSuspensionCountdown() {
    if (!isAccountSuspended()) return;

    const remainingMs = getSuspensionRemainingMs();
    const countdown = formatSuspensionCountdown(remainingMs);
    const untilText = formatSuspendedClock(state.currentUser.suspendedUntil);

    $('#account-suspended-detail').text(t('Account_Suspended_Detail', untilText));
    $('#account-suspended-countdown').text(countdown);
    $('#suspended-modal-until').text(t('Account_Suspended_Modal_Until', untilText));
    $('#suspended-modal-countdown').text(countdown);

    if (remainingMs <= 0) {
        hub.refreshUserProfile();
    }
}

function updateAccountStatusUI() {
    const suspended = isAccountSuspended();
    $('#dashboard').toggleClass('has-suspended-banner', suspended);
    $('#account-suspended-banner').toggleClass('d-none', !suspended);
    $('#account-status-badge').toggleClass('d-none', !suspended);
    $('#user-avatar').toggleClass('user-avatar-suspended', suspended);
    $('#btn-action').toggleClass('fab-suspended', suspended);

    if (suspended) {
        updateSuspensionCountdown();
    }
    hub.syncDashboardChromeTop();
}

function syncDashboardChromeTop() {
    const dashboard = document.getElementById('dashboard');
    if (!dashboard || dashboard.classList.contains('d-none')) {
        document.documentElement.style.setProperty('--map-chrome-left', '0px');
        document.documentElement.style.setProperty('--dashboard-chrome-top', '0px');
        document.body.classList.remove('dashboard-map-active');
        return;
    }

    document.body.classList.add('dashboard-map-active');

    const topBar = document.querySelector('.top-bar');
    const banner = document.getElementById('account-suspended-banner');
    const topBarH = topBar ? topBar.offsetHeight : 58;
    const bannerH = banner && !banner.classList.contains('d-none') ? banner.offsetHeight : 0;

    dashboard.style.setProperty('--top-bar-height', `${topBarH}px`);
    dashboard.style.setProperty('--dashboard-chrome-top', `${topBarH + bannerH}px`);
    document.documentElement.style.setProperty('--top-bar-height', `${topBarH}px`);
    document.documentElement.style.setProperty('--dashboard-chrome-top', `${topBarH + bannerH}px`);

    const sidebar = document.getElementById('listings-sidebar');
    const collapsed = sidebar?.classList.contains('is-collapsed');
    const sidebarW = sidebar ? sidebar.offsetWidth : 0;
    document.documentElement.style.setProperty('--requests-sidebar-width', `${sidebarW || 340}px`);
    document.documentElement.style.setProperty('--map-chrome-left', collapsed ? '0px' : `${sidebarW}px`);
}

function isSidebarCollapsed() {
    return document.getElementById('listings-sidebar')?.classList.contains('is-collapsed');
}

function updateSidebarToggleUi(collapsed) {
    const iconClass = collapsed ? 'bi bi-chevron-right' : 'bi bi-chevron-left';
    const labelKey = collapsed ? 'Sidebar_Open' : 'Sidebar_Close';
    const label = t(labelKey);

    $('#btn-toggle-sidebar, #btn-sidebar-header-collapse').each(function () {
        const $btn = $(this);
        $btn.attr('aria-expanded', collapsed ? 'false' : 'true');
        $btn.attr('title', label);
        $btn.attr('aria-label', label);
        $btn.find('i').attr('class', iconClass);
    });
}

function setSidebarCollapsed(collapsed, persist = true) {
    const sidebar = document.getElementById('listings-sidebar');
    if (!sidebar) return;

    sidebar.classList.toggle('is-collapsed', collapsed);
    document.body.classList.toggle('sidebar-collapsed', collapsed);
    updateSidebarToggleUi(collapsed);

    if (persist) {
        localStorage.setItem(C.SIDEBAR_COLLAPSED_KEY, collapsed ? '1' : '0');
    }

    hub.syncDashboardChromeTop();
    hub.refreshMapView();
    window.setTimeout(() => hub.refreshMapView(), 320);
}

function toggleSidebarCollapsed() {
    setSidebarCollapsed(!isSidebarCollapsed());
}

function initSidebarToggle() {
    const collapsed = localStorage.getItem(C.SIDEBAR_COLLAPSED_KEY) === '1';
    setSidebarCollapsed(collapsed, false);

    $('#btn-toggle-sidebar, #btn-sidebar-header-collapse').off('click.sidebar').on('click.sidebar', function (e) {
        e.preventDefault();
        e.stopPropagation();
        toggleSidebarCollapsed();
    });
}

function initDashboardChromeLayout() {
    hub.syncDashboardChromeTop();

    const topBar = document.querySelector('.top-bar');
    const banner = document.getElementById('account-suspended-banner');

    if (typeof ResizeObserver !== 'undefined') {
        if (state.dashboardChromeObserver) {
            state.dashboardChromeObserver.disconnect();
        }
        state.dashboardChromeObserver = new ResizeObserver(() => {
            hub.syncDashboardChromeTop();
            hub.refreshMapView();
        });
        if (topBar) state.dashboardChromeObserver.observe(topBar);
        if (banner) state.dashboardChromeObserver.observe(banner);
        const sidebar = document.getElementById('listings-sidebar');
        if (sidebar) state.dashboardChromeObserver.observe(sidebar);
    }

    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', syncDashboardChromeTop);
        window.visualViewport.addEventListener('scroll', syncDashboardChromeTop);
    }
}

function showSuspendedNotice(forceModal = false) {
    if (!isAccountSuspended()) return;
    hub.updateAccountStatusUI();
    if (forceModal || !sessionStorage.getItem('suspensionModalShown')) {
        updateSuspensionCountdown();
        modals.suspendedModal.show();
        sessionStorage.setItem('suspensionModalShown', '1');
    }
}

function pulseSuspendedBanner() {
    const $banner = $('#account-suspended-banner');
    $banner.addClass('account-suspended-pulse');
    setTimeout(() => $banner.removeClass('account-suspended-pulse'), 900);
}

function blockIfSuspended() {
    if (!isAccountSuspended()) return false;
    hub.showToast(t('Account_Suspended_ActionBlocked'), 4500);
    pulseSuspendedBanner();
    return true;
}

async function refreshUserProfile() {
    if (!state.currentUser) return;
    try {
        const profile = await hub.apiGet('/api/auth/me');
        applyUserProfile(profile);
        if (!isAccountSuspended()) {
            sessionStorage.removeItem('suspensionModalShown');
        }
    } catch (_) { /* ignore */ }
}

function loadUser() {
    const stored = localStorage.getItem(C.USER_STORAGE_KEY);
    if (!stored || !hub.getRefreshToken()) return;

    state.currentUser = normalizeUser(JSON.parse(stored));
    (async () => {
        if (!hub.getAccessToken() || hub.isAccessExpired()) {
            try {
                const refreshed = await hub.tryRefreshToken();
                if (!refreshed) {
                    logout();
                    return;
                }
            } catch {
                logout();
                return;
            }
        }
        hub.showDashboard();
    })();
}

function saveUser(user) {
    state.currentUser = normalizeUser(user);
    localStorage.setItem(C.USER_STORAGE_KEY, JSON.stringify(state.currentUser));
}

async function logout() {
    const refreshToken = hub.getRefreshToken();
    hub.stopCountdownTimer();
    try {
        if (refreshToken && hub.getAccessToken()) {
            await hub.apiPost('/api/auth/logout', { refreshToken });
        }
    } catch (_) { /* ignore */ }
    state.currentUser = null;
    state.userLocation = null;
    hub.clearAuthTokens();
    localStorage.removeItem(C.USER_STORAGE_KEY);
    if (state.connection) state.connection.stop();
    $('#dashboard').addClass('d-none');
    $('#auth-screen').removeClass('d-none');
    document.body.classList.remove('dashboard-map-active');
    document.documentElement.style.setProperty('--map-chrome-left', '0px');
}

function showAuthError(msg) {
    $('#auth-success').addClass('d-none');
    $('#auth-alert').text(msg).removeClass('d-none');
}

function hideAuthError() {
    $('#auth-alert').addClass('d-none');
}

function showAuthSuccess(msg) {
    hideAuthError();
    $('#auth-success').text(msg).removeClass('d-none');
}

function hideAuthSuccess() {
    $('#auth-success').addClass('d-none');
}

function setRegisterBirthdateLimits() {
    const input = document.getElementById('register-birthdate');
    if (!input) return;
    const today = new Date();
    const max = new Date(today.getFullYear() - 18, today.getMonth(), today.getDate());
    const min = new Date(today.getFullYear() - 100, today.getMonth(), today.getDate());
    input.max = max.toISOString().slice(0, 10);
    input.min = min.toISOString().slice(0, 10);
}

function handleEmailConfirmationRedirect() {
    const params = new URLSearchParams(window.location.search);
    if (params.get('emailConfirmed') === '1') {
        showAuthSuccess(t('Auth_EmailConfirmed'));
        $('#auth-tabs .nav-link[data-tab="login"]').trigger('click');
        window.history.replaceState({}, '', window.location.pathname);
        return;
    }
    if (params.get('emailConfirmError') === '1') {
        showAuthError(t('Auth_EmailConfirmFailed'));
        $('#auth-resend-wrap').removeClass('d-none');
        window.history.replaceState({}, '', window.location.pathname);
    }
}

function showResendConfirmation(show) {
    $('#auth-resend-wrap').toggleClass('d-none', !show);
}

$('#auth-tabs .nav-link').on('click', function () {
    $('#auth-tabs .nav-link').removeClass('active');
    $(this).addClass('active');
    hideAuthError();
    hideAuthSuccess();
    showResendConfirmation(false);
    const tab = $(this).data('tab');
    if (tab === 'login') {
        $('#login-form').removeClass('d-none');
        $('#register-form').addClass('d-none');
    } else {
        $('#login-form').addClass('d-none');
        $('#register-form').removeClass('d-none');
    }
});

$('#login-form').on('submit', async function (e) {
    e.preventDefault();
    hideAuthError();
    hideAuthSuccess();
    showResendConfirmation(false);
    try {
        const res = await hub.apiPost('/api/auth/login', {
            username: $('#login-username').val(),
            password: $('#login-password').val()
        });
        hub.saveAuthTokens(res);
        saveUser(res.user || res.User);
        hub.showDashboard();
    } catch (err) {
        showAuthError(err.message);
        if (err.message === t('Error_EmailNotConfirmed')) {
            showResendConfirmation(true);
            $('#resend-email').val($('#login-username').val().includes('@')
                ? $('#login-username').val()
                : $('#register-email').val() || '');
        }
    }
});

$('#btn-resend-confirmation').on('click', async function () {
    const email = ($('#resend-email').val() || '').trim();
    if (!email) {
        showAuthError(t('Error_EmailRequired'));
        return;
    }
    try {
        await hub.apiPost('/api/auth/resend-confirmation', { email });
        showAuthSuccess(t('Auth_ResendConfirmationDone'));
    } catch (err) {
        showAuthError(err.message);
    }
});

$('#register-form').on('submit', async function (e) {
    e.preventDefault();
    hideAuthError();
    hideAuthSuccess();
    const password = $('#register-password').val();
    const confirm = $('#register-password-confirm').val();
    if (password !== confirm) {
        showAuthError(t('Auth_PasswordMismatch'));
        return;
    }
    try {
        const registeredEmail = ($('#register-email').val() || '').trim();
        const res = await hub.apiPost('/api/auth/register', {
            username: $('#register-username').val(),
            email: registeredEmail,
            password,
            firstName: $('#register-firstname').val(),
            lastName: $('#register-lastname').val(),
            dateOfBirth: $('#register-birthdate').val()
        });
        showAuthSuccess(res.message || t('Auth_RegisterPending'));
        $('#auth-tabs .nav-link[data-tab="login"]').trigger('click');
        $('#register-form')[0].reset();
        setRegisterBirthdateLimits();
        $('#resend-email').val(registeredEmail || res.email || '');
        showResendConfirmation(true);
    } catch (err) {
        showAuthError(err.message);
    }
});

$('#btn-logout').on('click', logout);


  hub.normalizeUser = normalizeUser;
  hub.isAccountSuspended = isAccountSuspended;
  hub.applyUserProfile = applyUserProfile;
  hub.formatSuspendedClock = formatSuspendedClock;
  hub.getSuspensionRemainingMs = getSuspensionRemainingMs;
  hub.formatSuspensionCountdown = formatSuspensionCountdown;
  hub.updateSuspensionCountdown = updateSuspensionCountdown;
  hub.updateAccountStatusUI = updateAccountStatusUI;
  hub.syncDashboardChromeTop = syncDashboardChromeTop;
  hub.isSidebarCollapsed = isSidebarCollapsed;
  hub.updateSidebarToggleUi = updateSidebarToggleUi;
  hub.setSidebarCollapsed = setSidebarCollapsed;
  hub.toggleSidebarCollapsed = toggleSidebarCollapsed;
  hub.initSidebarToggle = initSidebarToggle;
  hub.initDashboardChromeLayout = initDashboardChromeLayout;
  hub.showSuspendedNotice = showSuspendedNotice;
  hub.pulseSuspendedBanner = pulseSuspendedBanner;
  hub.blockIfSuspended = blockIfSuspended;
  hub.refreshUserProfile = refreshUserProfile;
  hub.loadUser = loadUser;
  hub.saveUser = saveUser;
  hub.logout = logout;
  hub.showAuthError = showAuthError;
  hub.hideAuthError = hideAuthError;
  hub.showAuthSuccess = showAuthSuccess;
  hub.hideAuthSuccess = hideAuthSuccess;
  hub.setRegisterBirthdateLimits = setRegisterBirthdateLimits;
  hub.handleEmailConfirmationRedirect = handleEmailConfirmationRedirect;
  hub.showResendConfirmation = showResendConfirmation;
}
