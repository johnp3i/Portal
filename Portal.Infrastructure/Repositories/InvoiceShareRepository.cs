using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for InvoiceShare entity CRUD operations against the [invoice].[InvoiceShare] table.
/// </summary>
public class InvoiceShareRepository : GenericStoredProcedureRepository<InvoiceShare>
{
    public InvoiceShareRepository(DbContext context) : base(context) { }

    public async Task InsertAsync(InvoiceShare entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [invoice].[InvoiceShare]
                    ([InvoiceId], [BusinessId], [ShareToken], [SnapshotHtml], [CustomerEmail],
                     [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive])
                VALUES
                    (@InvoiceId, @BusinessId, @ShareToken, @SnapshotHtml, @CustomerEmail,
                     @ExpiresAtUtc, @CreatedAtUtc, @CreatedByUserId, @IsActive)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@InvoiceId", entity.InvoiceId),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@ShareToken", entity.ShareToken),
                new SqlParameter("@SnapshotHtml", entity.SnapshotHtml),
                new SqlParameter("@CustomerEmail", entity.CustomerEmail),
                new SqlParameter("@ExpiresAtUtc", entity.ExpiresAtUtc),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc),
                new SqlParameter("@CreatedByUserId", entity.CreatedByUserId),
                new SqlParameter("@IsActive", entity.IsActive)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<InvoiceShare?> GetByTokenAsync(string token)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [invoice].[InvoiceShare]
                WHERE [ShareToken] = @ShareToken";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query, new SqlParameter("@ShareToken", token));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<InvoiceShare?> GetActiveByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [invoice].[InvoiceShare]
                WHERE [InvoiceId] = @InvoiceId AND [IsActive] = 1";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@InvoiceId", invoiceId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<InvoiceShare>> GetByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [invoice].[InvoiceShare]
                WHERE [InvoiceId] = @InvoiceId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@InvoiceId", invoiceId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<InvoiceShare>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [invoice].[InvoiceShare]
                WHERE [BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeactivateByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[InvoiceShare]
                SET [IsActive] = 0
                WHERE [InvoiceId] = @InvoiceId AND [IsActive] = 1";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@InvoiceId", invoiceId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeactivateByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[InvoiceShare]
                SET [IsActive] = 0
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task ReactivateByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[InvoiceShare]
                SET [IsActive] = 1
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }
}
