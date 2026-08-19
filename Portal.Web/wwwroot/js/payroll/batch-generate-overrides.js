/* ==========================================================================
   Batch Generate Overrides — Editable Earnings for Payslip Preview
   Handles opening the earnings edit modal, validating inputs, calling the
   recalculation endpoint, managing override state, and confirming batch
   generation with overrides applied.
   ========================================================================== */

/**
 * In-memory override store — keyed by employeeId.
 * Each entry: { earningLines: [...], result: { totalEarnings, totalEmployeeDeductions, netSalary, totalEmployerContributions } }
 */
var earningsOverrides = new Map();

/**
 * Currently active employee ID in the modal.
 */
var currentModalEmployeeId = null;

/**
 * Gets the antiforgery token from the page.
 */
function getAntiForgeryToken() {
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

/* --------------------------------------------------------------------------
   10.1 — Override State Management
   -------------------------------------------------------------------------- */

/**
 * Gets the original earning lines for an employee from the server-rendered data.
 * Falls back to a single Basic Salary line if no earning lines exist.
 * @param {number} employeeId
 * @returns {Array} earning lines array
 */
function getOriginalEarningLines(employeeId) {
    if (typeof employeeEarningsData !== 'undefined' && employeeEarningsData[employeeId]) {
        return employeeEarningsData[employeeId];
    }
    // Fallback: no earning lines data available
    return [];
}

/* --------------------------------------------------------------------------
   10.2 — Open Modal with Earning Lines
   -------------------------------------------------------------------------- */

/**
 * Opens the Edit Earnings modal for a specific employee.
 * Shows overridden amounts if previously edited, otherwise original data.
 * @param {number} employeeId
 */
window.openEarningsEditModal = function (employeeId) {
    currentModalEmployeeId = employeeId;

    var override = earningsOverrides.get(employeeId);
    var employeeName = getEmployeeName(employeeId);

    // Set modal title
    var titleEl = document.getElementById('editEarningsTitle');
    if (titleEl) {
        titleEl.textContent = 'Edit Earnings — ' + employeeName;
    }

    if (override) {
        populateModal(override.earningLines, true);
    } else {
        var originalLines = getOriginalEarningLines(employeeId);
        populateModal(originalLines, false);
    }

    document.getElementById('editEarningsModal').style.display = 'flex';
};

/**
 * Gets the employee name from the table row.
 * @param {number} employeeId
 * @returns {string}
 */
function getEmployeeName(employeeId) {
    var row = document.querySelector('tr[data-employee-id="' + employeeId + '"]');
    if (row) {
        var nameCell = row.querySelector('td:first-child strong');
        return nameCell ? nameCell.textContent : 'Employee';
    }
    return 'Employee';
}

/**
 * Populates the modal with earning line inputs.
 * @param {Array} earningLines - Array of earning line objects
 * @param {boolean} showReset - Whether to show the Reset to Default button
 */
function populateModal(earningLines, showReset) {
    var container = document.getElementById('earningsLinesContainer');
    container.innerHTML = '';

    if (earningLines.length === 0) {
        container.innerHTML = '<p style="color:#5a6a7a;font-size:14px;">No earning lines configured for this employee.</p>';
    }

    for (var i = 0; i < earningLines.length; i++) {
        var line = earningLines[i];
        var lineHtml = '<div class="earning-line-row" style="display:flex;gap:12px;align-items:flex-end;margin-bottom:14px;flex-wrap:wrap;">';
        lineHtml += '<div class="field" style="flex:1;min-width:160px;margin-bottom:0;">';
        lineHtml += '<label style="font-size:12px;font-weight:600;color:#364152;">' + escapeHtml(line.earningTypeName || line.EarningTypeName || 'Earning') + '</label>';
        if (line.description || line.Description) {
            lineHtml += '<span style="font-size:11px;color:#8a9bab;display:block;">' + escapeHtml(line.description || line.Description) + '</span>';
        }
        lineHtml += '</div>';
        lineHtml += '<div class="field" style="min-width:140px;margin-bottom:0;">';
        lineHtml += '<label style="font-size:11px;font-weight:600;color:#364152;">Amount (&euro;)</label>';
        lineHtml += '<input type="number" class="earning-amount-input" min="0" step="0.01" ';
        lineHtml += 'data-earning-type-id="' + (line.earningTypeId || line.Id || line.id || 0) + '" ';
        lineHtml += 'data-description="' + escapeHtml(line.description || line.Description || '') + '" ';
        lineHtml += 'data-earning-type-name="' + escapeHtml(line.earningTypeName || line.EarningTypeName || '') + '" ';
        lineHtml += 'data-overtime-multiplier="' + (line.overtimeMultiplier || line.OvertimeMultiplier || '') + '" ';
        lineHtml += 'data-overtime-hours="' + (line.overtimeHours || line.OvertimeHours || '') + '" ';
        lineHtml += 'value="' + (line.amount !== undefined ? line.amount : (line.Amount !== undefined ? line.Amount : 0)) + '" ';
        lineHtml += 'oninput="validateEarningInput(this)" />';
        lineHtml += '<div class="earning-validation-error" style="display:none;font-size:11px;color:#C24A4A;margin-top:4px;"></div>';
        lineHtml += '</div>';
        lineHtml += '</div>';
        container.innerHTML += lineHtml;
    }

    // Show/hide Reset to Default button
    var resetBtn = document.getElementById('btnResetToDefault');
    if (resetBtn) {
        resetBtn.style.display = showReset ? 'inline-flex' : 'none';
    }
}

/**
 * Escapes HTML characters to prevent XSS.
 * @param {string} str
 * @returns {string}
 */
function escapeHtml(str) {
    if (!str) return '';
    var div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

/* --------------------------------------------------------------------------
   10.3 — Validation and Save Flow
   -------------------------------------------------------------------------- */

/**
 * Validates an earning amount input field.
 * @param {HTMLInputElement} input
 */
window.validateEarningInput = function (input) {
    var errorDiv = input.parentElement.querySelector('.earning-validation-error');
    var value = input.value.trim();

    if (value === '') {
        // Treat empty as zero — valid
        if (errorDiv) {
            errorDiv.style.display = 'none';
            errorDiv.textContent = '';
        }
        input.style.borderColor = '';
        return true;
    }

    var numValue = parseFloat(value);
    if (isNaN(numValue) || !isFinite(numValue)) {
        if (errorDiv) {
            errorDiv.textContent = 'Please enter a valid number.';
            errorDiv.style.display = 'block';
        }
        input.style.borderColor = '#C24A4A';
        return false;
    }

    if (numValue < 0) {
        if (errorDiv) {
            errorDiv.textContent = 'Amount cannot be negative.';
            errorDiv.style.display = 'block';
        }
        input.style.borderColor = '#C24A4A';
        return false;
    }

    // Valid
    if (errorDiv) {
        errorDiv.style.display = 'none';
        errorDiv.textContent = '';
    }
    input.style.borderColor = '';
    return true;
};

/**
 * Saves the earnings override — validates, calls recalculation endpoint,
 * updates the Map, row, and summary cards.
 */
window.saveEarningsOverride = async function () {
    // Validate all inputs
    var inputs = document.querySelectorAll('#earningsLinesContainer .earning-amount-input');
    var allValid = true;
    var earningLines = [];

    for (var i = 0; i < inputs.length; i++) {
        var input = inputs[i];
        if (!validateEarningInput(input)) {
            allValid = false;
        }

        var amount = input.value.trim() === '' ? 0 : parseFloat(input.value);
        earningLines.push({
            earningTypeId: parseInt(input.getAttribute('data-earning-type-id')) || 0,
            description: input.getAttribute('data-description') || null,
            amount: amount,
            overtimeMultiplier: input.getAttribute('data-overtime-multiplier') ? parseFloat(input.getAttribute('data-overtime-multiplier')) : null,
            overtimeHours: input.getAttribute('data-overtime-hours') ? parseFloat(input.getAttribute('data-overtime-hours')) : null
        });
    }

    if (!allValid) {
        return;
    }

    // Call recalculation endpoint
    BlockUI.show('Recalculating...');

    try {
        var response = await fetch('/Payroll/AxPostRecalculateEmployee', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify({
                employeeId: currentModalEmployeeId,
                periodId: batchPeriodId,
                earningLines: earningLines
            })
        });

        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            // Store override in Map
            earningsOverrides.set(currentModalEmployeeId, {
                earningLines: earningLines.map(function (line, idx) {
                    return {
                        earningTypeId: line.earningTypeId,
                        description: line.description,
                        amount: line.amount,
                        overtimeMultiplier: line.overtimeMultiplier,
                        overtimeHours: line.overtimeHours,
                        earningTypeName: inputs[idx].getAttribute('data-earning-type-name') || 'Earning',
                        EarningTypeName: inputs[idx].getAttribute('data-earning-type-name') || 'Earning'
                    };
                }),
                result: {
                    totalEarnings: data.totalEarnings,
                    totalEmployeeDeductions: data.totalEmployeeDeductions,
                    netSalary: data.netSalary,
                    totalEmployerContributions: data.totalEmployerContributions
                }
            });

            // Update the employee row
            updateEmployeeRow(currentModalEmployeeId, data);

            // Update summary cards
            updateSummaryCards();

            // Close modal
            closeEarningsModal();
        } else {
            Swal.fire({
                icon: 'error',
                title: 'Recalculation Failed',
                text: data.error || data.message || 'An unexpected error occurred.',
                confirmButtonColor: '#0D5EA6'
            });
        }
    } catch (ex) {
        BlockUI.hide();
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'An unexpected error occurred.',
            confirmButtonColor: '#0D5EA6'
        });
    }
};

