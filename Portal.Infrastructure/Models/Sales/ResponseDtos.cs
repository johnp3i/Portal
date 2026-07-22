namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// A prepared response ready for review before sending.
/// </summary>
public class PreparedResponseDto
{
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public int LeadResponseTypeId { get; set; }
    public string ResponseTypeName { get; set; } = null!;
    public string? Subject { get; set; }
    public string RenderedBody { get; set; } = null!;
    public int ResponseTimeInHours { get; set; }
}

/// <summary>
/// Request model for sending a response to a lead.
/// </summary>
public class SendResponseRequest
{
    public int LeadRequestId { get; set; }
    public int LeadResponseTypeId { get; set; }
    public int? LeadResponseTemplateId { get; set; }
    public string? ResponseText { get; set; }
}

/// <summary>
/// Values to substitute in template placeholders.
/// </summary>
public class TemplatePlaceholderValues
{
    public string ContactName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string ResponseTime { get; set; } = string.Empty;
}
