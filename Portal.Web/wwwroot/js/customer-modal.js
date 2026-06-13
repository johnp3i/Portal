/* ==========================================================================
   Customer Modal — Inline customer creation for Quotation and Invoice forms.
   Handles opening/closing the modal, client-side validation, AJAX submission,
   and updating the target customer dropdown on success.
   Requirements: 3.1, 3.2, 3.3, 3.4, 4.1–4.8, 5.1–5.4, 7.1–7.3
   ========================================================================== */

/**
 * Module-level variable to store the target dropdown element id.
 * Set when openCustomerModal(dropdownId) is called.
 */
var _customerModalTargetDropdownId = null;

/**
 * Gets the antiforgery token from the modal form.
 */
function getCustomerModalAntiForgeryToken() {
    var tokenInput = document.querySelector('#customerModalForm input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

/**
 * Opens the customer creation modal.
 * Clears all fields and validation messages, sets focus on Name input,
 * and stores the target dropdown id for use after successful creation.
 * @param {string} dropdownId - The id of the target <select> element to update on success
 */
function openCustomerModal(dropdownId) {
    _customerModalTargetDropdownId = dropdownId;

    var modal = document.getElementById('customerModal');
    if (!modal) return;

    // Clear all input fields
    var form = document.getElementById('customerModalForm');
    if (form) {
        var inputs = form.querySelectorAll('input:not([type="hidden"])');
        for (var i = 0; i < inputs.length; i++) {
            inputs[i].value = '';
        }
    }

    // Clear all validation messages
    clearCustomerValidationMessages();

    // Show modal with flex display (activates flexbox centering)
    modal.style.display = 'flex';

    // Set focus on the Name field
    var nameInput = document.getElementById('modalCustomerName');
    if (nameInput) {
        setTimeout(function () { nameInput.focus(); }, 50);
    }
}

/**
 * Closes the customer creation modal without making any server request.
 */
function closeCustomerModal() {
    var modal = document.getElementById('customerModal');
    if (modal) {
        modal.style.display = 'none';
    }
}

/**
 * Clears all validation messages in the customer modal.
 */
function clearCustomerValidationMessages() {
    var messages = document.querySelectorAll('#customerModalForm .modal-validation-msg');
    for (var i = 0; i < messages.length; i++) {
        messages[i].textContent = '';
    }
}

/**
 * Validates the customer modal form fields.
 * Returns true if valid, false otherwise.
 * Displays inline red validation messages adjacent to failing fields.
 */
function validateCustomerForm() {
    var isValid = true;

    // Clear previous validation messages before re-evaluating
    clearCustomerValidationMessages();

    // Name: required (non-whitespace)
    var nameInput = document.getElementById('modalCustomerName');
    var nameValue = nameInput ? nameInput.value.trim() : '';
    if (!nameValue) {
        var nameValidation = document.getElementById('validationName');
        if (nameValidation) {
            nameValidation.textContent = 'Customer name is required.';
        }
        isValid = false;
    }

    // Email: valid format if non-empty
    var emailInput = document.getElementById('modalCustomerEmail');
    var emailValue = emailInput ? emailInput.value.trim() : '';
    if (emailValue) {
        var emailRegex = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
        if (!emailRegex.test(emailValue)) {
            var emailValidation = document.getElementById('validationEmail');
            if (emailValidation) {
                emailValidation.textContent = 'Please enter a valid email address.';
            }
            isValid = false;
        }
    }

    return isValid;
}

/**
 * Validates and submits the customer modal form via AJAX.
 * On success: closes modal, appends new option to target dropdown, selects it, shows success alert.
 * On error: shows appropriate SweetAlert2 notification.
 */
async function submitCustomerModal() {
    // Client-side validation
    if (!validateCustomerForm()) {
        return;
    }

    // Build form data from the modal form
    var form = document.getElementById('customerModalForm');
    if (!form) return;

    var formData = new FormData(form);

    // Block UI while request is in progress
    BlockUI.show('Creating customer...');

    try {
        var response = await fetch('/Customer/CreateInline', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getCustomerModalAntiForgeryToken()
            },
            body: formData
        });

        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            // Validate response contains valid id and name
            if (data.id && data.name) {
                // Append new option to target dropdown and select it
                var dropdown = document.getElementById(_customerModalTargetDropdownId);
                if (dropdown) {
                    var option = document.createElement('option');
                    option.value = data.id;
                    option.textContent = data.name;
                    dropdown.appendChild(option);
                    dropdown.value = data.id;
                }

                // Close modal
                closeCustomerModal();

                // Show success notification
                Swal.fire({
                    title: 'Customer Created',
                    text: 'The customer has been created successfully.',
                    icon: 'success',
                    confirmButtonColor: '#0D5EA6'
                });
            } else {
                // Malformed response — missing id/name
                closeCustomerModal();
                Swal.fire({
                    title: 'Warning',
                    text: 'Customer created but the dropdown could not be updated.',
                    icon: 'warning',
                    confirmButtonColor: '#0D5EA6'
                });
            }
        } else {
            // Server returned an error response
            Swal.fire({
                title: 'Error',
                text: data.message || 'An unexpected error occurred.',
                icon: 'error',
                confirmButtonColor: '#0D5EA6'
            });
        }
    } catch (e) {
        // Network error or unexpected failure
        BlockUI.hide();
        Swal.fire({
            title: 'Error',
            text: 'An unexpected error occurred. Please try again.',
            icon: 'error',
            confirmButtonColor: '#0D5EA6'
        });
    }
}

// --- Event Listeners ---

// Close modal on Escape key (Requirement 2.6, 7.1)
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        var modal = document.getElementById('customerModal');
        if (modal && modal.style.display !== 'none' && modal.style.display !== '') {
            closeCustomerModal();
        }
    }
});

// Close modal on backdrop click (Requirement 7.2)
document.addEventListener('click', function (e) {
    var modal = document.getElementById('customerModal');
    if (modal && e.target === modal) {
        closeCustomerModal();
    }
});

// Expose functions globally for onclick attributes
window.openCustomerModal = openCustomerModal;
window.closeCustomerModal = closeCustomerModal;
window.submitCustomerModal = submitCustomerModal;
