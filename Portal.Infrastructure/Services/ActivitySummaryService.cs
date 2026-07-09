using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Transforms raw AuditLog records into business-friendly activity summaries.
/// </summary>
public class ActivitySummaryService : IActivitySummaryService
{
    private readonly PortalDbContext _dbContext;
    private readonly UserNameResolver _userNameResolver;
    private readonly ICurrentTenantService _tenantService;

    // TableName → Friendly entity type
    private static readonly Dictionary<string, string> EntityTypeMap = new()
    {
        ["Invoice"] = "Invoice",
        ["InvoiceLine"] = "Invoice",
        ["Quotation"] = "Quotation",
        ["QuotationLine"] = "Quotation",
        ["QuotationContact"] = "Quotation",
        ["Customer"] = "Customer",
        ["Purchase"] = "Purchase",
        ["Payment"] = "Payment",
        ["CreditNote"] = "Credit Note",
        ["CreditNoteLine"] = "Credit Note",
        ["Business"] = "Settings",
        ["BusinessProfile"] = "Settings"
    };

    // Entity type → detail route pattern
    private static readonly Dictionary<string, string> EntityRoutes = new()
    {
        ["Invoice"] = "/Invoice/Details/",
        ["Customer"] = "/Customer/Details/",
        ["Quotation"] = "/Quotation/Details/",
        ["Purchase"] = "/Purchase/Details/"
    };

    // Keys to look for to extract a human-readable entity identifier
    private static readonly string[] IdentifierKeys = { "InvoiceNumber", "QuotationNumber", "Name", "CompanyName", "Description", "Label" };

    public ActivitySummaryService(PortalDbContext dbContext, UserNameResolver userNameResolver, ICurrentTenantService tenantService)
    {
        _dbContext = dbContext;
        _userNameResolver = userNameResolver;
        _tenantService = tenantService;
    }

