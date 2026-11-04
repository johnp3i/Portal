/**
 * Sales Contacts — CRUD interactions
 */
(function () {
    'use strict';

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    // Search is handled by a native <form method="get"> in the view (Enter + Clear work
    // without JS), matching the Products page pattern.

    window.openCreateContactModal = function () {
        document.getElementById('contactModalTitle').textContent = 'New Contact';
        document.getElementById('contactId').value = '';
        document.getElementById('contactFirstName').value = '';
        document.getElementById('contactLastName').value = '';
        document.getElementById('contactEmail').value = '';
        document.getElementById('contactPhone').value = '';
        document.getElementById('contactCompany').value = '';
        document.getElementById('contactJobTitle').value = '';
        document.getElementById('contactCountry').value = '';
        document.getElementById('contactNotes').value = '';
        document.getElementById('contactModal').style.display = 'flex';
    };

    // Reads row data from the button's data-* attributes (safer than hand-escaped inline
    // onclick args — no quote/newline/markup-injection risk).
    window.editContactFromData = function (btn) {
        document.getElementById('contactModalTitle').textContent = 'Edit Contact';
        document.getElementById('contactId').value = btn.getAttribute('data-id') || '';
        document.getElementById('contactFirstName').value = btn.getAttribute('data-first-name') || '';
        document.getElementById('contactLastName').value = btn.getAttribute('data-last-name') || '';
        document.getElementById('contactEmail').value = btn.getAttribute('data-email') || '';
        document.getElementById('contactPhone').value = btn.getAttribute('data-phone') || '';
        document.getElementById('contactCompany').value = btn.getAttribute('data-company') || '';
        document.getElementById('contactJobTitle').value = btn.getAttribute('data-job-title') || '';
        document.getElementById('contactCountry').value = btn.getAttribute('data-country') || '';
        document.getElementById('contactNotes').value = btn.getAttribute('data-notes') || '';
        document.getElementById('contactModal').style.display = 'flex';
    };

    window.deactivateContactFromData = function (btn) {
        deactivateContact(parseInt(btn.getAttribute('data-id')), btn.getAttribute('data-name') || '');
    };

    window.activateContactFromData = function (btn) {
        activateContact(parseInt(btn.getAttribute('data-id')), btn.getAttribute('data-name') || '');
    };

    window.closeContactModal = function () {
        document.getElementById('contactModal').style.display = 'none';
    };

    // Consistent modal dismissal: click the backdrop or press Escape to close.
    document.addEventListener('DOMContentLoaded', function () {
        var modal = document.getElementById('contactModal');
        if (modal) {
            modal.addEventListener('click', function (e) { if (e.target === modal) closeContactModal(); });
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && modal && modal.style.display !== 'none') closeContactModal();
        });
    });

    window.submitContact = async function () {
        var id = document.getElementById('contactId').value;
        var payload = {
            firstName: document.getElementById('contactFirstName').value,
            lastName: document.getElementById('contactLastName').value || null,
            email: document.getElementById('contactEmail').value || null,
            phoneNumber: document.getElementById('contactPhone').value || null,
            companyName: document.getElementById('contactCompany').value || null,
            jobTitle: document.getElementById('contactJobTitle').value || null,
            country: document.getElementById('contactCountry').value || null,
            notes: document.getElementById('contactNotes').value || null
        };

        if (!payload.firstName) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'First name is required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        var url = id ? '/Sales/AxPostUpdateContact' : '/Sales/AxPostCreateContact';
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
                closeContactModal();
                Swal.fire({ icon: 'success', title: 'Saved', text: 'Contact saved successfully.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.deactivateContact = function (id, name) {
        Swal.fire({
            title: 'Deactivate Contact?',
            text: 'Are you sure you want to deactivate ' + name + '?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, deactivate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#C24A4A'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Deactivating...');
                try {
                    var response = await fetch('/Sales/AxPostDeactivateContact?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Contact deactivated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    window.activateContact = function (id, name) {
        Swal.fire({
            title: 'Activate Contact?',
            text: 'Reactivate ' + name + '?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, activate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Activating...');
                try {
                    var response = await fetch('/Sales/AxPostActivateContact?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Contact activated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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
})();
