using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for supplier management.
/// </summary>
public class SupplierService : ISupplierService
{
    private readonly SupplierRepository _supplierRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;

    public SupplierService(
        SupplierRepository supplierRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService)
    {
        _supplierRepository = supplierRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
    }

    public async Task<List<Supplier>> GetSuppliersAsync()
    {
        return await _supplierRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
    }

    public async Task<List<Supplier>> GetActiveSuppliersAsync()
    {
        var suppliers = await _supplierRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
        return suppliers.Where(s => s.IsActive).ToList();
    }

    public async Task<Supplier?> GetSupplierByIdAsync(int id)
    {
        return await _supplierRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<ServiceResult> CreateSupplierAsync(Supplier supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            return ServiceResult.Fail("Supplier name is required.");
        }

        supplier.BusinessId = _currentTenantService.CurrentBusinessId;
        supplier.IsActive = true;
        supplier.CreatedAtUtc = DateTime.UtcNow;

        var newId = await _supplierRepository.InsertAsync(supplier);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Create",
            TableName = "purchase.Supplier",
            RecordId = newId.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok(newId);
    }

    public async Task<ServiceResult> UpdateSupplierAsync(Supplier supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            return ServiceResult.Fail("Supplier name is required.");
        }

        var existing = await _supplierRepository.GetByIdAndBusinessIdAsync(supplier.Id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Supplier not found.");
        }

        supplier.BusinessId = _currentTenantService.CurrentBusinessId;
        await _supplierRepository.UpdateAsync(supplier);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeactivateSupplierAsync(int id)
    {
        var existing = await _supplierRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Supplier not found.");
        }

        await _supplierRepository.DeactivateAsync(id, _currentTenantService.CurrentBusinessId);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Deactivate",
            TableName = "purchase.Supplier",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }
}
