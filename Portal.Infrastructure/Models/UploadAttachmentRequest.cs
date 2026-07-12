using Microsoft.AspNetCore.Http;

namespace Portal.Infrastructure.Models;

/// <summary>
/// Request model for uploading a file attachment to a business entity.
/// </summary>
public class UploadAttachmentRequest
{
    public int BusinessId { get; set; }

    public string UserId { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public IFormFile File { get; set; } = null!;
}
