/* ==========================================================================
   Salary Register — Filter AJAX and Quick Edit Salary
   Handles filter dropdown changes (department/status) via AJAX table refresh,
   and inline quick-edit of base salary via SweetAlert2 modal.
   ========================================================================== */

/**
 * Gets the antiforgery token from the page.
 */
function getAntiForgeryToken() {
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

/* --------------------------------------------------------------------------
   11.1 — Filter Change → AJAX Table Refresh
   -------------------------------------------------------------------------- */

/**
 * Refreshes the salary register table based on selected filter values.
 * Called when Department or Status dropdown changes.
 */
window.refreshSalaryRegister = async function () {
    var departmentId = document.getElementById('filterDepartment').value;
    var isActive = document.getElementById('filterStatus').value;

    var url = '/Payroll/AxGetSalaryRegisterData?';
    var params = [];
    if (departmentId) params.push('departmentId=' + encodeURIComponent(departmentId));
    if (isActive !== '') params.push('isActive=' + encodeURIComponent(isActive));
    url += params.join('&');

    BlockUI.show('Loading...');

    try {
        var response = await fetch(url);
        var result = await response.json();
        BlockUI.hide();

        if (result.success) {
            renderSalaryTable(result.data, result.totalEmployees, result.totalMonthlyPayroll);
        } else {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: result.message || 'Failed to load salary data.',
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
 * Re-renders the salary register table body and summary with new data.
 * @param {Array} employees - Array of employee row objects
 * @param {number} totalEmployees - Total employee count
 * @param {number} totalMonthlyPayroll - Total monthly payroll amount
 */
function renderSalaryTable(employees, totalEmployees, totalMonthlyPayroll) {
    var container = document.getElementById('salaryRegisterTableContainer');
    if (!container) return;

    if (!employees || employees.length === 0) {
        container.innerHTML = '<div class="empty-state"><p>No employees match the selected filters.</p></div>';
        return;
    }

    var html = '<table class="data-table">';
    html += '<thead><tr>';
    html += '<th>Employee Name</th>';
    html += '<th data-mobile="hide">Department</th>';
    html += '<th data-mobile="hide">Salary Type</th>';
    html += '<th class="text-right">Base Salary (&euro;)</th>';
    html += '<th class="text-right" data-mobile="hide">Hourly Rate (&euro;)</th>';
    html += '<th>Status</th>';
    html += '<th>Actions</th>';
    html += '</tr></thead>';
    html += '<tbody id="salaryRegisterBody">';

    for (var i = 0; i < employees.length; i++) {
        var emp = employees[i];
        html += '<tr>';
        html += '<td><strong>' + escapeHtml(emp.employeeName) + '</strong></td>';
        html += '<td data-mobile="hide">' + escapeHtml(emp.departmentName || '—') + '</td>';
        html += '<td data-mobile="hide">' + escapeHtml(emp.salaryType) + '</td>';
        html += '<td class="text-right">';
        html += '<span class="salary-cell" style="cursor:pointer;color:#0D5EA6;font-weight:700;" ';
        html += 'data-employee-id="' + emp.employeeId + '" ';
        html += 'data-current-salary="' + emp.baseSalary + '" ';
        html += 'onclick="quickEditSalary(this)">';
        html += '&euro;' + parseFloat(emp.baseSalary).toFixed(2);
        html += '</span></td>';
        html += '<td class="text-right" data-mobile="hide">';
        if (emp.hourlyRate !== null && emp.hourlyRate !== undefined) {
            html += '<span>&euro;' + parseFloat(emp.hourlyRate).toFixed(2) + '</span>';
        } else {
            html += '<span>—</span>';
        }
        html += '</td>';
        html += '<td>';
        if (emp.isActive) {
            html += '<span class="pill pill-green">Active</span>';
        } else {
            html += '<span class="pill pill-grey">Inactive</span>';
        }
        html += '</td>';
        html += '<td class="table-actions">';
        html += '<a class="btn btn-sm btn-secondary" href="/Payroll/EmployeeForm/' + emp.employeeId + '">Edit</a>';
        html += '</td>';
        html += '</tr>';
    }

    html += '</tbody></table>';

    // Summary footer
    html += '<div id="salaryRegisterSummary" style="margin-top:18px;padding:12px 16px;background:rgba(13,94,166,0.04);border-radius:10px;font-size:14px;color:#364152;font-weight:600;">';
    html += 'Total: ' + totalEmployees + ' employees &middot; Monthly payroll: &euro;' + parseFloat(totalMonthlyPayroll).toFixed(2);
    html += ' <span style="font-weight:400;color:#5a6a7a;">(active monthly employees only)</span>';
    html += '</div>';

    container.innerHTML = html;
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
   11.2 — Quick Edit Salary via SweetAlert2
   -------------------------------------------------------------------------- */

/**
 * Opens a SweetAlert2 input modal to update an employee's base salary.
 * @param {HTMLElement} element - The clicked salary cell element
 */
window.quickEditSalary = function (element) {
    var employeeId = element.getAttribute('data-employee-id');
    var currentSalary = element.getAttribute('data-current-salary');

    Swal.fire({
        title: 'Update Base Salary',
        input: 'number',
        inputValue: currentSalary,
        inputAttributes: {
            min: '0.01',
            step: '0.01'
        },
        inputValidator: function (value) {
            if (!value || value.trim() === '') {
                return 'Please enter a salary amount.';
            }
            var numValue = parseFloat(value);
            if (isNaN(numValue) || !isFinite(numValue)) {
                return 'Please enter a valid number.';
            }
            if (numValue <= 0) {
                return 'Salary must be greater than zero.';
            }
            return null;
        },
        showCancelButton: true,
        confirmButtonText: 'Update',
        cancelButtonText: 'Cancel',
        confirmButtonColor: '#0D5EA6'
    }).then(async function (result) {
        if (!result.isConfirmed) return;

        BlockUI.show('Updating salary...');

        try {
            var response = await fetch('/Payroll/AxPostUpdateBaseSalary', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify({
                    employeeId: parseInt(employeeId),
                    newSalary: parseFloat(result.value)
                })
            });

            var data = await response.json();
            BlockUI.hide();

            if (data.success) {
                // Update cell in DOM
                element.textContent = '€' + parseFloat(result.value).toFixed(2);
                element.setAttribute('data-current-salary', result.value);

                // Update summary footer if visible
                updateSummaryAfterEdit();

                Swal.fire({
                    icon: 'success',
                    title: 'Updated',
                    text: data.message || 'Salary updated successfully.',
                    timer: 1500,
                    showConfirmButton: false
                });
            } else {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: data.message || 'Failed to update salary.',
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

/**
 * Recalculates and updates the summary footer after a quick edit.
 * Refreshes data from the server to ensure accurate totals.
 */
function updateSummaryAfterEdit() {
    // Refresh the register to get accurate server-side totals
    var departmentId = document.getElementById('filterDepartment').value;
    var isActive = document.getElementById('filterStatus').value;

    var url = '/Payroll/AxGetSalaryRegisterData?';
    var params = [];
    if (departmentId) params.push('departmentId=' + encodeURIComponent(departmentId));
    if (isActive !== '') params.push('isActive=' + encodeURIComponent(isActive));
    url += params.join('&');

    fetch(url).then(function (response) {
        return response.json();
    }).then(function (result) {
        if (result.success) {
            var summaryDiv = document.getElementById('salaryRegisterSummary');
            if (summaryDiv) {
                summaryDiv.innerHTML = 'Total: ' + result.totalEmployees + ' employees &middot; Monthly payroll: &euro;' + parseFloat(result.totalMonthlyPayroll).toFixed(2) + ' <span style="font-weight:400;color:#5a6a7a;">(active monthly employees only)</span>';
            }
        }
    }).catch(function () {
        // Silently fail — summary may be slightly stale, next filter change will fix
    });
}
