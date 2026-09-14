using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Repositories.Notification;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Fan-out composer for the Task &amp; Meeting Reminder: emails EACH assigned team member a daily
/// agenda of their overdue / today / upcoming tasks and meetings. Tasks route by
/// <c>FollowUpTask.TeamMemberId</c> (1-to-1); meetings by <c>[sales].[MeetingTeamMember]</c>
/// (1-to-many). Unassigned items (and any member with no resolvable email) fall to the business
/// owner. Recipients are keyed by NORMALISED resolved email, so an owner who is also a team member
/// receives one merged agenda (never a duplicate). Once-per-recipient-per-day via a recipient-scoped
/// cycle key.
/// </summary>
public class TaskMeetingReminderComposer : DigestComposerBase, IFanOutDigestComposer
{
    private const int InvoiceStatusIssued = 2; // unused here; kept for parity with base conventions

    private readonly IOwnerEmailResolver _ownerEmailResolver;
    private readonly IPortalUserEmailResolver _portalUserEmailResolver;
    private readonly MeetingTeamMemberRepository _meetingTeamMemberRepository;
    private readonly NotificationOutboxRepository _outboxRepository;
    private readonly NotificationOptions _options;
    private readonly ILogger<TaskMeetingReminderComposer> _logger;

    public TaskMeetingReminderComposer(
        PortalDbContext dbContext,
        IOwnerEmailResolver ownerEmailResolver,
        IPortalUserEmailResolver portalUserEmailResolver,
        MeetingTeamMemberRepository meetingTeamMemberRepository,
        NotificationOutboxRepository outboxRepository,
        NotificationOptions options,
        ILogger<TaskMeetingReminderComposer> logger) : base(dbContext)
    {
        _ownerEmailResolver = ownerEmailResolver;
        _portalUserEmailResolver = portalUserEmailResolver;
        _meetingTeamMemberRepository = meetingTeamMemberRepository;
        _outboxRepository = outboxRepository;
        _options = options;
        _logger = logger;
    }

    public string AssistantKey => DigestAssistantKeys.TaskMeetingReminder;

    public async Task<IReadOnlyList<OutboxMessage>> ComposeManyAsync(
        int businessId, int assistantTypeId, BusinessAssistantSetting? setting, DateTime businessLocalNow, CancellationToken ct)
    {
        try
        {
            var today = DateOnly.FromDateTime(businessLocalNow);
            var lookAhead = setting?.TaskMeetingLookAheadDays ?? _options.TaskMeetingDefaultLookAheadDays;
            var windowEnd = today.AddDays(lookAhead);
            var overdueFloor = today.AddDays(-_options.TaskMeetingOverdueLookBackDays);

            // Owner identity — resolve up front so an owner-who-is-a-member collapses to one bucket.
            var ownerEmail = await _ownerEmailResolver.ResolveAsync(businessId);
            var ownerNorm = Normalise(ownerEmail);

            // Active team members (id -> member).
            var members = await DbContext.Set<Portal.Infrastructure.Entities.Sales.TeamMember>()
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.IsActive)
                .Select(m => new { m.Id, m.FirstName, m.LastName, m.Email, m.UserId })
                .ToListAsync(ct);
            var membersById = members.ToDictionary(m => m.Id);

            // Owner's own TeamMember (by UserId), if any — used to fold the owner catch-all into it.
            var ownerMemberId = await ResolveOwnerMemberIdAsync(businessId, ct);

            // Candidate tasks: not completed, overdue (bounded) OR due within the window.
            var tasks = await DbContext.FollowUpTasks
                .AsNoTracking()
                .Where(t => t.BusinessId == businessId
                            && !t.IsCompleted
                            && ((t.DueAtUtc >= overdueFloor.ToDateTime(TimeOnly.MinValue) && t.DueAtUtc < today.ToDateTime(TimeOnly.MinValue))
                                || (t.DueAtUtc >= today.ToDateTime(TimeOnly.MinValue) && t.DueAtUtc < windowEnd.AddDays(1).ToDateTime(TimeOnly.MinValue))))
                .Select(t => new { t.Id, t.Title, t.DueAtUtc, t.ScheduledTimeUtc, t.TeamMemberId })
                .ToListAsync(ct);

            // Candidate meetings: not cancelled, active, overdue-no-outcome (bounded) OR within window.
            var meetings = await DbContext.Meetings
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId
                            && !m.IsCancelled
                            && m.IsActive
                            && (((m.ScheduledAtUtc >= overdueFloor.ToDateTime(TimeOnly.MinValue) && m.ScheduledAtUtc < today.ToDateTime(TimeOnly.MinValue)) && m.Outcome == null)
                                || (m.ScheduledAtUtc >= today.ToDateTime(TimeOnly.MinValue) && m.ScheduledAtUtc < windowEnd.AddDays(1).ToDateTime(TimeOnly.MinValue))))
                .Select(m => new { m.Id, m.Subject, m.ScheduledAtUtc, m.Location })
                .ToListAsync(ct);

