namespace Portal.Web.Models;

/// <summary>
/// Request model for creating a new External Platform.
/// </summary>
public class CreateExternalPlatformRequest
{
    public string Name { get; set; } = null!;
    public string PlatformCode { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>
/// Request model for updating an existing External Platform.
/// </summary>
public class UpdateExternalPlatformRequest : CreateExternalPlatformRequest
{
    public int Id { get; set; }
}
