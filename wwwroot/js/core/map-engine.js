'use strict';

import { createLeafletMapEngine } from '../adapters/leaflet-adapter.js';

/** Punto di accesso unico alla mappa — swap adapter qui per cambiare libreria. */
export const mapEngine = createLeafletMapEngine();
