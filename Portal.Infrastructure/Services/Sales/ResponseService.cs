using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for lead responses and templates.
/// </summary>
public class ResponseService : IResponseService
{
    private readonly LeadResponseRepository _responseRepository;
    private readonly LeadResponseTemplateRepository _templateRepository;
    private readonly LeadRequestRepository _leadRequestRepository;
    private readonly SalesContactRepository _contactRepository;
    private readonly SalesProductRepository _productRepository;
    private readonly LeadResponseTypeRepository _responseTypeRepository;
    private readonly ILeadRequestService _leadRequestService;
    private readonly ICurrentTenantService _tenantService;

    public ResponseService(
        LeadResponseRepository responseRepository,
        LeadResponseTemplateRepository templateRepository,
        LeadRequestRepository leadRequestRepository,
        SalesContactRepository contactRepository,
        SalesProductRepository productRepository,
        LeadResponseTypeRepository responseTypeRepository,
        ILeadRequestService leadRequestService,
        ICurrentTenantService tenantService)
    {
        _responseRepository = responseRepository;
        _templateRepository = templateRepository;
        _leadRequestRepository = leadRequestRepository;
        _contactRepository = contactRepository;
        _productRepository = productRepository;
        _responseTypeRepository = responseTypeRepository;
        _leadRequestService = leadRequestService;
        _tenantService = tenantService;
    }

