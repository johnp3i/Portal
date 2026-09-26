namespace Portal.Infrastructure.Models.Storage;

/// <summary>Byte-size formatting shared by the storage pages (auto KB / MB / GB).</summary>
public static class StorageFormat
{
    public static string Bytes(long bytes)
    {
        if (bytes <= 0) return "0 KB";
        const double kb = 1024d, mb = kb * 1024d, gb = mb * 1024d;
        if (bytes >= gb) return $"{bytes / gb:0.##} GB";
        if (bytes >= mb) return $"{bytes / mb:0.##} MB";
        return $"{bytes / kb:0.##} KB";
    }
}

/// <summary>One category row in the per-business "by category" breakdown.</summary>
public class StorageCategoryDto
{
    public string Name { get; set; } = null!;
    public int Files { get; set; }
    public long Bytes { get; set; }
    /// <summary>True for Signatures — shown as "not counted yet" with no size.</summary>
    public bool NotCountedYet { get; set; }

    public string SizeDisplay => NotCountedYet ? "—" : StorageFormat.Bytes(Bytes);
}

/// <summary>Document-attachment breakdown by record type (Purchase, Invoice, …).</summary>
public class StorageTypeDto
{
    public string RecordType { get; set; } = null!;
    public int Files { get; set; }
    public long Bytes { get; set; }
    public string SizeDisplay => StorageFormat.Bytes(Bytes);
}

/// <summary>Per-business storage summary for the My Business → Storage tab.</summary>
public class BusinessStorageDto
{
    public long TotalBytes { get; set; }
    public string TotalDisplay => StorageFormat.Bytes(TotalBytes);

    /// <summary>Plan storage cap in bytes; null = unlimited (no cap set).</summary>
    public long? LimitBytes { get; set; }
    public bool HasLimit => LimitBytes.HasValue && LimitBytes.Value > 0;
    public string LimitDisplay => HasLimit ? StorageFormat.Bytes(LimitBytes!.Value) : "No limit";

    /// <summary>Percentage of the plan limit used (0–100+, capped at 100 for the bar width).</summary>
    public int UsedPercent => HasLimit ? (int)Math.Round(TotalBytes * 100d / LimitBytes!.Value) : 0;
    public int UsedPercentCapped => Math.Min(UsedPercent, 100);
    public bool IsOverLimit => HasLimit && TotalBytes > LimitBytes!.Value;
    public bool IsNearLimit => HasLimit && UsedPercent >= 80 && !IsOverLimit;

    /// <summary>Measured categories (attachments, compliance, logos) + signatures (not counted).</summary>
    public List<StorageCategoryDto> Categories { get; set; } = new();

    /// <summary>Document-attachment breakdown by record type, largest first.</summary>
    public List<StorageTypeDto> AttachmentsByType { get; set; } = new();

    /// <summary>Percentage of total per measured category (for the stacked bar); keyed by category name.</summary>
    public int PercentOf(long bytes) => TotalBytes <= 0 ? 0 : (int)Math.Round(bytes * 100d / TotalBytes);
}

/// <summary>One business row on the SuperAdmin platform Storage page.</summary>
public class AdminStorageRowDto
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = null!;
    public string PlanName { get; set; } = "No Plan";
    public string Status { get; set; } = "unknown";

    public long AttachmentBytes { get; set; }
    public long ComplianceBytes { get; set; }
    public long LogoBytes { get; set; }
    public long TotalBytes => AttachmentBytes + ComplianceBytes + LogoBytes;

    /// <summary>Plan storage cap in bytes; null = unlimited.</summary>
    public long? LimitBytes { get; set; }
    public bool HasLimit => LimitBytes.HasValue && LimitBytes.Value > 0;

    public string AttachmentDisplay => StorageFormat.Bytes(AttachmentBytes);
    public string ComplianceDisplay => StorageFormat.Bytes(ComplianceBytes);
    public string LogoDisplay => StorageFormat.Bytes(LogoBytes);
    public string TotalDisplay => StorageFormat.Bytes(TotalBytes);
    public string LimitDisplay => HasLimit ? StorageFormat.Bytes(LimitBytes!.Value) : "—";

    public int UsedPercent => HasLimit ? (int)Math.Round(TotalBytes * 100d / LimitBytes!.Value) : 0;
    public bool IsOverLimit => HasLimit && TotalBytes > LimitBytes!.Value;
    public bool IsNearLimit => HasLimit && UsedPercent >= 80 && !IsOverLimit;
}

/// <summary>Platform-wide summary KPIs for the SuperAdmin Storage page header.</summary>
public class AdminStorageSummaryDto
{
    public long TotalBytes { get; set; }
    public int BusinessesWithFiles { get; set; }
    public long LargestBusinessBytes { get; set; }
    public long AverageBytes { get; set; }

    public string TotalDisplay => StorageFormat.Bytes(TotalBytes);
    public string LargestDisplay => StorageFormat.Bytes(LargestBusinessBytes);
    public string AverageDisplay => StorageFormat.Bytes(AverageBytes);
}

/// <summary>Full model for the SuperAdmin Storage page (rows + summary + current sort).</summary>
public class AdminStoragePageDto
{
    public List<AdminStorageRowDto> Rows { get; set; } = new();
    public AdminStorageSummaryDto Summary { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? PlanFilter { get; set; }
    public string SortBy { get; set; } = "size";   // "size" | "name"
    public string SortDir { get; set; } = "desc";   // "asc" | "desc"
}
