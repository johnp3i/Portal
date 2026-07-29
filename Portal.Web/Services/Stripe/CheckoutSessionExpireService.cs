using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Services;
using Serilog;
using Stripe;
using Stripe.Checkout;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Expires pending Stripe Checkout Sessions when an invoice becomes fully paid.
/// Best-effort — never throws exceptions to callers.
/// </summary>
public class CheckoutSessionExpireService : ICheckoutSessionExpireService
{
    private readonly PortalDbContext _dbContext;
    private readonly IStripeKeyResolutionService _keyResolutionService;

    public CheckoutSessionExpireService(
        PortalDbContext dbContext,
        IStripeKeyResolutionService keyResolutionService)
    {
        _dbContext = dbContext;
        _keyResolutionService = keyResolutionService;
    }

    public async Task TryExpirePendingSessionsAsync(int invoiceId, int businessId, string? excludeSessionId = null)
    {
        try
        {
            // 1. Query pending sessions for this invoice
            var query = _dbContext.StripeCheckoutSessions
                .Where(s => s.InvoiceId == invoiceId
                         && s.BusinessId == businessId
                         && s.Status == "pending");

            if (!string.IsNullOrEmpty(excludeSessionId))
                query = query.Where(s => s.StripeSessionId != excludeSessionId);

            var pendingSessions = await query.ToListAsync();

            if (pendingSessions.Count == 0)
                return;

            Log.Information("Auto-expire: Starting for InvoiceId={InvoiceId}, PendingSessions={Count}",
                invoiceId, pendingSessions.Count);

            // 2. Resolve Stripe keys for this business
            ResolvedStripeKeys resolvedKeys;
            try
            {
                resolvedKeys = await _keyResolutionService.ResolveKeysAsync(businessId);
                if (string.IsNullOrEmpty(resolvedKeys.SecretKey))
                {
                    Log.Warning("Auto-expire: No Stripe secret key available for BusinessId={BusinessId}, skipping", businessId);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Auto-expire: Key resolution failed for BusinessId={BusinessId}", businessId);
                return;
            }

            // 3. Expire each session
            var requestOptions = new RequestOptions { ApiKey = resolvedKeys.SecretKey };
            var sessionService = new SessionService();
            var succeeded = 0;
            var failed = 0;

            foreach (var session in pendingSessions)
            {
                try
                {
                    await sessionService.ExpireAsync(session.StripeSessionId, null, requestOptions);
                    session.Status = "expired";
                    session.CompletedAtUtc = DateTime.UtcNow;
                    succeeded++;
                    Log.Information("Auto-expire: Expired session {StripeSessionId} for InvoiceId={InvoiceId}",
                        session.StripeSessionId, invoiceId);
                }
                catch (StripeException stripeEx) when (stripeEx.StripeError?.Code == "resource_missing"
                    || stripeEx.Message.Contains("already expired", StringComparison.OrdinalIgnoreCase)
                    || stripeEx.Message.Contains("already been completed", StringComparison.OrdinalIgnoreCase))
                {
                    // Session already expired or completed on Stripe's side — update local record
                    session.Status = stripeEx.Message.Contains("completed", StringComparison.OrdinalIgnoreCase)
                        ? "completed"
                        : "expired";
                    session.CompletedAtUtc ??= DateTime.UtcNow;
                    succeeded++;
                }
                catch (StripeException stripeEx)
                {
                    Log.Warning(stripeEx, "Auto-expire: Failed to expire session {StripeSessionId} for InvoiceId={InvoiceId}",
                        session.StripeSessionId, invoiceId);
                    failed++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Auto-expire: Unexpected error expiring session {StripeSessionId} for InvoiceId={InvoiceId}",
                        session.StripeSessionId, invoiceId);
                    failed++;
                }
            }

            // 4. Batch save all status updates
            await _dbContext.SaveChangesAsync();

            Log.Information("Auto-expire: Completed for InvoiceId={InvoiceId}. Processed={Total}, Succeeded={Succeeded}, Failed={Failed}",
                invoiceId, pendingSessions.Count, succeeded, failed);
        }
        catch (Exception ex)
        {
            // Top-level safety net — never throw to caller
            Log.Error(ex, "Auto-expire: Unhandled error for InvoiceId={InvoiceId}, BusinessId={BusinessId}", invoiceId, businessId);
        }
    }
}
