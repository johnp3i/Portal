using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Supplier entity CRUD operations against the [purchase].[Supplier] table.
/// </summary>
public class SupplierRepository : GenericStoredProcedureRepository<Supplier>
{
    public SupplierRepository(DbContext context) : base(context) { }

    public async Task<List<Supplier>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [IsActive], [CreatedAtUtc]
                FROM [purchase].[Supplier]
                WHERE Supplier.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Supplier?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [IsActive], [CreatedAtUtc]
                FROM [purchase].[Supplier]
                WHERE Supplier.Id = @Id AND Supplier.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<int> InsertAsync(Supplier entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [purchase].[Supplier]
                    ([BusinessId], [Name], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @Name, @IsActive, @CreatedAtUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var idParam = new SqlParameter("@BusinessId", entity.BusinessId);
            var nameParam = new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value);
            var isActiveParam = new SqlParameter("@IsActive", entity.IsActive);
            var createdParam = new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc);

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = query;
            command.Parameters.Add(idParam);
            command.Parameters.Add(nameParam);
            command.Parameters.Add(isActiveParam);
            command.Parameters.Add(createdParam);

            if (command.Connection!.State != System.Data.ConnectionState.Open)
                await command.Connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();
            var insertedId = result != null ? Convert.ToInt32(result) : 0;
            entity.Id = insertedId;
            return insertedId;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(Supplier entity)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Supplier]
                SET
                    [Name] = @Name
                WHERE Supplier.Id = @Id AND Supplier.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Supplier]
                SET
                    [IsActive] = 0
                WHERE Supplier.Id = @Id AND Supplier.BusinessId = @BusinessId";

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
