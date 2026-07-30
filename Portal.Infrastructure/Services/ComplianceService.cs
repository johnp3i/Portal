using Microsoft.AspNetCore.Http;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Compliance;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service implementation for all compliance module business operations.
/// </summary>
public class ComplianceService : IComplianceService
{
    private readonly ComplianceRepository _repository;
    private readonly IFileStorageService _fileStorageService;

    private static readonly Dictionary<string, string[]> ValidTransitions = new()
    {
        ["Pending"] = new[] { "InProgress", "Submitted" },
        ["InProgress"] = new[] { "Submitted" },
        ["Submitted"] = new[] { "Approved", "Rejected" },
        ["Rejected"] = new[] { "InProgress" },
        ["Approved"] = Array.Empty<string>()
    };

    public ComplianceService(ComplianceRepository repository, IFileStorageService fileStorageService)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
    }

    #region Category Management

    public async Task<List<ApplicationCategoryDto>> GetCategoriesAsync()
    {
        try
        {
            var categories = await _repository.GetAllCategoriesAsync();

            return categories.Select(c => new ApplicationCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateCategoryAsync(CreateCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Category name is required.");

            var entity = new ApplicationCategory
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim()
            };

            var id = await _repository.InsertCategoryAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateCategoryAsync(UpdateCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Category name is required.");

            var entity = new ApplicationCategory
            {
                Id = request.Id,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim()
            };

            await _repository.UpdateCategoryAsync(entity);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Type Management

    public async Task<List<ApplicationTypeDto>> GetAllTypesAsync()
    {
        try
        {
            var types = await _repository.GetAllTypesAsync();
            var categories = await _repository.GetAllCategoriesAsync();
            var categoryLookup = categories.ToDictionary(c => c.Id, c => c.Name);

            return types.Select(t => new ApplicationTypeDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Country = t.Country,
                ApplicationCategoryId = t.ApplicationCategoryId,
                CategoryName = categoryLookup.GetValueOrDefault(t.ApplicationCategoryId, string.Empty),
                Frequency = t.Frequency,
                DefaultDueMonth = t.DefaultDueMonth,
                DefaultDueDay = t.DefaultDueDay,
                EstimatedAmount = t.EstimatedAmount,
                FrequencyInterval = t.FrequencyInterval,
                IsActive = t.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateTypeAsync(CreateApplicationTypeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Application type name is required.");

            if (string.IsNullOrWhiteSpace(request.Country))
                return ServiceResult.Fail("Country is required.");

            var exists = await _repository.TypeExistsAsync(request.Name.Trim(), request.Country.Trim(), null);
            if (exists)
                return ServiceResult.Fail("An application type with this name and country already exists.");

            var entity = new ApplicationType
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Country = request.Country.Trim(),
                ApplicationCategoryId = request.ApplicationCategoryId,
                Frequency = request.Frequency,
                DefaultDueMonth = request.DefaultDueMonth,
                DefaultDueDay = request.DefaultDueDay,
                EstimatedAmount = request.EstimatedAmount,
                FrequencyInterval = request.FrequencyInterval
            };

            var id = await _repository.InsertTypeAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateTypeAsync(UpdateApplicationTypeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Application type name is required.");

            if (string.IsNullOrWhiteSpace(request.Country))
                return ServiceResult.Fail("Country is required.");

            var exists = await _repository.TypeExistsAsync(request.Name.Trim(), request.Country.Trim(), request.Id);
            if (exists)
                return ServiceResult.Fail("An application type with this name and country already exists.");

            var entity = new ApplicationType
            {
                Id = request.Id,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Country = request.Country.Trim(),
                ApplicationCategoryId = request.ApplicationCategoryId,
                Frequency = request.Frequency,
                DefaultDueMonth = request.DefaultDueMonth,
                DefaultDueDay = request.DefaultDueDay,
                EstimatedAmount = request.EstimatedAmount,
                FrequencyInterval = request.FrequencyInterval
            };

            await _repository.UpdateTypeAsync(entity);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateTypeAsync(int typeId)
    {
        try
        {
            await _repository.DeactivateTypeAsync(typeId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ActivateTypeAsync(int typeId)
    {
        try
        {
            await _repository.ActivateTypeAsync(typeId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Import

    public async Task<List<ApplicationTypeDto>> GetAvailableTemplatesAsync(string? country)
    {
        try
        {
            var types = await _repository.GetAllTypesAsync();
            var categories = await _repository.GetAllCategoriesAsync();
            var categoryLookup = categories.ToDictionary(c => c.Id, c => c.Name);

            var filtered = types.Where(t => t.IsActive).AsEnumerable();
            if (!string.IsNullOrWhiteSpace(country))
                filtered = filtered.Where(t => t.Country.Equals(country.Trim(), StringComparison.OrdinalIgnoreCase));

            return filtered.Select(t => new ApplicationTypeDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Country = t.Country,
                ApplicationCategoryId = t.ApplicationCategoryId,
                CategoryName = categoryLookup.GetValueOrDefault(t.ApplicationCategoryId, string.Empty),
                Frequency = t.Frequency,
                DefaultDueMonth = t.DefaultDueMonth,
                DefaultDueDay = t.DefaultDueDay,
                EstimatedAmount = t.EstimatedAmount,
                FrequencyInterval = t.FrequencyInterval,
                IsActive = t.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<bool> HasDuplicatesAsync(int businessId, int[] typeIds, int year)
    {
        try
        {
            foreach (var typeId in typeIds)
            {
                var exists = await _repository.ExistsForTypeAndPeriodAsync(businessId, typeId, year);
                if (exists)
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult<int>> ImportTemplatesAsync(int businessId, ImportTemplatesRequest request)
    {
        try
        {
            if (request.TemplateIds == null || request.TemplateIds.Length == 0)
                return ServiceResult<int>.Fail("No templates selected for import.");

            var types = await _repository.GetApplicationTypesByIdsAsync(request.TemplateIds);
            if (types.Count == 0)
                return ServiceResult<int>.Fail("No valid templates found.");

            var applications = new List<BusinessApplication>();

            foreach (var type in types)
            {
                List<DateTime> dueDates;

                if (request.OneOffDueDate.HasValue)
                {
                    dueDates = new List<DateTime> { request.OneOffDueDate.Value };
                }
                else
                {
                    var overriddenDueDay = request.DueDayOverrides != null && request.DueDayOverrides.TryGetValue(type.Id, out var customDay)
                        ? customDay
                        : type.DefaultDueDay;
                    dueDates = CalculateDueDates(type.Frequency, request.Year, type.DefaultDueMonth, overriddenDueDay);
                }

                foreach (var dueDate in dueDates)
                {
                    applications.Add(new BusinessApplication
                    {
                        BusinessId = businessId,
                        ApplicationTypeId = type.Id,
                        DueDate = dueDate,
                        Status = "Pending",
                        EstimatedAmount = type.EstimatedAmount
                    });
                }
            }

            if (applications.Count > 0)
                await _repository.InsertBatchAsync(applications);

            return ServiceResult<int>.Ok(applications.Count);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Business Applications

    public async Task<PagedResult<BusinessApplicationDto>> GetApplicationsAsync(
        int businessId, string? category, string? status,
        DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)
    {
        try
        {
            var (items, totalCount) = await _repository.GetPagedAsync(
                businessId, category, status, dateFrom, dateTo, page, pageSize);

            var typeIds = items.Select(a => a.ApplicationTypeId).Distinct().ToArray();
            var types = await _repository.GetApplicationTypesByIdsAsync(typeIds);
            var typeLookup = types.ToDictionary(t => t.Id);

            var categories = await _repository.GetAllCategoriesAsync();
            var categoryLookup = categories.ToDictionary(c => c.Id, c => c.Name);

            var dtos = items.Select(a =>
            {
                var type = typeLookup.GetValueOrDefault(a.ApplicationTypeId);
                var (dueStatus, daysUntilDue) = CalculateDueStatus(a.DueDate, a.Status);

                return new BusinessApplicationDto
                {
                    Id = a.Id,
                    ApplicationName = type?.Name ?? string.Empty,
                    CategoryName = type != null ? categoryLookup.GetValueOrDefault(type.ApplicationCategoryId, string.Empty) : string.Empty,
                    DueDate = a.DueDate,
                    Status = a.Status,
                    ReferenceNumber = a.ReferenceNumber,
                    EstimatedAmount = a.EstimatedAmount,
                    AttachmentCount = 0,
                    DueStatus = dueStatus,
                    DaysUntilDue = daysUntilDue
                };
            }).ToList();

            return new PagedResult<BusinessApplicationDto>
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

    public async Task<BusinessApplicationDetailDto?> GetApplicationDetailAsync(int id, int businessId)
    {
        try
        {
            var record = await _repository.GetByIdAsync(id, businessId);
            if (record == null)
                return null;

            var type = await _repository.GetApplicationTypeByIdAsync(record.ApplicationTypeId);
            var categories = await _repository.GetAllCategoriesAsync();
            var categoryLookup = categories.ToDictionary(c => c.Id, c => c.Name);

            var attachments = await _repository.GetAttachmentsForApplicationAsync(id);
            var (dueStatus, daysUntilDue) = CalculateDueStatus(record.DueDate, record.Status);

            var allowedTransitions = ValidTransitions.ContainsKey(record.Status)
                ? ValidTransitions[record.Status]
                : Array.Empty<string>();

            return new BusinessApplicationDetailDto
            {
                Id = record.Id,
                ApplicationName = type?.Name ?? string.Empty,
                CategoryName = type != null ? categoryLookup.GetValueOrDefault(type.ApplicationCategoryId, string.Empty) : string.Empty,
                Frequency = type?.Frequency ?? string.Empty,
                DueDate = record.DueDate,
                Status = record.Status,
                ReferenceNumber = record.ReferenceNumber,
                Notes = record.Notes,
                EstimatedAmount = record.EstimatedAmount,
                SubmittedAtUtc = record.SubmittedAtUtc,
                ApprovedAtUtc = record.ApprovedAtUtc,
                CreatedAtUtc = record.CreatedAtUtc,
                DueStatus = dueStatus,
                DaysUntilDue = daysUntilDue,
                AllowedTransitions = allowedTransitions,
                Attachments = attachments.Select(a => new ApplicationAttachmentDto
                {
                    Id = a.Id,
                    OriginalFileName = a.OriginalFileName,
                    FileSizeBytes = a.FileSizeBytes,
                    CreatedAtUtc = a.CreatedAtUtc
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateStatusAsync(int id, string newStatus, int businessId)
    {
        try
        {
            var record = await _repository.GetByIdAsync(id, businessId);
            if (record == null)
                return ServiceResult.Fail("Application not found.");

            if (!ValidTransitions.ContainsKey(record.Status))
                return ServiceResult.Fail($"Current status '{record.Status}' does not support transitions.");

            var allowed = ValidTransitions[record.Status];
            if (!allowed.Contains(newStatus))
                return ServiceResult.Fail($"Transition from '{record.Status}' to '{newStatus}' is not allowed.");

            DateTime? submittedAtUtc = record.SubmittedAtUtc;
            DateTime? approvedAtUtc = record.ApprovedAtUtc;

            if (newStatus == "Submitted")
                submittedAtUtc = DateTime.UtcNow;

            if (newStatus == "Approved")
                approvedAtUtc = DateTime.UtcNow;

            await _repository.UpdateStatusAsync(id, newStatus, submittedAtUtc, approvedAtUtc);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateDetailsAsync(int id, string? referenceNumber, string? notes, decimal? estimatedAmount, int businessId)
    {
        try
        {
            var record = await _repository.GetByIdAsync(id, businessId);
            if (record == null)
                return ServiceResult.Fail("Application not found.");

            await _repository.UpdateDetailsAsync(id, referenceNumber?.Trim(), notes?.Trim(), estimatedAmount);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateFilingAsync(int businessId, CreateFilingRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Filing name is required.");

            if (request.DueDate == default)
                return ServiceResult.Fail("Due date is required.");

            // Create a custom ApplicationType (IsActive=false so it won't appear in template import)
            var customType = new ApplicationType
            {
                Name = request.Name.Trim(),
                Description = "Custom filing",
                Country = "Custom",
                ApplicationCategoryId = request.ApplicationCategoryId,
                Frequency = "One-off",
                IsActive = false
            };

            var typeId = await _repository.InsertTypeAsync(customType);

            var application = new BusinessApplication
            {
                BusinessId = businessId,
                ApplicationTypeId = typeId,
                DueDate = request.DueDate,
                Status = "Pending",
                Notes = request.Notes?.Trim(),
                EstimatedAmount = request.EstimatedAmount
            };

            await _repository.InsertSingleAsync(application);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Attachments

    public async Task<ServiceResult<AttachmentResultDto>> UploadAttachmentAsync(
        int applicationId, int businessId, string userId, IFormFile file)
    {
        try
        {
            var application = await _repository.GetByIdAsync(applicationId, businessId);
            if (application == null)
                return ServiceResult<AttachmentResultDto>.Fail("Application not found.");

            if (file.ContentType != "application/pdf")
                return ServiceResult<AttachmentResultDto>.Fail("Only PDF files are accepted.");

            if (file.Length > 5 * 1024 * 1024)
                return ServiceResult<AttachmentResultDto>.Fail("File size must not exceed 5 MB.");

            var attachmentCount = await _repository.GetAttachmentCountAsync(applicationId);
            if (attachmentCount >= 3)
                return ServiceResult<AttachmentResultDto>.Fail("Maximum of 3 attachments per application.");

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";

            var storagePath = await _fileStorageService.UploadAsync(
                businessId, "compliance", applicationId, file.FileName, file.OpenReadStream());

            var entity = new ApplicationAttachment
            {
                BusinessApplicationId = applicationId,
                FileName = uniqueFileName,
                OriginalFileName = file.FileName,
                FilePath = storagePath,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedByUserId = userId
            };

            var id = await _repository.InsertAttachmentAsync(entity);

            var result = new AttachmentResultDto
            {
                Id = id,
                OriginalFileName = file.FileName
            };

            return ServiceResult<AttachmentResultDto>.Ok(result);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeleteAttachmentAsync(int attachmentId, int businessId)
    {
        try
        {
            var attachment = await _repository.GetAttachmentByIdAsync(attachmentId, businessId);
            if (attachment == null)
                return ServiceResult.Fail("Attachment not found.");

            await _fileStorageService.DeleteAsync(attachment.FilePath);
            await _repository.DeleteAttachmentAsync(attachmentId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<FileDownloadResult?> DownloadAttachmentAsync(int attachmentId, int businessId)
    {
        try
        {
            var attachment = await _repository.GetAttachmentByIdAsync(attachmentId, businessId);
            if (attachment == null)
                return null;

            var stream = await _fileStorageService.DownloadAsync(attachment.FilePath);

            return new FileDownloadResult
            {
                FileStream = stream,
                ContentType = attachment.ContentType,
                OriginalFileName = attachment.OriginalFileName
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Dashboard & Calendar

    public async Task<List<UpcomingFilingDto>> GetUpcomingFilingsAsync(int businessId, int days = 30, int maxItems = 5)
    {
        try
        {
            var items = await _repository.GetUpcomingAsync(businessId, days, maxItems);

            var typeIds = items.Select(a => a.ApplicationTypeId).Distinct().ToArray();
            var types = await _repository.GetApplicationTypesByIdsAsync(typeIds);
            var typeLookup = types.ToDictionary(t => t.Id, t => t.Name);

            return items.Select(a =>
            {
                var (dueStatus, daysUntilDue) = CalculateDueStatus(a.DueDate, a.Status);

                return new UpcomingFilingDto
                {
                    Id = a.Id,
                    ApplicationName = typeLookup.GetValueOrDefault(a.ApplicationTypeId, string.Empty),
                    DueDate = a.DueDate,
                    Status = a.Status,
                    DueStatus = dueStatus,
                    DaysUntilDue = daysUntilDue,
                    EstimatedAmount = a.EstimatedAmount
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<CalendarFilingDto>> GetCalendarDataAsync(int businessId, int year)
    {
        try
        {
            var items = await _repository.GetCalendarAsync(businessId, year);

            var typeIds = items.Select(a => a.ApplicationTypeId).Distinct().ToArray();
            var types = await _repository.GetApplicationTypesByIdsAsync(typeIds);
            var typeLookup = types.ToDictionary(t => t.Id, t => t.Name);

            return items.Select(a =>
            {
                var (dueStatus, _) = CalculateDueStatus(a.DueDate, a.Status);

                return new CalendarFilingDto
                {
                    Id = a.Id,
                    ApplicationName = typeLookup.GetValueOrDefault(a.ApplicationTypeId, string.Empty),
                    DueDate = a.DueDate,
                    Status = a.Status,
                    DueStatus = dueStatus,
                    EstimatedAmount = a.EstimatedAmount
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Private Helpers

    private static (string DueStatus, int? DaysUntilDue) CalculateDueStatus(DateTime dueDate, string status)
    {
        if (status == "Submitted" || status == "Approved" || status == "Rejected")
            return ("normal", null);

        var today = DateTime.UtcNow.Date;
        var daysUntil = (dueDate.Date - today).Days;

        return daysUntil switch
        {
            < 0 => ("overdue", daysUntil),
            <= 3 => ("urgent", daysUntil),
            <= 7 => ("warning", daysUntil),
            _ => ("normal", daysUntil)
        };
    }

    private List<DateTime> CalculateDueDates(string frequency, int year, int? defaultDueMonth, int? defaultDueDay)
    {
        var dueDay = defaultDueDay ?? 1;

        return frequency switch
        {
            "Monthly" => Enumerable.Range(1, 12)
                .Select(m => new DateTime(year, m, Math.Min(dueDay, DateTime.DaysInMonth(year, m))))
                .ToList(),
            "Quarterly" => new[] { 1, 4, 7, 10 }
                .Select(m => new DateTime(year, m, Math.Min(dueDay, DateTime.DaysInMonth(year, m))))
                .ToList(),
            "Annual" => new List<DateTime>
            {
                new DateTime(year, defaultDueMonth ?? 1, Math.Min(dueDay, DateTime.DaysInMonth(year, defaultDueMonth ?? 1)))
            },
            "Multi-Year" => new List<DateTime>
            {
                new DateTime(year, defaultDueMonth ?? 1, Math.Min(dueDay, DateTime.DaysInMonth(year, defaultDueMonth ?? 1)))
            },
            _ => new List<DateTime>()
        };
    }

    #endregion
}
