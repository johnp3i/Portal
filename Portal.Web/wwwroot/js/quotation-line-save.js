/* ==========================================================================
   Quotation Line Save — AJAX form interception module
   Intercepts UpdateLine/AddLine form submissions, sends via fetch(),
   provides overlay feedback, success flash, and inline error messaging.
   ========================================================================== */

(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', initLineSaveHandlers);

    /**
     * Queries all forms whose action contains /UpdateLine or /AddLine
     * and attaches submit event listeners.
     */
    function initLineSaveHandlers() {
        var forms = document.querySelectorAll('form[action*="/UpdateLine"], form[action*="/AddLine"]');
        forms.forEach(function (form) {
            form.addEventListener('submit', handleSubmit);
        });
    }

    /**
     * Core submit handler — preventDefault, show overlay, serialize FormData,
     * fetch with AJAX header, handle response.
     */
    async function handleSubmit(e) {
        e.preventDefault();

        var form = e.target;
        var lineCard = form.closest('.line-card');
        var action = form.getAttribute('action') || '';
        var isAddLine = action.indexOf('/AddLine') !== -1;

        clearError(lineCard);
        showOverlay();

        var controller = new AbortController();
        var timeoutId = setTimeout(function () {
            controller.abort();
        }, 30000);

        try {
            var formData = new FormData(form);
            var response = await fetch(action, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData,
                signal: controller.signal
            });

            clearTimeout(timeoutId);

            var data;
            try {
                data = await response.json();
            } catch (parseError) {
                hideOverlay();
                showError(lineCard, 'An unexpected error occurred.');
                return;
            }

            hideOverlay();

            if (data.success) {
                if (isAddLine) {
                    window.location.reload();
                } else {
                    flashSuccess(lineCard);
                }
            } else {
                showError(lineCard, data.message || 'An unexpected error occurred.');
            }
        } catch (error) {
            clearTimeout(timeoutId);
            hideOverlay();

            if (error.name === 'AbortError') {
                showError(lineCard, 'The request timed out. Please try again.');
            } else {
                showError(lineCard, 'Unable to reach the server. Check your connection.');
            }
        }
    }

    /**
     * Creates and displays the blockUI overlay with spinner.
     */
    function showOverlay() {
        var existing = document.querySelector('.blockui-overlay');
        if (existing) return;

        var overlay = document.createElement('div');
        overlay.className = 'blockui-overlay';

        var spinner = document.createElement('div');
        spinner.className = 'spinner';
        overlay.appendChild(spinner);

        document.body.appendChild(overlay);
    }

    /**
     * Removes the blockUI overlay from the DOM.
     */
    function hideOverlay() {
        var overlay = document.querySelector('.blockui-overlay');
        if (overlay) {
            overlay.remove();
        }
    }

    /**
     * Adds the .line-card--saved class to trigger the success animation,
     * then removes it after 2 seconds.
     */
    function flashSuccess(lineCard) {
        if (!lineCard) return;

        lineCard.classList.add('line-card--saved');
        setTimeout(function () {
            lineCard.classList.remove('line-card--saved');
        }, 2000);
    }

    /**
     * Inserts an error element adjacent to the line card with a dismiss button.
     */
    function showError(lineCard, message) {
        if (!lineCard) return;

        clearError(lineCard);

        var errorEl = document.createElement('div');
        errorEl.className = 'line-card__error';
        errorEl.textContent = message;

        var dismissBtn = document.createElement('button');
        dismissBtn.className = 'dismiss-btn';
        dismissBtn.type = 'button';
        dismissBtn.textContent = '\u00D7';
        dismissBtn.addEventListener('click', function () {
            errorEl.remove();
        });

        errorEl.appendChild(dismissBtn);
        lineCard.insertAdjacentElement('afterend', errorEl);
    }

    /**
     * Removes any existing error element for the given line card.
     */
    function clearError(lineCard) {
        if (!lineCard) return;

        var nextEl = lineCard.nextElementSibling;
        if (nextEl && nextEl.classList.contains('line-card__error')) {
            nextEl.remove();
        }
    }
})();
