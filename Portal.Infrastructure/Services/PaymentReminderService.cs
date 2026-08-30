using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.PaymentReminders;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Core payment reminder evaluation, sending, and querying logic.
/// Handles both automated daily evaluation and manual reminder dispatch.
/// All operations enforce tenant isolation via BusinessId.
/// </summary>
public class PaymentReminderService : IPaymentReminderService
{
    private readonly PortalDbContext _dbContext;
    private readonly IPaymentReminderScheduleService _scheduleService;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IInvoiceSharingService _sharingService;

    // Financial status constants for eligible invoices
    private static readonly int[] EligibleFinancialStatuses = { 1, 2, 4 }; // Unpaid, PartiallyPaid, Overdue

    public PaymentReminderService(
        PortalDbContext dbContext,
        IPaymentReminderScheduleService scheduleService,
        IEmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        IInvoiceSharingService sharingService)
    {
        _dbContext = dbContext;
        _scheduleService = scheduleService;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _sharingService = sharingService;
    }

    /// <inheritdoc />
    public async Task<ReminderEvaluationResult> EvaluateAndSendAsync(int businessId, DateOnly evaluationDate)
    {
        try
        {
            var result = new ReminderEvaluationResult();

            // 1. Load schedule (or defaults)
            var schedule = await _scheduleService.GetScheduleAsync(businessId);

            // 2. Get enabled tiers only
            var enabledTiers = schedule.Where(t => t.IsEnabled).ToList();
            if (!enabledTiers.Any()) return result;

            // 3. Load eligible invoices (status IN {1=Unpaid, 2=PartiallyPaid, 4=Overdue}, not deleted, not disputed)
            var invoices = await _dbContext.Invoices
                .Include(i => i.Customer)
                .Where(i => i.BusinessId == businessId
                            && !i.IsDeleted
                            && !i.IsDisputed
                            && EligibleFinancialStatuses.Contains(i.InvoiceFinancialStatusTypeId))
                .ToListAsync();

            result.InvoicesEvaluated = invoices.Count;

            // 4. Load existing logs for idempotency check (logs from evaluation date, excluding test sends)
            var evaluationDateStart = evaluationDate.ToDateTime(TimeOnly.MinValue);
            var evaluationDateEnd = evaluationDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var existingLogs = await _dbContext.PaymentReminderLogs
                .Where(l => l.BusinessId == businessId
                           && !l.IsTestSend
                           && l.SentAtUtc >= evaluationDateStart
                           && l.SentAtUtc < evaluationDateEnd)
                .ToListAsync();

            // 5. Get suppression days from first tier (they share this config)
            var suppressionDays = enabledTiers.First().PartialPaymentSuppressionDays;
            var suppressionCutoff = evaluationDate.AddDays(-suppressionDays).ToDateTime(TimeOnly.MinValue);

            // 6. Get business name for email
            var businessName = await GetBusinessNameAsync(businessId);
            var baseUrl = GetBaseUrl();

            // Pre-load active share tokens for eligible invoices
            var activeShareTokens = await _dbContext.InvoiceShares
                .Where(s => s.BusinessId == businessId && s.IsActive && s.ExpiresAtUtc > DateTimeOffset.UtcNow)
                .GroupBy(s => s.InvoiceId)
                .Select(g => new { InvoiceId = g.Key, ShareToken = g.OrderByDescending(s => s.CreatedAtUtc).First().ShareToken })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.ShareToken);

            // 7. For each invoice x enabled tier
            foreach (var invoice in invoices)
            {
                // Skip if customer has no email
                if (string.IsNullOrEmpty(invoice.Customer?.Email)) continue;

                // Skip if customer opted out
                if (invoice.Customer.IsReminderOptedOut) continue;

                // Check recent partial payment suppression (non-voided payments within window)
                var hasRecentPayment = await _dbContext.Payments
                    .AnyAsync(p => p.InvoiceId == invoice.Id
                                  && !p.IsVoided
                                  && p.PaymentDateUtc >= suppressionCutoff);
                if (hasRecentPayment) continue;

                foreach (var tier in enabledTiers)
                {
                    // Check if evaluation date matches trigger (DueDate + DaysOffset)
                    var triggerDate = invoice.DueDate.AddDays(tier.DaysOffset);
                    if (evaluationDate != triggerDate) continue;

                    // Idempotency: skip if already sent today for this invoice/tier
                    if (existingLogs.Any(l => l.InvoiceId == invoice.Id
                                             && l.EscalationTier == tier.EscalationTier
                                             && l.IsSentSuccessfully)) continue;

                    // Check max reminders per tier (exclude test sends)
                    var tierLogCount = await _dbContext.PaymentReminderLogs
                        .CountAsync(l => l.InvoiceId == invoice.Id
                                       && l.EscalationTier == tier.EscalationTier
                                       && l.IsSentSuccessfully
                                       && !l.IsTestSend);
                    if (tierLogCount >= tier.MaxRemindersPerTier) continue;

                    // Check min interval since last same-tier reminder (exclude test sends)
                    var lastSameTypeReminder = await _dbContext.PaymentReminderLogs
                        .Where(l => l.InvoiceId == invoice.Id
                                   && l.EscalationTier == tier.EscalationTier
                                   && l.IsSentSuccessfully
                                   && !l.IsTestSend)
                        .OrderByDescending(l => l.SentAtUtc)
                        .FirstOrDefaultAsync();

                    if (lastSameTypeReminder != null)
                    {
                        var daysSinceLastReminder = (evaluationDateStart - lastSameTypeReminder.SentAtUtc).Days;
                        if (daysSinceLastReminder < tier.MinIntervalDays) continue;
                    }

                    // Calculate outstanding amount
                    var totalPaid = await _dbContext.Payments
                        .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                    var outstandingAmount = invoice.TotalAmount - totalPaid;

                    // SEND the reminder
                    try
                    {
                        await _emailService.SendPaymentReminderEmailAsync(
                            invoice.Customer.Email,
                            invoice.Customer.Name,
                            invoice.InvoiceNumber,
                            outstandingAmount,
                            invoice.DueDate,
                            businessName,
                            tier.EscalationTier,
                            await GetShareTokenForEvaluationAsync(invoice.Id, businessId, activeShareTokens),
                            baseUrl);

                        // Log success
                        _dbContext.PaymentReminderLogs.Add(new PaymentReminderLog
                        {
                            BusinessId = businessId,
                            InvoiceId = invoice.Id,
                            CustomerId = invoice.CustomerId,
                            RecipientEmail = invoice.Customer.Email,
                            EscalationTier = tier.EscalationTier,
                            IsSentSuccessfully = true,
                            IsManualTrigger = false,
                            SentAtUtc = DateTime.UtcNow,
                            TrackingToken = TrackingTokenGenerator.Generate(),
                            IsTestSend = false
                        });
                        result.RemindersSent++;
                    }
                    catch (Exception ex)
                    {
                        // Log failure — do not retry in same evaluation cycle
                        _dbContext.PaymentReminderLogs.Add(new PaymentReminderLog
                        {
                            BusinessId = businessId,
                            InvoiceId = invoice.Id,
                            CustomerId = invoice.CustomerId,
                            RecipientEmail = invoice.Customer.Email,
                            EscalationTier = tier.EscalationTier,
                            IsSentSuccessfully = false,
                            ErrorMessage = ex.Message,
                            IsManualTrigger = false,
                            SentAtUtc = DateTime.UtcNow,
                            TrackingToken = TrackingTokenGenerator.Generate(),
                            IsTestSend = false
                        });
                        result.RemindersFailed++;
                    }
                }
            }

            await _dbContext.SaveChangesAsync();
            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ManualReminderResult> SendManualReminderAsync(int businessId, int invoiceId, string escalationTier)
    {
        try
        {
            // 1. Load the invoice with customer
            var invoice = await _dbContext.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == businessId);

            if (invoice == null)
                return new ManualReminderResult { Success = false, ErrorMessage = "Invoice not found." };

            // 2. Validate invoice status (must be Unpaid=1, PartiallyPaid=2, or Overdue=4)
            if (!EligibleFinancialStatuses.Contains(invoice.InvoiceFinancialStatusTypeId))
                return new ManualReminderResult { Success = false, ErrorMessage = "Reminders can only be sent for unpaid, partially paid, or overdue invoices." };

            // 3. Check customer has email
            if (string.IsNullOrEmpty(invoice.Customer?.Email))
                return new ManualReminderResult { Success = false, ErrorMessage = "Customer has no email address on record." };

            // 4. Check if disputed
            if (invoice.IsDisputed)
                return new ManualReminderResult { Success = false, ErrorMessage = "Cannot send reminders for disputed invoices." };

            // 5. Check customer opt-out (return warning, don't block)
            if (invoice.Customer.IsReminderOptedOut)
                return new ManualReminderResult { Success = true, CustomerOptedOut = true, ErrorMessage = "Customer has opted out of payment reminders." };

            // 6. Calculate outstanding amount (TotalAmount - sum of non-voided payments)
            var totalPaid = await _dbContext.Payments
                .Where(p => p.InvoiceId == invoiceId && p.BusinessId == businessId && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var outstandingAmount = invoice.TotalAmount - totalPaid;

            // 7. Send email
            try
            {
                var businessName = await GetBusinessNameAsync(businessId);

                var (shareToken, shareWasCreated) = await GetOrCreateShareTokenAsync(invoiceId, businessId);

                await _emailService.SendPaymentReminderEmailAsync(
                    invoice.Customer.Email, invoice.Customer.Name, invoice.InvoiceNumber,
                    outstandingAmount, invoice.DueDate,
                    businessName, escalationTier, shareToken, GetBaseUrl());

                // Log success
                _dbContext.PaymentReminderLogs.Add(new PaymentReminderLog
                {
                    BusinessId = businessId,
                    InvoiceId = invoice.Id,
                    CustomerId = invoice.CustomerId,
                    RecipientEmail = invoice.Customer.Email,
                    EscalationTier = escalationTier,
                    IsSentSuccessfully = true,
                    IsManualTrigger = true,
                    SentAtUtc = DateTime.UtcNow,
                    TrackingToken = TrackingTokenGenerator.Generate(),
                    IsTestSend = false
                });
                await _dbContext.SaveChangesAsync();

                var message = shareWasCreated
                    ? "Reminder sent successfully. A share link was automatically created for this invoice."
                    : "Reminder sent successfully.";
                return new ManualReminderResult { Success = true, ErrorMessage = message };
            }
            catch (Exception ex)
            {
                // Log failure
                _dbContext.PaymentReminderLogs.Add(new PaymentReminderLog
                {
                    BusinessId = businessId,
                    InvoiceId = invoice.Id,
                    CustomerId = invoice.CustomerId,
                    RecipientEmail = invoice.Customer.Email,
                    EscalationTier = escalationTier,
                    IsSentSuccessfully = false,
                    ErrorMessage = ex.Message,
                    IsManualTrigger = true,
                    SentAtUtc = DateTime.UtcNow,
                    TrackingToken = TrackingTokenGenerator.Generate(),
                    IsTestSend = false
                });
                await _dbContext.SaveChangesAsync();

                return new ManualReminderResult { Success = false, ErrorMessage = "Failed to send reminder email." };
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<PaymentReminderLogDto>> GetHistoryByInvoiceAsync(int businessId, int invoiceId)
    {
        try
        {
            return await _dbContext.PaymentReminderLogs
                .Where(l => l.BusinessId == businessId && l.InvoiceId == invoiceId)
                .OrderByDescending(l => l.SentAtUtc)
                .Select(l => new PaymentReminderLogDto
                {
                    EscalationTier = l.EscalationTier,
                    RecipientEmail = l.RecipientEmail,
                    SentAtUtc = l.SentAtUtc,
                    IsManualTrigger = l.IsManualTrigger,
                    IsSentSuccessfully = l.IsSentSuccessfully,
                    ErrorMessage = l.ErrorMessage,
                    IsOpened = l.IsOpened,
                    OpenedAtUtc = l.OpenedAtUtc,
                    OpenCount = l.OpenCount,
                    LastOpenedAtUtc = l.LastOpenedAtUtc,
                    IsTestSend = l.IsTestSend
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ReminderDashboardWidgetDto> GetDashboardWidgetDataAsync(int businessId)
    {
        try
        {
            var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);

            // Total reminders sent successfully this week (exclude test sends)
            var totalSentThisWeek = await _dbContext.PaymentReminderLogs
                .CountAsync(l => l.BusinessId == businessId
                                && l.IsSentSuccessfully
                                && !l.IsTestSend
                                && l.SentAtUtc >= weekStart);

            // Get all successful reminders for this business (exclude test sends)
            var recentReminders = await _dbContext.PaymentReminderLogs
                .Where(l => l.BusinessId == businessId && l.IsSentSuccessfully && !l.IsTestSend)
                .ToListAsync();

            // Get non-voided payments from this week
            var recentPayments = await _dbContext.Payments
                .Where(p => p.BusinessId == businessId
                           && !p.IsVoided
                           && p.PaymentDateUtc >= weekStart)
                .ToListAsync();

            // Correlate: for each payment, check if a reminder was sent for the same invoice
            // within 7 days before the payment
            var paymentsAfterReminder = 0;
            var amountAfterReminder = 0m;

            foreach (var payment in recentPayments)
            {
                var hadRecentReminder = recentReminders.Any(r =>
                    r.InvoiceId == payment.InvoiceId
                    && (payment.PaymentDateUtc - r.SentAtUtc).TotalDays >= 0
                    && (payment.PaymentDateUtc - r.SentAtUtc).TotalDays <= 7);

                if (hadRecentReminder)
                {
                    paymentsAfterReminder++;
                    amountAfterReminder += payment.Amount;
                }
            }

            return new ReminderDashboardWidgetDto
            {
                TotalRemindersSentThisWeek = totalSentThisWeek,
                PaymentsReceivedAfterReminder = paymentsAfterReminder,
                AmountReceivedAfterReminder = amountAfterReminder
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<int>> GetEligibleBusinessIdsAsync()
    {
        try
        {
            return await _dbContext.BusinessPlans
                .Where(bp => bp.IsActive)
                .Where(bp => bp.Business.IsReminderSystemEnabled)
                .Where(bp => bp.Plan.PlanFeatures
                    .Any(pf => pf.ModuleName == PortalModules.PaymentReminderAuto && pf.IsIncluded))
                .Select(bp => bp.BusinessId)
                .Distinct()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RecordOpenEventAsync(string trackingToken)
    {
        try
        {
            if (string.IsNullOrEmpty(trackingToken)) return;

            var log = await _dbContext.PaymentReminderLogs
                .FirstOrDefaultAsync(l => l.TrackingToken == trackingToken);

            if (log == null) return;

            if (!log.IsOpened)
            {
                log.IsOpened = true;
                log.OpenedAtUtc = DateTime.UtcNow;
                log.OpenCount = 1;
            }
            else
            {
                log.OpenCount++;
                log.LastOpenedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TestReminderResult> SendTestReminderAsync(int businessId, int invoiceId, string escalationTier, string testRecipientEmail)
    {
        try
        {
            // 1. Validate email format
            if (string.IsNullOrWhiteSpace(testRecipientEmail) || !IsValidEmail(testRecipientEmail))
                return new TestReminderResult { Success = false, Message = "Please enter a valid email address." };

            // 2. Load invoice with customer (tenant isolation)
            var invoice = await _dbContext.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == businessId);

            if (invoice == null)
                return new TestReminderResult { Success = false, Message = "Invoice not found." };

            // 3. Calculate outstanding amount
            var totalPaid = await _dbContext.Payments
                .Where(p => p.InvoiceId == invoiceId && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var outstandingAmount = invoice.TotalAmount - totalPaid;

            // 4. Send test email (with [TEST] prefix and tracking token)
            var trackingToken = TrackingTokenGenerator.Generate();
            var businessName = await GetBusinessNameAsync(businessId);

            var (invoiceShareToken, shareWasCreated) = await GetOrCreateShareTokenAsync(invoiceId, businessId);

            try
            {
                await _emailService.SendPaymentReminderEmailAsync(
                    testRecipientEmail,
                    invoice.Customer?.Name ?? "Customer",
                    invoice.InvoiceNumber,
                    outstandingAmount,
                    invoice.DueDate,
                    businessName,
                    escalationTier,
                    invoiceShareToken,
                    GetBaseUrl(),
                    trackingToken: trackingToken,
                    isTestSend: true);

                // 5. Log as test send
                _dbContext.PaymentReminderLogs.Add(new PaymentReminderLog
                {
                    BusinessId = businessId,
                    InvoiceId = invoice.Id,
                    CustomerId = invoice.CustomerId,
                    RecipientEmail = testRecipientEmail,
                    EscalationTier = escalationTier,
                    IsSentSuccessfully = true,
                    IsManualTrigger = true,
                    SentAtUtc = DateTime.UtcNow,
                    TrackingToken = trackingToken,
                    IsTestSend = true
                });
                await _dbContext.SaveChangesAsync();

                return new TestReminderResult { Success = true, Message = shareWasCreated
                    ? "Test reminder sent successfully. A share link was automatically created for this invoice."
                    : "Test reminder sent successfully." };
            }
            catch (Exception ex)
            {
                // Log failed test send
                _dbContext.PaymentReminderLogs.Add(new PaymentReminderLog
                {
                    BusinessId = businessId,
                    InvoiceId = invoice.Id,
                    CustomerId = invoice.CustomerId,
                    RecipientEmail = testRecipientEmail,
                    EscalationTier = escalationTier,
                    IsSentSuccessfully = false,
                    ErrorMessage = ex.Message,
                    IsManualTrigger = true,
                    SentAtUtc = DateTime.UtcNow,
                    TrackingToken = trackingToken,
                    IsTestSend = true
                });
                await _dbContext.SaveChangesAsync();

                return new TestReminderResult { Success = false, Message = "Failed to send test reminder." };
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<UpcomingReminderDto>> GetUpcomingRemindersAsync(int businessId, int daysAhead = 14, string? tierFilter = null)
    {
        try
        {
            var projections = new List<UpcomingReminderDto>();

            // 1. Load schedule
            var schedule = await _scheduleService.GetScheduleAsync(businessId);
            var enabledTiers = schedule
                .Where(t => t.IsEnabled)
                .Where(t => tierFilter == null || t.EscalationTier.Equals(tierFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!enabledTiers.Any()) return projections;

            // 2. Determine date range
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var endDate = startDate.AddDays(daysAhead);

            // 3. Load eligible invoices (same filter as EvaluateAndSendAsync)
            var invoices = await _dbContext.Invoices
                .Include(i => i.Customer)
                .Where(i => i.BusinessId == businessId
                            && !i.IsDeleted
                            && !i.IsDisputed
                            && EligibleFinancialStatuses.Contains(i.InvoiceFinancialStatusTypeId))
                .ToListAsync();

            if (!invoices.Any()) return projections;

            // 4. Get suppression days
            var suppressionDays = enabledTiers.First().PartialPaymentSuppressionDays;

            // 5. For each invoice x tier, check if trigger date falls within window
            foreach (var invoice in invoices)
            {
                if (string.IsNullOrEmpty(invoice.Customer?.Email)) continue;
                if (invoice.Customer.IsReminderOptedOut) continue;

                foreach (var tier in enabledTiers)
                {
                    var triggerDate = invoice.DueDate.AddDays(tier.DaysOffset);

                    if (triggerDate < startDate || triggerDate > endDate) continue;

                    // Check partial payment suppression relative to the trigger date
                    var suppressionCutoff = triggerDate.AddDays(-suppressionDays).ToDateTime(TimeOnly.MinValue);
                    var triggerDateTime = triggerDate.ToDateTime(TimeOnly.MinValue);
                    var hasRecentPayment = await _dbContext.Payments
                        .AnyAsync(p => p.InvoiceId == invoice.Id
                                      && !p.IsVoided
                                      && p.PaymentDateUtc >= suppressionCutoff
                                      && p.PaymentDateUtc <= triggerDateTime);
                    if (hasRecentPayment) continue;

                    // Check max reminders per tier (exclude test sends)
                    var tierLogCount = await _dbContext.PaymentReminderLogs
                        .CountAsync(l => l.InvoiceId == invoice.Id
                                       && l.EscalationTier == tier.EscalationTier
                                       && l.IsSentSuccessfully
                                       && !l.IsTestSend);
                    if (tierLogCount >= tier.MaxRemindersPerTier) continue;

                    // Check min interval since last same-tier reminder (exclude test sends)
                    var lastSameTypeReminder = await _dbContext.PaymentReminderLogs
                        .Where(l => l.InvoiceId == invoice.Id
                                   && l.EscalationTier == tier.EscalationTier
                                   && l.IsSentSuccessfully
                                   && !l.IsTestSend)
                        .OrderByDescending(l => l.SentAtUtc)
                        .FirstOrDefaultAsync();

                    if (lastSameTypeReminder != null)
                    {
                        var daysSinceLastReminder = (triggerDateTime - lastSameTypeReminder.SentAtUtc).Days;
                        if (daysSinceLastReminder < tier.MinIntervalDays) continue;
                    }

                    // Calculate outstanding amount
                    var totalPaid = await _dbContext.Payments
                        .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                    var outstandingAmount = invoice.TotalAmount - totalPaid;

                    projections.Add(new UpcomingReminderDto
                    {
                        ScheduledDate = triggerDate,
                        InvoiceNumber = invoice.InvoiceNumber,
                        CustomerName = invoice.Customer.Name,
                        EscalationTier = tier.EscalationTier,
                        OutstandingAmount = outstandingAmount,
                        DueDate = invoice.DueDate
                    });
                }
            }

            return projections.OrderBy(p => p.ScheduledDate).ThenBy(p => p.EscalationTier).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ReminderHistoryPageResult> GetAllReminderHistoryAsync(
        int businessId,
        string? tier = null,
        string? status = null,
        string? method = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? customer = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            var query = _dbContext.PaymentReminderLogs
                .Include(l => l.Customer)
                .Include(l => l.Invoice)
                .Where(l => l.BusinessId == businessId)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(tier) && !tier.Equals("All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(l => l.EscalationTier == tier);

            if (!string.IsNullOrEmpty(status))
            {
                if (status.Equals("Sent", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(l => l.IsSentSuccessfully == true);
                else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(l => l.IsSentSuccessfully == false);
            }

            if (!string.IsNullOrEmpty(method))
            {
                if (method.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(l => l.IsManualTrigger == false && l.IsTestSend == false);
                else if (method.Equals("Manual", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(l => l.IsManualTrigger == true && l.IsTestSend == false);
                else if (method.Equals("Test", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(l => l.IsTestSend == true);
            }

            if (dateFrom.HasValue)
                query = query.Where(l => l.SentAtUtc >= dateFrom.Value);

            if (dateTo.HasValue)
            {
                var endOfDay = dateTo.Value.Date.AddDays(1);
                query = query.Where(l => l.SentAtUtc < endOfDay);
            }

            if (!string.IsNullOrEmpty(customer))
                query = query.Where(l => l.Customer.Name.Contains(customer));

            // Order by most recent first
            var ordered = query.OrderByDescending(l => l.SentAtUtc);

            // Count total matching
            var totalCount = await ordered.CountAsync();

            // Page slice
            var items = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new ReminderHistoryItemDto
                {
                    Id = l.Id,
                    SentAtUtc = l.SentAtUtc,
                    InvoiceId = l.InvoiceId,
                    InvoiceNumber = l.Invoice.InvoiceNumber,
                    CustomerName = l.Customer.Name,
                    EscalationTier = l.EscalationTier,
                    RecipientEmail = l.RecipientEmail,
                    IsManualTrigger = l.IsManualTrigger,
                    IsTestSend = l.IsTestSend,
                    IsSentSuccessfully = l.IsSentSuccessfully,
                    IsOpened = l.IsOpened
                })
                .ToListAsync();

            return new ReminderHistoryPageResult
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the active share token for an invoice, or creates a new one if none exists.
    /// Returns a tuple of (shareToken, wasCreated).
    /// </summary>
    private async Task<(string? ShareToken, bool WasCreated)> GetOrCreateShareTokenAsync(int invoiceId, int businessId)
    {
        // Try to find existing active share
        var existingToken = await _dbContext.InvoiceShares
            .Where(s => s.InvoiceId == invoiceId && s.BusinessId == businessId && s.IsActive && s.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => s.ShareToken)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(existingToken))
            return (existingToken, false);

        // No active share — create one (expires in 30 days, no email sent)
        try
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
            var share = await _sharingService.ShareAsync(invoiceId, expiresAt, sendEmail: false, userId);
            return (share.ShareToken, true);
        }
        catch (Exception ex)
        {
            // If share creation fails, send reminder without link (graceful degradation)
            return (null, false);
        }
    }

    /// <summary>
    /// Gets share token from pre-loaded dictionary, or creates one if missing (for background evaluation).
    /// </summary>
    private async Task<string?> GetShareTokenForEvaluationAsync(int invoiceId, int businessId, Dictionary<int, string> preloadedTokens)
    {
        if (preloadedTokens.TryGetValue(invoiceId, out var token))
            return token;

        var (newToken, _) = await GetOrCreateShareTokenAsync(invoiceId, businessId);
        if (!string.IsNullOrEmpty(newToken))
            preloadedTokens[invoiceId] = newToken;
        return newToken;
    }

    /// <summary>
    /// Simple email format validation: contains exactly one @, non-empty local and domain parts,
    /// domain contains at least one dot.
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1) return false;

        var domain = email[(atIndex + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }

    /// <summary>
    /// Gets the business name for use in reminder emails.
    /// </summary>
    private async Task<string> GetBusinessNameAsync(int businessId)
    {
        var business = await _dbContext.Businesses
            .Where(b => b.Id == businessId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync();

        return business ?? "Business";
    }

    /// <summary>
    /// Gets the base URL for constructing links in reminder emails.
    /// </summary>
    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null) return "";
        return $"{request.Scheme}://{request.Host}";
    }
}
