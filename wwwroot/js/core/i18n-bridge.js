'use strict';

export function createT() {
    return (...args) => SpotsterI18n.t(...args);
}

export function translateError(t, msg) {
    if (!msg || typeof msg !== 'string') return t('Toast_NetworkError');
    const translated = t(msg);
    return translated !== msg ? translated : msg;
}

export function cultureHeaders(extra = {}) {
    return { ...extra, ...SpotsterI18n.getCultureHeaders() };
}

export function prop(obj, camel, pascal) {
    if (!obj) return undefined;
    return obj[camel] ?? obj[pascal];
}
