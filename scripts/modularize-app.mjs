import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..', 'wwwroot', 'js');
const srcPath = path.join(root, 'app.js');
const src = fs.readFileSync(srcPath, 'utf8');

const bodyMatch = src.match(/\(async function \(\) \{([\s\S]*)\}\)\(\);/);
if (!bodyMatch) throw new Error('Cannot parse app.js IIFE');
let body = bodyMatch[1];

// Remove init header (i18n await + t helper + early helpers moved to core)
const initEnd = body.indexOf('    const API_BASE');
if (initEnd < 0) throw new Error('Cannot find API_BASE');
body = body.slice(initEnd);

const sections = [
    { name: 'auth', marker: '// --- Auth ---', file: 'features/auth.js' },
    { name: 'dashboard', marker: '// --- Dashboard ---', file: 'features/dashboard.js' },
    { name: 'api', marker: '// --- API ---', file: 'core/api.js' },
    { name: 'parkings', marker: '// --- Parkings ---', file: 'features/parkings.js' },
    { name: 'fab', marker: '// --- FAB & actions ---', file: 'features/fab.js' },
    { name: 'search', marker: '// --- Search parking ---', file: 'features/search.js' },
    { name: 'report', marker: '// --- Report (photo) ---', file: 'features/report.js' },
    { name: 'vote', marker: '// --- Vote / parking detail ---', file: 'features/vote.js' },
    { name: 'signalr', marker: '// --- SignalR ---', file: 'features/signalr.js' },
    { name: 'profile', marker: '// --- Profile & Reviews ---', file: 'features/profile.js' },
    { name: 'utils', marker: '// --- Utils ---', file: 'core/dom.js' }
];

const markers = sections.map(s => s.marker);
const chunks = new Map();

for (let i = 0; i < sections.length; i++) {
    const start = body.indexOf(sections[i].marker);
    if (start < 0) throw new Error(`Missing marker ${sections[i].marker}`);
    let end = body.length;
    for (let j = i + 1; j < sections.length; j++) {
        const next = body.indexOf(sections[j].marker);
        if (next >= 0) { end = next; break; }
    }
    chunks.set(sections[i].file, body.slice(start, end));
}

// Pre-API chunk (map, geo, layout before API marker) -> features/map.js
const apiStart = body.indexOf('// --- API ---');
const authStart = body.indexOf('// --- Auth ---');
const dashStart = body.indexOf('// --- Dashboard ---');
const preApi = body.slice(dashStart, apiStart);
chunks.set('features/map.js', preApi);

// Between API and Parkings: requests + chat
const parkStart = body.indexOf('// --- Parkings ---');
const apiChunk = body.slice(apiStart, parkStart);
chunks.set('features/requests.js', apiChunk.replace('// --- API ---', '// --- Requests & Chat ---\n'));

// Tail after utils: wiring
const utilsStart = body.indexOf('// --- Utils ---');
const tailStart = body.indexOf("$('#report-modal').on('hidden.bs.modal'");
chunks.set('app/bootstrap.js', body.slice(tailStart));

// Auth chunk includes everything from Auth to Dashboard
const authChunk = body.slice(authStart, dashStart);
chunks.set('features/auth.js', authChunk);

const stateVars = [
    'viewRadiusMeters', 'currentUser', 'map', 'mapTileLayer', 'markers', 'parkings', 'parkingRequests',
    'selectedParkingId', 'userLocation', 'mapLimitCircle', 'userPositionMarker', 'chatLocationLayerGroup',
    'activeChatLocateKey', 'requestLayers', 'requestLayerGroup', 'parkingLayerGroup', 'searchPreviewLayer',
    'searchPreviewLayerGroup', 'loadRequestsSeq', 'geocodedLocation', 'addressSuggestTimer', 'addressSuggestions',
    'activeSuggestionIndex', 'activeSheet', 'requestsDealFilter', 'connection', 'selectedPhotoFile',
    'pendingDeleteListing', 'profilePhotoVersion', 'profileTargetUserId', 'profileTargetUsername',
    'profileTargetPhotoUrl', 'profileSelectedRating', 'profileActiveTab', 'profileSearchTimer',
    'activeChatRequestId', 'activeChatIsMine', 'activeChatGuestUserId', 'ownerConversationCache',
    'paymentMethodOptions', 'editingRequestId', 'countdownTimerId', 'reportCaptureLocation',
    'skipParkingReloadUntil', 'loadParkingsSeq', 'locationSyncTimerId', 'geoRequestInFlight',
    'geoPermissionState', 'geoWatchId', 'geoWatchdogId', 'mapPickMode', 'refreshInFlight',
    'dashboardChromeObserver'
];

