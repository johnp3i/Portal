/* ==========================================================================
   Expense Insights — Charts, period switching, budget management, CSV export
   Depends on: Chart.js v4 (loaded via CDN), BlockUI (block-ui.js), SweetAlert2
   Server data: window.ExpenseInsightsData (set in view's Scripts section)
   ========================================================================== */

(function () {
    'use strict';

    // =========================================================================
    // Design System Colour Palette
    // =========================================================================
    var COLOURS = [
        '#0D5EA6', // Primary Blue
        '#57B8E8', // Cyan
        '#129867', // Green
        '#C8912E', // Amber
        '#C24A4A', // Danger
        '#6366F1', // Indigo
        '#8B5CF6', // Violet
        '#EC4899', // Pink
        '#14B8A6', // Teal
        '#F59E0B'  // Gold
    ];

    // =========================================================================
    // State
    // =========================================================================
    var currentPeriod = 'CurrentMonth';
    var currentStartDate = null;
    var currentEndDate = null;
    var pieChart = null;
    var barChart = null;
    var trendChart = null;

    // =========================================================================
    // Initialization
    // =========================================================================
    document.addEventListener('DOMContentLoaded', function () {
        bindPeriodButtons();
        bindCustomDateInputs();
        bindExportButton();
        bindBudgetSaveButtons();
        bindRowExpansion();

        // Initial render from server-provided data
        if (typeof window.ExpenseInsightsData !== 'undefined' && window.ExpenseInsightsData) {
            var serverData = window.ExpenseInsightsData;
            if (serverData.hasData && serverData.categories) {
                renderInsights({
                    hasData: serverData.hasData,
                    categories: serverData.categories,
                    summary: null // Summary cards already rendered server-side on initial load
                });
            }
            if (serverData.trendData) {
                renderTrendChart(serverData.trendData);
            }
            if (serverData.selectedPeriod) {
                currentPeriod = serverData.selectedPeriod;
            }
        }

        // Restore saved period from localStorage
        var savedPeriod = localStorage.getItem('expenseInsights_period');
        var savedStartDate = localStorage.getItem('expenseInsights_startDate');
        var savedEndDate = localStorage.getItem('expenseInsights_endDate');

        if (savedPeriod && savedPeriod !== 'CurrentMonth') {
            currentPeriod = savedPeriod;
            currentStartDate = savedStartDate;
            currentEndDate = savedEndDate;

            // Update the active button
            var targetBtn = document.querySelector('[data-period="' + savedPeriod + '"]');
            if (targetBtn) {
                setActivePeriodButton(targetBtn);
            }

            // If custom, show date inputs and populate values
            if (savedPeriod === 'Custom' && savedStartDate && savedEndDate) {
                showCustomDateInputs();
                var startInput = document.getElementById('insightsStartDate');
                var endInput = document.getElementById('insightsEndDate');
                if (startInput) startInput.value = savedStartDate;
                if (endInput) endInput.value = savedEndDate;
            }

            // Load data for the saved period
            loadInsightsData();
        }
    });

    // =========================================================================
    // Period Switching
    // =========================================================================
    function bindPeriodButtons() {
        var buttons = document.querySelectorAll('[data-period]');
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].addEventListener('click', function () {
                var period = this.getAttribute('data-period');
                setActivePeriodButton(this);

                if (period === 'Custom') {
                    showCustomDateInputs();
                    return;
                }

                hideCustomDateInputs();
                currentPeriod = period;
                currentStartDate = null;
                currentEndDate = null;

                // Persist selected period to localStorage
                localStorage.setItem('expenseInsights_period', period);
                localStorage.removeItem('expenseInsights_startDate');
                localStorage.removeItem('expenseInsights_endDate');

                loadInsightsData();
            });
        }
    }

    function setActivePeriodButton(activeBtn) {
        var buttons = document.querySelectorAll('[data-period]');
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.remove('active');
        }
        activeBtn.classList.add('active');
    }

    function showCustomDateInputs() {
        var container = document.getElementById('customRange');
        if (container) container.classList.add('visible');
    }

    function hideCustomDateInputs() {
        var container = document.getElementById('customRange');
        if (container) container.classList.remove('visible');
    }

    // =========================================================================
    // Custom Date Range
    // =========================================================================
    function bindCustomDateInputs() {
        var applyBtn = document.getElementById('btnApplyCustomRange');
        if (applyBtn) {
            applyBtn.addEventListener('click', validateAndLoadCustomRange);
        }
    }

    function validateAndLoadCustomRange() {
        var startInput = document.getElementById('insightsStartDate');
        var endInput = document.getElementById('insightsEndDate');

        if (!startInput || !endInput) return;

        var startVal = startInput.value;
        var endVal = endInput.value;

        // Both must be filled
        if (!startVal || !endVal) return;

        // Validate start <= end
        if (startVal > endVal) {
            Swal.fire({
                title: 'Invalid Date Range',
                text: 'Start date must be before or equal to the end date.',
                icon: 'warning',
                confirmButtonColor: '#0D5EA6'
            });
            return;
        }

        currentPeriod = 'Custom';
        currentStartDate = startVal;
        currentEndDate = endVal;

        // Persist custom period and dates to localStorage
        localStorage.setItem('expenseInsights_period', 'Custom');
        localStorage.setItem('expenseInsights_startDate', currentStartDate);
        localStorage.setItem('expenseInsights_endDate', currentEndDate);

        loadInsightsData();
    }

    // =========================================================================
    // AJAX — Load Insights Data
    // =========================================================================
    function loadInsightsData() {
        var url = '/ExpenseInsight/AxGetInsightsData?periodType=' + encodeURIComponent(currentPeriod);
        if (currentPeriod === 'Custom' && currentStartDate && currentEndDate) {
            url += '&startDate=' + encodeURIComponent(currentStartDate);
            url += '&endDate=' + encodeURIComponent(currentEndDate);
        }

        BlockUI.show('Loading expense insights...');

        fetch(url, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                BlockUI.hide();
                if (result.success) {
                    renderInsights(result.data);
                } else {
                    Swal.fire({
                        title: 'Error',
                        text: result.message || 'Failed to load expense insights.',
                        icon: 'error',
                        confirmButtonColor: '#0D5EA6'
                    });
                }
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({
                    title: 'Error',
                    text: 'An unexpected error occurred loading expense insights.',
                    icon: 'error',
                    confirmButtonColor: '#0D5EA6'
                });
            });
    }

    // =========================================================================
    // Render All Insights (after AJAX or initial load)
    // =========================================================================
    function renderInsights(data) {
        var emptyState = document.getElementById('emptyState');
        var dataContent = document.getElementById('dataContent');

        if (!data || !data.hasData) {
            // Show empty state, hide charts/table
            if (emptyState) emptyState.style.display = 'block';
            if (dataContent) dataContent.style.display = 'none';
            return;
        }

        // Show data, hide empty state
        if (emptyState) emptyState.style.display = 'none';
        if (dataContent) dataContent.style.display = 'block';

        renderSummaryCards(data.summary);
        renderBudgetAlerts(data.budgetExceededCount, data.budgetApproachingCount);
        renderPieChart(data.categories);
        renderBarChart(data.categories);
        renderBreakdownTable(data.categories);
    }

    // =========================================================================
    // Summary Cards
    // =========================================================================
    function renderSummaryCards(summary) {
        if (!summary) return;

        var totalSpendEl = document.querySelector('[data-total-spend]');
        var categoriesCountEl = document.querySelector('[data-categories-count]');
        var topCategoryEl = document.querySelector('[data-top-category]');
        var avgPerCategoryEl = document.querySelector('[data-avg-per-category]');

        if (totalSpendEl) totalSpendEl.textContent = formatCurrency(summary.totalSpend);
        if (categoriesCountEl) categoriesCountEl.textContent = summary.categoriesWithSpend;
        if (topCategoryEl) topCategoryEl.textContent = summary.topCategoryName || '—';
        if (avgPerCategoryEl) avgPerCategoryEl.textContent = formatCurrency(summary.averagePerCategory);
    }

    // =========================================================================
    // Budget Alerts Banner
    // =========================================================================
    function renderBudgetAlerts(exceededCount, approachingCount) {
        var banner = document.querySelector('[data-budget-alerts]');
        if (!banner) return;

        if (exceededCount === 0 && approachingCount === 0) {
            banner.style.display = 'none';
            return;
        }

        var html = '';
        if (exceededCount > 0) {
            html += '<div class="budget-alert-item danger">';
            html += '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>';
            html += '<span data-exceeded-count>' + exceededCount + '</span> ' + (exceededCount === 1 ? 'category' : 'categories') + ' exceeded budget';
            html += '</div>';
        }
        if (approachingCount > 0) {
            html += '<div class="budget-alert-item warning">';
            html += '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>';
            html += '<span data-approaching-count>' + approachingCount + '</span> ' + (approachingCount === 1 ? 'category' : 'categories') + ' approaching limit';
            html += '</div>';
        }

        banner.innerHTML = html;
        banner.style.display = 'flex';
    }

    // =========================================================================
    // Pie Chart (Doughnut)
    // =========================================================================
    function renderPieChart(categories) {
        var canvas = document.getElementById('pieChart');
        if (!canvas) return;

        // Hide pie chart if fewer than 2 categories
        var pieContainer = canvas.closest('.chart-container') || canvas.parentElement;
        if (!categories || categories.length < 2) {
            if (pieContainer) pieContainer.style.display = 'none';
            return;
        }
        if (pieContainer) pieContainer.style.display = 'block';

        // Sort by totalSpend descending and limit to top 8, grouping the rest as "Other"
        var sorted = categories.slice().sort(function (a, b) { return b.totalSpend - a.totalSpend; });
        var chartCategories = sorted;

        if (sorted.length > 8) {
            chartCategories = sorted.slice(0, 8);
            var otherTotal = 0;
            for (var k = 8; k < sorted.length; k++) {
                otherTotal += sorted[k].totalSpend;
            }
            chartCategories.push({ categoryName: 'Other', totalSpend: otherTotal });
        }

        var labels = [];
        var dataPoints = [];
        var backgroundColors = [];

        for (var i = 0; i < chartCategories.length; i++) {
            labels.push(chartCategories[i].categoryName);
            dataPoints.push(chartCategories[i].totalSpend);
            // Use neutral grey for the "Other" slice
            if (chartCategories[i].categoryName === 'Other') {
                backgroundColors.push('#94A3B8');
            } else {
                backgroundColors.push(getColour(i));
            }
        }

        if (pieChart) {
            pieChart.destroy();
        }

        pieChart = new Chart(canvas, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: dataPoints,
                    backgroundColor: backgroundColors,
                    borderWidth: 2,
                    borderColor: '#ffffff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'right',
                        labels: {
                            boxWidth: 12,
                            padding: 10,
                            font: { size: 12 },
                            generateLabels: function (chart) {
                                var data = chart.data;
                                var total = data.datasets[0].data.reduce(function (a, b) { return a + b; }, 0);
                                return data.labels.map(function (label, i) {
                                    var value = data.datasets[0].data[i];
                                    var pct = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
                                    return {
                                        text: label + ' (' + pct + '%)',
                                        fillStyle: data.datasets[0].backgroundColor[i],
                                        hidden: false,
                                        index: i
                                    };
                                });
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                var value = context.parsed;
                                var total = context.dataset.data.reduce(function (a, b) { return a + b; }, 0);
                                var percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
                                return context.label + ': ' + formatCurrency(value) + ' (' + percentage + '%)';
                            }
                        }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Bar Chart (Horizontal, sorted desc)
    // =========================================================================
    function renderBarChart(categories) {
        var canvas = document.getElementById('barChart');
        if (!canvas) return;

        var barContainer = canvas.closest('.chart-container') || canvas.parentElement;
        if (!categories || categories.length === 0) {
            if (barContainer) barContainer.style.display = 'none';
            return;
        }
        if (barContainer) barContainer.style.display = 'block';

        // Sort descending by spend and limit to top 10 categories
        var sorted = categories.slice().sort(function (a, b) { return b.totalSpend - a.totalSpend; });
        if (sorted.length > 10) sorted = sorted.slice(0, 10);

        var fullLabels = [];
        var displayLabels = [];
        var dataPoints = [];
        var backgroundColors = [];
        var totalSpend = 0;

        for (var i = 0; i < sorted.length; i++) {
            var name = sorted[i].categoryName;
            fullLabels.push(name);
            displayLabels.push(name.length > 25 ? name.substring(0, 22) + '...' : name);
            dataPoints.push(sorted[i].totalSpend);
            backgroundColors.push(getColour(i));
            totalSpend += sorted[i].totalSpend;
        }

        if (barChart) {
            barChart.destroy();
        }

        barChart = new Chart(canvas, {
            type: 'bar',
            data: {
                labels: displayLabels,
                datasets: [{
                    label: 'Spend',
                    data: dataPoints,
                    backgroundColor: backgroundColors,
                    borderRadius: 4,
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: function (tooltipItems) {
                                // Show the full category name (not truncated) in the tooltip title
                                var index = tooltipItems[0].dataIndex;
                                return fullLabels[index];
                            },
                            label: function (context) {
                                var value = context.parsed.x;
                                var percentage = totalSpend > 0 ? ((value / totalSpend) * 100).toFixed(1) : '0.0';
                                return formatCurrency(value) + ' (' + percentage + '%)';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return getCurrencySymbol() + value.toLocaleString();
                            }
                        },
                        grid: { color: 'rgba(0,0,0,0.04)' }
                    },
                    y: {
                        grid: { display: false }
                    }
                }
            }
        });
    }

    // =========================================================================
    // Trend Line Chart
    // =========================================================================
    function renderTrendChart(data) {
        var canvas = document.getElementById('trendChart');
        if (!canvas) return;

        var trendSection = document.querySelector('[data-trend-section]');
        var insufficientMsg = document.querySelector('[data-trend-insufficient]');

        if (!data || !data.hasSufficientData) {
            if (canvas) canvas.style.display = 'none';
            if (insufficientMsg) insufficientMsg.style.display = 'block';
            return;
        }

        if (canvas) canvas.style.display = 'block';
        if (insufficientMsg) insufficientMsg.style.display = 'none';

        var datasets = [];
        for (var i = 0; i < data.series.length; i++) {
            var series = data.series[i];
            datasets.push({
                label: series.categoryName,
                data: series.monthlyTotals,
                borderColor: getColour(i),
                backgroundColor: getColour(i),
                borderWidth: 2.5,
                tension: 0.3,
                fill: false,
                pointRadius: 3,
                pointHoverRadius: 5
            });
        }

        if (trendChart) {
            trendChart.destroy();
        }

        trendChart = new Chart(canvas, {
            type: 'line',
            data: {
                labels: data.monthLabels,
                datasets: datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, padding: 12, font: { size: 12 } }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return context.dataset.label + ': ' + formatCurrency(context.parsed.y);
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return getCurrencySymbol() + value.toLocaleString();
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
    // Breakdown Table
    // =========================================================================
    function renderBreakdownTable(categories) {
        var tbody = document.querySelector('[data-breakdown-body]');
        if (!tbody) return;

        if (!categories || categories.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;color:#5a6a7a;padding:24px;">No expense data for this period.</td></tr>';
            return;
        }

        var html = '';
        for (var i = 0; i < categories.length; i++) {
            var cat = categories[i];
            var hasSuppliers = cat.topSuppliers && cat.topSuppliers.length > 0;

            // Category row
            html += '<tr class="category-row" data-category-id="' + cat.expenseCategoryId + '">';
            html += '<td>';
            html += '<svg class="chevron-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"/></svg>';
            html += '</td>';
            html += '<td><strong>' + escapeHtml(cat.categoryName) + '</strong></td>';
            html += '<td><span class="type-pill">' + escapeHtml(cat.expenseTypeName) + '</span></td>';
            html += '<td class="text-right">' + formatCurrency(cat.totalSpend) + '</td>';
            html += '<td class="text-right">' + cat.percentageOfTotal.toFixed(1) + '%</td>';
            html += '<td class="text-right">' + renderVariance(cat.variance, cat.varianceValue) + '</td>';
            html += '<td class="text-right">' + (cat.budgetLimit != null ? formatCurrency(cat.budgetLimit) : '<span style="color:#8a9aaa;">—</span>') + '</td>';
            html += '<td>' + renderBudgetStatus(cat.budgetStatus) + '</td>';
            html += '</tr>';

            // Supplier sub-rows (hidden by default)
            if (hasSuppliers) {
                for (var j = 0; j < cat.topSuppliers.length; j++) {
                    var supplier = cat.topSuppliers[j];
                    html += '<tr class="supplier-row" data-parent-category="' + cat.expenseCategoryId + '">';
                    html += '<td></td>';
                    html += '<td colspan="2">' + escapeHtml(supplier.supplierName) + '</td>';
                    html += '<td class="text-right">' + formatCurrency(supplier.totalSpend) + '</td>';
                    html += '<td class="text-right">' + supplier.percentageOfCategory.toFixed(1) + '%</td>';
                    html += '<td colspan="3"></td>';
                    html += '</tr>';
                }
            } else {
                html += '<tr class="supplier-row" data-parent-category="' + cat.expenseCategoryId + '">';
                html += '<td></td>';
                html += '<td colspan="7" style="font-style:italic;color:#8a9aaa;">No supplier data available for this category.</td>';
                html += '</tr>';
            }
        }

        tbody.innerHTML = html;

        // Re-bind expansion after render
        bindRowExpansionInTable(tbody);
    }

    function renderVariance(variance, varianceValue) {
        if (!variance || variance === 'N/A') return '<span style="color:#5a6a7a;">N/A</span>';
        if (variance === '\u2014' || variance === '—') return '<span style="color:#5a6a7a;">—</span>';
        if (variance === 'New') return '<span class="status-badge no-limit">New</span>';

        var numericValue = varianceValue != null ? varianceValue : parseFloat(variance);
        if (isNaN(numericValue)) return '<span style="color:#5a6a7a;">' + escapeHtml(variance) + '</span>';

        if (numericValue > 0) {
            return '<span class="variance-up">\u2191 +' + Math.abs(numericValue).toFixed(1) + '%</span>';
        } else if (numericValue < 0) {
            return '<span class="variance-down">\u2193 ' + numericValue.toFixed(1) + '%</span>';
        }
        return '<span class="variance-neutral">0.0%</span>';
    }

    function renderBudgetStatus(status) {
        if (!status || status === 'No Limit') return '<span class="status-badge no-limit">No Limit</span>';
        if (status === 'Exceeded') return '<span class="status-badge exceeded">Exceeded</span>';
        if (status === 'Approaching') return '<span class="status-badge approaching">Approaching</span>';
        if (status === 'Within Limit') return '<span class="status-badge within-limit">Within Limit</span>';
        return '<span class="status-badge no-limit">' + escapeHtml(status) + '</span>';
    }

    // =========================================================================
    // Row Expansion (Supplier drill-down)
    // =========================================================================
    function bindRowExpansion() {
        var tbody = document.querySelector('[data-breakdown-body]');
        if (tbody) {
            bindRowExpansionInTable(tbody);
        }
    }

    function bindRowExpansionInTable(tbody) {
        var categoryRows = tbody.querySelectorAll('.category-row');
        for (var i = 0; i < categoryRows.length; i++) {
            categoryRows[i].addEventListener('click', function () {
                var categoryId = this.getAttribute('data-category-id');
                var chevron = this.querySelector('.chevron-icon');
                var supplierRows = tbody.querySelectorAll('.supplier-row[data-parent-category="' + categoryId + '"]');
                var isExpanded = chevron && chevron.classList.contains('expanded');

                if (isExpanded) {
                    // Collapse
                    if (chevron) chevron.classList.remove('expanded');
                    for (var j = 0; j < supplierRows.length; j++) {
                        supplierRows[j].classList.remove('visible');
                    }
                } else {
                    // Expand
                    if (chevron) chevron.classList.add('expanded');
                    for (var j = 0; j < supplierRows.length; j++) {
                        supplierRows[j].classList.add('visible');
                    }
                }
            });
        }
    }

    // =========================================================================
    // CSV Export
    // =========================================================================
    function bindExportButton() {
        var exportBtn = document.getElementById('btnExportCsv');
        if (!exportBtn) return;

        exportBtn.addEventListener('click', function () {
            var url = '/ExpenseInsight/ExportCsv?periodType=' + encodeURIComponent(currentPeriod);
            if (currentPeriod === 'Custom' && currentStartDate && currentEndDate) {
                url += '&startDate=' + encodeURIComponent(currentStartDate);
                url += '&endDate=' + encodeURIComponent(currentEndDate);
            }
            window.location.href = url;
        });
    }

    // =========================================================================
    // Budget Save
    // =========================================================================
    function bindBudgetSaveButtons() {
        // Use event delegation for budget save and clear buttons
        document.addEventListener('click', function (e) {
            var saveBtn = e.target.closest('[data-save-budget]');
            if (saveBtn) {
                var categoryId = saveBtn.getAttribute('data-save-budget');
                var inputEl = document.querySelector('[data-budget-input="' + categoryId + '"]');
                if (!inputEl) return;

                var rawValue = inputEl.value.trim();
                var periodLimitEur = null;

                if (rawValue !== '') {
                    periodLimitEur = parseFloat(rawValue);
                    if (isNaN(periodLimitEur) || periodLimitEur <= 0) {
                        Swal.fire({
                            title: 'Invalid Value',
                            text: 'Budget limit must be a positive number greater than zero.',
                            icon: 'warning',
                            confirmButtonColor: '#0D5EA6'
                        });
                        return;
                    }
                    if (periodLimitEur > 999999999.99) {
                        Swal.fire({
                            title: 'Invalid Value',
                            text: 'Budget limit cannot exceed 999,999,999.99.',
                            icon: 'warning',
                            confirmButtonColor: '#0D5EA6'
                        });
                        return;
                    }
                }

                saveBudgetLimit(categoryId, periodLimitEur);
                return;
            }

            var clearBtn = e.target.closest('[data-clear-budget]');
            if (clearBtn) {
                var categoryId = clearBtn.getAttribute('data-clear-budget');
                saveBudgetLimit(categoryId, null);
            }
        });
    }

    function saveBudgetLimit(categoryId, periodLimitEur) {
        BlockUI.show('Saving budget limit...');

        var formData = new FormData();
        formData.append('expenseCategoryId', categoryId);
        if (periodLimitEur !== null) {
            formData.append('periodLimitEur', periodLimitEur);
        }

        fetch('/ExpenseInsight/AxPostUpdateBudget', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: formData
        })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                BlockUI.hide();
                if (result.success) {
                    Swal.fire({
                        title: 'Saved',
                        text: result.message || 'Budget limit updated successfully.',
                        icon: 'success',
                        confirmButtonColor: '#0D5EA6'
                    });
                    // Update the status badge for this category
                    if (result.budgetStatus) {
                        var statusEl = document.querySelector('[data-budget-status="' + categoryId + '"]');
                        if (statusEl) {
                            statusEl.className = 'status-badge ' + getBudgetStatusClass(result.budgetStatus);
                            statusEl.textContent = result.budgetStatus;
                        }
                    }
                } else {
                    Swal.fire({
                        title: 'Error',
                        text: result.message || 'Failed to save budget limit.',
                        icon: 'error',
                        confirmButtonColor: '#0D5EA6'
                    });
                }
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({
                    title: 'Error',
                    text: 'An unexpected error occurred saving the budget limit.',
                    icon: 'error',
                    confirmButtonColor: '#0D5EA6'
                });
            });
    }

    function getBudgetStatusClass(status) {
        if (status === 'Exceeded') return 'exceeded';
        if (status === 'Approaching') return 'approaching';
        if (status === 'Within Limit') return 'within-limit';
        return 'no-limit';
    }

    // =========================================================================
    // Utility Helpers
    // =========================================================================
    function getCurrencySymbol() {
        if (typeof window.ExpenseInsightsData !== 'undefined' && window.ExpenseInsightsData && window.ExpenseInsightsData.currencySymbol) {
            return window.ExpenseInsightsData.currencySymbol;
        }
        return '€';
    }

    function formatCurrency(value) {
        if (value == null) return getCurrencySymbol() + '0.00';
        return getCurrencySymbol() + Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function getColour(index) {
        return COLOURS[index % COLOURS.length];
    }

    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    // =========================================================================
    // Expose global functions (called from onclick attributes in the view)
    // =========================================================================
    window.loadInsightsData = loadInsightsData;
    window.validateAndLoadCustomRange = validateAndLoadCustomRange;

})();
