using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for QuotationLine entity CRUD operations against the [quotation].[QuotationLine] table.
/// </summary>
public class QuotationLineRepository : GenericStoredProcedureRepository<QuotationLine>
{
    public QuotationLineRepository(DbContext context) : base(context) { }

    public virtual async Task<List<QuotationLine>> GetByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType], [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle], [IsReverseCharge], [ProductCode], [ProductPriceTierId], [PriceTierName]
                FROM [quotation].[QuotationLine]
                WHERE [QuotationId] = @QuotationId
                ORDER BY [SortOrder]";

            return await ExecuteStoredProcedure(query, new SqlParameter("@QuotationId", quotationId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<QuotationLine?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType], [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle], [IsReverseCharge], [ProductCode], [ProductPriceTierId], [PriceTierName]
                FROM [quotation].[QuotationLine]
                WHERE [Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task InsertAsync(QuotationLine entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[QuotationLine]
                    ([QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType], [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle], [ProductCode], [IsReverseCharge], [ProductPriceTierId], [PriceTierName])
                VALUES
                    (@QuotationId, @Description, @Quantity, @UnitPrice, @VatRate, @Discount, @DiscountType, @CostPrice, @LineTotal, @SortOrder, @ReferenceUrl, @ProposalSectionId, @Subtitle, @ProductCode, @IsReverseCharge, @ProductPriceTierId, @PriceTierName)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@QuotationId", entity.QuotationId),
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
                new SqlParameter("@ProposalSectionId", entity.ProposalSectionId ?? (object)DBNull.Value),
                new SqlParameter("@Subtitle", entity.Subtitle ?? (object)DBNull.Value),
                new SqlParameter("@ProductCode", entity.ProductCode ?? (object)DBNull.Value),
                new SqlParameter("@IsReverseCharge", entity.IsReverseCharge),
                new SqlParameter("@ProductPriceTierId", entity.ProductPriceTierId ?? (object)DBNull.Value),
                new SqlParameter("@PriceTierName", entity.PriceTierName ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task UpdateAsync(QuotationLine entity)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[QuotationLine]
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
                    [ProposalSectionId] = @ProposalSectionId,
                    [Subtitle] = @Subtitle,
                    [IsReverseCharge] = @IsReverseCharge
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
                new SqlParameter("@ProposalSectionId", entity.ProposalSectionId ?? (object)DBNull.Value),
                new SqlParameter("@Subtitle", entity.Subtitle ?? (object)DBNull.Value),
                new SqlParameter("@IsReverseCharge", entity.IsReverseCharge)
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
                DELETE FROM [quotation].[QuotationLine]
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeleteAllByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                DELETE FROM [quotation].[QuotationLine]
                WHERE [QuotationId] = @QuotationId";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@QuotationId", quotationId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
