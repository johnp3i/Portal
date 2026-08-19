// ═══════════════════════════════════════════════════════════
// Follow-Up Tasks — Shared JS for Pipeline, LeadDetail, Tasks pages
// ═══════════════════════════════════════════════════════════

function getAntiForgeryToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

// ─── Today's Actions Panel (Pipeline page) ──────────────────

var _todaysActionsCollapsed = localStorage.getItem('todaysActionsCollapsed') === 'true';

async function loadTodaysActions() {
    try {
        var response = await fetch('/Sales/AxGetTodaysActions');
        var data = await response.json();
        if (!data.success) return;

        var tasks = data.data;
        var panel = document.getElementById('todaysActionsPanel');
        var list = document.getElementById('todaysActionsList');
        var badges = document.getElementById('todaysBadges');
        var skeleton = document.getElementById('todaysActionsSkeleton');

        // Remove skeleton if present
        if (skeleton) skeleton.remove();

        if (!tasks || tasks.length === 0) {
            panel.style.display = 'none';
            return;
        }

        panel.style.display = '';

        // Count badges
        var overdue = tasks.filter(t => t.urgency === 'overdue').length;
        var today = tasks.filter(t => t.urgency === 'today').length;
        var tomorrow = tasks.filter(t => t.urgency === 'tomorrow').length;
        var upcoming = tasks.filter(t => t.urgency === 'upcoming').length;

        var badgeHtml = '';
        if (overdue > 0) badgeHtml += '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(194,74,74,.08);color:#C24A4A;">' + overdue + ' overdue</span>';
        if (today > 0) badgeHtml += '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(200,145,46,.08);color:#C8912E;">' + today + ' today</span>';
        if (tomorrow > 0) badgeHtml += '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(138,155,171,.08);color:#8a9bab;">' + tomorrow + ' tomorrow</span>';
        if (upcoming > 0) badgeHtml += '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(13,94,166,.06);color:#0D5EA6;">' + upcoming + ' upcoming</span>';
        badges.innerHTML = badgeHtml;

        // Render tasks
        if (_todaysActionsCollapsed) {
            list.style.display = 'none';
            document.getElementById('todaysCollapseIcon').innerHTML = '<path d="M6 9l6 6 6-6"/>';
        } else {
            list.style.display = '';
        }

        var html = '';
        tasks.forEach(function (t) {
            html += renderTaskCard(t);
        });
        list.innerHTML = html;
    } catch (e) {
        console.error('Failed to load today\'s actions', e);
    }
}

function toggleTodaysActions() {
    var list = document.getElementById('todaysActionsList');
    var icon = document.getElementById('todaysCollapseIcon');
    _todaysActionsCollapsed = !_todaysActionsCollapsed;
    localStorage.setItem('todaysActionsCollapsed', _todaysActionsCollapsed);

    if (_todaysActionsCollapsed) {
        list.style.display = 'none';
        icon.innerHTML = '<path d="M6 9l6 6 6-6"/>';
    } else {
        list.style.display = '';
        icon.innerHTML = '<path d="M18 15l-6-6-6 6"/>';
    }
}

// ─── Task Card Rendering ────────────────────────────────────

