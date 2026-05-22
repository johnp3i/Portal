using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for invoice sharing — generating secure share links,
/// rendering HTML snapshots, managing share lifecycle, and email notifications.
/// </summary>
public class InvoiceSharingService : IInvoiceSharingService
{
    private readonly IInvoiceRenderer _invoiceRenderer;
    private readonly InvoiceShareRepository _shareRepository;
    private readonly IInvoiceService _invoiceService;
    private readonly ICustomerService _customerService;
    private readonly IEmailService _emailService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<InvoiceSharingService> _logger;

    public InvoiceSharingService(
        IInvoiceRenderer invoiceRenderer,
        InvoiceShareRepository shareRepository,
        IInvoiceService invoiceService,
        ICustomerService customerService,
        IEmailService emailService,
        ICurrentTenantService tenantService,
        ILogger<InvoiceSharingService> logger)
    {
        _invoiceRenderer = invoiceRenderer;
        _shareRepository = shareRepository;
        _invoiceService = invoiceService;
        _customerService = customerService;
        _emailService = emailService;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<InvoiceShare> ShareAsync(int invoiceId, DateTimeOffset expiresAtUtc, bool sendEmail, string userId, string? recipientEmail = null)
    {
        // Validate expiration date (must be at least 1 day in the future)
        if (expiresAtUtc <= DateTimeOffset.UtcNow.AddDays(1))
            throw new ArgumentException("Expiration date must be at least 1 day in the future.");

        var businessId = _tenantService.CurrentBusinessId;

        // Validate invoice exists and belongs to business
        var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId);
        if (invoice == null || invoice.BusinessId != businessId)
            throw new InvalidOperationException("Invoice not found.");

        // Validate customer exists
        var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId);
        if (customer == null)
            throw new InvalidOperationException("Customer not found.");

        // Use recipient email override if provided, otherwise fall back to customer email
        var emailToUse = !string.IsNullOrWhiteSpace(recipientEmail) ? recipientEmail : customer.Email;
        if (string.IsNullOrWhiteSpace(emailToUse))
            throw new ArgumentException("A recipient email is required for sharing an invoice.");

        // Generate 32-byte cryptographically secure URL-safe Base64 token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var shareToken = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        // Deactivate previous active share for same invoice
        await _shareRepository.DeactivateByInvoiceIdAsync(invoiceId);

        // Render HTML snapshot via IInvoiceRenderer
        var snapshotHtml = await _invoiceRenderer.RenderAsync(invoiceId);

        // Persist InvoiceShare record
        var share = new InvoiceShare
        {
            InvoiceId = invoiceId,
            BusinessId = businessId,
            ShareToken = shareToken,
            SnapshotHtml = snapshotHtml,
            CustomerEmail = emailToUse,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = userId,
            IsActive = true
        };

        await _shareRepository.InsertAsync(share);

        // Send email notification if requested (catch and log failures without rolling back share)
        if (sendEmail)
        {
            try
            {
                await _emailService.SendInvoiceEmailAsync(
                    emailToUse,
                    shareToken,
                    invoice.InvoiceNumber,
                    invoice.Business?.Name ?? string.Empty,
                    invoice.TotalAmount,
                    invoice.DueDate,
                    expiresAtUtc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invoice email for invoice {InvoiceId}", invoiceId);
            }
        }

        _logger.LogInformation("Invoice shared for invoice {InvoiceId} with token {Token}", invoiceId, shareToken);

        return share;
    }

    public async Task<InvoiceShare?> GetByTokenAsync(string token)
    {
        return await _shareRepository.GetByTokenAsync(token);
    }

    public async Task<InvoiceShare?> GetActiveShareByInvoiceIdAsync(int invoiceId)
    {
        return await _shareRepository.GetActiveByInvoiceIdAsync(invoiceId);
    }

    public async Task<List<InvoiceShare>> GetSharesByBusinessIdAsync(int businessId)
    {
        return await _shareRepository.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
    }

    public async Task CancelShareAsync(int shareId)
    {
        var businessId = _tenantService.CurrentBusinessId;
        await _shareRepository.DeactivateByIdAsync(shareId, businessId);
    }

    public async Task ReactivateShareAsync(int shareId)
    {
        var businessId = _tenantService.CurrentBusinessId;
        await _shareRepository.ReactivateByIdAsync(shareId, businessId);
    }
}