const constNames = [
    'API_BASE', 'VIEW_RADIUS_OPTIONS', 'DEFAULT_VIEW_RADIUS', 'VIEW_RADIUS_KEY',
    'VIRTUAL_ZONE_CELL_DEGREES', 'OWN_REPORT_COORDS_KEY', 'MSG_READ_KEY', 'USER_STORAGE_KEY',
    'ACCESS_TOKEN_KEY', 'REFRESH_TOKEN_KEY', 'ACCESS_EXPIRES_KEY', 'CHAT_LOCATION_PREFIX',
    'CHAT_PHOTO_PREFIX', 'SIDEBAR_COLLAPSED_KEY'
];

const modalNames = [
    'parkingDetailModal', 'reportModal', 'searchModal', 'chatModal',
    'suspendedModal', 'profileModal', 'confirmDeleteModal'
];

function transformChunk(code) {
    let out = code;
    // Rename local shadowing map in markThreadRead
    out = out.replace(
        'const map = getMsgReadMap();',
        'const readMap = getMsgReadMap();'
    );
    out = out.replace(/\bmap\[key\]/g, 'readMap[key]');
    out = out.replace(/localStorage\.setItem\(MSG_READ_KEY, JSON\.stringify\(map\)\)/g,
        'localStorage.setItem(MSG_READ_KEY, JSON.stringify(readMap))');

    for (const v of stateVars) {
        const re = new RegExp(`(?<![.\\w])${v}(?![\\w])`, 'g');
        out = out.replace(re, `state.${v}`);
    }
    for (const c of constNames) {
        const re = new RegExp(`(?<![.\\w])${c}(?![\\w])`, 'g');
        out = out.replace(re, `C.${c}`);
    }
    for (const m of modalNames) {
        const re = new RegExp(`(?<![.\\w])${m}(?![\\w])`, 'g');
        out = out.replace(re, `modals.${m}`);
    }
    return out;
}

