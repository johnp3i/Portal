/* ==========================================================================
   Invoice Line Modal — Modal open/close, population, and submission
   ========================================================================== */
(function () {
    'use strict';

    var currentMode = null; // 'add' or 'edit'
    var currentLineId = null;
    var currentInvoiceId = null;

    // Initialize on DOMContentLoaded
    document.addEventListener('DOMContentLoaded', function () {
        // Get invoiceId from page (set by Edit.cshtml as global `invoiceId`)
        currentInvoiceId = window.invoiceId;

        // Overlay click to close
        var overlay = document.getElementById('invoiceLineItemModal');
        if (overlay) {
            overlay.addEventListener('click', function (e) {
                if (e.target === this) {
                    hideInvoiceLineItemModal();
                }
            });
        }

        // Escape key handler to close modal
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                var modal = document.getElementById('invoiceLineItemModal');
                if (modal && modal.style.display !== 'none') {
                    hideInvoiceLineItemModal();
                }
            }
        });

        // Reverse charge toggle handler
        var reverseChargeCheckbox = document.getElementById('invoiceLineModalReverseCharge');
        if (reverseChargeCheckbox) {
            reverseChargeCheckbox.addEventListener('change', function () {
                var vatInput = document.getElementById('invoiceLineModalVatRate');
                if (this.checked) {
                    vatInput.dataset.previousVatRate = vatInput.value;
                    vatInput.value = '0';
                    vatInput.readOnly = true;
                    vatInput.style.opacity = '0.6';
                } else {
                    vatInput.value = vatInput.dataset.previousVatRate || '';
                    vatInput.readOnly = false;
                    vatInput.style.opacity = '1';
                }
            });
        }

        // Submit button handler
        var submitBtn = document.getElementById('invoiceLineModalSubmitBtn');
        if (submitBtn) {
            submitBtn.addEventListener('click', submitInvoiceLineItemForm);
        }

        // Clear description validation on input
        var descriptionInput = document.getElementById('invoiceLineModalDescription');
        if (descriptionInput) {
            descriptionInput.addEventListener('input', function () {
                var existing = descriptionInput.parentElement.querySelector('.field-validation-error');
                if (existing) {
                    existing.remove();
                }
            });
        }

        // Clear quantity validation on input
        var quantityInput = document.getElementById('invoiceLineModalQuantity');
        if (quantityInput) {
            quantityInput.addEventListener('input', function () {
                var existing = quantityInput.parentElement.querySelector('.field-validation-error');
                if (existing) {
                    existing.remove();
                }
            });
        }
    });

    /**
     * Opens the line item modal in Edit mode, pre-populated from the table row data attributes.
     * @param {string|number} lineId - The ID of the line item to edit.
     */
    window.showEditInvoiceLineModal = function (lineId) {
        var row = document.querySelector('tr[data-line-id="' + lineId + '"]');
        if (!row) return;

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

        // Populate form fields
        document.getElementById('invoiceLineModalLineId').value = lineId;
        document.getElementById('invoiceLineModalInvoiceId').value = currentInvoiceId || '';
        document.getElementById('invoiceLineModalProductCode').value = productCode;
        document.getElementById('invoiceLineModalDescription').value = description;
        document.getElementById('invoiceLineModalSubtitleField').value = subtitle;
        document.getElementById('invoiceLineModalReferenceUrl').value = referenceUrl;
        document.getElementById('invoiceLineModalQuantity').value = quantity;
        document.getElementById('invoiceLineModalUnitPrice').value = unitPrice;
        document.getElementById('invoiceLineModalVatRate').value = vatRate;
        document.getElementById('invoiceLineModalCostPrice').value = costPrice;
        document.getElementById('invoiceLineModalDiscount').value = discount;
        document.getElementById('invoiceLineModalDiscountType').value = discountType;

        // Handle Reverse Charge state
        var reverseChargeCheckbox = document.getElementById('invoiceLineModalReverseCharge');
        var vatInput = document.getElementById('invoiceLineModalVatRate');
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

        // Collapse Advanced section when opening
        var advancedContent = document.getElementById('invoiceLineAdvancedContent');
        if (advancedContent) {
            advancedContent.style.display = 'none';
        }
        var toggleBtn = document.querySelector('.advanced-toggle');
        if (toggleBtn) {
            toggleBtn.classList.remove('expanded');
        }

        // Set modal title and subtitle
        document.getElementById('invoiceLineModalTitle').textContent = 'Edit Line Item';
        document.getElementById('invoiceLineModalSubtitle').textContent = 'Update the details for this line item.';

        // Set submit button to "Save Changes" with primary style
        var submitBtn = document.getElementById('invoiceLineModalSubmitBtn');
        submitBtn.textContent = 'Save Changes';
        submitBtn.className = 'btn btn-primary';

        // Store mode and line ID
        currentMode = 'edit';
        currentLineId = lineId;

        // Show modal
        document.getElementById('invoiceLineItemModal').style.display = 'flex';
    };

    /**
     * Opens the line item modal in Add mode with default values.
     */
    window.showAddInvoiceLineModal = function () {
        // Reset/clear all form fields
        var form = document.getElementById('invoiceLineItemForm');
        if (form) {
            form.reset();
        }

        // Set hidden fields
        document.getElementById('invoiceLineModalLineId').value = '';
        document.getElementById('invoiceLineModalInvoiceId').value = currentInvoiceId || '';
        document.getElementById('invoiceLineModalProductCode').value = '';

        // Clear text/number inputs explicitly (in case reset doesn't clear all)
        document.getElementById('invoiceLineModalDescription').value = '';
        document.getElementById('invoiceLineModalSubtitleField').value = '';
        document.getElementById('invoiceLineModalReferenceUrl').value = '';
        document.getElementById('invoiceLineModalCostPrice').value = '';

        // Pre-fill defaults: Quantity=1, Discount=0, DiscountType=Percentage, VAT% empty
        document.getElementById('invoiceLineModalQuantity').value = '1';
        document.getElementById('invoiceLineModalUnitPrice').value = '';
        document.getElementById('invoiceLineModalVatRate').value = '';
        document.getElementById('invoiceLineModalDiscount').value = '0';
        document.getElementById('invoiceLineModalDiscountType').value = 'Percentage';

        // Ensure Reverse Charge is unchecked and VAT is editable
        var reverseChargeCheckbox = document.getElementById('invoiceLineModalReverseCharge');
        reverseChargeCheckbox.checked = false;
        var vatInput = document.getElementById('invoiceLineModalVatRate');
        vatInput.readOnly = false;
        vatInput.style.opacity = '1';
        delete vatInput.dataset.previousVatRate;

        // Collapse Advanced section when opening
        var advancedContent = document.getElementById('invoiceLineAdvancedContent');
        if (advancedContent) {
            advancedContent.style.display = 'none';
        }
        var toggleBtn = document.querySelector('.advanced-toggle');
        if (toggleBtn) {
            toggleBtn.classList.remove('expanded');
        }

        // Set modal title and subtitle
        document.getElementById('invoiceLineModalTitle').textContent = 'Add Line Item';
        document.getElementById('invoiceLineModalSubtitle').textContent = 'Add a new line item to this invoice.';

        // Set submit button to "Add Line" with green/success style
        var submitBtn = document.getElementById('invoiceLineModalSubmitBtn');
        submitBtn.textContent = 'Add Line';
        submitBtn.className = 'btn btn-success';

        // Store mode
        currentMode = 'add';
        currentLineId = null;

        // Show modal
        document.getElementById('invoiceLineItemModal').style.display = 'flex';
    };

    /**
     * Hides the line item modal and resets form state.
     */
    window.hideInvoiceLineItemModal = function () {
        // Hide modal overlay
        document.getElementById('invoiceLineItemModal').style.display = 'none';

        // Reset form fields
        var form = document.getElementById('invoiceLineItemForm');
        if (form) {
            form.reset();
        }

        // Reset VAT field state (in case reverse charge was active)
        var vatInput = document.getElementById('invoiceLineModalVatRate');
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
     * Toggles the Advanced section visibility in the modal.
     */
    window.toggleAdvancedSection = function () {
        var content = document.getElementById('invoiceLineAdvancedContent');
        var toggleBtn = document.querySelector('.advanced-toggle');

        if (content.style.display === 'none') {
            content.style.display = 'block';
            if (toggleBtn) {
                toggleBtn.classList.add('expanded');
            }
        } else {
            content.style.display = 'none';
            if (toggleBtn) {
                toggleBtn.classList.remove('expanded');
            }
        }
    };

    /**
     * Shows a SweetAlert2 confirmation dialog and removes the invoice line item via AJAX.
     * Called from the Remove (×) button in the line item table rows.
     * @param {number} lineId - The line item ID to remove.
     */
    window.confirmRemoveInvoiceLine = async function (lineId) {
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
            var token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            var response = await fetch('/Invoice/RemoveLine', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: 'lineId=' + encodeURIComponent(lineId),
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
                Swal.fire({ icon: 'error', title: 'Error', text: data.message || 'Unable to reach the server.', confirmButtonColor: '#0D5EA6' });
            }
        } catch (error) {
            clearTimeout(timeoutId);
            BlockUI.hide();

            if (error.name === 'AbortError') {
                Swal.fire({ icon: 'error', title: 'Error', text: 'The request timed out. Please try again.', confirmButtonColor: '#0D5EA6' });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: 'Unable to reach the server.', confirmButtonColor: '#0D5EA6' });
            }
        }
    };

    /**
     * Handles form submission for both Add and Edit modes.
     * Gathers form data, validates, determines endpoint, and follows
     * BlockUI → fetch → response → feedback pattern.
     */
    async function submitInvoiceLineItemForm() {
        // Validate Description
        var descriptionInput = document.getElementById('invoiceLineModalDescription');
        var descriptionValue = descriptionInput.value.trim();
        if (!descriptionValue) {
            // Remove any existing error message
            var existingDescErr = descriptionInput.parentElement.querySelector('.field-validation-error');
            if (existingDescErr) existingDescErr.remove();
            // Show inline validation error
            var descError = document.createElement('span');
            descError.className = 'field-validation-error';
            descError.style.color = '#C24A4A';
            descError.style.fontSize = '12px';
            descError.style.display = 'block';
            descError.style.marginTop = '4px';
            descError.textContent = 'Description is required.';
            descriptionInput.parentElement.appendChild(descError);
            descriptionInput.focus();
            return;
        }

        // Validate Quantity
        var quantityInput = document.getElementById('invoiceLineModalQuantity');
        var quantityValue = parseFloat(quantityInput.value);
        if (isNaN(quantityValue) || quantityValue <= 0) {
            // Remove any existing error message
            var existingQtyErr = quantityInput.parentElement.querySelector('.field-validation-error');
            if (existingQtyErr) existingQtyErr.remove();
            // Show inline validation error
            var qtyError = document.createElement('span');
            qtyError.className = 'field-validation-error';
            qtyError.style.color = '#C24A4A';
            qtyError.style.fontSize = '12px';
            qtyError.style.display = 'block';
            qtyError.style.marginTop = '4px';
            qtyError.textContent = 'Quantity must be greater than zero.';
            quantityInput.parentElement.appendChild(qtyError);
            quantityInput.focus();
            return;
        }

        // Build URL-encoded form data
        var data = 'invoiceId=' + encodeURIComponent(document.getElementById('invoiceLineModalInvoiceId').value)
            + '&description=' + encodeURIComponent(document.getElementById('invoiceLineModalDescription').value)
            + '&subtitle=' + encodeURIComponent(document.getElementById('invoiceLineModalSubtitleField').value || '')
            + '&referenceUrl=' + encodeURIComponent(document.getElementById('invoiceLineModalReferenceUrl').value || '')
            + '&quantity=' + encodeURIComponent(document.getElementById('invoiceLineModalQuantity').value)
            + '&unitPrice=' + encodeURIComponent(document.getElementById('invoiceLineModalUnitPrice').value)
            + '&vatRate=' + encodeURIComponent(document.getElementById('invoiceLineModalVatRate').value)
            + '&discount=' + encodeURIComponent(document.getElementById('invoiceLineModalDiscount').value || '0')
            + '&discountType=' + encodeURIComponent(document.getElementById('invoiceLineModalDiscountType').value)
            + '&costPrice=' + encodeURIComponent(document.getElementById('invoiceLineModalCostPrice').value || '')
            + '&productCode=' + encodeURIComponent(document.getElementById('invoiceLineModalProductCode').value || '')
            + '&isReverseCharge=' + document.getElementById('invoiceLineModalReverseCharge').checked;

        // Add lineId for edit mode
        if (currentMode === 'edit') {
            data += '&lineId=' + encodeURIComponent(currentLineId);
        }

        // Determine endpoint based on mode
        var url = currentMode === 'add' ? '/Invoice/AddLine' : '/Invoice/UpdateLine';

        // BlockUI show
        BlockUI.show('Saving...');

        var controller = new AbortController();
        var timeoutId = setTimeout(function () { controller.abort(); }, 30000);

        try {
            var token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            var response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: data,
                signal: controller.signal
            });

            clearTimeout(timeoutId);

            var result;
            try {
                result = await response.json();
            } catch (parseError) {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
                return;
            }

            BlockUI.hide();

            if (result.success) {
                location.reload();
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message || 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
            }
        } catch (error) {
            clearTimeout(timeoutId);
            BlockUI.hide();

            if (error.name === 'AbortError') {
                Swal.fire({ icon: 'error', title: 'Error', text: 'The request timed out. Please try again.', confirmButtonColor: '#0D5EA6' });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
            }
        }
    }

})();
