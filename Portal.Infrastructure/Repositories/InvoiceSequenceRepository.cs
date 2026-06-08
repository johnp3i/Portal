using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Billing;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for InvoiceSequence entity operations against the [billing].[InvoiceSequence] table.
/// Provides atomic increment-and-return of the sequence counter per year using MERGE with HOLDLOCK.
/// </summary>
public class InvoiceSequenceRepository : GenericStoredProcedureRepository<InvoiceSequence>, IInvoiceSequenceRepository
{
    public InvoiceSequenceRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Atomically increments and returns the next sequence number for the given year.
    /// Creates the year row if it does not exist (LastNumber starts at 1).
    /// Throws InvalidOperationException if the annual limit (9999) is exceeded.
    /// </summary>
    public async Task<int> IncrementAndGetAsync(int year)
    {
        try
        {
            const string query = @"
                MERGE [billing].[InvoiceSequence] WITH (HOLDLOCK) AS Target
                USING (SELECT @Year AS [Year]) AS Source
                ON Target.[Year] = Source.[Year]
                WHEN MATCHED THEN
                    UPDATE SET Target.[LastNumber] = Target.[LastNumber] + 1
                WHEN NOT MATCHED THEN
                    INSERT ([Year], [LastNumber], [CreatedAtUtc])
                    VALUES (@Year, 1, GETUTCDATE())
                OUTPUT INSERTED.[LastNumber];";

            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var transaction = _context.Database.CurrentTransaction;
            if (transaction != null)
                command.Transaction = transaction.GetDbTransaction();

            command.Parameters.Add(new SqlParameter("@Year", year));

            var result = await command.ExecuteScalarAsync();
            int lastNumber = (int)result!;

            if (lastNumber > 9999)
            {
                throw new InvalidOperationException(
                    $"Annual invoice sequence limit exceeded for year {year}. LastNumber: {lastNumber}.");
            }

            return lastNumber;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