/**
 * Updates the employee row in the table with recalculated values.
 * @param {number} employeeId
 * @param {object} data - Recalculation result
 */
function updateEmployeeRow(employeeId, data) {
    var row = document.querySelector('tr[data-employee-id="' + employeeId + '"]');
    if (!row) return;

    var cells = row.querySelectorAll('td');
    // Column order: Employee, Department, Total Earnings, Deductions, Net Salary, Employer Cost, Actions
    if (cells.length >= 6) {
        cells[2].innerHTML = '&euro;' + parseFloat(data.totalEarnings).toFixed(2);
        cells[3].innerHTML = '&euro;' + parseFloat(data.totalEmployeeDeductions).toFixed(2);
        cells[4].innerHTML = '&euro;' + parseFloat(data.netSalary).toFixed(2);
        cells[5].innerHTML = '&euro;' + parseFloat(data.totalEmployerContributions).toFixed(2);
    }

    // Add modified indicator
    if (!row.classList.contains('modified')) {
        row.classList.add('modified');
        // Add badge to first cell
        var nameCell = cells[0];
        if (nameCell && !nameCell.querySelector('.badge-modified')) {
            nameCell.innerHTML += ' <span class="badge-modified" style="display:inline-block;font-size:10px;font-weight:700;background:#C8912E;color:#fff;padding:2px 7px;border-radius:6px;margin-left:6px;vertical-align:middle;">Modified</span>';
        }
    }
}

