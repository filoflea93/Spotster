(function (global) {
    'use strict';

    const LANG_KEY = 'spotsterLang';
    let strings = {};
    let currentLang = 'it';

    function normalizeLang(lang) {
        if (!lang) return 'it';
        lang = String(lang).toLowerCase();
        return lang.startsWith('en') ? 'en' : 'it';
    }

    function getLang() {
        return currentLang;
    }

    function format(template, args) {
        if (!template) return '';
        return template.replace(/\{(\d+)\}/g, (_, index) => {
            const i = parseInt(index, 10);
            return args[i] !== undefined ? args[i] : `{${index}}`;
        });
    }

    function t(key, ...args) {
        const template = strings[key] || key;
        return args.length ? format(template, args) : template;
    }

    function getCultureHeaders() {
        return { 'X-Culture': currentLang, 'Accept-Language': currentLang };
    }

    async function loadLanguage(lang) {
        currentLang = normalizeLang(lang);
        localStorage.setItem(LANG_KEY, currentLang);
        document.documentElement.lang = currentLang;

        const res = await fetch(`/api/localization/strings?culture=${currentLang}`);
        if (!res.ok) {
            console.warn('Unable to load translations');
            return;
        }
        strings = await res.json();
        applyDom();
        updateLangSwitcher();
    }

    function applyDom() {
        document.querySelectorAll('[data-i18n]').forEach(el => {
            const key = el.getAttribute('data-i18n');
            const attr = el.getAttribute('data-i18n-attr');
            const value = t(key);
            if (attr) {
                el.setAttribute(attr, value);
            } else {
                el.textContent = value;
            }
        });

        document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
            el.setAttribute('placeholder', t(el.getAttribute('data-i18n-placeholder')));
        });

        const titleEl = document.querySelector('title[data-i18n]');
        if (titleEl) {
            document.title = t(titleEl.getAttribute('data-i18n'));
        }
    }

    function updateLangSwitcher() {
        document.querySelectorAll('.lang-btn').forEach(btn => {
            const lang = btn.getAttribute('data-lang');
            btn.classList.toggle('active', lang === currentLang);
        });
    }

    async function init() {
        const stored = localStorage.getItem(LANG_KEY);
        await loadLanguage(stored || 'it');
    }

    async function setLanguage(lang) {
        if (normalizeLang(lang) === currentLang) return;
        await loadLanguage(lang);
        if (typeof global.onLanguageChanged === 'function') {
            global.onLanguageChanged(currentLang);
        }
    }

    global.SpotsterI18n = {
        init,
        t,
        getLang,
        setLanguage,
        loadLanguage,
        applyDom,
        getCultureHeaders
    };
})(window);
