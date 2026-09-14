// ─── Shared Contact Info Modal ──────────────────────────────
// Included on Sales/Tasks and Sales/Meetings. Opens a read-only
// modal with a contact's details when a contact name is clicked.
// Requires the _ContactInfoModal partial markup on the page,
// plus BlockUI and SweetAlert2 (loaded globally in the layout).
(function () {
    'use strict';

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function setText(id, value) {
        var el = document.getElementById(id);
        if (el) el.textContent = value && String(value).trim() !== '' ? value : '—';
    }

    // Renders a value as a clickable link (mailto:/tel:). Falls back to an em-dash
    // when empty. The visible text is HTML-escaped; the href uses encodeURIComponent
    // for the scheme-specific part.
    function setLink(id, value, scheme) {
        var el = document.getElementById(id);
        if (!el) return;
        var v = value && String(value).trim() !== '' ? String(value).trim() : '';
        if (v === '') {
            el.textContent = '—';
            return;
        }
        var href = scheme + ':' + (scheme === 'tel'
            ? v.replace(/[^\d+]/g, '')   // keep digits and leading +
            : encodeURIComponent(v));
        el.innerHTML = '<a href="' + href + '" style="color:#0D5EA6;text-decoration:none;">' + escapeHtml(v) + '</a>';
    }

    function formatDate(iso) {
        if (!iso) return '—';
        var d = new Date(iso);
        if (isNaN(d.getTime())) return '—';
        var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return String(d.getDate()).padStart(2, '0') + ' ' + months[d.getMonth()] + ' ' + d.getFullYear();
    }

    window.openContactModal = function (id) {
        if (!id) return;

        BlockUI.show('Loading contact...');
        fetch('/Sales/AxGetContactDetail?id=' + encodeURIComponent(id))
            .then(function (response) { return response.json(); })
            .then(function (result) {
                BlockUI.hide();
                if (!result.success || !result.data) {
                    Swal.fire({ icon: 'error', title: 'Error', text: result.message || 'Failed to load contact.', confirmButtonColor: '#0D5EA6' });
                    return;
                }

                var c = result.data;

                setText('contactModalTitle', c.fullName);
                setText('contactModalSubtitle', (c.companyName || 'No company') + ' · ' + (c.isActive ? 'Active' : 'Inactive'));
                setLink('contactModalEmail', c.email, 'mailto');
                setLink('contactModalPhone', c.phoneNumber, 'tel');
                setText('contactModalJobTitle', c.jobTitle);
                setText('contactModalCountry', c.country);
                setText('contactModalCompany', c.companyName);
                setText('contactModalCreated', formatDate(c.createdAtUtc));

                // Notes (hide the block when there are none)
                var notesBlock = document.getElementById('contactModalNotesBlock');
                var notesEl = document.getElementById('contactModalNotes');
                if (c.notes && String(c.notes).trim() !== '') {
                    if (notesEl) notesEl.textContent = c.notes;
                    if (notesBlock) notesBlock.style.display = 'block';
                } else {
                    if (notesBlock) notesBlock.style.display = 'none';
                }

                // Interest history
                var history = c.interestHistory || [];
                var countEl = document.getElementById('contactModalInterestCount');
                if (countEl) countEl.textContent = history.length;

                var body = document.getElementById('contactModalInterestBody');
                var emptyEl = document.getElementById('contactModalInterestEmpty');
                var tableWrap = document.getElementById('contactModalInterestTableWrap');

                if (history.length === 0) {
                    if (tableWrap) tableWrap.style.display = 'none';
                    if (emptyEl) emptyEl.style.display = 'block';
                } else {
                    if (emptyEl) emptyEl.style.display = 'none';
                    if (tableWrap) tableWrap.style.display = 'block';
                    var rows = '';
                    history.forEach(function (interest) {
                        var colour = interest.stageColour || '#8a9bab';
                        var request = interest.requestText || '';
                        if (request.length > 60) request = request.substring(0, 60) + '…';
                        rows += '<tr>';
                        rows += '<td>' + escapeHtml(interest.productName || '—') + '</td>';
                        rows += '<td><span style="display:inline-flex;align-items:center;gap:6px;padding:4px 12px;border-radius:8px;font-size:12px;font-weight:700;background:' + colour + '12;color:' + colour + ';border:1.5px solid ' + colour + '30;"><span style="width:7px;height:7px;border-radius:50%;background:' + colour + ';display:inline-block;"></span>' + escapeHtml(interest.stageName) + '</span></td>';
                        rows += '<td>' + (request ? escapeHtml(request) : '—') + '</td>';
                        rows += '<td>' + formatDate(interest.createdAtUtc) + '</td>';
                        rows += '<td><a href="/Sales/LeadDetail/' + interest.leadRequestId + '" class="btn btn-secondary" style="padding:4px 10px;font-size:12px;">View</a></td>';
                        rows += '</tr>';
                    });
                    if (body) body.innerHTML = rows;
                }

                var modal = document.getElementById('contactInfoModal');
                if (modal) modal.style.display = 'flex';
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
            });
    };

    window.closeContactModal = function () {
        var modal = document.getElementById('contactInfoModal');
        if (modal) modal.style.display = 'none';
    };
})();
