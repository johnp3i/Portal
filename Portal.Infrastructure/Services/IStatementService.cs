using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for customer statement generation, audit logging, and email history.
/// All operations enforce tenant isolation via BusinessId.
/// </summary>
public interface IStatementService
{
    /// <summary>
    /// Generates a complete statement of account for a customer within the specified period.
    /// Computes opening balance, builds chronological transaction lines, and calculates running balances.
    /// </summary>
    Task<StatementResultDto> GenerateStatementAsync(int customerId, DateOnly fromDate, DateOnly toDate, int businessId, string userId);

    /// <summary>
    /// Logs a PDF download audit event.
    /// </summary>
    Task LogPdfDownloadAsync(int customerId, DateOnly fromDate, DateOnly toDate, int businessId, string userId);

    /// <summary>
    /// Logs an email send audit event and persists an email history record.
    /// </summary>
    Task LogEmailSentAsync(int customerId, DateOnly fromDate, DateOnly toDate, string recipientEmail, int businessId, string userId);

    /// <summary>
    /// Retrieves the email history for a customer, ordered by most recent first.
    /// </summary>
    Task<List<StatementEmailHistoryDto>> GetEmailHistoryAsync(int customerId, int businessId);
}
