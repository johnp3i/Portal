using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Business logic for proposal acceptance — validating share state,
/// recording audit-trail acceptance, and enforcing one-per-share invariant.
/// </summary>
public class ProposalAcceptanceService : IProposalAcceptanceService
{
    private readonly ProposalAcceptanceRepository _acceptanceRepository;
    private readonly ProposalShareRepository _shareRepository;
    private readonly ILogger<ProposalAcceptanceService> _logger;

    public ProposalAcceptanceService(
        ProposalAcceptanceRepository acceptanceRepository,
        ProposalShareRepository shareRepository,
        ILogger<ProposalAcceptanceService> logger)
    {
        _acceptanceRepository = acceptanceRepository;
        _shareRepository = shareRepository;
        _logger = logger;
    }

    public async Task<ProposalAcceptanceResult> AcceptAsync(string shareToken, string ipAddress, string userAgent)
    {
        // Look up the share by token
        var share = await _shareRepository.GetByTokenAsync(shareToken);

        if (share == null)
        {
            return new ProposalAcceptanceResult
            {
                Success = false,
                Message = "Share link not found."
            };
        }

        // Validate share is active
        if (!share.IsActive)
        {
            return new ProposalAcceptanceResult
            {
                Success = false,
                Message = "This share link is no longer valid."
            };
        }

        // Validate share is not expired
        if (share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return new ProposalAcceptanceResult
            {
                Success = false,
                Message = "This share link has expired."
            };
        }

        // Check for existing acceptance (one-per-share invariant)
        var existing = await _acceptanceRepository.GetByProposalShareIdAsync(share.Id);

        if (existing != null)
        {
            return new ProposalAcceptanceResult
            {
                Success = false,
                AlreadyAccepted = true,
                AcceptedAtUtc = existing.AcceptedAtUtc,
                Message = "This proposal has already been accepted."
            };
        }

        // Build the acceptance entity with full audit trail
        var entity = new ProposalAcceptance
        {
            ProposalShareId = share.Id,
            AcceptedTerms = ProposalAcceptanceConstants.AcceptanceTermsText,
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
            _logger.LogWarning(ex, "Duplicate acceptance detected for ProposalShareId {ProposalShareId} (race condition)", share.Id);

            var raceExisting = await _acceptanceRepository.GetByProposalShareIdAsync(share.Id);

            return new ProposalAcceptanceResult
            {
                Success = false,
                AlreadyAccepted = true,
                AcceptedAtUtc = raceExisting?.AcceptedAtUtc,
                Message = "This proposal has already been accepted."
            };
        }

        _logger.LogInformation("Proposal acceptance recorded for ProposalShareId {ProposalShareId}", share.Id);

        return new ProposalAcceptanceResult
        {
            Success = true,
            AcceptedAtUtc = entity.AcceptedAtUtc,
            Message = "Proposal accepted successfully."
        };
    }

    public async Task<ProposalAcceptance?> GetByProposalShareIdAsync(int proposalShareId)
    {
        return await _acceptanceRepository.GetByProposalShareIdAsync(proposalShareId);
    }

    public async Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds)
    {
        return await _acceptanceRepository.GetAcceptedShareIdsAsync(shareIds);
    }
}
