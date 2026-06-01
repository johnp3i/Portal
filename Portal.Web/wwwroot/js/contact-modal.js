/**
 * Contact Modal — Context-aware contact form modal for the landing page.
 * 
 * Opens with contextual badge/title/subtitle based on the CTA button's
 * data-inquiry-type attribute. Handles form validation, reCAPTCHA v3 token
 * acquisition, fetch submission, and success/error feedback via button text
 * changes and a toast notification.
 * 
 * Requirements: 1.1–1.6, 3.1–3.5, 4.1–4.6, 5.2–5.4, 13.2
 */
(function () {
    'use strict';

    // --- Context map ---
    var MODAL_CONTEXTS = {
        'Demo Request': {
            badge: 'Demo Request',
            title: 'Request a Demo',
            subtitle: 'See the platform in action. We\'ll walk you through it.'
        },
        'Pricing - Core': {
            badge: 'Core Plan',
            title: 'Interested in the Core plan?',
            subtitle: 'Tell us about your team and we\'ll help you get started.'
        },
        'Pricing - Enhanced': {
            badge: 'Enhanced Plan',
            title: 'Interested in the Enhanced plan?',
            subtitle: 'Tell us about your team and we\'ll help you get started.'
        },
        'Pricing - Enterprise': {
            badge: 'Enterprise Plan',
            title: 'Let\'s tailor a plan for you',
            subtitle: 'Tell us about your organisation and we\'ll build a custom proposal.'
        },
        'General Inquiry': {
            badge: 'Contact Us',
            title: 'Talk to Us',
            subtitle: 'Have a question? We\'ll get back to you within 24 hours.'
        }
    };

    // --- DOM references ---
    var overlay = document.getElementById('contactModalOverlay');
    var card = document.getElementById('contactModalCard');
    var closeBtn = document.getElementById('contactModalClose');
    var badge = document.getElementById('contactModalBadge');
    var title = document.getElementById('contactModalTitle');
    var subtitle = document.getElementById('contactModalSubtitle');
    var hiddenInquiry = document.getElementById('contactInquiryType');
    var form = document.getElementById('contactForm');
    var submitBtn = document.getElementById('contactFormSubmit');
    var firstNameInput = document.getElementById('contactFirstName');
    var emailInput = document.getElementById('contactEmail');

    // --- reCAPTCHA site key ---
    var scriptTag = document.querySelector('script[data-recaptcha-sitekey]');
    var siteKey = scriptTag ? scriptTag.getAttribute('data-recaptcha-sitekey') : '';

    var DEFAULT_BUTTON_TEXT = 'Send Request';
    var buttonRevertTimer = null;

    // --- Modal open ---
    function openModal(inquiryType) {
        var context = MODAL_CONTEXTS[inquiryType] || MODAL_CONTEXTS['General Inquiry'];

        badge.textContent = context.badge;
        title.textContent = context.title;
        subtitle.textContent = context.subtitle;
        hiddenInquiry.value = inquiryType || 'General Inquiry';

        overlay.classList.add('is-visible');
        document.body.style.overflow = 'hidden';

        // Move focus to first focusable element (Requirement 13.2)
        var firstFocusable = card.querySelector('input:not([type="hidden"]):not([tabindex="-1"]), select, button');
        if (firstFocusable) {
            setTimeout(function () { firstFocusable.focus(); }, 50);
        }
    }

    // --- Modal close ---
    function closeModal() {
        overlay.classList.remove('is-visible');
        document.body.style.overflow = '';
        resetForm();
    }

    // --- Form reset ---
    function resetForm() {
        form.reset();
        submitBtn.disabled = false;
        submitBtn.textContent = DEFAULT_BUTTON_TEXT;
        if (buttonRevertTimer) {
            clearTimeout(buttonRevertTimer);
            buttonRevertTimer = null;
        }
    }

    // --- Validation ---
    function isValidEmail(email) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    }

    function validateForm() {
        var firstName = firstNameInput.value.trim();
        var email = emailInput.value.trim();

        if (!firstName) {
            firstNameInput.focus();
            return false;
        }
        if (!email || !isValidEmail(email)) {
            emailInput.focus();
            return false;
        }
        return true;
    }

    // --- Button text with auto-revert ---
    function setButtonText(text, revertAfterMs) {
        submitBtn.textContent = text;
        if (revertAfterMs) {
            buttonRevertTimer = setTimeout(function () {
                submitBtn.textContent = DEFAULT_BUTTON_TEXT;
                submitBtn.disabled = false;
                buttonRevertTimer = null;
            }, revertAfterMs);
        }
    }

    // --- reCAPTCHA token ---
    function getRecaptchaToken() {
        // Skip reCAPTCHA in development when no site key is configured
        if (!siteKey) {
            return Promise.resolve('');
        }
        if (typeof grecaptcha === 'undefined') {
            return Promise.reject(new Error('reCAPTCHA not available'));
        }
        return new Promise(function (resolve, reject) {
            grecaptcha.ready(function () {
                grecaptcha.execute(siteKey, { action: 'contact_form' })
                    .then(resolve)
                    .catch(reject);
            });
        });
    }

    // --- Success toast ---
    function showSuccessToast(message) {
        var toast = document.createElement('div');
        toast.setAttribute('role', 'status');
        toast.setAttribute('aria-live', 'polite');
        toast.style.cssText = 'position:fixed;top:28px;left:50%;transform:translateX(-50%);z-index:10000;background:#129867;color:#fff;padding:16px 28px;border-radius:14px;font-family:Inter,system-ui,sans-serif;font-size:15px;font-weight:600;box-shadow:0 12px 36px rgba(18,152,103,.28);opacity:0;transition:opacity .3s ease;max-width:90vw;text-align:center;';
        toast.textContent = message;
        document.body.appendChild(toast);

        // Fade in
        requestAnimationFrame(function () {
            toast.style.opacity = '1';
        });

        // Auto-hide after 6 seconds
        setTimeout(function () {
            toast.style.opacity = '0';
            setTimeout(function () {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                }
            }, 300);
        }, 6000);
    }

    // --- Form submission ---
    function handleSubmit(e) {
        e.preventDefault();

        if (!validateForm()) {
            return;
        }

        // Disable button and show sending state (Requirement 4.1)
        submitBtn.disabled = true;
        submitBtn.textContent = 'Sending...';

        // Acquire reCAPTCHA token (Requirement 5.2)
        getRecaptchaToken()
            .then(function (token) {
                return submitForm(token);
            })
            .catch(function (err) {
                // reCAPTCHA failure (Requirement 5.3)
                if (err && err.message === 'reCAPTCHA not available') {
                    setButtonText('Verification failed \u2014 try again', 3000);
                    return;
                }
                // If it was a reCAPTCHA execute failure
                setButtonText('Verification failed \u2014 try again', 3000);
            });
    }

    function submitForm(recaptchaToken) {
        var formData = new FormData(form);
        formData.append('recaptchaToken', recaptchaToken);

        // Get antiforgery token
        var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        var tokenValue = tokenInput ? tokenInput.value : '';

        return fetch(form.action, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': tokenValue
            },
            body: formData
        })
        .then(function (response) {
            if (response.ok) {
                // Success (Requirement 4.2)
                closeModal();
                showSuccessToast('Your request has been sent. We will get in touch within 24 hours.');
            } else {
                // Server error (Requirement 4.3)
                setButtonText('Something went wrong \u2014 try again', 3000);
            }
        })
        .catch(function () {
            // Network error (Requirement 4.4)
            setButtonText('Connection error \u2014 try again', 3000);
        });
    }

    // --- Event listeners ---

    // CTA buttons open modal
    document.addEventListener('click', function (e) {
        var ctaBtn = e.target.closest('[data-inquiry-type]');
        if (ctaBtn) {
            e.preventDefault();
            var inquiryType = ctaBtn.getAttribute('data-inquiry-type');
            openModal(inquiryType);
        }
    });

    // Close button
    if (closeBtn) {
        closeBtn.addEventListener('click', closeModal);
    }

    // Backdrop click (Requirement 3.3)
    if (overlay) {
        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) {
                closeModal();
            }
        });
    }

    // Escape key (Requirement 3.4)
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && overlay.classList.contains('is-visible')) {
            closeModal();
        }
    });

    // Form submit
    if (form) {
        form.addEventListener('submit', handleSubmit);
    }

})();
