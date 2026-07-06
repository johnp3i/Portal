using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for BusinessPaymentDetail entity operations against the [portal].[BusinessPaymentDetail] table.
/// </summary>
public class BusinessPaymentDetailRepository : GenericStoredProcedureRepository<BusinessPaymentDetail>
{
    public BusinessPaymentDetailRepository(DbContext context) : base(context) { }

    public async Task<List<BusinessPaymentDetail>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Label], [BankName], [Iban], [PayeeName],
                       [SwiftBic], [SortOrder], [IsActive], [CreatedAtUtc]
                FROM [portal].[BusinessPaymentDetail]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1
                ORDER BY [SortOrder]";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task InsertAsync(BusinessPaymentDetail entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [portal].[BusinessPaymentDetail]
                    ([BusinessId], [Label], [BankName], [Iban], [PayeeName], [SwiftBic], [SortOrder], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @Label, @BankName, @Iban, @PayeeName, @SwiftBic, @SortOrder, 1, GETUTCDATE())";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Label", entity.Label),
                new SqlParameter("@BankName", entity.BankName),
                new SqlParameter("@Iban", entity.Iban),
                new SqlParameter("@PayeeName", entity.PayeeName),
                new SqlParameter("@SwiftBic", entity.SwiftBic ?? (object)DBNull.Value),
                new SqlParameter("@SortOrder", entity.SortOrder)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task DeleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                DELETE FROM [portal].[BusinessPaymentDetail]
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateAsync(int id, int businessId, string label, string bankName, string iban, string payeeName, string? swiftBic)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[BusinessPaymentDetail]
                SET [Label] = @Label, [BankName] = @BankName, [Iban] = @Iban, [PayeeName] = @PayeeName, [SwiftBic] = @SwiftBic
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Label", label),
                new SqlParameter("@BankName", bankName),
                new SqlParameter("@Iban", iban),
                new SqlParameter("@PayeeName", payeeName),
                new SqlParameter("@SwiftBic", swiftBic ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