function renderTaskCard(t) {
    cacheTaskData(t);
    var urgencyClass = t.urgency === 'overdue' ? 'overdue' : t.urgency === 'today' ? 'today' : 'tomorrow';
    var dotColor = t.urgency === 'overdue' ? '#C24A4A' : t.urgency === 'today' ? '#C8912E' : '#b8c4d0';
    var snoozedWarning = t.snoozedCount >= 3 ? ' style="border:1.5px dashed #C8912E;"' : '';

    var typeIcon = getTaskTypeIcon(t.taskType);
    var urgencyBadge = getUrgencyBadge(t);
    var assignedTo = t.assignedToName ? ' &middot; Assigned to: <strong>' + escapeHtml(t.assignedToName) + '</strong>' : '';
    var snoozedNote = t.snoozedCount >= 3 ? ' &middot; Snoozed ' + t.snoozedCount + ' times' : '';

    return '<div class="task-card-action" data-task-id="' + t.id + '"' + snoozedWarning + '>' +
        '<span style="width:8px;height:8px;border-radius:50%;background:' + dotColor + ';flex-shrink:0;"></span>' +
        '<div style="width:32px;height:32px;border-radius:8px;display:flex;align-items:center;justify-content:center;flex-shrink:0;' + getTypeIconBg(t.taskType) + '">' + typeIcon + '</div>' +
        '<div style="flex:1;min-width:0;">' +
            '<div style="font-size:14px;font-weight:600;color:#0B1B28;">' + (t.leadRequestId ? '<a href="/Sales/LeadDetail/' + t.leadRequestId + '" style="color:#0B1B28;text-decoration:none;border-bottom:1px dashed rgba(13,94,166,.3);">' + escapeHtml(t.title) + '</a>' : escapeHtml(t.title)) + '</div>' +
            '<div style="font-size:12px;color:#8a9bab;margin-top:3px;">' + urgencyBadge + assignedTo + snoozedNote + '</div>' +
        '</div>' +
        '<div style="display:flex;gap:6px;flex-shrink:0;align-items:center;">' +
            (t.notes ? '<button class="btn btn-sm" style="background:rgba(13,94,166,.06);color:#0D5EA6;border:1.5px solid rgba(13,94,166,.12);padding:5px 8px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;" onclick="openViewNotesModal(' + t.id + ')" title="View notes">&#128221;</button>' : '') +
            '<button class="btn btn-sm" style="background:rgba(138,155,171,.06);color:#5E7385;border:1.5px solid rgba(138,155,171,.15);padding:5px 8px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;" onclick="openEditTaskModal(' + t.id + ')" title="Edit task">&#9998;</button>' +
            '<button class="btn btn-sm" style="background:rgba(18,152,103,.08);color:#129867;border:1.5px solid rgba(18,152,103,.2);padding:5px 10px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;" onclick="completeTask(' + t.id + ')">&#10003; Complete</button>' +
            '<div style="position:relative;display:inline-block;">' +
                '<button class="btn btn-sm" style="background:rgba(87,184,232,.08);color:#1a8fc7;border:1.5px solid rgba(87,184,232,.2);padding:5px 10px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;" onclick="toggleSnoozeMenu(event, ' + t.id + ')">&#9193; Snooze &#9662;</button>' +
                '<div id="snoozeMenu_' + t.id + '" class="snooze-menu-dropdown" style="display:none;position:absolute;top:100%;right:0;background:#fff;border:1.5px solid rgba(13,94,166,.08);border-radius:10px;box-shadow:0 8px 24px rgba(0,0,0,.1);padding:6px;min-width:150px;z-index:10;margin-top:4px;">' +
                    '<a href="#" onclick="snoozeTask(event,' + t.id + ',1)" style="display:block;padding:8px 12px;font-size:13px;color:#0B1B28;text-decoration:none;border-radius:6px;">+1 day (Tomorrow)</a>' +
                    '<a href="#" onclick="snoozeTask(event,' + t.id + ',3)" style="display:block;padding:8px 12px;font-size:13px;color:#0B1B28;text-decoration:none;border-radius:6px;">+3 days</a>' +
                    '<a href="#" onclick="snoozeTask(event,' + t.id + ',7)" style="display:block;padding:8px 12px;font-size:13px;color:#0B1B28;text-decoration:none;border-radius:6px;">Next week</a>' +
                '</div>' +
            '</div>' +
        '</div>' +
    '</div>';
}

