// ============================================================================
// Product Performance — collapsible section with two horizontal bar charts
// (Top Products by Revenue, Top Products by Units Sold) on the Products page.
//
// Behaviour:
//   - Collapsed by default; expand/collapse state persisted in localStorage.
//   - Data is fetched the first time the section is expanded (collapsed costs nothing),
//     and re-fetched whenever the period selector changes while expanded.
//   - Chart.js is self-hosted (loaded by the view) — no third-party CDN.
//   - A help button opens a SweetAlert2 modal explaining what each chart shows.
// ============================================================================
(function () {
    'use strict';

    var STORAGE_KEY = 'productPerformance_expanded';

    var section = document.getElementById('productPerformanceSection');
    if (!section) return;

    var toggleBtn = document.getElementById('productPerformanceToggle');
    var body = document.getElementById('productPerformanceBody');
    var spinner = document.getElementById('perfSpinner');
    var errorBox = document.getElementById('perfError');
    var chartsWrap = document.getElementById('perfCharts');
    var periodSelect = document.getElementById('perfPeriodSelect');
    var periodLabel = document.getElementById('perfPeriodLabel');
    var helpBtn = document.getElementById('perfHelpBtn');

    var revenueCanvas = document.getElementById('perfRevenueChart');
    var unitsCanvas = document.getElementById('perfUnitsChart');
    var revenueEmpty = document.getElementById('perfRevenueEmpty');
    var unitsEmpty = document.getElementById('perfUnitsEmpty');

    var revenueChart = null;
    var unitsChart = null;
    var hasLoaded = false;

    var BLUE = '#0D5EA6';
    var CYAN = '#57B8E8';

    // ── Collapse / expand ───────────────────────────────────────────────────
    function isExpanded() {
        return section.classList.contains('is-open');
    }

    function setExpanded(expanded, skipFetch) {
        section.classList.toggle('is-open', expanded);
        body.style.display = expanded ? 'block' : 'none';
        try { localStorage.setItem(STORAGE_KEY, expanded ? '1' : '0'); } catch (e) { /* ignore */ }

        if (expanded && !hasLoaded && !skipFetch) {
            fetchPerformance();
        }
    }

    toggleBtn.addEventListener('click', function () {
        setExpanded(!isExpanded());
    });

    // Re-fetch when the period changes (only matters while expanded).
    periodSelect.addEventListener('change', function () {
        if (isExpanded()) {
            fetchPerformance();
        } else {
            // Expanding will trigger a fresh fetch anyway; just mark stale.
            hasLoaded = false;
        }
    });

    // ── Currency / number formatting ────────────────────────────────────────
    function formatCurrency(value, symbol) {
        var n = Number(value) || 0;
        return (symbol || '') + n.toLocaleString(undefined, {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function formatUnits(value) {
        var n = Number(value) || 0;
        // Show up to 2 decimals only when the quantity is fractional.
        return n % 1 === 0
            ? n.toLocaleString()
            : n.toLocaleString(undefined, { minimumFractionDigits: 1, maximumFractionDigits: 2 });
    }

    // ── Fetch ───────────────────────────────────────────────────────────────
    function fetchPerformance() {
        var period = periodSelect.value || 'year';

        spinner.style.display = 'flex';
        errorBox.style.display = 'none';
        chartsWrap.style.display = 'none';

        fetch('/Product/AxGetProductPerformance?period=' + encodeURIComponent(period))
            .then(function (r) { return r.json(); })
            .then(function (result) {
                spinner.style.display = 'none';

                if (!result || !result.success || !result.data) {
                    showError((result && result.message) || 'Could not load product performance.');
                    return;
                }

                hasLoaded = true;
                render(result.data);
            })
            .catch(function () {
                spinner.style.display = 'none';
                showError('Could not load product performance. Please try again.');
            });
    }

    function showError(message) {
        errorBox.textContent = message;
        errorBox.style.display = 'block';
        chartsWrap.style.display = 'none';
    }

    // ── Render ───────────────────────────────────────────────────────────────
    function render(data) {
        if (periodLabel) periodLabel.textContent = data.periodLabel || '';
        chartsWrap.style.display = 'grid';

        var symbol = data.currencySymbol || '';

        // Revenue chart
        var revenue = data.revenue || [];
        if (revenue.length === 0) {
            revenueEmpty.style.display = 'block';
            revenueCanvas.style.display = 'none';
            if (revenueChart) { revenueChart.destroy(); revenueChart = null; }
        } else {
            revenueEmpty.style.display = 'none';
            revenueCanvas.style.display = 'block';
            revenueChart = renderBar(revenueCanvas, revenueChart, revenue, BLUE, function (v) {
                return formatCurrency(v, symbol);
            });
        }

        // Units chart
        var units = data.units || [];
        if (units.length === 0) {
            unitsEmpty.style.display = 'block';
            unitsCanvas.style.display = 'none';
            if (unitsChart) { unitsChart.destroy(); unitsChart = null; }
        } else {
            unitsEmpty.style.display = 'none';
            unitsCanvas.style.display = 'block';
            unitsChart = renderBar(unitsCanvas, unitsChart, units, CYAN, formatUnits);
        }
    }

    function renderBar(canvas, existing, points, color, valueFormatter) {
        if (existing) existing.destroy();

        var labels = points.map(function (p) { return p.description; });
        var values = points.map(function (p) { return Number(p.value) || 0; });

        return new Chart(canvas, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: color,
                    borderColor: color,
                    borderWidth: 0,
                    borderRadius: 6,
                    maxBarThickness: 26
                }]
            },
            options: {
                indexAxis: 'y',               // horizontal bars
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return valueFormatter(ctx.parsed.x);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        ticks: {
                            font: { size: 11 },
                            callback: function (value) { return valueFormatter(value); }
                        }
                    },
                    y: {
                        ticks: { font: { size: 11 }, autoSkip: false }
                    }
                }
            }
        });
    }

    // ── Help modal (SweetAlert2) ──────────────────────────────────────────────
    helpBtn.addEventListener('click', function () {
        var html =
            '<div style="text-align:left;font-size:14px;line-height:1.55;color:#33475b;">' +
            '<p style="margin:0 0 14px;"><strong style="color:#0D5EA6;">Top Products by Revenue</strong><br>' +
            'Your biggest earners for the selected period. Each bar is the total sales value ' +
            '(quantity × price) taken from <em>issued</em> invoices. Use it to see where your money comes from.</p>' +
            '<p style="margin:0 0 14px;"><strong style="color:#0D5EA6;">Top Products by Units Sold</strong><br>' +
            'Your biggest movers by volume. Each bar is the total quantity sold. A cheap, high-volume ' +
            'item can lead here while a pricier item leads on revenue &mdash; that contrast is the point.</p>' +
            '<p style="margin:0 0 6px;"><strong style="color:#0D5EA6;">The period selector</strong><br>' +
            'Both charts respect it. It defaults to <strong>This Year</strong> so a large one-off sale from ' +
            'years ago does not distort today&rsquo;s picture. Switch to Last Quarter, Last 12 Months, or All Time as needed.</p>' +
            '<p style="margin:14px 0 0;font-size:12.5px;color:#5E7385;">Only issued, non-deleted, non-cancelled invoices are counted. Quotations are excluded.</p>' +
            '</div>';

        Swal.fire({
            title: 'What these charts show',
            html: html,
            icon: 'info',
            confirmButtonColor: '#0D5EA6',
            confirmButtonText: 'Got it',
            width: 640
        });
    });

    // ── Initial state ─────────────────────────────────────────────────────────
    var stored = null;
    try { stored = localStorage.getItem(STORAGE_KEY); } catch (e) { /* ignore */ }
    // Default: collapsed.
    setExpanded(stored === '1', false);
})();