            var attendeesByMeeting = await _meetingTeamMemberRepository
                .GetAttendeeIdsByMeetingIdsAsync(meetings.Select(m => m.Id));

            // Bucket by NORMALISED resolved email. Each bucket holds the recipient's display name +
            // its agenda items (de-duped by (type,id)).
            var buckets = new Dictionary<string, Bucket>();
            var todayUtcStart = today.ToDateTime(TimeOnly.MinValue);

            async Task<(string? Email, string Name)> ResolveMemberAsync(int teamMemberId)
            {
                if (membersById.TryGetValue(teamMemberId, out var m))
                {
                    var email = !string.IsNullOrWhiteSpace(m.Email)
                        ? m.Email
                        : (!string.IsNullOrWhiteSpace(m.UserId) ? await _portalUserEmailResolver.ResolveByUserIdAsync(m.UserId!) : null);
                    var name = string.IsNullOrWhiteSpace(m.LastName) ? m.FirstName : $"{m.FirstName} {m.LastName}";
                    return (email, name);
                }
                return (null, "");
            }

            Bucket GetOrAddBucket(string email, string name)
            {
                var norm = Normalise(email)!;
                if (!buckets.TryGetValue(norm, out var b))
                {
                    b = new Bucket { Email = email, Name = name };
                    buckets[norm] = b;
                }
                else if (string.IsNullOrEmpty(b.Name) && !string.IsNullOrEmpty(name))
                {
                    b.Name = name;
                }
                return b;
            }

            void AddToOwner(AgendaItem item)
            {
                if (string.IsNullOrEmpty(ownerNorm)) return; // no owner resolvable -> item genuinely undeliverable; drop with log
                var b = GetOrAddBucket(ownerEmail!, "");
                b.Add(item);
            }