function getTaskTypeIcon(type) {
    switch (type) {
        case 'Call': return '<svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M22 16.92v3a2 2 0 01-2.18 2 19.79 19.79 0 01-8.63-3.07 19.5 19.5 0 01-6-6 19.79 19.79 0 01-3.07-8.67A2 2 0 014.11 2h3a2 2 0 012 1.72c.127.96.361 1.903.7 2.81a2 2 0 01-.45 2.11L8.09 9.91a16 16 0 006 6l1.27-1.27a2 2 0 012.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0122 16.92z"/></svg>';
        case 'Email': return '<svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><path d="M22 6l-10 7L2 6"/></svg>';
        case 'Follow-up': return '<svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M21 11.5a8.38 8.38 0 01-.9 3.8 8.5 8.5 0 01-7.6 4.7 8.38 8.38 0 01-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 01-.9-3.8 8.5 8.5 0 014.7-7.6 8.38 8.38 0 013.8-.9h.5a8.48 8.48 0 018 8v.5z"/></svg>';
        case 'Meeting Prep': return '<svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>';
        default: return '<svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>';
    }
}

function getTypeIconBg(type) {
    switch (type) {
        case 'Call': return 'background:rgba(13,94,166,.08);color:#0D5EA6;';
        case 'Email': return 'background:rgba(18,152,103,.08);color:#129867;';
        case 'Follow-up': return 'background:rgba(200,145,46,.08);color:#C8912E;';
        case 'Meeting Prep': return 'background:rgba(87,184,232,.08);color:#1a8fc7;';
        default: return 'background:rgba(138,155,171,.08);color:#8a9bab;';
    }
}

function getUrgencyBadge(t) {
    if (t.urgency === 'overdue') {
        var daysOverdue = Math.ceil((new Date() - new Date(t.dueAtUtc)) / (1000 * 60 * 60 * 24));
        return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(194,74,74,.08);color:#C24A4A;margin-right:6px;">Overdue ' + daysOverdue + ' day' + (daysOverdue > 1 ? 's' : '') + '</span>';
    } else if (t.urgency === 'today') {
        return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(200,145,46,.08);color:#C8912E;margin-right:6px;">Due today</span>';
    } else if (t.urgency === 'tomorrow') {
        return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(138,155,171,.08);color:#8a9bab;margin-right:6px;">Tomorrow</span>';
    } else {
        var dueDate = new Date(t.dueAtUtc).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
        return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(13,94,166,.06);color:#0D5EA6;margin-right:6px;">Due ' + dueDate + '</span>';
    }
}

// ─── Actions ────────────────────────────────────────────────

async function completeTask(taskId) {
    BlockUI.show('Completing...');
    try {
        var response = await fetch('/Sales/AxPostCompleteTask?id=' + taskId, {
            method: 'POST',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        });
        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            // Remove card from DOM
            var card = document.querySelector('[data-task-id="' + taskId + '"]');
            if (card) card.remove();
            // Refresh panel counts
            loadTodaysActions();
            // Also refresh lead detail if present
            if (typeof loadLeadTasks === 'function') loadLeadTasks();
        } else {
            Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
    }
}

function toggleSnoozeMenu(event, taskId) {
    event.stopPropagation();
    // Close all other menus
    document.querySelectorAll('.snooze-menu-dropdown').forEach(function (m) {
        if (m.id !== 'snoozeMenu_' + taskId) m.style.display = 'none';
    });
    var menu = document.getElementById('snoozeMenu_' + taskId);
    menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
}

async function snoozeTask(event, taskId, days) {
    event.preventDefault();
    event.stopPropagation();

    var newDate = new Date();
    newDate.setDate(newDate.getDate() + days);
    var isoDate = newDate.toISOString();

    // Hide menu
    document.getElementById('snoozeMenu_' + taskId).style.display = 'none';

    BlockUI.show('Snoozing...');
    try {
        var response = await fetch('/Sales/AxPostSnoozeTask?id=' + taskId + '&newDueDate=' + encodeURIComponent(isoDate), {
            method: 'POST',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        });
        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            loadTodaysActions();
            if (typeof loadLeadTasks === 'function') loadLeadTasks();
        } else {
            Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
    }
}

// ─── Create Task Modal ──────────────────────────────────────

