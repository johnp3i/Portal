using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Stripe;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for StripeCustomer entity operations against the [stripe].[Customer] table.
/// </summary>
public class StripeCustomerRepository : GenericStoredProcedureRepository<StripeCustomer>
{
    public StripeCustomerRepository(DbContext context) : base(context) { }

    public virtual async Task<StripeCustomer?> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [StripeCustomerId], [CreatedAtUtc]
                FROM [stripe].[Customer]
                WHERE [BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task InsertAsync(StripeCustomer entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [stripe].[Customer]
                    ([BusinessId], [StripeCustomerId], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @StripeCustomerId, @CreatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@StripeCustomerId", entity.StripeCustomerId),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<StripeCustomer?> GetByStripeCustomerIdAsync(string stripeCustomerId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [StripeCustomerId], [CreatedAtUtc]
                FROM [stripe].[Customer]
                WHERE [StripeCustomerId] = @StripeCustomerId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@StripeCustomerId", stripeCustomerId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
