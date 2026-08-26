/**
 * Bulk Discount Modal — Shared JS for Invoice and Quotation edit views.
 * Handles modal open/close, type toggling, live preview, and submission.
 */
(function () {
    'use strict';

    var _bdContext = {
        subtotal: 0,
        netAmount: 0,
        currencySymbol: '€',
        applyUrl: '',
        removeUrl: '',
        documentId: 0,
        documentIdParam: 'invoiceId',
        currentType: 'Percentage'
    };

    window.openBulkDiscountModal = function (subtotal, netAmount, currencySymbol, applyUrl, removeUrl, documentId, documentIdParam) {
        _bdContext.subtotal = subtotal;
        _bdContext.netAmount = netAmount;
        _bdContext.currencySymbol = currencySymbol || '€';
        _bdContext.applyUrl = applyUrl;
        _bdContext.removeUrl = removeUrl;
        _bdContext.documentId = documentId;
        _bdContext.documentIdParam = documentIdParam || 'invoiceId';
        _bdContext.currentType = 'Percentage';

        // Reset UI
        document.getElementById('bdValue').value = '';
        document.getElementById('bdValidation').style.display = 'none';
        document.getElementById('bdPreview').style.display = 'none';
        document.getElementById('bdConfirmBtn').disabled = true;
        setBulkDiscountType('Percentage');

        document.getElementById('bulkDiscountModal').style.display = 'flex';
    };

    window.closeBulkDiscountModal = function () {
        document.getElementById('bulkDiscountModal').style.display = 'none';
    };

    window.setBulkDiscountType = function (type) {
        _bdContext.currentType = type;
        var pctBtn = document.getElementById('bdTypePercentage');
        var fixBtn = document.getElementById('bdTypeFixed');
        var input = document.getElementById('bdValue');
        var label = document.getElementById('bdValueLabel');

        if (type === 'Percentage') {
            pctBtn.className = 'btn btn-primary';
            fixBtn.className = 'btn btn-secondary';
            input.max = '100';
            input.placeholder = 'e.g. 15';
            label.textContent = 'Discount Percentage';
        } else {
            pctBtn.className = 'btn btn-secondary';
            fixBtn.className = 'btn btn-primary';
            input.max = _bdContext.netAmount.toString();
            input.placeholder = 'e.g. ' + _bdContext.netAmount.toFixed(2);
            label.textContent = 'Discount Amount (' + _bdContext.currencySymbol + ')';
        }

        input.value = '';
        document.getElementById('bdPreview').style.display = 'none';
        document.getElementById('bdValidation').style.display = 'none';
        document.getElementById('bdConfirmBtn').disabled = true;
    };

    window.updateBulkDiscountPreview = function () {
        var value = parseFloat(document.getElementById('bdValue').value);
        var validation = document.getElementById('bdValidation');
        var preview = document.getElementById('bdPreview');
        var previewAmount = document.getElementById('bdPreviewAmount');
        var confirmBtn = document.getElementById('bdConfirmBtn');

        if (isNaN(value) || value <= 0) {
            validation.textContent = 'Please enter a valid positive value.';
            validation.style.display = 'block';
            preview.style.display = 'none';
            confirmBtn.disabled = true;
            return;
        }

        if (_bdContext.currentType === 'Percentage') {
            if (value > 100) {
                validation.textContent = 'Percentage cannot exceed 100%.';
                validation.style.display = 'block';
                preview.style.display = 'none';
                confirmBtn.disabled = true;
                return;
            }
            var discountAmount = Math.round(_bdContext.subtotal * value / 100 * 100) / 100;
            previewAmount.textContent = '-' + _bdContext.currencySymbol + discountAmount.toFixed(2);
        } else {
            if (value > _bdContext.netAmount) {
                validation.textContent = 'Amount cannot exceed the net amount (' + _bdContext.currencySymbol + _bdContext.netAmount.toFixed(2) + ').';
                validation.style.display = 'block';
                preview.style.display = 'none';
                confirmBtn.disabled = true;
                return;
            }
            previewAmount.textContent = '-' + _bdContext.currencySymbol + value.toFixed(2);
        }

        validation.style.display = 'none';
        preview.style.display = 'block';
        confirmBtn.disabled = false;
    };

    window.confirmBulkDiscount = function () {
        var value = parseFloat(document.getElementById('bdValue').value);
        if (isNaN(value) || value <= 0) return;

        var params = new URLSearchParams();
        params.append(_bdContext.documentIdParam, _bdContext.documentId);
        params.append('discountType', _bdContext.currentType);
        params.append('discountValue', value);

        BlockUI.show('Applying discount...');
        fetch(_bdContext.applyUrl + '?' + params.toString(), {
            method: 'POST',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            BlockUI.hide();
            closeBulkDiscountModal();
            if (data.success) {
                Swal.fire({ icon: 'success', title: 'Discount Applied', text: 'Bulk discount has been applied.', confirmButtonColor: '#0D5EA6' }).then(function () {
                    window.location.reload();
                });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
            }
        })
        .catch(function () {
            BlockUI.hide();
            closeBulkDiscountModal();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        });
    };

    window.removeBulkDiscount = function (removeUrl, documentId, documentIdParam) {
        // Use passed params if available, otherwise fall back to modal context
        var url = removeUrl || _bdContext.removeUrl;
        var docId = documentId || _bdContext.documentId;
        var docIdParam = documentIdParam || _bdContext.documentIdParam;

        Swal.fire({
            title: 'Remove Discount?',
            text: 'This will remove the document-level discount.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, remove',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#C24A4A',
            cancelButtonColor: '#5E7385'
        }).then(function (result) {
            if (!result.isConfirmed) return;

            var params = new URLSearchParams();
            params.append(docIdParam, docId);

            BlockUI.show('Removing discount...');
            fetch(url + '?' + params.toString(), {
                method: 'POST',
                headers: { 'RequestVerificationToken': getAntiForgeryToken() }
            })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                BlockUI.hide();
                if (data.success) {
                    Swal.fire({ icon: 'success', title: 'Removed', text: 'Bulk discount has been removed.', confirmButtonColor: '#0D5EA6' }).then(function () {
                        window.location.reload();
                    });
                } else {
                    Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' });
                }
            })
            .catch(function () {
                BlockUI.hide();
                Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
            });
        });
    };

    // Update DOM totals breakdown after apply/remove
    window.updateTotalsBreakdown = function (totals) {
        if (!totals) return;
        var el = function(id) { return document.getElementById(id); };
        if (el('totalsGrossSubtotal')) el('totalsGrossSubtotal').textContent = totals.grossSubtotal.toFixed(2);
        if (el('totalsLineDiscounts')) {
            el('totalsLineDiscounts').textContent = '-' + totals.lineDiscounts.toFixed(2);
            el('totalsLineDiscountsRow').style.display = totals.hasLineDiscounts ? 'flex' : 'none';
        }
        if (el('totalsNetSubtotal')) el('totalsNetSubtotal').textContent = totals.netSubtotal.toFixed(2);
        if (el('totalsInvoiceDiscount')) {
            el('totalsInvoiceDiscount').textContent = '-' + totals.invoiceDiscount.toFixed(2);
            el('totalsInvoiceDiscountRow').style.display = totals.hasInvoiceDiscount ? 'flex' : 'none';
        }
        if (el('totalsNetAmount')) el('totalsNetAmount').textContent = totals.netAmount.toFixed(2);
        if (el('totalsVat')) el('totalsVat').textContent = totals.vat.toFixed(2);
        if (el('totalsTotal')) el('totalsTotal').textContent = totals.total.toFixed(2);
        // Show/hide remove button
        if (el('removeBulkDiscountBtn')) {
            el('removeBulkDiscountBtn').style.display = totals.hasInvoiceDiscount ? '' : 'none';
        }
        // Show/hide adjustment line banner
        if (el('adjustmentLineBanner')) {
            el('adjustmentLineBanner').style.display = totals.hasInvoiceDiscount ? '' : 'none';
        }
        // Show/hide bulk discount button context
        if (el('bulkDiscountActionBtn')) {
            el('bulkDiscountActionBtn').style.display = '';
        }
        // Update modal context with new values so reopening uses fresh data
        _bdContext.subtotal = totals.netSubtotal;
        _bdContext.netAmount = totals.netAmount;
    };

    window.refreshBulkDiscountContext = function (netSubtotal) {
        _bdContext.subtotal = netSubtotal;
        _bdContext.netAmount = netSubtotal;
        // Update the button's onclick attribute with new values
        var btn = document.getElementById('bulkDiscountActionBtn');
        if (btn) {
            var currentOnclick = btn.getAttribute('onclick') || '';
            var updated = currentOnclick.replace(
                /openBulkDiscountModal\(\s*[\d.]+\s*,\s*[\d.]+/,
                'openBulkDiscountModal(' + netSubtotal.toFixed(2) + ', ' + netSubtotal.toFixed(2)
            );
            btn.setAttribute('onclick', updated);
        }
    };

    function getAntiForgeryToken() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }
})();
