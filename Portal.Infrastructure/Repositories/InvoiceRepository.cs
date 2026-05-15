using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Invoice entity CRUD operations against the [invoice].[Invoice] table.
/// </summary>
public class InvoiceRepository : GenericStoredProcedureRepository<Invoice>
{
    public InvoiceRepository(DbContext context) : base(context) { }

    public async Task<List<InvoiceListDto>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id],
                       [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[CustomerId],
                       [customer].[Customer].[Name] AS [CustomerName],
                       [invoice].[Invoice].[InvoiceDate],
                       [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[TotalAmount],
                       [invoice].[InvoiceStatusType].[Name] AS [StatusName],
                       [invoice].[InvoiceFinancialStatusType].[Name] AS [FinancialStatusName],
                       [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId]
                FROM [invoice].[Invoice]
                INNER JOIN [customer].[Customer] ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [invoice].[InvoiceStatusType] ON [invoice].[Invoice].[InvoiceStatusTypeId] = [invoice].[InvoiceStatusType].[Id]
                INNER JOIN [invoice].[InvoiceFinancialStatusType] ON [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = [invoice].[InvoiceFinancialStatusType].[Id]
                WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                ORDER BY [invoice].[Invoice].[InvoiceDate] DESC";

            var results = new List<InvoiceListDto>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new InvoiceListDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                        CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                        InvoiceDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                        DueDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DueDate"))),
                        TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                        FinancialStatusName = reader.GetString(reader.GetOrdinal("FinancialStatusName")),
                        InvoiceStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceStatusTypeId")),
                        InvoiceFinancialStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceFinancialStatusTypeId"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Invoice?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id], [invoice].[Invoice].[BusinessId], [invoice].[Invoice].[CustomerId],
                       [invoice].[Invoice].[QuotationId], [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId], [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[InvoiceDate], [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[Subtotal], [invoice].[Invoice].[TaxAmount],
                       [invoice].[Invoice].[TotalAmount], [invoice].[Invoice].[CurrencyCode],
                       [invoice].[Invoice].[Notes], [invoice].[Invoice].[IsGrandTotalShown],
                       [invoice].[Invoice].[CreatedAtUtc], [invoice].[Invoice].[UpdatedAtUtc]
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[Id] = @Id AND [invoice].[Invoice].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int> InsertAsync(Invoice entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [invoice].[Invoice]
                    ([BusinessId], [CustomerId], [QuotationId], [InvoiceStatusTypeId],
                     [InvoiceFinancialStatusTypeId], [InvoiceNumber], [InvoiceDate], [DueDate],
                     [Subtotal], [TaxAmount], [TotalAmount], [CurrencyCode], [Notes],
                     [IsGrandTotalShown], [CreatedAtUtc], [UpdatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @CustomerId, @QuotationId, @InvoiceStatusTypeId,
                     @InvoiceFinancialStatusTypeId, @InvoiceNumber, @InvoiceDate, @DueDate,
                     @Subtotal, @TaxAmount, @TotalAmount, @CurrencyCode, @Notes,
                     @IsGrandTotalShown, @CreatedAtUtc, @UpdatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@CustomerId", entity.CustomerId));
                command.Parameters.Add(new SqlParameter("@QuotationId", entity.QuotationId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@InvoiceStatusTypeId", entity.InvoiceStatusTypeId));
                command.Parameters.Add(new SqlParameter("@InvoiceFinancialStatusTypeId", entity.InvoiceFinancialStatusTypeId));
                command.Parameters.Add(new SqlParameter("@InvoiceNumber", entity.InvoiceNumber));
                command.Parameters.Add(new SqlParameter("@InvoiceDate", entity.InvoiceDate));
                command.Parameters.Add(new SqlParameter("@DueDate", entity.DueDate));
                command.Parameters.Add(new SqlParameter("@Subtotal", entity.Subtotal));
                command.Parameters.Add(new SqlParameter("@TaxAmount", entity.TaxAmount));
                command.Parameters.Add(new SqlParameter("@TotalAmount", entity.TotalAmount));
                command.Parameters.Add(new SqlParameter("@CurrencyCode", entity.CurrencyCode));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsGrandTotalShown", entity.IsGrandTotalShown));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc));

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

    public async Task UpdateAsync(Invoice entity)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[Invoice]
                SET
                    [CustomerId] = @CustomerId,
                    [InvoiceStatusTypeId] = @InvoiceStatusTypeId,
                    [InvoiceFinancialStatusTypeId] = @InvoiceFinancialStatusTypeId,
                    [InvoiceDate] = @InvoiceDate,
                    [DueDate] = @DueDate,
                    [Subtotal] = @Subtotal,
                    [TaxAmount] = @TaxAmount,
                    [TotalAmount] = @TotalAmount,
                    [Notes] = @Notes,
                    [IsGrandTotalShown] = @IsGrandTotalShown,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@CustomerId", entity.CustomerId),
                new SqlParameter("@InvoiceStatusTypeId", entity.InvoiceStatusTypeId),
                new SqlParameter("@InvoiceFinancialStatusTypeId", entity.InvoiceFinancialStatusTypeId),
                new SqlParameter("@InvoiceDate", entity.InvoiceDate),
                new SqlParameter("@DueDate", entity.DueDate),
                new SqlParameter("@Subtotal", entity.Subtotal),
                new SqlParameter("@TaxAmount", entity.TaxAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
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
                SELECT ISNULL(MAX(CAST(RIGHT([InvoiceNumber], 5) AS INT)), 0) + 1
                FROM [invoice].[Invoice]
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

    public async Task<Invoice?> GetByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id], [invoice].[Invoice].[BusinessId], [invoice].[Invoice].[CustomerId],
                       [invoice].[Invoice].[QuotationId], [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId], [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[InvoiceDate], [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[Subtotal], [invoice].[Invoice].[TaxAmount],
                       [invoice].[Invoice].[TotalAmount], [invoice].[Invoice].[CurrencyCode],
                       [invoice].[Invoice].[Notes], [invoice].[Invoice].[IsGrandTotalShown],
                       [invoice].[Invoice].[CreatedAtUtc], [invoice].[Invoice].[UpdatedAtUtc]
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[QuotationId] = @QuotationId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@QuotationId", quotationId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
