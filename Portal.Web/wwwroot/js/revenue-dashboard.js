/* ==========================================================================
   Revenue Dashboard — Chart initialization, table search & pagination
   Depends on: Chart.js v4 (loaded via CDN), BlockUI (block-ui.js)
   Inline script variables: revenueCollectedData, invoicedVsCollectedData,
                            vatLiabilityData, currencySymbol,
                            overdueCurrentPage, overdueTotalPages,
                            paymentsCurrentPage, paymentsTotalPages
   ========================================================================== */

(function () {
    'use strict';

    // =========================================================================
    // State
    // =========================================================================
    var overdueState = { page: overdueCurrentPage || 1, totalPages: overdueTotalPages || 1 };
    var paymentsState = { page: paymentsCurrentPage || 1, totalPages: paymentsTotalPages || 1 };

    // =========================================================================
    // Initialization
    // =========================================================================
    document.addEventListener('DOMContentLoaded', function () {
        initRevenueCollectedChart();
        initInvoicedVsCollectedChart();
        initVatLiabilityChart();
        renderOverduePagination();
        renderPaymentsPagination();
        bindSearchEnterKeys();
    });

    // =========================================================================
    // Chart: Revenue Collected (Line)
    // =========================================================================
    function initRevenueCollectedChart() {
        var ctx = document.getElementById('revenueLineChart');
        if (!ctx || typeof revenueCollectedData === 'undefined') return;

        new Chart(ctx, {
            type: 'line',
            data: {
                labels: revenueCollectedData.labels,
                datasets: [{
                    label: 'Revenue Collected',
                    data: revenueCollectedData.amounts,
                    borderColor: '#0D5EA6',
                    backgroundColor: 'rgba(13, 94, 166, 0.08)',
                    borderWidth: 2.5,
                    tension: 0.35,
                    fill: true,
                    pointBackgroundColor: '#0D5EA6',
                    pointRadius: 4,
                    pointHoverRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return currencySymbol + context.parsed.y.toLocaleString(undefined, { minimumFractionDigits: 2 });
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return currencySymbol + value.toLocaleString();
                            }
                        },
                        grid: { color: 'rgba(0,0,0,0.04)' }
                    },
                    x: {
                        grid: { display: false }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Chart: Invoiced vs Collected (Bar)
    // =========================================================================
    function initInvoicedVsCollectedChart() {
        var ctx = document.getElementById('invoicedVsCollectedChart');
        if (!ctx || typeof invoicedVsCollectedData === 'undefined') return;

        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: invoicedVsCollectedData.labels,
                datasets: [
                    {
                        label: 'Invoiced',
                        data: invoicedVsCollectedData.invoiced,
                        backgroundColor: 'rgba(87, 184, 232, 0.7)',
                        borderColor: '#57B8E8',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: 'Collected',
                        data: invoicedVsCollectedData.collected,
                        backgroundColor: 'rgba(18, 152, 103, 0.7)',
                        borderColor: '#129867',
                        borderWidth: 1,
                        borderRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, padding: 16, font: { size: 12 } }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return context.dataset.label + ': ' + currencySymbol + context.parsed.y.toLocaleString(undefined, { minimumFractionDigits: 2 });
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return currencySymbol + value.toLocaleString();
                            }
                        },
                        grid: { color: 'rgba(0,0,0,0.04)' }
                    },
                    x: {
                        grid: { display: false }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Chart: VAT Liability by Period (Bar)
    // =========================================================================
    function initVatLiabilityChart() {
        var ctx = document.getElementById('vatLiabilityChart');
        if (!ctx || typeof vatLiabilityData === 'undefined') return;

        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: vatLiabilityData.labels,
                datasets: [
                    {
                        label: 'Output VAT',
                        data: vatLiabilityData.output,
                        backgroundColor: 'rgba(87, 184, 232, 0.7)',
                        borderColor: '#57B8E8',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: 'Input VAT',
                        data: vatLiabilityData.input,
                        backgroundColor: 'rgba(18, 152, 103, 0.7)',
                        borderColor: '#129867',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: 'Net Payable',
                        data: vatLiabilityData.net,
                        backgroundColor: 'rgba(194, 74, 74, 0.7)',
                        borderColor: '#C24A4A',
                        borderWidth: 1,
                        borderRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, padding: 16, font: { size: 12 } }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return context.dataset.label + ': ' + currencySymbol + context.parsed.y.toLocaleString(undefined, { minimumFractionDigits: 2 });
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return currencySymbol + value.toLocaleString();
                            }
                        },
                        grid: { color: 'rgba(0,0,0,0.04)' }
                    },
                    x: {
                        grid: { display: false }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Overdue Invoices — Search & Pagination
    // =========================================================================
    function searchOverdueInvoices() {
        overdueState.page = 1;
        loadOverdueInvoices();
    }

    function loadOverdueInvoices() {
        var searchTerm = document.getElementById('overdueSearch').value.trim();
        var url = '/Revenue/GetOverdueInvoices?page=' + overdueState.page + '&pageSize=10';
        if (searchTerm) {
            url += '&search=' + encodeURIComponent(searchTerm);
        }

        BlockUI.show('Loading overdue invoices...');

        fetch(url, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) { return response.json(); })
            .then(function (data) {
                BlockUI.hide();
                if (data.success) {
                    overdueState.page = data.currentPage;
                    overdueState.totalPages = data.totalPages;
                    renderOverdueTable(data.data, data.totalCount);
                    renderOverduePagination();
                } else {
                    Swal.fire({ title: 'Error', text: data.message || 'Failed to load overdue invoices.', icon: 'error', confirmButtonColor: '#0D5EA6' });
                }
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' });
            });
    }

    function renderOverdueTable(items, totalCount) {
        var tbody = document.getElementById('overdueTableBody');
        if (!tbody) return;

        if (!items || items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;color:var(--muted);padding:24px;">No overdue invoices found.</td></tr>';
            return;
        }

        var html = '';
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            html += '<tr>';
            html += '<td><strong>' + escapeHtml(item.invoiceNumber) + '</strong></td>';
            html += '<td>' + escapeHtml(item.customerName) + '</td>';
            html += '<td>' + formatDate(item.dueDate) + '</td>';
            html += '<td><span class="pill pill-red">' + item.daysOverdue + ' days</span></td>';
            html += '<td style="text-align:right;font-weight:700;color:var(--red);">' + currencySymbol + formatAmount(item.outstandingBalance) + '</td>';
            html += '<td><a href="/Revenue/InvoiceDetail/' + item.id + '" style="color:var(--blue);font-weight:700;font-size:13px;">View</a></td>';
            html += '</tr>';
        }
        tbody.innerHTML = html;
    }

    function renderOverduePagination() {
        var infoEl = document.getElementById('overduePaginationInfo');
        var controlsEl = document.getElementById('overduePaginationControls');
        if (!infoEl || !controlsEl) return;

        // Info text
        var pageSize = 10;
        var start = overdueState.totalPages > 0 ? ((overdueState.page - 1) * pageSize) + 1 : 0;
        var end = overdueState.page * pageSize;

        // We don't have totalCount in state from initial render, so show page-based info
        infoEl.textContent = overdueState.totalPages > 0
            ? 'Page ' + overdueState.page + ' of ' + overdueState.totalPages
            : 'No overdue invoices';

        // Controls
        var html = '';
        if (overdueState.page > 1) {
            html += '<button class="btn btn-secondary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" onclick="goOverduePage(' + (overdueState.page - 1) + ')">← Prev</button>';
        }
        for (var p = 1; p <= overdueState.totalPages; p++) {
            if (p === overdueState.page) {
                html += '<button class="btn btn-primary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" disabled>' + p + '</button>';
            } else {
                html += '<button class="btn btn-secondary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" onclick="goOverduePage(' + p + ')">' + p + '</button>';
            }
        }
        if (overdueState.page < overdueState.totalPages) {
            html += '<button class="btn btn-secondary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" onclick="goOverduePage(' + (overdueState.page + 1) + ')">Next →</button>';
        }
        controlsEl.innerHTML = html;
    }

    function goOverduePage(page) {
        overdueState.page = page;
        loadOverdueInvoices();
    }

    // =========================================================================
    // Recent Payments — Search & Pagination
    // =========================================================================
    function searchRecentPayments() {
        paymentsState.page = 1;
        loadRecentPayments();
    }

    function loadRecentPayments() {
        var searchTerm = document.getElementById('paymentsSearch').value.trim();
        var url = '/Revenue/GetRecentPayments?page=' + paymentsState.page + '&pageSize=10';
        if (searchTerm) {
            url += '&search=' + encodeURIComponent(searchTerm);
        }

        BlockUI.show('Loading recent payments...');

        fetch(url, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) { return response.json(); })
            .then(function (data) {
                BlockUI.hide();
                if (data.success) {
                    paymentsState.page = data.currentPage;
                    paymentsState.totalPages = data.totalPages;
                    renderPaymentsTable(data.data, data.totalCount);
                    renderPaymentsPagination();
                } else {
                    Swal.fire({ title: 'Error', text: data.message || 'Failed to load recent payments.', icon: 'error', confirmButtonColor: '#0D5EA6' });
                }
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' });
            });
    }

    function renderPaymentsTable(items, totalCount) {
        var tbody = document.getElementById('paymentsTableBody');
        if (!tbody) return;

        if (!items || items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;color:var(--muted);padding:24px;">No recent payments found.</td></tr>';
            return;
        }

        var html = '';
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var amountColor = item.isFullPayment ? 'var(--green)' : 'var(--gold)';
            var pillClass = item.isFullPayment ? 'pill pill-green' : 'pill pill-gold';
            var pillText = item.isFullPayment ? 'Full Payment' : 'Partial';

            html += '<tr>';
            html += '<td>' + formatDate(item.paymentDateUtc) + '</td>';
            html += '<td><strong>' + escapeHtml(item.invoiceNumber) + '</strong></td>';
            html += '<td>' + escapeHtml(item.customerName) + '</td>';
            html += '<td>' + escapeHtml(item.paymentMethodName) + '</td>';
            html += '<td style="text-align:right;font-weight:700;color:' + amountColor + ';">' + currencySymbol + formatAmount(item.amount) + '</td>';
            html += '<td><span class="' + pillClass + '">' + pillText + '</span></td>';
            html += '</tr>';
        }
        tbody.innerHTML = html;
    }

    function renderPaymentsPagination() {
        var infoEl = document.getElementById('paymentsPaginationInfo');
        var controlsEl = document.getElementById('paymentsPaginationControls');
        if (!infoEl || !controlsEl) return;

        infoEl.textContent = paymentsState.totalPages > 0
            ? 'Page ' + paymentsState.page + ' of ' + paymentsState.totalPages
            : 'No payments';

        var html = '';
        if (paymentsState.page > 1) {
            html += '<button class="btn btn-secondary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" onclick="goPaymentsPage(' + (paymentsState.page - 1) + ')">← Prev</button>';
        }
        for (var p = 1; p <= paymentsState.totalPages; p++) {
            if (p === paymentsState.page) {
                html += '<button class="btn btn-primary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" disabled>' + p + '</button>';
            } else {
                html += '<button class="btn btn-secondary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" onclick="goPaymentsPage(' + p + ')">' + p + '</button>';
            }
        }
        if (paymentsState.page < paymentsState.totalPages) {
            html += '<button class="btn btn-secondary" style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;" onclick="goPaymentsPage(' + (paymentsState.page + 1) + ')">Next →</button>';
        }
        controlsEl.innerHTML = html;
    }

    function goPaymentsPage(page) {
        paymentsState.page = page;
        loadRecentPayments();
    }

    // =========================================================================
    // Utility Helpers
    // =========================================================================
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

    function formatDate(dateStr) {
        if (!dateStr) return '';
        var d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        var year = d.getFullYear();
        var month = String(d.getMonth() + 1).padStart(2, '0');
        var day = String(d.getDate()).padStart(2, '0');
        return year + '-' + month + '-' + day;
    }

    function bindSearchEnterKeys() {
        var overdueInput = document.getElementById('overdueSearch');
        if (overdueInput) {
            overdueInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    searchOverdueInvoices();
                }
            });
        }

        var paymentsInput = document.getElementById('paymentsSearch');
        if (paymentsInput) {
            paymentsInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    searchRecentPayments();
                }
            });
        }
    }

    // =========================================================================
    // Expose global functions (called from onclick attributes in the view)
    // =========================================================================
    window.searchOverdueInvoices = searchOverdueInvoices;
    window.searchRecentPayments = searchRecentPayments;
    window.goOverduePage = goOverduePage;
    window.goPaymentsPage = goPaymentsPage;

})();
