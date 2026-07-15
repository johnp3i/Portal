using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Evaluates platform health signals and assembles a narrative system briefing for SuperAdmin users.
/// Queries the Logging database for errors, slow queries, job status, and failed logins.
/// Queries the Portal database for business metrics.
/// </summary>
public class SystemBriefingService : ISystemBriefingService
{
    private const int MaxInsights = 6;
    private readonly LoggingDbContext _loggingDb;
    private readonly PortalDbContext _portalDb;
    private readonly IConfiguration _configuration;

    public SystemBriefingService(LoggingDbContext loggingDb, PortalDbContext portalDb, IConfiguration configuration)
    {
        _loggingDb = loggingDb;
        _portalDb = portalDb;
        _configuration = configuration;
    }

    public async Task<BriefingViewModel> GenerateBriefingAsync()
    {
        var insights = new List<BriefingInsight>();
        var now = DateTime.UtcNow;

        try { await EvaluateErrorTrend(now, insights); } catch { }
        try { await EvaluateBackgroundJobs(now, insights); } catch { }
        try { await EvaluateSlowQueries(now, insights); } catch { }
        try { await EvaluateFailedLogins(now, insights); } catch { }
        try { await EvaluateBusinessMetrics(insights); } catch { }
        try { EvaluateStorageUsage(insights); } catch { }

        insights = insights.OrderBy(i => i.Priority).Take(MaxInsights).ToList();

        var state = DetermineState(insights);

        return new BriefingViewModel
        {
            Greeting = "System Status",
            Subtitle = state switch
            {
                BriefingState.Critical => "Issues detected in the last 24 hours",
                BriefingState.Normal => "Elevated activity — monitoring recommended",
                BriefingState.AllClear => "All systems operational",
                _ => "Your operational briefing"
            },
            State = state,
            Insights = insights,
            DateDisplay = now.ToString("dd MMM yyyy, HH:mm") + " UTC"
        };
    }

    private async Task EvaluateErrorTrend(DateTime now, List<BriefingInsight> insights)
    {
        var last24h = now.AddHours(-24);
        var last48h = now.AddHours(-48);

        var currentCount = await _loggingDb.Logs
            .Where(l => (l.Level == "Error" || l.Level == "Fatal") && l.TimeStamp >= last24h)
            .CountAsync();

        var previousCount = await _loggingDb.Logs
            .Where(l => (l.Level == "Error" || l.Level == "Fatal") && l.TimeStamp >= last48h && l.TimeStamp < last24h)
            .CountAsync();

        if (currentCount == 0)
        {
            insights.Add(new BriefingInsight
            {
                Priority = 1,
                Severity = BriefingSeverity.Positive,
                Html = "<strong>0 errors</strong> in the last 24 hours. All services running normally."
            });
            return;
        }

        // Find most frequent source
        var topSource = await _loggingDb.Logs
            .Where(l => (l.Level == "Error" || l.Level == "Fatal") && l.TimeStamp >= last24h && l.SourceContext != null)
            .GroupBy(l => l.SourceContext)
            .OrderByDescending(g => g.Count())
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .FirstOrDefaultAsync();

        var sourceName = topSource?.Source?.Split('.').LastOrDefault() ?? "Unknown";

        // Find most frequent exception type
        var topException = await _loggingDb.Logs
            .Where(l => (l.Level == "Error" || l.Level == "Fatal") && l.TimeStamp >= last24h && l.Exception != null)
            .Select(l => l.Exception!)
            .FirstOrDefaultAsync();

        var exceptionType = "Exception";
        if (!string.IsNullOrEmpty(topException))
        {
            var colonIdx = topException.IndexOf(':');
            var dotIdx = topException.LastIndexOf('.', colonIdx > 0 ? colonIdx : topException.Length - 1);
            if (colonIdx > 0)
                exceptionType = topException.Substring(dotIdx + 1, colonIdx - dotIdx - 1).Trim();
            else if (topException.Length > 60)
                exceptionType = topException.Substring(0, 60);
        }

        // Determine severity
        var isSpike = previousCount > 0 && currentCount >= previousCount * 2;
        var pctChange = previousCount > 0 ? (int)Math.Round((double)(currentCount - previousCount) / previousCount * 100) : 100;
        var changeText = previousCount > 0 ? $" — up <strong>{pctChange}%</strong> from yesterday" : "";

        var severity = isSpike ? BriefingSeverity.Urgent : BriefingSeverity.Action;

        insights.Add(new BriefingInsight
        {
            Priority = 1,
            Severity = severity,
            Html = $"<strong>{currentCount} errors</strong> in the last 24 hours{changeText}. Most frequent: <code style=\"font-size:12px;background:rgba(194,74,74,.06);padding:2px 6px;border-radius:4px;color:#C24A4A;\">{exceptionType}</code> in <strong>{sourceName}</strong>. <a href=\"/Admin/SystemLogs?level=Error\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">View error logs →</a>"
        });
    }