/**
 * Updates the summary cards (totals row and top cards) based on current table data.
 */
function updateSummaryCards() {
    var rows = document.querySelectorAll('table.data-table tbody tr[data-employee-id]');
    var totalEarnings = 0;
    var totalDeductions = 0;
    var totalNet = 0;
    var totalEmployerCost = 0;

    for (var i = 0; i < rows.length; i++) {
        var cells = rows[i].querySelectorAll('td');
        if (cells.length >= 6) {
            totalEarnings += parseAmount(cells[2].textContent);
            totalDeductions += parseAmount(cells[3].textContent);
            totalNet += parseAmount(cells[4].textContent);
            totalEmployerCost += parseAmount(cells[5].textContent);
        }
    }

    // Update totals row
    var totalsRow = document.querySelector('tr.totals-row');
    if (totalsRow) {
        var totalCells = totalsRow.querySelectorAll('td');
        if (totalCells.length >= 6) {
            totalCells[2].innerHTML = '<strong>&euro;' + totalEarnings.toFixed(2) + '</strong>';
            totalCells[3].innerHTML = '<strong>&euro;' + totalDeductions.toFixed(2) + '</strong>';
            totalCells[4].innerHTML = '<strong>&euro;' + totalNet.toFixed(2) + '</strong>';
            totalCells[5].innerHTML = '<strong>&euro;' + totalEmployerCost.toFixed(2) + '</strong>';
        }
    }

    // Update summary cards at top of page
    var summaryCards = document.querySelectorAll('.glass.card-pad p[style*="font-size:24px"]');
    if (summaryCards.length >= 2) {
        // Total Payroll Cost = sum of Net Salary (second card)
        summaryCards[1].innerHTML = '&euro;' + totalNet.toFixed(2);
        // Total Employer Contributions (third card)
        if (summaryCards.length >= 3) {
            summaryCards[2].innerHTML = '&euro;' + totalEmployerCost.toFixed(2);
        }
    }
}

/**
 * Parses a currency amount from a table cell text (e.g., "€1,234.56" → 1234.56).
 * @param {string} text
 * @returns {number}
 */
function parseAmount(text) {
    if (!text) return 0;
    var cleaned = text.replace(/[€\s,]/g, '');
    var val = parseFloat(cleaned);
    return isNaN(val) ? 0 : val;
}

/* --------------------------------------------------------------------------
   10.4 — Cancel Button Behavior
   -------------------------------------------------------------------------- */

