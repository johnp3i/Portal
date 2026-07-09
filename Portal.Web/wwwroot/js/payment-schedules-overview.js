/* ==========================================================================
   Payment Schedules Overview — KPI rendering, monthly timeline, table with
   client-side filtering and pagination.
   Depends on: BlockUI (block-ui.js), SweetAlert2 (Swal)
   ========================================================================== */

(function () {
    'use strict';

    // =========================================================================
    // State
    // =========================================================================
    var state = {
        data: null,             // Full response from API
        filteredSchedules: [],  // Schedules after filter applied
        currentPage: 1,
        pageSize: 10,
        selectedYear: null      // Currently selected year in timeline
    };

    // =========================================================================
    // Initialization
    // =========================================================================
    document.addEventListener('DOMContentLoaded', function () {
        loadOverviewData();
        bindEvents();
    });

    // =========================================================================
    // Data Loading
    // =========================================================================
    function loadOverviewData() {
        BlockUI.show('Loading payment schedules...');

        fetch('/Revenue/AxGetPaymentSchedulesOverview', {
            method: 'GET',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                BlockUI.hide();

                if (!result.success) {
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: result.message || 'Failed to load payment schedules.',
                        confirmButtonColor: '#0D5EA6'
                    });
                    return;
                }

                state.data = result.data;
                state.filteredSchedules = state.data.schedules.slice();
                state.selectedYear = getCurrentYearFromAvailable();
                state.currentPage = 1;

                renderKpis();
                renderTimeline();
                renderTable();
                renderPagination();
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: 'An unexpected error occurred loading payment schedules.',
                    confirmButtonColor: '#0D5EA6'
                });
            });
    }

    // =========================================================================
    // Event Binding
    // =========================================================================
    function bindEvents() {
        var btnFilter = document.getElementById('btnFilter');
        var btnClear = document.getElementById('btnClear');

        if (btnFilter) {
            btnFilter.addEventListener('click', applyFilters);
        }
        if (btnClear) {
            btnClear.addEventListener('click', clearFilters);
        }
    }

    // =========================================================================
    // KPI Rendering
    // =========================================================================
    function renderKpis() {
        var container = document.getElementById('kpiStrip');
        if (!container || !state.data) return;

        var kpis = state.data.kpis;
        var cs = state.data.currencySymbol || '€';

        var cards = [
            { label: 'Total Scheduled', value: kpis.totalScheduled, cssClass: 'blue' },
            { label: 'Collected', value: kpis.collected, cssClass: 'green' },
            { label: 'Due This Month', value: kpis.dueThisMonth, cssClass: 'amber' },
            { label: 'Overdue', value: kpis.overdue, cssClass: 'red' }
        ];

        var html = '';
        for (var i = 0; i < cards.length; i++) {
            var card = cards[i];
            html += '<div class="kpi-card ' + card.cssClass + '">';
            html += '<div class="kpi-label">' + card.label + '</div>';
            html += '<div class="kpi-value">' + cs + formatAmount(card.value) + '</div>';
            html += '</div>';
        }

        container.innerHTML = html;
    }

    // =========================================================================
    // Timeline Rendering
    // =========================================================================
    function renderTimeline() {
        var container = document.getElementById('timelineContainer');
        if (!container || !state.data) return;

        var timeline = state.data.timeline;
        var availableYears = state.data.availableYears || [];
        var cs = state.data.currencySymbol || '€';

        // Header with year selector
        var html = '<div class="timeline-header">';
        html += '<h3>Monthly Payment Plan</h3>';
        html += '<div class="year-selector">';

        for (var i = 0; i < availableYears.length; i++) {
            var year = availableYears[i];
            var isActive = year === state.selectedYear;
            html += '<button class="year-btn' + (isActive ? ' active' : '') + '" data-year="' + year + '">' + year + '</button>';
        }

        html += '</div></div>';

        // Filter timeline entries by selected year
        var filteredEntries = [];
        for (var j = 0; j < timeline.length; j++) {
            var entry = timeline[j];
            if (entry.isNoDueDate || entry.year === state.selectedYear) {
                filteredEntries.push(entry);
            }
        }

        // Find max amount for proportional bar width
        var maxAmount = 0;
        for (var k = 0; k < filteredEntries.length; k++) {
            if (filteredEntries[k].totalAmount > maxAmount) {
                maxAmount = filteredEntries[k].totalAmount;
            }
        }

        // Render timeline rows
        if (filteredEntries.length === 0) {
            html += '<div class="empty-state"><p>No payment timeline data for the selected year.</p></div>';
        } else {
            for (var m = 0; m < filteredEntries.length; m++) {
                var row = filteredEntries[m];
                var barWidth = maxAmount > 0 ? ((row.totalAmount / maxAmount) * 100) : 0;
                var isOverdue = row.hasOverdue && !row.isNoDueDate;
                var isNoDate = row.isNoDueDate;

                var monthClass = 'timeline-month';
                if (isOverdue) monthClass += ' overdue';
                if (isNoDate) monthClass += ' no-date';

                var barClass = 'timeline-bar';
                if (isOverdue) barClass += ' overdue';
                if (isNoDate) barClass += ' no-date';

                var monthLabel = row.monthName;
                if (isOverdue) monthLabel += ' (overdue)';

                html += '<div class="timeline-row">';
                html += '<div class="' + monthClass + '">' + escapeHtml(monthLabel) + '</div>';
                html += '<div class="timeline-bar-wrap"><div class="' + barClass + '" style="width:' + barWidth.toFixed(1) + '%;"></div></div>';
                html += '<div class="timeline-amount">' + cs + formatAmount(row.totalAmount) + '</div>';
                html += '<div class="timeline-count">' + row.instalmentCount + ' instalment' + (row.instalmentCount !== 1 ? 's' : '') + '</div>';
                html += '</div>';
            }
        }

        container.innerHTML = html;

        // Bind year selector clicks
        var yearButtons = container.querySelectorAll('.year-btn');
        for (var n = 0; n < yearButtons.length; n++) {
            yearButtons[n].addEventListener('click', function () {
                state.selectedYear = parseInt(this.getAttribute('data-year'), 10);
                renderTimeline();
            });
        }
    }

    // =========================================================================
    // Table Rendering
    // =========================================================================
    function renderTable() {
        var container = document.getElementById('tableContainer');
        if (!container) return;

        var schedules = getPagedSchedules();
        var cs = state.data ? (state.data.currencySymbol || '€') : '€';

        if (state.filteredSchedules.length === 0) {
            container.innerHTML = '<div class="empty-state"><p>No active payment schedules found.</p></div>';
            return;
        }

        var html = '<table>';
        html += '<thead><tr>';
        html += '<th>Invoice</th>';
        html += '<th>Customer</th>';
        html += '<th>Schedule Total</th>';
        html += '<th>Paid</th>';
        html += '<th>Remaining</th>';
        html += '<th>Next Due</th>';
        html += '<th>Progress</th>';
        html += '<th>Status</th>';
        html += '</tr></thead>';
        html += '<tbody>';

        for (var i = 0; i < schedules.length; i++) {
            var s = schedules[i];
            html += '<tr>';
            html += '<td><a class="invoice-link" href="/Revenue/InvoiceDetail/' + s.invoiceId + '">' + escapeHtml(s.invoiceNumber) + '</a></td>';
            html += '<td>' + escapeHtml(s.customerName) + '</td>';
            html += '<td>' + cs + formatAmount(s.scheduleTotal) + '</td>';
            html += '<td>' + cs + formatAmount(s.paid) + '</td>';
            html += '<td>' + cs + formatAmount(s.remaining) + '</td>';
            html += '<td>' + escapeHtml(s.nextDue || '—') + '</td>';
            html += '<td><div class="progress-cell"><div class="progress-bar-mini"><div class="fill" style="width:' + s.progressPercentage + '%;"></div></div><span class="progress-pct">' + s.progressPercentage + '%</span></div></td>';
            html += '<td>' + renderStatusBadge(s.status) + '</td>';
            html += '</tr>';
        }

        html += '</tbody></table>';
        container.innerHTML = html;
    }

    function renderStatusBadge(status) {
        if (status === 'On Track') {
            return '<span class="badge-green">On Track</span>';
        } else if (status === 'Has Overdue') {
            return '<span class="badge-red">Has Overdue</span>';
        } else if (status === 'Completed') {
            return '<span class="badge-grey">Completed</span>';
        }
        return '<span class="badge-grey">' + escapeHtml(status) + '</span>';
    }

    // =========================================================================
    // Pagination
    // =========================================================================
    function renderPagination() {
        var infoEl = document.getElementById('paginationInfo');
        var controlsEl = document.getElementById('paginationControls');
        if (!infoEl || !controlsEl) return;

        var total = state.filteredSchedules.length;
        var totalPages = Math.ceil(total / state.pageSize);

        if (total === 0) {
            infoEl.textContent = '';
            controlsEl.innerHTML = '';
            return;
        }

        var start = ((state.currentPage - 1) * state.pageSize) + 1;
        var end = Math.min(state.currentPage * state.pageSize, total);
        infoEl.textContent = 'Showing ' + start + '–' + end + ' of ' + total;

        var html = '';
        for (var p = 1; p <= totalPages; p++) {
            if (p === state.currentPage) {
                html += '<button class="page-btn active">' + p + '</button>';
            } else {
                html += '<button class="page-btn" data-page="' + p + '">' + p + '</button>';
            }
        }
        controlsEl.innerHTML = html;

        // Bind page button clicks
        var pageButtons = controlsEl.querySelectorAll('.page-btn[data-page]');
        for (var i = 0; i < pageButtons.length; i++) {
            pageButtons[i].addEventListener('click', function () {
                state.currentPage = parseInt(this.getAttribute('data-page'), 10);
                renderTable();
                renderPagination();
            });
        }
    }

    function getPagedSchedules() {
        var start = (state.currentPage - 1) * state.pageSize;
        var end = start + state.pageSize;
        return state.filteredSchedules.slice(start, end);
    }

    // =========================================================================
    // Client-Side Filtering
    // =========================================================================
    function applyFilters() {
        if (!state.data) return;

        var statusFilter = document.getElementById('filterStatus').value;
        var invoiceFilter = document.getElementById('filterInvoice').value.trim().toLowerCase();
        var customerFilter = document.getElementById('filterCustomer').value.trim().toLowerCase();

        var all = state.data.schedules;
        var filtered = [];

        for (var i = 0; i < all.length; i++) {
            var schedule = all[i];

            // Status filter
            if (statusFilter !== 'All' && schedule.status !== statusFilter) {
                continue;
            }

            // Invoice number filter (case-insensitive contains)
            if (invoiceFilter && schedule.invoiceNumber.toLowerCase().indexOf(invoiceFilter) === -1) {
                continue;
            }

            // Customer name filter (case-insensitive contains)
            if (customerFilter && schedule.customerName.toLowerCase().indexOf(customerFilter) === -1) {
                continue;
            }

            filtered.push(schedule);
        }

        state.filteredSchedules = filtered;
        state.currentPage = 1;
        renderTable();
        renderPagination();
    }

    function clearFilters() {
        document.getElementById('filterStatus').value = 'All';
        document.getElementById('filterInvoice').value = '';
        document.getElementById('filterCustomer').value = '';

        if (state.data) {
            state.filteredSchedules = state.data.schedules.slice();
        }
        state.currentPage = 1;
        renderTable();
        renderPagination();
    }

    // =========================================================================
    // Utility Helpers
    // =========================================================================
    function getCurrentYearFromAvailable() {
        if (!state.data || !state.data.availableYears || state.data.availableYears.length === 0) {
            return new Date().getFullYear();
        }

        var currentYear = new Date().getFullYear();
        var years = state.data.availableYears;

        // If current year is in the list, use it
        for (var i = 0; i < years.length; i++) {
            if (years[i] === currentYear) return currentYear;
        }

        // Otherwise default to the first available year
        return years[0];
    }

    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

    function formatAmount(value) {
        if (value == null) return '0.00';
        return Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

})();
