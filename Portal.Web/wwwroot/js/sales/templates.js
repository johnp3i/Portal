/**
 * Sales Templates — CRUD interactions
 */
(function () {
    'use strict';

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    window.openCreateTemplateModal = function () {
        document.getElementById('templateModalTitle').textContent = 'New Template';
        document.getElementById('templateId').value = '';
        document.getElementById('templateName').value = '';
        document.getElementById('templateProductId').value = '';
        document.getElementById('templateResponseTypeId').value = '1';
        document.getElementById('templateSubject').value = '';
        if (window.templateQuill) window.templateQuill.root.innerHTML = '';
        document.getElementById('templateResponseTime').value = '24';
        document.getElementById('templateModal').style.display = 'flex';
    };

    window.editTemplate = async function (id) {
        BlockUI.show('Loading...');
        try {
            var response = await fetch('/Sales/AxGetTemplateById?id=' + id);
            var result = await response.json();
            BlockUI.hide();

            if (!result.success) {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
                return;
            }

            var t = result.data;
            document.getElementById('templateModalTitle').textContent = 'Edit Template';
            document.getElementById('templateId').value = t.id;
            document.getElementById('templateName').value = t.name || '';
            document.getElementById('templateProductId').value = t.productId || '';
            document.getElementById('templateResponseTypeId').value = t.leadResponseTypeId || '';
            document.getElementById('templateSubject').value = t.subject || '';
            if (window.templateQuill) window.templateQuill.root.innerHTML = t.bodyTemplate || '';
            document.getElementById('templateResponseTime').value = t.responseTimeInHours || 24;
            document.getElementById('templateModal').style.display = 'flex';
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to load template.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.closeTemplateModal = function () {
        document.getElementById('templateModal').style.display = 'none';
    };

    window.submitTemplate = async function () {
        var id = document.getElementById('templateId').value;
        var bodyHtml = window.templateQuill ? window.templateQuill.root.innerHTML : '';
        // Quill inserts <p><br></p> for empty — treat as empty
        if (bodyHtml === '<p><br></p>') bodyHtml = '';

        var payload = {
            name: document.getElementById('templateName').value,
            productId: document.getElementById('templateProductId').value ? parseInt(document.getElementById('templateProductId').value) : null,
            leadResponseTypeId: parseInt(document.getElementById('templateResponseTypeId').value),
            subject: document.getElementById('templateSubject').value || null,
            bodyTemplate: bodyHtml,
            responseTimeInHours: parseInt(document.getElementById('templateResponseTime').value) || 24
        };

        if (!payload.name || !payload.bodyTemplate) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Name and body template are required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        var url = id ? '/Sales/AxPostUpdateTemplate' : '/Sales/AxPostCreateTemplate';
        if (id) payload.id = parseInt(id);

        BlockUI.show('Saving...');
        try {
            var response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify(payload)
            });
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                closeTemplateModal();
                Swal.fire({ icon: 'success', title: 'Saved', text: 'Template saved successfully.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.deactivateTemplate = function (id, name) {
        Swal.fire({
            title: 'Deactivate Template?',
            text: 'Are you sure you want to deactivate "' + name + '"?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, deactivate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#C24A4A'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Deactivating...');
                try {
                    var response = await fetch('/Sales/AxPostDeactivateTemplate?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Template deactivated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    window.activateTemplate = function (id, name) {
        Swal.fire({
            title: 'Activate Template?',
            text: 'Reactivate "' + name + '"?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, activate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Activating...');
                try {
                    var response = await fetch('/Sales/AxPostActivateTemplate?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Template activated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
                    } else {
                        Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
                    }
                } catch (e) {
                    BlockUI.hide();
                    Swal.fire({ icon: 'error', title: 'Error', text: 'An error occurred.', confirmButtonColor: '#0D5EA6' });
                }
            }
        });
    };
})();
