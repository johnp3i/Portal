/* ==========================================================================
   Product Autocomplete — Reusable autocomplete for Invoice & Quotation
   line item forms. Searches the Product catalog and historical line items
   via GET /Product/Autocomplete?query=X.

   Features:
   - Triggers after 2+ characters with 300ms debounce
   - Displays dropdown with Product results and Historical results
   - On selection: auto-fills Description, UnitPrice, VatRate, CostPrice, ProductCode
   - Works with dynamically added line item rows (event delegation)
   - Suppresses all errors gracefully (no UI disruption)
   ========================================================================== */

(function () {
    'use strict';

    var DEBOUNCE_MS = 300;
    var MIN_QUERY_LENGTH = 2;
    var AUTOCOMPLETE_URL = '/Product/Autocomplete';

    var debounceTimer = null;
    var activeDropdown = null;
    var activeInput = null;
    var selectedIndex = -1;

    // --- Utility Functions ---

    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function formatDate(dateStr) {
        if (!dateStr) return '';
        try {
            var d = new Date(dateStr);
            if (isNaN(d.getTime())) return '';
            return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
        } catch (e) {
            return '';
        }
    }

    function formatPrice(value) {
        if (value == null || value === '') return '0.00';
        return parseFloat(value).toFixed(2);
    }

    // --- Dropdown Management ---

    function createDropdown(input) {
        var field = input.closest('.field') || input.parentElement;
        var existing = field.querySelector('.product-autocomplete-dropdown');
        if (existing) return existing;

        var dropdown = document.createElement('div');
        dropdown.className = 'product-autocomplete-dropdown';
        field.appendChild(dropdown);
        return dropdown;
    }

    function showDropdown(dropdown) {
        dropdown.classList.add('active');
    }

    function hideDropdown(dropdown) {
        if (dropdown) {
            dropdown.classList.remove('active');
            dropdown.innerHTML = '';
        }
        selectedIndex = -1;
        activeDropdown = null;
        activeInput = null;
    }

    function hideAllDropdowns() {
        var dropdowns = document.querySelectorAll('.product-autocomplete-dropdown.active');
        dropdowns.forEach(function (dd) {
            hideDropdown(dd);
        });
    }

    // --- Result Rendering ---

    function renderResults(dropdown, results, input) {
        dropdown.innerHTML = '';

        if (!results || results.length === 0) {
            hideDropdown(dropdown);
            return;
        }

        results.forEach(function (entry, index) {
            var item = document.createElement('div');
            item.className = 'product-autocomplete-item';
            item.dataset.index = index;

            if (entry.source === 'Product') {
                // Product result: ProductCode, Description, UnitPrice, SupplierName
                var supplierText = entry.supplierName ? ' | ' + escapeHtml(entry.supplierName) : '';
                var codeText = entry.productCode ? '<span class="pac-code">' + escapeHtml(entry.productCode) + '</span> ' : '';
                item.innerHTML = '<div class="pac-main">'
                    + '<span class="pac-badge pac-badge-product">Product</span> '
                    + codeText
                    + '<span class="pac-desc">' + escapeHtml(entry.description) + '</span>'
                    + '</div>'
                    + '<div class="pac-meta">'
                    + 'Price: ' + formatPrice(entry.unitPrice)
                    + (entry.vatRate != null ? ' | VAT: ' + parseFloat(entry.vatRate).toFixed(2) + '%' : '')
                    + supplierText
                    + '</div>';
            } else {
                // Historical result: Description, UnitPrice, Date, Source indicator
                var sourceLabel = entry.source === 'Invoice' ? 'Invoice' : 'Quotation';
                var badgeClass = entry.source === 'Invoice' ? 'pac-badge-invoice' : 'pac-badge-quotation';
                var dateText = entry.date ? ' | ' + formatDate(entry.date) : '';
                var codeText2 = entry.productCode ? '<span class="pac-code">' + escapeHtml(entry.productCode) + '</span> ' : '';
                item.innerHTML = '<div class="pac-main">'
                    + '<span class="pac-badge ' + badgeClass + '">' + sourceLabel + '</span> '
                    + codeText2
                    + '<span class="pac-desc">' + escapeHtml(entry.description) + '</span>'
                    + '</div>'
                    + '<div class="pac-meta">'
                    + 'Price: ' + formatPrice(entry.unitPrice)
                    + (entry.vatRate != null ? ' | VAT: ' + parseFloat(entry.vatRate).toFixed(2) + '%' : '')
                    + dateText
                    + '</div>';
            }

            item.addEventListener('mousedown', function (e) {
                e.preventDefault();
                selectResult(input, entry);
                hideDropdown(dropdown);
            });

            dropdown.appendChild(item);
        });

        showDropdown(dropdown);
        activeDropdown = dropdown;
        activeInput = input;
        selectedIndex = -1;
    }

    // --- Selection Logic ---

    function selectResult(input, entry) {
        // Set the Description field value
        input.value = entry.description || '';

        // Find sibling fields in the same form/container
        var container = findLineItemContainer(input);
        if (!container) return;

        // Auto-fill UnitPrice
        var unitPriceField = findFieldByName(container, 'UnitPrice') || findFieldByName(container, 'unitPrice');
        if (unitPriceField) {
            unitPriceField.value = entry.unitPrice != null ? formatPrice(entry.unitPrice) : '';
            triggerChange(unitPriceField);
        }

        // Auto-fill VatRate
        var vatRateField = findFieldByName(container, 'VatRate') || findFieldByName(container, 'vatRate');
        if (vatRateField && entry.vatRate != null) {
            vatRateField.value = parseFloat(entry.vatRate).toFixed(2);
            triggerChange(vatRateField);
        }

        // Auto-fill CostPrice
        var costPriceField = findFieldByName(container, 'CostPrice') || findFieldByName(container, 'costPrice');
        if (costPriceField && entry.costPrice != null) {
            costPriceField.value = formatPrice(entry.costPrice);
            triggerChange(costPriceField);
        }

        // Auto-fill ProductCode (hidden field or visible field)
        var productCodeField = findFieldByName(container, 'ProductCode') || findFieldByName(container, 'productCode');
        if (productCodeField && entry.productCode) {
            productCodeField.value = entry.productCode;
            triggerChange(productCodeField);
        }

        // Trigger change on the description input itself
        triggerChange(input);
    }

    function triggerChange(el) {
        try {
            el.dispatchEvent(new Event('change', { bubbles: true }));
            el.dispatchEvent(new Event('input', { bubbles: true }));
        } catch (e) {
            // Suppress
        }
    }

    // --- Field Discovery ---

    /**
     * Finds the line item container (form, .line-card, .surface, or parent div)
     * that holds all the fields for a single line item.
     */
    function findLineItemContainer(input) {
        // Try .line-card or .surface.card-pad first (Invoice create/edit line items)
        var card = input.closest('.line-card') || input.closest('.surface.card-pad');
        if (card) return card;

        // Try form (Quotation line items are in individual forms)
        var form = input.closest('form');
        if (form) return form;

        // Fallback: parent container with form-grid
        var formGrid = input.closest('.form-grid');
        if (formGrid) return formGrid.parentElement;

        return input.parentElement;
    }

    /**
     * Finds an input/select field within a container by partial name match.
     * Handles indexed names like "lines[0].UnitPrice" and simple names like "UnitPrice".
     */
    function findFieldByName(container, fieldName) {
        // Direct name match
        var el = container.querySelector('[name="' + fieldName + '"]');
        if (el) return el;

        // Indexed name match (e.g., lines[0].UnitPrice)
        el = container.querySelector('[name$=".' + fieldName + '"]');
        if (el) return el;

        // ID-based match for Invoice Edit forms (e.g., newLine_unitPrice, editLine_unitPrice_123)
        var lowerName = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
        var inputs = container.querySelectorAll('input, select');
        for (var i = 0; i < inputs.length; i++) {
            var id = inputs[i].id || '';
            var name = inputs[i].name || '';
            if (id.toLowerCase().indexOf(lowerName.toLowerCase()) !== -1) return inputs[i];
            if (name.toLowerCase().indexOf(lowerName.toLowerCase()) !== -1) return inputs[i];
        }

        return null;
    }

    // --- Input Detection ---

    /**
     * Determines if an input is a Description field on a line item form.
     */
    function isLineItemDescriptionField(input) {
        if (input.tagName !== 'INPUT' || input.type !== 'text') return false;

        var name = (input.name || '').toLowerCase();
        var id = (input.id || '').toLowerCase();

        // Match name="Description" or name="lines[x].Description"
        if (name === 'description' || name.endsWith('.description')) return true;

        // Match id patterns like "newLine_description", "editLine_description_123"
        if (id.indexOf('description') !== -1) return true;

        return false;
    }

    /**
     * Determines if an input is on an Invoice or Quotation line item form.
     */
    function isOnLineItemForm(input) {
        var form = input.closest('form');
        if (form) {
            var action = (form.getAttribute('action') || '').toLowerCase();
            var aspAction = (form.getAttribute('asp-action') || '').toLowerCase();

            // Quotation line item forms
            if (action.indexOf('/addline') !== -1 || action.indexOf('/updateline') !== -1) return true;
            if (action.indexOf('/quotation/') !== -1) return true;

            // Invoice forms
            if (action.indexOf('/invoice/') !== -1) return true;
            if (aspAction === 'create' || aspAction === 'addline' || aspAction === 'updateline') return true;
        }

        // Invoice Create: lines are in #lineItemsContainer within #invoiceForm
        var invoiceForm = input.closest('#invoiceForm');
        if (invoiceForm) return true;

        // Invoice Edit: add line form or edit line form
        var addLineForm = input.closest('#addLineForm');
        if (addLineForm) return true;

        var editRow = input.closest('[id^="edit-row-"]');
        if (editRow) return true;

        // Check if inside a line-card or surface card-pad that looks like a line item
        var container = input.closest('.line-card') || input.closest('.surface.card-pad');
        if (container) {
            var hasUnitPrice = container.querySelector('[name="UnitPrice"], [name$=".UnitPrice"], [id*="unitPrice"], [id*="UnitPrice"]');
            if (hasUnitPrice) return true;
        }

        return false;
    }

    // --- API Call ---

    async function fetchAutocomplete(query, input) {
        if (!query || query.length < MIN_QUERY_LENGTH) {
            var dropdown = input.closest('.field')
                ? input.closest('.field').querySelector('.product-autocomplete-dropdown')
                : null;
            if (!dropdown) dropdown = input.parentElement.querySelector('.product-autocomplete-dropdown');
            if (dropdown) hideDropdown(dropdown);
            return;
        }

        try {
            var response = await fetch(AUTOCOMPLETE_URL + '?query=' + encodeURIComponent(query));
            if (!response.ok) return;
            var results = await response.json();
            var dropdown = createDropdown(input);
            renderResults(dropdown, results, input);
        } catch (e) {
            // Suppress all errors gracefully — autocomplete is supplementary
        }
    }

    // --- Event Handlers (Delegated) ---

    // Input handler with debounce
    document.addEventListener('input', function (e) {
        var input = e.target;
        if (!isLineItemDescriptionField(input)) return;
        if (!isOnLineItemForm(input)) return;

        clearTimeout(debounceTimer);
        var query = input.value.trim();

        if (query.length < MIN_QUERY_LENGTH) {
            var field = input.closest('.field') || input.parentElement;
            var dropdown = field.querySelector('.product-autocomplete-dropdown');
            if (dropdown) hideDropdown(dropdown);
            return;
        }

        debounceTimer = setTimeout(function () {
            fetchAutocomplete(query, input);
        }, DEBOUNCE_MS);
    });

    // Hide dropdown on blur (with delay for mousedown to fire first)
    document.addEventListener('focusout', function (e) {
        var input = e.target;
        if (!isLineItemDescriptionField(input)) return;

        setTimeout(function () {
            var field = input.closest('.field') || input.parentElement;
            var dropdown = field.querySelector('.product-autocomplete-dropdown');
            if (dropdown) hideDropdown(dropdown);
        }, 200);
    });

    // Show dropdown on focus if there's already content
    document.addEventListener('focusin', function (e) {
        var input = e.target;
        if (!isLineItemDescriptionField(input)) return;
        if (!isOnLineItemForm(input)) return;

        var query = input.value.trim();
        if (query.length >= MIN_QUERY_LENGTH) {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(function () {
                fetchAutocomplete(query, input);
            }, DEBOUNCE_MS);
        }
    });

    // Keyboard navigation
    document.addEventListener('keydown', function (e) {
        if (!activeDropdown || !activeDropdown.classList.contains('active')) return;
        if (!isLineItemDescriptionField(e.target)) return;

        var items = activeDropdown.querySelectorAll('.product-autocomplete-item');
        if (items.length === 0) return;

        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
            items.forEach(function (it, i) { it.classList.toggle('selected', i === selectedIndex); });
            items[selectedIndex].scrollIntoView({ block: 'nearest' });
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selectedIndex = Math.max(selectedIndex - 1, 0);
            items.forEach(function (it, i) { it.classList.toggle('selected', i === selectedIndex); });
            items[selectedIndex].scrollIntoView({ block: 'nearest' });
        } else if (e.key === 'Enter' && selectedIndex >= 0) {
            e.preventDefault();
            items[selectedIndex].dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
        } else if (e.key === 'Escape') {
            hideDropdown(activeDropdown);
        }
    });

    // Close dropdown when clicking outside
    document.addEventListener('click', function (e) {
        if (!activeDropdown) return;
        if (!e.target.closest('.product-autocomplete-dropdown') && !isLineItemDescriptionField(e.target)) {
            hideAllDropdowns();
        }
    });

})();