    public async Task<List<ActivityItemDto>> TransformAsync(List<AuditLog> records)
    {
        try
        {
            if (records.Count == 0) return new List<ActivityItemDto>();

            // Batch-resolve user names
            var userIds = records.Select(r => r.UserId).Distinct();
            var userNames = await _userNameResolver.ResolveNamesAsync(userIds);

            var items = new List<ActivityItemDto>();
            foreach (var record in records)
            {
                items.Add(TransformSingle(record, userNames));
            }

            return items;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ActivityStatsDto> GetQuickStatsAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);

            var weekRecords = await _dbContext.AuditLogs
                .Where(a => a.BusinessId == businessId && a.Timestamp >= sevenDaysAgo)
                .ToListAsync();

            if (weekRecords.Count == 0)
            {
                return new ActivityStatsDto
                {
                    ChangesThisWeek = 0,
                    ActiveTeamMembers = 0,
                    MostActiveArea = "None",
                    LastActivityUtc = null
                };
            }

            var totalChanges = weekRecords.Count;
            var activeMembers = weekRecords.Where(r => r.UserId != null).Select(r => r.UserId).Distinct().Count();
            var mostActiveTableName = weekRecords.GroupBy(r => r.TableName).OrderByDescending(g => g.Count()).First().Key;
            var mostActiveArea = EntityTypeMap.GetValueOrDefault(mostActiveTableName, mostActiveTableName);
            var lastActivity = weekRecords.Max(r => r.Timestamp);

            return new ActivityStatsDto
            {
                ChangesThisWeek = totalChanges,
                ActiveTeamMembers = activeMembers,
                MostActiveArea = mostActiveArea,
                LastActivityUtc = lastActivity
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private ActivityItemDto TransformSingle(AuditLog record, Dictionary<string, string> userNames)
    {
        var actorName = _userNameResolver.GetDisplayName(userNames, record.UserId);
        var entityType = EntityTypeMap.GetValueOrDefault(record.TableName, record.TableName);
        var actionType = GetActionType(record);
        var isStatusChange = actionType == "StatusChanged";

        // Parse JSON values
        var oldVals = TryParseJson(record.OldValues);
        var newVals = TryParseJson(record.NewValues);

        // Resolve entity identifier
        var entityDisplayRef = ResolveEntityIdentifier(record, oldVals, newVals);

        // Generate detail URL
        string? entityDetailUrl = null;
        if (record.Action != "Delete" && EntityRoutes.TryGetValue(entityType, out var route))
        {
            entityDetailUrl = route + record.RecordId;
        }

        // Build summary
        var summary = BuildSummary(actorName, actionType, entityType, entityDisplayRef, oldVals, newVals, isStatusChange);

        // Parse field changes for detail panel
        var changedFields = BuildChangedFields(record.Action, oldVals, newVals);

        // Resolve status names for status changes
        string? oldStatus = null;
        string? newStatus = null;
        if (isStatusChange)
        {
            (oldStatus, newStatus) = ResolveStatusNames(oldVals, newVals);
        }

        return new ActivityItemDto
        {
            Id = record.Id,
            Summary = summary,
            ActorName = actorName,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = record.RecordId,
            EntityDisplayRef = entityDisplayRef,
            EntityDetailUrl = entityDetailUrl,
            TimestampUtc = record.Timestamp,
            OldValues = record.OldValues,
            NewValues = record.NewValues,
            ChangedFields = changedFields,
            IsStatusChange = isStatusChange,
            OldStatus = oldStatus,
            NewStatus = newStatus
        };
    }

    private static string GetActionType(AuditLog record)
    {
        if (record.Action == "Insert") return "Created";
        if (record.Action == "Delete") return "Deleted";
        if (record.Action == "Update")
        {
            // Check if it's a status change
            var newVals = TryParseJson(record.NewValues);
            if (newVals.Keys.Any(k => k.EndsWith("StatusTypeId") || k == "Status"))
                return "StatusChanged";
            return "Edited";
        }
        return "Edited";
    }

    private static string BuildSummary(string actorName, string actionType, string entityType, string? entityRef, Dictionary<string, object?> oldVals, Dictionary<string, object?> newVals, bool isStatusChange)
    {
        var entityDisplay = !string.IsNullOrEmpty(entityRef) ? $"{entityType} {entityRef}" : entityType;

        return actionType switch
        {
            "Created" => $"{actorName} created {entityDisplay}",
            "Deleted" => $"{actorName} deleted {entityDisplay}",
            "StatusChanged" => BuildStatusChangeSummary(actorName, entityDisplay, oldVals, newVals),
            "Edited" => BuildEditSummary(actorName, entityDisplay, newVals),
            _ => $"{actorName} modified {entityDisplay}"
        };
    }

    private static string BuildStatusChangeSummary(string actorName, string entityDisplay, Dictionary<string, object?> oldVals, Dictionary<string, object?> newVals)
    {
        var statusKey = newVals.Keys.FirstOrDefault(k => k.EndsWith("StatusTypeId") || k == "Status");
        if (statusKey == null) return $"{actorName} changed status of {entityDisplay}";

        var oldVal = oldVals.GetValueOrDefault(statusKey)?.ToString() ?? "?";
        var newVal = newVals.GetValueOrDefault(statusKey)?.ToString() ?? "?";
        return $"{actorName} changed status of {entityDisplay} from {oldVal} to {newVal}";
    }

    private static string BuildEditSummary(string actorName, string entityDisplay, Dictionary<string, object?> newVals)
    {
        var changedKeys = newVals.Keys.Take(2).ToList();
        if (changedKeys.Count == 0) return $"{actorName} edited {entityDisplay}";

        var fieldsSummary = string.Join(", ", changedKeys.Select(k => k.ToLower()));
        return $"{actorName} edited {entityDisplay} — updated {fieldsSummary}";
    }

    private static string? ResolveEntityIdentifier(AuditLog record, Dictionary<string, object?> oldVals, Dictionary<string, object?> newVals)
    {
        var sourceVals = record.Action == "Delete" ? oldVals : newVals;
        foreach (var key in IdentifierKeys)
        {
            if (sourceVals.TryGetValue(key, out var val) && val != null)
            {
                var str = val.ToString();
                if (!string.IsNullOrEmpty(str)) return str;
            }
        }
        return null;
    }

    private static List<FieldChangeDto>? BuildChangedFields(string action, Dictionary<string, object?> oldVals, Dictionary<string, object?> newVals)
    {
        if (action == "Insert")
        {
            return newVals.Select(kv => new FieldChangeDto
            {
                FieldName = kv.Key,
                OldValue = null,
                NewValue = kv.Value?.ToString()
            }).ToList();
        }

        if (action == "Delete")
        {
            return oldVals.Select(kv => new FieldChangeDto
            {
                FieldName = kv.Key,
                OldValue = kv.Value?.ToString(),
                NewValue = null
            }).ToList();
        }

        if (action == "Update")
        {
            var allKeys = oldVals.Keys.Union(newVals.Keys).Distinct();
            return allKeys.Select(key => new FieldChangeDto
            {
                FieldName = key,
                OldValue = oldVals.GetValueOrDefault(key)?.ToString(),
                NewValue = newVals.GetValueOrDefault(key)?.ToString()
            }).ToList();
        }

        return null;
    }

    private static (string? OldStatus, string? NewStatus) ResolveStatusNames(Dictionary<string, object?> oldVals, Dictionary<string, object?> newVals)
    {
        var statusKey = newVals.Keys.FirstOrDefault(k => k.EndsWith("StatusTypeId") || k == "Status");
        if (statusKey == null) return (null, null);

        return (oldVals.GetValueOrDefault(statusKey)?.ToString(), newVals.GetValueOrDefault(statusKey)?.ToString());
    }

    private static Dictionary<string, object?> TryParseJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, object?>();
        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
            return result;
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>();
        }
    }
}
