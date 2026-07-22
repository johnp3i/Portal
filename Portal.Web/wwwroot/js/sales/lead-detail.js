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
        var currentText = document.getElementById('requestDetailsText')?.textContent || '';
        if (currentText === 'No request details provided.') currentText = '';

        Swal.fire({
            title: 'Edit Request Details',
            html: '<textarea id="swalRequestDetails" rows="5" style="width:100%;padding:12px 16px;border:1.5px solid rgba(13,94,166,.15);border-radius:12px;font-size:14px;font-family:Inter,sans-serif;resize:vertical;">' + currentText.trim() + '</textarea>',
            showCancelButton: true,
            confirmButtonText: 'Save',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6',
            preConfirm: function () {
                return document.getElementById('swalRequestDetails').value;
            }
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Saving...');
                try {
                    var response = await fetch('/Sales/AxPostUpdateRequestDetails', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                        body: JSON.stringify({ id: id, requestText: result.value })
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Saved', text: 'Request details updated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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
        var text = bodyEl.innerText || bodyEl.textContent;
        if (!text || text.trim() === '' || text.trim() === 'Select a template to see the rendered preview.') {
            Swal.fire({ icon: 'info', title: 'Nothing to copy', text: 'Body is empty.', confirmButtonColor: '#0D5EA6' });
            return;
        }
        navigator.clipboard.writeText(text).then(function () {
            Swal.fire({ icon: 'success', title: 'Copied', text: 'Body copied to clipboard.', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false });
        });
    };

    window.copyComposeAll = function () {
        var subject = document.getElementById('composeSubject').value || '';
        var bodyEl = document.getElementById('composeBodyPreview');
        var body = bodyEl.innerText || bodyEl.textContent;
        if (!subject && (!body || body.trim() === '' || body.trim() === 'Select a template to see the rendered preview.')) {
            Swal.fire({ icon: 'info', title: 'Nothing to copy', text: 'No content to copy.', confirmButtonColor: '#0D5EA6' });
            return;
        }
        var combined = '';
        if (subject) combined += 'Subject: ' + subject + '\n\n';
        combined += body;
        navigator.clipboard.writeText(combined).then(function () {
            Swal.fire({ icon: 'success', title: 'Copied', text: 'Subject and body copied to clipboard.', confirmButtonColor: '#0D5EA6', timer: 1500, showConfirmButton: false });
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
})();
