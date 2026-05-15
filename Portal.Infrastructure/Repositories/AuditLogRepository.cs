using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for AuditLog entity insert operations against the [audit].[AuditLog] table.
/// Append-only — no UPDATE or DELETE operations.
/// </summary>
public class AuditLogRepository : GenericStoredProcedureRepository<AuditLog>
{
    public AuditLogRepository(DbContext context) : base(context) { }

    public async Task InsertAsync(AuditLog entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [audit].[AuditLog]
                    ([BusinessId], [UserId], [Action], [TableName], [RecordId], [OldValues], [NewValues], [Timestamp])
                VALUES
                    (@BusinessId, @UserId, @Action, @TableName, @RecordId, @OldValues, @NewValues, @Timestamp)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId ?? (object)DBNull.Value),
                new SqlParameter("@UserId", entity.UserId ?? (object)DBNull.Value),
                new SqlParameter("@Action", entity.Action),
                new SqlParameter("@TableName", entity.TableName),
                new SqlParameter("@RecordId", entity.RecordId),
                new SqlParameter("@OldValues", entity.OldValues ?? (object)DBNull.Value),
                new SqlParameter("@NewValues", entity.NewValues ?? (object)DBNull.Value),
                new SqlParameter("@Timestamp", entity.Timestamp)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }
}
