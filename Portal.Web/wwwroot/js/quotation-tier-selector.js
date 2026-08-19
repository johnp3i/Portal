/* ==========================================================================
   Quotation Tier Selector — Fetches and displays price tiers when a product
   is selected from the autocomplete, updates UnitPrice/CostPrice on change.
   ========================================================================== */
(function () {
    'use strict';

    var tierRow = null;
    var tierSelect = null;
    var tierHiddenInput = null;
    var currentTiers = [];
    var currentCurrencySymbol = '\u20ac';

    document.addEventListener('DOMContentLoaded', function () {
        tierRow = document.getElementById('priceTierRow');
        tierSelect = document.getElementById('lineModalPriceTier');
        tierHiddenInput = document.getElementById('selectedPriceTierId');

        if (tierSelect) {
            tierSelect.addEventListener('change', onTierChange);
        }
    });

    /**
     * Called when a product is selected from the catalog autocomplete.
     * Fetches tiers for the product and shows/hides the tier dropdown accordingly.
     * @param {string|null} productCode - The product code from the autocomplete selection.
     */
    window.fetchProductTiers = async function (productCode, description) {
        // Reset tier state
        hideTierSelector();
        currentTiers = [];

        // Build the query using productCode when available, otherwise fall back to description.
        var query = '';
        if (productCode) {
            query = 'productCode=' + encodeURIComponent(productCode);
        } else if (description) {
            query = 'description=' + encodeURIComponent(description);
        } else {
            return;
        }

        try {
            var response = await fetch('/Quotation/AxGetProductTiersForSelection?' + query);
            if (!response.ok) return;

            var result = await response.json();
            if (!result.success || !result.data || !result.data.hasTiers) return;

            var data = result.data;
            currentTiers = data.tiers || [];
            currentCurrencySymbol = data.currencySymbol || '\u20ac';

            if (currentTiers.length === 0) return;

            // Populate the dropdown
            populateTierDropdown(currentTiers, data.defaultTierId, data.currencySymbol);

            // Show the tier row
            if (tierRow) {
                tierRow.style.display = '';
            }

            // Pre-select the default tier and update prices
            if (data.defaultTierId) {
                tierSelect.value = data.defaultTierId.toString();
                applyTierPrices(data.defaultTierId);
            }
        } catch (e) {
            // Silently fail — tier selection is supplementary to product selection
        }
    };

    /**
     * Populates the tier dropdown with options.
     */
    function populateTierDropdown(tiers, defaultTierId, currencySymbol) {
        if (!tierSelect) return;

        tierSelect.innerHTML = '';

        tiers.forEach(function (tier) {
            var option = document.createElement('option');
            option.value = tier.id.toString();
            option.textContent = tier.tierName + ' \u2014 ' + currencySymbol + formatPrice(tier.sellingPrice);
            if (tier.id === defaultTierId) {
                option.selected = true;
            }
            tierSelect.appendChild(option);
        });
    }

    /**
     * Handles tier dropdown change — updates UnitPrice and CostPrice fields.
     */
    function onTierChange() {
        var selectedTierId = parseInt(tierSelect.value);
        if (isNaN(selectedTierId)) {
            if (tierHiddenInput) tierHiddenInput.value = '';
            return;
        }
        applyTierPrices(selectedTierId);
    }

    /**
     * Applies the selected tier's prices to the form fields.
     */
    function applyTierPrices(tierId) {
        var tier = currentTiers.find(function (t) { return t.id === tierId; });
        if (!tier) return;

        var unitPriceInput = document.getElementById('lineModalUnitPrice');
        var costPriceInput = document.getElementById('lineModalCostPrice');

        if (unitPriceInput) {
            unitPriceInput.value = tier.sellingPrice.toFixed(2);
        }
        if (costPriceInput) {
            costPriceInput.value = tier.costPrice > 0 ? tier.costPrice.toFixed(2) : '';
        }

        // Update hidden input with selected tier ID
        if (tierHiddenInput) {
            tierHiddenInput.value = tierId.toString();
        }

        // Update the price-highlight box to mirror the selected tier's prices
        updatePriceHighlight(tier);
    }

    /**
     * Populates and shows the green price-highlight box below the tier dropdown.
     */
    function updatePriceHighlight(tier) {
        var box = document.getElementById('tierPriceHighlight');
        var unitEl = document.getElementById('tierPriceHighlightUnit');
        var costEl = document.getElementById('tierPriceHighlightCost');
        if (!box || !unitEl || !costEl) return;

        unitEl.textContent = currentCurrencySymbol + parseFloat(tier.sellingPrice).toFixed(2);
        costEl.textContent = tier.costPrice > 0 ? currentCurrencySymbol + parseFloat(tier.costPrice).toFixed(2) : '\u2014';
        box.style.display = 'flex';
    }

    /**
     * Hides the tier selector and resets its state.
     */
    function hideTierSelector() {
        if (tierRow) {
            tierRow.style.display = 'none';
        }
        if (tierSelect) {
            tierSelect.innerHTML = '<option value="">\u2014 Select Tier \u2014</option>';
        }
        if (tierHiddenInput) {
            tierHiddenInput.value = '';
        }
        var box = document.getElementById('tierPriceHighlight');
        if (box) box.style.display = 'none';
        currentTiers = [];
    }

    /**
     * Resets the tier selector — called when the modal is opened for Add or Edit.
     */
    window.resetTierSelector = function () {
        hideTierSelector();
    };

    /**
     * Formats a numeric price value with 2 decimal places.
     */
    function formatPrice(value) {
        if (typeof value !== 'number') return '0.00';
        return value.toFixed(2);
    }

})();
