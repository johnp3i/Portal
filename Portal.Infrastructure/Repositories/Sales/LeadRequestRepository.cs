using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadRequest] entity CRUD operations.
/// </summary>
public class LeadRequestRepository : GenericStoredProcedureRepository<LeadRequest>
{
    public LeadRequestRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(LeadRequest entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[LeadRequest]
                    ([BusinessId], [ContactId], [ProductId], [LeadSourceTypeId],
                     [LeadSourceReferenceTypeId], [LeadStatusTypeId], [SourceUrl],
                     [RequestText], [AssignedToUserId], [IsCancelled], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @ContactId, @ProductId, @LeadSourceTypeId,
                     @LeadSourceReferenceTypeId, @LeadStatusTypeId, @SourceUrl,
                     @RequestText, @AssignedToUserId, 0, 1, @CreatedAtUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var transaction = _context.Database.CurrentTransaction;
            if (transaction != null)
                command.Transaction = transaction.GetDbTransaction();

            command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
            command.Parameters.Add(new SqlParameter("@ContactId", entity.ContactId));
            command.Parameters.Add(new SqlParameter("@ProductId", entity.ProductId ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@LeadSourceTypeId", entity.LeadSourceTypeId));
            command.Parameters.Add(new SqlParameter("@LeadSourceReferenceTypeId", entity.LeadSourceReferenceTypeId ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@LeadStatusTypeId", 1)); // Default: New
            command.Parameters.Add(new SqlParameter("@SourceUrl", entity.SourceUrl ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@RequestText", entity.RequestText ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@AssignedToUserId", entity.AssignedToUserId ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@CreatedAtUtc", DateTime.UtcNow));

            var result = await command.ExecuteScalarAsync();
            return (int)result!;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateStageAsync(int id, int businessId, int leadStatusTypeId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[LeadRequest]
                SET [LeadStatusTypeId] = @LeadStatusTypeId
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@LeadStatusTypeId", leadStatusTypeId)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateAssignmentAsync(int id, int businessId, string? assignedToUserId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[LeadRequest]
                SET [AssignedToUserId] = @AssignedToUserId
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@AssignedToUserId", assignedToUserId ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task CancelAsync(int id, int businessId, string? description)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[LeadRequest]
                SET [IsCancelled] = 1,
                    [CancellationTimestamp] = @CancellationTimestamp,
                    [CancellationDescription] = @CancellationDescription
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@CancellationTimestamp", DateTime.UtcNow),
                new SqlParameter("@CancellationDescription", description ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task ReactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[LeadRequest]
                SET [IsCancelled] = 0,
                    [CancellationTimestamp] = NULL,
                    [CancellationDescription] = NULL,
                    [LeadStatusTypeId] = 1
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

    public async Task UpdateTeamMemberAsync(int id, int businessId, int? teamMemberId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[LeadRequest]
                SET [TeamMemberId] = @TeamMemberId
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@TeamMemberId", teamMemberId ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateRequestTextAsync(int id, int businessId, string? requestText)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[LeadRequest]
                SET [RequestText] = @RequestText
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@RequestText", requestText ?? (object)DBNull.Value)
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
                UPDATE [sales].[LeadRequest]
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

    public async Task<LeadRequest?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ContactId], [ProductId], [LeadSourceTypeId],
                       [LeadSourceReferenceTypeId], [LeadStatusTypeId], [SourceUrl], [RequestText],
                       [AssignedToUserId], [TeamMemberId], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadRequest]
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

    public async Task<List<LeadRequest>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ContactId], [ProductId], [LeadSourceTypeId],
                       [LeadSourceReferenceTypeId], [LeadStatusTypeId], [SourceUrl], [RequestText],
                       [AssignedToUserId], [TeamMemberId], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadRequest]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
            return results.OrderByDescending(x => x.CreatedAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<LeadRequest>> GetByContactIdAsync(int contactId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ContactId], [ProductId], [LeadSourceTypeId],
                       [LeadSourceReferenceTypeId], [LeadStatusTypeId], [SourceUrl], [RequestText],
                       [AssignedToUserId], [TeamMemberId], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadRequest]
                WHERE [ContactId] = @ContactId AND [BusinessId] = @BusinessId AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query,
                new SqlParameter("@ContactId", contactId),
                new SqlParameter("@BusinessId", businessId));
            return results.OrderByDescending(x => x.CreatedAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<LeadRequest>> GetPagedAsync(
        string? assignedToUserId, int? productId, int? leadStatusTypeId,
        string? searchTerm, int page, int pageSize, int businessId)
    {
        try
        {
            const string countQuery = @"
                SELECT COUNT(*)
                FROM [sales].[LeadRequest]
                WHERE [sales].[LeadRequest].[BusinessId] = @BusinessId
                  AND [sales].[LeadRequest].[IsActive] = 1
                  AND (@AssignedToUserId IS NULL OR [sales].[LeadRequest].[AssignedToUserId] = @AssignedToUserId)
                  AND (@ProductId IS NULL OR [sales].[LeadRequest].[ProductId] = @ProductId)
                  AND (@LeadStatusTypeId IS NULL OR [sales].[LeadRequest].[LeadStatusTypeId] = @LeadStatusTypeId)";

            const string dataQuery = @"
                SELECT [Id], [BusinessId], [ContactId], [ProductId], [LeadSourceTypeId],
                       [LeadSourceReferenceTypeId], [LeadStatusTypeId], [SourceUrl], [RequestText],
                       [AssignedToUserId], [TeamMemberId], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedAtUtc]
                FROM [sales].[LeadRequest]
                WHERE [sales].[LeadRequest].[BusinessId] = @BusinessId
                  AND [sales].[LeadRequest].[IsActive] = 1
                  AND (@AssignedToUserId IS NULL OR [sales].[LeadRequest].[AssignedToUserId] = @AssignedToUserId)
                  AND (@ProductId IS NULL OR [sales].[LeadRequest].[ProductId] = @ProductId)
                  AND (@LeadStatusTypeId IS NULL OR [sales].[LeadRequest].[LeadStatusTypeId] = @LeadStatusTypeId)
                ORDER BY [sales].[LeadRequest].[CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var assignedParam = string.IsNullOrWhiteSpace(assignedToUserId) ? (object)DBNull.Value : assignedToUserId;
            var productParam = productId.HasValue ? (object)productId.Value : DBNull.Value;
            var statusParam = leadStatusTypeId.HasValue ? (object)leadStatusTypeId.Value : DBNull.Value;

            int totalCount;
            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = countQuery;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    countCommand.Transaction = transaction.GetDbTransaction();

                countCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                countCommand.Parameters.Add(new SqlParameter("@AssignedToUserId", assignedParam));
                countCommand.Parameters.Add(new SqlParameter("@ProductId", productParam));
                countCommand.Parameters.Add(new SqlParameter("@LeadStatusTypeId", statusParam));

                var countResult = await countCommand.ExecuteScalarAsync();
                totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
            }

            if (totalCount == 0)
            {
                return new PagedResult<LeadRequest>
                {
                    Items = new List<LeadRequest>(),
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
                new SqlParameter("@AssignedToUserId", assignedParam),
                new SqlParameter("@ProductId", productParam),
                new SqlParameter("@LeadStatusTypeId", statusParam),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize));

            return new PagedResult<LeadRequest>
            {
                Items = results,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