/**
 * Closes the earnings modal without saving changes.
 */
window.closeEarningsModal = function () {
    currentModalEmployeeId = null;
    document.getElementById('editEarningsModal').style.display = 'none';
};

/**
 * Resets an employee's override back to original default earning lines.
 */
window.resetToDefault = function () {
    if (!currentModalEmployeeId) return;

    // Remove override from Map
    earningsOverrides.delete(currentModalEmployeeId);

    // Remove modified indicator from row
    var row = document.querySelector('tr[data-employee-id="' + currentModalEmployeeId + '"]');
    if (row) {
        row.classList.remove('modified');
        var badge = row.querySelector('.badge-modified');
        if (badge) badge.remove();
    }

    // Recalculate with original defaults by calling server
    var originalLines = getOriginalEarningLines(currentModalEmployeeId);

    // Convert original lines to API format
    var earningLines = originalLines.map(function (line) {
        return {
            earningTypeId: line.earningTypeId || line.Id || line.id || 0,
            description: line.description || line.Description || null,
            amount: line.amount !== undefined ? line.amount : (line.Amount !== undefined ? line.Amount : 0),
            overtimeMultiplier: line.overtimeMultiplier || line.OvertimeMultiplier || null,
            overtimeHours: line.overtimeHours || line.OvertimeHours || null
        };
    });

    BlockUI.show('Resetting to defaults...');

    fetch('/Payroll/AxPostRecalculateEmployee', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': getAntiForgeryToken()
        },
        body: JSON.stringify({
            employeeId: currentModalEmployeeId,
            periodId: batchPeriodId,
            earningLines: earningLines
        })
    }).then(function (response) {
        return response.json();
    }).then(function (data) {
        BlockUI.hide();
        if (data.success) {
            updateEmployeeRow(currentModalEmployeeId, data);
            updateSummaryCards();
        }
        closeEarningsModal();
    }).catch(function () {
        BlockUI.hide();
        closeEarningsModal();
    });
};

/* --------------------------------------------------------------------------
   10.5 — Confirm Batch with Overrides
   -------------------------------------------------------------------------- */

/**
 * Overrides the existing confirmGenerate function to support earnings overrides.
 * If overrides exist, calls AxPostConfirmBatchWithOverrides; otherwise uses standard confirm.
 */
window.confirmGenerate = function () {
    var hasOverrides = earningsOverrides.size > 0;
    var employeeCount = document.querySelectorAll('table.data-table tbody tr[data-employee-id]').length;

    var confirmText = hasOverrides
        ? 'This will generate payslips for ' + employeeCount + ' employees with ' + earningsOverrides.size + ' override(s) applied, and move the period to Preview status.'
        : 'This will generate payslips for ' + employeeCount + ' employees and move the period to Preview status.';

    Swal.fire({
        title: 'Confirm Generation?',
        text: confirmText,
        icon: 'info',
        showCancelButton: true,
        confirmButtonText: 'Yes, generate',
        cancelButtonText: 'Cancel',
        confirmButtonColor: '#0D5EA6'
    }).then(async function (result) {
        if (!result.isConfirmed) return;

        BlockUI.show('Generating payslips...');

        try {
            var response;

            if (hasOverrides) {
                // Build overrides payload
                var overrides = [];
                earningsOverrides.forEach(function (value, key) {
                    overrides.push({
                        employeeId: key,
                        earningLines: value.earningLines.map(function (line) {
                            return {
                                earningTypeId: line.earningTypeId,
                                description: line.description,
                                amount: line.amount,
                                overtimeMultiplier: line.overtimeMultiplier,
                                overtimeHours: line.overtimeHours
                            };
                        })
                    });
                });

                response = await fetch('/Payroll/AxPostConfirmBatchWithOverrides', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    body: JSON.stringify({
                        periodId: batchPeriodId,
                        overrides: overrides
                    })
                });
            } else {
                // Standard confirm (no overrides)
                response = await fetch('/Payroll/AxPostConfirmBatch?periodId=' + batchPeriodId, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': getAntiForgeryToken()
                    }
                });
            }

            var data = await response.json();
            BlockUI.hide();

            if (data.success) {
                Swal.fire({
                    icon: 'success',
                    title: 'Done',
                    text: data.message,
                    confirmButtonColor: '#0D5EA6'
                }).then(function () {
                    window.location.href = '/Payroll/PeriodDetail?id=' + batchPeriodId;
                });
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: data.message,
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } catch (ex) {
            BlockUI.hide();
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'An unexpected error occurred.',
                confirmButtonColor: '#0D5EA6'
            });
        }
    });
};
