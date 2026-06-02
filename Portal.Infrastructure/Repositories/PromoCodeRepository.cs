using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PromoCode entity operations against the [dbo].[PromoCode] table.
/// </summary>
public class PromoCodeRepository : GenericStoredProcedureRepository<PromoCode>
{
    public PromoCodeRepository(PortalDbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new promo code record and returns the generated Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(PromoCode entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [dbo].[PromoCode]
                    ([Code], [DurationMonths], [MaxRedemptions], [CurrentRedemptions],
                     [ExpiresAtUtc], [BoundEmail], [IsRevoked], [CreatedByUserId], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@Code, @DurationMonths, @MaxRedemptions, @CurrentRedemptions,
                     @ExpiresAtUtc, @BoundEmail, @IsRevoked, @CreatedByUserId, @CreatedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@Code", entity.Code));
                command.Parameters.Add(new SqlParameter("@DurationMonths", entity.DurationMonths));
                command.Parameters.Add(new SqlParameter("@MaxRedemptions", entity.MaxRedemptions));
                command.Parameters.Add(new SqlParameter("@CurrentRedemptions", entity.CurrentRedemptions));
                command.Parameters.Add(new SqlParameter("@ExpiresAtUtc", entity.ExpiresAtUtc));
                command.Parameters.Add(new SqlParameter("@BoundEmail", entity.BoundEmail ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsRevoked", entity.IsRevoked));
                command.Parameters.Add(new SqlParameter("@CreatedByUserId", entity.CreatedByUserId));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a promo code by its database Id.
    /// Returns null if no matching record exists.
    /// </summary>
    public virtual async Task<PromoCode?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [dbo].[PromoCode].[Id],
                       [dbo].[PromoCode].[Code],
                       [dbo].[PromoCode].[DurationMonths],
                       [dbo].[PromoCode].[MaxRedemptions],
                       [dbo].[PromoCode].[CurrentRedemptions],
                       [dbo].[PromoCode].[ExpiresAtUtc],
                       [dbo].[PromoCode].[BoundEmail],
                       [dbo].[PromoCode].[IsRevoked],
                       [dbo].[PromoCode].[CreatedByUserId],
                       [dbo].[PromoCode].[CreatedAtUtc]
                FROM [dbo].[PromoCode]
                WHERE [dbo].[PromoCode].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a promo code by its code value using case-insensitive comparison.
    /// Returns null if no matching code exists.
    /// </summary>
    public virtual async Task<PromoCode?> GetByCodeAsync(string code)
    {
        try
        {
            const string query = @"
                SELECT [dbo].[PromoCode].[Id],
                       [dbo].[PromoCode].[Code],
                       [dbo].[PromoCode].[DurationMonths],
                       [dbo].[PromoCode].[MaxRedemptions],
                       [dbo].[PromoCode].[CurrentRedemptions],
                       [dbo].[PromoCode].[ExpiresAtUtc],
                       [dbo].[PromoCode].[BoundEmail],
                       [dbo].[PromoCode].[IsRevoked],
                       [dbo].[PromoCode].[CreatedByUserId],
                       [dbo].[PromoCode].[CreatedAtUtc]
                FROM [dbo].[PromoCode]
                WHERE UPPER([dbo].[PromoCode].[Code]) = UPPER(@Code)";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Code", code));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks whether a promo code with the given code value already exists.
    /// </summary>
    public virtual async Task<bool> CodeExistsAsync(string code)
    {
        try
        {
            const string query = @"
                SELECT COUNT(1)
                FROM [dbo].[PromoCode]
                WHERE [dbo].[PromoCode].[Code] = @Code";

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

                command.Parameters.Add(new SqlParameter("@Code", code));

                var result = await command.ExecuteScalarAsync();
                return (int)result! > 0;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Revokes a promo code by setting IsRevoked = 1.
    /// Only revokes if the code is not already revoked (IsRevoked = 0).
    /// Returns true if the update was applied.
    /// </summary>
    public virtual async Task<bool> RevokeAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [dbo].[PromoCode]
                SET [IsRevoked] = 1
                WHERE [dbo].[PromoCode].[Id] = @Id
                  AND [dbo].[PromoCode].[IsRevoked] = 0";

            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));

            return rowsAffected > 0;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Atomically increments CurrentRedemptions by 1, but only if CurrentRedemptions is still
    /// less than MaxRedemptions. This is the concurrent redemption guard.
    /// Returns true if the increment was applied (redemption succeeded).
    /// </summary>
    public virtual async Task<bool> IncrementRedemptionsAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [dbo].[PromoCode]
                SET [CurrentRedemptions] = [CurrentRedemptions] + 1
                WHERE [dbo].[PromoCode].[Id] = @Id
                  AND [dbo].[PromoCode].[CurrentRedemptions] < [dbo].[PromoCode].[MaxRedemptions]";

            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));

            return rowsAffected > 0;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a paginated, filtered list of promo codes.
    /// Supports optional status filter (Active, Redeemed, Expired, Revoked).
    /// Results are ordered by CreatedAtUtc descending (newest first).
    /// </summary>
    public virtual async Task<PagedResult<PromoCode>> GetFilteredAsync(PromoCodeFilter filter)
    {
        try
        {
            var statusCondition = GetStatusCondition(filter.Status);

            var countQuery = $@"
                SELECT COUNT(1)
                FROM [dbo].[PromoCode]
                {statusCondition}";

            var dataQuery = $@"
                SELECT [dbo].[PromoCode].[Id],
                       [dbo].[PromoCode].[Code],
                       [dbo].[PromoCode].[DurationMonths],
                       [dbo].[PromoCode].[MaxRedemptions],
                       [dbo].[PromoCode].[CurrentRedemptions],
                       [dbo].[PromoCode].[ExpiresAtUtc],
                       [dbo].[PromoCode].[BoundEmail],
                       [dbo].[PromoCode].[IsRevoked],
                       [dbo].[PromoCode].[CreatedByUserId],
                       [dbo].[PromoCode].[CreatedAtUtc]
                FROM [dbo].[PromoCode]
                {statusCondition}
                ORDER BY [dbo].[PromoCode].[CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                // Execute count query
                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;

                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        countCommand.Transaction = transaction.GetDbTransaction();

                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
                }

                // Compute pagination
                int totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / filter.PageSize) : 0;

                if (totalCount == 0)
                {
                    return new PagedResult<PromoCode>
                    {
                        Items = new List<PromoCode>(),
                        CurrentPage = 1,
                        PageSize = filter.PageSize,
                        TotalCount = 0
                    };
                }

                int page = filter.Page;
                if (page > totalPages)
                    page = totalPages;
                if (page < 1)
                    page = 1;

                int offset = (page - 1) * filter.PageSize;

                // Execute data query
                var results = new List<PromoCode>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;

                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        dataCommand.Transaction = transaction.GetDbTransaction();

                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", filter.PageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(new PromoCode
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Code = reader.GetString(reader.GetOrdinal("Code")),
                            DurationMonths = reader.GetInt32(reader.GetOrdinal("DurationMonths")),
                            MaxRedemptions = reader.GetInt32(reader.GetOrdinal("MaxRedemptions")),
                            CurrentRedemptions = reader.GetInt32(reader.GetOrdinal("CurrentRedemptions")),
                            ExpiresAtUtc = reader.GetDateTime(reader.GetOrdinal("ExpiresAtUtc")),
                            BoundEmail = reader.IsDBNull(reader.GetOrdinal("BoundEmail")) ? null : reader.GetString(reader.GetOrdinal("BoundEmail")),
                            IsRevoked = reader.GetBoolean(reader.GetOrdinal("IsRevoked")),
                            CreatedByUserId = reader.GetString(reader.GetOrdinal("CreatedByUserId")),
                            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
                        });
                    }
                }

