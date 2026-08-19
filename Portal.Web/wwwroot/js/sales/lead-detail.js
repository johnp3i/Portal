/**
 * Sales Lead Detail — stage changes, respond, schedule meeting, mark as won
 */
(function () {
    'use strict';

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    window.changeStage = async function (id, statusId) {
        BlockUI.show('Updating stage...');
        try {
            var response = await fetch('/Sales/AxPostChangeLeadStage?id=' + id + '&leadStatusTypeId=' + statusId, {
                method: 'POST',
                headers: { 'RequestVerificationToken': getAntiForgeryToken() }
            });
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                window.location.reload();
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.markAsWon = function (id) {
        Swal.fire({
            title: 'Mark as Won?',
            text: 'This will move the lead to Won and convert the contact to a customer.',
            icon: 'info',
            showCancelButton: true,
            confirmButtonText: 'Yes, mark as won',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#129867'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Processing...');
                try {
                    var response = await fetch('/Sales/AxPostMarkAsWon?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Won!', text: 'Lead marked as won and contact converted to customer.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    window.editRequestDetails = function (id) {
        // Redirect to the full edit function
        editLeadInfo();
    };

    window.editLeadInfo = function () {
        var grid = document.getElementById('leadInfoGrid');
        var leadId = parseInt(grid.dataset.leadId);
        var currentProductId = grid.dataset.productId || '';
        var currentSourceTypeId = grid.dataset.sourceTypeId || '';
        var currentSourceRefTypeId = grid.dataset.sourceRefTypeId || '';
        var currentSourceUrl = grid.dataset.sourceUrl || '';
        var currentRequestText = grid.dataset.requestText || '';

        // Build product options
        var productOptions = '<option value="">None</option>';
        if (window._leadLookups && _leadLookups.products) {
            _leadLookups.products.forEach(function (p) {
                productOptions += '<option value="' + p.id + '"' + (p.id == currentProductId ? ' selected' : '') + '>' + p.name + '</option>';
            });
        }

        // Build source type options
        var sourceOptions = '';
        if (window._leadLookups && _leadLookups.sourceTypes) {
            _leadLookups.sourceTypes.forEach(function (s) {
                sourceOptions += '<option value="' + s.id + '"' + (s.id == currentSourceTypeId ? ' selected' : '') + '>' + s.name + '</option>';
            });
        }

        // Build source reference options
        var sourceRefOptions = '<option value="">None</option>';
        if (window._leadLookups && _leadLookups.sourceRefTypes) {
            _leadLookups.sourceRefTypes.forEach(function (s) {
                sourceRefOptions += '<option value="' + s.id + '"' + (s.id == currentSourceRefTypeId ? ' selected' : '') + '>' + s.name + '</option>';
            });
        }

        var modalHtml = '<div id="editLeadInfoModal" style="position:fixed;inset:0;background:rgba(11,27,40,.4);display:flex;align-items:center;justify-content:center;z-index:1000;backdrop-filter:blur(2px);" onclick="if(event.target===this)closeEditLeadInfoModal()">'
            + '<div style="background:#fff;border-radius:24px;padding:32px;width:520px;max-width:95vw;box-shadow:0 20px 60px rgba(0,0,0,.15);max-height:90vh;overflow-y:auto;">'
            + '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:20px;">'
            + '<h3 style="font-family:Manrope,sans-serif;font-size:18px;font-weight:700;margin:0;">Edit Lead Information</h3>'
            + '<button onclick="closeEditLeadInfoModal()" style="background:none;border:none;cursor:pointer;color:#8a9bab;font-size:22px;">&times;</button>'
            + '</div>'
            + '<div class="field" style="margin-bottom:18px;"><label>Product</label><select id="editLeadProduct">' + productOptions + '</select></div>'
            + '<div class="field" style="margin-bottom:18px;"><label>Source</label><select id="editLeadSourceType">' + sourceOptions + '</select></div>'
            + '<div class="field" style="margin-bottom:18px;"><label>Source Reference</label><select id="editLeadSourceRef">' + sourceRefOptions + '</select></div>'
            + '<div class="field" style="margin-bottom:18px;"><label>Source URL</label><input type="text" id="editLeadSourceUrl" value="' + currentSourceUrl.replace(/"/g, '&quot;') + '" placeholder="https://..." /></div>'
            + '<div class="field" style="margin-bottom:18px;"><label>Request Details</label><textarea id="editLeadRequestText" rows="4" style="resize:vertical;">' + currentRequestText.replace(/</g, '&lt;').replace(/>/g, '&gt;') + '</textarea></div>'
            + '<div style="display:flex;justify-content:flex-end;gap:10px;margin-top:20px;">'
            + '<button class="btn btn-secondary" onclick="closeEditLeadInfoModal()">Cancel</button>'
            + '<button class="btn btn-primary" onclick="submitEditLeadInfo(' + leadId + ')">Save Changes</button>'
            + '</div>'
            + '</div></div>';

        document.body.insertAdjacentHTML('beforeend', modalHtml);
    };

    window.closeEditLeadInfoModal = function () {
        var modal = document.getElementById('editLeadInfoModal');
        if (modal) modal.remove();
    };

    window.submitEditLeadInfo = async function (leadId) {
        var payload = {
            id: leadId,
            productId: document.getElementById('editLeadProduct').value ? parseInt(document.getElementById('editLeadProduct').value) : null,
            leadSourceTypeId: parseInt(document.getElementById('editLeadSourceType').value),
            leadSourceReferenceTypeId: document.getElementById('editLeadSourceRef').value ? parseInt(document.getElementById('editLeadSourceRef').value) : null,
            sourceUrl: document.getElementById('editLeadSourceUrl').value.trim() || null,
            requestText: document.getElementById('editLeadRequestText').value.trim() || null
        };

        if (!payload.leadSourceTypeId) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Source is required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        BlockUI.show('Saving...');
        try {
            var response = await fetch('/Sales/AxPostUpdateRequestDetails', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify(payload)
            });
            var data = await response.json();
            BlockUI.hide();

            if (data.success) {
                closeEditLeadInfoModal();
                Swal.fire({ icon: 'success', title: 'Saved', text: 'Lead information updated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.reactivateLead = function (id) {
        Swal.fire({
            title: 'Reactivate Lead?',
            text: 'This will reactivate the lead and return it to the "New" stage.',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, reactivate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Reactivating...');
                try {
                    var response = await fetch('/Sales/AxPostReactivateLead?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Reactivated', text: 'Lead has been reactivated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    window.cancelLead = function (id) {
        Swal.fire({
            title: 'Cancel Lead?',
            text: 'Please provide a reason for cancellation:',
            icon: 'warning',
            input: 'text',
            inputPlaceholder: 'Reason (optional)',
            showCancelButton: true,
            confirmButtonText: 'Cancel Lead',
            cancelButtonText: 'Go Back',
            confirmButtonColor: '#C24A4A'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Cancelling...');
                try {
                    var response = await fetch('/Sales/AxPostCancelLead?id=' + id + '&description=' + encodeURIComponent(result.value || ''), {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Cancelled', text: 'Lead has been cancelled.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    window.openRespondModal = async function (leadRequestId) {
        // Show the compose response modal and load templates
        document.getElementById('composeLeadRequestId').value = leadRequestId;
        document.getElementById('composeTemplateSelect').innerHTML = '<option value="">Loading...</option>';
        document.getElementById('composeSubject').value = '';
        document.getElementById('composeBodyPreview').innerHTML = '<p style="color:#8a9bab;font-size:13px;">Select a template to see the rendered preview.</p>';
        document.getElementById('composeResponseTypeId').value = '';
        document.getElementById('composeTemplateName').textContent = '';
        document.getElementById('composeModal').style.display = 'flex';

        // Load available templates
        try {
            var response = await fetch('/Sales/AxGetTemplatesForLead');
            var result = await response.json();

            var select = document.getElementById('composeTemplateSelect');
            select.innerHTML = '<option value="">— Select a template —</option>';

            if (result.success && result.data && result.data.length > 0) {
                result.data.forEach(function (t) {
                    var opt = document.createElement('option');
                    opt.value = t.id;
                    opt.textContent = t.name + (t.productName ? ' (' + t.productName + ')' : ' (All Products)');
                    select.appendChild(opt);
                });
            } else {
                select.innerHTML = '<option value="">No templates available</option>';
            }
        } catch (e) {
            document.getElementById('composeTemplateSelect').innerHTML = '<option value="">Failed to load templates</option>';
        }
    };

    window.closeComposeModal = function () {
        document.getElementById('composeModal').style.display = 'none';
    };

    window.onTemplateSelected = async function () {
        var templateId = document.getElementById('composeTemplateSelect').value;
        var leadRequestId = document.getElementById('composeLeadRequestId').value;

        if (!templateId) {
            document.getElementById('composeSubject').value = '';
            document.getElementById('composeBodyPreview').innerHTML = '<p style="color:#8a9bab;font-size:13px;">Select a template to see the rendered preview.</p>';
            document.getElementById('composeResponseTypeId').value = '';
            document.getElementById('composeTemplateName').textContent = '';
            return;
        }

        BlockUI.show('Rendering template...');
        try {
            var response = await fetch('/Sales/AxGetRenderTemplate?templateId=' + templateId + '&leadRequestId=' + leadRequestId);
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                var data = result.data;
                document.getElementById('composeSubject').value = data.subject || '';
                document.getElementById('composeBodyPreview').innerHTML = data.renderedBody || '<p style="color:#8a9bab;">Empty template body.</p>';
                document.getElementById('composeResponseTypeId').value = data.leadResponseTypeId || '';
                document.getElementById('composeTemplateName').textContent = data.templateName || '';
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to render template.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.copyComposeSubject = function () {
        var subject = document.getElementById('composeSubject').value;
        if (!subject) {
            Swal.fire({ icon: 'info', title: 'Nothing to copy', text: 'Subject is empty.', confirmButtonColor: '#0D5EA6' });
            return;
        }
        navigator.clipboard.writeText(subject).then(function () {
            Swal.fire({ icon: 'success', title: 'Copied', text: 'Subject copied to clipboard.', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false });
        });
    };

    window.copyComposeBody = function () {
        var bodyEl = document.getElementById('composeBodyPreview');
        var html = bodyEl.innerHTML;
        var text = bodyEl.innerText || bodyEl.textContent;
        if (!text || text.trim() === '' || text.trim() === 'Select a template to see the rendered preview.') {
            Swal.fire({ icon: 'info', title: 'Nothing to copy', text: 'Body is empty.', confirmButtonColor: '#0D5EA6' });
            return;
        }
        // Copy as formatted HTML so it pastes correctly into email clients
        var blob = new Blob([html], { type: 'text/html' });
        var textBlob = new Blob([text], { type: 'text/plain' });
        var item = new ClipboardItem({ 'text/html': blob, 'text/plain': textBlob });
        navigator.clipboard.write([item]).then(function () {
            Swal.fire({ icon: 'success', title: 'Copied', text: 'Email body copied with formatting.', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false });
        }).catch(function() {
            // Fallback to plain text if HTML clipboard not supported
            navigator.clipboard.writeText(text).then(function () {
                Swal.fire({ icon: 'success', title: 'Copied', text: 'Body copied as plain text.', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false });
            });
        });
    };

    window.copyComposeAll = function () {
        var subject = document.getElementById('composeSubject').value || '';
        var bodyEl = document.getElementById('composeBodyPreview');
        var html = bodyEl.innerHTML;
        var text = bodyEl.innerText || bodyEl.textContent;
        if (!subject && (!text || text.trim() === '' || text.trim() === 'Select a template to see the rendered preview.')) {
            Swal.fire({ icon: 'info', title: 'Nothing to copy', text: 'No content to copy.', confirmButtonColor: '#0D5EA6' });
            return;
        }
        // Build full HTML with subject as a heading
        var fullHtml = '';
        if (subject) fullHtml += '<p><strong>Subject:</strong> ' + subject + '</p><br>';
        fullHtml += html;

        var fullText = '';
        if (subject) fullText += 'Subject: ' + subject + '\n\n';
        fullText += text;

        var blob = new Blob([fullHtml], { type: 'text/html' });
        var textBlob = new Blob([fullText], { type: 'text/plain' });
        var item = new ClipboardItem({ 'text/html': blob, 'text/plain': textBlob });
        navigator.clipboard.write([item]).then(function () {
            Swal.fire({ icon: 'success', title: 'Copied', text: 'Subject and body copied with formatting.', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false });
        }).catch(function() {
            navigator.clipboard.writeText(fullText).then(function () {
                Swal.fire({ icon: 'success', title: 'Copied', text: 'Content copied as plain text.', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false });
            });
        });
    };

    window.logComposeResponse = async function () {
        var leadRequestId = parseInt(document.getElementById('composeLeadRequestId').value);
        var templateId = document.getElementById('composeTemplateSelect').value;
        var responseTypeId = parseInt(document.getElementById('composeResponseTypeId').value);
        var bodyEl = document.getElementById('composeBodyPreview');
        var responseText = bodyEl.innerText || bodyEl.textContent;

        if (!templateId) {
            Swal.fire({ icon: 'warning', title: 'No template selected', text: 'Please select a template before logging a response.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        if (!responseTypeId) responseTypeId = 1; // default Email

        var payload = {
            leadRequestId: leadRequestId,
            leadResponseTypeId: responseTypeId,
            leadResponseTemplateId: parseInt(templateId),
            responseText: responseText
        };

        BlockUI.show('Logging response...');
        try {
            var response = await fetch('/Sales/AxPostSendResponse', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify(payload)
            });
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                closeComposeModal();
                Swal.fire({ icon: 'success', title: 'Logged', text: 'Response recorded successfully.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to log response.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.openScheduleMeetingModal = function (leadRequestId, contactId) {
        // Redirect to meetings page or open inline modal
        window.location.href = '/Sales/Meetings?leadRequestId=' + leadRequestId + '&contactId=' + contactId;
    };

    window.showActivityInfo = function () {
        Swal.fire({
            title: 'Activity Feed',
            icon: 'info',
            confirmButtonColor: '#0D5EA6',
            html: '<div style="text-align:left;font-size:13px;line-height:1.8;color:#0B1B28;">'
                + '<p style="margin-bottom:12px;color:#5E7385;">The activity feed automatically logs the following actions performed on this lead:</p>'
                + '<table style="width:100%;border-collapse:collapse;font-size:12px;">'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Lead Created</td><td style="padding:6px 0;color:#5E7385;">When the lead was added</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Stage Changed</td><td style="padding:6px 0;color:#5E7385;">Lead moves to a different stage</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Response Logged</td><td style="padding:6px 0;color:#5E7385;">A response is composed and logged</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Meeting Scheduled</td><td style="padding:6px 0;color:#5E7385;">A meeting is scheduled from this lead</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Meeting Cancelled</td><td style="padding:6px 0;color:#5E7385;">A linked meeting is cancelled</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Proposal Linked</td><td style="padding:6px 0;color:#5E7385;">A quotation is linked to this lead</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Invoice Linked</td><td style="padding:6px 0;color:#5E7385;">An invoice is linked to this lead</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Assigned / Unassigned</td><td style="padding:6px 0;color:#5E7385;">A team member is assigned or removed</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Marked as Won</td><td style="padding:6px 0;color:#5E7385;">Lead won, contact converted to customer</td></tr>'
                + '<tr style="border-bottom:1px solid rgba(13,94,166,.08);"><td style="padding:6px 0;font-weight:700;">Lead Cancelled</td><td style="padding:6px 0;color:#5E7385;">Lead cancelled with optional reason</td></tr>'
                + '<tr><td style="padding:6px 0;font-weight:700;">Lead Reactivated</td><td style="padding:6px 0;color:#5E7385;">A cancelled lead is reactivated</td></tr>'
                + '</table>'
                + '</div>'
        });
    };

    window.assignTeamMember = async function (leadId, teamMemberId) {
        if (!teamMemberId) {
            // Unassign
            BlockUI.show('Unassigning...');
            try {
                var response = await fetch('/Sales/AxPostUnassignTeamMember?leadId=' + leadId, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                });
                var data = await response.json();
                BlockUI.hide();

                if (data.success) {
                    window.location.reload();
                } else {
                    Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
                }
            } catch (e) {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
            }
        } else {
            // Assign
            BlockUI.show('Assigning...');
            try {
                var response = await fetch('/Sales/AxPostAssignTeamMember?leadId=' + leadId + '&teamMemberId=' + teamMemberId, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                });
                var data = await response.json();
                BlockUI.hide();

                if (data.success) {
                    window.location.reload();
                } else {
                    Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
                }
            } catch (e) {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
            }
        }
    };
})();



