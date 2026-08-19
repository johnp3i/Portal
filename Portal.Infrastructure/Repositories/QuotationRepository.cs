using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Quotation entity CRUD operations against the [quotation].[Quotation] table.
/// </summary>
public class QuotationRepository : GenericStoredProcedureRepository<Quotation>
{
    public QuotationRepository(DbContext context) : base(context) { }

    public async Task<List<Quotation>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [quotation].[Quotation].[Id], [quotation].[Quotation].[BusinessId], [quotation].[Quotation].[CustomerId],
                       [quotation].[Quotation].[QuotationStatusTypeId], [quotation].[Quotation].[Reference],
                       [quotation].[Quotation].[ValidUntil], [quotation].[Quotation].[Subtotal],
                       [quotation].[Quotation].[TaxAmount], [quotation].[Quotation].[TotalAmount],
                       [quotation].[Quotation].[Notes], [quotation].[Quotation].[CreatedAtUtc],
                       [quotation].[Quotation].[UpdatedAtUtc], [quotation].[Quotation].[QuotationContactId],
                       [quotation].[Quotation].[IsGrandTotalShown], [quotation].[Quotation].[IsDeleted],
                       [quotation].[Quotation].[DeletedAtUtc], [quotation].[Quotation].[LeadRequestId]
                FROM [quotation].[Quotation]
                WHERE [quotation].[Quotation].[BusinessId] = @BusinessId
                  AND [quotation].[Quotation].[IsDeleted] = 0";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Quotation?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [quotation].[Quotation].[Id], [quotation].[Quotation].[BusinessId], [quotation].[Quotation].[CustomerId],
                       [quotation].[Quotation].[QuotationStatusTypeId], [quotation].[Quotation].[Reference],
                       [quotation].[Quotation].[ValidUntil], [quotation].[Quotation].[Subtotal],
                       [quotation].[Quotation].[TaxAmount], [quotation].[Quotation].[TotalAmount],
                       [quotation].[Quotation].[Notes], [quotation].[Quotation].[CreatedAtUtc],
                       [quotation].[Quotation].[UpdatedAtUtc], [quotation].[Quotation].[QuotationContactId],
                       [quotation].[Quotation].[IsGrandTotalShown], [quotation].[Quotation].[IsDeleted],
                       [quotation].[Quotation].[DeletedAtUtc], [quotation].[Quotation].[LeadRequestId]
                FROM [quotation].[Quotation]
                WHERE [quotation].[Quotation].[Id] = @Id AND [quotation].[Quotation].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(Quotation entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[Quotation]
                    ([BusinessId], [CustomerId], [QuotationStatusTypeId], [Reference], [ValidUntil],
                     [Subtotal], [TaxAmount], [TotalAmount], [Notes], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@BusinessId, @CustomerId, @QuotationStatusTypeId, @Reference, @ValidUntil,
                     @Subtotal, @TaxAmount, @TotalAmount, @Notes, @CreatedAtUtc, @UpdatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@CustomerId", entity.CustomerId),
                new SqlParameter("@QuotationStatusTypeId", entity.QuotationStatusTypeId),
                new SqlParameter("@Reference", entity.Reference),
                new SqlParameter("@ValidUntil", entity.ValidUntil.HasValue ? entity.ValidUntil.Value : (object)DBNull.Value),
                new SqlParameter("@Subtotal", entity.Subtotal),
                new SqlParameter("@TaxAmount", entity.TaxAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int> InsertAndReturnIdAsync(Quotation entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[Quotation]
                    ([BusinessId], [CustomerId], [QuotationStatusTypeId], [Reference], [ValidUntil],
                     [Subtotal], [TaxAmount], [TotalAmount], [Notes], [CreatedAtUtc], [UpdatedAtUtc],
                     [QuotationContactId], [IsGrandTotalShown])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @CustomerId, @QuotationStatusTypeId, @Reference, @ValidUntil,
                     @Subtotal, @TaxAmount, @TotalAmount, @Notes, @CreatedAtUtc, @UpdatedAtUtc,
                     @QuotationContactId, @IsGrandTotalShown)";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@CustomerId", entity.CustomerId));
                command.Parameters.Add(new SqlParameter("@QuotationStatusTypeId", entity.QuotationStatusTypeId));
                command.Parameters.Add(new SqlParameter("@Reference", entity.Reference));
                command.Parameters.Add(new SqlParameter("@ValidUntil", entity.ValidUntil.HasValue ? entity.ValidUntil.Value : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Subtotal", entity.Subtotal));
                command.Parameters.Add(new SqlParameter("@TaxAmount", entity.TaxAmount));
                command.Parameters.Add(new SqlParameter("@TotalAmount", entity.TotalAmount));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc));
                command.Parameters.Add(new SqlParameter("@QuotationContactId", entity.QuotationContactId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsGrandTotalShown", entity.IsGrandTotalShown));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task UpdateAsync(Quotation entity)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[Quotation]
                SET
                    [CustomerId] = @CustomerId,
                    [QuotationStatusTypeId] = @QuotationStatusTypeId,
                    [Reference] = @Reference,
                    [ValidUntil] = @ValidUntil,
                    [Subtotal] = @Subtotal,
                    [TaxAmount] = @TaxAmount,
                    [TotalAmount] = @TotalAmount,
                    [Notes] = @Notes,
                    [QuotationContactId] = @QuotationContactId,
                    [IsGrandTotalShown] = @IsGrandTotalShown,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@CustomerId", entity.CustomerId),
                new SqlParameter("@QuotationStatusTypeId", entity.QuotationStatusTypeId),
                new SqlParameter("@Reference", entity.Reference),
                new SqlParameter("@ValidUntil", entity.ValidUntil.HasValue ? entity.ValidUntil.Value : (object)DBNull.Value),
                new SqlParameter("@Subtotal", entity.Subtotal),
                new SqlParameter("@TaxAmount", entity.TaxAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@QuotationContactId", entity.QuotationContactId ?? (object)DBNull.Value),
                new SqlParameter("@IsGrandTotalShown", entity.IsGrandTotalShown),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int> GetNextSequentialNumberAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*) + 1
                FROM [quotation].[Quotation]
                WHERE [BusinessId] = @BusinessId";

            var result = await _context.Database
                .SqlQueryRaw<int>(query, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task SoftDeleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[Quotation]
                SET [quotation].[Quotation].[IsDeleted] = 1,
                    [quotation].[Quotation].[DeletedAtUtc] = GETUTCDATE(),
                    [quotation].[Quotation].[UpdatedAtUtc] = GETUTCDATE()
                WHERE [quotation].[Quotation].[Id] = @Id
                  AND [quotation].[Quotation].[BusinessId] = @BusinessId
                  AND [quotation].[Quotation].[IsDeleted] = 0";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Quotation?> GetLatestByLeadRequestIdAsync(int leadRequestId)
    {
        try
        {
            const string query = @"
                SELECT TOP 1
                       [quotation].[Quotation].[Id], [quotation].[Quotation].[BusinessId], [quotation].[Quotation].[CustomerId],
                       [quotation].[Quotation].[QuotationStatusTypeId], [quotation].[Quotation].[Reference],
                       [quotation].[Quotation].[ValidUntil], [quotation].[Quotation].[Subtotal],
                       [quotation].[Quotation].[TaxAmount], [quotation].[Quotation].[TotalAmount],
                       [quotation].[Quotation].[Notes], [quotation].[Quotation].[CreatedAtUtc],
                       [quotation].[Quotation].[UpdatedAtUtc], [quotation].[Quotation].[QuotationContactId],
                       [quotation].[Quotation].[IsGrandTotalShown], [quotation].[Quotation].[IsDeleted],
                       [quotation].[Quotation].[DeletedAtUtc], [quotation].[Quotation].[LeadRequestId]
                FROM [quotation].[Quotation]
                WHERE [quotation].[Quotation].[LeadRequestId] = @LeadRequestId
                  AND [quotation].[Quotation].[IsDeleted] = 0
                ORDER BY [quotation].[Quotation].[CreatedAtUtc] DESC";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@LeadRequestId", leadRequestId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<(List<QuotationListDto> Items, int TotalCount)> GetPagedByBusinessIdAsync(
        int businessId,
        int? statusFilter,
        int? customerFilter,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? searchTerm,
        int offset,
        int pageSize)
    {
        try
        {
            const string query = @"
                SELECT [quotation].[Quotation].[Id],
                       [quotation].[Quotation].[Reference],
                       [customer].[Customer].[Name] AS [CustomerName],
                       [quotation].[QuotationStatusType].[Name] AS [StatusName],
                       [quotation].[Quotation].[QuotationStatusTypeId],
                       [quotation].[Quotation].[TotalAmount],
                       [quotation].[Quotation].[ValidUntil],
                       [quotation].[Quotation].[CreatedAtUtc],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [quotation].[Quotation]
                INNER JOIN [customer].[Customer] ON [quotation].[Quotation].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [quotation].[QuotationStatusType] ON [quotation].[Quotation].[QuotationStatusTypeId] = [quotation].[QuotationStatusType].[Id]
                WHERE [quotation].[Quotation].[BusinessId] = @BusinessId
                  AND [quotation].[Quotation].[IsDeleted] = 0
                  AND (@StatusFilter IS NULL OR [quotation].[Quotation].[QuotationStatusTypeId] = @StatusFilter)
                  AND (@CustomerFilter IS NULL OR [quotation].[Quotation].[CustomerId] = @CustomerFilter)
                  AND (@DateFrom IS NULL OR [quotation].[Quotation].[CreatedAtUtc] >= @DateFrom)
                  AND (@DateTo IS NULL OR [quotation].[Quotation].[CreatedAtUtc] <= @DateTo)
                  AND (@SearchTerm IS NULL OR (
                      [quotation].[Quotation].[Reference] LIKE '%' + @SearchTerm + '%'
                      OR [customer].[Customer].[Name] LIKE '%' + @SearchTerm + '%'
                  ))
                ORDER BY [quotation].[Quotation].[CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<QuotationListDto>();
            int totalCount = 0;
            var connection = _context.Database.GetDbConnection();

            // Escape SQL wildcards in search term
            string? escapedSearchTerm = searchTerm != null
                ? searchTerm.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")
                : null;

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@StatusFilter", statusFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CustomerFilter", customerFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@DateFrom", (object?)dateFrom ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@DateTo", (object?)dateTo ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SearchTerm", (object?)escapedSearchTerm ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Offset", offset));
                command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    results.Add(new QuotationListDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Reference = reader.GetString(reader.GetOrdinal("Reference")),
                        CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                        QuotationStatusTypeId = reader.GetInt32(reader.GetOrdinal("QuotationStatusTypeId")),
                        TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                        ValidUntil = reader.IsDBNull(reader.GetOrdinal("ValidUntil"))
                            ? null
                            : DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ValidUntil"))),
                        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return (results, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
