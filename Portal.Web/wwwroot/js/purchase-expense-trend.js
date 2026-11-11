/* ==========================================================================
   Purchases — Expense Trend (collapsible line chart)
   Plots spend over the selected period, grouped by expense category or supplier,
   reflecting the SAME filters as the Purchases list/KPIs. Collapsed by default.
   Depends on: Chart.js v4 (self-hosted at /js/vendor/chart.umd.min.js).
   ========================================================================== */
(function () {
    'use strict';

    var COLOURS = [
        '#0D5EA6', '#57B8E8', '#129867', '#C8912E', '#C24A4A',
        '#6366F1', '#8B5CF6', '#EC4899', '#14B8A6', '#F59E0B'
    ];
    var OTHER_COLOUR = '#94A3B8';
    var STORAGE_KEY = 'purchaseExpenseTrend_expanded';

    var section, body, canvas, toggleBtn, spinner, emptyState, chartInstance = null;
    var dimension = 'category';
    var loadedOnce = false;
    var currencySymbol = '\u20AC';

    document.addEventListener('DOMContentLoaded', function () {
        section = document.getElementById('expenseTrendSection');
        if (!section) return;

        body = document.getElementById('expenseTrendBody');
        canvas = document.getElementById('expenseTrendChart');
        toggleBtn = document.getElementById('expenseTrendToggle');
        spinner = document.getElementById('expenseTrendSpinner');
        emptyState = document.getElementById('expenseTrendEmpty');
        currencySymbol = section.getAttribute('data-currency') || '\u20AC';

        toggleBtn.addEventListener('click', onToggle);

        // Dimension toggle buttons.
        var dimBtns = section.querySelectorAll('[data-trend-dimension]');
        for (var i = 0; i < dimBtns.length; i++) {
            dimBtns[i].addEventListener('click', function () {
                var dim = this.getAttribute('data-trend-dimension');
                if (dim === dimension) return;
                dimension = dim;
                setActiveDimension();
                fetchTrend();
            });
        }

        // Restore expanded state (default collapsed).
        if (localStorage.getItem(STORAGE_KEY) === '1') {
            expand();
        }
    });

    function onToggle() {
        if (body.style.display === 'none' || body.style.display === '') {
            expand();
        } else {
            collapse();
        }
    }

    function expand() {
        body.style.display = 'block';
        section.classList.add('is-open');
        localStorage.setItem(STORAGE_KEY, '1');
        if (!loadedOnce) {
            fetchTrend();
        }
    }

    function collapse() {
        body.style.display = 'none';
        section.classList.remove('is-open');
        localStorage.setItem(STORAGE_KEY, '0');
    }

    function setActiveDimension() {
        var dimBtns = section.querySelectorAll('[data-trend-dimension]');
        for (var i = 0; i < dimBtns.length; i++) {
            var active = dimBtns[i].getAttribute('data-trend-dimension') === dimension;
            dimBtns[i].classList.toggle('active', active);
        }
    }

    // Build the AJAX URL from the current page filter (embedded as data-* on the section).
    function buildUrl() {
        var params = new URLSearchParams();
        var map = {
            supplierId: 'data-supplier-id',
            expenseCategoryId: 'data-expense-category-id',
            purchaseOriginTypeId: 'data-origin-type-id',
            purchaseTypeId: 'data-purchase-type-id',
            vatPeriodId: 'data-vat-period-id',
            dateFrom: 'data-date-from',
            dateTo: 'data-date-to',
            searchTerm: 'data-search-term'
        };
        for (var key in map) {
            var val = section.getAttribute(map[key]);
            if (val !== null && val !== '') params.append(key, val);
        }
        if (section.getAttribute('data-include-payroll') === 'true') {
            params.append('includePayroll', 'true');
        }
        params.append('dimension', dimension);
        return '/Purchase/AxGetExpenseTrend?' + params.toString();
    }

    function fetchTrend() {
        showSpinner(true);
        hideEmpty();
        fetch(buildUrl(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                showSpinner(false);
                loadedOnce = true;
                if (!result || !result.success || !result.data || !result.data.series || result.data.series.length === 0) {
                    renderEmpty();
                    return;
                }
                render(result.data);
            })
            .catch(function () {
                showSpinner(false);
                loadedOnce = true;
                renderError();
            });
    }

    function render(data) {
        hideEmpty();
        canvas.style.display = 'block';

        var datasets = data.series.map(function (s, i) {
            var colour = s.name === 'Other' ? OTHER_COLOUR : COLOURS[i % COLOURS.length];
            return {
                label: s.name,
                data: s.data,
                borderColor: colour,
                backgroundColor: colour,
                borderWidth: 2,
                pointRadius: 2,
                pointHoverRadius: 4,
                tension: 0.25,
                fill: false
            };
        });

        if (chartInstance) {
            chartInstance.destroy();
        }

        chartInstance = new Chart(canvas, {
            type: 'line',
            data: { labels: data.labels, datasets: datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, padding: 12, font: { size: 12 } } },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return ctx.dataset.label + ': ' + currencySymbol + formatAmount(ctx.parsed.y);
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) { return currencySymbol + formatAmount(value); }
                        }
                    }
                }
            }
        });
    }

    function renderEmpty() {
        if (chartInstance) { chartInstance.destroy(); chartInstance = null; }
        canvas.style.display = 'none';
        emptyState.textContent = 'No expense data for the selected filters.';
        emptyState.style.display = 'block';
    }

    function renderError() {
        if (chartInstance) { chartInstance.destroy(); chartInstance = null; }
        canvas.style.display = 'none';
        emptyState.textContent = 'Could not load the expense trend. Please try again.';
        emptyState.style.display = 'block';
    }

    function hideEmpty() { if (emptyState) emptyState.style.display = 'none'; }
    function showSpinner(on) { if (spinner) spinner.style.display = on ? 'flex' : 'none'; }

    function formatAmount(v) {
        var n = Number(v) || 0;
        return n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
})();
