using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for invoice sharing — generating secure share links,
/// rendering HTML snapshots, managing share lifecycle, and email notifications.
/// </summary>
public interface IInvoiceSharingService
{
    Task<InvoiceShare> ShareAsync(int invoiceId, DateTimeOffset expiresAtUtc, bool sendEmail, string userId, string? recipientEmail = null);
    Task<InvoiceShare?> GetByTokenAsync(string token);
    Task<InvoiceShare?> GetActiveShareByInvoiceIdAsync(int invoiceId);
    Task<List<InvoiceShare>> GetSharesByBusinessIdAsync(int businessId);
    Task CancelShareAsync(int shareId);
    Task ReactivateShareAsync(int shareId);
}
