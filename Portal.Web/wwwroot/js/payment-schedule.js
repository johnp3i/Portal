/* ==========================================================================
   Payment Schedule — CRUD operations for instalment plans on invoice detail
   Depends on: BlockUI (block-ui.js), SweetAlert2
   Container: #scheduleContainer with data-invoice-id and data-has-permission
   ========================================================================== */

(function () {
    'use strict';

    // =========================================================================
    // State
    // =========================================================================
    var state = {
        invoiceId: 0,
        hasPermission: false,
        scheduleId: null,
        outstandingBalance: 0,
        currencySymbol: '€',
        instalmentCounter: 0,
        vatWarningCache: null
    };

    // =========================================================================
    // Helpers
    // =========================================================================

    /**
     * Gets the antiforgery token from the page.
     */
    function getAntiForgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    /**
     * Formats a decimal as currency with the business currency symbol.
     */
    function formatCurrency(value) {
        return state.currencySymbol + parseFloat(value).toFixed(2);
    }

    /**
     * Formats a decimal value to 2 decimal places without currency symbol.
     */
    function formatAmount(value) {
        if (value == null) return '0.00';
        return parseFloat(value).toFixed(2);
    }

    /**
     * Formats a date string (ISO or DateOnly) for display.
     */
    function formatDate(dateStr) {
        if (!dateStr) return 'No date';
        var d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        return d.toLocaleDateString('en-IE', { day: 'numeric', month: 'short', year: 'numeric' });
    }

    /**
     * Returns an HTML status badge based on status ID and name.
     */
    function getStatusBadge(statusId, statusName) {
        var badgeClasses = {
            1: 'badge-pending',
            2: 'badge-due',
            3: 'badge-overdue',
            4: 'badge-paid',
            5: 'badge-partial'
        };
        return '<span class="badge ' + (badgeClasses[statusId] || '') + '">' + escapeHtml(statusName) + '</span>';
    }

    /**
     * Escapes HTML entities to prevent XSS.
     */
    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

    // =========================================================================
    // Load Schedule
    // =========================================================================

    /**
     * Fetches the payment schedule for the current invoice and renders the view.
     */
    async function loadSchedule() {
        var container = document.getElementById('scheduleContainer');
        if (!container) return;

        try {
            var response = await fetch('/Revenue/AxGetPaymentSchedule?invoiceId=' + state.invoiceId);
            var result = await response.json();

            if (result.success) {
                if (result.data) {
                    state.scheduleId = result.data.id;
                    renderActiveSchedule(result.data);
                } else {
                    state.scheduleId = null;
                    renderEmptyState();
                }
            } else {
                renderEmptyState();
            }
        } catch (error) {
            renderEmptyState();
        }
    }

    // =========================================================================
    // Render — Empty State
    // =========================================================================

    /**
     * Renders the empty state when no schedule exists.
     */
    function renderEmptyState() {
        var container = document.getElementById('scheduleContainer');
        if (!container) return;

        var html = '<div class="empty-state">';
        html += '<div class="empty-state-icon">&#128197;</div>';
        html += '<div class="empty-state-title">No payment schedule for this invoice</div>';
        html += '<p class="empty-state-text">Create an instalment plan to track how this invoice will be paid over time.</p>';

        if (state.hasPermission) {
            html += '<button class="btn btn-primary" onclick="PaymentSchedule.showCreateForm()">Create Payment Schedule</button>';
        } else {
            // Upgrade teaser for users without plan/permission access
            html += '<div style="margin-top:8px;">';
            html += '<svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#57B8E8" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" style="margin-bottom:12px;"><path d="M12 2L2 7l10 5 10-5-10-5z"></path><path d="M2 17l10 5 10-5"></path><path d="M2 12l10 5 10-5"></path></svg>';
            html += '<p style="font-size:13px;color:#5E7385;line-height:1.6;max-width:380px;margin:0 auto 16px;">Schedule instalment plans, auto-match payments, and track progress with VAT deadline warnings.</p>';
            html += '<div style="display:inline-block;padding:8px 16px;background:#EEF4F8;border-radius:10px;margin-bottom:16px;">';
            html += '<span style="font-size:12px;color:#0B1B28;font-weight:600;">Available in the <span style="color:#0D5EA6;">Professional</span> plan</span>';
            html += '</div>';
            html += '<br />';
            html += '<a href="/Account/Billing" style="display:inline-block;margin-top:8px;padding:10px 24px;background:#0D5EA6;color:#ffffff;border-radius:8px;text-decoration:none;font-weight:700;font-size:13px;">Go to Billing</a>';
            html += '</div>';
        }

        html += '</div>';
        container.innerHTML = html;
    }

    // =========================================================================
    // Render — Active Schedule
    // =========================================================================

    /**
     * Renders the active schedule with progress bar, instalment table, and controls.
     */
    function renderActiveSchedule(data) {
        var container = document.getElementById('scheduleContainer');
        if (!container) return;

        var totalAmount = data.totalPaid + data.totalRemaining;
        var progressPct = totalAmount > 0 ? ((data.totalPaid / totalAmount) * 100).toFixed(1) : 0;

        var html = '';

        // Header
        html += '<div class="card-header">';
        html += '<h3>Payment Schedule</h3>';
        if (state.hasPermission) {
            html += '<div class="card-header-actions">';
            html += '<button class="btn btn-danger-text" onclick="PaymentSchedule.confirmDeleteSchedule(' + data.id + ')">Delete</button>';
            html += '</div>';
        }
        html += '</div>';

        // Progress bar
        html += '<div class="progress-summary">';
        html += '<div class="progress-summary-row">';
        html += '<span>' + data.completedCount + ' of ' + data.totalCount + ' instalments paid</span>';
        html += '<span>' + formatCurrency(data.totalPaid) + ' received of ' + formatCurrency(totalAmount) + ' total</span>';
        html += '</div>';
        html += '<div class="progress-track">';
        html += '<div class="progress-fill" style="width:' + progressPct + '%;"></div>';
        html += '</div>';
        html += '</div>';

        // Instalment table
        html += '<table>';
        html += '<thead><tr>';
        html += '<th>#</th><th>Amount</th><th>Due Date</th><th>Status</th>';
        if (state.hasPermission) {
            html += '<th>Actions</th>';
        }
        html += '</tr></thead>';
        html += '<tbody>';

        for (var i = 0; i < data.instalments.length; i++) {
            var inst = data.instalments[i];
            var isRemainder = inst.isRemainder === true;
            var hasMatchedPayments = inst.matchedAmount > 0;

            // Remainder rows are visually nested
            if (isRemainder) {
                html += '<tr style="background:#FAFCFE;">';
                html += '<td style="padding-left:28px;color:#5E7385;font-size:13px;">↳</td>';
                html += '<td style="font-size:13px;color:#5E7385;">' + formatCurrency(inst.amount) + '</td>';
                html += '<td style="font-size:13px;color:#5E7385;">' + formatDate(inst.dueDate) + '</td>';
                html += '<td>' + getStatusBadge(inst.statusId, inst.statusName) + '</td>';
            } else {
                html += '<tr>';
                html += '<td>' + inst.sequenceNumber + '</td>';
                html += '<td>' + formatCurrency(inst.amount) + '</td>';
                html += '<td>' + formatDate(inst.dueDate) + '</td>';
                html += '<td>' + getStatusBadge(inst.statusId, inst.statusName) + '</td>';
            }

            if (state.hasPermission) {
                html += '<td>';
                // Only show actions for instalments that are NOT paid and NOT partially paid (has matched payments)
                if (inst.statusId !== 4 && !hasMatchedPayments) {
                    html += '<button class="btn btn-secondary btn-sm" onclick="PaymentSchedule.showEditInstalment(' + inst.id + ', ' + inst.amount + ', \'' + (inst.dueDate || '') + '\')">Edit</button> ';
                    if (!isRemainder) {
                        html += '<button class="btn btn-danger-text" onclick="PaymentSchedule.confirmRemoveInstalment(' + inst.id + ')">Remove</button>';
                    }
                }
                html += '</td>';
            }

            html += '</tr>';
        }

        html += '</tbody></table>';

        // Add instalment button
        if (state.hasPermission) {
            html += '<div style="margin-top:16px;">';
            html += '<button class="btn-link" onclick="PaymentSchedule.showAddInstalmentForm(' + data.id + ')">+ Add Instalment</button>';
            html += '</div>';
        }

        // History accordion
        html += '<div style="margin-top:24px;">';
        html += '<button class="btn-link" onclick="PaymentSchedule.toggleHistory(' + data.id + ')" id="historyToggleBtn">Show History</button>';
        html += '<div id="historyContainer" style="display:none;margin-top:12px;"></div>';
        html += '</div>';

        container.innerHTML = html;
    }

    // =========================================================================
    // Render — Create Form
    // =========================================================================

    /**
     * Renders the create schedule form with dynamic instalment rows.
     */
    function showCreateForm() {
        var container = document.getElementById('scheduleContainer');
        if (!container) return;

        state.instalmentCounter = 0;

        var html = '';
        html += '<h3 class="form-heading">Create Payment Schedule</h3>';

        // Invoice info bar
        html += '<div class="invoice-info">';
        html += '<span>Invoice <strong>' + escapeHtml(container.dataset.invoiceNumber || '') + '</strong></span>';
        html += '<span>Outstanding Balance: <strong>' + formatCurrency(state.outstandingBalance) + '</strong></span>';
        html += '</div>';

        // VAT warning placeholder
        html += '<div id="vatWarningBanner"></div>';

        // Instalments label
        html += '<div style="margin-bottom:8px;font-size:13px;font-weight:600;color:#5E7385;text-transform:uppercase;letter-spacing:0.3px;">Instalments</div>';

        // Instalment rows container
        html += '<div id="instalmentRows"></div>';

        // Add instalment link
        html += '<div style="margin-top:16px;">';
        html += '<button class="btn-link" onclick="PaymentSchedule.addInstalmentRow()">+ Add Instalment</button>';
        html += '</div>';

        // Balance validation summary
        html += '<div id="balanceValidation" style="display:none;margin-top:16px;margin-bottom:24px;"></div>';

        // Form footer
        html += '<div class="form-footer">';
        html += '<button class="btn btn-secondary" onclick="PaymentSchedule.cancelCreate()">Cancel</button>';
        html += '<button class="btn btn-primary" onclick="PaymentSchedule.submitCreateSchedule()">Create Schedule</button>';
        html += '</div>';

        container.innerHTML = html;

        // Add two default instalment rows
        addInstalmentRow();
        addInstalmentRow();
    }

    // =========================================================================
    // Create Form — Instalment Row Management
    // =========================================================================

    /**
     * Adds a new instalment row to the create form.
     */
    function addInstalmentRow() {
        var rowsContainer = document.getElementById('instalmentRows');
        if (!rowsContainer) return;

        state.instalmentCounter++;
        var index = state.instalmentCounter;

        var row = document.createElement('div');
        row.className = 'instalment-row';
        row.setAttribute('data-row-index', index);

        row.innerHTML = '<span class="instalment-num">' + index + '</span>'
            + '<div class="instalment-field">'
            + '<label>Amount</label>'
            + '<input type="number" step="0.01" min="0.01" class="instalment-amount" data-index="' + index + '" placeholder="0.00" />'
            + '</div>'
            + '<div class="instalment-field">'
            + '<label>Due Date</label>'
            + '<input type="date" class="instalment-date" data-index="' + index + '" />'
            + '</div>'
            + '<button class="instalment-remove" title="Remove instalment" onclick="PaymentSchedule.removeInstalmentRow(' + index + ')">&times;</button>';

        rowsContainer.appendChild(row);

        // Bind change events for validation and VAT warning
        var amountInput = row.querySelector('.instalment-amount');
        var dateInput = row.querySelector('.instalment-date');

        amountInput.addEventListener('input', function () {
            // Clear auto-suggest flag if user manually edits the last row
            if (this.getAttribute('data-auto-suggested') === 'true') {
                this.removeAttribute('data-auto-suggested');
                var parentRow = this.closest('.instalment-row');
                if (parentRow) hideSuggestionHint(parentRow);
            }
            onAmountChange();
            autoSuggestRemainingBalance();
            updateBalanceValidation();
        });
        dateInput.addEventListener('change', function () {
            onDateChange();
            updateBalanceValidation();
        });

        updateBalanceValidation();
        renumberRows();
    }

    /**
     * Removes an instalment row from the create form.
     */
    function removeInstalmentRow(index) {
        var rowsContainer = document.getElementById('instalmentRows');
        if (!rowsContainer) return;

        var row = rowsContainer.querySelector('[data-row-index="' + index + '"]');
        if (row) {
            rowsContainer.removeChild(row);
            renumberRows();
            updateBalanceValidation();
            autoSuggestRemainingBalance();
        }
    }

    /**
     * Renumbers the instalment rows sequentially after add/remove.
     */
    function renumberRows() {
        var rowsContainer = document.getElementById('instalmentRows');
        if (!rowsContainer) return;

        var rows = rowsContainer.querySelectorAll('.instalment-row');
        for (var i = 0; i < rows.length; i++) {
            var numSpan = rows[i].querySelector('.instalment-num');
            if (numSpan) numSpan.textContent = (i + 1);
        }
    }

    // =========================================================================
    // Balance Validation
    // =========================================================================

    /**
     * Updates the balance validation display in real-time.
     */
    function updateBalanceValidation() {
        var validationDiv = document.getElementById('balanceValidation');
        if (!validationDiv) return;

        var total = getInstalmentTotal();
        var balance = state.outstandingBalance;
        var diff = Math.abs(total - balance);

        if (total === 0) {
            validationDiv.style.display = 'none';
            return;
        }

        validationDiv.style.display = 'flex';

        if (diff < 0.01) {
            // Matches
            validationDiv.className = 'validation-summary validation-ok';
            validationDiv.innerHTML = '&#10003; Total: ' + formatCurrency(total) + ' &mdash; Matches outstanding balance';
        } else if (total > balance) {
            validationDiv.className = 'validation-summary validation-error';
            validationDiv.innerHTML = '&#10007; Total: ' + formatCurrency(total) + ' &mdash; Exceeds outstanding balance by ' + formatCurrency(total - balance);
        } else {
            validationDiv.className = 'validation-summary validation-warning';
            validationDiv.innerHTML = '&#9888; Total: ' + formatCurrency(total) + ' &mdash; ' + formatCurrency(balance - total) + ' remaining to allocate';
        }
    }

    /**
     * Calculates the sum of all instalment amounts in the create form.
     */
    function getInstalmentTotal() {
        var inputs = document.querySelectorAll('#instalmentRows .instalment-amount');
        var total = 0;
        for (var i = 0; i < inputs.length; i++) {
            var val = parseFloat(inputs[i].value);
            if (!isNaN(val)) total += val;
        }
        return Math.round(total * 100) / 100;
    }

    // =========================================================================
    // Auto-Suggest Remaining Balance
    // =========================================================================

    /**
     * Auto-suggests the remaining balance in the last instalment row.
     * Only fills if the last row's amount is 0/empty and there are at least 2 rows.
     */
    function autoSuggestRemainingBalance() {
        var rowsContainer = document.getElementById('instalmentRows');
        if (!rowsContainer) return;

        var rows = rowsContainer.querySelectorAll('.instalment-row');
        if (rows.length < 2) return;

        var lastRow = rows[rows.length - 1];
        var lastAmountInput = lastRow.querySelector('.instalment-amount');
        if (!lastAmountInput) return;

        var lastCurrentValue = parseFloat(lastAmountInput.value) || 0;

        // Only auto-suggest if the last row is empty/zero (don't overwrite manual entries)
        // We track this with a data attribute
        var wasAutoSuggested = lastAmountInput.getAttribute('data-auto-suggested') === 'true';
        if (lastCurrentValue !== 0 && !wasAutoSuggested) return;

        // Calculate sum of all rows EXCEPT the last one
        var sumExceptLast = 0;
        for (var i = 0; i < rows.length - 1; i++) {
            var input = rows[i].querySelector('.instalment-amount');
            var val = parseFloat(input.value) || 0;
            sumExceptLast += val;
        }

        var remaining = state.outstandingBalance - sumExceptLast;
        remaining = Math.round(remaining * 100) / 100;

        if (remaining > 0.001) {
            lastAmountInput.value = remaining.toFixed(2);
            lastAmountInput.setAttribute('data-auto-suggested', 'true');
            showSuggestionHint(lastRow);
        } else {
            // Zero or negative — clear the suggestion completely
            lastAmountInput.value = '';
            lastAmountInput.removeAttribute('data-auto-suggested');
            hideSuggestionHint(lastRow);
        }
    }

    /**
     * Shows a subtle hint below the last instalment row indicating the value was auto-suggested.
     */
    function showSuggestionHint(row) {
        var existingHint = row.querySelector('.suggestion-hint');
        if (existingHint) return; // Already showing

        var hint = document.createElement('div');
        hint.className = 'suggestion-hint';
        hint.style.cssText = 'font-size:11px;color:#5E7385;font-style:italic;margin-top:4px;margin-left:36px;';
        hint.innerHTML = '&#128161; Suggested: remaining balance';
        row.appendChild(hint);
    }

    /**
     * Hides the suggestion hint from a row.
     */
    function hideSuggestionHint(row) {
        var hint = row.querySelector('.suggestion-hint');
        if (hint) hint.remove();
    }

    // =========================================================================
    // VAT Warning
    // =========================================================================

    var vatWarningDebounceTimer = null;

    /**
     * Called on date change — debounces and fetches VAT warning from server.
     */
    function onDateChange() {
        if (vatWarningDebounceTimer) {
            clearTimeout(vatWarningDebounceTimer);
        }
        vatWarningDebounceTimer = setTimeout(function () {
            fetchVatWarning();
        }, 300);
    }

    /**
     * Called on amount change — re-evaluates locally using cached data (no server call).
     */
    function onAmountChange() {
        if (!state.vatWarningCache) return;

        var rowsContainer = document.getElementById('instalmentRows');
        if (!rowsContainer) return;
        var firstRow = rowsContainer.querySelector('.instalment-row');
        if (!firstRow) return;

        var amountInput = firstRow.querySelector('.instalment-amount');
        var firstAmount = parseFloat(amountInput.value) || 0;

        // Re-evaluate highlight: if first instalment amount < TaxAmount, highlight
        state.vatWarningCache.highlightVatAmount = firstAmount < state.vatWarningCache.taxAmount;

        // Re-render with updated data
        renderVatWarning(state.vatWarningCache);
    }

    /**
     * Fetches VAT warning from server based on first instalment's date and amount.
     */
    async function fetchVatWarning() {
        var rowsContainer = document.getElementById('instalmentRows');
        if (!rowsContainer) return;

        var firstRow = rowsContainer.querySelector('.instalment-row');
        if (!firstRow) return;

        var dateInput = firstRow.querySelector('.instalment-date');
        var amountInput = firstRow.querySelector('.instalment-amount');
        if (!dateInput || !dateInput.value) {
            state.vatWarningCache = null;
            clearVatWarning();
            return;
        }

        var firstAmount = parseFloat(amountInput.value) || 0;
        var firstDueDate = dateInput.value;

        try {
            var url = '/Revenue/AxGetVatWarning?invoiceId=' + state.invoiceId
                + '&firstDueDate=' + encodeURIComponent(firstDueDate)
                + '&firstAmount=' + firstAmount;

            var response = await fetch(url);
            var result = await response.json();

            if (result.success && result.data && result.data.showWarning) {
                state.vatWarningCache = result.data;
                renderVatWarning(result.data);
            } else {
                state.vatWarningCache = null;
                clearVatWarning();
            }
        } catch (error) {
            state.vatWarningCache = null;
            clearVatWarning();
        }
    }

    /**
     * Legacy function name — now delegates to appropriate handlers.
     * Keep for backward compatibility with existing event bindings.
     */
    async function onInstalmentDateChange() {
        await fetchVatWarning();
    }

    /**
     * Renders the VAT warning banner — adapts based on whether the first instalment covers VAT.
     */
    function renderVatWarning(warning) {
        var banner = document.getElementById('vatWarningBanner');
        if (!banner) return;

        var cs = state.currencySymbol || '€';
        var html = '';

        if (warning.highlightVatAmount) {
            // First instalment does NOT cover VAT — show amber warning
            html += '<div class="warning-banner">';
            html += '<div class="warning-banner-content">';
            html += '<span class="warning-banner-icon">&#9888;&#65039;</span>';
            html += '<div>';
            html += '<div class="warning-banner-text">The VAT for this invoice (<strong>' + cs + formatAmount(warning.taxAmount) + '</strong>) will need to be paid to the tax authority regardless of when you receive payment. Consider setting your first instalment to at least cover the VAT amount.</div>';
            if (warning.submissionDeadline) {
                html += '<div class="warning-banner-note">VAT submission deadline: <strong>' + formatDate(warning.submissionDeadline) + '</strong></div>';
            }
            html += '</div></div></div>';
        } else {
            // First instalment covers VAT — show green confirmation
            html += '<div style="background:#E6F7EF;border-left:4px solid #129867;border-radius:8px;padding:14px 20px;margin:20px 0;">';
            html += '<div style="display:flex;gap:12px;align-items:center;">';
            html += '<span style="font-size:18px;">&#10003;</span>';
            html += '<div style="font-size:14px;color:#0B1B28;line-height:1.5;">Your first instalment covers the VAT amount (' + cs + formatAmount(warning.taxAmount) + '). VAT submission deadline: <strong>' + formatDate(warning.submissionDeadline) + '</strong></div>';
            html += '</div></div>';
        }

        banner.innerHTML = html;
    }

    /**
     * Clears the VAT warning banner.
     */
    function clearVatWarning() {
        var banner = document.getElementById('vatWarningBanner');
        if (banner) banner.innerHTML = '';
    }

    // =========================================================================
    // Create Schedule — Submit
    // =========================================================================

    /**
     * Validates and submits the create schedule form.
     */
    async function submitCreateSchedule() {
        var total = getInstalmentTotal();
        var balance = state.outstandingBalance;

        if (Math.abs(total - balance) >= 0.01) {
            Swal.fire({
                icon: 'warning',
                title: 'Balance Mismatch',
                text: 'The total of all instalments (' + formatCurrency(total) + ') does not equal the outstanding balance (' + formatCurrency(balance) + ').',
                confirmButtonColor: '#0D5EA6'
            });
            return;
        }

        // Collect instalments
        var rows = document.querySelectorAll('#instalmentRows .instalment-row');
        var instalments = [];

        for (var i = 0; i < rows.length; i++) {
            var amountInput = rows[i].querySelector('.instalment-amount');
            var dateInput = rows[i].querySelector('.instalment-date');

            var amount = parseFloat(amountInput.value);
            if (isNaN(amount) || amount <= 0) {
                Swal.fire({
                    icon: 'warning',
                    title: 'Invalid Amount',
                    text: 'Instalment ' + (i + 1) + ' must have an amount greater than zero.',
                    confirmButtonColor: '#0D5EA6'
                });
                return;
            }

            instalments.push({
                amount: amount,
                dueDate: dateInput.value || null
            });
        }

        if (instalments.length === 0) {
            Swal.fire({
                icon: 'warning',
                title: 'No Instalments',
                text: 'Please add at least one instalment.',
                confirmButtonColor: '#0D5EA6'
            });
            return;
        }

        var payload = {
            invoiceId: state.invoiceId,
            instalments: instalments
        };

        BlockUI.show('Creating schedule...');

        try {
            var response = await fetch('/Revenue/AxPostCreatePaymentSchedule', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });

            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                Swal.fire({
                    icon: 'success',
                    title: 'Done',
                    text: result.message || 'Payment schedule created.',
                    confirmButtonColor: '#0D5EA6'
                });
                loadSchedule();
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: result.message || 'Failed to create schedule.',
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } catch (error) {
            BlockUI.hide();
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Something went wrong.',
                confirmButtonColor: '#0D5EA6'
            });
        }
    }

    /**
     * Cancels the create form and returns to the previous view.
     */
    function cancelCreate() {
        loadSchedule();
    }

    // =========================================================================
    // Update Instalment (Inline Edit)
    // =========================================================================

    /**
     * Shows the edit instalment dialog using SweetAlert2.
     */
    function showEditInstalment(instalmentId, currentAmount, currentDueDate) {
        Swal.fire({
            title: 'Edit Instalment',
            html: '<div style="text-align:left;">'
                + '<div style="margin-bottom:12px;">'
                + '<label style="font-size:13px;font-weight:600;color:#5E7385;display:block;margin-bottom:4px;">Amount</label>'
                + '<input id="swalEditAmount" type="number" step="0.01" min="0.01" value="' + parseFloat(currentAmount).toFixed(2) + '" class="swal2-input" style="margin:0;width:100%;" />'
                + '</div>'
                + '<div>'
                + '<label style="font-size:13px;font-weight:600;color:#5E7385;display:block;margin-bottom:4px;">Due Date</label>'
                + '<input id="swalEditDate" type="date" value="' + (currentDueDate || '') + '" class="swal2-input" style="margin:0;width:100%;" />'
                + '</div>'
                + '</div>',
            showCancelButton: true,
            confirmButtonText: 'Save',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6',
            preConfirm: function () {
                var amount = parseFloat(document.getElementById('swalEditAmount').value);
                var dueDate = document.getElementById('swalEditDate').value || null;
                if (isNaN(amount) || amount <= 0) {
                    Swal.showValidationMessage('Amount must be greater than zero.');
                    return false;
                }
                return { amount: amount, dueDate: dueDate };
            }
        }).then(function (result) {
            if (result.isConfirmed) {
                saveInstalmentEdit(instalmentId, result.value.amount, result.value.dueDate);
            }
        });
    }

    /**
     * Saves the instalment edit via AJAX.
     */
    async function saveInstalmentEdit(instalmentId, newAmount, newDueDate) {
        var payload = {
            instalmentId: instalmentId,
            scheduleId: state.scheduleId,
            newAmount: newAmount,
            newDueDate: newDueDate,
            clearDueDate: !newDueDate
        };

        BlockUI.show('Saving...');

        try {
            var response = await fetch('/Revenue/AxPostUpdateInstalment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });

            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                Swal.fire({
                    icon: 'success',
                    title: 'Done',
                    text: result.message || 'Instalment updated.',
                    confirmButtonColor: '#0D5EA6'
                });
                loadSchedule();
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: result.message || 'Failed to update instalment.',
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } catch (error) {
            BlockUI.hide();
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Something went wrong.',
                confirmButtonColor: '#0D5EA6'
            });
        }
    }

    // =========================================================================
    // Add Instalment (to existing schedule)
    // =========================================================================

    /**
     * Shows the add instalment dialog using SweetAlert2.
     */
    function showAddInstalmentForm(scheduleId) {
        Swal.fire({
            title: 'Add Instalment',
            html: '<div style="text-align:left;">'
                + '<div style="margin-bottom:12px;">'
                + '<label style="font-size:13px;font-weight:600;color:#5E7385;display:block;margin-bottom:4px;">Amount</label>'
                + '<input id="swalAddAmount" type="number" step="0.01" min="0.01" placeholder="0.00" class="swal2-input" style="margin:0;width:100%;" />'
                + '</div>'
                + '<div>'
                + '<label style="font-size:13px;font-weight:600;color:#5E7385;display:block;margin-bottom:4px;">Due Date (optional)</label>'
                + '<input id="swalAddDate" type="date" class="swal2-input" style="margin:0;width:100%;" />'
                + '</div>'
                + '</div>',
            showCancelButton: true,
            confirmButtonText: 'Add',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6',
            preConfirm: function () {
                var amount = parseFloat(document.getElementById('swalAddAmount').value);
                var dueDate = document.getElementById('swalAddDate').value || null;
                if (isNaN(amount) || amount <= 0) {
                    Swal.showValidationMessage('Amount must be greater than zero.');
                    return false;
                }
                return { amount: amount, dueDate: dueDate };
            }
        }).then(function (result) {
            if (result.isConfirmed) {
                addInstalmentToSchedule(scheduleId, result.value.amount, result.value.dueDate);
            }
        });
    }

    /**
     * Submits the add instalment request.
     */
    async function addInstalmentToSchedule(scheduleId, amount, dueDate) {
        var payload = {
            scheduleId: scheduleId,
            amount: amount,
            dueDate: dueDate
        };

        BlockUI.show('Adding instalment...');

        try {
            var response = await fetch('/Revenue/AxPostAddInstalment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });

            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                Swal.fire({
                    icon: 'success',
                    title: 'Done',
                    text: result.message || 'Instalment added.',
                    confirmButtonColor: '#0D5EA6'
                });
                loadSchedule();
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: result.message || 'Failed to add instalment.',
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } catch (error) {
            BlockUI.hide();
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Something went wrong.',
                confirmButtonColor: '#0D5EA6'
            });
        }
    }

    // =========================================================================
    // Remove Instalment
    // =========================================================================

    /**
     * Shows confirmation dialog before removing an instalment.
     */
    function confirmRemoveInstalment(instalmentId) {
        Swal.fire({
            title: 'Remove Instalment?',
            text: 'This instalment will be removed from the schedule.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, remove it',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#C24A4A'
        }).then(function (result) {
            if (result.isConfirmed) {
                removeInstalment(instalmentId);
            }
        });
    }

    /**
     * Removes an instalment via AJAX.
     */
    async function removeInstalment(instalmentId) {
        BlockUI.show('Removing instalment...');

        try {
            var response = await fetch('/Revenue/AxPostRemoveInstalment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(instalmentId)
            });

            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                Swal.fire({
                    icon: 'success',
                    title: 'Done',
                    text: result.message || 'Instalment removed.',
                    confirmButtonColor: '#0D5EA6'
                });
                loadSchedule();
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: result.message || 'Failed to remove instalment.',
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } catch (error) {
            BlockUI.hide();
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Something went wrong.',
                confirmButtonColor: '#0D5EA6'
            });
        }
    }

    // =========================================================================
    // Delete Schedule
    // =========================================================================

    /**
     * Shows SweetAlert2 confirmation before deleting the entire schedule.
     */
    function confirmDeleteSchedule(scheduleId) {
        Swal.fire({
            title: 'Delete Payment Schedule?',
            text: 'This action cannot be undone.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, delete it',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#C24A4A'
        }).then(function (result) {
            if (result.isConfirmed) {
                deleteSchedule(scheduleId);
            }
        });
    }

    /**
     * Deletes the schedule via AJAX.
     */
    async function deleteSchedule(scheduleId) {
        BlockUI.show('Deleting schedule...');

        try {
            var response = await fetch('/Revenue/AxPostDeletePaymentSchedule', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(scheduleId)
            });

            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                Swal.fire({
                    icon: 'success',
                    title: 'Done',
                    text: result.message || 'Payment schedule deleted.',
                    confirmButtonColor: '#0D5EA6'
                });
                loadSchedule();
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: result.message || 'Failed to delete schedule.',
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } catch (error) {
            BlockUI.hide();
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Something went wrong.',
                confirmButtonColor: '#0D5EA6'
            });
        }
    }

    // =========================================================================
    // History
    // =========================================================================

    /**
     * Toggles the history accordion, fetching data on first open.
     */
    async function toggleHistory(scheduleId) {
        var container = document.getElementById('historyContainer');
        var btn = document.getElementById('historyToggleBtn');
        if (!container) return;

        if (container.style.display === 'none') {
            container.style.display = 'block';
            if (btn) btn.textContent = 'Hide History';
            await loadHistory(scheduleId);
        } else {
            container.style.display = 'none';
            if (btn) btn.textContent = 'Show History';
        }
    }

    /**
     * Fetches and renders the schedule history timeline.
     */
    async function loadHistory(scheduleId) {
        var container = document.getElementById('historyContainer');
        if (!container) return;

        container.innerHTML = '<p style="color:#5E7385;font-size:13px;">Loading history...</p>';

        try {
            var response = await fetch('/Revenue/AxGetScheduleHistory?scheduleId=' + scheduleId);
            var result = await response.json();

            if (result.success && result.data && result.data.length > 0) {
                renderHistoryTimeline(result.data);
            } else {
                container.innerHTML = '<p style="color:#5E7385;font-size:13px;">No history recorded yet.</p>';
            }
        } catch (error) {
            container.innerHTML = '<p style="color:#5E7385;font-size:13px;">Failed to load history.</p>';
        }
    }

    /**
     * Renders the modification history as a timeline.
     */
    function renderHistoryTimeline(history) {
        var container = document.getElementById('historyContainer');
        if (!container) return;

        var html = '<div class="timeline">';

        for (var i = 0; i < history.length; i++) {
            var entry = history[i];
            var dotClass = entry.fieldChanged === 'Created' ? 'timeline-dot-green' : 'timeline-dot-blue';
            var description = buildHistoryDescription(entry);

            html += '<div class="timeline-entry">';
            html += '<div class="timeline-dot ' + dotClass + '"></div>';
            html += '<div class="timeline-text">' + description + '</div>';
            html += '<div class="timeline-time">' + formatDateTime(entry.changedAtUtc) + '</div>';
            html += '</div>';
        }

        html += '</div>';
        container.innerHTML = html;
    }

    /**
     * Builds a human-readable description for a history entry.
     */
    function buildHistoryDescription(entry) {
        var field = entry.fieldChanged || '';

        if (field === 'Created') {
            return 'Payment schedule created' + (entry.newValue ? ' with ' + escapeHtml(entry.newValue) + ' instalments' : '');
        }
        if (field === 'Deleted') {
            return 'Payment schedule deleted';
        }
        if (field === 'InstalmentAdded') {
            return 'Instalment added' + (entry.newValue ? ' (' + escapeHtml(entry.newValue) + ')' : '');
        }
        if (field === 'InstalmentRemoved') {
            return 'Instalment removed' + (entry.oldValue ? ' (' + escapeHtml(entry.oldValue) + ')' : '');
        }

        // Generic field change
        var text = 'Changed <strong>' + escapeHtml(field) + '</strong>';
        if (entry.oldValue) text += ' from ' + escapeHtml(entry.oldValue);
        if (entry.newValue) text += ' to ' + escapeHtml(entry.newValue);
        return text;
    }

    /**
     * Formats a UTC datetime for display.
     */
    function formatDateTime(utcStr) {
        if (!utcStr) return '';
        var d = new Date(utcStr);
        if (isNaN(d.getTime())) return utcStr;
        return d.toLocaleDateString('en-IE', { day: 'numeric', month: 'short', year: 'numeric' })
            + ' at ' + d.toLocaleTimeString('en-IE', { hour: '2-digit', minute: '2-digit' });
    }

    // =========================================================================
    // Initialization
    // =========================================================================

    document.addEventListener('DOMContentLoaded', function () {
        var container = document.getElementById('scheduleContainer');
        if (!container) return;

        state.invoiceId = parseInt(container.dataset.invoiceId) || 0;
        state.hasPermission = container.dataset.hasPermission === 'true';
        state.outstandingBalance = parseFloat(container.dataset.outstandingBalance) || 0;
        state.currencySymbol = container.dataset.currencySymbol || '€';

        if (state.invoiceId > 0) {
            loadSchedule();
        }
    });

    // =========================================================================
    // Public API (exposed for onclick handlers)
    // =========================================================================

    window.PaymentSchedule = {
        loadSchedule: loadSchedule,
        showCreateForm: showCreateForm,
        addInstalmentRow: addInstalmentRow,
        removeInstalmentRow: removeInstalmentRow,
        cancelCreate: cancelCreate,
        submitCreateSchedule: submitCreateSchedule,
        showEditInstalment: showEditInstalment,
        showAddInstalmentForm: showAddInstalmentForm,
        confirmRemoveInstalment: confirmRemoveInstalment,
        confirmDeleteSchedule: confirmDeleteSchedule,
        toggleHistory: toggleHistory
    };

})();