    public async Task<PreparedResponseDto?> PrepareResponseAsync(int leadRequestId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(leadRequestId, businessId);
            if (lead == null) return null;

            var template = await _templateRepository.FindMatchingTemplateAsync(lead.ProductId, businessId);
            if (template == null)
            {
                // No template — return empty prepared response
                return new PreparedResponseDto
                {
                    LeadResponseTypeId = 1, // Email default
                    ResponseTypeName = "Email",
                    RenderedBody = string.Empty,
                    ResponseTimeInHours = 24
                };
            }

            // Build placeholder values
            var contact = await _contactRepository.GetByIdAsync(lead.ContactId, businessId);
            var product = lead.ProductId.HasValue
                ? await _productRepository.GetByIdAsync(lead.ProductId.Value, businessId)
                : null;

            var placeholders = new TemplatePlaceholderValues
            {
                ContactName = contact != null
                    ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                    : string.Empty,
                ProductName = product?.Name ?? string.Empty,
                BusinessName = string.Empty, // Would need BusinessProfile — simplified for now
                ResponseTime = $"{template.ResponseTimeInHours} hours"
            };

            var renderedBody = RenderTemplate(template.BodyTemplate, placeholders);

            var responseTypes = await _responseTypeRepository.GetAllAsync();
            var responseType = responseTypes.FirstOrDefault(rt => rt.Id == template.LeadResponseTypeId);

            return new PreparedResponseDto
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                LeadResponseTypeId = template.LeadResponseTypeId,
                ResponseTypeName = responseType?.Name ?? "Unknown",
                Subject = template.Subject,
                RenderedBody = renderedBody,
                ResponseTimeInHours = template.ResponseTimeInHours
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SendResponseAsync(SendResponseRequest request, string userId)
    {
        try
        {
            var entity = new LeadResponse
            {
                LeadRequestId = request.LeadRequestId,
                LeadResponseTypeId = request.LeadResponseTypeId,
                LeadResponseTemplateId = request.LeadResponseTemplateId,
                RespondedByUserId = userId,
                ResponseText = request.ResponseText,
                IsAutomated = false,
                SentAtUtc = DateTime.UtcNow
            };

            var id = await _responseRepository.InsertAsync(entity);

            // Suggest stage transition
            await _leadRequestService.SuggestStageTransitionAsync(request.LeadRequestId, "response_sent");

            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateTemplateAsync(CreateTemplateRequest request)
    {
        try
        {
            var entity = new LeadResponseTemplate
            {
                BusinessId = _tenantService.CurrentBusinessId,
                ProductId = request.ProductId,
                LeadResponseTypeId = request.LeadResponseTypeId,
                Name = request.Name,
                Subject = request.Subject,
                BodyTemplate = request.BodyTemplate,
                ResponseTimeInHours = request.ResponseTimeInHours,
                IsActive = true
            };

            var id = await _templateRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateTemplateAsync(UpdateTemplateRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var existing = await _templateRepository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Template not found.");

            existing.ProductId = request.ProductId;
            existing.LeadResponseTypeId = request.LeadResponseTypeId;
            existing.Name = request.Name;
            existing.Subject = request.Subject;
            existing.BodyTemplate = request.BodyTemplate;
            existing.ResponseTimeInHours = request.ResponseTimeInHours;

            await _templateRepository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateTemplateAsync(int id)
    {
        try
        {
            await _templateRepository.DeactivateAsync(id, _tenantService.CurrentBusinessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ActivateTemplateAsync(int id)
    {
        try
        {
            await _templateRepository.ActivateAsync(id, _tenantService.CurrentBusinessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<TemplateDetailDto?> GetTemplateByIdAsync(int id)
    {
        try
        {
            var template = await _templateRepository.GetByIdAsync(id, _tenantService.CurrentBusinessId);
            if (template == null) return null;

            return new TemplateDetailDto
            {
                Id = template.Id,
                ProductId = template.ProductId,
                LeadResponseTypeId = template.LeadResponseTypeId,
                Name = template.Name,
                Subject = template.Subject,
                BodyTemplate = template.BodyTemplate,
                ResponseTimeInHours = template.ResponseTimeInHours,
                IsActive = template.IsActive
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<TemplateListDto>> GetTemplatesPagedAsync(int page, int pageSize)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var paged = await _templateRepository.GetPagedAsync(page, pageSize, businessId);

            var responseTypes = await _responseTypeRepository.GetAllAsync();
            var products = await _productRepository.GetAllActiveAsync(businessId);

            var items = paged.Items.Select(t => new TemplateListDto
            {
                Id = t.Id,
                Name = t.Name,
                ProductName = t.ProductId.HasValue
                    ? products.FirstOrDefault(p => p.Id == t.ProductId.Value)?.Name
                    : null,
                ResponseTypeName = responseTypes.FirstOrDefault(rt => rt.Id == t.LeadResponseTypeId)?.Name ?? "Unknown",
                ResponseTimeInHours = t.ResponseTimeInHours,
                IsActive = t.IsActive,
                CreatedAtUtc = t.CreatedAtUtc
            }).ToList();

            return new PagedResult<TemplateListDto>
            {
                Items = items,
                CurrentPage = paged.CurrentPage,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<LeadResponseHistoryDto>> GetResponsesForLeadAsync(int leadRequestId)
    {
        try
        {
            var responses = await _responseRepository.GetByLeadRequestIdAsync(leadRequestId);
            var responseTypes = await _responseTypeRepository.GetAllAsync();

            return responses.Select(r => new LeadResponseHistoryDto
            {
                Id = r.Id,
                ResponseTypeName = responseTypes.FirstOrDefault(rt => rt.Id == r.LeadResponseTypeId)?.Name ?? "Unknown",
                ResponseText = r.ResponseText,
                IsAutomated = r.IsAutomated,
                SentAtUtc = r.SentAtUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Renders a template body by replacing all 4 placeholders with values or empty string.
    /// Placeholders: {{ContactName}}, {{ProductName}}, {{BusinessName}}, {{ResponseTime}}
    /// </summary>
    private static string RenderTemplate(string bodyTemplate, TemplatePlaceholderValues values)
    {
        return bodyTemplate
            .Replace("{{ContactName}}", values.ContactName)
            .Replace("{{ProductName}}", values.ProductName)
            .Replace("{{BusinessName}}", values.BusinessName)
            .Replace("{{ResponseTime}}", values.ResponseTime);
    }

    public async Task<List<TemplateListDto>> GetActiveTemplatesAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var templates = await _templateRepository.GetAllActiveAsync(businessId);
            var responseTypes = await _responseTypeRepository.GetAllAsync();
            var products = await _productRepository.GetAllActiveAsync(businessId);

            return templates.Select(t => new TemplateListDto
            {
                Id = t.Id,
                Name = t.Name,
                ProductName = t.ProductId.HasValue
                    ? products.FirstOrDefault(p => p.Id == t.ProductId.Value)?.Name
                    : null,
                ResponseTypeName = responseTypes.FirstOrDefault(rt => rt.Id == t.LeadResponseTypeId)?.Name ?? "Unknown",
                ResponseTimeInHours = t.ResponseTimeInHours,
                IsActive = t.IsActive,
                CreatedAtUtc = t.CreatedAtUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PreparedResponseDto?> RenderTemplateForLeadAsync(int templateId, int leadRequestId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var template = await _templateRepository.GetByIdAsync(templateId, businessId);
            if (template == null) return null;

            var lead = await _leadRequestRepository.GetByIdAsync(leadRequestId, businessId);
            if (lead == null) return null;

            var contact = await _contactRepository.GetByIdAsync(lead.ContactId, businessId);
            var product = lead.ProductId.HasValue
                ? await _productRepository.GetByIdAsync(lead.ProductId.Value, businessId)
                : null;

            var placeholders = new TemplatePlaceholderValues
            {
                ContactName = contact != null
                    ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                    : string.Empty,
                ProductName = product?.Name ?? string.Empty,
                BusinessName = string.Empty,
                ResponseTime = $"{template.ResponseTimeInHours} hours"
            };

            var renderedBody = RenderTemplate(template.BodyTemplate, placeholders);

            var responseTypes = await _responseTypeRepository.GetAllAsync();
            var responseType = responseTypes.FirstOrDefault(rt => rt.Id == template.LeadResponseTypeId);

            return new PreparedResponseDto
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                LeadResponseTypeId = template.LeadResponseTypeId,
                ResponseTypeName = responseType?.Name ?? "Unknown",
                Subject = template.Subject,
                RenderedBody = renderedBody,
                ResponseTimeInHours = template.ResponseTimeInHours
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
