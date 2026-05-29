using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for InvoiceLine entity CRUD operations against the [invoice].[InvoiceLine] table.
/// </summary>
public class InvoiceLineRepository : GenericStoredProcedureRepository<InvoiceLine>
{
    public InvoiceLineRepository(DbContext context) : base(context) { }

    public async Task<List<InvoiceLine>> GetByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [Description], [Quantity], [UnitPrice], [VatRate],
                       [Discount], [DiscountType], [CostPrice], [LineTotal], [SortOrder],
                       [ReferenceUrl], [Subtitle], [InvoiceSectionId], [ProductCode],
                       [IsReverseCharge], [ProductTypeId]
                FROM [invoice].[InvoiceLine]
                WHERE [InvoiceId] = @InvoiceId
                ORDER BY [SortOrder]";

            return await ExecuteStoredProcedure(query, new SqlParameter("@InvoiceId", invoiceId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<InvoiceLine?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceId], [Description], [Quantity], [UnitPrice], [VatRate],
                       [Discount], [DiscountType], [CostPrice], [LineTotal], [SortOrder],
                       [ReferenceUrl], [Subtitle], [InvoiceSectionId], [ProductCode],
                       [IsReverseCharge], [ProductTypeId]
                FROM [invoice].[InvoiceLine]
                WHERE [Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<int> InsertAsync(InvoiceLine entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [invoice].[InvoiceLine]
                    ([InvoiceId], [Description], [Quantity], [UnitPrice], [VatRate],
                     [Discount], [DiscountType], [CostPrice], [LineTotal], [SortOrder],
                     [ReferenceUrl], [Subtitle], [InvoiceSectionId], [ProductCode],
                     [IsReverseCharge], [ProductTypeId])
                OUTPUT INSERTED.Id
                VALUES
                    (@InvoiceId, @Description, @Quantity, @UnitPrice, @VatRate,
                     @Discount, @DiscountType, @CostPrice, @LineTotal, @SortOrder,
                     @ReferenceUrl, @Subtitle, @InvoiceSectionId, @ProductCode,
                     @IsReverseCharge, @ProductTypeId)";

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
                command.Parameters.Add(new SqlParameter("@Description", entity.Description));
                command.Parameters.Add(new SqlParameter("@Quantity", entity.Quantity));
                command.Parameters.Add(new SqlParameter("@UnitPrice", entity.UnitPrice));
                command.Parameters.Add(new SqlParameter("@VatRate", entity.VatRate));
                command.Parameters.Add(new SqlParameter("@Discount", entity.Discount));
                command.Parameters.Add(new SqlParameter("@DiscountType", entity.DiscountType));
                command.Parameters.Add(new SqlParameter("@CostPrice", entity.CostPrice ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@LineTotal", entity.LineTotal));
                command.Parameters.Add(new SqlParameter("@SortOrder", entity.SortOrder));
                command.Parameters.Add(new SqlParameter("@ReferenceUrl", entity.ReferenceUrl ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Subtitle", entity.Subtitle ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@InvoiceSectionId", entity.InvoiceSectionId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ProductCode", entity.ProductCode ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsReverseCharge", entity.IsReverseCharge));
                command.Parameters.Add(new SqlParameter("@ProductTypeId", entity.ProductTypeId ?? (object)DBNull.Value));

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

    public async Task<List<int>> BulkInsertAsync(List<InvoiceLine> lines)
    {
        try
        {
            var insertedIds = new List<int>();

            foreach (var line in lines)
            {
                var id = await InsertAsync(line);
                insertedIds.Add(id);
            }

            return insertedIds;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(InvoiceLine entity)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[InvoiceLine]
                SET
                    [Description] = @Description,
                    [Quantity] = @Quantity,
                    [UnitPrice] = @UnitPrice,
                    [VatRate] = @VatRate,
                    [Discount] = @Discount,
                    [DiscountType] = @DiscountType,
                    [CostPrice] = @CostPrice,
                    [LineTotal] = @LineTotal,
                    [SortOrder] = @SortOrder,
                    [ReferenceUrl] = @ReferenceUrl,
                    [Subtitle] = @Subtitle,
                    [InvoiceSectionId] = @InvoiceSectionId,
                    [ProductCode] = @ProductCode,
                    [IsReverseCharge] = @IsReverseCharge,
                    [ProductTypeId] = @ProductTypeId
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Description", entity.Description),
                new SqlParameter("@Quantity", entity.Quantity),
                new SqlParameter("@UnitPrice", entity.UnitPrice),
                new SqlParameter("@VatRate", entity.VatRate),
                new SqlParameter("@Discount", entity.Discount),
                new SqlParameter("@DiscountType", entity.DiscountType),
                new SqlParameter("@CostPrice", entity.CostPrice ?? (object)DBNull.Value),
                new SqlParameter("@LineTotal", entity.LineTotal),
                new SqlParameter("@SortOrder", entity.SortOrder),
                new SqlParameter("@ReferenceUrl", entity.ReferenceUrl ?? (object)DBNull.Value),
                new SqlParameter("@Subtitle", entity.Subtitle ?? (object)DBNull.Value),
                new SqlParameter("@InvoiceSectionId", entity.InvoiceSectionId ?? (object)DBNull.Value),
                new SqlParameter("@ProductCode", entity.ProductCode ?? (object)DBNull.Value),
                new SqlParameter("@IsReverseCharge", entity.IsReverseCharge),
                new SqlParameter("@ProductTypeId", entity.ProductTypeId ?? (object)DBNull.Value)
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
                DELETE FROM [invoice].[InvoiceLine]
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateSectionIdAsync(int lineId, int? sectionId)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[InvoiceLine]
                SET [InvoiceSectionId] = @InvoiceSectionId
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", lineId),
                new SqlParameter("@InvoiceSectionId", sectionId ?? (object)DBNull.Value)
            );
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
                UPDATE [invoice].[InvoiceLine]
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
