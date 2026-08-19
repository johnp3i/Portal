/* ==========================================================================
   Quotation Line Modal — Modal open/close, population, and submission
   ========================================================================== */
(function () {
    'use strict';

    var currentMode = null; // 'add' or 'edit'
    var currentLineId = null;
    var currentQuotationId = null;

    // Initialize on DOMContentLoaded
    document.addEventListener('DOMContentLoaded', function () {
        // Get quotationId from page (set by Edit.cshtml as global `quotationId`)
        currentQuotationId = window.quotationId;

        // Note: clicking the overlay does NOT close the modal — the user must use
        // Cancel or Save to avoid accidental data loss while editing a line item.

        // Reverse charge toggle handler
        var reverseChargeCheckbox = document.getElementById('lineModalReverseCharge');
        if (reverseChargeCheckbox) {
            reverseChargeCheckbox.addEventListener('change', function () {
                var vatInput = document.getElementById('lineModalVatRate');
                if (this.checked) {
                    vatInput.dataset.previousVatRate = vatInput.value;
                    vatInput.value = '0';
                    vatInput.readOnly = true;
                    vatInput.style.opacity = '0.6';
                } else {
                    vatInput.value = vatInput.dataset.previousVatRate || '0';
                    vatInput.readOnly = false;
                    vatInput.style.opacity = '1';
                }
            });
        }

        // Submit button handler
        var submitBtn = document.getElementById('lineModalSubmitBtn');
        if (submitBtn) {
            submitBtn.addEventListener('click', submitLineItemForm);
        }

        // Description → tier lookup. Fire as the user types (debounced) and on blur, so the
        // tier selector appears without waiting for the field to lose focus.
        var descInput = document.getElementById('lineModalDescription');
        if (descInput) {
            var descTierDebounce = null;

            var triggerTierLookup = function () {
                // If a product code is already set (autocomplete was used), that flow handles tiers.
                var productCode = document.getElementById('lineModalProductCode').value;
                if (productCode) return;

                var descVal = descInput.value.trim();
                if (descVal && typeof window.fetchProductTiers === 'function') {
                    window.fetchProductTiers(null, descVal);
                } else if (typeof window.resetTierSelector === 'function') {
                    window.resetTierSelector();
                }
            };

            descInput.addEventListener('input', function () {
                if (descTierDebounce) clearTimeout(descTierDebounce);
                descTierDebounce = setTimeout(triggerTierLookup, 400);
            });

            descInput.addEventListener('blur', function () {
                if (descTierDebounce) clearTimeout(descTierDebounce);
                triggerTierLookup();
            });
        }
    });

    /**
     * Opens the line item modal in Edit mode, pre-populated from the table row data attributes.
     * @param {string|number} lineId - The ID of the line item to edit.
     * @param {string|number|null} sectionId - The section the line belongs to.
     */
    window.showEditLineModal = function (lineId, sectionId) {
        var row = document.querySelector('tr[data-line-id="' + lineId + '"]');
        if (!row) return;

        // Reset tier selector before populating
        if (typeof window.resetTierSelector === 'function') {
            window.resetTierSelector();
        }

        // Read data attributes from the table row
        var description = row.getAttribute('data-description') || '';
        var subtitle = row.getAttribute('data-subtitle') || '';
        var referenceUrl = row.getAttribute('data-reference-url') || '';
        var quantity = row.getAttribute('data-quantity') || '';
        var unitPrice = row.getAttribute('data-unit-price') || '';
        var vatRate = row.getAttribute('data-vat-rate') || '';
        var discount = row.getAttribute('data-discount') || '0';
        var discountType = row.getAttribute('data-discount-type') || 'Percentage';
        var costPrice = row.getAttribute('data-cost-price') || '';
        var isReverseCharge = row.getAttribute('data-is-reverse-charge') === 'true';
        var productCode = row.getAttribute('data-product-code') || '';
        var lineSectionId = row.getAttribute('data-section-id') || '';

        // Populate form fields
        document.getElementById('lineModalLineId').value = lineId;
        document.getElementById('lineModalSectionId').value = sectionId || '';
        document.getElementById('lineModalProductCode').value = productCode;
        document.getElementById('lineModalDescription').value = description;
        document.getElementById('lineModalSubtitleField').value = subtitle;
        document.getElementById('lineModalReferenceUrl').value = referenceUrl;
        document.getElementById('lineModalQuantity').value = quantity;
        document.getElementById('lineModalUnitPrice').value = unitPrice;
        document.getElementById('lineModalVatRate').value = vatRate;
        document.getElementById('lineModalCostPrice').value = costPrice;
        document.getElementById('lineModalDiscount').value = discount;
        document.getElementById('lineModalDiscountType').value = discountType;

        // Set Move to Section dropdown to current section
        var moveToSectionSelect = document.getElementById('lineModalMoveToSection');
        if (moveToSectionSelect) {
            moveToSectionSelect.value = lineSectionId || '';
        }

        // Handle Reverse Charge state
        var reverseChargeCheckbox = document.getElementById('lineModalReverseCharge');
        var vatInput = document.getElementById('lineModalVatRate');
        if (isReverseCharge) {
            reverseChargeCheckbox.checked = true;
            vatInput.dataset.previousVatRate = vatRate;
            vatInput.value = '0';
            vatInput.readOnly = true;
            vatInput.style.opacity = '0.6';
        } else {
            reverseChargeCheckbox.checked = false;
            vatInput.readOnly = false;
            vatInput.style.opacity = '1';
        }

        // Set modal title and subtitle
        document.getElementById('lineModalTitle').textContent = 'Edit Line Item';
        document.getElementById('lineModalSubtitle').textContent = 'Update the details for this line item.';

        // Set submit button to "Save Changes" with primary style
        var submitBtn = document.getElementById('lineModalSubmitBtn');
        submitBtn.textContent = 'Save Changes';
        submitBtn.className = 'btn btn-primary';

        // Store mode and line ID
        currentMode = 'edit';
        currentLineId = lineId;

        // Show modal
        document.getElementById('lineItemModal').style.display = 'flex';

        // Fetch current tiers for draft re-selection (always fetches fresh prices)
        if (productCode && typeof window.fetchProductTiers === 'function') {
            window.fetchProductTiers(productCode);
        }
    };

    /**
     * Opens the line item modal in Add mode with default values.
     * @param {string|number|null} sectionId - The section to add the line to.
     */
    window.showAddLineModal = function (sectionId) {
        // Reset/clear all form fields
        var form = document.getElementById('lineItemForm');
        if (form) {
            form.reset();
        }

        // Reset tier selector
        if (typeof window.resetTierSelector === 'function') {
            window.resetTierSelector();
        }

        // Set hidden fields
        document.getElementById('lineModalLineId').value = '';
        document.getElementById('lineModalSectionId').value = sectionId || '';
        document.getElementById('lineModalProductCode').value = '';

        // Clear text/number inputs explicitly (in case reset doesn't clear all)
        document.getElementById('lineModalDescription').value = '';
        document.getElementById('lineModalSubtitleField').value = '';
        document.getElementById('lineModalReferenceUrl').value = '';
        document.getElementById('lineModalCostPrice').value = '';

        // Pre-fill defaults: Quantity=1, Discount=0, VAT%=19 (Cyprus standard rate)
        document.getElementById('lineModalQuantity').value = '1';
        document.getElementById('lineModalUnitPrice').value = '';
        document.getElementById('lineModalVatRate').value = '19';
        document.getElementById('lineModalDiscount').value = '0';
        document.getElementById('lineModalDiscountType').value = 'Percentage';

        // Set Move to Section dropdown
        var moveToSectionSelect = document.getElementById('lineModalMoveToSection');
        if (moveToSectionSelect) {
            moveToSectionSelect.value = sectionId || '';
        }

        // Ensure Reverse Charge is unchecked and VAT is editable
        var reverseChargeCheckbox = document.getElementById('lineModalReverseCharge');
        reverseChargeCheckbox.checked = false;
        var vatInput = document.getElementById('lineModalVatRate');
        vatInput.readOnly = false;
        vatInput.style.opacity = '1';
        delete vatInput.dataset.previousVatRate;

        // Set modal title and subtitle
        document.getElementById('lineModalTitle').textContent = 'Add Line Item';
        document.getElementById('lineModalSubtitle').textContent = 'Add a new item to this section.';

        // Set submit button to "Add Line" with green/success style
        var submitBtn = document.getElementById('lineModalSubmitBtn');
        submitBtn.textContent = 'Add Line';
        submitBtn.className = 'btn btn-success';

        // Store mode
        currentMode = 'add';
        currentLineId = null;

        // Show modal
        document.getElementById('lineItemModal').style.display = 'flex';

        // If a description is already present (e.g. pre-filled), attempt a tier lookup by description
        var descVal = document.getElementById('lineModalDescription').value.trim();
        if (descVal && typeof window.fetchProductTiers === 'function') {
            window.fetchProductTiers(null, descVal);
        }
    };

    /**
     * Hides the line item modal and resets form state.
     */
    window.hideLineItemModal = function () {
        // Hide modal overlay
        document.getElementById('lineItemModal').style.display = 'none';

        // Reset form fields
        var form = document.getElementById('lineItemForm');
        if (form) {
            form.reset();
        }

        // Reset tier selector
        if (typeof window.resetTierSelector === 'function') {
            window.resetTierSelector();
        }

        // Reset VAT field state (in case reverse charge was active)
        var vatInput = document.getElementById('lineModalVatRate');
        if (vatInput) {
            vatInput.readOnly = false;
            vatInput.style.opacity = '1';
            delete vatInput.dataset.previousVatRate;
        }

        // Clear mode state
        currentMode = null;
        currentLineId = null;
    };

    /**
     * Shows a SweetAlert2 confirmation dialog and removes the line item via AJAX.
     * Called from the Remove (×) button in the line item table rows.
     * @param {number} quotationId - The quotation ID.
     * @param {number} lineId - The line item ID to remove.
     */
    window.confirmRemoveLine = async function (quotationId, lineId) {
        var result = await Swal.fire({
            title: 'Remove this line item?',
            text: 'This action cannot be undone.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#C24A4A',
            cancelButtonColor: '#6b7c8d',
            confirmButtonText: 'Yes, remove it',
            cancelButtonText: 'Cancel'
        });

        if (!result.isConfirmed) return;

        BlockUI.show('Removing...');

        var controller = new AbortController();
        var timeoutId = setTimeout(function () { controller.abort(); }, 30000);

        try {
            var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            var formData = new FormData();
            if (tokenInput) {
                formData.append('__RequestVerificationToken', tokenInput.value);
            }

            var response = await fetch('/Quotation/RemoveLine/' + quotationId + '/' + lineId, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: formData,
                signal: controller.signal
            });

            clearTimeout(timeoutId);

            var data;
            try {
                data = await response.json();
            } catch (parseError) {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
                return;
            }

            BlockUI.hide();

            if (data.success) {
                location.reload();
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: data.message || 'Failed to remove line item.', confirmButtonColor: '#0D5EA6' });
            }
        } catch (error) {
            clearTimeout(timeoutId);
            BlockUI.hide();

            if (error.name === 'AbortError') {
                Swal.fire({ icon: 'error', title: 'Error', text: 'The request timed out. Please try again.', confirmButtonColor: '#0D5EA6' });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: 'Unable to reach the server. Check your connection.', confirmButtonColor: '#0D5EA6' });
            }
        }
    };

    /**
     * Handles form submission for both Add and Edit modes.
     * Gathers FormData, determines endpoint, handles move-to-section,
     * and follows BlockUI → fetch → response → feedback pattern.
     */
    async function submitLineItemForm() {
        var form = document.getElementById('lineItemForm');
        var formData = new FormData(form);

        // Determine URL based on mode
        var url;
        if (currentMode === 'add') {
            url = '/Quotation/AddLine/' + currentQuotationId;
        } else {
            url = '/Quotation/UpdateLine/' + currentQuotationId + '/' + currentLineId;
        }

        // Check if section was changed (edit mode only) — move line first
        var originalSectionId = document.getElementById('lineModalSectionId').value;
        var newSectionId = document.getElementById('lineModalMoveToSection').value;
        var sectionChanged = currentMode === 'edit' && originalSectionId !== newSectionId;

        // BlockUI show
        BlockUI.show('Saving...');

        var controller = new AbortController();
        var timeoutId = setTimeout(function () { controller.abort(); }, 30000);

        try {
            // If section changed, move the line first
            if (sectionChanged) {
                var moveResponse = await fetch('/api/sections/move-line', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        lineId: parseInt(currentLineId),
                        targetSectionId: newSectionId ? parseInt(newSectionId) : null
                    }),
                    signal: controller.signal
                });
                var moveData = await moveResponse.json();
                if (!moveData.success) {
                    clearTimeout(timeoutId);
                    BlockUI.hide();
                    Swal.fire({ icon: 'error', title: 'Error', text: moveData.message || 'Failed to move line item.', confirmButtonColor: '#0D5EA6' });
                    return;
                }
            }

            // Submit the line item form
            var response = await fetch(url, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: formData,
                signal: controller.signal
            });

            clearTimeout(timeoutId);

            var data;
            try {
                data = await response.json();
            } catch (parseError) {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
                return;
            }

            BlockUI.hide();

            if (data.success) {
                location.reload();
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: data.message || 'Unable to save line item.', confirmButtonColor: '#0D5EA6' });
            }
        } catch (error) {
            clearTimeout(timeoutId);
            BlockUI.hide();

            if (error.name === 'AbortError') {
                Swal.fire({ icon: 'error', title: 'Error', text: 'The request timed out. Please try again.', confirmButtonColor: '#0D5EA6' });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: 'Unable to reach the server. Check your connection.', confirmButtonColor: '#0D5EA6' });
            }
        }
    }

})();
