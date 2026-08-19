/**
 * Sales Insights — Date filtering, AJAX metrics loading, metric card rendering
 * Depends on: BlockUI (block-ui.js), SweetAlert2
 */
(function () {
    'use strict';

    // =========================================================================
    // State
    // =========================================================================
    var currentStartDate = null;
    var currentEndDate = null;

    // =========================================================================
    // Initialization
    // =========================================================================
    document.addEventListener('DOMContentLoaded', function () {
        setDefaultDateRange();
        bindDateFilterButtons();
        bindCustomDateInputs();
        loadMetrics();
    });

    // =========================================================================
    // Date Range Helpers
    // =========================================================================
    function setDefaultDateRange() {
        var now = new Date();
        var firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
        currentStartDate = formatDate(firstDay);
        currentEndDate = formatDate(now);

        // Set "This Month" as active by default
        var defaultBtn = document.querySelector('[data-period="ThisMonth"]');
        if (defaultBtn) defaultBtn.classList.add('active');
    }

    function formatDate(date) {
        var year = date.getFullYear();
        var month = String(date.getMonth() + 1).padStart(2, '0');
        var day = String(date.getDate()).padStart(2, '0');
        return year + '-' + month + '-' + day;
    }

    function getDateRangeForPeriod(period) {
        var now = new Date();
        var start, end;

        switch (period) {
            case 'ThisWeek':
                var dayOfWeek = now.getDay();
                var diff = dayOfWeek === 0 ? 6 : dayOfWeek - 1; // Monday as first day
                start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - diff);
                end = now;
                break;
            case 'ThisMonth':
                start = new Date(now.getFullYear(), now.getMonth(), 1);
                end = now;
                break;
            case 'LastMonth':
                start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
                end = new Date(now.getFullYear(), now.getMonth(), 0);
                break;
            case 'ThisQuarter':
                var quarterMonth = Math.floor(now.getMonth() / 3) * 3;
                start = new Date(now.getFullYear(), quarterMonth, 1);
                end = now;
                break;
            case 'Last6Months':
                start = new Date(now.getFullYear(), now.getMonth() - 6, 1);
                end = now;
                break;
            case 'ThisYear':
                start = new Date(now.getFullYear(), 0, 1);
                end = now;
                break;
            default:
                start = new Date(now.getFullYear(), now.getMonth(), 1);
                end = now;
                break;
        }

        return { startDate: formatDate(start), endDate: formatDate(end) };
    }

    // =========================================================================
    // Date Filter Buttons
    // =========================================================================
    function bindDateFilterButtons() {
        var buttons = document.querySelectorAll('[data-period]');
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].addEventListener('click', function () {
                var period = this.getAttribute('data-period');

                // Update active state
                var allButtons = document.querySelectorAll('[data-period]');
                for (var j = 0; j < allButtons.length; j++) {
                    allButtons[j].classList.remove('active');
                }
                this.classList.add('active');

                if (period === 'Custom') {
                    showCustomDateInputs();
                    return;
                }

                hideCustomDateInputs();
                var range = getDateRangeForPeriod(period);
                currentStartDate = range.startDate;
                currentEndDate = range.endDate;
                loadMetrics();
            });
        }
    }

    // =========================================================================
    // Custom Date Range
    // =========================================================================
    function bindCustomDateInputs() {
        var applyBtn = document.getElementById('btnApplyCustomRange');
        if (applyBtn) {
            applyBtn.addEventListener('click', function () {
                var startInput = document.getElementById('insightsStartDate');
                var endInput = document.getElementById('insightsEndDate');

                if (!startInput || !endInput) return;

                var startVal = startInput.value;
                var endVal = endInput.value;

                if (!startVal || !endVal) {
                    Swal.fire({ icon: 'warning', title: 'Validation', text: 'Please select both start and end dates.', confirmButtonColor: '#0D5EA6' });
                    return;
                }

                if (startVal > endVal) {
                    Swal.fire({ icon: 'warning', title: 'Invalid Date Range', text: 'Start date must be before or equal to end date.', confirmButtonColor: '#0D5EA6' });
                    return;
                }

                currentStartDate = startVal;
                currentEndDate = endVal;
                loadMetrics();
            });
        }
    }

    function showCustomDateInputs() {
        var container = document.getElementById('customRange');
        if (container) container.style.display = 'flex';
    }

    function hideCustomDateInputs() {
        var container = document.getElementById('customRange');
        if (container) container.style.display = 'none';
    }

    // =========================================================================
    // AJAX — Load Metrics
    // =========================================================================
    function loadMetrics() {
        if (!currentStartDate || !currentEndDate) return;

        var url = '/Sales/AxGetInsightsMetrics?startDate=' + encodeURIComponent(currentStartDate) + '&endDate=' + encodeURIComponent(currentEndDate);

        BlockUI.show('Loading insights...');

        fetch(url, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                BlockUI.hide();
                if (result.success) {
                    renderMetrics(result.data);
                } else {
                    Swal.fire({ icon: 'error', title: 'Error', text: result.message || 'Failed to load insights.', confirmButtonColor: '#0D5EA6' });
                }
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred loading insights.', confirmButtonColor: '#0D5EA6' });
            });
    }

    // =========================================================================
    // Render Metrics
    // =========================================================================
    function renderMetrics(data) {
        if (!data) return;

        renderMetricCard('newLeadsCount', data.newLeadsCount, null);
        renderMetricCard('responseSla', data.responseSlaPercentage, getSlaColour(data.responseSlaPercentage));
        renderMetricCard('demoConversion', data.demoConversionRate, getConversionColour(data.demoConversionRate));
        renderMetricCard('proposalConversion', data.proposalConversionRate, getConversionColour(data.proposalConversionRate));
        renderMetricCard('winRate', data.winRate, getWinRateColour(data.winRate));
        renderMetricCard('avgSalesCycle', data.averageSalesCycleDays, null);

        renderRevenueList('revenueByProduct', data.revenueByProduct);
        renderRevenueList('revenueBySource', data.revenueBySource);
    }

    function renderMetricCard(elementId, value, colour) {
        var el = document.getElementById(elementId);
        if (!el) return;

        var valueEl = el.querySelector('[data-value]');
        if (!valueEl) return;

        if (value === null || value === undefined) {
            valueEl.textContent = 'No data';
            valueEl.style.color = '#8a9bab';
        } else {
            // Format based on card type
            if (elementId === 'newLeadsCount') {
                valueEl.textContent = value;
            } else if (elementId === 'avgSalesCycle') {
                valueEl.textContent = Math.round(value) + ' days';
            } else {
                valueEl.textContent = value.toFixed(1) + '%';
            }

            valueEl.style.color = colour || '#0B1B28';
        }
    }

    // =========================================================================
    // Colour Thresholds
    // =========================================================================
    function getSlaColour(value) {
        if (value === null || value === undefined) return null;
        if (value >= 80) return '#129867';  // green
        if (value >= 50) return '#C8912E';  // amber
        return '#C24A4A';                    // red
    }

    function getConversionColour(value) {
        if (value === null || value === undefined) return null;
        if (value >= 30) return '#129867';
        if (value >= 15) return '#C8912E';
        return '#C24A4A';
    }

    function getWinRateColour(value) {
        if (value === null || value === undefined) return null;
        if (value >= 30) return '#129867';
        if (value >= 15) return '#C8912E';
        return '#C24A4A';
    }

    // =========================================================================
    // Revenue Lists
    // =========================================================================
    function renderRevenueList(elementId, items) {
        var container = document.getElementById(elementId);
        if (!container) return;

        if (!items || items.length === 0) {
            container.innerHTML = '<p style="color:#8a9bab;font-size:13px;padding:12px 0;">No data available.</p>';
            return;
        }

        var html = '';
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            html += '<div style="display:flex;align-items:center;gap:12px;padding:10px 0;border-bottom:1px solid rgba(13,94,166,.06);">';
            html += '<div style="flex:1;min-width:0;">';
            html += '<div style="font-size:13px;font-weight:600;color:#0B1B28;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">' + escapeHtml(item.name) + '</div>';
            html += '<div style="margin-top:4px;height:6px;background:#EEF4F8;border-radius:3px;overflow:hidden;">';
            html += '<div style="height:100%;width:' + item.percentage.toFixed(1) + '%;background:#0D5EA6;border-radius:3px;"></div>';
            html += '</div>';
            html += '</div>';
            html += '<div style="font-size:13px;font-weight:700;color:#0B1B28;white-space:nowrap;">' + formatCurrency(item.totalRevenue) + '</div>';
            html += '<div style="font-size:12px;color:#5a6a7a;white-space:nowrap;">' + item.percentage.toFixed(1) + '%</div>';
            html += '</div>';
        }

        container.innerHTML = html;
    }

    // =========================================================================
    // Utilities
    // =========================================================================
    function formatCurrency(value) {
        if (value === null || value === undefined) return '—';
        return '€' + Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }
})();
