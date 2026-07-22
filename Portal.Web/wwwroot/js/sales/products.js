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
        document.getElementById('productModalTitle').textContent = 'New Product';
        document.getElementById('productId').value = '';
        document.getElementById('productName').value = '';
        document.getElementById('productDescription').value = '';
        document.getElementById('productModal').style.display = 'flex';
    };

    window.editProduct = function (id, name, description) {
        document.getElementById('productModalTitle').textContent = 'Edit Product';
        document.getElementById('productId').value = id;
        document.getElementById('productName').value = name;
        document.getElementById('productDescription').value = description || '';
        document.getElementById('productModal').style.display = 'flex';
    };

    window.closeProductModal = function () {
        document.getElementById('productModal').style.display = 'none';
    };

    window.submitProduct = async function () {
        var id = document.getElementById('productId').value;
        var payload = {
            name: document.getElementById('productName').value,
            description: document.getElementById('productDescription').value || null
        };

        if (!payload.name) {
            Swal.fire({ icon: 'warning', title: 'Validation', text: 'Product name is required.', confirmButtonColor: '#0D5EA6' });
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
                Swal.fire({ icon: 'success', title: 'Saved', text: 'Product saved successfully.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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
            title: 'Deactivate Product?',
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
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Product deactivated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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
            title: 'Activate Product?',
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
                        Swal.fire({ icon: 'success', title: 'Done', text: 'Product activated.', confirmButtonColor: '#0D5EA6' }).then(function () { window.location.reload(); });
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