function openCreateTaskModal(leadRequestId, contactId, contactName) {
    var titleDefault = contactName ? 'Follow up \u2014 ' + contactName : '';

    var modalHtml = '<div id="createTaskModal" style="position:fixed;inset:0;background:rgba(11,27,40,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">' +
        '<div style="background:#fff;border-radius:20px;padding:32px;width:440px;max-width:95vw;box-shadow:0 20px 60px rgba(0,0,0,.15);">' +
            '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:20px;">' +
                '<h3 style="font-family:Manrope,sans-serif;font-size:18px;font-weight:700;margin:0;">Schedule Follow-up</h3>' +
                '<button onclick="closeCreateTaskModal()" style="background:none;border:none;cursor:pointer;color:#8a9bab;font-size:22px;">&times;</button>' +
            '</div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Title</label><input type="text" id="taskTitle" value="' + escapeAttr(titleDefault) + '" maxlength="200" /></div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Type</label><select id="taskType">' +
                '<option value="Call">Call</option>' +
                '<option value="Email">Email</option>' +
                '<option value="Follow-up" selected>Follow-up</option>' +
                '<option value="Meeting Prep">Meeting Prep</option>' +
                '<option value="Other">Other</option>' +
            '</select></div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Due Date</label>' +
                '<div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:10px;">' +
                    '<button class="preset-btn" onclick="setTaskPreset(1)">Tomorrow</button>' +
                    '<button class="preset-btn active" onclick="setTaskPreset(3)">In 3 days</button>' +
                    '<button class="preset-btn" onclick="setTaskPreset(7)">Next week</button>' +
                    '<button class="preset-btn" onclick="setTaskPresetNextMonday()">Next Monday</button>' +
                '</div>' +
                '<input type="date" id="taskDueDate" value="' + getPresetDate(3) + '" />' +
            '</div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Notes (optional)</label><textarea id="taskNotes" rows="2" placeholder="Add context for the follow-up..."></textarea></div>' +
            '<input type="hidden" id="taskLeadRequestId" value="' + (leadRequestId || '') + '" />' +
            '<input type="hidden" id="taskContactId" value="' + (contactId || '') + '" />' +
            '<div style="display:flex;justify-content:flex-end;gap:10px;margin-top:20px;">' +
                '<button class="btn btn-secondary" onclick="closeCreateTaskModal()">Cancel</button>' +
                '<button class="btn btn-primary" onclick="submitCreateTask()">Create Task</button>' +
            '</div>' +
        '</div>' +
    '</div>';

    document.body.insertAdjacentHTML('beforeend', modalHtml);
}

function closeCreateTaskModal() {
    var modal = document.getElementById('createTaskModal');
    if (modal) modal.remove();
}

function setTaskPreset(days) {
    document.getElementById('taskDueDate').value = getPresetDate(days);
    document.querySelectorAll('#createTaskModal .preset-btn').forEach(function (b) { b.classList.remove('active'); });
    event.target.classList.add('active');
}

function setTaskPresetNextMonday() {
    var d = new Date();
    var day = d.getDay();
    var diff = day === 0 ? 1 : 8 - day;
    d.setDate(d.getDate() + diff);
    document.getElementById('taskDueDate').value = d.toISOString().split('T')[0];
    document.querySelectorAll('#createTaskModal .preset-btn').forEach(function (b) { b.classList.remove('active'); });
    event.target.classList.add('active');
}

function getPresetDate(days) {
    var d = new Date();
    d.setDate(d.getDate() + days);
    return d.toISOString().split('T')[0];
}

async function submitCreateTask() {
    var title = document.getElementById('taskTitle').value.trim();
    if (!title) {
        Swal.fire({ icon: 'warning', title: 'Required', text: 'Please enter a title.', confirmButtonColor: '#0D5EA6' });
        return;
    }

    var payload = {
        title: title,
        taskType: document.getElementById('taskType').value,
        dueAtUtc: document.getElementById('taskDueDate').value + 'T09:00:00Z',
        notes: document.getElementById('taskNotes').value.trim() || null,
        leadRequestId: document.getElementById('taskLeadRequestId').value ? parseInt(document.getElementById('taskLeadRequestId').value) : null,
        contactId: document.getElementById('taskContactId').value ? parseInt(document.getElementById('taskContactId').value) : null,
        teamMemberId: null
    };

    BlockUI.show('Creating task...');
    try {
        var response = await fetch('/Sales/AxPostCreateTask', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify(payload)
        });
        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            closeCreateTaskModal();
            // Small delay to ensure server has committed, then refresh
            setTimeout(function() {
                if (typeof loadTodaysActions === 'function') loadTodaysActions();
                if (typeof loadLeadTasks === 'function') loadLeadTasks();
                if (typeof loadTasksPage === 'function') loadTasksPage(1);
            }, 300);
            Swal.fire({ icon: 'success', title: 'Task Created', text: 'Follow-up task has been scheduled.', confirmButtonColor: '#0D5EA6' });
        } else {
            Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
    }
}

