using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Billing;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Subscription entity CRUD operations against the [billing].[Subscription] table.
/// </summary>
public class SubscriptionRepository : GenericStoredProcedureRepository<Subscription>
{
    public SubscriptionRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Gets the subscription record for a given business.
    /// Returns null if no subscription exists.
    /// </summary>
    public virtual async Task<Subscription?> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [billing].[Subscription].[Id],
                       [billing].[Subscription].[BusinessId],
                       [billing].[Subscription].[PlanId],
                       [billing].[Subscription].[Status],
                       [billing].[Subscription].[StripeSubscriptionId],
                       [billing].[Subscription].[CurrentPeriodStart],
                       [billing].[Subscription].[CurrentPeriodEnd],
                       [billing].[Subscription].[CancelledAtUtc],
                       [billing].[Subscription].[IsGraceAccessUsed],
                       [billing].[Subscription].[CreatedAtUtc]
                FROM [billing].[Subscription]
                WHERE [billing].[Subscription].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new subscription record and returns the new Subscription.Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(Subscription entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [billing].[Subscription]
                    ([BusinessId], [PlanId], [Status], [StripeSubscriptionId],
                     [CurrentPeriodStart], [CurrentPeriodEnd], [CancelledAtUtc], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @PlanId, @Status, @StripeSubscriptionId,
                     @CurrentPeriodStart, @CurrentPeriodEnd, @CancelledAtUtc, @CreatedAtUtc)";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@PlanId", entity.PlanId));
                command.Parameters.Add(new SqlParameter("@Status", entity.Status));
                command.Parameters.Add(new SqlParameter("@StripeSubscriptionId", entity.StripeSubscriptionId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CurrentPeriodStart", entity.CurrentPeriodStart));
                command.Parameters.Add(new SqlParameter("@CurrentPeriodEnd", entity.CurrentPeriodEnd));
                command.Parameters.Add(new SqlParameter("@CancelledAtUtc", entity.CancelledAtUtc ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the subscription status and optionally sets the CancelledAtUtc timestamp.
    /// </summary>
    public virtual async Task UpdateStatusAsync(int id, string status, DateTime? cancelledAtUtc)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[Subscription]
                SET [Status] = @Status,
                    [CancelledAtUtc] = @CancelledAtUtc
                WHERE [billing].[Subscription].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@Status", status),
                new SqlParameter("@CancelledAtUtc", cancelledAtUtc ?? (object)DBNull.Value));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Atomically consumes the grace access for an expired subscription.
    /// Sets Status='cancelled', CancelledAtUtc=UtcNow, IsGraceAccessUsed=1
    /// only if IsGraceAccessUsed is currently 0 and Status is 'active'.
    /// Returns true if the update was applied (this request gets grace access).
    /// Returns false if another request already consumed it.
    /// </summary>
    public virtual async Task<bool> ConsumeGraceAccessAsync(int subscriptionId)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[Subscription] WITH (ROWLOCK)
                SET [Status] = 'cancelled',
                    [CancelledAtUtc] = @CancelledAtUtc,
                    [IsGraceAccessUsed] = 1
                WHERE [billing].[Subscription].[Id] = @Id
                  AND [billing].[Subscription].[IsGraceAccessUsed] = 0
                  AND [billing].[Subscription].[Status] = 'active'";

            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", subscriptionId),
                new SqlParameter("@CancelledAtUtc", DateTime.UtcNow));

            return rowsAffected == 1;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the subscription period dates, status, and plan.
    /// Used when processing invoice.paid or customer.subscription.updated webhook events.
    /// </summary>
    public virtual async Task UpdatePeriodAsync(int id, DateTime periodStart, DateTime periodEnd, string status, int planId)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[Subscription]
                SET [CurrentPeriodStart] = @CurrentPeriodStart,
                    [CurrentPeriodEnd] = @CurrentPeriodEnd,
                    [Status] = @Status,
                    [PlanId] = @PlanId
                WHERE [billing].[Subscription].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@CurrentPeriodStart", periodStart),
                new SqlParameter("@CurrentPeriodEnd", periodEnd),
                new SqlParameter("@Status", status),
                new SqlParameter("@PlanId", planId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the subscription period dates, status, and plan, and resets IsGraceAccessUsed to 0.
    /// Used when a webhook renewal arrives with Status="active" and a CurrentPeriodEnd later than
    /// the previously stored value, indicating a genuine renewal that should reset the grace flag.
    /// </summary>
    public virtual async Task UpdatePeriodWithGraceResetAsync(int id, DateTime periodStart, DateTime periodEnd, string status, int planId)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[Subscription]
                SET [CurrentPeriodStart] = @CurrentPeriodStart,
                    [CurrentPeriodEnd] = @CurrentPeriodEnd,
                    [Status] = @Status,
                    [PlanId] = @PlanId,
                    [IsGraceAccessUsed] = 0
                WHERE [billing].[Subscription].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@CurrentPeriodStart", periodStart),
                new SqlParameter("@CurrentPeriodEnd", periodEnd),
                new SqlParameter("@Status", status),
                new SqlParameter("@PlanId", planId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Activates a trialing subscription by setting the StripeSubscriptionId, updating the status
    /// to "active", updating the period dates, and resetting IsGraceAccessUsed.
    /// Used when a promo trial user subscribes via Stripe and the webhook needs to upgrade
    /// their existing trialing subscription record.
    /// </summary>
    public virtual async Task ActivateTrialingSubscriptionAsync(int id, string stripeSubscriptionId, DateTime periodStart, DateTime periodEnd, int planId)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[Subscription]
                SET [StripeSubscriptionId] = @StripeSubscriptionId,
                    [Status] = 'active',
                    [CurrentPeriodStart] = @CurrentPeriodStart,
                    [CurrentPeriodEnd] = @CurrentPeriodEnd,
                    [PlanId] = @PlanId,
                    [IsGraceAccessUsed] = 0,
                    [CancelledAtUtc] = NULL
                WHERE [billing].[Subscription].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@StripeSubscriptionId", stripeSubscriptionId),
                new SqlParameter("@CurrentPeriodStart", periodStart),
                new SqlParameter("@CurrentPeriodEnd", periodEnd),
                new SqlParameter("@PlanId", planId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
