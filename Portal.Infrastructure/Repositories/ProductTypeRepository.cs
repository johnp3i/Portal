using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Read-only repository for ProductType lookup table against the [product].[ProductType] table.
/// </summary>
public class ProductTypeRepository : GenericStoredProcedureRepository<ProductType>
{
    public ProductTypeRepository(DbContext context) : base(context) { }

    public async Task<List<ProductType>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name]
                FROM [product].[ProductType]
                ORDER BY [Id]";

            return await ExecuteStoredProcedure(query);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<ProductType?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name]
                FROM [product].[ProductType]
                WHERE [Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
