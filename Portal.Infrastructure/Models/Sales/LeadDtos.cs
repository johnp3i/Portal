namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// DTO for a lead card shown on the Kanban board.
/// </summary>
public class LeadCardDto
{
    public int Id { get; set; }
    public string ContactName { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? ProductName { get; set; }
    public string? AssignedToUserName { get; set; }
    public string? AssignedToUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int LeadStatusTypeId { get; set; }
}

/// <summary>
/// DTO for displaying a lead in the table view.
/// </summary>
public class LeadTableRowDto
{
    public int Id { get; set; }
    public string ContactName { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? ProductName { get; set; }
    public string StageName { get; set; } = null!;
    public string? StageColour { get; set; }
    public string SourceName { get; set; } = null!;
    public string? AssignedToUserName { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Filter parameters for pipeline view.
/// </summary>
public class LeadFilterDto
{
    public string? AssignedToUserId { get; set; }
    public int? ProductId { get; set; }
    public int? LeadStatusTypeId { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

/// <summary>
/// Request model for creating a new lead.
/// </summary>
public class CreateLeadRequestDto
{
    public int ContactId { get; set; }
    public int? ProductId { get; set; }
    public int LeadSourceTypeId { get; set; }
    public int? LeadSourceReferenceTypeId { get; set; }
    public string? SourceUrl { get; set; }
    public string? RequestText { get; set; }
}

/// <summary>
/// Detailed lead view for the LeadDetail page.
/// </summary>
public class LeadRequestDetailDto
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public string ContactName { get; set; } = null!;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? CompanyName { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string SourceName { get; set; } = null!;
    public string? SourceReferenceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? RequestText { get; set; }
    public int LeadStatusTypeId { get; set; }
    public string StageName { get; set; } = null!;
    public string? StageColour { get; set; }
    public bool IsTerminal { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancellationDescription { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<LeadResponseHistoryDto> Responses { get; set; } = new();
    public List<LeadMeetingDto> Meetings { get; set; } = new();
    public List<LinkedDocumentDto> LinkedQuotations { get; set; } = new();
    public List<LinkedDocumentDto> LinkedInvoices { get; set; } = new();
}

/// <summary>
/// Response entry in lead detail history.
/// </summary>
public class LeadResponseHistoryDto
{
    public int Id { get; set; }
    public string ResponseTypeName { get; set; } = null!;
    public string? ResponseText { get; set; }
    public string? RespondedByUserName { get; set; }
    public bool IsAutomated { get; set; }
    public DateTime SentAtUtc { get; set; }
}

/// <summary>
/// Meeting entry on the lead detail page.
/// </summary>
public class LeadMeetingDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = null!;
    public string MeetingTypeName { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsCancelled { get; set; }
}

/// <summary>
/// A linked quotation or invoice on the lead detail page.
/// </summary>
public class LinkedDocumentDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = null!;
    public string? Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Pipeline data grouped by stage for Kanban rendering.
/// </summary>
public class PipelineStageGroupDto
{
    public int LeadStatusTypeId { get; set; }
    public string StageName { get; set; } = null!;
    public string? Colour { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsTerminal { get; set; }
    public int Count { get; set; }
    public List<LeadCardDto> Leads { get; set; } = new();
}
