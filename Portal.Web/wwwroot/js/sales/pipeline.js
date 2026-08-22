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
        initKpiToggle();
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
        var assignedFilter = document.getElementById('filterAssigned');

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

        // Populate Assigned To filter with team members
        if (lookups.teamMembers && lookups.teamMembers.length > 0) {
            assignedFilter.innerHTML = '<option value="">All Members</option>';
            lookups.teamMembers.forEach(function (t) {
                assignedFilter.innerHTML += '<option value="' + t.id + '">' + t.displayName + '</option>';
            });
        }
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
        } catch (e) { /* Request aborted — page navigating away */ }
    }

    window.loadPipelineData = async function () {
        var assignedFilter = document.getElementById('filterAssigned').value;
        var productFilter = document.getElementById('filterProduct').value;
        var url = '/Sales/AxGetPipelineData?teamMemberId=' + encodeURIComponent(assignedFilter || '') + '&productId=' + encodeURIComponent(productFilter || '');

        BlockUI.show('Loading pipeline...');
        try {
            var response = await fetch(url);
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                window._lastPipelineStages = result.data;
                renderKanban(result.data);
                renderStagePillNav(result.data);
                renderTable(result.data);
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

    function renderStagePillNav(stages) {
        var existing = document.getElementById('stagePillNav');
        if (existing) existing.remove();

        var nav = document.createElement('div');
        nav.id = 'stagePillNav';
        nav.className = 'stage-pill-nav';

        stages.forEach(function (stage, index) {
            var pill = document.createElement('button');
            pill.className = 'stage-pill';
            pill.textContent = stage.stageName;
            pill.style.cssText = 'background:' + (stage.colour || '#8a9bab') + '18;color:' + (stage.colour || '#8a9bab') + ';border:1.5px solid ' + (stage.colour || '#8a9bab') + '30;';
            pill.setAttribute('aria-label', 'Scroll to ' + stage.stageName + ' stage');
            pill.onclick = function () { scrollToStage(index); };
            nav.appendChild(pill);
        });

        var board = document.getElementById('pipelineBoard');
        board.parentNode.insertBefore(nav, board);
    }

    function scrollToStage(index) {
        var columns = document.querySelectorAll('.pipeline-column');
        if (columns[index]) {
            columns[index].scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'start' });
        }
    }

    function renderKanban(stages) {
        var board = document.getElementById('pipelineBoard');
        // Remove skeleton if present
        var skeleton = document.getElementById('pipelineSkeleton');
        if (skeleton) skeleton.remove();
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

                // Priority badge (shown above contact name when assigned)
                var priorityBadge = '';
                if (lead.priorityName && lead.priorityColour) {
                    priorityBadge = '<div style="margin-bottom:6px;"><span style="display:inline-block;padding:2px 8px;border-radius:10px;font-size:10px;font-weight:700;color:' + lead.priorityColour + ';background:' + lead.priorityColour + '14;border:1px solid ' + lead.priorityColour + '30;">' + lead.priorityName + '</span></div>';
                }

                // Days since last activity indicator
                var daysVal = Math.max(0, lead.daysSinceLastActivity || 0);
                var daysText = daysVal === 0 ? 'Today' : daysVal + 'd ago';
                var daysColour = '#8a9bab';
                if (daysVal > 14) {
                    daysColour = '#C24A4A';
                } else if (daysVal > 7) {
                    daysColour = '#C8912E';
                }

                cards += '<div class="pipeline-card" style="background:#fff;border-radius:14px;padding:16px;margin-bottom:10px;box-shadow:0 2px 8px rgba(13,94,166,.06);border:1px solid rgba(13,94,166,.06);cursor:pointer;transition:box-shadow .15s,transform .15s;" onmouseover="this.style.boxShadow=\'0 6px 16px rgba(13,94,166,.12)\';this.style.transform=\'translateY(-1px)\'" onmouseout="this.style.boxShadow=\'0 2px 8px rgba(13,94,166,.06)\';this.style.transform=\'none\'" onclick="window.open(\'/Sales/LeadDetail/' + lead.id + '\', \'_blank\')">'
                    + priorityBadge
                    + '<div style="font-weight:700;font-size:14px;color:#0B1B28;">' + lead.contactName + '</div>'
                    + (lead.companyName ? '<div style="font-size:12px;color:#5E7385;margin-top:2px;">' + lead.companyName + '</div>' : '')
                    + (lead.productName ? '<div style="margin-top:8px;"><span style="display:inline-block;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:rgba(13,94,166,.08);color:#0D5EA6;">' + lead.productName + '</span></div>' : '')
                    + '<div style="display:flex;align-items:center;gap:8px;margin-top:10px;">'
                    + '<div style="width:22px;height:22px;border-radius:50%;background:' + (stage.colour || '#0D5EA6') + ';color:#fff;display:flex;align-items:center;justify-content:center;font-size:10px;font-weight:700;">' + initials + '</div>'
                    + '<span style="font-size:11px;color:' + daysColour + ';font-weight:' + (daysVal > 7 ? '700' : '400') + ';">' + daysText + '</span>'
                    + '</div>'
                    + '</div>';
            });

            col.innerHTML = header + cards;
            board.appendChild(col);
        });
    }

    function renderTable(stages) {
        var tbody = document.getElementById('leadsTableBody');
        var allLeads = [];

        stages.forEach(function (stage) {
            stage.leads.forEach(function (lead) {
                allLeads.push({
                    id: lead.id,
                    contactName: lead.contactName || '—',
                    companyName: lead.companyName || '—',
                    productName: lead.productName || '—',
                    stageName: stage.stageName,
                    stageColour: stage.colour || '#8a9bab',
                    sourceName: lead.sourceName || '—',
                    createdAtUtc: lead.createdAtUtc
                });
            });
        });

        // Sort by created date descending (newest first)
        allLeads.sort(function (a, b) {
            return new Date(b.createdAtUtc) - new Date(a.createdAtUtc);
        });

        if (allLeads.length === 0) {
            tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:#8a9bab;padding:24px;">No leads found.</td></tr>';
            document.getElementById('paginationInfo').textContent = '';
            document.getElementById('paginationControls').innerHTML = '';
            return;
        }

        // Pagination
        var pageSize = 20;
        var totalPages = Math.ceil(allLeads.length / pageSize);
        if (typeof window._tableCurrentPage === 'undefined') window._tableCurrentPage = 1;
        if (window._tableCurrentPage > totalPages) window._tableCurrentPage = totalPages;
        var start = (window._tableCurrentPage - 1) * pageSize;
        var pageLeads = allLeads.slice(start, start + pageSize);

        var html = '';
        pageLeads.forEach(function (lead) {
            var dateStr = new Date(lead.createdAtUtc).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
            html += '<tr>'
                + '<td style="font-weight:600;">' + lead.contactName + '</td>'
                + '<td>' + lead.companyName + '</td>'
                + '<td>' + lead.productName + '</td>'
                + '<td><span class="pill" style="background:' + lead.stageColour + '18;color:' + lead.stageColour + ';border:1px solid ' + lead.stageColour + '30;">' + lead.stageName + '</span></td>'
                + '<td>' + lead.sourceName + '</td>'
                + '<td>' + dateStr + '</td>'
                + '<td><button class="tbl-action tbl-action--primary" onclick="window.open(\'/Sales/LeadDetail/' + lead.id + '\', \'_blank\')">View</button></td>'
                + '</tr>';
        });

        tbody.innerHTML = html;

        // Pagination info and controls
        document.getElementById('paginationInfo').textContent = 'Showing ' + (start + 1) + '–' + Math.min(start + pageSize, allLeads.length) + ' of ' + allLeads.length;

        var controlsHtml = '';
        if (window._tableCurrentPage > 1) {
            controlsHtml += '<button class="btn btn-secondary" style="padding:6px 12px;font-size:13px;font-weight:700;border-radius:8px;" onclick="goToTablePage(' + (window._tableCurrentPage - 1) + ')">Previous</button> ';
        }
        if (window._tableCurrentPage < totalPages) {
            controlsHtml += '<button class="btn btn-secondary" style="padding:6px 12px;font-size:13px;font-weight:700;border-radius:8px;" onclick="goToTablePage(' + (window._tableCurrentPage + 1) + ')">Next</button>';
        }
        document.getElementById('paginationControls').innerHTML = controlsHtml;
    }

    window.goToTablePage = function (page) {
        window._tableCurrentPage = page;
        renderTable(window._lastPipelineStages || []);
    };

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
        localStorage.setItem('pipeline_view', view);
        document.getElementById('kanbanView').style.display = view === 'kanban' ? 'block' : 'none';
        document.getElementById('tableView').style.display = view === 'table' ? 'block' : 'none';
        document.getElementById('btnKanbanView').classList.toggle('active', view === 'kanban');
        document.getElementById('btnTableView').classList.toggle('active', view === 'table');
    };

    // Restore saved view preference on load
    (function restoreView() {
        var saved = localStorage.getItem('pipeline_view') || 'kanban';
        document.getElementById('kanbanView').style.display = saved === 'kanban' ? 'block' : 'none';
        document.getElementById('tableView').style.display = saved === 'table' ? 'block' : 'none';
        document.getElementById('btnKanbanView').classList.toggle('active', saved === 'kanban');
        document.getElementById('btnTableView').classList.toggle('active', saved === 'table');
    })();

    window.applyFilters = function () { loadPipelineData(); };
    window.clearFilters = function () {
        document.getElementById('filterAssigned').value = '';
        document.getElementById('filterProduct').value = '';
        loadPipelineData();
    };

    // ── Recent Lead Activity ─────────────────────────────────────────────
    async function loadRecentLeadActivity() {
        var container = document.getElementById('recentLeadActivityList');
        try {
            var response = await fetch('/Sales/AxGetRecentLeadActivity');
            var result = await response.json();
            if (result.success && result.data && result.data.length > 0) {
                var html = '';
                result.data.forEach(function (entry) {
                    var ts = new Date(entry.createdAtUtc).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
                    html += '<div style="display:flex;align-items:baseline;gap:10px;padding:6px 0;border-bottom:1px solid rgba(138,155,171,.08);">';
                    html += '<span style="font-size:11px;color:#8a9bab;white-space:nowrap;">' + ts + '</span>';
                    html += '<span>' + escapeHtml(entry.description) + '</span>';
                    if (entry.leadName) {
                        html += '<span style="margin-left:auto;font-size:11px;color:#0D5EA6;white-space:nowrap;">' + escapeHtml(entry.leadName) + '</span>';
                    }
                    html += '</div>';
                });
                container.innerHTML = html;
            } else {
                container.innerHTML = '<div style="color:#8a9bab;">No recent activity.</div>';
            }
        } catch (e) {
            container.innerHTML = '<div style="color:#8a9bab;">Unable to load recent activity.</div>';
        }
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function initKpiToggle() {
        var kpiSection = document.getElementById('kpiFooterSection');
        if (!kpiSection) return;

        kpiSection.addEventListener('click', function () {
            if (window.innerWidth > 768) return;
            kpiSection.classList.toggle('kpi-expanded');
        });
    }

    function initSwipeGesture(cardElement) {
        if (window.innerWidth > 768) return;
        if (!('ontouchstart' in window)) return;

        var startX = 0;
        var currentX = 0;
        var isSwiping = false;
        var threshold = 40;
        var revealWidth = 140;

        cardElement.style.transition = 'transform 0.2s ease';

        cardElement.addEventListener('touchstart', function (e) {
            startX = e.touches[0].clientX;
            currentX = startX;
            isSwiping = true;
            cardElement.style.transition = 'none';
        }, { passive: true });

        cardElement.addEventListener('touchmove', function (e) {
            if (!isSwiping) return;
            currentX = e.touches[0].clientX;
            var deltaX = startX - currentX;

            if (Math.abs(deltaX) > threshold) {
                var translate = Math.min(deltaX, revealWidth);
                if (translate > 0) {
                    cardElement.style.transform = 'translateX(-' + translate + 'px)';
                }
            }
        }, { passive: true });

        cardElement.addEventListener('touchend', function () {
            isSwiping = false;
            cardElement.style.transition = 'transform 0.2s ease';
            var deltaX = startX - currentX;

            if (deltaX > threshold) {
                document.querySelectorAll('.swipe-revealed').forEach(function (other) {
                    if (other !== cardElement) {
                        other.style.transform = 'translateX(0)';
                        other.classList.remove('swipe-revealed');
                    }
                });
                cardElement.style.transform = 'translateX(-' + revealWidth + 'px)';
                cardElement.classList.add('swipe-revealed');
            } else {
                cardElement.style.transform = 'translateX(0)';
                cardElement.classList.remove('swipe-revealed');
            }
        }, { passive: true });
    }

    window.initSwipeGesture = initSwipeGesture;

    // Load recent lead activity on page init
    loadRecentLeadActivity();

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
