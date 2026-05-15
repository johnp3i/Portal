using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for InvoiceSection entity CRUD operations against the [invoice].[InvoiceSection] table.
/// </summary>
public class InvoiceSectionRepository : GenericStoredProcedureRepository<InvoiceSection>
{
    public InvoiceSectionRepository(DbContext context) : base(context) { }

    public async Task<List<InvoiceSection>> GetByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [Name], [SortOrder], [ColumnConfiguration], [SectionType],
                       [Description], [Notes], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown]
                FROM [invoice].[InvoiceSection]
                WHERE [InvoiceId] = @InvoiceId
                ORDER BY [SortOrder]";

            return await ExecuteStoredProcedure(query, new SqlParameter("@InvoiceId", invoiceId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<InvoiceSection?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [Name], [SortOrder], [ColumnConfiguration], [SectionType],
                       [Description], [Notes], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown]
                FROM [invoice].[InvoiceSection]
                WHERE [invoice].[InvoiceSection].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int> InsertAsync(InvoiceSection entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [invoice].[InvoiceSection]
                    ([InvoiceId], [Name], [SortOrder], [ColumnConfiguration], [SectionType],
                     [Description], [Notes], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown])
                OUTPUT INSERTED.Id
                VALUES
                    (@InvoiceId, @Name, @SortOrder, @ColumnConfiguration, @SectionType,
                     @Description, @Notes, @IsEmphasized, @AccentColor, @Label, @IsTotalsTableShown)";

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

                command.Parameters.Add(new SqlParameter("@InvoiceId", entity.InvoiceId));
                command.Parameters.Add(new SqlParameter("@Name", entity.Name));
                command.Parameters.Add(new SqlParameter("@SortOrder", entity.SortOrder));
                command.Parameters.Add(new SqlParameter("@ColumnConfiguration", entity.ColumnConfiguration));
                command.Parameters.Add(new SqlParameter("@SectionType", entity.SectionType));
                command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsEmphasized", entity.IsEmphasized));
                command.Parameters.Add(new SqlParameter("@AccentColor", entity.AccentColor ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Label", entity.Label ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsTotalsTableShown", entity.IsTotalsTableShown));

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

    public async Task<List<int>> BulkInsertAsync(List<InvoiceSection> sections)
    {
        try
        {
            var insertedIds = new List<int>();

            foreach (var section in sections)
            {
                var id = await InsertAsync(section);
                insertedIds.Add(id);
            }

            return insertedIds;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(InvoiceSection entity)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[InvoiceSection]
                SET
                    [Name] = @Name,
                    [SortOrder] = @SortOrder,
                    [ColumnConfiguration] = @ColumnConfiguration,
                    [SectionType] = @SectionType,
                    [Description] = @Description,
                    [Notes] = @Notes,
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
                new SqlParameter("@SectionType", entity.SectionType),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
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
                DELETE FROM [invoice].[InvoiceSection]
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateSortOrdersAsync(List<(int Id, int SortOrder)> updates)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[InvoiceSection]
                SET [SortOrder] = @SortOrder
                WHERE [Id] = @Id";

            foreach (var (id, sortOrder) in updates)
            {
                await _context.Database.ExecuteSqlRawAsync(query,
                    new SqlParameter("@Id", id),
                    new SqlParameter("@SortOrder", sortOrder)
                );
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
