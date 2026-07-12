using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Template CRUD and resolution for parser templates.
/// </summary>
public interface IParserTemplateService
{
    Task<List<ParserTemplate>> GetTemplatesForSupplierAsync(int supplierId, int businessId);
    Task<List<ParserTemplate>> GetAllForBusinessAsync(int businessId);
    Task<ParserTemplate?> GetTemplateByIdAsync(int templateId, int businessId);
    Task<ServiceResult<int>> CreateTemplateAsync(ParserTemplate template);
    Task<ServiceResult> UpdateTemplateAsync(ParserTemplate template, bool isSuperAdmin);
    Task<ServiceResult> DeleteTemplateAsync(int templateId, int businessId, bool isSuperAdmin);
}
