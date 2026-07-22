/**
 * Sales Pipeline — Kanban board and table view interactions
 */
(function () {
    'use strict';

    var lookups = null;

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    // Load pipeline data on page load
    document.addEventListener('DOMContentLoaded', async function () {
        await loadLookups();
        await loadPipelineData();
    });

    async function loadLookups() {
        try {
            var response = await fetch('/Sales/AxGetLookups');
            var result = await response.json();
            if (result.success) {
                lookups = result.data;
                populateCreateLeadForm();
            }
        } catch (e) { console.error('Failed to load lookups', e); }
    }

    function populateCreateLeadForm() {
        if (!lookups) return;

        var contactSelect = document.getElementById('leadContactId');
        var productSelect = document.getElementById('leadProductId');
        var sourceSelect = document.getElementById('leadSourceTypeId');
        var sourceRefSelect = document.getElementById('leadSourceRefTypeId');

        // Load contacts async
        loadContactsForSelect(contactSelect);

        productSelect.innerHTML = '<option value="">None</option>';
        lookups.products.forEach(function (p) {
            productSelect.innerHTML += '<option value="' + p.id + '">' + p.name + '</option>';
        });

        sourceSelect.innerHTML = '';
        lookups.sources.forEach(function (s) {
            sourceSelect.innerHTML += '<option value="' + s.id + '">' + s.name + '</option>';
        });

        sourceRefSelect.innerHTML = '<option value="">None</option>';
        lookups.sourceReferences.forEach(function (s) {
            sourceRefSelect.innerHTML += '<option value="' + s.id + '">' + s.name + '</option>';
        });
    }

    async function loadContactsForSelect(selectEl) {
        try {
            var response = await fetch('/Sales/AxGetContactsSearch?page=1');
            var result = await response.json();
            if (result.success) {
                selectEl.innerHTML = '<option value="">Select contact...</option>';
                result.data.forEach(function (c) {
                    selectEl.innerHTML += '<option value="' + c.id + '">' + c.fullName + (c.companyName ? ' (' + c.companyName + ')' : '') + '</option>';
                });
            }
        } catch (e) { console.error('Failed to load contacts', e); }
    }

    window.loadPipelineData = async function () {
        var assignedFilter = document.getElementById('filterAssigned').value;
        var productFilter = document.getElementById('filterProduct').value;
        var url = '/Sales/AxGetPipelineData?assignedToUserId=' + encodeURIComponent(assignedFilter || '') + '&productId=' + encodeURIComponent(productFilter || '');

        BlockUI.show('Loading pipeline...');
        try {
            var response = await fetch(url);
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                renderKanban(result.data);
                updatePipelineKpis(result.data);
                renderCancelledLeads(result.cancelledLeads || []);
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to load pipeline data.', confirmButtonColor: '#0D5EA6' });
        }
    };

    function renderKanban(stages) {
        var board = document.getElementById('pipelineBoard');
        board.innerHTML = '';

        stages.forEach(function (stage) {
            var col = document.createElement('div');
            col.className = 'pipeline-column';
            col.style.cssText = 'min-width:220px;flex:1;max-width:280px;background:' + (stage.colour || '#8a9bab') + '08;border-radius:16px;padding:16px;border:1px solid ' + (stage.colour || '#8a9bab') + '15;min-height:200px;';

            var header = '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;">'
                + '<div style="display:flex;align-items:center;gap:8px;">'
                + '<span style="width:8px;height:8px;border-radius:50%;background:' + (stage.colour || '#8a9bab') + ';display:inline-block;"></span>'
                + '<span style="font-weight:700;font-size:14px;color:#0B1B28;">' + stage.stageName + '</span>'
                + '</div>'
                + '<span style="background:rgba(13,94,166,.06);color:#5E7385;padding:3px 10px;border-radius:10px;font-size:12px;font-weight:700;">' + stage.count + '</span>'
                + '</div>';

            var cards = '';
            stage.leads.forEach(function (lead) {
                var initials = lead.contactName ? lead.contactName.charAt(0).toUpperCase() : '?';
                cards += '<div class="pipeline-card" style="background:#fff;border-radius:14px;padding:16px;margin-bottom:10px;box-shadow:0 2px 8px rgba(13,94,166,.06);border:1px solid rgba(13,94,166,.06);cursor:pointer;transition:box-shadow .15s,transform .15s;" onmouseover="this.style.boxShadow=\'0 6px 16px rgba(13,94,166,.12)\';this.style.transform=\'translateY(-1px)\'" onmouseout="this.style.boxShadow=\'0 2px 8px rgba(13,94,166,.06)\';this.style.transform=\'none\'" onclick="window.open(\'/Sales/LeadDetail/' + lead.id + '\', \'_blank\')">'
                    + '<div style="font-weight:700;font-size:14px;color:#0B1B28;">' + lead.contactName + '</div>'
                    + (lead.companyName ? '<div style="font-size:12px;color:#5E7385;margin-top:2px;">' + lead.companyName + '</div>' : '')
                    + (lead.productName ? '<div style="margin-top:8px;"><span style="display:inline-block;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(13,94,166,.08);color:#0D5EA6;">' + lead.productName + '</span></div>' : '')
                    + '<div style="display:flex;align-items:center;gap:8px;margin-top:10px;">'
                    + '<div style="width:22px;height:22px;border-radius:50%;background:' + (stage.colour || '#0D5EA6') + ';color:#fff;display:flex;align-items:center;justify-content:center;font-size:10px;font-weight:700;">' + initials + '</div>'
                    + '<span style="font-size:11px;color:#8a9bab;">' + timeAgo(lead.createdAtUtc) + '</span>'
                    + '</div>'
                    + '</div>';
            });

            col.innerHTML = header + cards;
            board.appendChild(col);
        });
    }

    function timeAgo(dateStr) {
        var now = new Date();
        var date = new Date(dateStr);
        var diff = Math.floor((now - date) / 1000);
        if (diff < 60) return 'just now';
        if (diff < 3600) return Math.floor(diff / 60) + 'm ago';
        if (diff < 86400) return Math.floor(diff / 3600) + 'h ago';
        return Math.floor(diff / 86400) + 'd ago';
    }

    function renderCancelledLeads(leads) {
        var section = document.getElementById('cancelledLeadsSection');
        var list = document.getElementById('cancelledLeadsList');
        var count = document.getElementById('cancelledCount');

        if (!leads || leads.length === 0) {
            section.style.display = 'none';
            return;
        }

        section.style.display = 'block';
        count.textContent = leads.length;

        var html = '';
        leads.forEach(function(lead) {
            var initials = lead.contactName ? lead.contactName.charAt(0).toUpperCase() : '?';
            html += '<div style="background:#fff;border-radius:14px;padding:14px 16px;box-shadow:0 2px 8px rgba(13,94,166,.04);border:1.5px solid rgba(194,74,74,.12);min-width:200px;max-width:260px;cursor:pointer;opacity:.8;transition:opacity .15s;" onmouseover="this.style.opacity=\'1\'" onmouseout="this.style.opacity=\'.8\'" onclick="window.open(\'/Sales/LeadDetail/' + lead.id + '\', \'_blank\')">'
                + '<div style="font-weight:700;font-size:13px;color:#0B1B28;">' + lead.contactName + '</div>'
                + (lead.companyName ? '<div style="font-size:12px;color:#5E7385;margin-top:2px;">' + lead.companyName + '</div>' : '')
                + '<div style="display:flex;align-items:center;gap:6px;margin-top:8px;">'
                + '<div style="width:20px;height:20px;border-radius:50%;background:#C24A4A;color:#fff;display:flex;align-items:center;justify-content:center;font-size:9px;font-weight:700;">' + initials + '</div>'
                + '<span style="font-size:11px;color:#8a9bab;">' + timeAgo(lead.createdAtUtc) + '</span>'
                + '</div>'
                + '</div>';
        });

        list.innerHTML = html;
    }

    function updatePipelineKpis(stages) {
        var totalActive = 0;
        var thisMonth = 0;
        var wonCount = 0;
        var totalClosed = 0;
        var totalDaysToWon = 0;
        var wonWithDays = 0;
        var now = new Date();
        var monthStart = new Date(now.getFullYear(), now.getMonth(), 1);

        stages.forEach(function(stage) {
            stage.leads.forEach(function(lead) {
                var createdDate = new Date(lead.createdAtUtc);

                if (stage.stageName !== 'Won' && stage.stageName !== 'Lost' && stage.stageName !== 'Inactive') {
                    totalActive++;
                }

                if (createdDate >= monthStart) {
                    thisMonth++;
                }

                if (stage.stageName === 'Won') {
                    wonCount++;
                    var days = Math.floor((now - createdDate) / (1000 * 60 * 60 * 24));
                    totalDaysToWon += days;
                    wonWithDays++;
                }

                if (stage.stageName === 'Won' || stage.stageName === 'Lost') {
                    totalClosed++;
                }
            });
        });

        var conversionRate = totalClosed > 0 ? Math.round((wonCount / totalClosed) * 100) : 0;
        var avgDays = wonWithDays > 0 ? Math.round(totalDaysToWon / wonWithDays) : null;

        document.getElementById('kpiTotalLeads').textContent = totalActive;
        document.getElementById('kpiThisMonth').textContent = thisMonth;
        document.getElementById('kpiConversionRate').textContent = conversionRate + '%';
        document.getElementById('kpiAvgDaysToWon').textContent = avgDays !== null ? avgDays : '\u2014';
    }

    window.switchView = function (view) {
        document.getElementById('kanbanView').style.display = view === 'kanban' ? 'block' : 'none';
        document.getElementById('tableView').style.display = view === 'table' ? 'block' : 'none';
        document.getElementById('btnKanbanView').classList.toggle('active', view === 'kanban');
        document.getElementById('btnTableView').classList.toggle('active', view === 'table');
    };

    window.applyFilters = function () { loadPipelineData(); };
    window.clearFilters = function () {
        document.getElementById('filterAssigned').value = '';
        document.getElementById('filterProduct').value = '';
        loadPipelineData();
    };

    // ── Global Activity Feed ──────────────────────────────────────────────
    var globalFeedPage = 1;

    async function loadGlobalFeed(page) {
        var container = document.getElementById('globalActivityFeed');
        try {
            var response = await fetch('/Sales/AxGetGlobalActivityFeed?page=' + page);
            var result = await response.json();

            if (!result.success || !result.data || result.data.length === 0) {
                if (page === 1) container.innerHTML = '<p style="font-size:13px;color:#8a9bab;text-align:center;padding:16px 0;">No activity recorded yet.</p>';
                document.getElementById('globalFeedPaging').style.display = 'none';
                return;
            }

            var html = '';
            result.data.forEach(function(entry) {
                var dotColor = getGlobalFeedColor(entry.action);
                var icon = getGlobalFeedIcon(entry.action);
                var label = getGlobalFeedLabel(entry.action);
                var timeStr = new Date(entry.createdAtUtc).toLocaleString('en-GB', { day:'2-digit', month:'short', year:'numeric', hour:'2-digit', minute:'2-digit' });

                html += '<div style="display:flex;gap:12px;padding:10px 0;border-bottom:1px solid rgba(13,94,166,.05);">';
                html += '<div style="flex-shrink:0;width:26px;height:26px;border-radius:8px;background:' + dotColor + '18;color:' + dotColor + ';display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;">' + icon + '</div>';
                html += '<div style="flex:1;min-width:0;">';
                html += '<div style="display:flex;justify-content:space-between;align-items:center;">';
                html += '<span style="font-size:12px;font-weight:700;color:#0B1B28;">' + label + '</span>';
                html += '<span style="font-size:10px;color:#8a9bab;">' + timeStr + '</span>';
                html += '</div>';
                html += '<div style="font-size:12px;color:#5E7385;margin-top:2px;">' + entry.description + '</div>';
                if (entry.performedByName) html += '<div style="font-size:10px;color:#8a9bab;margin-top:2px;">by ' + entry.performedByName + '</div>';
                html += '</div></div>';
            });

            container.innerHTML = html;

            // Paging
            var paging = document.getElementById('globalFeedPaging');
            paging.style.display = 'flex';
            paging.innerHTML = '';
            if (page > 1) {
                var prevBtn = document.createElement('button');
                prevBtn.className = 'btn btn-secondary';
                prevBtn.style.cssText = 'padding:6px 14px;font-size:12px;';
                prevBtn.textContent = 'Previous';
                prevBtn.onclick = function() { globalFeedPage--; loadGlobalFeed(globalFeedPage); };
                paging.appendChild(prevBtn);
            }
            if (result.data.length >= 15) {
                var nextBtn = document.createElement('button');
                nextBtn.className = 'btn btn-secondary';
                nextBtn.style.cssText = 'padding:6px 14px;font-size:12px;';
                nextBtn.textContent = 'Next';
                nextBtn.onclick = function() { globalFeedPage++; loadGlobalFeed(globalFeedPage); };
                paging.appendChild(nextBtn);
            }
        } catch (e) {
            if (page === 1) container.innerHTML = '<p style="font-size:13px;color:#8a9bab;">Failed to load activity.</p>';
        }
    }

    function getGlobalFeedColor(a) { return { lead_created:'#8a9bab', stage_changed:'#0D5EA6', lead_cancelled:'#C24A4A', lead_reactivated:'#129867', response_logged:'#129867', meeting_scheduled:'#C8912E', meeting_cancelled:'#C24A4A', proposal_linked:'#57B8E8', invoice_linked:'#57B8E8', marked_as_won:'#129867', assigned:'#0D5EA6', unassigned:'#8a9bab', request_details_updated:'#8a9bab' }[a] || '#8a9bab'; }
    function getGlobalFeedIcon(a) { return { lead_created:'+', stage_changed:'→', lead_cancelled:'✕', lead_reactivated:'↺', response_logged:'💬', meeting_scheduled:'📅', meeting_cancelled:'📅', proposal_linked:'📄', invoice_linked:'📄', marked_as_won:'✓', assigned:'👤', unassigned:'👤', request_details_updated:'✏' }[a] || '•'; }
    function getGlobalFeedLabel(a) { return { lead_created:'Lead Created', stage_changed:'Stage Changed', lead_cancelled:'Lead Cancelled', lead_reactivated:'Lead Reactivated', response_logged:'Response Logged', meeting_scheduled:'Meeting Scheduled', meeting_cancelled:'Meeting Cancelled', proposal_linked:'Proposal Linked', invoice_linked:'Invoice Linked', marked_as_won:'Marked as Won', assigned:'Assigned', unassigned:'Unassigned', request_details_updated:'Details Updated' }[a] || a; }

    // Load global feed on page init
    loadGlobalFeed(1);

    window.openCreateLeadModal = function () {
        document.getElementById('createLeadModal').style.display = 'flex';
    };
    window.closeCreateLeadModal = function () {
        document.getElementById('createLeadModal').style.display = 'none';
    };

    window.submitCreateLead = async function () {
        var payload = {
            contactId: parseInt(document.getElementById('leadContactId').value),
            productId: document.getElementById('leadProductId').value ? parseInt(document.getElementById('leadProductId').value) : null,
            leadSourceTypeId: parseInt(document.getElementById('leadSourceTypeId').value),
            leadSourceReferenceTypeId: document.getElementById('leadSourceRefTypeId').value ? parseInt(document.getElementById('leadSourceRefTypeId').value) : null,
            sourceUrl: document.getElementById('leadSourceUrl').value || null,
            requestText: document.getElementById('leadRequestText').value || null
        };

        if (!payload.contactId || !payload.leadSourceTypeId) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Contact and Source are required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        BlockUI.show('Creating lead...');
        try {
            var response = await fetch('/Sales/AxPostCreateLeadRequest', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify(payload)
            });
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                closeCreateLeadModal();
                Swal.fire({ icon: 'success', title: 'Created', text: 'Lead created successfully.', confirmButtonColor: '#0D5EA6' }).then(function () { loadPipelineData(); });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };
})();
