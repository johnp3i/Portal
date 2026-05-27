using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Serilog;
using System.Security.Claims;
using System.Text.Json;

namespace Portal.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChangesInterceptor that automatically captures Insert, Update, and Delete
/// operations across PortalDbContext and writes AuditLog records.
///
/// Two-phase capture:
///   Phase 1 (SavingChanges/SavingChangesAsync): builds AuditEntry objects from the change
///   tracker before the save. For Modified/Deleted entities the PK is read now. For Added
///   entities the RecordId is left empty — identity PKs are not yet assigned.
///
///   Phase 2 (SavedChanges/SavedChangesAsync): fills in identity PKs for Added entries,
///   then writes all AuditLog records via AuditLogRepository.InsertAsync. Failures are
///   caught, logged via Serilog, and swallowed — the main save has already succeeded.
///
/// Registered as scoped to match PortalDbContext lifetime. A simple instance field
/// _pendingEntries is therefore safe (one interceptor instance per request).
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenantService _tenantService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditLogRepository _auditLogRepository;

    // Safe as instance field because the interceptor is registered as scoped.
    private List<AuditEntry>? _pendingEntries;

    public AuditInterceptor(
        ICurrentTenantService tenantService,
        IHttpContextAccessor httpContextAccessor,
        AuditLogRepository auditLogRepository)
    {
        _tenantService = tenantService;
        _httpContextAccessor = httpContextAccessor;
        _auditLogRepository = auditLogRepository;
    }

    // -------------------------------------------------------------------------
    // Phase 1 — capture pre-save state
    // -------------------------------------------------------------------------

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        _pendingEntries = BuildPendingEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _pendingEntries = BuildPendingEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Phase 2 — write audit records after the save succeeds
    // -------------------------------------------------------------------------

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        // Fire-and-forget: run async work synchronously on the calling thread.
        // This is acceptable here because SavedChanges is rarely called on a hot path
        // (most callers use SavedChangesAsync). The sync override is provided for
        // completeness and correctness.
        WriteAuditEntriesAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await WriteAuditEntriesAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Iterates the change tracker and builds an AuditEntry for each qualifying entity.
    /// AuditLog entities are skipped (recursion guard).
    /// </summary>
    private List<AuditEntry> BuildPendingEntries(DbContext? context)
    {
        if (context is null)
            return new List<AuditEntry>();

        var timestamp = DateTime.UtcNow;
        var businessId = _tenantService.CurrentBusinessId == 0
            ? (int?)null
            : _tenantService.CurrentBusinessId;
        var userId = _httpContextAccessor.HttpContext?
            .User?.FindFirstValue(ClaimTypes.NameIdentifier);

        var entries = new List<AuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Recursion guard — never audit the AuditLog table itself.
            if (entry.Entity is AuditLog)
                continue;

            // Only track meaningful state changes.
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var tableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            var action = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => string.Empty
            };

            string recordId;
            string? oldValues;
            string? newValues;

            if (entry.State == EntityState.Added)
            {
                // PK not yet assigned — will be filled in Phase 2.
                recordId = string.Empty;
                oldValues = null;
                newValues = SerializeScalarProperties(entry.Properties);
            }
            else if (entry.State == EntityState.Modified)
            {
                recordId = GetPrimaryKeyValue(entry);
                var modifiedProperties = entry.Properties
                    .Where(p => p.IsModified && IsScalarProperty(p))
                    .ToList();
                oldValues = SerializeOriginalValues(modifiedProperties);
                newValues = SerializeCurrentValues(modifiedProperties);
            }
            else // Deleted
            {
                recordId = GetPrimaryKeyValue(entry);
                oldValues = SerializeScalarProperties(entry.Properties);
                newValues = null;
            }

            entries.Add(new AuditEntry
            {
                Entry = entry,
                Action = action,
                TableName = tableName,
                OldValues = oldValues,
                NewValues = newValues,
                RecordId = recordId,
                BusinessId = businessId,
                UserId = userId,
                Timestamp = timestamp
            });
        }

        return entries;
    }

    /// <summary>
    /// Fills identity PKs for Added entries, then writes all pending AuditLog records.
    /// Failures per entry are caught, logged, and swallowed.
    /// </summary>
    private async Task WriteAuditEntriesAsync(CancellationToken cancellationToken)
    {
        var pending = _pendingEntries;
        _pendingEntries = null;

        if (pending is null || pending.Count == 0)
            return;

        // Fill in identity-generated PKs for Added entries now that the save has completed.
        foreach (var auditEntry in pending.Where(e => e.Action == "Insert"))
        {
            auditEntry.RecordId = auditEntry.Entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                ?.CurrentValue
                ?.ToString()
                ?? string.Empty;
        }

        // Write each audit record; swallow failures so the main save is unaffected.
        foreach (var auditEntry in pending)
        {
            try
            {
                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = auditEntry.BusinessId,
                    UserId = auditEntry.UserId,
                    Action = auditEntry.Action,
                    TableName = auditEntry.TableName,
                    RecordId = auditEntry.RecordId,
                    OldValues = auditEntry.OldValues,
                    NewValues = auditEntry.NewValues,
                    Timestamp = auditEntry.Timestamp
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "AuditInterceptor: failed to write audit log entry. " +
                    "Action={Action}, TableName={TableName}, RecordId={RecordId}",
                    auditEntry.Action, auditEntry.TableName, auditEntry.RecordId);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Property serialization helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true for scalar (non-shadow, non-navigation) properties that can be
    /// meaningfully serialized: value types and strings.
    /// </summary>
    private static bool IsScalarProperty(PropertyEntry p)
        => !p.Metadata.IsShadowProperty()
           && (p.Metadata.ClrType.IsValueType || p.Metadata.ClrType == typeof(string));

    /// <summary>
    /// Serializes all scalar non-shadow properties using their current values.
    /// Used for Added (NewValues) and Deleted (OldValues) entries.
    /// </summary>
    private static string? SerializeScalarProperties(IEnumerable<PropertyEntry> properties)
    {
        var dict = properties
            .Where(IsScalarProperty)
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Serializes the original (pre-save) values of the supplied properties.
    /// Used for Modified entries (OldValues).
    /// </summary>
    private static string? SerializeOriginalValues(IEnumerable<PropertyEntry> properties)
    {
        var dict = properties
            .Where(IsScalarProperty)
            .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);

        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Serializes the current (post-save) values of the supplied properties.
    /// Used for Modified entries (NewValues).
    /// </summary>
    private static string? SerializeCurrentValues(IEnumerable<PropertyEntry> properties)
    {
        var dict = properties
            .Where(IsScalarProperty)
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Reads the primary key value from the entity entry as a string.
    /// Returns an empty string if no PK property is found.
    /// </summary>
    private static string GetPrimaryKeyValue(EntityEntry entry)
        => entry.Properties
            .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
            ?.CurrentValue
            ?.ToString()
            ?? string.Empty;

    // -------------------------------------------------------------------------
    // Inner type — pending audit record (pre-write)
    // -------------------------------------------------------------------------

    private sealed class AuditEntry
    {
        /// <summary>The EF Core entity entry — used in Phase 2 to read identity PKs.</summary>
        public EntityEntry Entry { get; init; } = null!;

        /// <summary>"Insert" | "Update" | "Delete"</summary>
        public string Action { get; init; } = null!;

        public string TableName { get; init; } = null!;

        /// <summary>JSON of original values for Update/Delete; null for Insert.</summary>
        public string? OldValues { get; init; }

        /// <summary>JSON of current values for Insert/Update; null for Delete. Set after save for Insert.</summary>
        public string? NewValues { get; set; }

        /// <summary>PK value. Empty for Added entries until Phase 2 fills it in.</summary>
        public string RecordId { get; set; } = string.Empty;

        public int? BusinessId { get; init; }

        public string? UserId { get; init; }

        /// <summary>Captured at Phase 1 (pre-save) via DateTime.UtcNow.</summary>
        public DateTime Timestamp { get; init; }
    }
}
