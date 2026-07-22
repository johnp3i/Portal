/**
 * Sales Meetings — Create and cancel meetings
 */
(function () {
    'use strict';

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    document.addEventListener('DOMContentLoaded', function () {
        loadContactsForMeetingForm();
    });

    async function loadContactsForMeetingForm() {
        try {
            var response = await fetch('/Sales/AxGetContactsSearch?page=1');
            var result = await response.json();
            if (result.success) {
                var select = document.getElementById('meetingContactId');
                select.innerHTML = '<option value="">Select contact...</option>';
                result.data.forEach(function (c) {
                    select.innerHTML += '<option value="' + c.id + '">' + c.fullName + '</option>';
                });

                // Pre-select contact from query string if present
                var params = new URLSearchParams(window.location.search);
                var preselectedContact = params.get('contactId');
                if (preselectedContact) {
                    select.value = preselectedContact;
                    select.disabled = true;
                    select.style.opacity = '0.7';
                    select.style.cursor = 'not-allowed';
                }

                // Auto-open modal if coming from Lead Detail
                var leadRequestId = params.get('leadRequestId');
                if (leadRequestId) {
                    openCreateMeetingModal();
                }
            }
        } catch (e) { console.error('Failed to load contacts', e); }
    }

    window.openCreateMeetingModal = function () {
        document.getElementById('meetingSubject').value = '';
        document.getElementById('meetingScheduledAt').value = '';
        document.getElementById('meetingDuration').value = '60';
        document.getElementById('meetingLocation').value = '';
        document.getElementById('meetingNotes').value = '';
        document.getElementById('meetingModal').style.display = 'flex';
    };

    window.closeMeetingModal = function () {
        document.getElementById('meetingModal').style.display = 'none';
    };

    window.submitMeeting = async function () {
        var contactId = document.getElementById('meetingContactId').value;
        var subject = document.getElementById('meetingSubject').value;
        var scheduledAt = document.getElementById('meetingScheduledAt').value;

        if (!contactId || !subject || !scheduledAt) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Contact, subject, and date are required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        var payload = {
            contactId: parseInt(contactId),
            meetingTypeId: parseInt(document.getElementById('meetingTypeId').value),
            subject: subject,
            scheduledAtUtc: scheduledAt,
            durationMinutes: parseInt(document.getElementById('meetingDuration').value) || 60,
            location: document.getElementById('meetingLocation').value || null,
            notes: document.getElementById('meetingNotes').value || null,
            leadRequestId: new URLSearchParams(window.location.search).get('leadRequestId') ? parseInt(new URLSearchParams(window.location.search).get('leadRequestId')) : null
        };

        BlockUI.show('Scheduling...');
        try {
            var response = await fetch('/Sales/AxPostCreateMeeting', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify(payload)
            });
            var result = await response.json();
            BlockUI.hide();

            if (result.success) {
                closeMeetingModal();
                Swal.fire({ icon: 'success', title: 'Scheduled', text: 'Meeting scheduled successfully.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.cancelMeeting = function (id, subject) {
        Swal.fire({
            title: 'Cancel Meeting?',
            text: 'Cancel "' + subject + '"? This cannot be undone.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, cancel it',
            cancelButtonText: 'Go Back',
            confirmButtonColor: '#C24A4A'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Cancelling...');
                try {
                    var response = await fetch('/Sales/AxPostCancelMeeting?id=' + id + '&description=', {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Cancelled', text: 'Meeting cancelled.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    window.reactivateMeeting = function (id, subject) {
        Swal.fire({
            title: 'Reactivate Meeting?',
            text: 'Reactivate "' + subject + '"?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, activate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Reactivating...');
                try {
                    var response = await fetch('/Sales/AxPostReactivateMeeting?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Activated', text: 'Meeting reactivated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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
