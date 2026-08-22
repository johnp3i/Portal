/**
 * Sales Activity Feed — AJAX-driven listing, filtering, pagination
 */
(function () {
    'use strict';

    var _currentPage = 1;

    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function formatTimestamp(utcStr) {
        if (!utcStr) return '';
        return new Date(utcStr).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    function getActionBadgeHtml(action) {
        var colors = {
            'stage_changed': 'rgba(13,94,166,.08);color:#0D5EA6',
            'meeting_scheduled': 'rgba(87,184,232,.08);color:#1a8fc7',
            'meeting_cancelled': 'rgba(194,74,74,.08);color:#C24A4A',
            'response_sent': 'rgba(200,145,46,.08);color:#C8912E',
            'lead_created': 'rgba(18,152,103,.08);color:#129867',
            'lead_cancelled': 'rgba(194,74,74,.08);color:#C24A4A',
            'lead_reactivated': 'rgba(13,94,166,.08);color:#0D5EA6',
            'proposal_linked': 'rgba(87,184,232,.08);color:#1a8fc7',
            'invoice_linked': 'rgba(87,184,232,.08);color:#1a8fc7',
            'marked_as_won': 'rgba(18,152,103,.08);color:#129867',
            'assigned': 'rgba(13,94,166,.08);color:#0D5EA6',
            'unassigned': 'rgba(138,155,171,.08);color:#8a9bab',
            'task_completed': 'rgba(18,152,103,.08);color:#129867',
            'note_added': 'rgba(138,155,171,.08);color:#8a9bab'
        };
        var label = action ? action.replace(/_/g, ' ').replace(/\b\w/g, function (c) { return c.toUpperCase(); }) : 'Unknown';
        var c = colors[action] || 'rgba(138,155,171,.08);color:#8a9bab';
        return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:' + c + ';">' + escapeHtml(label) + '</span>';
    }

    // ─── AJAX Table Loading ─────────────────────────────────────

    window.loadActivityPage = function (page) {
        _currentPage = page;

        var actionType = document.getElementById('filterActionType').value;
        var dateFrom = document.getElementById('filterDateFrom').value;
        var dateTo = document.getElementById('filterDateTo').value;

        var params = new URLSearchParams();
        params.append('page', page);
        if (actionType) params.append('actionType', actionType);
        if (dateFrom) params.append('dateFrom', dateFrom);
        if (dateTo) params.append('dateTo', dateTo);

        var tbody = document.getElementById('activityTableBody');
        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;color:#8a9bab;padding:32px;">Loading activity...</td></tr>';

        fetch('/Sales/AxGetActivityFeedPage?' + params.toString())
            .then(function (response) { return response.json(); })
            .then(function (result) {
                if (result.success) {
                    renderActivityTable(result.data, result.totalCount, result.currentPage, result.totalPages);
                } else {
                    tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;color:#C24A4A;padding:32px;">Failed to load activity. Please try again.</td></tr>';
                    document.getElementById('activityPagination').style.display = 'none';
                }
            })
            .catch(function () {
                tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;color:#C24A4A;padding:32px;">Failed to load activity. Please try again.</td></tr>';
                document.getElementById('activityPagination').style.display = 'none';
            });
    };

    // ─── Table Rendering ────────────────────────────────────────

    function renderActivityTable(data, totalCount, currentPage, totalPages) {
        var tbody = document.getElementById('activityTableBody');
        var pagination = document.getElementById('activityPagination');
        var paginationInfo = document.getElementById('activityPaginationInfo');
        var paginationControls = document.getElementById('activityPaginationControls');

        if (!data || data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;color:#8a9bab;padding:32px;">No activity found.</td></tr>';
            pagination.style.display = 'none';
            return;
        }

        pagination.style.display = 'flex';

        var html = '';
        data.forEach(function (entry) {
            html += '<tr>';
            html += '<td style="white-space:nowrap;font-size:13px;color:#5E7385;">' + formatTimestamp(entry.createdAtUtc) + '</td>';
            html += '<td>' + getActionBadgeHtml(entry.action) + '</td>';
            html += '<td>' + escapeHtml(entry.description) + '</td>';
            html += '<td>' + (entry.leadName ? '<span style="color:#0D5EA6;">' + escapeHtml(entry.leadName) + '</span>' : '<span style="color:#8a9bab;">—</span>') + '</td>';
            html += '<td>' + (entry.performedByName ? escapeHtml(entry.performedByName) : '<span style="color:#8a9bab;">System</span>') + '</td>';
            html += '</tr>';
        });

        tbody.innerHTML = html;

        // Pagination info
        var pageSize = 15;
        var start = (currentPage - 1) * pageSize + 1;
        var end = Math.min(currentPage * pageSize, totalCount);
        paginationInfo.innerHTML = 'Showing ' + start + '–' + end + ' of ' + totalCount + ' entries';

        // Windowed pagination controls (max 7 visible)
        var controlsHtml = '';
        var maxVisible = 7;
        var startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
        var endPage = Math.min(totalPages, startPage + maxVisible - 1);
        if (endPage - startPage < maxVisible - 1) {
            startPage = Math.max(1, endPage - maxVisible + 1);
        }

        if (startPage > 1) {
            controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid rgba(13,94,166,.15);background:#fff;color:#0D5EA6;cursor:pointer;margin:0 2px;" onclick="loadActivityPage(1)">1</button>';
            if (startPage > 2) controlsHtml += '<span style="padding:6px 4px;font-size:13px;color:#8a9bab;">…</span>';
        }
        for (var i = startPage; i <= endPage; i++) {
            if (i === currentPage) {
                controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid #0D5EA6;background:#0D5EA6;color:#fff;cursor:default;margin:0 2px;">' + i + '</button>';
            } else {
                controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid rgba(13,94,166,.15);background:#fff;color:#0D5EA6;cursor:pointer;margin:0 2px;" onclick="loadActivityPage(' + i + ')">' + i + '</button>';
            }
        }
        if (endPage < totalPages) {
            if (endPage < totalPages - 1) controlsHtml += '<span style="padding:6px 4px;font-size:13px;color:#8a9bab;">…</span>';
            controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid rgba(13,94,166,.15);background:#fff;color:#0D5EA6;cursor:pointer;margin:0 2px;" onclick="loadActivityPage(' + totalPages + ')">' + totalPages + '</button>';
        }
        paginationControls.innerHTML = controlsHtml;
    }

    // ─── Filter & Quick Presets ─────────────────────────────────

    window.filterActivity = function () {
        loadActivityPage(1);
    };

    window.clearActivityFilters = function () {
        document.getElementById('filterActionType').value = '';
        document.getElementById('filterDateFrom').value = '';
        document.getElementById('filterDateTo').value = '';
        document.querySelectorAll('.period-shortcut').forEach(function (btn) {
            btn.classList.remove('active');
        });
        loadActivityPage(1);
    };

    window.setQuickPeriod = function (e, preset) {
        var today = new Date();
        var dateFrom = '';
        var dateTo = '';

        switch (preset) {
            case 'this_month':
                dateFrom = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().split('T')[0];
                dateTo = today.toISOString().split('T')[0];
                break;
            case 'last_month':
                var firstLastMonth = new Date(today.getFullYear(), today.getMonth() - 1, 1);
                var lastLastMonth = new Date(today.getFullYear(), today.getMonth(), 0);
                dateFrom = firstLastMonth.toISOString().split('T')[0];
                dateTo = lastLastMonth.toISOString().split('T')[0];
                break;
            case 'last_3':
                dateFrom = new Date(today.getFullYear(), today.getMonth() - 3, 1).toISOString().split('T')[0];
                dateTo = today.toISOString().split('T')[0];
                break;
            case 'last_6':
                dateFrom = new Date(today.getFullYear(), today.getMonth() - 6, 1).toISOString().split('T')[0];
                dateTo = today.toISOString().split('T')[0];
                break;
            case 'this_year':
                dateFrom = new Date(today.getFullYear(), 0, 1).toISOString().split('T')[0];
                dateTo = today.toISOString().split('T')[0];
                break;
            case 'last_year':
                dateFrom = new Date(today.getFullYear() - 1, 0, 1).toISOString().split('T')[0];
                dateTo = new Date(today.getFullYear() - 1, 11, 31).toISOString().split('T')[0];
                break;
            case 'all':
                dateFrom = '';
                dateTo = '';
                break;
        }

        document.getElementById('filterDateFrom').value = dateFrom;
        document.getElementById('filterDateTo').value = dateTo;

        document.querySelectorAll('.period-shortcut').forEach(function (btn) {
            btn.classList.remove('active');
        });
        if (e && e.target) {
            e.target.classList.add('active');
        }

        loadActivityPage(1);
    };

    // ─── Initialization ─────────────────────────────────────────

    document.addEventListener('DOMContentLoaded', function () {
        loadActivityPage(1);
    });

})();
