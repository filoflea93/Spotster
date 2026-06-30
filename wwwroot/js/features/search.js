import { C } from '../core/constants.js';
import { state } from '../core/state.js';
import { modals } from '../core/modals.js';
import { mapEngine } from '../core/map-engine.js';

export function registerSearch(hub, t) {
// --- Search parking ---
$('#btn-geocode').on('click', geocodeAddress);

$('#search-address').on('input', function () {
    state.geocodedLocation = null;
    $('#geocode-result').addClass('d-none');
    hub.clearSearchPreviewLayer();
    scheduleAddressSuggestions($(this).val());
});

$('#search-address').on('keydown', function (e) {
    const $list = $('#address-suggestions');
    if ($list.hasClass('d-none') || state.addressSuggestions.length === 0) {
        if (e.key === 'Enter') {
            e.preventDefault();
            geocodeAddress();
        }
        return;
    }

    if (e.key === 'ArrowDown') {
        e.preventDefault();
        state.activeSuggestionIndex = Math.min(state.activeSuggestionIndex + 1, state.addressSuggestions.length - 1);
        highlightSuggestion(state.activeSuggestionIndex);
    } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        state.activeSuggestionIndex = Math.max(state.activeSuggestionIndex - 1, 0);
        highlightSuggestion(state.activeSuggestionIndex);
    } else if (e.key === 'Enter') {
        e.preventDefault();
        if (state.activeSuggestionIndex >= 0) {
            selectAddressSuggestion(state.addressSuggestions[state.activeSuggestionIndex]);
        } else {
            geocodeAddress();
        }
    } else if (e.key === 'Escape') {
        hideAddressSuggestions();
    }
});

$('#search-address').on('blur', function () {
    setTimeout(hideAddressSuggestions, 200);
});

$('#search-address').on('focus', function () {
    if ($(this).val().trim().length >= 3 && state.addressSuggestions.length > 0) {
        showAddressSuggestions(state.addressSuggestions);
    }
});

function scheduleAddressSuggestions(query) {
    clearTimeout(state.addressSuggestTimer);
    query = (query || '').trim();
    if (query.length < 3) {
        hideAddressSuggestions();
        return;
    }

    state.addressSuggestTimer = setTimeout(() => fetchAddressSuggestions(query), 350);
}

async function fetchAddressSuggestions(query) {
    try {
        let url = `/api/parking/geocode/suggest?q=${encodeURIComponent(query)}`;
        if (state.userLocation) {
            url += `&lat=${state.userLocation.lat}&lng=${state.userLocation.lng}`;
        }
        const items = await hub.apiGet(url);
        state.addressSuggestions = items || [];
        state.activeSuggestionIndex = -1;
        showAddressSuggestions(state.addressSuggestions);
    } catch {
        hideAddressSuggestions();
    }
}

function showAddressSuggestions(items) {
    const $list = $('#address-suggestions').empty();
    if (!items || items.length === 0) {
        $list.html(`<div class="address-suggestions-empty">${t('Suggest_NoResults')}</div>`).removeClass('d-none');
        $('#search-address').attr('aria-expanded', 'true');
        return;
    }

    items.forEach((item, index) => {
        const $btn = $(`
            <button type="button" class="address-suggestion-item" data-index="${index}">
                <i class="bi bi-geo-alt-fill"></i>
                <div>
                    <div class="label">${hub.escapeHtml(item.shortLabel)}</div>
                    <div class="detail">${hub.escapeHtml(item.formattedAddress)}</div>
                </div>
            </button>
        `);
        $btn.on('mousedown', function (e) {
            e.preventDefault();
            selectAddressSuggestion(items[index]);
        });
        $list.append($btn);
    });

    $list.removeClass('d-none');
    $('#search-address').attr('aria-expanded', 'true');
}

function highlightSuggestion(index) {
    const $items = $('#address-suggestions .address-suggestion-item');
    $items.removeClass('active');
    if (index >= 0 && index < $items.length) {
        $items.eq(index).addClass('active');
        $items.eq(index)[0].scrollIntoView({ block: 'nearest' });
    }
}

function hideAddressSuggestions() {
    $('#address-suggestions').addClass('d-none').empty();
    $('#search-address').attr('aria-expanded', 'false');
    state.activeSuggestionIndex = -1;
}

function selectAddressSuggestion(item) {
    $('#search-address').val(item.shortLabel);
    hideAddressSuggestions();
    applyGeocodedLocation(item.latitude, item.longitude, item.formattedAddress);
}

function applyGeocodedLocation(lat, lng, address) {
    state.geocodedLocation = { lat, lng, address };
    $('#geocode-address-text').text(address);
    $('#geocode-result').removeClass('d-none');
    hub.clearSearchPreviewLayer();

    if (hub.isWithinViewRange(lat, lng) && mapEngine.isInitialized()) {
        const radius = parseInt($('#search-radius').val(), 10);
        state.searchPreviewLayer = mapEngine.addCircle(lat, lng, {
            radius,
            color: '#3B7DD8',
            weight: 2,
            fillColor: '#3B7DD8',
            fillOpacity: 0.15,
            dashArray: '6 8',
            className: 'search-preview-circle'
        }, { group: 'searchPreview' });
        mapEngine.setView(lat, lng, Math.max(mapEngine.getZoom(), 15));
    }
}

