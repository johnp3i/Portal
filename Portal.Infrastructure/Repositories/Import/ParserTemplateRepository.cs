using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Import;

namespace Portal.Infrastructure.Repositories.Import;

/// <summary>
/// Repository for ParserTemplate CRUD operations against the [import].[ParserTemplate] table.
/// </summary>
public class ParserTemplateRepository : GenericStoredProcedureRepository<ParserTemplate>
{
    public ParserTemplateRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Gets all active templates for a supplier (includes managed templates from other sources).
    /// </summary>
    public async Task<List<ParserTemplate>> GetTemplatesForSupplierAsync(int supplierId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [Name], [FileFormatType],
                       [HeaderRow], [DataStartRow], [SheetName], [ColumnMappingsJson],
                       [IsManaged], [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [import].[ParserTemplate]
                WHERE ParserTemplate.SupplierId = @SupplierId
                  AND ParserTemplate.BusinessId = @BusinessId
                  AND ParserTemplate.IsActive = 1
                ORDER BY ParserTemplate.Name";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@SupplierId", supplierId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all active templates for a business (all suppliers).
    /// </summary>
    public async Task<List<ParserTemplate>> GetAllForBusinessAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [Name], [FileFormatType],
                       [HeaderRow], [DataStartRow], [SheetName], [ColumnMappingsJson],
                       [IsManaged], [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [import].[ParserTemplate]
                WHERE ParserTemplate.BusinessId = @BusinessId
                  AND ParserTemplate.IsActive = 1
                ORDER BY ParserTemplate.SupplierId, ParserTemplate.Name";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single template by Id.
    /// </summary>
    public async Task<ParserTemplate?> GetByIdAsync(int templateId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [Name], [FileFormatType],
                       [HeaderRow], [DataStartRow], [SheetName], [ColumnMappingsJson],
                       [IsManaged], [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [import].[ParserTemplate]
                WHERE ParserTemplate.Id = @Id
                  AND ParserTemplate.BusinessId = @BusinessId
                  AND ParserTemplate.IsActive = 1";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", templateId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new parser template and returns the generated Id.
    /// </summary>
    public async Task<int> InsertAsync(ParserTemplate entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [import].[ParserTemplate]
                    ([BusinessId], [SupplierId], [Name], [FileFormatType],
                     [HeaderRow], [DataStartRow], [SheetName], [ColumnMappingsJson], [IsManaged])
                VALUES
                    (@BusinessId, @SupplierId, @Name, @FileFormatType,
                     @HeaderRow, @DataStartRow, @SheetName, @ColumnMappingsJson, @IsManaged);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@FileFormatType", entity.FileFormatType),
                new SqlParameter("@HeaderRow", entity.HeaderRow),
                new SqlParameter("@DataStartRow", entity.DataStartRow),
                new SqlParameter("@SheetName", entity.SheetName ?? (object)DBNull.Value),
                new SqlParameter("@ColumnMappingsJson", entity.ColumnMappingsJson),
                new SqlParameter("@IsManaged", entity.IsManaged)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates an existing parser template.
    /// </summary>
    public async Task UpdateAsync(ParserTemplate entity)
    {
        try
        {
            const string query = @"
                UPDATE [import].[ParserTemplate]
                SET [Name] = @Name,
                    [FileFormatType] = @FileFormatType,
                    [HeaderRow] = @HeaderRow,
                    [DataStartRow] = @DataStartRow,
                    [SheetName] = @SheetName,
                    [ColumnMappingsJson] = @ColumnMappingsJson,
                    [UpdatedAtUtc] = GETUTCDATE()
                WHERE ParserTemplate.Id = @Id
                  AND ParserTemplate.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@FileFormatType", entity.FileFormatType),
                new SqlParameter("@HeaderRow", entity.HeaderRow),
                new SqlParameter("@DataStartRow", entity.DataStartRow),
                new SqlParameter("@SheetName", entity.SheetName ?? (object)DBNull.Value),
                new SqlParameter("@ColumnMappingsJson", entity.ColumnMappingsJson));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Soft-deletes a template by setting IsActive = 0.
    /// </summary>
    public async Task DeleteAsync(int templateId, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [import].[ParserTemplate]
                SET [IsActive] = 0,
                    [UpdatedAtUtc] = GETUTCDATE()
                WHERE ParserTemplate.Id = @Id
                  AND ParserTemplate.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", templateId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
