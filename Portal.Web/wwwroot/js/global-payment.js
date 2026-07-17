/* ==========================================================================
   Global Payment — Record Payment modal for Statement page.
   Handles FIFO/Manual allocation, overpayment warning, and submission.
   Place at: Portal.Web/wwwroot/js/global-payment.js
   ========================================================================== */

function getAntiForgeryToken() {
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

var _gpOutstandingInvoices = [];
var _gpTotalOutstanding = 0;

async function openGlobalPaymentModal(customerId, customerName) {
    var modal = document.getElementById('globalPaymentModal');
    if (!modal) return;

    document.getElementById('gpCustomerId').value = customerId;
    document.getElementById('gpCustomerName').textContent = customerName;
    document.getElementById('gpTotalOutstanding').textContent = 'Loading...';
    document.getElementById('globalPaymentForm').reset();
    document.getElementById('gpManualSection').style.display = 'none';
    document.querySelector('input[name="gpAllocationMode"][value="fifo"]').checked = true;
    hideGpError();
    document.getElementById('gpPaymentDate').value = new Date().toISOString().split('T')[0];
    modal.style.display = 'flex';

    try {
        var response = await fetch('/Revenue/AxGetOutstandingInvoicesForCustomer?customerId=' + customerId);
        var data = await response.json();
        if (data.success) {
            _gpOutstandingInvoices = data.data;
            _gpTotalOutstanding = data.totalOutstanding;
            document.getElementById('gpTotalOutstanding').textContent = '\u20AC' + parseFloat(data.totalOutstanding).toFixed(2);
            renderManualInvoiceList(data.data);
        } else {
            document.getElementById('gpTotalOutstanding').textContent = 'Error';
        }
    } catch (e) {
        document.getElementById('gpTotalOutstanding').textContent = 'Error';
    }
}

function closeGlobalPaymentModal() {
    var modal = document.getElementById('globalPaymentModal');
    if (modal) modal.style.display = 'none';
}

function toggleManualAllocation() {
    var mode = document.querySelector('input[name="gpAllocationMode"]:checked').value;
    document.getElementById('gpManualSection').style.display = mode === 'manual' ? 'block' : 'none';
}

function renderManualInvoiceList(invoices) {
    var container = document.getElementById('gpInvoiceList');
    if (!invoices || invoices.length === 0) {
        container.innerHTML = '<p class="muted" style="font-size:13px;text-align:center;">No outstanding invoices.</p>';
        return;
    }
    var html = '<table style="width:100%;font-size:13px;"><thead><tr><th style="text-align:left;">Invoice</th><th style="text-align:left;">Date</th><th style="text-align:right;">Outstanding</th><th style="text-align:right;width:120px;">Allocate</th></tr></thead><tbody>';
    invoices.forEach(function (inv) {
        html += '<tr><td>' + inv.invoiceNumber + '</td><td>' + inv.invoiceDate + '</td><td style="text-align:right;">\u20AC' + parseFloat(inv.outstandingBalance).toFixed(2) + '</td><td style="text-align:right;"><input type="number" step="0.01" min="0" max="' + inv.outstandingBalance + '" data-invoice-id="' + inv.invoiceId + '" class="gp-alloc-input" style="width:100px;padding:4px 8px;border-radius:8px;border:1px solid rgba(13,94,166,.12);font-size:13px;text-align:right;" oninput="updateManualSum()" placeholder="0.00" /></td></tr>';
    });
    html += '</tbody></table>';
    container.innerHTML = html;
}

function updateManualSum() {
    var inputs = document.querySelectorAll('.gp-alloc-input');
    var sum = 0;
    inputs.forEach(function (input) { sum += parseFloat(input.value) || 0; });
    document.getElementById('gpManualSum').textContent = 'Allocated: \u20AC' + sum.toFixed(2);
}

function getManualAllocations() {
    var inputs = document.querySelectorAll('.gp-alloc-input');
    var allocations = [];
    inputs.forEach(function (input) {
        var amount = parseFloat(input.value) || 0;
        if (amount > 0) {
            allocations.push({ invoiceId: parseInt(input.getAttribute('data-invoice-id')), amount: amount });
        }
    });
    return allocations;
}

async function submitGlobalPayment() {
    hideGpError();
    var customerId = parseInt(document.getElementById('gpCustomerId').value);
    var amount = parseFloat(document.getElementById('gpAmount').value);
    var paymentDate = document.getElementById('gpPaymentDate').value;
    var methodId = parseInt(document.getElementById('gpPaymentMethod').value);
    var reference = document.getElementById('gpReference').value;
    var notes = document.getElementById('gpNotes').value;
    var mode = document.querySelector('input[name="gpAllocationMode"]:checked').value;

    if (!amount || amount <= 0) { showGpError('Amount must be greater than zero.'); return; }
    if (!paymentDate) { showGpError('Payment date is required.'); return; }
    if (!methodId) { showGpError('Please select a payment method.'); return; }

    var manualAllocations = null;
    if (mode === 'manual') {
        manualAllocations = getManualAllocations();
        if (manualAllocations.length === 0) { showGpError('Please allocate to at least one invoice.'); return; }
        var manualSum = manualAllocations.reduce(function (s, a) { return s + a.amount; }, 0);
        if (manualSum > amount) { showGpError('Sum of allocations exceeds the payment amount.'); return; }
    }

    if (amount > _gpTotalOutstanding && _gpTotalOutstanding > 0) {
        var excess = (amount - _gpTotalOutstanding).toFixed(2);
        var confirmation = await Swal.fire({
            title: 'Overpayment Detected',
            html: 'This payment exceeds the total outstanding by <strong>\u20AC' + excess + '</strong>.<br>The excess will be recorded as a credit balance.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Proceed',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6'
        });
        if (!confirmation.isConfirmed) return;
    }

    var payload = {
        customerId: customerId,
        amount: amount,
        paymentDateUtc: paymentDate + 'T00:00:00Z',
        paymentMethodTypeId: methodId,
        reference: reference || null,
        notes: notes || null,
        allocationMode: mode,
        manualAllocations: manualAllocations
    };

    BlockUI.show('Recording payment...');
    try {
        var response = await fetch('/Revenue/AxPostRecordGlobalPayment', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
            body: JSON.stringify(payload)
        });
        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            closeGlobalPaymentModal();
            var count = data.allocationCount || 0;
            var total = data.totalAllocated || 0;
            var credit = data.creditAmount || 0;
            var msg = data.message || ('Payment allocated across ' + count + ' invoice(s).');
            if (credit > 0) msg += '\nCredit: \u20AC' + parseFloat(credit).toFixed(2);
            Swal.fire({ title: 'Payment Recorded', text: msg, icon: 'success', confirmButtonColor: '#0D5EA6' }).then(function () { location.reload(); });
        } else {
            Swal.fire({ title: 'Error', text: data.message, icon: 'error', confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' });
    }
}

function showGpError(msg) { var el = document.getElementById('gpValidationError'); if (el) { el.textContent = msg; el.style.display = 'block'; } }
function hideGpError() { var el = document.getElementById('gpValidationError'); if (el) { el.textContent = ''; el.style.display = 'none'; } }

window.openGlobalPaymentModal = openGlobalPaymentModal;
window.closeGlobalPaymentModal = closeGlobalPaymentModal;
window.submitGlobalPayment = submitGlobalPayment;
window.toggleManualAllocation = toggleManualAllocation;
window.updateManualSum = updateManualSum;