    private async Task EvaluateBackgroundJobs(DateTime now, List<BriefingInsight> insights)
    {
        var last24h = now.AddHours(-24);

        // Check for PaymentReminder background service entries
        var recentJobLogs = await _loggingDb.Logs
            .Where(l => l.TimeStamp >= last24h
                && l.SourceContext != null
                && l.SourceContext.Contains("PaymentReminder"))
            .OrderByDescending(l => l.TimeStamp)
            .Take(5)
            .ToListAsync();

        if (recentJobLogs.Count == 0) return;

        var mostRecent = recentJobLogs.First();
        var isError = mostRecent.Level == "Error" || mostRecent.Level == "Fatal";

        if (isError)
        {
            var time = mostRecent.TimeStamp.ToString("HH:mm");
            insights.Add(new BriefingInsight
            {
                Priority = 2,
                Severity = BriefingSeverity.Urgent,
                Html = $"Payment Reminder background job <strong>failed</strong> at {time} UTC. <a href=\"/Admin/SystemLogs?sourceContext=PaymentReminder\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">View job status →</a>"
            });
        }
        else
        {
            var time = mostRecent.TimeStamp.ToString("HH:mm");
            insights.Add(new BriefingInsight
            {
                Priority = 2,
                Severity = BriefingSeverity.Positive,
                Html = $"Payment Reminder job completed successfully at {time} UTC."
            });
        }
    }

    private async Task EvaluateSlowQueries(DateTime now, List<BriefingInsight> insights)
    {
        var last24h = now.AddHours(-24);

        // EF Core logs slow commands at Warning level with "CommandExecuted" in the source
        var slowCount = await _loggingDb.Logs
            .Where(l => l.Level == "Warning"
                && l.TimeStamp >= last24h
                && l.SourceContext != null
                && l.SourceContext.Contains("Database.Command"))
            .CountAsync();

        if (slowCount == 0) return;

        var severity = slowCount > 10 ? BriefingSeverity.Action : BriefingSeverity.Positive;

        insights.Add(new BriefingInsight
        {
            Priority = 3,
            Severity = severity,
            Html = $"<strong>{slowCount} slow queries</strong> exceeded 1 second in the last 24 hours. <a href=\"/Admin/SystemLogs?level=Warning\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">View slow queries →</a>"
        });
    }

    private async Task EvaluateFailedLogins(DateTime now, List<BriefingInsight> insights)
    {
        var last24h = now.AddHours(-24);

        var failedLogins = await _loggingDb.Logs
            .Where(l => l.TimeStamp >= last24h
                && l.Level == "Warning"
                && l.Message != null
                && (l.Message.Contains("login failed") || l.Message.Contains("Invalid login") || l.Message.Contains("Access denied")))
            .CountAsync();

        if (failedLogins == 0) return;

        var severity = failedLogins > 5 ? BriefingSeverity.Action : BriefingSeverity.Positive;

        insights.Add(new BriefingInsight
        {
            Priority = 4,
            Severity = severity,
            Html = $"<strong>{failedLogins} failed login attempts</strong> in the last 24 hours. <a href=\"/Admin/SystemLogs?level=Warning\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">Review security →</a>"
        });
    }

    private async Task EvaluateBusinessMetrics(List<BriefingInsight> insights)
    {
        var activeBusinesses = await _portalDb.Set<Portal.Infrastructure.Entities.BusinessPlan>()
            .IgnoreQueryFilters()
            .Where(bp => bp.IsActive)
            .Select(bp => bp.BusinessId)
            .Distinct()
            .CountAsync();

        insights.Add(new BriefingInsight
        {
            Priority = 5,
            Severity = BriefingSeverity.Positive,
            Html = $"<strong>{activeBusinesses} businesses</strong> active with subscriptions."
        });
    }

    private void EvaluateStorageUsage(List<BriefingInsight> insights)
    {
        var basePath = _configuration["FileStorage:BasePath"];
        if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            return;

        var totalBytes = Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);

        var sizeDisplay = totalBytes < 1024 * 1024 * 1024
            ? $"{totalBytes / (1024.0 * 1024.0):N1} MB"
            : $"{totalBytes / (1024.0 * 1024.0 * 1024.0):N2} GB";

        var severity = totalBytes > 10L * 1024 * 1024 * 1024
            ? BriefingSeverity.Action
            : BriefingSeverity.Positive;

        insights.Add(new BriefingInsight
        {
            Priority = 6,
            Severity = severity,
            Html = $"Upload storage: <strong>{sizeDisplay}</strong> used across all businesses. {(severity == BriefingSeverity.Action ? "Consider cleanup." : "No cleanup needed.")}"
        });
    }

    private static BriefingState DetermineState(List<BriefingInsight> insights)
    {
        if (insights.Any(i => i.Severity == BriefingSeverity.Urgent))
            return BriefingState.Critical;

        if (insights.Any(i => i.Severity == BriefingSeverity.Action))
            return BriefingState.Normal; // Warning state uses "Normal" enum value

        return BriefingState.AllClear;
    }
}
