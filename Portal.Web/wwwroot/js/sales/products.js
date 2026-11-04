/**
 * Sales Products — CRUD interactions
 */
(function () {
    'use strict';

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    window.openCreateProductModal = function () {
        document.getElementById('productModalTitle').textContent = 'New Product / Service';
        document.getElementById('productId').value = '';
        document.getElementById('productName').value = '';
        document.getElementById('productDescription').value = '';
        document.getElementById('productCatalogLink').value = '';
        document.getElementById('productModal').style.display = 'flex';
        loadCatalogProducts(null);
    };

    // Reads row data from the button's data-* attributes (safer than hand-escaped inline args).
    window.editProductFromData = function (btn) {
        var id = btn.getAttribute('data-id');
        var catalogProductId = btn.getAttribute('data-catalog-id') || '';
        document.getElementById('productModalTitle').textContent = 'Edit Product / Service';
        document.getElementById('productId').value = id;
        document.getElementById('productName').value = btn.getAttribute('data-name') || '';
        document.getElementById('productDescription').value = btn.getAttribute('data-description') || '';
        document.getElementById('productCatalogLink').value = catalogProductId;
        document.getElementById('productModal').style.display = 'flex';
        loadCatalogProducts(catalogProductId);
    };

    window.deactivateProductFromData = function (btn) {
        deactivateProduct(parseInt(btn.getAttribute('data-id')), btn.getAttribute('data-name') || '');
    };

    window.activateProductFromData = function (btn) {
        activateProduct(parseInt(btn.getAttribute('data-id')), btn.getAttribute('data-name') || '');
    };

    window.closeProductModal = function () {
        document.getElementById('productModal').style.display = 'none';
    };

    // Consistent modal dismissal: click the backdrop or press Escape to close.
    document.addEventListener('DOMContentLoaded', function () {
        var modal = document.getElementById('productModal');
        if (modal) {
            modal.addEventListener('click', function (e) { if (e.target === modal) closeProductModal(); });
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && modal && modal.style.display !== 'none') closeProductModal();
        });
    });

    window.submitProduct = async function () {
        var id = document.getElementById('productId').value;
        var catalogLinkValue = document.getElementById('productCatalogLink').value;
        var payload = {
            name: document.getElementById('productName').value,
            description: document.getElementById('productDescription').value || null,
            productId: catalogLinkValue ? parseInt(catalogLinkValue) : null
        };

        if (!payload.name) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Name is required.', confirmButtonColor: '#0D5EA6' });
            return;
        }

        var url = id ? '/Sales/AxPostUpdateProduct' : '/Sales/AxPostCreateProduct';
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
                closeProductModal();
                Swal.fire({ icon: 'success', title: 'Saved', text: 'Saved successfully.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
            } else {
                Swal.fire({ icon: 'error', title: 'Error', text: result.message, confirmButtonColor: '#0D5EA6' });
            }
        } catch (e) {
            BlockUI.hide();
            Swal.fire({ icon: 'error', title: 'Error', text: 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' });
        }
    };

    window.deactivateProduct = function (id, name) {
        Swal.fire({
            title: 'Deactivate?',
            text: 'Are you sure you want to deactivate "' + name + '"?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, deactivate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#C24A4A'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Deactivating...');
                try {
                    var response = await fetch('/Sales/AxPostDeactivateProduct?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Deactivated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    window.activateProduct = function (id, name) {
        Swal.fire({
            title: 'Activate?',
            text: 'Reactivate "' + name + '"?',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, activate',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#0D5EA6'
        }).then(async function (result) {
            if (result.isConfirmed) {
                BlockUI.show('Activating...');
                try {
                    var response = await fetch('/Sales/AxPostActivateProduct?id=' + id, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() }
                    });
                    var data = await response.json();
                    BlockUI.hide();

                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Activated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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

    async function loadCatalogProducts(preselectedId) {
        var select = document.getElementById('productCatalogLink');
        if (select.options.length > 1) {
            // Already loaded — just set selection
            if (preselectedId) select.value = preselectedId;
            return;
        }
        try {
            var response = await fetch('/Sales/AxGetCatalogProducts');
            var result = await response.json();
            if (result.success && result.data) {
                result.data.forEach(function(p) {
                    var opt = document.createElement('option');
                    opt.value = p.id;
                    opt.textContent = p.name + (p.productCode ? ' (' + p.productCode + ')' : '') + (p.sellingPrice ? ' — €' + p.sellingPrice.toFixed(2) : '');
                    select.appendChild(opt);
                });
                if (preselectedId) select.value = preselectedId;
            }
        } catch (e) { /* silent fail */ }
    }
})();
