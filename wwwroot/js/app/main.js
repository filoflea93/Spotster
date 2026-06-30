import { mapEngine } from '../core/map-engine.js';
import { createT, translateError, cultureHeaders, prop } from '../core/i18n-bridge.js';
import { initModals } from '../core/modals.js';
import { registerDom } from '../core/dom.js';
import { registerApi } from '../core/api.js';
import { registerAuth } from '../features/auth.js';
import { registerMap } from '../features/map.js';
import { registerReport } from '../features/report.js';
import { registerParkings } from '../features/parkings.js';
import { registerRequests } from '../features/requests.js';
import { registerVote } from '../features/vote.js';
import { registerSearch } from '../features/search.js';
import { registerSignalr } from '../features/signalr.js';
import { registerProfile } from '../features/profile.js';
import { registerFab } from '../features/fab.js';
import { wireEvents } from './bootstrap.js';

await SpotsterI18n.init();

const t = createT();
const hub = {
    t,
    prop,
    cultureHeaders,
    translateError: (msg) => translateError(t, msg),
    mapEngine
};

initModals();
registerDom(hub);
registerAuth(hub, t);
registerApi(hub, hub.translateError, cultureHeaders);
registerMap(hub, t);
registerReport(hub, t);
registerParkings(hub, t);
registerRequests(hub, t);
registerVote(hub, t);
registerSearch(hub, t);
registerSignalr(hub, t);
registerProfile(hub, t);
registerFab(hub, t);
wireEvents(hub, t);
