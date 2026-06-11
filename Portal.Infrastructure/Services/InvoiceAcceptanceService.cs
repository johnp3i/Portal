using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for invoice acceptance — validating share state,
/// recording audit-trail acceptance, and enforcing one-per-share invariant.
/// </summary>
public class InvoiceAcceptanceService : IInvoiceAcceptanceService
{
    private readonly InvoiceAcceptanceRepository _acceptanceRepository;
    private readonly IInvoiceSharingService _sharingService;
    private readonly ILogger<InvoiceAcceptanceService> _logger;

    public InvoiceAcceptanceService(
        InvoiceAcceptanceRepository acceptanceRepository,
        IInvoiceSharingService sharingService,
        ILogger<InvoiceAcceptanceService> logger)
    {
        _acceptanceRepository = acceptanceRepository;
        _sharingService = sharingService;
        _logger = logger;
    }

    public async Task<InvoiceAcceptanceResult> AcceptAsync(string shareToken, string ipAddress, string userAgent)
    {
        // Look up the share by token
        var share = await _sharingService.GetByTokenAsync(shareToken);

        if (share == null)
        {
            return new InvoiceAcceptanceResult
            {
                Success = false,
                Message = "Share link not found."
            };
        }

        // Validate share is active
        if (!share.IsActive)
        {
            return new InvoiceAcceptanceResult
            {
                Success = false,
                Message = "This share link is no longer valid."
            };
        }

        // Validate share is not expired
        if (share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return new InvoiceAcceptanceResult
            {
                Success = false,
                Message = "This share link has expired."
            };
        }

        // Check for existing acceptance (one-per-share invariant)
        var existing = await _acceptanceRepository.GetByInvoiceShareIdAsync(share.Id);

        if (existing != null)
        {
            return new InvoiceAcceptanceResult
            {
                Success = false,
                AlreadyAccepted = true,
                AcceptedAtUtc = existing.AcceptedAtUtc,
                Message = "This invoice has already been accepted."
            };
        }

        // Build the acceptance entity with full audit trail
        var entity = new InvoiceAcceptance
        {
            InvoiceShareId = share.Id,
            AcceptedTerms = InvoiceAcceptanceConstants.AcceptanceTermsText,
            AcceptedAtUtc = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        try
        {
            await _acceptanceRepository.InsertAsync(entity);
        }
        catch (DbUpdateException ex)
        {
            // UNIQUE constraint violation — race condition where another request
            // accepted the same share between our check and insert.
            // Treat as duplicate acceptance.
            _logger.LogWarning(ex, "Duplicate acceptance detected for InvoiceShareId {InvoiceShareId} (race condition)", share.Id);

            var raceExisting = await _acceptanceRepository.GetByInvoiceShareIdAsync(share.Id);

            return new InvoiceAcceptanceResult
            {
                Success = false,
                AlreadyAccepted = true,
                AcceptedAtUtc = raceExisting?.AcceptedAtUtc,
                Message = "This invoice has already been accepted."
            };
        }

        _logger.LogInformation("Invoice acceptance recorded for InvoiceShareId {InvoiceShareId}", share.Id);

        return new InvoiceAcceptanceResult
        {
            Success = true,
            AcceptedAtUtc = entity.AcceptedAtUtc,
            Message = "Invoice accepted successfully."
        };
    }

    public async Task<InvoiceAcceptance?> GetByInvoiceShareIdAsync(int invoiceShareId)
    {
        return await _acceptanceRepository.GetByInvoiceShareIdAsync(invoiceShareId);
    }

    public async Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds)
    {
        return await _acceptanceRepository.GetAcceptedShareIdsAsync(shareIds);
    }
}
