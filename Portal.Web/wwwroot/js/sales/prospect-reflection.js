/*
 * Call Reflection — private, author-only self-evaluation on a prospect activity.
 * Entirely separate from the commercial activity: saving never changes the prospect's status,
 * score, interaction signal, or conversion. Follows the platform AJAX pattern:
 * BlockUI.show -> fetch -> BlockUI.hide -> SweetAlert2.
 */
(function () {
    'use strict';

    // Six self-assessment criteria: [key, title, reflective question].
    var CRITERIA = [
        ['openingCredibility', 'Opening & credibility', 'Did I introduce myself naturally and establish relevance?'],
        ['listeningDiscovery', 'Listening & discovery', 'Did I understand their situation before presenting?'],
        ['adaptability', 'Adaptability', 'Did I adjust to their communication style and circumstances?'],
        ['clarityRelevance', 'Clarity & relevance', 'Did I explain benefits in terms of their work?'],
        ['conversationControl', 'Conversation control', 'Did I keep the discussion focused without dominating it?'],
        ['nextStepAgreement', 'Next-step agreement', 'Did I seek or confirm a clear, appropriate next action?']
    ];

    // Situational communication tips (static guidance — not prospect profiling).
    var TIPS = {
        busy: ['Busy / direct', 'Short opening, immediate relevance, one question.'],
        analytical: ['Analytical / technical questions', 'Explain the mechanism and show concrete evidence.'],
        experienced: ['Experienced / sceptical', 'Respect established processes; explore gaps without assuming failure.'],
        friendly: ['Friendly / conversational', 'Allow natural rapport while keeping a clear objective.'],
        price: ['Price-focused', 'Answer cost clearly; relate it to value without evasion.'],
        distracted: ['Distracted / driving', 'Avoid lengthy explanations; offer a convenient follow-up.']
    };

    // Current in-memory ratings for the open reflection: { criterionKey: 1..5 }. Absence = not assessed.
    var scores = {};

    function token() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }
    function jsonHeaders() {
        return { 'Content-Type': 'application/json', 'RequestVerificationToken': token() };
    }
    async function postJson(url, payload) {
        var response = await fetch(url, { method: 'POST', headers: jsonHeaders(), body: JSON.stringify(payload) });
        return await response.json();
    }
    function el(id) { return document.getElementById(id); }
    function val(id) { var e = el(id); return e ? e.value : ''; }
    function setVal(id, v) { var e = el(id); if (e) e.value = (v === null || v === undefined) ? '' : v; }
    function strOrNull(id) { var v = (val(id) || '').trim(); return v === '' ? null : v; }
    function errSwal(text) { return Swal.fire({ icon: 'error', title: 'Error', text: text, confirmButtonColor: '#0D5EA6' }); }
    function okSwal(title, text) { return Swal.fire({ icon: 'success', title: title, text: text, confirmButtonColor: '#0D5EA6' }); }

    function escapeHtml(v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
        });
    }

    function openModal() {
        var m = el('reflectionModal');
        if (m) { m.style.display = 'flex'; document.body.style.overflow = 'hidden'; }
    }
    function closeModal() {
        var m = el('reflectionModal');
        if (m) { m.style.display = 'none'; document.body.style.overflow = ''; }
    }

    // Render the six criterion rows from the current `scores`.
    function renderCriteria() {
        var host = el('reflectionCriteria');
        if (!host) return;
        host.innerHTML = CRITERIA.map(function (c) {
            var key = c[0], title = c[1], q = c[2];
            var current = scores[key];
            var buttons = [1, 2, 3, 4, 5].map(function (n) {
                var sel = current === n ? ' sel' : '';
                return '<button type="button" class="reflect-score' + sel + '" data-key="' + key + '" data-value="' + n + '" aria-pressed="' + (current === n) + '">' + n + '</button>';
            }).join('');
            var naSel = !current ? ' sel' : '';
            buttons += '<button type="button" class="reflect-score na' + naSel + '" data-key="' + key + '" data-value="0" aria-pressed="' + (!current) + '">Not assessed</button>';
            var stateLabel = current ? (current + ' / 5') : 'Not assessed';
            return '' +
                '<div class="reflect-crit">' +
                    '<div class="reflect-crit-head">' +
                        '<span class="reflect-crit-title">' + escapeHtml(title) + '</span>' +
                        '<span class="reflect-crit-state" id="reflect-state-' + key + '">' + stateLabel + '</span>' +
                    '</div>' +
                    '<div class="reflect-crit-q">' + escapeHtml(q) + '</div>' +
                    '<div class="reflect-scores" role="group" aria-label="' + escapeHtml(title) + ' rating">' + buttons + '</div>' +
                '</div>';
        }).join('');

        host.querySelectorAll('.reflect-score').forEach(function (b) {
            b.addEventListener('click', function () {
                var key = b.getAttribute('data-key');
                var v = parseInt(b.getAttribute('data-value'), 10);
                if (v >= 1 && v <= 5) { scores[key] = v; } else { delete scores[key]; }
                renderCriteria();
                renderAverage();
            });
        });
    }

    // Average over RATED criteria only. None rated => "Not scored", never 0.
    function renderAverage() {
        var rated = CRITERIA.map(function (c) { return scores[c[0]]; })
            .filter(function (v) { return Number.isInteger(v) && v >= 1 && v <= 5; });
        var avgEl = el('reflectionAverage');
        var countEl = el('reflectionAssessedCount');
        if (rated.length === 0) {
            if (avgEl) avgEl.textContent = '—';
            if (countEl) countEl.textContent = 'Not scored · 0 of 6 assessed';
            return;
        }
        var avg = rated.reduce(function (a, b) { return a + b; }, 0) / rated.length;
        if (avgEl) avgEl.textContent = avg.toFixed(1) + ' / 5';
        if (countEl) countEl.textContent = rated.length + ' of 6 assessed';
    }

    function renderReflectionTip() {
        var sel = el('reflectTipSituation');
        var body = el('reflectTipBody');
        if (!sel || !body) return;
        var t = TIPS[sel.value] || TIPS.busy;
        body.innerHTML = '<strong>' + escapeHtml(t[0]) + '</strong><br>' + escapeHtml(t[1]);
    }

    // Reset all fields to an empty reflection.
    function resetFields() {
        scores = {};
        setVal('reflectObservations', '');
        setVal('reflectStrengths', '');
        setVal('reflectImprovement', '');
        setVal('reflectPreCallObjective', '');
        setVal('reflectPreCallFocus', '');
        renderCriteria();
        renderAverage();
        renderReflectionTip();
        var del = el('reflectionDeleteBtn');
        if (del) del.style.display = 'none';
    }

    // Populate the modal from a loaded DTO (or leave empty for a new reflection).
    function populate(dto) {
        resetFields();
        if (!dto) return;
        CRITERIA.forEach(function (c) {
            var v = dto[c[0]];
            if (Number.isInteger(v) && v >= 1 && v <= 5) scores[c[0]] = v;
        });
        setVal('reflectObservations', dto.observations || '');
        setVal('reflectStrengths', dto.strengths || '');
        setVal('reflectImprovement', dto.improvement || dto.nextImprovement || '');
        setVal('reflectPreCallObjective', dto.preCallObjective || '');
        setVal('reflectPreCallFocus', dto.preCallFocus || '');
        renderCriteria();
        renderAverage();
        var del = el('reflectionDeleteBtn');
        if (del) del.style.display = '';
    }

    // Enable/disable the Save button (used to block saving while the existing reflection loads, so
    // a blank in-memory form can never overwrite real persisted data).
    function setSaveEnabled(enabled) {
        var btn = el('reflectionSaveBtn');
        if (btn) { btn.disabled = !enabled; btn.style.opacity = enabled ? '' : '.55'; btn.style.pointerEvents = enabled ? '' : 'none'; }
    }

    // Toggle which sections show: full reflection vs pure pre-call intention.
    function setMode(mode) {
        setVal('reflectionMode', mode);
        var body = el('reflectionBody');
        if (body) body.style.display = (mode === 'precall') ? 'none' : '';
    }

    /**
     * Open the reflection for a specific activity. Loads any existing reflection for the current
     * user (author-only) and shows its ratings/notes; otherwise opens a blank one.
     */
    window.openReflection = function (activityId, activityType, typeName, dateText, outcome) {
        setVal('reflectionActivityId', activityId);
        setMode('activity');
        el('reflectionTitle').textContent = 'Call Reflection';
        var ctx = (typeName || 'Call');
        if (dateText) ctx += ' · ' + dateText;
        if (outcome) ctx += ' · ' + outcome;
        el('reflectionContext').textContent = ctx;
        resetFields();
        openModal();

        // Block saving until the existing reflection has loaded, so we never persist a blank form
        // over real data if the load is slow or fails.
        setSaveEnabled(false);
        BlockUI.show('Loading reflection…');
        fetch('/Prospecting/AxGetSelfEvaluation?activityId=' + encodeURIComponent(activityId))
            .then(function (r) { return r.json(); })
            .then(function (res) {
                BlockUI.hide();
                if (!res.success) { closeModal(); errSwal(res.message || 'Could not load the reflection.'); return; }
                populate(res.data); // null = no reflection yet (blank form, delete hidden)
                setSaveEnabled(true);
            })
            .catch(function () { BlockUI.hide(); closeModal(); errSwal('An unexpected error occurred.'); });
    };

    /**
     * Open the modal to capture a pre-call intention only (no activity yet). The objective/focus are
     * stored against the most recent reflectable activity when one exists; if none exists, we tell
     * the user to log the call first (we never fabricate a completed activity).
     */
    window.openPreCallIntention = function () {
        var latest = findLatestReflectableActivityId();
        if (!latest) {
            Swal.fire({
                icon: 'info',
                title: 'No call to attach to yet',
                text: 'Record the call, meeting or demo first, then capture your pre-call intention alongside the reflection. We never create a call just to store preparation.',
                confirmButtonColor: '#0D5EA6'
            });
            return;
        }
        setVal('reflectionActivityId', latest.id);
        setMode('precall');
        el('reflectionTitle').textContent = 'Pre-call intention';
        el('reflectionContext').textContent = 'Attaches to your latest ' + latest.label + ' · private';
        resetFields();
        openModal();

        // Load any existing reflection and populate the FULL form (ratings + notes too), so a save
        // from pre-call mode re-sends the real persisted values and never wipes them. If the load
        // fails we block saving rather than risk overwriting good data with blanks.
        setSaveEnabled(false);
        BlockUI.show('Loading…');
        fetch('/Prospecting/AxGetSelfEvaluation?activityId=' + encodeURIComponent(latest.id))
            .then(function (r) { return r.json(); })
            .then(function (res) {
                BlockUI.hide();
                if (!res.success) { closeModal(); errSwal(res.message || 'Could not load your reflection.'); return; }
                populate(res.data); // null = no reflection yet (blank form is correct here)
                setSaveEnabled(true);
            })
            .catch(function () { BlockUI.hide(); closeModal(); errSwal('An unexpected error occurred.'); });
    };

    // Most recent Call(2)/Meeting(4)/Demo(5) activity row on the page.
    function findLatestReflectableActivityId() {
        var rows = document.querySelectorAll('.activity-row');
        for (var i = 0; i < rows.length; i++) {
            var type = parseInt(rows[i].getAttribute('data-type'), 10);
            if (type === 2 || type === 4 || type === 5) {
                var label = type === 2 ? 'call' : (type === 4 ? 'meeting' : 'demo');
                return { id: parseInt(rows[i].getAttribute('data-id'), 10), label: label };
            }
        }
        return null;
    }

    // Reads the reflection context from a button's data-* attributes (avoids injecting user text
    // into an inline onclick string literal).
    window.openReflectionFromButton = function (btn) {
        if (!btn) return;
        window.openReflection(
            parseInt(btn.getAttribute('data-reflect-for'), 10),
            parseInt(btn.getAttribute('data-reflect-type'), 10),
            btn.getAttribute('data-reflect-typename') || 'Call',
            btn.getAttribute('data-reflect-date') || '',
            btn.getAttribute('data-reflect-outcome') || ''
        );
    };

    window.closeReflection = function () { closeModal(); };

    window.renderReflectionTip = renderReflectionTip;

    function ratingOrNull(key) {
        var v = scores[key];
        return (Number.isInteger(v) && v >= 1 && v <= 5) ? v : null;
    }

    window.saveReflection = async function () {
        var activityId = parseInt(val('reflectionActivityId'), 10);
        if (!activityId) { errSwal('No activity selected.'); return; }

        var payload = {
            prospectActivityId: activityId,
            openingCredibility: ratingOrNull('openingCredibility'),
            listeningDiscovery: ratingOrNull('listeningDiscovery'),
            adaptability: ratingOrNull('adaptability'),
            clarityRelevance: ratingOrNull('clarityRelevance'),
            conversationControl: ratingOrNull('conversationControl'),
            nextStepAgreement: ratingOrNull('nextStepAgreement'),
            observations: strOrNull('reflectObservations'),
            strengths: strOrNull('reflectStrengths'),
            nextImprovement: strOrNull('reflectImprovement'),
            preCallObjective: strOrNull('reflectPreCallObjective'),
            preCallFocus: strOrNull('reflectPreCallFocus')
        };

        BlockUI.show('Saving…');
        try {
            var res = await postJson('/Prospecting/AxPostSaveSelfEvaluation', payload);
            BlockUI.hide();
            if (res.success) {
                closeModal();
                okSwal('Saved', 'Your private reflection has been saved.').then(function () { window.location.reload(); });
            } else {
                errSwal(res.message || 'Could not save the reflection.');
            }
        } catch (e) {
            BlockUI.hide();
            errSwal('An unexpected error occurred.');
        }
    };

    window.deleteReflection = function () {
        var activityId = parseInt(val('reflectionActivityId'), 10);
        if (!activityId) return;
        Swal.fire({
            title: 'Delete this reflection?',
            text: 'Your private self-evaluation for this activity will be permanently removed. This cannot be undone.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, delete it',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#C24A4A',
            cancelButtonColor: '#5a6a7a'
        }).then(async function (result) {
            if (!result.isConfirmed) return;
            BlockUI.show('Deleting…');
            try {
                var res = await postJson('/Prospecting/AxPostDeleteSelfEvaluation', { prospectActivityId: activityId });
                BlockUI.hide();
                if (res.success) {
                    closeModal();
                    okSwal('Deleted', 'Your reflection has been removed.').then(function () { window.location.reload(); });
                } else {
                    errSwal(res.message || 'Could not delete the reflection.');
                }
            } catch (e) {
                BlockUI.hide();
                errSwal('An unexpected error occurred.');
            }
        });
    };

    // Close on Escape / backdrop click, matching the other Portal modals.
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            var m = el('reflectionModal');
            if (m && m.style.display === 'flex') closeModal();
        }
    });
    document.addEventListener('DOMContentLoaded', function () {
        var m = el('reflectionModal');
        if (m) {
            m.addEventListener('click', function (e) { if (e.target === m) closeModal(); });
        }
        renderReflectionTip();

        // Deep link from the My Development review queue: /Prospecting/ProspectDetail/{id}?reflect={activityId}
        // auto-opens that activity's reflection via its button (so context + labels are correct).
        try {
            var params = new URLSearchParams(window.location.search);
            var reflectId = params.get('reflect');
            if (reflectId) {
                var btn = document.querySelector('.tl-reflect[data-reflect-for="' + reflectId + '"]');
                if (btn) { btn.click(); }
            }
        } catch (e) { /* no-op */ }
    });
})();
