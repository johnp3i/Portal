using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for customer statement generation, audit logging, and email history retrieval.
/// Orchestrates opening balance computation, statement line assembly, and running balance calculation.
/// Enforces tenant isolation via BusinessId parameter.
/// </summary>
public class StatementService : IStatementService
{
    private readonly StatementRepository _statementRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly MembershipDbContext _membershipDbContext;

    public StatementService(
        StatementRepository statementRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService,
        MembershipDbContext membershipDbContext)
    {
        _statementRepository = statementRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
        _membershipDbContext = membershipDbContext;
    }

    /// <inheritdoc />
    public async Task<StatementResultDto> GenerateStatementAsync(int customerId, DateOnly fromDate, DateOnly toDate, int businessId, string userId)
    {
        // If businessId is unresolvable, return empty result
        if (businessId <= 0)
        {
            return new StatementResultDto
            {
                OpeningBalance = 0m,
                ClosingBalance = 0m,
                TotalInvoiced = 0m,
                TotalPaid = 0m,
                InvoiceCount = 0,
                PaymentCount = 0,
                Lines = new List<StatementLineDto>()
            };
        }

        // 1. Compute opening balance: invoiced before period - paid before period
        var invoicedBefore = await _statementRepository.GetInvoicedTotalBeforeDateAsync(customerId, businessId, fromDate);
        var paidBefore = await _statementRepository.GetPaidTotalBeforeDateAsync(customerId, businessId, fromDate);
        var openingBalance = invoicedBefore - paidBefore;

        // 2. Fetch in-period invoices and payments
        var invoices = await _statementRepository.GetInvoicesInPeriodAsync(customerId, businessId, fromDate, toDate);
        var payments = await _statementRepository.GetPaymentsInPeriodAsync(customerId, businessId, fromDate, toDate);

        // 3. Build statement lines for invoices
        var transactionLines = new List<StatementLineDto>();

        foreach (var invoice in invoices)
        {
            transactionLines.Add(new StatementLineDto
            {
                Date = invoice.InvoiceDate,
                Type = StatementLineType.Invoice,
                Reference = invoice.InvoiceNumber,
                Description = invoice.Notes ?? string.Empty,
                Debit = invoice.TotalAmount,
                Credit = 0m,
                RunningBalance = 0m // Will be computed after sorting
            });
        }

        // 4. Build statement lines for payments
        foreach (var payment in payments)
        {
            var reference = string.IsNullOrEmpty(payment.Reference)
                ? payment.PaymentMethodName
                : $"{payment.PaymentMethodName} · Ref: {payment.Reference}";

            transactionLines.Add(new StatementLineDto
            {
                Date = DateOnly.FromDateTime(payment.PaymentDateUtc),
                Type = StatementLineType.Payment,
                Reference = reference,
                Description = payment.Notes ?? string.Empty,
                Debit = 0m,
                Credit = payment.Amount,
                RunningBalance = 0m // Will be computed after sorting
            });
        }

        // 5. Sort lines chronologically by Date, invoices before payments on same date
        transactionLines = transactionLines
            .OrderBy(l => l.Date)
            .ThenBy(l => l.Type == StatementLineType.Invoice ? 0 : 1)
            .ToList();

        // 6. Build the full lines list with Opening line prepended
        var allLines = new List<StatementLineDto>();

        // Prepend Opening line
        allLines.Add(new StatementLineDto
        {
            Date = fromDate,
            Type = StatementLineType.Opening,
            Reference = "Balance brought forward",
            Description = string.Empty,
            Debit = 0m,
            Credit = 0m,
            RunningBalance = openingBalance
        });

        // 7. Compute running balance sequentially
        var runningBalance = openingBalance;
        foreach (var line in transactionLines)
        {
            runningBalance = runningBalance + line.Debit - line.Credit;
            line.RunningBalance = runningBalance;
            allLines.Add(line);
        }

        // 8. Append Closing line
        allLines.Add(new StatementLineDto
        {
            Date = toDate,
            Type = StatementLineType.Closing,
            Reference = "Balance carried forward",
            Description = string.Empty,
            Debit = 0m,
            Credit = 0m,
            RunningBalance = runningBalance
        });

        // 9. Compute summary totals
        var totalInvoiced = transactionLines.Where(l => l.Type == StatementLineType.Invoice).Sum(l => l.Debit);
        var totalPaid = transactionLines.Where(l => l.Type == StatementLineType.Payment).Sum(l => l.Credit);
        var invoiceCount = transactionLines.Count(l => l.Type == StatementLineType.Invoice);
        var paymentCount = transactionLines.Count(l => l.Type == StatementLineType.Payment);

        // 10. Log audit entry for statement generation
        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            UserId = userId,
            Action = "StatementGenerated",
            TableName = "Statement",
            RecordId = customerId.ToString(),
            OldValues = null,
            NewValues = $"{{\"CustomerId\":{customerId},\"FromDate\":\"{fromDate:yyyy-MM-dd}\",\"ToDate\":\"{toDate:yyyy-MM-dd}\"}}",
            Timestamp = DateTime.UtcNow
        });

        return new StatementResultDto
        {
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance,
            TotalInvoiced = totalInvoiced,
            TotalPaid = totalPaid,
            InvoiceCount = invoiceCount,
            PaymentCount = paymentCount,
            Lines = allLines
        };
    }

    /// <inheritdoc />
    public async Task LogPdfDownloadAsync(int customerId, DateOnly fromDate, DateOnly toDate, int businessId, string userId)
    {
        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            UserId = userId,
            Action = "StatementPdfDownloaded",
            TableName = "Statement",
            RecordId = customerId.ToString(),
            OldValues = null,
            NewValues = $"{{\"CustomerId\":{customerId},\"FromDate\":\"{fromDate:yyyy-MM-dd}\",\"ToDate\":\"{toDate:yyyy-MM-dd}\"}}",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public async Task LogEmailSentAsync(int customerId, DateOnly fromDate, DateOnly toDate, string recipientEmail, int businessId, string userId)
    {
        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            UserId = userId,
            Action = "StatementEmailed",
            TableName = "Statement",
            RecordId = customerId.ToString(),
            OldValues = null,
            NewValues = $"{{\"CustomerId\":{customerId},\"FromDate\":\"{fromDate:yyyy-MM-dd}\",\"ToDate\":\"{toDate:yyyy-MM-dd}\",\"RecipientEmail\":\"{recipientEmail}\"}}",
            Timestamp = DateTime.UtcNow
        });

        await _statementRepository.InsertEmailHistoryAsync(new StatementEmailHistory
        {
            BusinessId = businessId,
            CustomerId = customerId,
            FromDate = fromDate,
            ToDate = toDate,
            RecipientEmail = recipientEmail,
            SentByUserId = userId,
            SentAtUtc = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public async Task<List<StatementEmailHistoryDto>> GetEmailHistoryAsync(int customerId, int businessId)
    {
        // If businessId is unresolvable, return empty list
        if (businessId <= 0)
            return new List<StatementEmailHistoryDto>();

        var history = await _statementRepository.GetEmailHistoryByCustomerAsync(customerId, businessId);

        // Resolve user display names from the Membership database
        var userIds = history.Select(h => h.SentByDisplayName).Distinct().ToList();
        var userNames = await _membershipDbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        foreach (var record in history)
        {
            if (userNames.TryGetValue(record.SentByDisplayName, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
            {
                record.SentByDisplayName = displayName;
            }
        }

        return history;
    }
}
