/* ==========================================================================
   Revenue Payment — Payment Modal and Void interactions
   Handles opening/closing the payment modal, form submission with validation,
   and voiding payments with confirmation dialogs.
   ========================================================================== */

/**
 * Gets the antiforgery token from the form on the page.
 */
function getAntiForgeryToken() {
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

/**
 * Opens the payment modal and populates the context bar with invoice details.
 * @param {number} invoiceId - The invoice ID to record payment against
 * @param {string} invoiceNumber - The invoice number for display
 * @param {string} customerName - The customer name for display
 * @param {number} outstandingBalance - The remaining balance on the invoice
 */
function openPaymentModal(invoiceId, invoiceNumber, customerName, outstandingBalance) {
    var modal = document.getElementById('paymentModal');
    if (!modal) return;

    // Show context bar, hide invoice selector
    var contextBar = document.getElementById('paymentModalContext');
    var selectorBar = document.getElementById('paymentModalInvoiceSelector');
    if (contextBar) contextBar.style.display = 'flex';
    if (selectorBar) selectorBar.style.display = 'none';

    // Populate context bar
    var invoiceInfo = document.getElementById('paymentModalInvoiceInfo');
    var remainingBalance = document.getElementById('paymentModalRemainingBalance');

    if (invoiceInfo) {
        invoiceInfo.textContent = invoiceNumber ? (invoiceNumber + ' — ' + customerName) : '—';
    }
    if (remainingBalance) {
        remainingBalance.textContent = outstandingBalance != null ? ('€' + parseFloat(outstandingBalance).toFixed(2)) : '—';
    }

    // Set hidden field and data attribute
    var hiddenField = document.getElementById('paymentInvoiceId');
    if (hiddenField) {
        hiddenField.value = invoiceId || '';
    }

    var form = document.getElementById('paymentForm');
    if (form) {
        form.setAttribute('data-outstanding-balance', outstandingBalance || 0);
        form.reset();
    }

    // Set default payment date to today
    var paymentDateInput = document.getElementById('paymentDate');
    if (paymentDateInput) {
        var today = new Date().toISOString().split('T')[0];
        paymentDateInput.value = today;
    }

    // Clear any previous validation errors
    hideValidationError();

    // Show modal
    modal.style.display = 'flex';
}

/**
 * Opens the payment modal with an invoice selector dropdown.
 * Fetches invoices with outstanding balance and populates the dropdown.
 * Used from the Dashboard "Record Payment" button where no invoice is pre-selected.
 */
async function openPaymentModalWithSelector() {
    var modal = document.getElementById('paymentModal');
    if (!modal) return;

    // Hide context bar, show invoice selector
    var contextBar = document.getElementById('paymentModalContext');
    var selectorBar = document.getElementById('paymentModalInvoiceSelector');
    if (contextBar) contextBar.style.display = 'none';
    if (selectorBar) selectorBar.style.display = 'block';

    // Reset form
    var form = document.getElementById('paymentForm');
    if (form) {
        form.setAttribute('data-outstanding-balance', 0);
        form.reset();
    }

    var hiddenField = document.getElementById('paymentInvoiceId');
    if (hiddenField) hiddenField.value = '';

    // Set default payment date to today
    var paymentDateInput = document.getElementById('paymentDate');
    if (paymentDateInput) {
        var today = new Date().toISOString().split('T')[0];
        paymentDateInput.value = today;
    }

    hideValidationError();

    // Fetch invoices with outstanding balance
    var select = document.getElementById('paymentInvoiceSelect');
    if (select) {
        select.innerHTML = '<option value="">Loading...</option>';
    }

    // Show modal immediately
    modal.style.display = 'flex';

    try {
        var response = await fetch('/Revenue/GetInvoicesWithOutstandingBalance');
        var data = await response.json();

        if (data.success && select) {
            select.innerHTML = '<option value="">-- Select an invoice --</option>';
            for (var i = 0; i < data.data.length; i++) {
                var inv = data.data[i];
                var option = document.createElement('option');
                option.value = inv.id;
                option.setAttribute('data-outstanding', inv.outstandingBalance);
                option.setAttribute('data-customer', inv.customerName);
                option.textContent = inv.invoiceNumber + ' — ' + inv.customerName + ' (€' + parseFloat(inv.outstandingBalance).toFixed(2) + ' outstanding)';
                select.appendChild(option);
            }
        }
    } catch (e) {
        if (select) {
            select.innerHTML = '<option value="">Failed to load invoices</option>';
        }
    }
}

/**
 * Handles invoice selection from the dropdown in the payment modal.
 * Updates the hidden field and outstanding balance data attribute.
 */
function onInvoiceSelected(selectElement) {
    var selectedOption = selectElement.options[selectElement.selectedIndex];
    var invoiceId = selectElement.value;
    var outstanding = parseFloat(selectedOption.getAttribute('data-outstanding') || '0');

    var hiddenField = document.getElementById('paymentInvoiceId');
    if (hiddenField) hiddenField.value = invoiceId;

    var form = document.getElementById('paymentForm');
    if (form) form.setAttribute('data-outstanding-balance', outstanding);

    // Update context info below selector
    var remainingBalance = document.getElementById('paymentModalRemainingBalance');
    if (remainingBalance) {
        remainingBalance.textContent = invoiceId ? ('€' + outstanding.toFixed(2)) : '—';
    }
}

/**
 * Closes the payment modal and resets the form.
 */
function closePaymentModal() {
    var modal = document.getElementById('paymentModal');
    if (modal) {
        modal.style.display = 'none';
    }

    var form = document.getElementById('paymentForm');
    if (form) {
        form.reset();
    }

    // Reset selector state
    var selectorBar = document.getElementById('paymentModalInvoiceSelector');
    if (selectorBar) selectorBar.style.display = 'none';
    var contextBar = document.getElementById('paymentModalContext');
    if (contextBar) contextBar.style.display = 'flex';

    hideValidationError();
}

/**
 * Validates and submits the payment form.
 * Validates amount > 0 and ≤ outstanding balance before submission.
 */
async function submitPayment() {
    var form = document.getElementById('paymentForm');
    if (!form) return;

    // Validate invoice is selected
    var invoiceIdField = document.getElementById('paymentInvoiceId');
    if (!invoiceIdField || !invoiceIdField.value) {
        showValidationError('Please select an invoice.');
        return;
    }

    var amountInput = document.getElementById('paymentAmount');
    var amount = parseFloat(amountInput ? amountInput.value : '0');
    var outstandingBalance = parseFloat(form.getAttribute('data-outstanding-balance') || '0');

    // Validate amount > 0
    if (!amount || amount <= 0) {
        showValidationError('Payment amount must be greater than zero.');
        return;
    }

    // Validate amount ≤ outstanding balance
    if (amount > outstandingBalance) {
        showValidationError('Amount cannot exceed the outstanding balance of €' + outstandingBalance.toFixed(2) + '.');
        return;
    }

    // Validate payment method is selected
    var paymentMethodInput = document.getElementById('paymentMethod');
    if (!paymentMethodInput || !paymentMethodInput.value || paymentMethodInput.value === '0' || paymentMethodInput.value === '') {
        showValidationError('Please select a payment method.');
        return;
    }

    hideValidationError();

    // Build form data
    var formData = new FormData(form);

    BlockUI.show('Processing...');

    try {
        var response = await fetch('/Revenue/RecordPayment', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: formData
        });

        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            closePaymentModal();
            Swal.fire({
                title: 'Payment Recorded',
                text: data.message || 'The payment has been recorded successfully.',
                icon: 'success',
                confirmButtonColor: '#0D5EA6'
            }).then(function () {
                location.reload();
            });
        } else {
            Swal.fire({
                title: 'Error',
                text: data.message || 'An unexpected error occurred.',
                icon: 'error',
                confirmButtonColor: '#0D5EA6'
            });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({
            title: 'Error',
            text: 'An unexpected error occurred. Please try again.',
            icon: 'error',
            confirmButtonColor: '#0D5EA6'
        });
    }
}

