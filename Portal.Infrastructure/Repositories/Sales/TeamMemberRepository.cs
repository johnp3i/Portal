using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[TeamMember] entity CRUD operations.
/// </summary>
public class TeamMemberRepository : GenericStoredProcedureRepository<TeamMember>
{
    public TeamMemberRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(TeamMember entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[TeamMember]
                    ([BusinessId], [FirstName], [LastName], [Email], [PhoneNumber], [Role], [UserId], [IsActive])
                VALUES
                    (@BusinessId, @FirstName, @LastName, @Email, @PhoneNumber, @Role, @UserId, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

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
                command.Parameters.Add(new SqlParameter("@FirstName", entity.FirstName));
                command.Parameters.Add(new SqlParameter("@LastName", entity.LastName ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@PhoneNumber", entity.PhoneNumber ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Role", entity.Role ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@UserId", entity.UserId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateAsync(TeamMember entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[TeamMember]
                SET [FirstName] = @FirstName,
                    [LastName] = @LastName,
                    [Email] = @Email,
                    [PhoneNumber] = @PhoneNumber,
                    [Role] = @Role,
                    [UserId] = @UserId
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@FirstName", entity.FirstName),
                new SqlParameter("@LastName", entity.LastName ?? (object)DBNull.Value),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@PhoneNumber", entity.PhoneNumber ?? (object)DBNull.Value),
                new SqlParameter("@Role", entity.Role ?? (object)DBNull.Value),
                new SqlParameter("@UserId", entity.UserId ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task DeactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[TeamMember]
                SET [IsActive] = 0
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

    public async Task ActivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[TeamMember]
                SET [IsActive] = 1
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

    public async Task<TeamMember?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber], [Role], [UserId], [IsActive], [CreatedAtUtc]
                FROM [sales].[TeamMember]
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<TeamMember>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber], [Role], [UserId], [IsActive], [CreatedAtUtc]
                FROM [sales].[TeamMember]
                WHERE [BusinessId] = @BusinessId";

            var results = await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
            return results.OrderBy(t => t.FirstName).ThenBy(t => t.LastName).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<TeamMember>> GetActiveByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber], [Role], [UserId], [IsActive], [CreatedAtUtc]
                FROM [sales].[TeamMember]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
            return results.OrderBy(t => t.FirstName).ThenBy(t => t.LastName).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<TeamMember?> CheckDuplicateEmailAsync(string email, int businessId, int? excludeId = null)
    {
        try
        {
            var query = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber], [Role], [UserId], [IsActive], [CreatedAtUtc]
                FROM [sales].[TeamMember]
                WHERE [BusinessId] = @BusinessId AND [Email] = @Email";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Email", email)
            };

            if (excludeId.HasValue)
            {
                query += " AND [Id] != @ExcludeId";
                parameters.Add(new SqlParameter("@ExcludeId", excludeId.Value));
            }

            return await ExecuteSingleRecordStoredProcedure(query, parameters.ToArray());
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
