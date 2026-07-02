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
        bindSystemToggle();
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
        var tiers = ['Friendly', 'Firm', 'Formal'].map(function (tier) {
            return {
                escalationTier: tier,
                daysOffset: parseInt(document.getElementById('daysOffset-' + tier).value, 10),
                maxRemindersPerTier: parseInt(document.getElementById('maxReminders-' + tier).value, 10),
                minIntervalDays: parseInt(document.getElementById('minInterval-' + tier).value, 10),
                partialPaymentSuppressionDays: parseInt(document.getElementById('suppressionDays').value, 10),
                isEnabled: document.getElementById('tierEnabled-' + tier).checked
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
    function showEmailTab(tier, event) {
        document.querySelectorAll('.email-tab-content').forEach(function (el) {
            el.style.display = 'none';
        });
        document.querySelectorAll('.email-tab-btn').forEach(function (el) {
            el.classList.remove('active');
        });

        var preview = document.getElementById('emailPreview-' + tier);
        if (preview) preview.style.display = 'block';

        if (event && event.target) {
            event.target.classList.add('active');
        }
    }

    // =========================================================================
    // System Reminder Toggle
    // =========================================================================
    function bindSystemToggle() {
        var toggle = document.getElementById('systemReminderToggle');
        if (!toggle) return;

        toggle.addEventListener('change', function () {
            var label = document.getElementById('systemReminderStatus');
            if (!label) return;
            label.textContent = this.checked ? 'Enabled' : 'Disabled';
            label.style.color = this.checked ? '#129867' : '#5a6a7a';
        });
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

})();
