/* ==========================================================================
   Invoice Tier Selector — Fetches and displays price tiers when a product
   is selected from the autocomplete, updates UnitPrice/CostPrice on change.
   ========================================================================== */
(function () {
    'use strict';

    var tierRow = null;
    var tierSelect = null;
    var tierHiddenInput = null;
    var currentTiers = [];

    document.addEventListener('DOMContentLoaded', function () {
        tierRow = document.getElementById('invoiceLineTierSelectorRow');
        tierSelect = document.getElementById('invoiceLineModalPriceTier');
        tierHiddenInput = document.getElementById('invoiceLineModalProductPriceTierId');

        if (tierSelect) {
            tierSelect.addEventListener('change', onTierChange);
        }
    });

    /**
     * Called when a product is selected from the catalog/product autocomplete.
     * Fetches tiers for the product and shows/hides the tier dropdown accordingly.
     * @param {string|null} productCode - The product code from the autocomplete selection.
     */
    window.fetchInvoiceProductTiers = async function (productCode) {
        // Reset tier state
        hideTierSelector();
        currentTiers = [];

        if (!productCode) return;

        try {
            var response = await fetch('/Invoice/AxGetProductTiersForSelection?productCode=' + encodeURIComponent(productCode));
            if (!response.ok) return;

            var result = await response.json();
            if (!result.success || !result.data || !result.data.hasTiers) return;

            var data = result.data;
            currentTiers = data.tiers || [];

            if (currentTiers.length === 0) return;

            // Populate the dropdown
            populateTierDropdown(currentTiers, data.defaultTierId, data.currencySymbol);

            // Show the tier row
            if (tierRow) {
                tierRow.style.display = 'block';
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
            option.dataset.sellingPrice = tier.sellingPrice;
            option.dataset.costPrice = tier.costPrice;
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

        var unitPriceInput = document.getElementById('invoiceLineModalUnitPrice');
        var costPriceInput = document.getElementById('invoiceLineModalCostPrice');

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
    }

    /**
     * Hides the tier selector and resets its state.
     */
    function hideTierSelector() {
        if (tierRow) {
            tierRow.style.display = 'none';
        }
        if (tierSelect) {
            tierSelect.innerHTML = '';
        }
        if (tierHiddenInput) {
            tierHiddenInput.value = '';
        }
        currentTiers = [];
    }

    /**
     * Resets the invoice tier selector — called when the modal is opened for Add or Edit.
     */
    window.resetInvoiceTierSelector = function () {
        hideTierSelector();
    };

    /**
     * Hides the invoice tier selector — alias for external use.
     */
    window.hideInvoiceTierSelector = function () {
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
