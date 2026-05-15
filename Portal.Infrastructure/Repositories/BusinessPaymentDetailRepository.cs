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
                       [SortOrder], [IsActive], [CreatedAtUtc]
                FROM [portal].[BusinessPaymentDetail]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1
                ORDER BY [SortOrder]";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
