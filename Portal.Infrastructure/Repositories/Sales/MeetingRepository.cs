using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[Meeting] entity CRUD operations.
/// </summary>
public class MeetingRepository : GenericStoredProcedureRepository<Meeting>
{
    public MeetingRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(Meeting entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[Meeting]
                    ([BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                     [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                     [Notes], [IsCancelled], [IsActive], [CreatedByUserId], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @LeadRequestId, @ContactId, @MeetingTypeId,
                     @Subject, @ScheduledAtUtc, @DurationMinutes, @Location,
                     @Notes, 0, 1, @CreatedByUserId, @CreatedAtUtc);
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
                command.Parameters.Add(new SqlParameter("@LeadRequestId", entity.LeadRequestId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ContactId", entity.ContactId));
                command.Parameters.Add(new SqlParameter("@MeetingTypeId", entity.MeetingTypeId));
                command.Parameters.Add(new SqlParameter("@Subject", entity.Subject));
                command.Parameters.Add(new SqlParameter("@ScheduledAtUtc", entity.ScheduledAtUtc));
                command.Parameters.Add(new SqlParameter("@DurationMinutes", entity.DurationMinutes));
                command.Parameters.Add(new SqlParameter("@Location", entity.Location ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedByUserId", entity.CreatedByUserId));
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

    public async Task UpdateAsync(Meeting entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Meeting]
                SET [MeetingTypeId] = @MeetingTypeId,
                    [Subject] = @Subject,
                    [ScheduledAtUtc] = @ScheduledAtUtc,
                    [DurationMinutes] = @DurationMinutes,
                    [Location] = @Location,
                    [Notes] = @Notes,
                    [Outcome] = @Outcome
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@MeetingTypeId", entity.MeetingTypeId),
                new SqlParameter("@Subject", entity.Subject),
                new SqlParameter("@ScheduledAtUtc", entity.ScheduledAtUtc),
                new SqlParameter("@DurationMinutes", entity.DurationMinutes),
                new SqlParameter("@Location", entity.Location ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@Outcome", entity.Outcome ?? (object)DBNull.Value)
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
                UPDATE [sales].[Meeting]
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
                UPDATE [sales].[Meeting]
                SET [IsCancelled] = 0,
                    [CancellationTimestamp] = NULL,
                    [CancellationDescription] = NULL
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

    public async Task<Meeting?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[Meeting]
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

    public async Task<List<Meeting>> GetByLeadRequestIdAsync(int leadRequestId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[Meeting]
                WHERE [LeadRequestId] = @LeadRequestId
                  AND [BusinessId] = @BusinessId
                  AND [IsCancelled] = 0
                  AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@BusinessId", businessId));
            return results.OrderByDescending(x => x.ScheduledAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<Meeting?> GetUpcomingByLeadRequestIdAsync(int leadRequestId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT TOP 1
                       [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[Meeting]
                WHERE [LeadRequestId] = @LeadRequestId
                  AND [BusinessId] = @BusinessId
                  AND [IsCancelled] = 0
                  AND [IsActive] = 1
                  AND [ScheduledAtUtc] > GETUTCDATE()
                ORDER BY [ScheduledAtUtc] ASC";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<Meeting>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[Meeting]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
            return results.OrderByDescending(x => x.ScheduledAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
