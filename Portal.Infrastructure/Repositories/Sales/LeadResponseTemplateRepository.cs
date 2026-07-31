using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadResponseTemplate] entity CRUD operations.
/// </summary>
public class LeadResponseTemplateRepository : GenericStoredProcedureRepository<LeadResponseTemplate>
{
    public LeadResponseTemplateRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(LeadResponseTemplate entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[LeadResponseTemplate]
                    ([BusinessId], [ProductId], [LeadResponseTypeId], [Name],
                     [Subject], [BodyTemplate], [ResponseTimeInHours], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @ProductId, @LeadResponseTypeId, @Name,
                     @Subject, @BodyTemplate, @ResponseTimeInHours, 1, @CreatedAtUtc);
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
                command.Parameters.Add(new SqlParameter("@ProductId", entity.ProductId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@LeadResponseTypeId", entity.LeadResponseTypeId));
                command.Parameters.Add(new SqlParameter("@Name", entity.Name));
                command.Parameters.Add(new SqlParameter("@Subject", entity.Subject ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@BodyTemplate", entity.BodyTemplate));
                command.Parameters.Add(new SqlParameter("@ResponseTimeInHours", entity.ResponseTimeInHours));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", DateTime.UtcNow));

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

    public async Task UpdateAsync(LeadResponseTemplate entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[LeadResponseTemplate]
                SET [ProductId] = @ProductId,
                    [LeadResponseTypeId] = @LeadResponseTypeId,
                    [Name] = @Name,
                    [Subject] = @Subject,
                    [BodyTemplate] = @BodyTemplate,
                    [ResponseTimeInHours] = @ResponseTimeInHours
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@ProductId", entity.ProductId ?? (object)DBNull.Value),
                new SqlParameter("@LeadResponseTypeId", entity.LeadResponseTypeId),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Subject", entity.Subject ?? (object)DBNull.Value),
                new SqlParameter("@BodyTemplate", entity.BodyTemplate),
                new SqlParameter("@ResponseTimeInHours", entity.ResponseTimeInHours)
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
                UPDATE [sales].[LeadResponseTemplate]
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
                UPDATE [sales].[LeadResponseTemplate]
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

    public async Task<LeadResponseTemplate?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ProductId], [LeadResponseTypeId], [Name],
                       [Subject], [BodyTemplate], [ResponseTimeInHours], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadResponseTemplate]
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

    /// <summary>
    /// Finds a matching template: first tries product-specific, then falls back to null product (generic).
    /// </summary>
    public async Task<LeadResponseTemplate?> FindMatchingTemplateAsync(int? productId, int businessId)
    {
        try
        {
            // Try product-specific first
            if (productId.HasValue)
            {
                const string productQuery = @"
                    SELECT TOP 1 [Id], [BusinessId], [ProductId], [LeadResponseTypeId], [Name],
                           [Subject], [BodyTemplate], [ResponseTimeInHours], [IsActive], [CreatedAtUtc]
                    FROM [sales].[LeadResponseTemplate]
                    WHERE [BusinessId] = @BusinessId AND [ProductId] = @ProductId AND [IsActive] = 1
                    ORDER BY [CreatedAtUtc] DESC";

                var productMatch = await ExecuteSingleRecordStoredProcedureUnfiltered(productQuery,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@ProductId", productId.Value));

                if (productMatch != null)
                    return productMatch;
            }

            // Fallback to generic (no product)
            const string genericQuery = @"
                SELECT TOP 1 [Id], [BusinessId], [ProductId], [LeadResponseTypeId], [Name],
                       [Subject], [BodyTemplate], [ResponseTimeInHours], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadResponseTemplate]
                WHERE [BusinessId] = @BusinessId AND [ProductId] IS NULL AND [IsActive] = 1
                ORDER BY [CreatedAtUtc] DESC";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(genericQuery,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<LeadResponseTemplate>> GetPagedAsync(int page, int pageSize, int businessId)
    {
        try
        {
            const string countQuery = @"
                SELECT COUNT(*)
                FROM [sales].[LeadResponseTemplate]
                WHERE [BusinessId] = @BusinessId";

            const string dataQuery = @"
                SELECT [Id], [BusinessId], [ProductId], [LeadResponseTypeId], [Name],
                       [Subject], [BodyTemplate], [ResponseTimeInHours], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadResponseTemplate]
                WHERE [BusinessId] = @BusinessId
                ORDER BY [Name]
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        countCommand.Transaction = transaction.GetDbTransaction();

                    countCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
                }

                if (totalCount == 0)
                {
                    return new PagedResult<LeadResponseTemplate>
                    {
                        Items = new List<LeadResponseTemplate>(),
                        CurrentPage = 1,
                        PageSize = pageSize,
                        TotalCount = 0
                    };
                }

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page > totalPages) page = totalPages;
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;

                var results = await ExecuteStoredProcedure(dataQuery,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@Offset", offset),
                    new SqlParameter("@PageSize", pageSize));

                return new PagedResult<LeadResponseTemplate>
                {
                    Items = results,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
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

    /// <summary>
    /// Returns all active templates for a business, ordered by Name.
    /// </summary>
    public async Task<List<LeadResponseTemplate>> GetAllActiveAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ProductId], [LeadResponseTypeId], [Name],
                       [Subject], [BodyTemplate], [ResponseTimeInHours], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadResponseTemplate]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1
                ORDER BY [Name]
                OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