/**
 * Voids a payment after confirmation dialog.
 * @param {number} paymentId - The payment ID to void
 */
function voidPayment(paymentId) {
    Swal.fire({
        title: 'Void Payment?',
        text: 'This action cannot be undone. The payment will be marked as voided and the invoice balance will be recalculated.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Yes, Void Payment',
        cancelButtonText: 'Cancel',
        confirmButtonColor: '#C24A4A',
        cancelButtonColor: '#6c757d'
    }).then(async function (result) {
        if (!result.isConfirmed) return;

        BlockUI.show('Processing...');

        try {
            var formData = new FormData();
            formData.append('paymentId', paymentId);

            var response = await fetch('/Revenue/AxPostVoidPaymentSmart', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: formData
            });

            var data = await response.json();
            BlockUI.hide();

            if (data.success) {
                Swal.fire({
                    title: 'Payment Voided',
                    text: data.message || 'The payment has been voided successfully.',
                    icon: 'success',
                    confirmButtonColor: '#0D5EA6'
                }).then(function () {
                    location.reload();
                });
            } else {
                Swal.fire({
                    title: 'Error',
                    text: data.message || 'An unexpected error occurred.',
                    icon: 'error',
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({
                title: 'Error',
                text: 'An unexpected error occurred. Please try again.',
                icon: 'error',
                confirmButtonColor: '#0D5EA6'
            });
        }
    });
}

/**
 * Shows a validation error message in the payment form.
 * @param {string} message - The error message to display
 */
function showValidationError(message) {
    var errorDiv = document.getElementById('paymentValidationError');
    if (errorDiv) {
        errorDiv.textContent = message;
        errorDiv.style.display = 'block';
    }
}

/**
 * Hides the validation error message in the payment form.
 */
function hideValidationError() {
    var errorDiv = document.getElementById('paymentValidationError');
    if (errorDiv) {
        errorDiv.textContent = '';
        errorDiv.style.display = 'none';
    }
}

// Expose functions globally for onclick attributes
window.openPaymentModal = openPaymentModal;
window.openPaymentModalWithSelector = openPaymentModalWithSelector;
window.onInvoiceSelected = onInvoiceSelected;
window.closePaymentModal = closePaymentModal;
window.submitPayment = submitPayment;
window.voidPayment = voidPayment;

/**
 * Generates a payment receipt for a given payment ID.
 * @param {number} paymentId - The payment ID to generate a receipt for
 */
async function generateReceiptFromDashboard(paymentId) {
    BlockUI.show('Checking receipt...');
    try {
        var checkResponse = await fetch('/Receipt/AxGetHasReceipt?paymentId=' + paymentId);
        var checkData = await checkResponse.json();
        BlockUI.hide();

        if (checkData.success && checkData.hasReceipt) {
            window.location.href = '/Receipt/Detail/' + checkData.receiptId;
            return;
        }

        openReceiptModal(paymentId);
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to check receipt status.', confirmButtonColor: '#0D5EA6' });
    }
}
window.generateReceiptFromDashboard = generateReceiptFromDashboard;
