using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Read-only service for aggregating payment schedule data for the overview page.
/// </summary>
public interface IPaymentScheduleOverviewService
{
    /// <summary>
    /// Retrieves all overview data for the Payment Schedules page:
    /// KPI metrics, monthly timeline, and table rows for all active schedules.
    /// </summary>
    Task<PaymentScheduleOverviewDto> GetOverviewAsync(int businessId);
}

/// <summary>
/// Aggregates active payment schedule data into KPI metrics, monthly timeline,
/// and a filterable table of schedules with progress indicators.
/// </summary>
public class PaymentScheduleOverviewService : IPaymentScheduleOverviewService
{
    private const int StatusPending = 1;
    private const int StatusDue = 2;
    private const int StatusOverdue = 3;
    private const int StatusPaid = 4;

    private readonly PaymentScheduleOverviewRepository _repository;
    private readonly IInstalmentStatusEngine _statusEngine;
    private readonly PortalDbContext _portalDbContext;

    public PaymentScheduleOverviewService(
        PaymentScheduleOverviewRepository repository,
        IInstalmentStatusEngine statusEngine,
        PortalDbContext portalDbContext)
    {
        _repository = repository;
        _statusEngine = statusEngine;
        _portalDbContext = portalDbContext;
    }

    public async Task<PaymentScheduleOverviewDto> GetOverviewAsync(int businessId)
    {
        // 1. Fetch raw rows
        var rawRows = await _repository.GetActiveSchedulesWithInstalmentsAsync(businessId);

        // 2. Get currency symbol from BusinessProfile (default "€")
        var currencySymbol = await GetCurrencySymbolAsync(businessId);

        // Handle empty state
        if (rawRows == null || rawRows.Count == 0)
        {
            return new PaymentScheduleOverviewDto
            {
                Kpis = new OverviewKpiDto(),
                Timeline = new List<MonthlyTimelineEntryDto>(),
                Schedules = new List<ScheduleTableRowDto>(),
                AvailableYears = new List<int>(),
                CurrencySymbol = currencySymbol
            };
        }

        // 3. Compute instalment status for each row
        var rowsWithStatus = rawRows.Select(row => new
        {
            Row = row,
            Status = _statusEngine.DetermineStatus(row.DueDate, row.Amount, row.MatchedAmount)
        }).ToList();

        // 4. Aggregate KPIs
        var totalScheduled = rowsWithStatus.Sum(r => r.Row.Amount);
        var collected = rowsWithStatus.Sum(r => r.Row.MatchedAmount);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueThisMonth = rowsWithStatus
            .Where(r => r.Row.DueDate.HasValue
                        && r.Row.DueDate.Value.Year == today.Year
                        && r.Row.DueDate.Value.Month == today.Month
                        && (r.Status == StatusPending || r.Status == StatusDue || r.Status == StatusOverdue))
            .Sum(r => r.Row.Amount);

        var overdue = rowsWithStatus
            .Where(r => r.Status == StatusOverdue)
            .Sum(r => r.Row.Amount - r.Row.MatchedAmount);

        var kpis = new OverviewKpiDto
        {
            TotalScheduled = totalScheduled,
            Collected = collected,
            DueThisMonth = dueThisMonth,
            Overdue = overdue
        };

        // 5. Build monthly timeline
        var timeline = new List<MonthlyTimelineEntryDto>();

        // Group instalments with due dates by year/month
        var datedInstalments = rowsWithStatus.Where(r => r.Row.DueDate.HasValue).ToList();
        var groupedByMonth = datedInstalments
            .GroupBy(r => new { r.Row.DueDate!.Value.Year, r.Row.DueDate!.Value.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month);

        foreach (var group in groupedByMonth)
        {
            var monthDate = new DateTime(group.Key.Year, group.Key.Month, 1);
            timeline.Add(new MonthlyTimelineEntryDto
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                MonthName = monthDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                TotalAmount = group.Sum(r => r.Row.Amount),
                InstalmentCount = group.Count(),
                HasOverdue = group.Any(r => r.Status == StatusOverdue),
                IsNoDueDate = false
            });
        }

        // Null-date instalments go into a special entry
        var noDueDateInstalments = rowsWithStatus.Where(r => !r.Row.DueDate.HasValue).ToList();
        if (noDueDateInstalments.Any())
        {
            timeline.Add(new MonthlyTimelineEntryDto
            {
                Year = 0,
                Month = 0,
                MonthName = "No date assigned",
                TotalAmount = noDueDateInstalments.Sum(r => r.Row.Amount),
                InstalmentCount = noDueDateInstalments.Count,
                HasOverdue = noDueDateInstalments.Any(r => r.Status == StatusOverdue),
                IsNoDueDate = true
            });
        }

        // Extract available years from instalment due dates
        var availableYears = datedInstalments
            .Select(r => r.Row.DueDate!.Value.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        // 6. Build table rows (group by ScheduleId)
        var scheduleGroups = rowsWithStatus.GroupBy(r => r.Row.ScheduleId);
        var scheduleRows = new List<ScheduleTableRowDto>();

        foreach (var scheduleGroup in scheduleGroups)
        {
            var firstRow = scheduleGroup.First().Row;
            var instalments = scheduleGroup.ToList();

            var scheduleTotal = instalments.Sum(i => i.Row.Amount);
            var paid = instalments.Sum(i => i.Row.MatchedAmount);
            var remaining = scheduleTotal - paid;

            // NextDue = earliest DueDate among instalments with status Due/Overdue/Pending
            var unpaidInstalments = instalments
                .Where(i => i.Status == StatusPending || i.Status == StatusDue || i.Status == StatusOverdue)
                .Where(i => i.Row.DueDate.HasValue)
                .OrderBy(i => i.Row.DueDate!.Value)
                .ToList();

            string? nextDue = unpaidInstalments.FirstOrDefault()?.Row.DueDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

            // ProgressPercentage = (Paid / ScheduleTotal) * 100, capped at 100
            var progressPercentage = scheduleTotal > 0
                ? (int)Math.Min((paid / scheduleTotal) * 100, 100)
                : 0;

            // Status determination
            string status;
            if (instalments.All(i => i.Status == StatusPaid))
            {
                status = "Completed";
            }
            else if (instalments.Any(i => i.Status == StatusOverdue))
            {
                status = "Has Overdue";
            }
            else
            {
                status = "On Track";
            }

            scheduleRows.Add(new ScheduleTableRowDto
            {
                ScheduleId = firstRow.ScheduleId,
                InvoiceId = firstRow.InvoiceId,
                InvoiceNumber = firstRow.InvoiceNumber,
                CustomerName = firstRow.CustomerName,
                ScheduleTotal = scheduleTotal,
                Paid = paid,
                Remaining = remaining,
                NextDue = nextDue,
                ProgressPercentage = progressPercentage,
                Status = status
            });
        }

        // 7. Sort table rows: "Has Overdue" first, then by NextDue date ascending
        scheduleRows = scheduleRows
            .OrderByDescending(r => r.Status == "Has Overdue")
            .ThenBy(r => r.NextDue == null ? DateTime.MaxValue : DateTime.ParseExact(r.NextDue, "dd MMM yyyy", CultureInfo.InvariantCulture))
            .ToList();

        // 8. Return assembled DTO
        return new PaymentScheduleOverviewDto
        {
            Kpis = kpis,
            Timeline = timeline,
            Schedules = scheduleRows,
            AvailableYears = availableYears,
            CurrencySymbol = currencySymbol
        };
    }

    /// <summary>
    /// Gets the currency symbol for the business from BusinessProfile.
    /// Falls back to "€" if no profile is found.
    /// </summary>
    private async Task<string> GetCurrencySymbolAsync(int businessId)
    {
        var currencySymbol = await _portalDbContext.BusinessProfiles
            .Where(bp => bp.BusinessId == businessId)
            .Select(bp => bp.CurrencySymbol)
            .FirstOrDefaultAsync();

        return currencySymbol ?? "€";
    }
}
