/**
 * Prospects & Campaigns — CRUD, activities, convert-to-lead, and Excel import.
 * Follows the platform AJAX pattern: BlockUI.show -> fetch -> BlockUI.hide -> Swal.
 */
(function () {
    'use strict';

    function token() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function jsonHeaders() {
        return { 'Content-Type': 'application/json', 'RequestVerificationToken': token() };
    }

    function esc(v) {
        if (v === null || v === undefined) return '';
        return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function ok(title, text) {
        return Swal.fire({ icon: 'success', title: title, text: text, confirmButtonColor: '#0D5EA6' });
    }
    function err(text) {
        return Swal.fire({ icon: 'error', title: 'Error', text: text, confirmButtonColor: '#0D5EA6' });
    }
    function warn(text) {
        return Swal.fire({ icon: 'warning', title: 'Validation', text: text, confirmButtonColor: '#0D5EA6' });
    }

    async function postJson(url, payload) {
        var response = await fetch(url, { method: 'POST', headers: jsonHeaders(), body: JSON.stringify(payload) });
        return await response.json();
    }

    function val(id) { var el = document.getElementById(id); return el ? el.value : ''; }
    function num(id) { var v = val(id); return v === '' ? null : parseInt(v, 10); }
    function strOrNull(id) { var v = val(id); return v && v.trim() !== '' ? v.trim() : null; }

    // ══════════════════════════════════════════════════════════
    // CAMPAIGNS
    // ══════════════════════════════════════════════════════════

    window.openCreateCampaignModal = function () {
        document.getElementById('campaignModalTitle').textContent = 'New Campaign';
        document.getElementById('campaignId').value = '';
        ['campaignName', 'campaignMarket', 'campaignNotes'].forEach(function (id) { document.getElementById(id).value = ''; });
        document.getElementById('campaignProduct').value = '';
        document.getElementById('campaignStart').value = '';
        document.getElementById('campaignEnd').value = '';
        document.getElementById('campaignTarget').value = '0';
        document.getElementById('campaignStatusField').style.display = 'none';
        document.getElementById('campaignModal').style.display = 'flex';
    };

    window.editCampaign = function (id, name, productId, market, start, end, target, status) {
        document.getElementById('campaignModalTitle').textContent = 'Edit Campaign';
        document.getElementById('campaignId').value = id;
        document.getElementById('campaignName').value = name || '';
        document.getElementById('campaignProduct').value = productId === null || productId === undefined ? '' : productId;
        document.getElementById('campaignMarket').value = market || '';
        document.getElementById('campaignStart').value = start || '';
        document.getElementById('campaignEnd').value = end || '';
        document.getElementById('campaignTarget').value = target != null ? target : 0;
        document.getElementById('campaignStatusField').style.display = 'block';
        document.getElementById('campaignStatus').value = status;
        document.getElementById('campaignModal').style.display = 'flex';
    };

    window.closeCampaignModal = function () {
        document.getElementById('campaignModal').style.display = 'none';
    };

    window.submitCampaign = async function () {
        var id = val('campaignId');
        var payload = {
            name: val('campaignName'),
            salesProductId: num('campaignProduct'),
            market: strOrNull('campaignMarket'),
            startDate: strOrNull('campaignStart'),
            endDate: strOrNull('campaignEnd'),
            weeklyCallTarget: parseInt(val('campaignTarget') || '0', 10),
            notes: strOrNull('campaignNotes')
        };
        if (!payload.name) { warn('Campaign name is required.'); return; }

        var url = '/Prospecting/AxPostCreateCampaign';
        if (id) {
            url = '/Prospecting/AxPostUpdateCampaign';
            payload.id = parseInt(id, 10);
            payload.status = parseInt(val('campaignStatus') || '2', 10);
        }

        BlockUI.show('Saving...');
        try {
            var result = await postJson(url, payload);
            BlockUI.hide();
            if (result.success) {
                closeCampaignModal();
                ok('Saved', 'Campaign saved.').then(function () { window.location.reload(); });
            } else { err(result.message); }
        } catch (e) { BlockUI.hide(); err('An unexpected error occurred.'); }
    };

    // ══════════════════════════════════════════════════════════
    // PROSPECTS — working list (AJAX-loaded table)
    // ══════════════════════════════════════════════════════════

    var listCampaignId = 0;
    var currentPage = 1;

    window.initProspectsList = function (campaignId) {
        listCampaignId = campaignId;
        loadProspects(1);
    };

    window.applyProspectFilters = function () { loadProspects(1); };
    window.clearProspectFilters = function () {
        document.getElementById('filterPriority').value = '';
        document.getElementById('filterStatus').value = '';
        document.getElementById('filterSegment').value = '';
        document.getElementById('filterNextAction').value = '';
        loadProspects(1);
    };

    function priorityPill(p) {
        if (p === 'A') return 'pill-green';
        if (p === 'B') return 'pill-gold';
        return 'pill-grey';
    }
    function statusPill(s) {
        switch (s) {
            case 3: return 'pill-blue';
            case 4: return 'pill-cyan';
            case 5: return 'pill-green';
            case 6: return 'pill-grey';
            default: return 'pill-slate';
        }
    }
    var STATUS_NAMES = { 1: 'Research', 2: 'Ready', 3: 'Contacting', 4: 'Engaged', 5: 'Converted', 6: 'Disqualified' };
    var ACTIVITY_NAMES = { 1: 'Research', 2: 'Call', 3: 'Email', 4: 'Meeting', 5: 'Demo', 6: 'Note', 7: 'Social', 8: 'Other' };

    async function loadProspects(page) {
        currentPage = page || 1;
        var params = new URLSearchParams();
        params.set('campaignId', listCampaignId);
        params.set('page', currentPage);
        params.set('pageSize', 15);
        var pr = val('filterPriority'); if (pr) params.set('priority', pr);
        var st = val('filterStatus'); if (st) params.set('status', st);
        var sg = val('filterSegment'); if (sg) params.set('segment', sg);
        var na = val('filterNextAction'); if (na) params.set('nextAction', na);

        var body = document.getElementById('prospectTableBody');
        BlockUI.show('Loading...');
        try {
            var response = await fetch('/Prospecting/AxGetProspects?' + params.toString());
            var result = await response.json();
            BlockUI.hide();

            if (!result.success) { err(result.message); return; }
            renderProspects(result.data, result.pagination);
        } catch (e) {
            BlockUI.hide();
            body.innerHTML = '<tr><td colspan="8" style="text-align:center;color:#C24A4A;padding:24px;">Failed to load prospects.</td></tr>';
        }
    }

    function fmtDate(iso) {
        if (!iso) return '';
        var d = new Date(iso);
        if (isNaN(d)) return '';
        return d.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
    }

    function renderProspects(items, pagination) {
        var body = document.getElementById('prospectTableBody');
        if (!items || items.length === 0) {
            body.innerHTML = '<tr><td colspan="8" style="text-align:center;color:#73889a;padding:24px;">No prospects match these filters.</td></tr>';
        } else {
            var html = '';
            items.forEach(function (p) {
                var last = p.lastActivityAtUtc
                    ? fmtDate(p.lastActivityAtUtc) + ' · ' + (ACTIVITY_NAMES[p.lastActivityType] || '')
                    : '—';
                var next = p.nextAction
                    ? '<span style="color:#C8912E;font-weight:700;">' + (p.nextActionDate ? fmtDate(p.nextActionDate) + ' — ' : '') + esc(p.nextAction) + '</span>'
                    : '<span style="color:#73889a;">—</span>';
                var action = (p.status === 4)
                    ? '<button class="tbl-action tbl-action--primary" onclick="location.href=\'/Prospecting/ProspectDetail/' + p.id + '\'">Convert</button>'
                    : '';
                html += '<tr>' +
                    '<td><a href="/Prospecting/ProspectDetail/' + p.id + '" style="color:var(--blue);font-weight:600;">' + esc(p.name) + '</a>' +
                    (p.location ? '<div style="font-size:11px;color:#73889a;">' + esc(p.location) + '</div>' : '') + '</td>' +
                    '<td>' + esc(p.segment || '—') + '</td>' +
                    '<td><span class="pill pill-sm ' + priorityPill(p.priority) + '">' + esc(p.priority) + '</span></td>' +
                    '<td style="font-weight:700;">' + p.total + '</td>' +
                    '<td><span class="pill pill-sm ' + statusPill(p.status) + '">' + (STATUS_NAMES[p.status] || '') + '</span></td>' +
                    '<td>' + esc(last) + '</td>' +
                    '<td>' + next + '</td>' +
                    '<td style="white-space:nowrap;text-align:right;">' +
                    '<a href="/Prospecting/ProspectDetail/' + p.id + '" class="tbl-action tbl-action--secondary">Open</a>' + action + '</td>' +
                    '</tr>';
            });
            body.innerHTML = html;
        }
        renderPagination(pagination);
    }

    function renderPagination(p) {
        var info = document.getElementById('prospectPaginationInfo');
        var controls = document.getElementById('prospectPaginationControls');
        if (!p || p.totalCount === 0) { info.textContent = ''; controls.innerHTML = ''; return; }
        var from = (p.currentPage - 1) * p.pageSize + 1;
        var to = Math.min(p.currentPage * p.pageSize, p.totalCount);
        info.textContent = 'Showing ' + from + '–' + to + ' of ' + p.totalCount;
        var html = '';
        if (p.currentPage > 1) html += '<button class="btn btn-secondary" style="padding:6px 12px;font-size:13px;" onclick="gotoProspectPage(' + (p.currentPage - 1) + ')">Previous</button> ';
        if (p.currentPage < p.totalPages) html += '<button class="btn btn-secondary" style="padding:6px 12px;font-size:13px;" onclick="gotoProspectPage(' + (p.currentPage + 1) + ')">Next</button>';
        controls.innerHTML = html;
    }
    window.gotoProspectPage = function (page) { loadProspects(page); };

    // ── Add / Edit prospect ──
    window.openCreateProspectModal = function () {
        document.getElementById('prospectModalTitle').textContent = 'Add Prospect';
        document.getElementById('prospectId').value = '';
        ['prospectName', 'prospectSegment', 'prospectLocation', 'prospectBusinessType', 'prospectContactRole',
            'prospectPhone', 'prospectEmail', 'prospectWebsite', 'prospectWhyFit', 'prospectEvidence',
            'prospectSourceUrl', 'prospectFirstContact', 'prospectNextAction', 'prospectNextActionDate'].forEach(function (id) {
                var el = document.getElementById(id); if (el) el.value = '';
            });
        ['prospectIcp', 'prospectPain', 'prospectAccess', 'prospectLearning'].forEach(function (id) { document.getElementById(id).value = '0'; });
        document.getElementById('prospectModal').style.display = 'flex';
    };

    window.closeProspectModal = function () { document.getElementById('prospectModal').style.display = 'none'; };

    function scoreOk(id) { var v = parseInt(val(id) || '0', 10); return v >= 0 && v <= 5; }

    window.submitProspect = async function () {
        var name = val('prospectName');
        if (!name) { warn('Prospect name is required.'); return; }
        if (!scoreOk('prospectIcp') || !scoreOk('prospectPain') || !scoreOk('prospectAccess') || !scoreOk('prospectLearning')) {
            warn('Each score must be between 0 and 5.'); return;
        }
        var campaignId = parseInt(document.getElementById('campaignIdField').value, 10);
        var id = val('prospectId');
        var payload = {
            prospectCampaignId: campaignId,
            name: name,
            segment: strOrNull('prospectSegment'),
            location: strOrNull('prospectLocation'),
            businessType: strOrNull('prospectBusinessType'),
            publicContactRole: strOrNull('prospectContactRole'),
            phone: strOrNull('prospectPhone'),
            email: strOrNull('prospectEmail'),
            website: strOrNull('prospectWebsite'),
            whyFit: strOrNull('prospectWhyFit'),
            publicEvidence: strOrNull('prospectEvidence'),
            researchSourceUrl: strOrNull('prospectSourceUrl'),
            recommendedFirstContact: strOrNull('prospectFirstContact'),
            icpFitScore: parseInt(val('prospectIcp') || '0', 10),
            painProbabilityScore: parseInt(val('prospectPain') || '0', 10),
            accessibilityScore: parseInt(val('prospectAccess') || '0', 10),
            learningValueScore: parseInt(val('prospectLearning') || '0', 10),
            nextAction: strOrNull('prospectNextAction'),
            nextActionDate: strOrNull('prospectNextActionDate')
        };
        var url = '/Prospecting/AxPostCreateProspect';
        if (id) { url = '/Prospecting/AxPostUpdateProspect'; payload.id = parseInt(id, 10); }

        BlockUI.show('Saving...');
        try {
            var result = await postJson(url, payload);
            BlockUI.hide();
            if (result.success) {
                closeProspectModal();
                ok('Saved', 'Prospect saved.').then(function () { loadProspects(currentPage); });
            } else { err(result.message); }
        } catch (e) { BlockUI.hide(); err('An unexpected error occurred.'); }
    };

    // ══════════════════════════════════════════════════════════
    // PROSPECT DETAIL — status, activity, convert
    // ══════════════════════════════════════════════════════════

    window.changeProspectStatus = function (prospectId, status) {
        BlockUI.show('Updating...');
        fetch('/Prospecting/AxPostUpdateProspectStatus?prospectId=' + prospectId + '&status=' + status, {
            method: 'POST', headers: { 'RequestVerificationToken': token() }
        }).then(function (r) { return r.json(); }).then(function (data) {
            BlockUI.hide();
            if (data.success) { ok('Done', 'Status updated.').then(function () { window.location.reload(); }); }
            else { err(data.message); window.location.reload(); }
        }).catch(function () { BlockUI.hide(); err('An unexpected error occurred.'); });
    };

    function setEl(id, v) { var el = document.getElementById(id); if (el) el.value = (v === null || v === undefined) ? '' : v; }
    function showEl(id, show) { var el = document.getElementById(id); if (el) el.style.display = show ? '' : 'none'; }

    // Type-aware outcome presets (chips populate the Outcome field).
    var ACTIVITY_PRESETS = {
        '1': ['Strong ICP', 'Weak fit', 'Needs more research'],
        '2': ['No answer', 'Left voicemail', 'Gatekeeper', 'Spoke — interested', 'Spoke — not now'],
        '3': ['Intro sent', 'Replied', 'Bounced', 'No reply yet'],
        '4': ['Meeting booked', 'Met — positive', 'Met — no fit'],
        '5': ['Demo booked', 'Demo done — interested', 'Demo done — not now'],
        '6': [],
        '7': ['DM sent', 'Connected', 'Followed'],
        '8': []
    };

    function renderActivityPresets(type) {
        var box = document.getElementById('activityPresets');
        if (!box) return;
        box.innerHTML = '';
        (ACTIVITY_PRESETS[type] || []).forEach(function (p) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'act-chip';
            b.textContent = p;
            b.onclick = function () { setEl('activityOutcome', p); };
            box.appendChild(b);
        });
    }

    window.setActivityWhen = function (which) {
        var d = new Date();
        if (which === 'yesterday') d.setDate(d.getDate() - 1);
        if (which === 'today') d.setHours(9, 0, 0, 0);
        var pad = function (n) { return String(n).padStart(2, '0'); };
        setEl('activityOccurredAt', d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes()));
    };

    // Next-action shortcut chips (fill the text field).
    window.setNextAction = function (text) { setEl('activityNextAction', text); };

    // Due-date shortcut chips (set the date input relative to today).
    window.setNextActionDate = function (which) {
        var d = new Date();
        switch (which) {
            case 'tomorrow': d.setDate(d.getDate() + 1); break;
            case '3d': d.setDate(d.getDate() + 3); break;
            case 'nextweek': d.setDate(d.getDate() + 7); break;
            case '2w': d.setDate(d.getDate() + 14); break;
            case '1m': d.setMonth(d.getMonth() + 1); break;
        }
        var pad = function (n) { return String(n).padStart(2, '0'); };
        setEl('activityNextActionDate', d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()));
    };

    var activitySentiment = null; // null | 1 | 2 | 3
    window.pickActivitySentiment = function (btn) {
        var v = btn.getAttribute('data-v');
        // Toggle off if the same one is clicked again.
        if (activitySentiment === v) { activitySentiment = null; clearSentimentButtons(); return; }
        activitySentiment = v;
        clearSentimentButtons();
        btn.className = v === '1' ? 'on-pos' : (v === '2' ? 'on-neu' : 'on-neg');
    };
    function clearSentimentButtons() {
        var seg = document.getElementById('activitySentiment');
        if (!seg) return;
        seg.querySelectorAll('button').forEach(function (b) { b.className = ''; });
    }
    function setSentiment(v) {
        activitySentiment = (v === null || v === undefined || v === '') ? null : String(v);
        clearSentimentButtons();
        if (activitySentiment) {
            var seg = document.getElementById('activitySentiment');
            var btn = seg ? seg.querySelector('button[data-v="' + activitySentiment + '"]') : null;
            if (btn) btn.className = activitySentiment === '1' ? 'on-pos' : (activitySentiment === '2' ? 'on-neu' : 'on-neg');
        }
    }

    window.toggleActivityFollowUp = function (cb) {
        showEl('activityFollowUpBox', cb.checked);
    };
    function setFollowUp(on) {
        var cb = document.getElementById('activityIsFollowUp');
        if (cb) { cb.checked = !!on; showEl('activityFollowUpBox', !!on); }
    }

    window.openActivityModal = function () {
        // Add mode
        setEl('activityId', '');
        document.getElementById('activityModalTitle').textContent = 'Add Activity';
        setEl('activityType', '2'); // default Call — the most common logged action
        setEl('activityNewStatus', '');
        setEl('activityOccurredAt', '');
        setEl('activityOutcome', '');
        setEl('activityNotes', '');
        setEl('activityNextAction', '');
        setEl('activityNextActionDate', '');
        setSentiment(null);
        setFollowUp(false);
        renderActivityPresets('2');
        setActivityWhen('now');
        showEl('activityNewStatusField', true); // status advance only relevant when adding
        document.getElementById('activityModal').style.display = 'flex';
    };

    window.editActivityFromRow = function (btn) {
        var row = btn.closest('.activity-row');
        if (!row) return;
        setEl('activityId', row.getAttribute('data-id'));
        document.getElementById('activityModalTitle').textContent = 'Edit Activity';
        var type = row.getAttribute('data-type');
        setEl('activityType', type);
        renderActivityPresets(type);
        setEl('activityOccurredAt', row.getAttribute('data-occurred'));   // "yyyy-MM-ddTHH:mm"
        setEl('activityOutcome', row.getAttribute('data-outcome'));
        setEl('activityNotes', row.getAttribute('data-notes'));
        setEl('activityNextAction', row.getAttribute('data-nextaction'));
        setEl('activityNextActionDate', row.getAttribute('data-nextactiondate'));
        setSentiment(row.getAttribute('data-sentiment'));
        setFollowUp(row.getAttribute('data-isfollowup') === '1');
        showEl('activityNewStatusField', false); // editing a past entry shouldn't move the prospect's status
        document.getElementById('activityModal').style.display = 'flex';
    };

    window.closeActivityModal = function () { document.getElementById('activityModal').style.display = 'none'; };

    window.submitActivity = async function () {
        var editingId = val('activityId');
        var occurred = strOrNull('activityOccurredAt'); // datetime-local -> "yyyy-MM-ddTHH:mm" or null
        var isFollowUp = !!(document.getElementById('activityIsFollowUp') && document.getElementById('activityIsFollowUp').checked);
        var sentiment = activitySentiment ? parseInt(activitySentiment, 10) : null;

        if (editingId) {
            var updatePayload = {
                id: parseInt(editingId, 10),
                activityType: parseInt(val('activityType'), 10),
                occurredAtUtc: occurred,
                sentiment: sentiment,
                isFollowUp: isFollowUp,
                outcome: strOrNull('activityOutcome'),
                notes: strOrNull('activityNotes'),
                nextAction: strOrNull('activityNextAction'),
                nextActionDate: strOrNull('activityNextActionDate')
            };
            BlockUI.show('Saving...');
            try {
                var upd = await postJson('/Prospecting/AxPostUpdateActivity', updatePayload);
                BlockUI.hide();
                if (upd.success) {
                    closeActivityModal();
                    ok('Saved', 'Activity updated.').then(function () { window.location.reload(); });
                } else { err(upd.message); }
            } catch (e) { BlockUI.hide(); err('An unexpected error occurred.'); }
            return;
        }

        var payload = {
            prospectId: parseInt(val('activityProspectId'), 10),
            activityType: parseInt(val('activityType'), 10),
            occurredAtUtc: occurred,
            sentiment: sentiment,
            isFollowUp: isFollowUp,
            outcome: strOrNull('activityOutcome'),
            notes: strOrNull('activityNotes'),
            nextAction: strOrNull('activityNextAction'),
            nextActionDate: strOrNull('activityNextActionDate'),
            newStatus: num('activityNewStatus')
        };
        BlockUI.show('Saving...');
        try {
            var result = await postJson('/Prospecting/AxPostAddActivity', payload);
            BlockUI.hide();
            if (result.success) {
                closeActivityModal();
                ok('Recorded', 'Activity added.').then(function () { window.location.reload(); });
            } else { err(result.message); }
        } catch (e) { BlockUI.hide(); err('An unexpected error occurred.'); }
    };

    // Re-render outcome presets whenever the type changes.
    document.addEventListener('DOMContentLoaded', function () {
        var typeSel = document.getElementById('activityType');
        if (typeSel) typeSel.addEventListener('change', function () { renderActivityPresets(this.value); });
    });

    window.openConvertModal = function () { document.getElementById('convertModal').style.display = 'flex'; };
    window.closeConvertModal = function () { document.getElementById('convertModal').style.display = 'none'; };

    window.submitConvert = async function () {
        var firstName = val('convertFirstName');
        if (!firstName) { warn('A first name is required to create the contact.'); return; }
        var email = strOrNull('convertEmail');
        var phone = strOrNull('convertPhone');
        if (!email && !phone) { warn('Either an email or a phone number is required.'); return; }

        var payload = {
            prospectId: parseInt(val('convertProspectId'), 10),
            firstName: firstName,
            lastName: strOrNull('convertLastName'),
            email: email,
            phoneNumber: phone,
            companyName: strOrNull('convertCompany'),
            jobTitle: strOrNull('convertJobTitle'),
            country: strOrNull('convertCountry'),
            requestText: strOrNull('convertRequestText')
        };

        BlockUI.show('Converting...');
        try {
            var result = await postJson('/Prospecting/AxPostConvertToLead', payload);
            BlockUI.hide();
            if (result.success) {
                closeConvertModal();
                Swal.fire({
                    icon: 'success', title: 'Converted',
                    text: 'Contact and lead created. This prospect is now marked Converted.',
                    confirmButtonColor: '#0D5EA6'
                }).then(function () { window.location.reload(); });
            } else { err(result.message); }
        } catch (e) { BlockUI.hide(); err('Conversion failed. No contact or lead was created.'); }
    };

    // ══════════════════════════════════════════════════════════
    // IMPORT — preview + confirm
    // ══════════════════════════════════════════════════════════

    // ── Ask AI for prospects: generates a copy-ready prompt that teaches the AI the exact format ──
    window.openAiModal = function () {
        var modal = document.getElementById('aiModal');
        if (!modal) return;
        generateAiPrompt();
        modal.style.display = 'flex';
    };
    window.closeAiModal = function () {
        var modal = document.getElementById('aiModal');
        if (modal) modal.style.display = 'none';
    };

    window.generateAiPrompt = function () {
        var industry = (val('aiIndustry') || '[your product]').trim();
        var region = (val('aiRegion') || '[your region]').trim();
        var desc = (val('aiDesc') || '[who you want to reach]').trim();
        var count = val('aiCount') || '30';

        var p =
            'You are a market research assistant. Find ' + count + ' real prospective customers for the following product and return them as a table I can paste into a spreadsheet.\n\n' +
            'PRODUCT / INDUSTRY: ' + industry + '\n' +
            'TARGET MARKET / REGION: ' + region + '\n' +
            'WHO I WANT: ' + desc + '\n\n' +
            'Return a Markdown table with EXACTLY these columns, in this order:\n' +
            'ID | Prospect | Segment | Location | Business Type | Public Contact / Role | Phone | Email | Official Website | Public Evidence | Why It May Fit | ICP Fit /5 | Pain Probability /5 | Accessibility /5 | Learning Value /5 | Recommended First Contact | Research Source\n\n' +
            'RULES:\n' +
            '- ID: a short row id like A01, A02, ...\n' +
            '- Prospect is required (the business or person name).\n' +
            '- Use only real, publicly verifiable organisations; put the source URL in "Research Source".\n' +
            '- Do NOT invent phone/email — leave blank if not public.\n' +
            '- Score each of the four dimensions from 0 to 5 (whole numbers) using these definitions:\n' +
            '    * ICP Fit /5 — how closely they match my ideal customer for the product above.\n' +
            '    * Pain Probability /5 — how likely they actively feel the problem the product solves.\n' +
            '    * Accessibility /5 — how reachable the decision-maker is (public contact, small org, warm route).\n' +
            '    * Learning Value /5 — how much I would learn about the market by talking to them.\n' +
            '- Do NOT compute a total or priority — I calculate those automatically.\n' +
            '- Prefer prospects that are realistically reachable over famous but unreachable ones.\n\n' +
            'Return only the table.';

        var out = document.getElementById('aiPromptOut');
        if (out) out.textContent = p;
    };

    window.copyAiPrompt = function () {
        var out = document.getElementById('aiPromptOut');
        var btn = document.getElementById('aiCopyBtn');
        if (!out) return;
        var text = out.textContent;
        var done = function () {
            if (!btn) return;
            var original = btn.textContent;
            btn.textContent = 'Copied ✓';
            setTimeout(function () { btn.textContent = original; }, 1500);
        };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(done).catch(function () { fallbackCopy(text); done(); });
        } else {
            fallbackCopy(text);
            done();
        }
    };

    function fallbackCopy(text) {
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand('copy'); } catch (e) { /* ignore */ }
        document.body.removeChild(ta);
    }

    window.onImportFileChosen = function () {
        var fileInput = document.getElementById('importFile');
        var label = document.getElementById('importFileName');
        if (label) {
            label.textContent = (fileInput.files && fileInput.files.length > 0)
                ? fileInput.files[0].name
                : 'No file selected.';
        }
    };

    window.previewImport = async function () {
        var fileInput = document.getElementById('importFile');
        if (!fileInput.files || fileInput.files.length === 0) { warn('Please choose an .xlsx file first.'); return; }
        var campaignId = document.getElementById('importCampaignId').value;

        var form = new FormData();
        form.append('file', fileInput.files[0]);
        form.append('campaignId', campaignId);

        BlockUI.show('Reading workbook...');
        try {
            var response = await fetch('/Prospecting/AxPostImportPreview', {
                method: 'POST', headers: { 'RequestVerificationToken': token() }, body: form
            });
            var result = await response.json();
            BlockUI.hide();
            if (!result.success) { err(result.message); return; }
            renderPreview(result.data);
        } catch (e) { BlockUI.hide(); err('Could not read the file. Please try again.'); }
    };

    function renderPreview(data) {
        // Collapse the "Expected format" help now that the user has a real preview to focus on.
        var formatHelp = document.getElementById('formatHelp');
        if (formatHelp) formatHelp.open = false;

        document.getElementById('previewSection').style.display = 'block';
        document.getElementById('sumTotal').textContent = data.totalRows;
        document.getElementById('sumNew').textContent = data.newRows;
        document.getElementById('sumDup').textContent = data.duplicateRows;
        document.getElementById('sumInvalid').textContent = data.invalidRows;

        var fe = document.getElementById('fileErrors');
        var confirmBtn = document.getElementById('confirmImportBtn');
        if (data.fileErrors && data.fileErrors.length > 0) {
            fe.style.display = 'block';
            fe.innerHTML = data.fileErrors.map(esc).join('<br>');
            confirmBtn.disabled = true;
        } else {
            fe.style.display = 'none';
            confirmBtn.disabled = (data.validRows === 0);
        }

        var body = document.getElementById('previewBody');
        var html = '';
        (data.rows || []).forEach(function (r) {
            var state = !r.isValid
                ? '<span class="pill pill-sm pill-red">Invalid</span>'
                : (r.isDuplicate ? '<span class="pill pill-sm pill-blue">Update</span>' : '<span class="pill pill-sm pill-green">New</span>');
            var errNote = (!r.isValid && r.errors && r.errors.length)
                ? '<div style="font-size:11px;color:#C24A4A;">' + r.errors.map(esc).join('; ') + '</div>' : '';
            html += '<tr>' +
                '<td>' + r.rowNumber + '</td>' +
                '<td>' + esc(r.name) + errNote + '</td>' +
                '<td>' + esc(r.segment || '—') + '</td>' +
                '<td style="font-weight:700;">' + r.total + '</td>' +
                '<td><span class="pill pill-sm ' + priorityPill(r.priority) + '">' + esc(r.priority) + '</span></td>' +
                '<td>' + state + '</td>' +
                '</tr>';
        });
        body.innerHTML = html;
    }

    window.resetImport = function () {
        document.getElementById('previewSection').style.display = 'none';
        document.getElementById('importFile').value = '';
        var label = document.getElementById('importFileName');
        if (label) label.textContent = 'No file selected.';
    };

    window.confirmImport = async function () {
        var fileInput = document.getElementById('importFile');
        if (!fileInput.files || fileInput.files.length === 0) { warn('Please choose the file again.'); return; }
        var campaignId = document.getElementById('importCampaignId').value;

        var form = new FormData();
        form.append('file', fileInput.files[0]);
        form.append('campaignId', campaignId);

        BlockUI.show('Importing...');
        try {
            var response = await fetch('/Prospecting/AxPostImportConfirm', {
                method: 'POST', headers: { 'RequestVerificationToken': token() }, body: form
            });
            var result = await response.json();
            BlockUI.hide();
            if (result.success) {
                var d = result.data || {};
                Swal.fire({
                    icon: 'success', title: 'Import complete',
                    text: (d.createdCount || 0) + ' created, ' + (d.updatedCount || 0) + ' updated, ' + (d.skippedInvalidCount || 0) + ' skipped.',
                    confirmButtonColor: '#0D5EA6'
                }).then(function () { window.location.href = '/Prospecting/Prospects?campaignId=' + campaignId; });
            } else { err(result.message); }
        } catch (e) { BlockUI.hide(); err('Import failed. No prospects were changed. Please try again.'); }
    };
})();