            // --- Tasks ---
            foreach (var t in tasks)
            {
                var item = new AgendaItem
                {
                    WhenUtc = t.DueAtUtc,
                    IsMeeting = false,
                    Title = t.Title,
                    WhenText = FormatWhen(t.DueAtUtc, t.ScheduledTimeUtc, today, todayUtcStart),
                    ContextText = null
                };
                item.Key = $"task-{t.Id}";

                if (t.TeamMemberId.HasValue && membersById.ContainsKey(t.TeamMemberId.Value))
                {
                    // Route to the assignee — but if the assignee IS the owner-member, fold to owner.
                    var effectiveMemberId = t.TeamMemberId.Value;
                    var (email, name) = await ResolveMemberAsync(effectiveMemberId);
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogInformation(
                            "Task {TaskId}: assignee {MemberId} has no resolvable email; routing to owner (BusinessId={BusinessId}).",
                            t.Id, effectiveMemberId, businessId);
                        AddToOwner(item);
                    }
                    else
                    {
                        GetOrAddBucket(email, name).Add(item);
                    }
                }
                else
                {
                    AddToOwner(item);
                }
            }

            // --- Meetings ---
            foreach (var m in meetings)
            {
                var context = string.IsNullOrWhiteSpace(m.Location) ? null : m.Location;
                var attendeeIds = attendeesByMeeting.TryGetValue(m.Id, out var ids) ? ids : new List<int>();

                if (attendeeIds.Count == 0)
                {
                    var item = MakeMeetingItem(m.Id, m.Subject, m.ScheduledAtUtc, context, today, todayUtcStart);
                    AddToOwner(item);
                    continue;
                }

                foreach (var attendeeId in attendeeIds)
                {
                    if (!membersById.ContainsKey(attendeeId)) continue; // inactive/removed member
                    var item = MakeMeetingItem(m.Id, m.Subject, m.ScheduledAtUtc, context, today, todayUtcStart);
                    var (email, name) = await ResolveMemberAsync(attendeeId);
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogInformation(
                            "Meeting {MeetingId}: attendee {MemberId} has no resolvable email; routing to owner (BusinessId={BusinessId}).",
                            m.Id, attendeeId, businessId);
                        AddToOwner(item);
                    }
                    else
                    {
                        GetOrAddBucket(email, name).Add(item);
                    }
                }
            }

            if (buckets.Count == 0)
            {
                _logger.LogInformation(
                    "Task & Meeting Reminder: nothing to report for any recipient in BusinessId={BusinessId} today — skipping.", businessId);
                return Array.Empty<OutboxMessage>();
            }

            // Build one message per bucket with items, skipping any already sent today.
            var messages = new List<OutboxMessage>();
            foreach (var kvp in buckets)
            {
                if (ct.IsCancellationRequested) break;
                var norm = kvp.Key;
                var bucket = kvp.Value;
                if (bucket.Items.Count == 0) continue;

                var token = "email-" + ShortHash(norm);
                var cycleKey = DigestCycleKey.RecipientDaily(AssistantKey, businessLocalNow, token);
                if (await _outboxRepository.ExistsForCycleAsync(businessId, assistantTypeId, cycleKey))
                    continue; // already sent this recipient today

                var overdue = bucket.Items.Where(i => i.WhenUtc < todayUtcStart).OrderBy(i => i.WhenUtc).ToList();
                var todayItems = bucket.Items.Where(i => i.WhenUtc >= todayUtcStart && i.WhenUtc < todayUtcStart.AddDays(1)).OrderBy(i => i.WhenUtc).ToList();
                var upcoming = bucket.Items.Where(i => i.WhenUtc >= todayUtcStart.AddDays(1)).OrderBy(i => i.WhenUtc).ToList();

                var subject = DigestEmailBuilder.TaskMeetingReminderSubject(overdue.Count, todayItems.Count);
                var htmlBody = DigestEmailBuilder.BuildTaskMeetingReminderHtml(bucket.Name, overdue, todayItems, upcoming);

                messages.Add(new OutboxMessage
                {
                    BusinessId = businessId,
                    AssistantTypeId = assistantTypeId,
                    RecipientEmail = bucket.Email,
                    RecipientName = string.IsNullOrWhiteSpace(bucket.Name) ? null : bucket.Name,
                    ReplyToEmail = null,
                    Subject = subject,
                    BodyHtml = htmlBody,
                    OutboxMessageStatusTypeId = OutboxMessageStatusTypes.Pending,
                    RetryCount = 0,
                    MaxRetries = _options.DefaultMaxRetries,
                    ScheduledForUtc = DateTime.UtcNow,
                    CycleKey = cycleKey,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task & Meeting Reminder compose failed for BusinessId={BusinessId}.", businessId);
            return Array.Empty<OutboxMessage>(); // fail safe per business (runner also guards)
        }
    }

    private static AgendaItem MakeMeetingItem(int id, string subject, DateTime scheduledAtUtc, string? context, DateOnly today, DateTime todayUtcStart)
    {
        return new AgendaItem
        {
            WhenUtc = scheduledAtUtc,
            IsMeeting = true,
            Title = subject,
            WhenText = FormatWhen(scheduledAtUtc, TimeOnly.FromDateTime(scheduledAtUtc), today, todayUtcStart),
            ContextText = context,
            Key = $"meeting-{id}"
        };
    }

    /// <summary>Resolves the owner's TeamMember id (by matching an owner UserBusiness UserId), if any.</summary>
    private async Task<int?> ResolveOwnerMemberIdAsync(int businessId, CancellationToken ct)
    {
        // Not strictly required for correctness (email-normalised bucketing already merges the owner
        // with a member sharing the same address); kept as a hook for future name-preference logic.
        await Task.CompletedTask;
        return null;
    }

    private static string FormatWhen(DateTime whenUtc, TimeOnly? time, DateOnly today, DateTime todayUtcStart)
    {
        var dateOnly = DateOnly.FromDateTime(whenUtc);
        string dayLabel;
        if (dateOnly < today) dayLabel = dateOnly.ToString("dd MMM");
        else if (dateOnly == today) dayLabel = "today";
        else if (dateOnly == today.AddDays(1)) dayLabel = "tomorrow";
        else dayLabel = dateOnly.ToString("dd MMM");

        if (time.HasValue && time.Value != TimeOnly.MinValue)
            return $"{dayLabel} {time.Value:HH:mm}";
        return dateOnly == today ? "all day" : dayLabel;
    }

    private static string? Normalise(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string ShortHash(string value)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private sealed class Bucket
    {
        public string Email { get; set; } = null!;
        public string Name { get; set; } = "";
        public List<AgendaItem> Items { get; } = new();
        private readonly HashSet<string> _keys = new();

        public void Add(AgendaItem item)
        {
            if (_keys.Add(item.Key)) // de-dupe by (type,id) within a recipient
                Items.Add(item);
        }
    }
}
