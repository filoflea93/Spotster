export function registerDom(hub) {
    function showToast(msg, duration = 3000, options = {}) {
        const useHtml = options.html === true;
        const $t = useHtml
            ? $(`<div class="toast-notification${duration > 3000 ? ' toast-long' : ''}${options.onClick ? ' clickable' : ''}">${msg}</div>`)
            : $(`<div class="toast-notification${options.onClick ? ' clickable' : ''}">${escapeHtml(msg)}</div>`);
        if (options.onClick) {
            $t.on('click', options.onClick);
        }
        const $c = $('<div class="toast-container"></div>').append($t);
        $('body').append($c);
        setTimeout(() => $c.remove(), duration);
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function escapeAttr(text) {
        return String(text ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;');
    }

    Object.assign(hub, { showToast, escapeHtml, escapeAttr });
}
