/* ==========================================================================
   Payment Reminder Settings — Schedule configuration, tier toggles,
   email preview tabs, and system toggle
   Depends on: BlockUI (block-ui.js), SweetAlert2
   ========================================================================== */

(function () {
    'use strict';

    // =========================================================================
    // Initialization
    // =========================================================================
    document.addEventListener('DOMContentLoaded', function () {
        bindSaveButton();
        bindTierToggles();
        initTierToggleStates();
    });

    // =========================================================================
    // Save Schedule
    // =========================================================================
    function bindSaveButton() {
        var btn = document.getElementById('btnSaveSchedule');
        if (!btn) return;
        btn.addEventListener('click', saveSchedule);
    }

    async function saveSchedule() {
        var tierNames = ['friendly', 'firm', 'formal'];
        var tiers = tierNames.map(function (tier) {
            var absOffset = parseInt(document.getElementById('offset-' + tier).value, 10);
            var direction = document.getElementById('direction-' + tier).value;
            var signedOffset = direction === 'before' ? -absOffset : absOffset;

            return {
                escalationTier: tier.charAt(0).toUpperCase() + tier.slice(1),
                daysOffset: signedOffset,
                maxRemindersPerTier: parseInt(document.getElementById('max-' + tier).value, 10),
                minIntervalDays: parseInt(document.getElementById('interval-' + tier).value, 10),
                partialPaymentSuppressionDays: parseInt(document.getElementById('suppression-days').value, 10),
                isEnabled: document.getElementById('toggle-' + tier).checked
            };
        });

        // Client-side validation: enforce Friendly < Firm < Formal offset ordering
        var friendly = tiers[0].daysOffset;
        var firm = tiers[1].daysOffset;
        var formal = tiers[2].daysOffset;

        if (isNaN(friendly) || isNaN(firm) || isNaN(formal)) {
            Swal.fire({ icon: 'warning', title: 'Validation Error', text: 'All day offset fields must be valid numbers.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        if (!(friendly < firm && firm < formal)) {
            Swal.fire({ icon: 'warning', title: 'Validation Error', text: 'Day offsets must follow the order: Friendly < Firm < Formal.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        BlockUI.show('Saving schedule...');
        try {
            var response = await fetch('/PaymentReminder/AxPostSaveSchedule', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify({ tiers: tiers })
            });
            var data = await response.json();
            BlockUI.hide();

            if (data.success) {
                Swal.fire({ icon: 'success', title: 'Saved', text: data.message, confirmButtonColor: '#0D5EA6' });
            } else {
                Swal.fire({ icon: 'warning', title: 'Validation Error', text: data.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    }

    // =========================================================================
    // Per-Tier Toggle Handlers
    // Grey out the rest of a row when its toggle is unchecked
    // =========================================================================
    function bindTierToggles() {
        document.querySelectorAll('.tier-toggle').forEach(function (toggle) {
            toggle.addEventListener('change', function () {
                applyTierToggleState(this);
            });
        });
    }

    function applyTierToggleState(toggle) {
        var row = toggle.closest('tr');
        if (!row) return;
        var otherCells = row.querySelectorAll('td:not(:first-child)');
        var opacity = toggle.checked ? '1' : '0.5';
        otherCells.forEach(function (cell) {
            cell.style.opacity = opacity;
        });
    }

    function initTierToggleStates() {
        // Apply correct opacity on page load based on initial checked state
        document.querySelectorAll('.tier-toggle').forEach(function (toggle) {
            applyTierToggleState(toggle);
        });
    }

    // =========================================================================
    // Email Preview Tab Switching
    // =========================================================================
    function showEmailTab(tier, btnElement) {
        // Hide all preview panels
        document.querySelectorAll('.reminder-email-preview').forEach(function (el) {
            el.classList.remove('reminder-email-preview--active');
        });

        // Remove active class from all tab buttons
        document.querySelectorAll('.reminder-tab-btn').forEach(function (el) {
            el.classList.remove('reminder-tab-btn--active');
        });

        // Show the selected preview panel
        var preview = document.getElementById('email-tab-' + tier);
        if (preview) preview.classList.add('reminder-email-preview--active');

        // Mark the clicked button as active
        if (btnElement) {
            btnElement.classList.add('reminder-tab-btn--active');
        }
    }

    // =========================================================================
    // Antiforgery Token Helper
    // =========================================================================
    function getAntiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    // =========================================================================
    // Expose global functions (called from onclick attributes in the view)
    // =========================================================================
    window.saveSchedule = saveSchedule;
    window.showEmailTab = showEmailTab;

    // Tier toggle — called from inline onchange="onTierToggle('friendly')"
    window.onTierToggle = function (tierName) {
        var toggle = document.getElementById('toggle-' + tierName);
        if (!toggle) return;

        var row = document.getElementById('row-' + tierName);
        if (!row) return;

        if (toggle.checked) {
            row.classList.remove('tier-disabled');
        } else {
            row.classList.add('tier-disabled');
        }
    };

    // System toggle — persists to database via AJAX
    window.toggleReminderSystem = async function (enabled) {
        var label = document.getElementById('system-toggle-label');
        BlockUI.show(enabled ? 'Enabling...' : 'Disabling...');
        try {
            var response = await fetch('/PaymentReminder/AxPostToggleReminderSystem?enabled=' + enabled, {
                method: 'POST',
                headers: { 'RequestVerificationToken': getAntiForgeryToken() }
            });
            var data = await response.json();
            BlockUI.hide();
            if (data.success) {
                if (label) {
                    label.textContent = enabled ? 'Enabled' : 'Disabled';
                    label.style.color = enabled ? '#129867' : '#5a6a7a';
                }
                Swal.fire({ icon: 'success', title: enabled ? 'Enabled' : 'Disabled', text: data.message, confirmButtonColor: '#0D5EA6', timer: 2000, showConfirmButton: false });
            } else {
                // Revert toggle on failure
                document.getElementById('system-toggle').checked = !enabled;
                Swal.fire({ icon: 'error', title: 'Error', text: data.message || 'Failed to update.', confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            document.getElementById('system-toggle').checked = !enabled;
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

})();
