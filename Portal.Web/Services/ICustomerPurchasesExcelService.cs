using Portal.Infrastructure.Models;

namespace Portal.Web.Services;

public interface ICustomerPurchasesExcelService
{
    /// <summary>
    /// Builds the purchases workbook. Returns null when the customer is not found for the
    /// current tenant (the tenant check happens inside the dashboard service).
    /// </summary>
    Task<(byte[] Content, string FileName)?> GenerateAsync(int customerId, PurchasesQuery query, CancellationToken ct);
}
