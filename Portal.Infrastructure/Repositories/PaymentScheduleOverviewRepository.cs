using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Read-only repository for the Payment Schedules Overview page.
/// Fetches all active schedules with their instalments, invoice numbers, and customer names in a single query.
/// </summary>
public class PaymentScheduleOverviewRepository : GenericStoredProcedureRepository<ScheduleOverviewRawRow>
{
    public PaymentScheduleOverviewRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Gets all active payment schedules with their instalments, invoice numbers, and customer names.
    /// Single query with JOINs — avoids N+1.
    /// </summary>
    public virtual async Task<List<ScheduleOverviewRawRow>> GetActiveSchedulesWithInstalmentsAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentSchedule].[Id] AS ScheduleId,
                       [revenue].[PaymentSchedule].[InvoiceId],
                       [invoice].[Invoice].[InvoiceNumber],
                       [customer].[Customer].[Name] AS CustomerName,
                       [invoice].[Invoice].[CustomerId],
                       [revenue].[PaymentScheduleInstalment].[Id] AS InstalmentId,
                       [revenue].[PaymentScheduleInstalment].[Amount],
                       [revenue].[PaymentScheduleInstalment].[MatchedAmount],
                       [revenue].[PaymentScheduleInstalment].[DueDate],
                       [revenue].[PaymentScheduleInstalment].[SequenceNumber]
                FROM [revenue].[PaymentSchedule]
                INNER JOIN [revenue].[PaymentScheduleInstalment]
                    ON [revenue].[PaymentSchedule].[Id] = [revenue].[PaymentScheduleInstalment].[PaymentScheduleId]
                INNER JOIN [invoice].[Invoice]
                    ON [revenue].[PaymentSchedule].[InvoiceId] = [invoice].[Invoice].[Id]
                INNER JOIN [customer].[Customer]
                    ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                WHERE [revenue].[PaymentSchedule].[BusinessId] = @BusinessId
                  AND [revenue].[PaymentSchedule].[IsActive] = 1
                ORDER BY [revenue].[PaymentSchedule].[Id], [revenue].[PaymentScheduleInstalment].[SequenceNumber]";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
