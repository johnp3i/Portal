namespace Portal.Infrastructure.Models;

/// <summary>
/// Business-friendly filter parameters for the Activity Log.
/// Maps to underlying AuditLogFilter via the controller.
/// </summary>
public class ActivityFilterDto
{
    public string? WhatChanged { get; set; } // Maps to TableName group
    public string? WhoChanged { get; set; } // UserId or "system"
    public string? ChangeType { get; set; } // "Created", "Edited", "Deleted", "StatusChanged"
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 8;
}