function extractFunctionNames(code) {
    const names = new Set();
    for (const m of code.matchAll(/(?:async\s+)?function\s+([A-Za-z_$][\w$]*)\s*\(/g)) names.add(m[1]);
    return [...names];
}

const allFunctions = new Set();
for (const [, chunk] of chunks) {
    extractFunctionNames(chunk).forEach(n => allFunctions.add(n));
}

function hubifyCalls(code, localFns) {
    let out = code;
    const sorted = [...allFunctions].sort((a, b) => b.length - a.length);
    for (const fn of sorted) {
        if (localFns.has(fn)) continue;
        if (['parseInt', 'parseFloat', 'String', 'Number', 'Math', 'JSON', 'Object', 'Array', 'Date', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'window', 'document', 'navigator', 'sessionStorage', 'localStorage', 'fetch', 'Promise', 'Error', 'FormData', 'FileReader', 'ResizeObserver', 'URLSearchParams'].includes(fn)) continue;
        const re = new RegExp(`(?<![.\\w])${fn}\\s*\\(`, 'g');
        out = out.replace(re, `hub.${fn}(`);
    }
    return out;
}

function stripLeadingIndent(code) {
    return code.split('\n').map(line => line.startsWith('    ') ? line.slice(4) : line).join('\n');
}

function buildModule(file, rawCode) {
    const localFns = new Set(extractFunctionNames(rawCode));
    let code = stripLeadingIndent(transformChunk(rawCode));
    code = hubifyCalls(code, localFns);

    const exports = [...localFns];
    const exportBlock = exports.length
        ? `\nexport function register${path.basename(file, '.js').replace(/(^|-)([a-z])/g, (_, __, c) => c.toUpperCase())}(hub, state, modals, C, t) {\n${code}\n${exports.map(f => `  hub.${f} = ${f};`).join('\n')}\n}\n`
        : '';

    const header = `import { C } from '../core/constants.js';\n`;

    if (file.startsWith('core/')) {
        if (file === 'core/api.js') {
            return `${header}import { state } from './state.js';\nimport { modals } from './modals.js';\n\nexport function registerApi(hub, t, translateError, cultureHeaders) {\n${code}\n  Object.assign(hub, { apiFetch, apiPostForm, apiPost, apiPut, apiGet, apiDelete, getAccessToken, getRefreshToken, isAccessExpired, saveAuthTokens, clearAuthTokens, authHeaders, tryRefreshToken });\n}\n`;
        }
        if (file === 'core/dom.js') {
            return `export function registerDom(hub) {\n${code}\n  Object.assign(hub, { showToast, escapeHtml, escapeAttr, prop });\n}\n`;
        }
    }

    return `${header}import { state } from '../core/state.js';\nimport { modals } from '../core/modals.js';\n\nexport function register${toRegisterName(file)}(hub, t) {\n${code}\n${exports.map(f => `  hub.${f} = ${f};`).join('\n')}\n}\n`;
}

function toRegisterName(file) {
    const base = file.replace(/^features\//, '').replace(/^core\//, '').replace(/\.js$/, '');
    return base.split(/[-]/).map(s => s[0].toUpperCase() + s.slice(1)).join('');
}

// Write core files
fs.mkdirSync(path.join(root, 'core'), { recursive: true });
fs.mkdirSync(path.join(root, 'features'), { recursive: true });
fs.mkdirSync(path.join(root, 'app'), { recursive: true });

fs.writeFileSync(path.join(root, 'core', 'constants.js'), `'use strict';

export const C = {
    API_BASE: '',
    VIEW_RADIUS_OPTIONS: [500, 1000, 2000, 3000, 5000],
    DEFAULT_VIEW_RADIUS: 2000,
    VIEW_RADIUS_KEY: 'spotsterViewRadius',
    VIRTUAL_ZONE_CELL_DEGREES: 0.00018,
    OWN_REPORT_COORDS_KEY: 'spotsterOwnReportCoords',
    MSG_READ_KEY: 'spotsterMsgRead',
    USER_STORAGE_KEY: 'spotsterUser',
    ACCESS_TOKEN_KEY: 'spotsterAccessToken',
    REFRESH_TOKEN_KEY: 'spotsterRefreshToken',
    ACCESS_EXPIRES_KEY: 'spotsterAccessExpires',
    CHAT_LOCATION_PREFIX: '__SPOTLOC__:',
    CHAT_PHOTO_PREFIX: '__SPOTPHOTO__:',
    SIDEBAR_COLLAPSED_KEY: 'spotsterSidebarCollapsed'
};
`);

fs.writeFileSync(path.join(root, 'core', 'state.js'), `'use strict';

import { C } from './constants.js';

export const state = {
    viewRadiusMeters: C.DEFAULT_VIEW_RADIUS,
    currentUser: null,
    map: null,
    mapTileLayer: null,
    markers: {},
    parkings: [],
    parkingRequests: [],
    selectedParkingId: null,
    userLocation: null,
    mapLimitCircle: null,
    userPositionMarker: null,
    chatLocationLayerGroup: null,
    activeChatLocateKey: null,
    requestLayers: {},
    requestLayerGroup: null,
    parkingLayerGroup: null,
    searchPreviewLayer: null,
    searchPreviewLayerGroup: null,
    loadRequestsSeq: 0,
    geocodedLocation: null,
    addressSuggestTimer: null,
    addressSuggestions: [],
    activeSuggestionIndex: -1,
    activeSheet: 'parkings',
    requestsDealFilter: 'all',
    connection: null,
    selectedPhotoFile: null,
    pendingDeleteListing: null,
    profilePhotoVersion: 0,
    profileTargetUserId: null,
    profileTargetUsername: '',
    profileTargetPhotoUrl: null,
    profileSelectedRating: 0,
    profileActiveTab: 'ratings',
    profileSearchTimer: null,
    activeChatRequestId: null,
    activeChatIsMine: false,
    activeChatGuestUserId: null,
    ownerConversationCache: {},
    paymentMethodOptions: [],
    editingRequestId: null,
    countdownTimerId: null,
    reportCaptureLocation: null,
    skipParkingReloadUntil: 0,
    loadParkingsSeq: 0,
    locationSyncTimerId: null,
    geoRequestInFlight: false,
    geoPermissionState: 'unknown',
    geoWatchId: null,
    geoWatchdogId: null,
    mapPickMode: false,
    refreshInFlight: null,
    dashboardChromeObserver: null
};
`);

fs.writeFileSync(path.join(root, 'core', 'modals.js'), `'use strict';

export const modals = {
    parkingDetailModal: null,
    reportModal: null,
    searchModal: null,
    chatModal: null,
    suspendedModal: null,
    profileModal: null,
    confirmDeleteModal: null
};

export function initModals() {
    modals.parkingDetailModal = new bootstrap.Modal('#parking-detail-modal');
    modals.reportModal = new bootstrap.Modal('#report-modal');
    modals.searchModal = new bootstrap.Modal('#search-modal');
    modals.chatModal = new bootstrap.Modal('#chat-modal');
    modals.suspendedModal = new bootstrap.Modal('#suspended-modal');
    modals.profileModal = new bootstrap.Modal('#profile-modal');
    modals.confirmDeleteModal = new bootstrap.Modal('#confirm-delete-modal');
}
`);

fs.writeFileSync(path.join(root, 'core', 'i18n-bridge.js'), `'use strict';

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
`);

for (const [file, chunk] of chunks) {
    if (file === 'app/bootstrap.js') continue;
    const outPath = path.join(root, file);
    fs.mkdirSync(path.dirname(outPath), { recursive: true });
    fs.writeFileSync(outPath, buildModule(file, chunk));
    console.log('Wrote', file);
}

// bootstrap
const bootstrapRaw = chunks.get('app/bootstrap.js');
let bootstrap = stripLeadingIndent(transformChunk(bootstrapRaw));
bootstrap = hubifyCalls(bootstrap, new Set());

fs.writeFileSync(path.join(root, 'app', 'bootstrap.js'), `import { state } from '../core/state.js';\nimport { modals } from '../core/modals.js';\nimport { C } from '../core/constants.js';\n\nexport function wireEvents(hub, t) {\n${bootstrap}\n}\n`);

console.log('Done. Review generated files.');
