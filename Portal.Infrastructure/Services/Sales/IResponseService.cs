using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for lead responses and templates.
/// </summary>
public interface IResponseService
{
    Task<PreparedResponseDto?> PrepareResponseAsync(int leadRequestId);
    Task<ServiceResult> SendResponseAsync(SendResponseRequest request, string userId);
    Task<ServiceResult> CreateTemplateAsync(CreateTemplateRequest request);
    Task<ServiceResult> UpdateTemplateAsync(UpdateTemplateRequest request);
    Task<ServiceResult> DeactivateTemplateAsync(int id);
    Task<ServiceResult> ActivateTemplateAsync(int id);
    Task<TemplateDetailDto?> GetTemplateByIdAsync(int id);
    Task<PagedResult<TemplateListDto>> GetTemplatesPagedAsync(int page, int pageSize);
    Task<List<LeadResponseHistoryDto>> GetResponsesForLeadAsync(int leadRequestId);
    Task<List<TemplateListDto>> GetActiveTemplatesAsync();
    Task<PreparedResponseDto?> RenderTemplateForLeadAsync(int templateId, int leadRequestId);
}
