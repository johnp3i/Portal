/* ==========================================================================
   Invoice Create — Line Items (client-side modal collection engine)

   The Create page has no persisted invoice yet, so unlike the Edit page
   (which AJAX-posts each line to /Invoice/AddLine and reloads), this engine:

     1. Opens the shared-look line modal (_CreateInvoiceLineModal.cshtml).
     2. Collects the entered line into an in-memory array.
     3. Renders a server-style .line-item-table row (with data-* attributes,
        so Edit re-opens the modal pre-filled, mirroring the Edit page).
     4. Maintains hidden lines[idx].* inputs inside #invoiceForm so the existing
        [HttpPost] Create action binds List<CreateInvoiceLineDto> unchanged.

   No controller changes: the field set matches CreateInvoiceLineDto exactly.
   ========================================================================== */
(function () {
    'use strict';

    // In-memory collection of lines. Each entry has a stable numeric `key` used
    // for DOM wiring; the submitted lines[idx] index is derived from array order.
    var lines = [];
    var nextKey = 1;
    var mode = null;        // 'add' | 'edit'
    var editingKey = null;  // key being edited

    // Per-open tier state (mirrors the old per-card tier logic, now modal-scoped).
    var modalTiers = [];
    var modalCurrency = '\u20ac';

    // ---- Small helpers --------------------------------------------------------

    function $(id) { return document.getElementById(id); }

    function esc(str) {
        if (str == null) return '';
        var d = document.createElement('div');
        d.textContent = String(str);
        return d.innerHTML;
    }

    function num(v, fallback) {
        var n = parseFloat(v);
        return isNaN(n) ? (fallback === undefined ? 0 : fallback) : n;
    }

    function fmt(v) { return num(v).toFixed(2); }

    // ---- Modal open / close ---------------------------------------------------

    function resetModalFields() {
        $('createLineModalProductCode').value = '';
        $('createLineModalProductPriceTierId').value = '';
        $('createLineModalDescription').value = '';
        $('createLineModalSubtitleField').value = '';
        $('createLineModalReferenceUrl').value = '';
        $('createLineModalQuantity').value = '1';
        $('createLineModalUnitPrice').value = '';
        $('createLineModalVatRate').value = '';
        $('createLineModalCostPrice').value = '';
        $('createLineModalDiscount').value = '0';
        $('createLineModalDiscountType').value = 'Percentage';

        var rc = $('createLineModalReverseCharge');
        rc.checked = false;
        var vat = $('createLineModalVatRate');
        vat.readOnly = false;
        vat.style.opacity = '1';
        delete vat.dataset.previousVatRate;

        hideModalTiers();
        collapseAdvanced();
        clearValidation();
    }

    function collapseAdvanced() {
        var content = $('createLineAdvancedContent');
        if (content) content.style.display = 'none';
        var toggle = document.querySelector('#createLineModal .advanced-toggle');
        if (toggle) toggle.classList.remove('expanded');
    }

    function clearValidation() {
        var errs = document.querySelectorAll('#createLineItemForm .field-validation-error');
        errs.forEach(function (e) { e.remove(); });
    }

    window.showAddCreateLineModal = function () {
        mode = 'add';
        editingKey = null;
        resetModalFields();
        $('createLineModalTitle').textContent = 'Add Line Item';
        $('createLineModalSubtitle').textContent = 'Add a new line item to this invoice.';
        var btn = $('createLineModalSubmitBtn');
        btn.textContent = 'Add Line';
        btn.className = 'btn btn-success';
        $('createLineModal').style.display = 'flex';
        $('createLineModalDescription').focus();
    };

    window.showEditCreateLineModal = function (key) {
        var line = lines.find(function (l) { return l.key === key; });
        if (!line) return;

        mode = 'edit';
        editingKey = key;
        resetModalFields();

        $('createLineModalProductCode').value = line.productCode || '';
        $('createLineModalProductPriceTierId').value = line.productPriceTierId || '';
        $('createLineModalDescription').value = line.description || '';
        $('createLineModalSubtitleField').value = line.subtitle || '';
        $('createLineModalReferenceUrl').value = line.referenceUrl || '';
        $('createLineModalQuantity').value = line.quantity;
        $('createLineModalUnitPrice').value = line.unitPrice;
        $('createLineModalVatRate').value = line.vatRate;
        $('createLineModalCostPrice').value = line.costPrice;
        $('createLineModalDiscount').value = line.discount;
        $('createLineModalDiscountType').value = line.discountType || 'Percentage';

        var rc = $('createLineModalReverseCharge');
        var vat = $('createLineModalVatRate');
        if (line.isReverseCharge) {
            rc.checked = true;
            vat.dataset.previousVatRate = line.vatRate;
            vat.value = '0';
            vat.readOnly = true;
            vat.style.opacity = '0.6';
        }

        $('createLineModalTitle').textContent = 'Edit Line Item';
        $('createLineModalSubtitle').textContent = 'Update the details for this line item.';
        var btn = $('createLineModalSubmitBtn');
        btn.textContent = 'Save Changes';
        btn.className = 'btn btn-primary';
        $('createLineModal').style.display = 'flex';
        $('createLineModalDescription').focus();
    };

    window.hideCreateLineModal = function () {
        $('createLineModal').style.display = 'none';
        mode = null;
        editingKey = null;
    };

    window.toggleCreateLineAdvanced = function () {
        var content = $('createLineAdvancedContent');
        var toggle = document.querySelector('#createLineModal .advanced-toggle');
        if (content.style.display === 'none') {
            content.style.display = 'block';
            if (toggle) toggle.classList.add('expanded');
        } else {
            content.style.display = 'none';
            if (toggle) toggle.classList.remove('expanded');
        }
    };

    // ---- Validation + submit --------------------------------------------------

    function showFieldError(input, message) {
        var existing = input.parentElement.querySelector('.field-validation-error');
        if (existing) existing.remove();
        var span = document.createElement('span');
        span.className = 'field-validation-error';
        span.style.color = '#C24A4A';
        span.style.fontSize = '12px';
        span.style.display = 'block';
        span.style.marginTop = '4px';
        span.textContent = message;
        input.parentElement.appendChild(span);
        input.focus();
    }

    window.submitCreateLineModal = function () {
        clearValidation();

        var descInput = $('createLineModalDescription');
        var description = descInput.value.trim();
        if (!description) {
            showFieldError(descInput, 'Description is required.');
            return;
        }

        var qtyInput = $('createLineModalQuantity');
        var quantity = parseFloat(qtyInput.value);
        if (isNaN(quantity) || quantity <= 0) {
            showFieldError(qtyInput, 'Quantity must be greater than zero.');
            return;
        }

        var data = {
            description: description,
            subtitle: $('createLineModalSubtitleField').value.trim(),
            referenceUrl: $('createLineModalReferenceUrl').value.trim(),
            quantity: quantity,
            unitPrice: num($('createLineModalUnitPrice').value, 0),
            vatRate: num($('createLineModalVatRate').value, 0),
            discount: num($('createLineModalDiscount').value, 0),
            discountType: $('createLineModalDiscountType').value || 'Percentage',
            costPrice: $('createLineModalCostPrice').value.trim(),
            productCode: $('createLineModalProductCode').value || '',
            productPriceTierId: $('createLineModalProductPriceTierId').value || '',
            isReverseCharge: $('createLineModalReverseCharge').checked
        };

        if (mode === 'edit' && editingKey != null) {
            var line = lines.find(function (l) { return l.key === editingKey; });
            if (line) Object.assign(line, data);
        } else {
            data.key = nextKey++;
            lines.push(data);
        }

        render();
        hideCreateLineModal();
    };

    window.removeCreateLine = async function (key) {
        var result = await Swal.fire({
            title: 'Remove this line item?',
            text: 'This action cannot be undone.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#C24A4A',
            cancelButtonColor: '#6b7c8d',
            confirmButtonText: 'Yes, remove it',
            cancelButtonText: 'Cancel'
        });
        if (!result.isConfirmed) return;

        lines = lines.filter(function (l) { return l.key !== key; });
        render();
    };

    // ---- Rendering: table rows + hidden lines[idx].* inputs -------------------

    function discountAmount(line) {
        return line.discountType === 'Percentage'
            ? (num(line.quantity) * num(line.unitPrice) * num(line.discount) / 100)
            : num(line.discount);
    }

    function render() {
        renderTable();
        renderHiddenInputs();
        updateCount();
    }

    function updateCount() {
        var countEl = $('createLineItemsCount');
        if (countEl) {
            countEl.textContent = '· ' + lines.length + ' item' + (lines.length !== 1 ? 's' : '');
        }
    }

    function renderTable() {
        var tableWrap = $('createLineItemsTableWrap');
        var emptyState = $('createLineItemsEmptyState');
        var addBtn = $('createAddLineBtn');

        if (lines.length === 0) {
            // Empty state has its own primary CTA, so hide the secondary dashed button.
            // Use 'flex' (not 'block') so the column-centering layout is preserved.
            if (tableWrap) tableWrap.style.display = 'none';
            if (emptyState) emptyState.style.display = 'flex';
            if (addBtn) addBtn.style.display = 'none';
            return;
        }

        if (emptyState) emptyState.style.display = 'none';
        if (tableWrap) tableWrap.style.display = 'block';
        if (addBtn) addBtn.style.display = 'inline-flex';

        var tbody = $('createLineItemsTableBody');
        var rows = '';
        lines.forEach(function (line, i) {
            var subtotal = num(line.quantity) * num(line.unitPrice);
            var da = discountAmount(line);
            var lineNet = subtotal - da;
            var total = lineNet + lineNet * num(line.vatRate) / 100;
            rows += '<tr data-key="' + line.key + '">'
                + '<td class="col-row-num"><span class="muted">' + (i + 1) + '</span></td>'
                + '<td class="col-description"><span class="line-desc">' + esc(line.description) + '</span>'
                + (line.subtitle ? '<span class="line-subtitle">' + esc(line.subtitle) + '</span>' : '')
                + '</td>'
                + '<td class="col-qty">' + num(line.quantity).toFixed(2) + '</td>'
                + '<td class="col-unit-price">\u20ac' + fmt(line.unitPrice) + '</td>'
                + '<td class="col-subtotal" style="font-weight:600;color:#0D5EA6;">\u20ac' + subtotal.toFixed(2) + '</td>'
                + '<td class="col-vat">' + num(line.vatRate).toFixed(2) + '%</td>'
                + '<td class="col-discount">' + (num(line.discount) > 0
                    ? '<span class="discount-green">-\u20ac' + da.toFixed(2) + '</span>'
                    : '<span>-</span>') + '</td>'
                + '<td class="col-total"><strong>\u20ac' + total.toFixed(2) + '</strong></td>'
                + '<td class="col-actions">'
                + '<button type="button" class="btn-edit" onclick="showEditCreateLineModal(' + line.key + ')">Edit</button> '
                + '<button type="button" class="btn-remove" onclick="removeCreateLine(' + line.key + ')">\u00d7</button>'
                + '</td>'
                + '</tr>';
        });
        tbody.innerHTML = rows;
    }

    // Build hidden inputs named lines[idx].Field so the Create action binds
    // List<CreateInvoiceLineDto>. Rebuilt from scratch on every change to keep
    // indices contiguous (MVC collection binding requires 0..n-1 with no gaps).
    function renderHiddenInputs() {
        var container = $('createLineHiddenInputs');
        var html = '';
        lines.forEach(function (line, idx) {
            var p = 'lines[' + idx + '].';
            html += hidden(p + 'Description', line.description);
            html += hidden(p + 'Subtitle', line.subtitle);
            html += hidden(p + 'ReferenceUrl', line.referenceUrl);
            html += hidden(p + 'Quantity', line.quantity);
            html += hidden(p + 'UnitPrice', line.unitPrice);
            html += hidden(p + 'VatRate', line.vatRate);
            html += hidden(p + 'Discount', line.discount);
            html += hidden(p + 'DiscountType', line.discountType);
            if (line.costPrice !== '' && line.costPrice != null) {
                html += hidden(p + 'CostPrice', line.costPrice);
            }
            if (line.productCode) html += hidden(p + 'ProductCode', line.productCode);
            if (line.productPriceTierId) html += hidden(p + 'ProductPriceTierId', line.productPriceTierId);
            // Bind bool: emit value only when true (CreateInvoiceLineDto.IsReverseCharge).
            html += hidden(p + 'IsReverseCharge', line.isReverseCharge ? 'true' : 'false');
        });
        container.innerHTML = html;
    }

    function hidden(name, value) {
        return '<input type="hidden" name="' + name + '" value="' + esc(value) + '" />';
    }

    // Expose a read of the current line count for the Bulk Discount info gate.
    window.getCreateLineCount = function () { return lines.length; };

    // ---- Reverse charge VAT lock (modal-scoped) -------------------------------

    function wireReverseCharge() {
        var rc = $('createLineModalReverseCharge');
        if (!rc) return;
        rc.addEventListener('change', function () {
            var vat = $('createLineModalVatRate');
            if (this.checked) {
                vat.dataset.previousVatRate = vat.value;
                vat.value = '0';
                vat.readOnly = true;
                vat.style.opacity = '0.6';
            } else {
                vat.value = vat.dataset.previousVatRate || '';
                vat.readOnly = false;
                vat.style.opacity = '1';
            }
        });
    }

    // ---- Product tier selection (modal-scoped) --------------------------------
    // Driven by product-autocomplete.js via the window.onProductAutocompleteSelected
    // hook: when a catalog product is picked in the modal's Description field, we
    // fetch its price tiers and show the tier selector.

    window.onProductAutocompleteSelected = function (container, entry) {
        // Only react when the selection happened inside the Create line modal.
        if (!container || !container.querySelector || !container.querySelector('#createLineModalDescription')) {
            return;
        }
        if (entry && entry.productCode) {
            fetchModalTiers(entry.productCode, null);
        } else if (entry && entry.description) {
            fetchModalTiers(null, entry.description);
        } else {
            hideModalTiers();
        }
    };

    async function fetchModalTiers(productCode, description) {
        hideModalTiers();
        var query = '';
        if (productCode) query = 'productCode=' + encodeURIComponent(productCode);
        else if (description) query = 'description=' + encodeURIComponent(description);
        else return;

        try {
            var response = await fetch('/Invoice/AxGetProductTiersForSelection?' + query);
            if (!response.ok) return;
            var result = await response.json();
            if (!result.success || !result.data || !result.data.hasTiers) return;

            var data = result.data;
            modalTiers = data.tiers || [];
            modalCurrency = data.currencySymbol || '\u20ac';
            if (modalTiers.length === 0) return;

            var select = $('createLineModalPriceTier');
            select.innerHTML = '';
            modalTiers.forEach(function (tier) {
                var opt = document.createElement('option');
                opt.value = tier.id.toString();
                opt.textContent = tier.tierName + ' \u2014 ' + modalCurrency + parseFloat(tier.sellingPrice).toFixed(2);
                if (tier.id === data.defaultTierId) opt.selected = true;
                select.appendChild(opt);
            });

            $('createLineTierSelectorRow').style.display = '';

            if (data.defaultTierId) {
                select.value = data.defaultTierId.toString();
                applyModalTier(data.defaultTierId);
            }
        } catch (e) { /* tiers are supplementary — fail silently */ }
    }

    window.onCreateModalTierChange = function () {
        var select = $('createLineModalPriceTier');
        var tierId = parseInt(select.value);
        if (isNaN(tierId)) {
            $('createLineModalProductPriceTierId').value = '';
            return;
        }
        applyModalTier(tierId);
    };

    function applyModalTier(tierId) {
        var tier = modalTiers.find(function (t) { return t.id === tierId; });
        if (!tier) return;

        $('createLineModalUnitPrice').value = parseFloat(tier.sellingPrice).toFixed(2);
        $('createLineModalCostPrice').value = tier.costPrice > 0 ? parseFloat(tier.costPrice).toFixed(2) : '';
        $('createLineModalProductPriceTierId').value = tierId.toString();

        var box = $('createLineTierHighlight');
        $('createLineTierHighlightUnit').textContent = modalCurrency + parseFloat(tier.sellingPrice).toFixed(2);
        $('createLineTierHighlightCost').textContent = tier.costPrice > 0 ? modalCurrency + parseFloat(tier.costPrice).toFixed(2) : '\u2014';
        if (box) box.style.display = 'flex';
    }

    function hideModalTiers() {
        var row = $('createLineTierSelectorRow');
        if (row) row.style.display = 'none';
        var select = $('createLineModalPriceTier');
        if (select) select.innerHTML = '';
        var box = $('createLineTierHighlight');
        if (box) box.style.display = 'none';
        modalTiers = [];
    }

    // ---- Init -----------------------------------------------------------------

    document.addEventListener('DOMContentLoaded', function () {
        wireReverseCharge();

        // Escape closes the modal (intentional keypress, low accidental-loss risk).
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                var modal = $('createLineModal');
                if (modal && modal.style.display !== 'none') hideCreateLineModal();
            }
        });

        // Clear inline validation as the user corrects fields.
        var descInput = $('createLineModalDescription');
        if (descInput) {
            descInput.addEventListener('input', function () {
                var e = descInput.parentElement.querySelector('.field-validation-error');
                if (e) e.remove();
            });
        }
        var qtyInput = $('createLineModalQuantity');
        if (qtyInput) {
            qtyInput.addEventListener('input', function () {
                var e = qtyInput.parentElement.querySelector('.field-validation-error');
                if (e) e.remove();
            });
        }

        // Start with the empty state (no auto-added blank line — the modal replaces it).
        render();
    });

})();
