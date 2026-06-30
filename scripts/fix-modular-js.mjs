import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'wwwroot', 'js');

const fixes = [
    [/#state\.map-/g, '#map-'],
    [/request-state\.map-popup/g, 'request-map-popup'],
    [/dashboard-state\.map-active/g, 'dashboard-map-active'],
    [/--state\.map-chrome-left/g, '--map-chrome-left'],
    [/state\.map-popup-action/g, 'map-popup-action'],
    [/\.\.\.currentUser\b/g, '...state.currentUser'],
    [/\brefreshMapView\(\)/g, 'hub.refreshMapView()'],
    [/\bupdateAccountStatusUI\(\)/g, 'hub.updateAccountStatusUI()'],
    [/\bsyncDashboardChromeTop\(\)/g, 'hub.syncDashboardChromeTop()'],
    [/\brefreshUserProfile\(\)/g, 'hub.refreshUserProfile()'],
];

function walk(dir) {
    for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
        const p = path.join(dir, ent.name);
        if (ent.isDirectory()) walk(p);
        else if (ent.name.endsWith('.js') && ent.name !== 'app.js') {
            let text = fs.readFileSync(p, 'utf8');
            let changed = false;
            for (const [re, rep] of fixes) {
                if (re.test(text)) {
                    text = text.replace(re, rep);
                    changed = true;
                }
                re.lastIndex = 0;
            }
            if (changed) fs.writeFileSync(p, text);
        }
    }
}

walk(root);
console.log('Fixed modular JS files');
