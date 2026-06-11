using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ProposalShare entity CRUD operations against the [quotation].[ProposalShare] table.
/// </summary>
public class ProposalShareRepository : GenericStoredProcedureRepository<ProposalShare>
{
    public ProposalShareRepository(DbContext context) : base(context) { }

    public virtual async Task<ProposalShare?> GetByTokenAsync(string token)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [quotation].[ProposalShare]
                WHERE [ShareToken] = @ShareToken";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query, new SqlParameter("@ShareToken", token));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<ProposalShare?> GetActiveByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [quotation].[ProposalShare]
                WHERE [QuotationId] = @QuotationId AND [IsActive] = 1";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@QuotationId", quotationId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<ProposalShare>> GetByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [quotation].[ProposalShare]
                WHERE [QuotationId] = @QuotationId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@QuotationId", quotationId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(ProposalShare entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[ProposalShare]
                    ([QuotationId], [BusinessId], [ShareToken], [SnapshotHtml], [CustomerEmail],
                     [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive])
                VALUES
                    (@QuotationId, @BusinessId, @ShareToken, @SnapshotHtml, @CustomerEmail,
                     @ExpiresAtUtc, @CreatedAtUtc, @CreatedByUserId, @IsActive)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@QuotationId", entity.QuotationId),
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

    public async Task<List<ProposalShare>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [BusinessId], [ShareToken], [SnapshotHtml],
                       [CustomerEmail], [ExpiresAtUtc], [CreatedAtUtc], [CreatedByUserId], [IsActive]
                FROM [quotation].[ProposalShare]
                WHERE [BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeactivateByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[ProposalShare]
                SET [IsActive] = 0
                WHERE [QuotationId] = @QuotationId AND [IsActive] = 1";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@QuotationId", quotationId));
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
                UPDATE [quotation].[ProposalShare]
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
                UPDATE [quotation].[ProposalShare]
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
