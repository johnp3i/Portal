using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ProposalSection entity CRUD operations against the [quotation].[ProposalSection] table.
/// </summary>
public class ProposalSectionRepository : GenericStoredProcedureRepository<ProposalSection>
{
    public ProposalSectionRepository(DbContext context) : base(context) { }

    public async Task<List<ProposalSection>> GetByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes], [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown]
                FROM [quotation].[ProposalSection]
                WHERE [QuotationId] = @QuotationId
                ORDER BY [SortOrder]";

            return await ExecuteStoredProcedure(query, new SqlParameter("@QuotationId", quotationId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns a single ProposalSection by its ID.
    /// </summary>
    public async Task<ProposalSection?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes], [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown]
                FROM [quotation].[ProposalSection]
                WHERE [quotation].[ProposalSection].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(ProposalSection entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[ProposalSection]
                    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes], [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown])
                VALUES
                    (@QuotationId, @Name, @SortOrder, @ColumnConfiguration, @Description, @Notes, @SectionType, @IsEmphasized, @AccentColor, @Label, @IsTotalsTableShown)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@QuotationId", entity.QuotationId),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@SortOrder", entity.SortOrder),
                new SqlParameter("@ColumnConfiguration", entity.ColumnConfiguration),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@SectionType", entity.SectionType),
                new SqlParameter("@IsEmphasized", entity.IsEmphasized),
                new SqlParameter("@AccentColor", entity.AccentColor ?? (object)DBNull.Value),
                new SqlParameter("@Label", entity.Label ?? (object)DBNull.Value),
                new SqlParameter("@IsTotalsTableShown", entity.IsTotalsTableShown)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int> InsertAndReturnIdAsync(ProposalSection entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[ProposalSection]
                    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes], [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown])
                OUTPUT INSERTED.Id
                VALUES
                    (@QuotationId, @Name, @SortOrder, @ColumnConfiguration, @Description, @Notes, @SectionType, @IsEmphasized, @AccentColor, @Label, @IsTotalsTableShown)";

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

                command.Parameters.Add(new SqlParameter("@QuotationId", entity.QuotationId));
                command.Parameters.Add(new SqlParameter("@Name", entity.Name));
                command.Parameters.Add(new SqlParameter("@SortOrder", entity.SortOrder));
                command.Parameters.Add(new SqlParameter("@ColumnConfiguration", entity.ColumnConfiguration));
                command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SectionType", entity.SectionType));
                command.Parameters.Add(new SqlParameter("@IsEmphasized", entity.IsEmphasized));
                command.Parameters.Add(new SqlParameter("@AccentColor", entity.AccentColor ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Label", entity.Label ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsTotalsTableShown", entity.IsTotalsTableShown));

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

    public async Task UpdateAsync(ProposalSection entity)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[ProposalSection]
                SET
                    [Name] = @Name,
                    [SortOrder] = @SortOrder,
                    [ColumnConfiguration] = @ColumnConfiguration,
                    [Description] = @Description,
                    [Notes] = @Notes,
                    [SectionType] = @SectionType,
                    [IsEmphasized] = @IsEmphasized,
                    [AccentColor] = @AccentColor,
                    [Label] = @Label,
                    [IsTotalsTableShown] = @IsTotalsTableShown
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@SortOrder", entity.SortOrder),
                new SqlParameter("@ColumnConfiguration", entity.ColumnConfiguration),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@SectionType", entity.SectionType),
                new SqlParameter("@IsEmphasized", entity.IsEmphasized),
                new SqlParameter("@AccentColor", entity.AccentColor ?? (object)DBNull.Value),
                new SqlParameter("@Label", entity.Label ?? (object)DBNull.Value),
                new SqlParameter("@IsTotalsTableShown", entity.IsTotalsTableShown)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            const string query = @"
                DELETE FROM [quotation].[ProposalSection]
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
