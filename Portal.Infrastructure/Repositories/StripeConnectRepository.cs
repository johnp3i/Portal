using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Stripe Connect data access — connected accounts and checkout sessions.
/// </summary>
public class StripeConnectRepository
{
    private readonly PortalDbContext _context;

    public StripeConnectRepository(PortalDbContext context)
    {
        _context = context;
    }

    // ─── Connected Accounts ───────────────────────────────────────────────

    /// <summary>
    /// Gets the active connected account for a business, or null if not connected.
    /// </summary>
    public async Task<StripeConnectedAccount?> GetConnectedAccountAsync(int businessId)
    {
        try
        {
            return await _context.StripeConnectedAccounts
                .FirstOrDefaultAsync(ca => ca.BusinessId == businessId && ca.IsActive);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new connected account record.
    /// </summary>
    public async Task InsertConnectedAccountAsync(StripeConnectedAccount entity)
    {
        try
        {
            _context.StripeConnectedAccounts.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Marks a connected account as disconnected (soft-delete).
    /// </summary>
    public async Task DisconnectAccountAsync(int businessId)
    {
        try
        {
            var account = await _context.StripeConnectedAccounts
                .FirstOrDefaultAsync(ca => ca.BusinessId == businessId && ca.IsActive);

            if (account != null)
            {
                account.IsActive = false;
                account.DisconnectedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // ─── Checkout Sessions ────────────────────────────────────────────────

    /// <summary>
    /// Inserts a new checkout session record and returns the Id.
    /// </summary>
    public async Task<int> InsertCheckoutSessionAsync(StripeCheckoutSession entity)
    {
        try
        {
            _context.StripeCheckoutSessions.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a checkout session by its Stripe session ID.
    /// </summary>
    public async Task<StripeCheckoutSession?> GetByStripeSessionIdAsync(string stripeSessionId)
    {
        try
        {
            return await _context.StripeCheckoutSessions
                .FirstOrDefaultAsync(cs => cs.StripeSessionId == stripeSessionId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates a checkout session after payment completion (fee, net, charge ID, status, payment ID).
    /// </summary>
    public async Task MarkSessionCompletedAsync(string stripeSessionId, decimal stripeFeeAmount, decimal netAmount, string stripeChargeId, int paymentId)
    {
        try
        {
            var session = await _context.StripeCheckoutSessions
                .FirstOrDefaultAsync(cs => cs.StripeSessionId == stripeSessionId);

            if (session != null)
            {
                session.Status = "completed";
                session.StripeFeeAmount = stripeFeeAmount;
                session.NetAmount = netAmount;
                session.StripeChargeId = stripeChargeId;
                session.PaymentId = paymentId;
                session.CompletedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Marks a session as expired.
    /// </summary>
    public async Task MarkSessionExpiredAsync(string stripeSessionId)
    {
        try
        {
            var session = await _context.StripeCheckoutSessions
                .FirstOrDefaultAsync(cs => cs.StripeSessionId == stripeSessionId);

            if (session != null)
            {
                session.Status = "expired";
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets completed checkout sessions for a business within a date range, for the Card Payments view.
    /// </summary>
    public async Task<List<StripeCheckoutSession>> GetCompletedSessionsAsync(int businessId, DateTime? fromUtc, DateTime? toUtc)
    {
        try
        {
            var query = _context.StripeCheckoutSessions
                .Where(cs => cs.BusinessId == businessId && cs.Status == "completed");

            if (fromUtc.HasValue)
                query = query.Where(cs => cs.CompletedAtUtc >= fromUtc.Value);

            if (toUtc.HasValue)
                query = query.Where(cs => cs.CompletedAtUtc < toUtc.Value);

            return await query
                .OrderByDescending(cs => cs.CompletedAtUtc)
                .Include(cs => cs.Invoice)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