                return new PagedResult<PromoCode>
                {
                    Items = results,
                    CurrentPage = page,
                    PageSize = filter.PageSize,
                    TotalCount = totalCount
                };
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Builds the WHERE clause for the status filter based on derived status logic.
    /// Status derivation: Revoked (IsRevoked=1), Redeemed (CurrentRedemptions=MaxRedemptions),
    /// Expired (ExpiresAtUtc in past), Active (otherwise).
    /// </summary>
    private static string GetStatusCondition(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "revoked" => "WHERE [dbo].[PromoCode].[IsRevoked] = 1",
            "redeemed" => "WHERE [dbo].[PromoCode].[IsRevoked] = 0 AND [dbo].[PromoCode].[CurrentRedemptions] = [dbo].[PromoCode].[MaxRedemptions]",
            "expired" => "WHERE [dbo].[PromoCode].[IsRevoked] = 0 AND [dbo].[PromoCode].[CurrentRedemptions] < [dbo].[PromoCode].[MaxRedemptions] AND [dbo].[PromoCode].[ExpiresAtUtc] < GETUTCDATE()",
            "active" => "WHERE [dbo].[PromoCode].[IsRevoked] = 0 AND [dbo].[PromoCode].[CurrentRedemptions] < [dbo].[PromoCode].[MaxRedemptions] AND [dbo].[PromoCode].[ExpiresAtUtc] > GETUTCDATE()",
            _ => ""
        };
    }
}
