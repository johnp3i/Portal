using Microsoft.AspNetCore.Http;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Compliance;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service interface for all compliance module business operations.
/// Covers template management, import, status workflow, attachments, and dashboard/calendar.
/// </summary>
public interface IComplianceService
{
    // Template Catalog (Admin)
    Task<List<ApplicationTypeDto>> GetAllTypesAsync();
    Task<ServiceResult> CreateTypeAsync(CreateApplicationTypeRequest request);
    Task<ServiceResult> UpdateTypeAsync(UpdateApplicationTypeRequest request);
    Task<ServiceResult> DeactivateTypeAsync(int typeId);
    Task<ServiceResult> ActivateTypeAsync(int typeId);

    // Category Management (Admin)
    Task<List<ApplicationCategoryDto>> GetCategoriesAsync();
    Task<ServiceResult> CreateCategoryAsync(CreateCategoryRequest request);
    Task<ServiceResult> UpdateCategoryAsync(UpdateCategoryRequest request);

    // Import
    Task<List<ApplicationTypeDto>> GetAvailableTemplatesAsync(string? country);
    Task<ServiceResult<int>> ImportTemplatesAsync(int businessId, ImportTemplatesRequest request);
    Task<bool> HasDuplicatesAsync(int businessId, int[] typeIds, int year);

    // Business Applications
    Task<PagedResult<BusinessApplicationDto>> GetApplicationsAsync(
        int businessId, string? category, string? status,
        DateTime? dateFrom, DateTime? dateTo, int page, int pageSize);
    Task<BusinessApplicationDetailDto?> GetApplicationDetailAsync(int id, int businessId);
    Task<ServiceResult> UpdateStatusAsync(int id, string newStatus, int businessId);
    Task<ServiceResult> UpdateDetailsAsync(int id, string? referenceNumber, string? notes, decimal? estimatedAmount, int businessId);
    Task<ServiceResult> CreateFilingAsync(int businessId, CreateFilingRequest request);

    // Attachments
    Task<ServiceResult<AttachmentResultDto>> UploadAttachmentAsync(
        int applicationId, int businessId, string userId, IFormFile file);
    Task<ServiceResult> DeleteAttachmentAsync(int attachmentId, int businessId);
    Task<FileDownloadResult?> DownloadAttachmentAsync(int attachmentId, int businessId);

    // Dashboard & Calendar
    Task<List<UpcomingFilingDto>> GetUpcomingFilingsAsync(int businessId, int days = 30, int maxItems = 5);
    Task<List<CalendarFilingDto>> GetCalendarDataAsync(int businessId, int year);
}