// ─── View Notes Modal ────────────────────────────────────────

function openViewNotesModal(taskId) {
    var t = _taskDataCache[taskId];
    if (!t || !t.notes) return;

    var modalHtml = '<div id="viewNotesModal" style="position:fixed;inset:0;background:rgba(11,27,40,.4);display:flex;align-items:center;justify-content:center;z-index:1000;" onclick="if(event.target===this)closeViewNotesModal()">' +
        '<div style="background:#fff;border-radius:20px;padding:32px;width:440px;max-width:95vw;box-shadow:0 20px 60px rgba(0,0,0,.15);">' +
            '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:16px;">' +
                '<h3 style="font-family:Manrope,sans-serif;font-size:16px;font-weight:700;margin:0;">Task Notes</h3>' +
                '<button onclick="closeViewNotesModal()" style="background:none;border:none;cursor:pointer;color:#8a9bab;font-size:22px;">&times;</button>' +
            '</div>' +
            '<div style="font-size:13px;font-weight:600;color:#5E7385;margin-bottom:12px;">' + escapeHtml(t.title) + '</div>' +
            '<div style="padding:16px;background:#f7fafc;border-radius:12px;border:1px solid rgba(13,94,166,.08);font-size:14px;color:#0B1B28;line-height:1.6;white-space:pre-wrap;">' + escapeHtml(t.notes) + '</div>' +
            '<div style="display:flex;justify-content:flex-end;gap:10px;margin-top:20px;">' +
                '<button class="btn btn-secondary" onclick="closeViewNotesModal()">Close</button>' +
                '<button class="btn btn-primary" onclick="closeViewNotesModal();openEditTaskModal(' + t.id + ')">Edit Task</button>' +
            '</div>' +
        '</div>' +
    '</div>';

    document.body.insertAdjacentHTML('beforeend', modalHtml);
}

function closeViewNotesModal() {
    var modal = document.getElementById('viewNotesModal');
    if (modal) modal.remove();
}

// ─── Edit Task Modal ────────────────────────────────────────

// Store task data for editing (populated by renderTaskCard or fetched)
var _taskDataCache = {};

function cacheTaskData(t) {
    _taskDataCache[t.id] = t;
}

