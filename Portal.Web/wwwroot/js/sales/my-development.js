/*
 * My Development — private, author-only personal-learning dashboard (Call Reflection module).
 * Loads the current user's own reflection aggregates (self-reported) and the practice focus.
 * Follows the platform AJAX pattern: BlockUI.show -> fetch -> BlockUI.hide -> SweetAlert2.
 */
(function () {
    'use strict';

    var currentPeriod = 'last_30'; // default quick period

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
    function errSwal(text) { return Swal.fire({ icon: 'error', title: 'Error', text: text, confirmButtonColor: '#0D5EA6' }); }
    function okSwal(title, text) { return Swal.fire({ icon: 'success', title: title, text: text, confirmButtonColor: '#0D5EA6' }); }

    function escapeHtml(v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
        });
    }

    function fmtDate(iso) {
        if (!iso) return '';
        var d = new Date(iso);
        if (isNaN(d)) return '';
        return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    }

    var ACTIVITY_PILL = { 2: ['Call', 'q-pill-call'], 4: ['Meeting', 'q-pill-meeting'], 5: ['Demo', 'q-pill-demo'] };

    // Build the request query from explicit dates (if both set) or the current quick period.
    function buildQuery() {
        var from = el('devFrom').value;
        var to = el('devTo').value;
        var params = new URLSearchParams();
        if (from && to) {
            params.set('dateFrom', from);
            params.set('dateTo', to);
        } else {
            params.set('period', currentPeriod);
        }
        return params.toString();
    }

    async function load() {
        BlockUI.show('Loading your development…');
        try {
            var response = await fetch('/Prospecting/AxGetMyDevelopment?' + buildQuery());
            var result = await response.json();
            BlockUI.hide();
            if (!result.success) { errSwal(result.message || 'Could not load your development data.'); return; }
            render(result.data);
        } catch (e) {
            BlockUI.hide();
            errSwal('An unexpected error occurred.');
        }
    }

    function render(d) {
        // Summary stats
        el('devEvalCount').textContent = d.evaluationsCompleted;
        el('devEvalRange').textContent = fmtDate(d.fromUtc) + ' – ' + fmtDate(d.toUtc);

        if (d.overallAverage == null) {
            el('devAvg').innerHTML = '—';
            el('devAvgHint').textContent = 'No criteria assessed yet';
        } else {
            el('devAvg').innerHTML = Number(d.overallAverage).toFixed(1) + ' <small>/ 5</small>';
            el('devAvgHint').textContent = 'Across ' + d.overallAssessedCount + ' assessed criteria';
        }

        el('devReflected').innerHTML = d.reflectedActivities + ' <small>of ' + d.eligibleActivities + '</small>';
        var notReviewed = Math.max(0, d.eligibleActivities - d.reflectedActivities);
        el('devReflectedHint').textContent = notReviewed === 0
            ? 'All eligible calls reviewed'
            : notReviewed + ' eligible call' + (notReviewed === 1 ? '' : 's') + ' not yet reviewed';

        // Practice focus
        setFocus(d.weeklyFocus);

        // Criterion bars
        var bars = el('devCriterionBars');
        if (!d.criterionAverages || d.criterionAverages.length === 0) {
            bars.innerHTML = '<div class="dev-empty">No reflections in this period yet.</div>';
        } else {
            bars.innerHTML = d.criterionAverages.map(function (c) {
                if (c.average == null) {
                    return '' +
                        '<div class="bar-row"><div class="crit">' + escapeHtml(c.label) + '<br><span class="n">n = 0</span></div>' +
                        '<div class="bar-track"></div><span class="na">—</span></div>';
                }
                var avg = Number(c.average);
                var pct = Math.max(2, Math.round(avg / 5 * 100));
                var low = avg < 3 ? ' low' : '';
                return '' +
                    '<div class="bar-row">' +
                        '<div class="crit">' + escapeHtml(c.label) + '<br><span class="n">n = ' + c.sampleSize + '</span></div>' +
                        '<div class="bar-track"><div class="bar-fill' + low + '" style="width:' + pct + '%"></div></div>' +
                        '<strong>' + avg.toFixed(1) + '</strong>' +
                    '</div>';
            }).join('');
        }

        // Recent lessons
        var lessons = el('devLessons');
        if (!d.recentLessons || d.recentLessons.length === 0) {
            lessons.innerHTML = '<div class="dev-empty">No improvement notes yet. Your "what I\'ll do differently" notes will appear here.</div>';
        } else {
            lessons.innerHTML = d.recentLessons.map(function (l) {
                return '<div class="lesson">' + escapeHtml(l.text) +
                    '<div class="meta">From your reflection · ' + fmtDate(l.dateUtc) + '</div></div>';
            }).join('');
        }

        // Review queue
        var queue = el('devReviewQueue');
        if (!d.reviewQueue || d.reviewQueue.length === 0) {
            queue.innerHTML = '<div class="dev-empty">Nothing waiting — every eligible call in this period has a reflection.</div>';
        } else {
            queue.innerHTML = d.reviewQueue.map(function (q) {
                var pill = ACTIVITY_PILL[q.activityType] || ['Call', 'q-pill-call'];
                var sub = fmtDate(q.occurredAtUtc) + (q.outcome ? ' · ' + escapeHtml(q.outcome) : '');
                return '' +
                    '<div class="queue-item">' +
                        '<div>' + escapeHtml(q.prospectName) + '<div class="q-sub">' + sub + '</div></div>' +
                        '<div style="display:flex;gap:8px;align-items:center;">' +
                            '<span class="q-pill ' + pill[1] + '">' + pill[0] + '</span>' +
                            '<a class="btn btn-secondary" style="padding:6px 12px;font-size:12px;" href="/Prospecting/ProspectDetail/' + q.prospectId + '?reflect=' + q.prospectActivityId + '">Evaluate</a>' +
                        '</div>' +
                    '</div>';
            }).join('');
        }
    }

    function setFocus(focus) {
        var box = el('devFocusCurrentBox');
        var cur = el('devFocusCurrent');
        var preview = el('devFocusPreview');
        if (focus && focus.trim()) {
            box.style.display = '';
            cur.textContent = focus;
            preview.textContent = focus;
            el('devFocusInput').value = focus;
        } else {
            box.style.display = 'none';
            preview.textContent = 'Not set';
            el('devFocusInput').value = '';
        }
    }

    window.saveDevFocus = async function () {
        var focus = (el('devFocusInput').value || '').trim();
        BlockUI.show('Saving…');
        try {
            var res = await postJson('/Prospecting/AxPostSavePracticeFocus', { focus: focus });
            BlockUI.hide();
            if (res.success) {
                setFocus(focus);
                okSwal('Saved', focus ? 'Your practice focus has been updated.' : 'Your practice focus has been cleared.');
            } else {
                errSwal(res.message || 'Could not save your focus.');
            }
        } catch (e) {
            BlockUI.hide();
            errSwal('An unexpected error occurred.');
        }
    };

    window.applyDevFilters = function () {
        // Explicit dates take precedence; clear quick-chip highlight when both are set.
        if (el('devFrom').value && el('devTo').value) {
            setActiveChip(null);
        }
        load();
    };

    window.clearDevFilters = function () {
        el('devFrom').value = '';
        el('devTo').value = '';
        currentPeriod = 'last_30';
        setActiveChip('last_30');
        load();
    };

    function setActiveChip(period) {
        document.querySelectorAll('.dev-chip').forEach(function (b) {
            var on = b.getAttribute('data-period') === period;
            b.classList.toggle('btn-primary', on);
            b.classList.toggle('btn-secondary', !on);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.dev-chip').forEach(function (b) {
            b.addEventListener('click', function () {
                currentPeriod = b.getAttribute('data-period');
                el('devFrom').value = '';
                el('devTo').value = '';
                setActiveChip(currentPeriod);
                load();
            });
        });
        setActiveChip('last_30');
        load();
    });
})();
