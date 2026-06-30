import { C } from './constants.js';
import { state } from './state.js';

export function registerApi(hub, translateError, cultureHeaders) {
    function getAccessToken() {
        return localStorage.getItem(C.ACCESS_TOKEN_KEY);
    }

    function getRefreshToken() {
        return localStorage.getItem(C.REFRESH_TOKEN_KEY);
    }

    function isAccessExpired() {
        const expires = localStorage.getItem(C.ACCESS_EXPIRES_KEY);
        if (!expires) return true;
        return new Date(expires) <= new Date(Date.now() + 60_000);
    }

    function saveAuthTokens(res) {
        localStorage.setItem(C.ACCESS_TOKEN_KEY, res.accessToken || res.AccessToken);
        localStorage.setItem(C.REFRESH_TOKEN_KEY, res.refreshToken || res.RefreshToken);
        localStorage.setItem(C.ACCESS_EXPIRES_KEY, res.accessTokenExpiresAt || res.AccessTokenExpiresAt);
    }

    function clearAuthTokens() {
        localStorage.removeItem(C.ACCESS_TOKEN_KEY);
        localStorage.removeItem(C.REFRESH_TOKEN_KEY);
        localStorage.removeItem(C.ACCESS_EXPIRES_KEY);
    }

    function authHeaders(extra = {}) {
        const headers = cultureHeaders(extra);
        const token = getAccessToken();
        if (token) headers['Authorization'] = `Bearer ${token}`;
        return headers;
    }

    async function tryRefreshToken() {
        const refreshToken = getRefreshToken();
        if (!refreshToken) return false;
        if (state.refreshInFlight) return state.refreshInFlight;

        state.refreshInFlight = (async () => {
            try {
                const res = await fetch(C.API_BASE + '/api/auth/refresh', {
                    method: 'POST',
                    headers: cultureHeaders({ 'Content-Type': 'application/json' }),
                    body: JSON.stringify({ refreshToken })
                });
                const data = await res.json().catch(() => ({}));
                if (!res.ok) throw new Error(translateError(data.error || 'Toast_NetworkError'));
                saveAuthTokens(data);
                hub.saveUser(data.user || data.User);
                return true;
            } finally {
                state.refreshInFlight = null;
            }
        })();

        return state.refreshInFlight;
    }

    async function apiFetch(url, options = {}, retrying = false) {
        const res = await fetch(C.API_BASE + url, {
            ...options,
            headers: authHeaders(options.headers || {})
        });

        if (res.status === 401 && !retrying && getRefreshToken()) {
            const refreshed = await tryRefreshToken();
            if (refreshed) return apiFetch(url, options, true);
        }

        const contentType = res.headers.get('content-type') || '';
        const hasJson = contentType.includes('application/json');
        const data = hasJson ? await res.json().catch(() => ({})) : null;

        if (!res.ok) {
            throw new Error(translateError((data && data.error) || 'Toast_NetworkError'));
        }

        if (res.status === 204) return null;
        return data;
    }

    async function apiPostForm(url, formData) {
        return apiFetch(url, { method: 'POST', body: formData });
    }

    async function apiPost(url, body) {
        return apiFetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
    }

    async function apiPut(url, body) {
        return apiFetch(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
    }

    async function apiGet(url) {
        return apiFetch(url, { method: 'GET' });
    }

    async function apiDelete(url) {
        return apiFetch(url, { method: 'DELETE' });
    }

    Object.assign(hub, {
        getAccessToken,
        getRefreshToken,
        isAccessExpired,
        saveAuthTokens,
        clearAuthTokens,
        authHeaders,
        tryRefreshToken,
        apiFetch,
        apiPostForm,
        apiPost,
        apiPut,
        apiGet,
        apiDelete
    });
}
