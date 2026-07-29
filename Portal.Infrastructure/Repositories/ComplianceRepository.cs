using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for all compliance module data access operations.
/// Handles ApplicationCategory, ApplicationType, BusinessApplication, and ApplicationAttachment entities.
/// Schema: [compliance]
/// </summary>
public class ComplianceRepository : GenericStoredProcedureRepository<BusinessApplication>
{
    public ComplianceRepository(DbContext context) : base(context) { }

    #region Category Methods

    /// <summary>
    /// Gets all active application categories ordered by name.
    /// </summary>
    public async Task<List<ApplicationCategory>> GetAllCategoriesAsync()
    {
        try
        {
            var results = new List<ApplicationCategory>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Description], [IsActive], [CreatedAtUtc]
                    FROM [compliance].[ApplicationCategory]
                    WHERE [compliance].[ApplicationCategory].[IsActive] = 1
                    ORDER BY [compliance].[ApplicationCategory].[Name]";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapApplicationCategory(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new application category and returns the generated Id.
    /// </summary>
    public async Task<int> InsertCategoryAsync(ApplicationCategory entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [compliance].[ApplicationCategory]
                    ([Name], [Description])
                VALUES
                    (@Name, @Description);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates an existing application category's Name and Description.
    /// </summary>
    public async Task UpdateCategoryAsync(ApplicationCategory entity)
    {
        try
        {
            const string query = @"
                UPDATE [compliance].[ApplicationCategory]
                SET [Name] = @Name,
                    [Description] = @Description
                WHERE [compliance].[ApplicationCategory].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Type Methods

    /// <summary>
    /// Gets all active application types ordered by name.
    /// </summary>
    public async Task<List<ApplicationType>> GetAllTypesAsync()
    {
        try
        {
            var results = new List<ApplicationType>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Description], [Country], [ApplicationCategoryId],
                           [Frequency], [DefaultDueMonth], [DefaultDueDay], [IsActive], [CreatedAtUtc]
                    FROM [compliance].[ApplicationType]
                    ORDER BY [compliance].[ApplicationType].[Name]";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapApplicationType(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new application type and returns the generated Id.
    /// </summary>
    public async Task<int> InsertTypeAsync(ApplicationType entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [compliance].[ApplicationType]
                    ([Name], [Description], [Country], [ApplicationCategoryId],
                     [Frequency], [DefaultDueMonth], [DefaultDueDay], [IsActive])
                VALUES
                    (@Name, @Description, @Country, @ApplicationCategoryId,
                     @Frequency, @DefaultDueMonth, @DefaultDueDay, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Country", entity.Country),
                new SqlParameter("@ApplicationCategoryId", entity.ApplicationCategoryId),
                new SqlParameter("@Frequency", entity.Frequency),
                new SqlParameter("@DefaultDueMonth", entity.DefaultDueMonth ?? (object)DBNull.Value),
                new SqlParameter("@DefaultDueDay", entity.DefaultDueDay ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates an existing application type.
    /// </summary>
    public async Task UpdateTypeAsync(ApplicationType entity)
    {
        try
        {
            const string query = @"
                UPDATE [compliance].[ApplicationType]
                SET [Name] = @Name,
                    [Description] = @Description,
                    [Country] = @Country,
                    [ApplicationCategoryId] = @ApplicationCategoryId,
                    [Frequency] = @Frequency,
                    [DefaultDueMonth] = @DefaultDueMonth,
                    [DefaultDueDay] = @DefaultDueDay
                WHERE [compliance].[ApplicationType].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Country", entity.Country),
                new SqlParameter("@ApplicationCategoryId", entity.ApplicationCategoryId),
                new SqlParameter("@Frequency", entity.Frequency),
                new SqlParameter("@DefaultDueMonth", entity.DefaultDueMonth ?? (object)DBNull.Value),
                new SqlParameter("@DefaultDueDay", entity.DefaultDueDay ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deactivates an application type by setting IsActive = 0.
    /// </summary>
    public async Task DeactivateTypeAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [compliance].[ApplicationType]
                SET [IsActive] = 0
                WHERE [compliance].[ApplicationType].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Reactivates an application type by setting IsActive = 1.
    /// </summary>
    public async Task ActivateTypeAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [compliance].[ApplicationType]
                SET [IsActive] = 1
                WHERE [compliance].[ApplicationType].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if an application type with the given name and country already exists.
    /// Optionally excludes a specific Id (for update scenarios).
    /// </summary>
    public async Task<bool> TypeExistsAsync(string name, string country, int? excludeId)
    {
        try
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@Name", name),
                new SqlParameter("@Country", country)
            };

            var query = @"
                SELECT COUNT(*)
                FROM [compliance].[ApplicationType]
                WHERE [compliance].[ApplicationType].[Name] = @Name
                  AND [compliance].[ApplicationType].[Country] = @Country
                  AND [compliance].[ApplicationType].[IsActive] = 1";

            if (excludeId.HasValue)
            {
                query += " AND [compliance].[ApplicationType].[Id] != @ExcludeId";
                parameters.Add(new SqlParameter("@ExcludeId", excludeId.Value));
            }

            var result = await _context.Database.SqlQueryRaw<int>(query, parameters.ToArray()).ToListAsync();
            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single application type by Id.
    /// </summary>
    public async Task<ApplicationType?> GetApplicationTypeByIdAsync(int id)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [Name], [Description], [Country], [ApplicationCategoryId],
                           [Frequency], [DefaultDueMonth], [DefaultDueDay], [IsActive], [CreatedAtUtc]
                    FROM [compliance].[ApplicationType]
                    WHERE [compliance].[ApplicationType].[Id] = @Id";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Id", id));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapApplicationType(reader);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets multiple application types by their Ids.
    /// </summary>
    public async Task<List<ApplicationType>> GetApplicationTypesByIdsAsync(int[] ids)
    {
        try
        {
            if (ids == null || ids.Length == 0)
                return new List<ApplicationType>();

            var results = new List<ApplicationType>();
            var parameters = new List<SqlParameter>();
            var idParams = new List<string>();

            for (var i = 0; i < ids.Length; i++)
            {
                var paramName = $"@Id{i}";
                idParams.Add(paramName);
                parameters.Add(new SqlParameter(paramName, ids[i]));
            }

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = $@"
                    SELECT [Id], [Name], [Description], [Country], [ApplicationCategoryId],
                           [Frequency], [DefaultDueMonth], [DefaultDueDay], [IsActive], [CreatedAtUtc]
                    FROM [compliance].[ApplicationType]
                    WHERE [compliance].[ApplicationType].[Id] IN ({string.Join(", ", idParams)})";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                foreach (var param in parameters)
                    command.Parameters.Add(param);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapApplicationType(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Business Application Methods

    /// <summary>
    /// Gets a paginated list of business applications with optional filtering.
    /// Returns items and total count for pagination.
    /// </summary>
    public async Task<(List<BusinessApplication> Items, int TotalCount)> GetPagedAsync(
        int businessId, string? category, string? status,
        DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)
    {
        try
        {
            int offset = (page - 1) * pageSize;

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize)
            };

            var whereClause = "[compliance].[BusinessApplication].[BusinessId] = @BusinessId";

            if (!string.IsNullOrEmpty(category))
            {
                whereClause += " AND [compliance].[ApplicationCategory].[Name] = @Category";
                parameters.Add(new SqlParameter("@Category", category));
            }

            if (!string.IsNullOrEmpty(status))
            {
                whereClause += " AND [compliance].[BusinessApplication].[Status] = @Status";
                parameters.Add(new SqlParameter("@Status", status));
            }

            if (dateFrom.HasValue)
            {
                whereClause += " AND [compliance].[BusinessApplication].[DueDate] >= @DateFrom";
                parameters.Add(new SqlParameter("@DateFrom", dateFrom.Value));
            }

            if (dateTo.HasValue)
            {
                whereClause += " AND [compliance].[BusinessApplication].[DueDate] <= @DateTo";
                parameters.Add(new SqlParameter("@DateTo", dateTo.Value));
            }

            var query = $@"
                SELECT [compliance].[BusinessApplication].[Id],
                       [compliance].[BusinessApplication].[BusinessId],
                       [compliance].[BusinessApplication].[ApplicationTypeId],
                       [compliance].[BusinessApplication].[DueDate],
                       [compliance].[BusinessApplication].[Status],
                       [compliance].[BusinessApplication].[ReferenceNumber],
                       [compliance].[BusinessApplication].[Notes],
                       [compliance].[BusinessApplication].[SubmittedAtUtc],
                       [compliance].[BusinessApplication].[ApprovedAtUtc],
                       [compliance].[BusinessApplication].[CreatedAtUtc],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [compliance].[BusinessApplication]
                INNER JOIN [compliance].[ApplicationType]
                    ON [compliance].[BusinessApplication].[ApplicationTypeId] = [compliance].[ApplicationType].[Id]
                INNER JOIN [compliance].[ApplicationCategory]
                    ON [compliance].[ApplicationType].[ApplicationCategoryId] = [compliance].[ApplicationCategory].[Id]
                WHERE {whereClause}
                ORDER BY [compliance].[BusinessApplication].[DueDate] ASC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<BusinessApplication>();
            int totalCount = 0;
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

                foreach (var param in parameters)
                    command.Parameters.Add(param);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    results.Add(MapBusinessApplication(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return (results, totalCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single business application by Id scoped to a business.
    /// </summary>
    public async Task<BusinessApplication?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [compliance].[BusinessApplication].[Id],
                       [compliance].[BusinessApplication].[BusinessId],
                       [compliance].[BusinessApplication].[ApplicationTypeId],
                       [compliance].[BusinessApplication].[DueDate],
                       [compliance].[BusinessApplication].[Status],
                       [compliance].[BusinessApplication].[ReferenceNumber],
                       [compliance].[BusinessApplication].[Notes],
                       [compliance].[BusinessApplication].[SubmittedAtUtc],
                       [compliance].[BusinessApplication].[ApprovedAtUtc],
                       [compliance].[BusinessApplication].[CreatedAtUtc]
                FROM [compliance].[BusinessApplication]
                WHERE [compliance].[BusinessApplication].[Id] = @Id
                  AND [compliance].[BusinessApplication].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a batch of business applications.
    /// </summary>
    public async Task InsertBatchAsync(List<BusinessApplication> entities)
    {
        try
        {
            const string query = @"
                INSERT INTO [compliance].[BusinessApplication]
                    ([BusinessId], [ApplicationTypeId], [DueDate], [Status],
                     [ReferenceNumber], [Notes], [SubmittedAtUtc], [ApprovedAtUtc])
                VALUES
                    (@BusinessId, @ApplicationTypeId, @DueDate, @Status,
                     @ReferenceNumber, @Notes, @SubmittedAtUtc, @ApprovedAtUtc)";

            foreach (var entity in entities)
            {
                await _context.Database.ExecuteSqlRawAsync(query,
                    new SqlParameter("@BusinessId", entity.BusinessId),
                    new SqlParameter("@ApplicationTypeId", entity.ApplicationTypeId),
                    new SqlParameter("@DueDate", entity.DueDate),
                    new SqlParameter("@Status", entity.Status),
                    new SqlParameter("@ReferenceNumber", entity.ReferenceNumber ?? (object)DBNull.Value),
                    new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                    new SqlParameter("@SubmittedAtUtc", entity.SubmittedAtUtc ?? (object)DBNull.Value),
                    new SqlParameter("@ApprovedAtUtc", entity.ApprovedAtUtc ?? (object)DBNull.Value));
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a single business application and returns the generated Id.
    /// </summary>
    public async Task<int> InsertSingleAsync(BusinessApplication entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [compliance].[BusinessApplication]
                    ([BusinessId], [ApplicationTypeId], [DueDate], [Status],
                     [ReferenceNumber], [Notes], [SubmittedAtUtc], [ApprovedAtUtc])
                VALUES
                    (@BusinessId, @ApplicationTypeId, @DueDate, @Status,
                     @ReferenceNumber, @Notes, @SubmittedAtUtc, @ApprovedAtUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@ApplicationTypeId", entity.ApplicationTypeId),
                new SqlParameter("@DueDate", entity.DueDate),
                new SqlParameter("@Status", entity.Status),
                new SqlParameter("@ReferenceNumber", entity.ReferenceNumber ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@SubmittedAtUtc", entity.SubmittedAtUtc ?? (object)DBNull.Value),
                new SqlParameter("@ApprovedAtUtc", entity.ApprovedAtUtc ?? (object)DBNull.Value)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the status and optional timestamps of a business application.
    /// </summary>
    public async Task UpdateStatusAsync(int id, string status, DateTime? submittedAtUtc, DateTime? approvedAtUtc)
    {
        try
        {
            const string query = @"
                UPDATE [compliance].[BusinessApplication]
                SET [Status] = @Status,
                    [SubmittedAtUtc] = @SubmittedAtUtc,
                    [ApprovedAtUtc] = @ApprovedAtUtc
                WHERE [compliance].[BusinessApplication].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@Status", status),
                new SqlParameter("@SubmittedAtUtc", submittedAtUtc ?? (object)DBNull.Value),
                new SqlParameter("@ApprovedAtUtc", approvedAtUtc ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the reference number and notes of a business application.
    /// </summary>
    public async Task UpdateDetailsAsync(int id, string? referenceNumber, string? notes)
    {
        try
        {
            const string query = @"
                UPDATE [compliance].[BusinessApplication]
                SET [ReferenceNumber] = @ReferenceNumber,
                    [Notes] = @Notes
                WHERE [compliance].[BusinessApplication].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@ReferenceNumber", referenceNumber ?? (object)DBNull.Value),
                new SqlParameter("@Notes", notes ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if a business application already exists for a given type and year.
    /// </summary>
    public async Task<bool> ExistsForTypeAndPeriodAsync(int businessId, int typeId, int year)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [compliance].[BusinessApplication]
                WHERE [compliance].[BusinessApplication].[BusinessId] = @BusinessId
                  AND [compliance].[BusinessApplication].[ApplicationTypeId] = @TypeId
                  AND YEAR([compliance].[BusinessApplication].[DueDate]) = @Year";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@TypeId", typeId),
                new SqlParameter("@Year", year)
            ).ToListAsync();

            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Dashboard & Calendar Methods

    /// <summary>
    /// Gets upcoming business applications (pending/in-progress) within a date range.
    /// </summary>
    public async Task<List<BusinessApplication>> GetUpcomingAsync(int businessId, int days, int maxItems)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var endDate = today.AddDays(days);

            const string query = @"
                SELECT TOP (@MaxItems)
                       [compliance].[BusinessApplication].[Id],
                       [compliance].[BusinessApplication].[BusinessId],
                       [compliance].[BusinessApplication].[ApplicationTypeId],
                       [compliance].[BusinessApplication].[DueDate],
                       [compliance].[BusinessApplication].[Status],
                       [compliance].[BusinessApplication].[ReferenceNumber],
                       [compliance].[BusinessApplication].[Notes],
                       [compliance].[BusinessApplication].[SubmittedAtUtc],
                       [compliance].[BusinessApplication].[ApprovedAtUtc],
                       [compliance].[BusinessApplication].[CreatedAtUtc]
                FROM [compliance].[BusinessApplication]
                WHERE [compliance].[BusinessApplication].[BusinessId] = @BusinessId
                  AND [compliance].[BusinessApplication].[Status] IN ('Pending', 'InProgress')
                  AND [compliance].[BusinessApplication].[DueDate] BETWEEN @Today AND @EndDate
                ORDER BY [compliance].[BusinessApplication].[DueDate] ASC";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@MaxItems", maxItems),
                new SqlParameter("@Today", today),
                new SqlParameter("@EndDate", endDate));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all business applications for a given business and year (calendar view).
    /// </summary>
    public async Task<List<BusinessApplication>> GetCalendarAsync(int businessId, int year)
    {
        try
        {
            const string query = @"
                SELECT [compliance].[BusinessApplication].[Id],
                       [compliance].[BusinessApplication].[BusinessId],
                       [compliance].[BusinessApplication].[ApplicationTypeId],
                       [compliance].[BusinessApplication].[DueDate],
                       [compliance].[BusinessApplication].[Status],
                       [compliance].[BusinessApplication].[ReferenceNumber],
                       [compliance].[BusinessApplication].[Notes],
                       [compliance].[BusinessApplication].[SubmittedAtUtc],
                       [compliance].[BusinessApplication].[ApprovedAtUtc],
                       [compliance].[BusinessApplication].[CreatedAtUtc]
                FROM [compliance].[BusinessApplication]
                WHERE [compliance].[BusinessApplication].[BusinessId] = @BusinessId
                  AND YEAR([compliance].[BusinessApplication].[DueDate]) = @Year
                ORDER BY [compliance].[BusinessApplication].[DueDate] ASC";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Year", year));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Attachment Methods

    /// <summary>
    /// Inserts a new application attachment and returns the generated Id.
    /// </summary>
    public async Task<int> InsertAttachmentAsync(ApplicationAttachment entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [compliance].[ApplicationAttachment]
                    ([BusinessApplicationId], [FileName], [OriginalFileName],
                     [FilePath], [ContentType], [FileSizeBytes], [UploadedByUserId])
                VALUES
                    (@BusinessApplicationId, @FileName, @OriginalFileName,
                     @FilePath, @ContentType, @FileSizeBytes, @UploadedByUserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessApplicationId", entity.BusinessApplicationId),
                new SqlParameter("@FileName", entity.FileName),
                new SqlParameter("@OriginalFileName", entity.OriginalFileName),
                new SqlParameter("@FilePath", entity.FilePath),
                new SqlParameter("@ContentType", entity.ContentType),
                new SqlParameter("@FileSizeBytes", entity.FileSizeBytes),
                new SqlParameter("@UploadedByUserId", entity.UploadedByUserId)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single attachment by Id with business ownership validation via BusinessApplication join.
    /// </summary>
    public async Task<ApplicationAttachment?> GetAttachmentByIdAsync(int id, int businessId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [compliance].[ApplicationAttachment].[Id],
                           [compliance].[ApplicationAttachment].[BusinessApplicationId],
                           [compliance].[ApplicationAttachment].[FileName],
                           [compliance].[ApplicationAttachment].[OriginalFileName],
                           [compliance].[ApplicationAttachment].[FilePath],
                           [compliance].[ApplicationAttachment].[ContentType],
                           [compliance].[ApplicationAttachment].[FileSizeBytes],
                           [compliance].[ApplicationAttachment].[UploadedByUserId],
                           [compliance].[ApplicationAttachment].[CreatedAtUtc]
                    FROM [compliance].[ApplicationAttachment]
                    INNER JOIN [compliance].[BusinessApplication]
                        ON [compliance].[ApplicationAttachment].[BusinessApplicationId] = [compliance].[BusinessApplication].[Id]
                    WHERE [compliance].[ApplicationAttachment].[Id] = @Id
                      AND [compliance].[BusinessApplication].[BusinessId] = @BusinessId";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Id", id));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapApplicationAttachment(reader);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes an attachment by Id.
    /// </summary>
    public async Task DeleteAttachmentAsync(int id)
    {
        try
        {
            const string query = @"
                DELETE FROM [compliance].[ApplicationAttachment]
                WHERE [compliance].[ApplicationAttachment].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the count of attachments for a specific business application.
    /// </summary>
    public async Task<int> GetAttachmentCountAsync(int applicationId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [compliance].[ApplicationAttachment]
                WHERE [compliance].[ApplicationAttachment].[BusinessApplicationId] = @ApplicationId";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@ApplicationId", applicationId)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all attachments for a specific business application, ordered by most recent first.
    /// </summary>
    public async Task<List<ApplicationAttachment>> GetAttachmentsForApplicationAsync(int applicationId)
    {
        try
        {
            var results = new List<ApplicationAttachment>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT [Id], [BusinessApplicationId], [FileName], [OriginalFileName],
                           [FilePath], [ContentType], [FileSizeBytes], [UploadedByUserId], [CreatedAtUtc]
                    FROM [compliance].[ApplicationAttachment]
                    WHERE [compliance].[ApplicationAttachment].[BusinessApplicationId] = @ApplicationId
                    ORDER BY [compliance].[ApplicationAttachment].[CreatedAtUtc] DESC";

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@ApplicationId", applicationId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(MapApplicationAttachment(reader));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Private Mapping Methods

    private static ApplicationCategory MapApplicationCategory(DbDataReader reader)
    {
        return new ApplicationCategory
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static ApplicationType MapApplicationType(DbDataReader reader)
    {
        return new ApplicationType
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Country = reader.GetString(reader.GetOrdinal("Country")),
            ApplicationCategoryId = reader.GetInt32(reader.GetOrdinal("ApplicationCategoryId")),
            Frequency = reader.GetString(reader.GetOrdinal("Frequency")),
            DefaultDueMonth = reader.IsDBNull(reader.GetOrdinal("DefaultDueMonth")) ? null : reader.GetInt32(reader.GetOrdinal("DefaultDueMonth")),
            DefaultDueDay = reader.IsDBNull(reader.GetOrdinal("DefaultDueDay")) ? null : reader.GetInt32(reader.GetOrdinal("DefaultDueDay")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static BusinessApplication MapBusinessApplication(DbDataReader reader)
    {
        return new BusinessApplication
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
            ApplicationTypeId = reader.GetInt32(reader.GetOrdinal("ApplicationTypeId")),
            DueDate = reader.GetDateTime(reader.GetOrdinal("DueDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            ReferenceNumber = reader.IsDBNull(reader.GetOrdinal("ReferenceNumber")) ? null : reader.GetString(reader.GetOrdinal("ReferenceNumber")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            SubmittedAtUtc = reader.IsDBNull(reader.GetOrdinal("SubmittedAtUtc")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAtUtc")),
            ApprovedAtUtc = reader.IsDBNull(reader.GetOrdinal("ApprovedAtUtc")) ? null : reader.GetDateTime(reader.GetOrdinal("ApprovedAtUtc")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    private static ApplicationAttachment MapApplicationAttachment(DbDataReader reader)
    {
        return new ApplicationAttachment
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            BusinessApplicationId = reader.GetInt32(reader.GetOrdinal("BusinessApplicationId")),
            FileName = reader.GetString(reader.GetOrdinal("FileName")),
            OriginalFileName = reader.GetString(reader.GetOrdinal("OriginalFileName")),
            FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
            ContentType = reader.GetString(reader.GetOrdinal("ContentType")),
            FileSizeBytes = reader.GetInt64(reader.GetOrdinal("FileSizeBytes")),
            UploadedByUserId = reader.GetString(reader.GetOrdinal("UploadedByUserId")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }

    #endregion
}
