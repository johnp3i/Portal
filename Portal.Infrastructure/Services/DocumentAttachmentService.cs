using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Helpers;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for document attachment management. Orchestrates file storage,
/// metadata persistence, validation, and authorization.
/// </summary>
public class DocumentAttachmentService : IDocumentAttachmentService
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxAttachmentsPerEntity = 5;

    private static readonly string[] ValidEntityTypes =
    {
        "Invoice", "CreditNote", "Quotation", "Payment", "Purchase", "Supplier", "Customer", "RevenueSummary"
    };

    private readonly DocumentAttachmentRepository _repository;
    private readonly IFileStorageService _fileStorageService;
    private readonly UserNameResolver _userNameResolver;

    public DocumentAttachmentService(
        DocumentAttachmentRepository repository,
        IFileStorageService fileStorageService,
        UserNameResolver userNameResolver)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
        _userNameResolver = userNameResolver;
    }

    public async Task<ServiceResult<AttachmentDto>> UploadAsync(UploadAttachmentRequest request)
    {
        try
        {
            // Validate entity type
            if (!ValidEntityTypes.Contains(request.EntityType, StringComparer.OrdinalIgnoreCase))
            {
                return ServiceResult<AttachmentDto>.Fail("Invalid entity type.");
            }

            var file = request.File;

            // Validate file size
            if (file.Length > MaxFileSizeBytes)
            {
                return ServiceResult<AttachmentDto>.Fail("File size exceeds the maximum of 5 MB.");
            }

            // Validate file type (extension + Content-Type + magic bytes)
            using var stream = file.OpenReadStream();
            var validationResult = FileTypeValidator.Validate(file.FileName, file.ContentType, stream);
            if (!validationResult.IsValid)
            {
                return ServiceResult<AttachmentDto>.Fail(validationResult.ErrorMessage!);
            }

            // Check attachment count limit (1 per Z-Report, 5 for other entities)
            var maxAllowed = request.EntityType.Equals("RevenueSummary", StringComparison.OrdinalIgnoreCase) ? 1 : MaxAttachmentsPerEntity;
            var currentCount = await _repository.GetCountAsync(request.BusinessId, request.EntityType, request.EntityId);
            if (currentCount >= maxAllowed)
            {
                var message = maxAllowed == 1
                    ? "A Z-Report can only have one attached file. Delete the existing file to upload a new one."
                    : $"Maximum of {MaxAttachmentsPerEntity} attachments per record reached.";
                return ServiceResult<AttachmentDto>.Fail(message);
            }

            // Upload file to storage
            stream.Position = 0;
            var storagePath = await _fileStorageService.UploadAsync(
                request.BusinessId, request.EntityType, request.EntityId, file.FileName, stream);

            // Determine the stored file name from the path
            var fileName = Path.GetFileName(storagePath);

            // Create metadata record
            var attachment = new DocumentAttachment
            {
                BusinessId = request.BusinessId,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                FileName = fileName,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                StoragePath = storagePath,
                FileSizeBytes = file.Length,
                UploadedByUserId = request.UserId,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            var newId = await _repository.InsertAsync(attachment);

            // Resolve display name for response
            var names = await _userNameResolver.ResolveNamesAsync(new[] { request.UserId });
            var displayName = _userNameResolver.GetDisplayName(names, request.UserId);

            var dto = new AttachmentDto
            {
                Id = newId,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                CreatedAtUtc = attachment.CreatedAtUtc,
                UploadedByDisplayName = displayName,
                IsOwnedByCurrentUser = true
            };

            return ServiceResult<AttachmentDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult<FileDownloadResult>> DownloadAsync(int attachmentId, int businessId)
    {
        try
        {
            var attachment = await _repository.GetByIdAsync(attachmentId, businessId);
            if (attachment == null)
            {
                return ServiceResult<FileDownloadResult>.Fail("Attachment not found.");
            }

            var fileExists = await _fileStorageService.ExistsAsync(attachment.StoragePath);
            if (!fileExists)
            {
                return ServiceResult<FileDownloadResult>.Fail("The file is unavailable. Please contact support.");
            }

            var stream = await _fileStorageService.DownloadAsync(attachment.StoragePath);

            var result = new FileDownloadResult
            {
                FileStream = stream,
                ContentType = attachment.ContentType,
                OriginalFileName = attachment.OriginalFileName
            };

            return ServiceResult<FileDownloadResult>.Ok(result);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeleteAsync(int attachmentId, string userId, int businessId, bool isOwner)
    {
        try
        {
            var attachment = await _repository.GetByIdAsync(attachmentId, businessId);
            if (attachment == null)
            {
                return ServiceResult.Fail("Attachment not found.");
            }

            // Authorization: uploader can delete their own, owner can delete any
            var isUploader = string.Equals(attachment.UploadedByUserId, userId, StringComparison.OrdinalIgnoreCase);
            if (!isUploader && !isOwner)
            {
                return ServiceResult.Fail("You do not have permission to delete this attachment.");
            }

            await _repository.SoftDeleteAsync(attachmentId, businessId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<AttachmentDto>> GetByEntityAsync(int businessId, string entityType, int entityId, string? currentUserId = null)
    {
        try
        {
            var attachments = await _repository.GetByEntityAsync(businessId, entityType, entityId);

            if (attachments.Count == 0)
                return new List<AttachmentDto>();

            // Resolve display names for all uploaders
            var userIds = attachments.Select(a => a.UploadedByUserId).Distinct();
            var names = await _userNameResolver.ResolveNamesAsync(userIds);

            return attachments.Select(a => new AttachmentDto
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                CreatedAtUtc = a.CreatedAtUtc,
                UploadedByDisplayName = _userNameResolver.GetDisplayName(names, a.UploadedByUserId),
                IsOwnedByCurrentUser = !string.IsNullOrEmpty(currentUserId) &&
                    string.Equals(a.UploadedByUserId, currentUserId, StringComparison.OrdinalIgnoreCase)
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<int> GetCountAsync(int businessId, string entityType, int entityId)
    {
        try
        {
            return await _repository.GetCountAsync(businessId, entityType, entityId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<Dictionary<int, int>> GetCountsForEntitiesAsync(int businessId, string entityType, int[] entityIds)
    {
        try
        {
            return await _repository.GetCountsForEntitiesAsync(businessId, entityType, entityIds);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<AttachmentIndexDto>> GetAllPagedAsync(
        int businessId, string? entityType, string? contentTypeFilter,
        string? uploadedByUserId, DateTime? dateFrom, DateTime? dateTo,
        int page, int pageSize, string? currentUserId = null)
    {
        try
        {
            var items = await _repository.GetAllPagedAsync(businessId, entityType, contentTypeFilter,
                uploadedByUserId, dateFrom, dateTo, page, pageSize);
            var totalCount = await _repository.GetAllCountAsync(businessId, entityType, contentTypeFilter,
                uploadedByUserId, dateFrom, dateTo);

            if (items.Count == 0)
            {
                return new PagedResult<AttachmentIndexDto>
                {
                    Items = new List<AttachmentIndexDto>(),
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }

            // Resolve display names
            var userIds = items.Select(a => a.UploadedByUserId).Distinct();
            var names = await _userNameResolver.ResolveNamesAsync(userIds);

            var dtos = items.Select(a => new AttachmentIndexDto
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                EntityReference = $"{a.EntityType} #{a.EntityId}",
                CreatedAtUtc = a.CreatedAtUtc,
                UploadedByDisplayName = _userNameResolver.GetDisplayName(names, a.UploadedByUserId),
                IsOwnedByCurrentUser = !string.IsNullOrEmpty(currentUserId) &&
                    string.Equals(a.UploadedByUserId, currentUserId, StringComparison.OrdinalIgnoreCase)
            }).ToList();

            return new PagedResult<AttachmentIndexDto>
            {
                Items = dtos,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<AttachmentIndexSummary> GetSummaryAsync(int businessId)
    {
        try
        {
            var (totalFiles, totalSizeBytes, entitiesWithFiles, thisMonthCount) =
                await _repository.GetSummaryAsync(businessId);

            return new AttachmentIndexSummary
            {
                TotalFiles = totalFiles,
                TotalSizeBytes = totalSizeBytes,
                EntitiesWithFiles = entitiesWithFiles,
                ThisMonthCount = thisMonthCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
