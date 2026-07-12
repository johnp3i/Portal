using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for document attachment management including upload, download, delete, and listing.
/// </summary>
public interface IDocumentAttachmentService
{
    Task<ServiceResult<AttachmentDto>> UploadAsync(UploadAttachmentRequest request);
    Task<ServiceResult<FileDownloadResult>> DownloadAsync(int attachmentId, int businessId);
    Task<ServiceResult> DeleteAsync(int attachmentId, string userId, int businessId, bool isOwner);
    Task<List<AttachmentDto>> GetByEntityAsync(int businessId, string entityType, int entityId, string? currentUserId = null);
    Task<int> GetCountAsync(int businessId, string entityType, int entityId);
    Task<Dictionary<int, int>> GetCountsForEntitiesAsync(int businessId, string entityType, int[] entityIds);
    Task<PagedResult<AttachmentIndexDto>> GetAllPagedAsync(int businessId, string? entityType, string? contentTypeFilter, string? uploadedByUserId, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize, string? currentUserId = null);
    Task<AttachmentIndexSummary> GetSummaryAsync(int businessId);
}

/// <summary>
/// Result model for file download operations containing the stream and metadata.
/// </summary>
public class FileDownloadResult
{
    public Stream FileStream { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
}