async function geocodeAddress() {
    const address = $('#search-address').val().trim();
    if (!address) {
        hub.showToast(t('Toast_EnterAddress'));
        return;
    }

    hideAddressSuggestions();
    const $btn = $('#btn-geocode').prop('disabled', true);
    try {
        const res = await hub.apiGet(`/api/parking/geocode?address=${encodeURIComponent(address)}`);
        applyGeocodedLocation(res.latitude, res.longitude, res.formattedAddress);
    } catch (err) {
        hub.showToast(err.message);
    } finally {
        $btn.prop('disabled', false);
    }
}

$('#search-radius').on('change', function () {
    if (state.geocodedLocation && mapEngine.isInitialized()) {
        hub.clearSearchPreviewLayer();
        state.searchPreviewLayer = mapEngine.addCircle(
            state.geocodedLocation.lat,
            state.geocodedLocation.lng,
            {
                radius: parseInt($(this).val(), 10),
                color: '#3B7DD8',
                weight: 2,
                fillColor: '#3B7DD8',
                fillOpacity: 0.15,
                dashArray: '6 8',
                className: 'search-preview-circle'
            },
            { group: 'searchPreview' }
        );
    }
});

function resetSearchForm() {
    state.editingRequestId = null;
    state.geocodedLocation = null;
    state.addressSuggestions = [];
    state.activeSuggestionIndex = -1;
    clearTimeout(state.addressSuggestTimer);
    $('#search-address').val('');
    $('#search-radius').val('500');
    $('#search-reward').val('');
    $('.payment-method-cb').prop('checked', false);
    $('#payment-other-text').val('');
    $('#payment-other-wrap').addClass('d-none');
    $('#geocode-result').addClass('d-none');
    hideAddressSuggestions();
    hub.clearSearchPreviewLayer();
    hub.setSearchModalMode('create');
}

$('#search-modal').on('hidden.bs.modal', function () {
    hub.clearSearchPreviewLayer();
    state.editingRequestId = null;
    hub.setSearchModalMode('create');
});

$('#btn-confirm-search').on('click', async function () {
    const address = $('#search-address').val().trim();
    if (!address && !state.geocodedLocation) {
        hub.showToast(t('Toast_GeocodeFirst'));
        return;
    }

    const $btn = $(this).prop('disabled', true);
    try {
        if (!state.geocodedLocation) {
            await geocodeAddress();
            if (!state.geocodedLocation) return;
        }

        const rewardVal = parseFloat($('#search-reward').val());
        const rewardAmount = isNaN(rewardVal) || rewardVal <= 0 ? null : rewardVal;
        const paymentMethods = hub.getSelectedPaymentMethods();

        if (rewardAmount && paymentMethods.length === 0) {
            hub.showToast(t('Toast_RewardNeedsPayment'));
            return;
        }

        if ($('.payment-method-cb[value="other"]').is(':checked') && !$('#payment-other-text').val().trim()) {
            hub.showToast(t('Toast_OtherPaymentRequired'));
            return;
        }

        const payload = {
            address: state.geocodedLocation.address || address,
            radiusMeters: parseInt($('#search-radius').val(), 10),
            rewardAmount: rewardAmount,
            paymentMethods: paymentMethods
        };

        let result;
        if (state.editingRequestId) {
            result = await hub.apiPut(`/api/parking/requests/${state.editingRequestId}`, payload);
            hub.showToast(t('Toast_RequestUpdated'));
        } else {
            result = await hub.apiPost('/api/parking/request', payload);
            hub.showToast(t('Toast_RequestPublished'));
        }

        hub.upsertRequest(result);
        hub.clearSearchPreviewLayer();
        modals.searchModal.hide();
        hub.switchListingsSheet('requests');
        hub.renderRequests();
        hub.focusMapOnRequest(result);
        hub.loadRequests();
    } catch (err) {
        hub.showToast(err.message);
    } finally {
        $btn.prop('disabled', false);
    }
});


  hub.scheduleAddressSuggestions = scheduleAddressSuggestions;
  hub.fetchAddressSuggestions = fetchAddressSuggestions;
  hub.showAddressSuggestions = showAddressSuggestions;
  hub.highlightSuggestion = highlightSuggestion;
  hub.hideAddressSuggestions = hideAddressSuggestions;
  hub.selectAddressSuggestion = selectAddressSuggestion;
  hub.applyGeocodedLocation = applyGeocodedLocation;
  hub.geocodeAddress = geocodeAddress;
  hub.resetSearchForm = resetSearchForm;
}
