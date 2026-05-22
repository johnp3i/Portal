using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for supplier management.
/// </summary>
public interface ISupplierService
{
    Task<List<Supplier>> GetSuppliersAsync();
    Task<List<Supplier>> GetActiveSuppliersAsync();
    Task<Supplier?> GetSupplierByIdAsync(int id);
    Task<ServiceResult> CreateSupplierAsync(Supplier supplier);
    Task<ServiceResult> UpdateSupplierAsync(Supplier supplier);
    Task<ServiceResult> DeactivateSupplierAsync(int id);
}
