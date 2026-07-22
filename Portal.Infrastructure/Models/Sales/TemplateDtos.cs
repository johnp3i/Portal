namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// DTO for displaying a response template in a list.
/// </summary>
public class TemplateListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? ProductName { get; set; }
    public string ResponseTypeName { get; set; } = null!;
    public int ResponseTimeInHours { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Detailed template view for editing.
/// </summary>
public class TemplateDetailDto
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public int LeadResponseTypeId { get; set; }
    public string Name { get; set; } = null!;
    public string? Subject { get; set; }
    public string BodyTemplate { get; set; } = null!;
    public int ResponseTimeInHours { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Request model for creating a response template.
/// </summary>
public class CreateTemplateRequest
{
    public int? ProductId { get; set; }
    public int LeadResponseTypeId { get; set; }
    public string Name { get; set; } = null!;
    public string? Subject { get; set; }
    public string BodyTemplate { get; set; } = null!;
    public int ResponseTimeInHours { get; set; }
}

/// <summary>
/// Request model for updating a response template.
/// </summary>
public class UpdateTemplateRequest
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public int LeadResponseTypeId { get; set; }
    public string Name { get; set; } = null!;
    public string? Subject { get; set; }
    public string BodyTemplate { get; set; } = null!;
    public int ResponseTimeInHours { get; set; }
}
