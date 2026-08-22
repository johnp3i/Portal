/**
 * Sales Meetings — AJAX-driven listing, filtering, pagination, edit modal
 */
(function () {
    'use strict';

    var _currentPage = 1;

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    // ─── Urgency Badge ──────────────────────────────────────────

    function getUrgencyBadgeHtml(urgency) {
        switch (urgency) {
            case 'today':
                return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(200,145,46,.08);color:#C8912E;">Today</span>';
            case 'upcoming':
                return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(13,94,166,.08);color:#0D5EA6;">Upcoming</span>';
            case 'needs_outcome':
                return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(194,74,74,.08);color:#C24A4A;">Needs Outcome</span>';
            case 'completed':
                return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(18,152,103,.08);color:#129867;">Completed</span>';
            case 'cancelled':
                return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(194,74,74,.08);color:#C24A4A;">Cancelled</span>';
            default:
                return '';
        }
    }

    // ─── Relative Time Label ────────────────────────────────────

    function getRelativeTimeLabel(scheduledAtUtc) {
        if (!scheduledAtUtc) return '';
        var now = new Date();
        var scheduled = new Date(scheduledAtUtc);
        var diffMs = scheduled - now;
        var absDiffMs = Math.abs(diffMs);
        var absDiffHours = Math.round(absDiffMs / (1000 * 60 * 60));
        var absDiffDays = Math.round(absDiffMs / (1000 * 60 * 60 * 24));

        if (absDiffMs < 24 * 60 * 60 * 1000) {
            // Less than 24 hours
            var hours = absDiffHours < 1 ? 1 : absDiffHours;
            if (diffMs > 0) {
                return 'in ' + hours + ' hour' + (hours > 1 ? 's' : '');
            } else {
                return hours + ' hour' + (hours > 1 ? 's' : '') + ' ago';
            }
        } else {
            // 24 hours or more
            var days = absDiffDays < 1 ? 1 : absDiffDays;
            if (diffMs > 0) {
                return 'in ' + days + ' day' + (days > 1 ? 's' : '');
            } else {
                return days + ' day' + (days > 1 ? 's' : '') + ' ago';
            }
        }
    }

    // ─── AJAX Table Loading ─────────────────────────────────────

    window.loadMeetingsPage = function (page) {
        _currentPage = page;

        var status = document.getElementById('filterStatus').value;
        var meetingTypeId = document.getElementById('filterMeetingType').value;
        var dateFrom = document.getElementById('filterDateFrom').value;
        var dateTo = document.getElementById('filterDateTo').value;

        var params = new URLSearchParams();
        params.append('page', page);
        if (status) params.append('status', status);
        if (meetingTypeId) params.append('meetingTypeId', meetingTypeId);
        if (dateFrom) params.append('dateFrom', dateFrom);
        if (dateTo) params.append('dateTo', dateTo);

        // Show loading indicator in table body (no BlockUI for table loading)
        var tbody = document.getElementById('meetingsTableBody');
        tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;color:#8a9bab;padding:32px;">Loading meetings...</td></tr>';

        fetch('/Sales/AxGetMeetingsPaged?' + params.toString())
            .then(function (response) { return response.json(); })
            .then(function (result) {
                if (result.success) {
                    renderMeetingsTable(result.data, result.totalCount, result.currentPage, result.totalPages);
                } else {
                    tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;color:#C24A4A;padding:32px;">Failed to load meetings. Please try again.</td></tr>';
                    document.getElementById('meetingsPagination').style.display = 'none';
                }
            })
            .catch(function () {
                tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;color:#C24A4A;padding:32px;">Failed to load meetings. Please try again.</td></tr>';
                document.getElementById('meetingsPagination').style.display = 'none';
            });
    };

    // ─── Table Rendering ────────────────────────────────────────

    function renderMeetingsTable(data, totalCount, currentPage, totalPages) {
        var tbody = document.getElementById('meetingsTableBody');
        var pagination = document.getElementById('meetingsPagination');
        var paginationInfo = document.getElementById('meetingsPaginationInfo');
        var paginationControls = document.getElementById('meetingsPaginationControls');

        if (!data || data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;color:#8a9bab;padding:32px;">No meetings found.</td></tr>';
            pagination.style.display = 'none';
            return;
        }

        pagination.style.display = 'flex';

        var html = '';
        data.forEach(function (m) {
            var relativeLabel = getRelativeTimeLabel(m.scheduledAtUtc);
            var scheduledDisplay = m.scheduledAtUtc ? new Date(m.scheduledAtUtc).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '';
            var relativeHtml = relativeLabel ? '<div style="font-size:11px;color:#8a9bab;margin-top:2px;">' + escapeHtml(relativeLabel) + '</div>' : '';

            var actionsHtml = '';
            if (m.isCancelled) {
                actionsHtml += '<button class="btn btn-sm" style="background:rgba(13,94,166,.06);color:#0D5EA6;border:1.5px solid rgba(13,94,166,.12);padding:5px 10px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;" onclick="reactivateMeeting(' + m.id + ', \'' + escapeHtml(m.subject).replace(/'/g, "\\'") + '\')">Activate</button>';
            } else {
                actionsHtml += '<button class="btn btn-sm" style="background:rgba(138,155,171,.06);color:#5E7385;border:1.5px solid rgba(138,155,171,.15);padding:5px 8px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;" onclick="openEditMeetingModal(' + m.id + ')" title="Edit">&#9998;</button> ';
                actionsHtml += '<a href="/Sales/AxGetDownloadIcs?id=' + m.id + '" class="btn btn-sm" style="background:rgba(87,184,232,.08);color:#1a8fc7;border:1.5px solid rgba(87,184,232,.2);padding:5px 10px;border-radius:8px;font-size:12px;font-weight:700;text-decoration:none;cursor:pointer;" title="Download calendar file">Calendar Task</a> ';
                actionsHtml += '<button class="btn btn-sm" style="background:rgba(194,74,74,.06);color:#C24A4A;border:1.5px solid rgba(194,74,74,.15);padding:5px 10px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;" onclick="cancelMeeting(' + m.id + ', \'' + escapeHtml(m.subject).replace(/'/g, "\\'") + '\')">Cancel</button>';
            }

            var leadLink = '';
            if (m.leadRequestId) {
                leadLink = ' <a href="/Sales/LeadDetail/' + m.leadRequestId + '" style="font-size:11px;color:#0D5EA6;text-decoration:none;border-bottom:1px dashed rgba(13,94,166,.3);">View Lead</a>';
            }

            html += '<tr>';
            html += '<td>' + escapeHtml(m.subject) + leadLink + '</td>';
            html += '<td>' + escapeHtml(m.meetingTypeName || '') + '</td>';
            html += '<td>' + escapeHtml(m.contactName || '') + '</td>';
            html += '<td>' + scheduledDisplay + relativeHtml + '</td>';
            html += '<td>' + (m.durationMinutes || 60) + ' min</td>';
            var outcomeDisplay = m.outcome ? (m.outcome.length > 80 ? escapeHtml(m.outcome.substring(0, 80)) + '…' : escapeHtml(m.outcome)) : '—';
            html += '<td>' + outcomeDisplay + '</td>';
            html += '<td>' + getUrgencyBadgeHtml(m.urgency) + '</td>';
            html += '<td style="white-space:nowrap;">' + actionsHtml + '</td>';
            html += '</tr>';
        });

        tbody.innerHTML = html;

        // Pagination info
        var pageSize = 15;
        var start = (currentPage - 1) * pageSize + 1;
        var end = Math.min(currentPage * pageSize, totalCount);
        paginationInfo.innerHTML = 'Showing ' + start + '–' + end + ' of ' + totalCount + ' meetings';

        // Pagination controls (windowed for large page counts)
        var controlsHtml = '';
        var maxVisible = 7;
        var startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
        var endPage = Math.min(totalPages, startPage + maxVisible - 1);
        if (endPage - startPage < maxVisible - 1) {
            startPage = Math.max(1, endPage - maxVisible + 1);
        }

        if (startPage > 1) {
            controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid rgba(13,94,166,.15);background:#fff;color:#0D5EA6;cursor:pointer;margin:0 2px;" onclick="loadMeetingsPage(1)">1</button>';
            if (startPage > 2) {
                controlsHtml += '<span style="padding:6px 4px;font-size:13px;color:#8a9bab;">…</span>';
            }
        }
        for (var i = startPage; i <= endPage; i++) {
            if (i === currentPage) {
                controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid #0D5EA6;background:#0D5EA6;color:#fff;cursor:default;margin:0 2px;">' + i + '</button>';
            } else {
                controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid rgba(13,94,166,.15);background:#fff;color:#0D5EA6;cursor:pointer;margin:0 2px;" onclick="loadMeetingsPage(' + i + ')">' + i + '</button>';
            }
        }
        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                controlsHtml += '<span style="padding:6px 4px;font-size:13px;color:#8a9bab;">…</span>';
            }
            controlsHtml += '<button style="padding:6px 12px;border-radius:8px;font-size:13px;font-weight:700;border:1.5px solid rgba(13,94,166,.15);background:#fff;color:#0D5EA6;cursor:pointer;margin:0 2px;" onclick="loadMeetingsPage(' + totalPages + ')">' + totalPages + '</button>';
        }
        paginationControls.innerHTML = controlsHtml;
    }

    // ─── Filter & Quick Presets ─────────────────────────────────

    window.filterMeetings = function () {
        loadMeetingsPage(1);
    };

    window.clearMeetingFilters = function () {
        document.getElementById('filterStatus').value = '';
        document.getElementById('filterMeetingType').value = '';
        document.getElementById('filterDateFrom').value = '';
        document.getElementById('filterDateTo').value = '';
        // Remove active class from quick period buttons
        document.querySelectorAll('.period-shortcut').forEach(function (btn) {
            btn.classList.remove('active');
        });
        loadMeetingsPage(1);
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
            case 'next_month':
                var firstNextMonth = new Date(today.getFullYear(), today.getMonth() + 1, 1);
                var lastNextMonth = new Date(today.getFullYear(), today.getMonth() + 2, 0);
                dateFrom = firstNextMonth.toISOString().split('T')[0];
                dateTo = lastNextMonth.toISOString().split('T')[0];
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

        // Toggle active class on quick period buttons
        document.querySelectorAll('.period-shortcut').forEach(function (btn) {
            btn.classList.remove('active');
        });
        if (e && e.target) {
            e.target.classList.add('active');
        }

        loadMeetingsPage(1);
    };

    // ─── Edit Meeting Modal ─────────────────────────────────────

    window.openEditMeetingModal = function (id) {
        BlockUI.show('Loading meeting...');
        fetch('/Sales/AxGetMeetingDetail?id=' + id)
            .then(function (response) { return response.json(); })
            .then(function (result) {
                BlockUI.hide();
                if (!result.success) {
                    Swal.fire({ icon: 'error', title: 'Error', text: result.message || 'Failed to load meeting.', confirmButtonColor: '#0D5EA6' });
                    return;
                }

                var m = result.data;
                document.getElementById('editMeetingId').value = m.id;
                document.getElementById('editMeetingContactName').textContent = m.contactName || '—';
                document.getElementById('editMeetingSubject').value = m.subject || '';
                document.getElementById('editMeetingTypeId').value = m.meetingTypeId || '';
                document.getElementById('editMeetingDuration').value = m.durationMinutes || 60;
                document.getElementById('editMeetingLocation').value = m.location || '';
                document.getElementById('editMeetingNotes').value = m.notes || '';
                document.getElementById('editMeetingOutcome').value = m.outcome || '';

                // Convert ISO datetime to datetime-local format (YYYY-MM-DDTHH:mm)
                if (m.scheduledAtUtc) {
                    var dt = new Date(m.scheduledAtUtc);
                    var year = dt.getFullYear();
                    var month = String(dt.getMonth() + 1).padStart(2, '0');
                    var day = String(dt.getDate()).padStart(2, '0');
                    var hours = String(dt.getHours()).padStart(2, '0');
                    var minutes = String(dt.getMinutes()).padStart(2, '0');
                    document.getElementById('editMeetingScheduledAt').value = year + '-' + month + '-' + day + 'T' + hours + ':' + minutes;
                } else {
                    document.getElementById('editMeetingScheduledAt').value = '';
                }

                document.getElementById('editMeetingModal').style.display = 'flex';
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
            });
    };

    window.submitEditMeeting = async function () {
        var subject = document.getElementById('editMeetingSubject').value.trim();
        var scheduledAt = document.getElementById('editMeetingScheduledAt').value;

        if (!subject || !scheduledAt) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Subject and scheduled date are required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        var payload = {
            id: parseInt(document.getElementById('editMeetingId').value),
            subject: subject,
            meetingTypeId: parseInt(document.getElementById('editMeetingTypeId').value),
            scheduledAtUtc: scheduledAt,
            durationMinutes: parseInt(document.getElementById('editMeetingDuration').value) || 60,
            location: document.getElementById('editMeetingLocation').value || null,
            notes: document.getElementById('editMeetingNotes').value || null,
            outcome: document.getElementById('editMeetingOutcome').value || null
        };

        BlockUI.show('Updating...');
        try {
            var response = await fetch('/Sales/AxPostUpdateMeeting', {
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
                closeEditMeetingModal();
                Swal.fire({ icon: 'success', title: 'Updated', text: 'Meeting updated successfully.', confirmButtonColor: '#0D5EA6' }).then(function () {
                    loadMeetingsPage(_currentPage);
                });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message || 'Failed to update meeting.', confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.closeEditMeetingModal = function () {
        document.getElementById('editMeetingModal').style.display = 'none';
    };

    // ─── Create Meeting Modal ───────────────────────────────────

    window.openCreateMeetingModal = function () {
        document.getElementById('meetingSubject').value = '';
        document.getElementById('meetingScheduledAt').value = '';
        document.getElementById('meetingDuration').value = '60';
        document.getElementById('meetingLocation').value = '';
        document.getElementById('meetingNotes').value = '';
        document.getElementById('meetingModal').style.display = 'flex';
    };

    window.closeMeetingModal = function () {
        document.getElementById('meetingModal').style.display = 'none';
    };

    window.submitMeeting = async function () {
        var contactId = document.getElementById('meetingContactId').value;
        var subject = document.getElementById('meetingSubject').value;
        var scheduledAt = document.getElementById('meetingScheduledAt').value;

        if (!contactId || !subject || !scheduledAt) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Contact, subject, and date are required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        var payload = {
            contactId: parseInt(contactId),
            meetingTypeId: parseInt(document.getElementById('meetingTypeId').value),
            subject: subject,
            scheduledAtUtc: scheduledAt,
            durationMinutes: parseInt(document.getElementById('meetingDuration').value) || 60,
            location: document.getElementById('meetingLocation').value || null,
            notes: document.getElementById('meetingNotes').value || null,
            leadRequestId: new URLSearchParams(window.location.search).get('leadRequestId') ? parseInt(new URLSearchParams(window.location.search).get('leadRequestId')) : null
        };

        BlockUI.show('Scheduling...');
        try {
            var response = await fetch('/Sales/AxPostCreateMeeting', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify(payload)
            });
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                closeMeetingModal();
                Swal.fire({ icon: 'success', title: 'Scheduled', text: 'Meeting scheduled successfully.', confirmButtonColor: '#0D5EA6' }).then(function () {
                    window.location.href = '/Sales/Meetings';
                });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    // ─── Cancel & Reactivate ────────────────────────────────────

    window.cancelMeeting = function (id, subject) {
        Swal.fire({
            title: 'Cancel Meeting?',
            text: 'Cancel "' + subject + '"? This cannot be undone.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, cancel it',
            cancelButtonText: 'Go Back',
            confirmButtonColor: '#C24A4A'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Cancelling...');
                try {
                    var response = await fetch('/Sales/AxPostCancelMeeting?id=' + id + '&description=', {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Cancelled', text: 'Meeting cancelled.', confirmButtonColor: '#0D5EA6' }).then(function () {
                            loadMeetingsPage(_currentPage);
                        });
                    } else {
                        Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
                    }
                } catch (e) {
                    BlockUI.hide();
                    Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
                }
            }
        });
    };

    window.reactivateMeeting = function (id, subject) {
        Swal.fire({
            title: 'Reactivate Meeting?',
            text: 'Reactivate "' + subject + '"?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, activate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Reactivating...');
                try {
                    var response = await fetch('/Sales/AxPostReactivateMeeting?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Activated', text: 'Meeting reactivated.', confirmButtonColor: '#0D5EA6' }).then(function () {
                            loadMeetingsPage(_currentPage);
                        });
                    } else {
                        Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
                    }
                } catch (e) {
                    BlockUI.hide();
                    Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
                }
            }
        });
    };

    // ─── Contacts for Create Modal ──────────────────────────────

    async function loadContactsForMeetingForm() {
        try {
            var response = await fetch('/Sales/AxGetContactsSearch?page=1');
            var result = await response.json();
            if (result.success) {
                var select = document.getElementById('meetingContactId');
                select.innerHTML = '<option value="">Select contact...</option>';
                result.data.forEach(function (c) {
                    select.innerHTML += '<option value="' + c.id + '">' + escapeHtml(c.fullName) + '</option>';
                });

                // Pre-select contact from query string if present
                var params = new URLSearchParams(window.location.search);
                var preselectedContact = params.get('contactId');
                if (preselectedContact) {
                    select.value = preselectedContact;
                    select.disabled = true;
                    select.style.opacity = '0.7';
                    select.style.cursor = 'not-allowed';
                }

                // Auto-open modal if coming from Lead Detail
                var leadRequestId = params.get('leadRequestId');
                if (leadRequestId) {
                    openCreateMeetingModal();
                }
            }
        } catch (e) {
            console.error('Failed to load contacts', e);
        }
    }

    // ─── Initialization ─────────────────────────────────────────

    document.addEventListener('DOMContentLoaded', function () {
        loadMeetingsPage(1);
        loadContactsForMeetingForm();
    });

})();