function openEditTaskModal(taskId) {
    var t = _taskDataCache[taskId];
    if (!t) {
        // If not cached, we can't open - shouldn't happen
        Swal.fire({ icon: 'warning', title: 'Cannot edit', text: 'Task data not available. Please refresh the page.', confirmButtonColor: '#0D5EA6' });
        return;
    }

    var dueDate = t.dueAtUtc ? t.dueAtUtc.split('T')[0] : '';

    var modalHtml = '<div id="editTaskModal" style="position:fixed;inset:0;background:rgba(11,27,40,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">' +
        '<div style="background:#fff;border-radius:20px;padding:32px;width:480px;max-width:95vw;box-shadow:0 20px 60px rgba(0,0,0,.15);">' +
            '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:20px;">' +
                '<h3 style="font-family:Manrope,sans-serif;font-size:18px;font-weight:700;margin:0;">Edit Task</h3>' +
                '<button onclick="closeEditTaskModal()" style="background:none;border:none;cursor:pointer;color:#8a9bab;font-size:22px;">&times;</button>' +
            '</div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Title</label><input type="text" id="editTaskTitle" value="' + escapeAttr(t.title) + '" maxlength="200" /></div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Type</label><select id="editTaskType">' +
                '<option value="Call"' + (t.taskType === 'Call' ? ' selected' : '') + '>Call</option>' +
                '<option value="Email"' + (t.taskType === 'Email' ? ' selected' : '') + '>Email</option>' +
                '<option value="Follow-up"' + (t.taskType === 'Follow-up' ? ' selected' : '') + '>Follow-up</option>' +
                '<option value="Meeting Prep"' + (t.taskType === 'Meeting Prep' ? ' selected' : '') + '>Meeting Prep</option>' +
                '<option value="Other"' + (t.taskType === 'Other' ? ' selected' : '') + '>Other</option>' +
            '</select></div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Due Date</label><input type="date" id="editTaskDueDate" value="' + dueDate + '" /></div>' +
            '<div class="field" style="margin-bottom:18px;"><label>Notes</label><textarea id="editTaskNotes" rows="4" placeholder="Add notes, comments, or context...">' + escapeHtml(t.notes || '') + '</textarea></div>' +
            '<input type="hidden" id="editTaskId" value="' + t.id + '" />' +
            '<div style="display:flex;justify-content:flex-end;gap:10px;margin-top:20px;">' +
                '<button class="btn btn-secondary" onclick="closeEditTaskModal()">Cancel</button>' +
                '<button class="btn btn-primary" onclick="submitEditTask()">Save Changes</button>' +
            '</div>' +
        '</div>' +
    '</div>';

    document.body.insertAdjacentHTML('beforeend', modalHtml);
}

function closeEditTaskModal() {
    var modal = document.getElementById('editTaskModal');
    if (modal) modal.remove();
}

async function submitEditTask() {
    var title = document.getElementById('editTaskTitle').value.trim();
    if (!title) {
        Swal.fire({ icon: 'warning', title: 'Required', text: 'Please enter a title.', confirmButtonColor: '#0D5EA6' });
        return;
    }

    var dueValue = document.getElementById('editTaskDueDate').value;
    if (!dueValue) {
        Swal.fire({ icon: 'warning', title: 'Required', text: 'Please select a due date.', confirmButtonColor: '#0D5EA6' });
        return;
    }

    var payload = {
        id: parseInt(document.getElementById('editTaskId').value),
        title: title,
        taskType: document.getElementById('editTaskType').value,
        dueAtUtc: dueValue + 'T09:00:00Z',
        notes: document.getElementById('editTaskNotes').value.trim() || null
    };

    BlockUI.show('Saving...');
    try {
        var response = await fetch('/Sales/AxPostUpdateTask', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify(payload)
        });
        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            closeEditTaskModal();
            Swal.fire({ icon: 'success', title: 'Saved', text: 'Task updated.', timer: 1500, showConfirmButton: false });
            setTimeout(function() {
                if (typeof loadTodaysActions === 'function') loadTodaysActions();
                if (typeof loadLeadTasks === 'function') loadLeadTasks();
                if (typeof loadTasksPage === 'function') loadTasksPage(1);
            }, 200);
        } else {
            Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
    }
}

// ─── Utilities ──────────────────────────────────────────────

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function escapeAttr(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ─── Close snooze menus on outside click ────────────────────
document.addEventListener('click', function () {
    document.querySelectorAll('.snooze-menu-dropdown').forEach(function (m) { m.style.display = 'none'; });
});

// ─── Auto-load on pipeline page ─────────────────────────────
if (document.getElementById('todaysActionsPanel')) {
    loadTodaysActions();
}

// ─── Navigation badge — overdue count ───────────────────────
(function loadNavOverdueBadge() {
    var badge = document.getElementById('navTaskOverdueBadge');
    if (!badge) return;

    fetch('/Sales/AxGetOverdueTaskCount')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (data.success && data.count > 0) {
                badge.textContent = data.count;
                badge.style.display = 'inline-block';
            } else {
                badge.style.display = 'none';
            }
        })
        .catch(function () { badge.style.display = 'none'; });
})();
