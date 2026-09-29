namespace Portal.Infrastructure.Entities.Storage;

/// <summary>
/// Reference (lookup) table of orphaned-file candidate statuses. The <see cref="Description"/> is
/// surfaced on the admin cleanup page (status legend) and documents each code for DB admins.
/// Schema: [Storage].OrphanedFileStatusType. Static seed data.
/// </summary>
public class OrphanedFileStatusType
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}

/// <summary>Well-known status ids (match the seed in migration 218).</summary>
public static class OrphanedFileStatus
{
    public const int Pending = 1;
    public const int Paused = 2;
    public const int Cancelled = 3;
    public const int Deleted = 4;
}
