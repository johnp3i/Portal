using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Receipt;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PaymentReceipt entity CRUD operations against the [revenue].[PaymentReceipt] table.
/// </summary>
public class PaymentReceiptRepository : GenericStoredProcedureRepository<PaymentReceipt>
{
    public PaymentReceiptRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new payment receipt and returns the new Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(PaymentReceipt entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[PaymentReceipt]
                    ([BusinessId], [ReceiptNumber], [CustomerId], [PaymentId], [ReceiptDate],
                     [TotalAmountReceived], [OutstandingBalanceAfter], [PaymentMethodTypeId],
                     [PaymentReference], [Notes], [SignatureId], [IsVoided], [CreatedByUserId], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @ReceiptNumber, @CustomerId, @PaymentId, @ReceiptDate,
                     @TotalAmountReceived, @OutstandingBalanceAfter, @PaymentMethodTypeId,
                     @PaymentReference, @Notes, @SignatureId, @IsVoided, @CreatedByUserId, @CreatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@ReceiptNumber", entity.ReceiptNumber));
                command.Parameters.Add(new SqlParameter("@CustomerId", entity.CustomerId));
                command.Parameters.Add(new SqlParameter("@PaymentId", entity.PaymentId));
                command.Parameters.Add(new SqlParameter("@ReceiptDate", entity.ReceiptDate));
                command.Parameters.Add(new SqlParameter("@TotalAmountReceived", entity.TotalAmountReceived));
                command.Parameters.Add(new SqlParameter("@OutstandingBalanceAfter", entity.OutstandingBalanceAfter));
                command.Parameters.Add(new SqlParameter("@PaymentMethodTypeId", entity.PaymentMethodTypeId));
                command.Parameters.Add(new SqlParameter("@PaymentReference", entity.PaymentReference ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SignatureId", entity.SignatureId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsVoided", entity.IsVoided));
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
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single receipt by Id and BusinessId for tenant isolation.
    /// </summary>
    public virtual async Task<PaymentReceipt?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentReceipt].[Id],
                       [revenue].[PaymentReceipt].[BusinessId],
                       [revenue].[PaymentReceipt].[ReceiptNumber],
                       [revenue].[PaymentReceipt].[CustomerId],
                       [revenue].[PaymentReceipt].[PaymentId],
                       [revenue].[PaymentReceipt].[ReceiptDate],
                       [revenue].[PaymentReceipt].[TotalAmountReceived],
                       [revenue].[PaymentReceipt].[OutstandingBalanceAfter],
                       [revenue].[PaymentReceipt].[PaymentMethodTypeId],
                       [revenue].[PaymentReceipt].[PaymentReference],
                       [revenue].[PaymentReceipt].[Notes],
                       [revenue].[PaymentReceipt].[SignatureId],
                       [revenue].[PaymentReceipt].[IsVoided],
                       [revenue].[PaymentReceipt].[CreatedByUserId],
                       [revenue].[PaymentReceipt].[CreatedAtUtc]
                FROM [revenue].[PaymentReceipt]
                WHERE [revenue].[PaymentReceipt].[Id] = @Id
                  AND [revenue].[PaymentReceipt].[BusinessId] = @BusinessId";

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
    /// Gets the receipt associated with a specific payment (if one exists).
    /// </summary>
    public virtual async Task<PaymentReceipt?> GetByPaymentIdAsync(int paymentId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentReceipt].[Id],
                       [revenue].[PaymentReceipt].[BusinessId],
                       [revenue].[PaymentReceipt].[ReceiptNumber],
                       [revenue].[PaymentReceipt].[CustomerId],
                       [revenue].[PaymentReceipt].[PaymentId],
                       [revenue].[PaymentReceipt].[ReceiptDate],
                       [revenue].[PaymentReceipt].[TotalAmountReceived],
                       [revenue].[PaymentReceipt].[OutstandingBalanceAfter],
                       [revenue].[PaymentReceipt].[PaymentMethodTypeId],
                       [revenue].[PaymentReceipt].[PaymentReference],
                       [revenue].[PaymentReceipt].[Notes],
                       [revenue].[PaymentReceipt].[SignatureId],
                       [revenue].[PaymentReceipt].[IsVoided],
                       [revenue].[PaymentReceipt].[CreatedByUserId],
                       [revenue].[PaymentReceipt].[CreatedAtUtc]
                FROM [revenue].[PaymentReceipt]
                WHERE [revenue].[PaymentReceipt].[PaymentId] = @PaymentId
                  AND [revenue].[PaymentReceipt].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@PaymentId", paymentId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets paginated receipts for a business with optional filters.
    /// </summary>
    public virtual async Task<(List<ReceiptListDto> Items, int TotalCount)> GetPagedAsync(
        int businessId, int? customerId, DateTime? fromDate, DateTime? toDate, bool? isVoided,
        int offset, int pageSize)
    {
        try
        {
            var filters = "";
            if (customerId.HasValue)
                filters += " AND [revenue].[PaymentReceipt].[CustomerId] = @CustomerId";
            if (fromDate.HasValue)
                filters += " AND [revenue].[PaymentReceipt].[ReceiptDate] >= @FromDate";
            if (toDate.HasValue)
                filters += " AND [revenue].[PaymentReceipt].[ReceiptDate] <= @ToDate";
            if (isVoided.HasValue)
                filters += " AND [revenue].[PaymentReceipt].[IsVoided] = @IsVoided";

            var countQuery = $@"
                SELECT COUNT(*)
                FROM [revenue].[PaymentReceipt]
                WHERE [revenue].[PaymentReceipt].[BusinessId] = @BusinessId{filters}";

            var dataQuery = $@"
                SELECT [revenue].[PaymentReceipt].[Id],
                       [revenue].[PaymentReceipt].[ReceiptNumber],
                       [customer].[Customer].[Name],
                       [revenue].[PaymentReceipt].[ReceiptDate],
                       [revenue].[PaymentReceipt].[CreatedAtUtc],
                       [revenue].[PaymentReceipt].[TotalAmountReceived],
                       [revenue].[PaymentReceipt].[IsVoided],
                       [revenue].[PaymentMethodType].[Name],
                       (SELECT COUNT(*) FROM [revenue].[PaymentReceiptLine]
                        WHERE [revenue].[PaymentReceiptLine].[PaymentReceiptId] = [revenue].[PaymentReceipt].[Id]) AS [LineCount]
                FROM [revenue].[PaymentReceipt]
                INNER JOIN [customer].[Customer]
                    ON [revenue].[PaymentReceipt].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [revenue].[PaymentMethodType]
                    ON [revenue].[PaymentReceipt].[PaymentMethodTypeId] = [revenue].[PaymentMethodType].[Id]
                WHERE [revenue].[PaymentReceipt].[BusinessId] = @BusinessId{filters}
                ORDER BY [revenue].[PaymentReceipt].[ReceiptDate] DESC, [revenue].[PaymentReceipt].[Id] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _context.Database.CurrentTransaction;

                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    if (transaction != null)
                        countCommand.Transaction = transaction.GetDbTransaction();
                    countCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    if (customerId.HasValue)
                        countCommand.Parameters.Add(new SqlParameter("@CustomerId", customerId.Value));
                    if (fromDate.HasValue)
                        countCommand.Parameters.Add(new SqlParameter("@FromDate", fromDate.Value));
                    if (toDate.HasValue)
                        countCommand.Parameters.Add(new SqlParameter("@ToDate", toDate.Value));
                    if (isVoided.HasValue)
                        countCommand.Parameters.Add(new SqlParameter("@IsVoided", isVoided.Value));

                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = (int)countResult!;
                }

                var items = new List<ReceiptListDto>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;
                    if (transaction != null)
                        dataCommand.Transaction = transaction.GetDbTransaction();
                    dataCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));
                    if (customerId.HasValue)
                        dataCommand.Parameters.Add(new SqlParameter("@CustomerId", customerId.Value));
                    if (fromDate.HasValue)
                        dataCommand.Parameters.Add(new SqlParameter("@FromDate", fromDate.Value));
                    if (toDate.HasValue)
                        dataCommand.Parameters.Add(new SqlParameter("@ToDate", toDate.Value));
                    if (isVoided.HasValue)
                        dataCommand.Parameters.Add(new SqlParameter("@IsVoided", isVoided.Value));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        items.Add(new ReceiptListDto
                        {
                            Id = reader.GetInt32(0),
                            ReceiptNumber = reader.GetString(1),
                            CustomerName = reader.GetString(2),
                            ReceiptDate = reader.GetDateTime(3),
                            CreatedAtUtc = reader.GetDateTime(4),
                            TotalAmountReceived = reader.GetDecimal(5),
                            IsVoided = reader.GetBoolean(6),
                            PaymentMethodName = reader.GetString(7),
                            LineCount = reader.GetInt32(8)
                        });
                    }
                }

                return (items, totalCount);
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

    /// <summary>
    /// Voids a receipt (sets IsVoided = 1).
    /// </summary>
    public virtual async Task VoidAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[PaymentReceipt]
                SET [IsVoided] = 1
                WHERE [revenue].[PaymentReceipt].[Id] = @Id
                  AND [revenue].[PaymentReceipt].[BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Generates a receipt number in the format REC-{InvoiceSeq}-{PaymentNumber}-{DDMMYY}.
    /// PaymentNumber = count of existing non-voided receipts for that invoice + 1.
    /// For multi-invoice (global) payments, uses the lowest invoice sequence number.
    /// </summary>
    public virtual async Task<string> GenerateReceiptNumberAsync(int businessId, string invoiceNumber, DateTime receiptDate)
    {
        try
        {
            // Extract the sequence part from invoice number (e.g., "INV-1-00093" → "00093")
            var parts = invoiceNumber.Split('-');
            var invoiceSeq = parts.Length >= 3 ? parts[^1] : invoiceNumber;

            // Count ALL existing receipts (including voided) to avoid number collisions
            var countQuery = @"
                SELECT COUNT(*)
                FROM [revenue].[PaymentReceipt]
                WHERE [revenue].[PaymentReceipt].[BusinessId] = @BusinessId
                  AND [revenue].[PaymentReceipt].[ReceiptNumber] LIKE 'REC-' + @InvoiceSeq + '-%'";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = countQuery;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@InvoiceSeq", invoiceSeq));

                var result = await command.ExecuteScalarAsync();
                var paymentNumber = (int)result! + 1;

                var datePart = receiptDate.ToString("ddMMyy");

                return $"REC-{invoiceSeq}-{paymentNumber}-{datePart}";
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
}
