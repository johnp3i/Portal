using System.Text.Json;
using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Import;
using Portal.Infrastructure.Repositories.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Business logic for parser template management including CRUD and validation.
/// </summary>
public class ParserTemplateService : IParserTemplateService
{
    private readonly ParserTemplateRepository _repository;

    public ParserTemplateService(ParserTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ParserTemplate>> GetTemplatesForSupplierAsync(int supplierId, int businessId)
    {
        try
        {
            return await _repository.GetTemplatesForSupplierAsync(supplierId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<ParserTemplate>> GetAllForBusinessAsync(int businessId)
    {
        try
        {
            return await _repository.GetAllForBusinessAsync(businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ParserTemplate?> GetTemplateByIdAsync(int templateId, int businessId)
    {
        try
        {
            return await _repository.GetByIdAsync(templateId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult<int>> CreateTemplateAsync(ParserTemplate template)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(template.Name))
                return ServiceResult<int>.Fail("Template name is required.");

            if (template.SupplierId <= 0)
                return ServiceResult<int>.Fail("Supplier is required.");

            if (string.IsNullOrWhiteSpace(template.FileFormatType))
                return ServiceResult<int>.Fail("File format type is required.");

            if (template.FileFormatType != "CSV" && template.FileFormatType != "Excel")
                return ServiceResult<int>.Fail("File format must be 'CSV' or 'Excel'.");

            // Validate column mappings
            var mappingsError = ValidateColumnMappings(template.ColumnMappingsJson);
            if (mappingsError != null)
                return ServiceResult<int>.Fail(mappingsError);

            var id = await _repository.InsertAsync(template);
            return ServiceResult<int>.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateTemplateAsync(ParserTemplate template, bool isSuperAdmin)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(template.Id, template.BusinessId);
            if (existing == null)
                return ServiceResult.Fail("Template not found.");

            // Non-admin cannot edit managed templates
            if (existing.IsManaged && !isSuperAdmin)
                return ServiceResult.Fail("Managed templates cannot be edited.");

            // Validate column mappings
            var mappingsError = ValidateColumnMappings(template.ColumnMappingsJson);
            if (mappingsError != null)
                return ServiceResult.Fail(mappingsError);

            await _repository.UpdateAsync(template);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeleteTemplateAsync(int templateId, int businessId, bool isSuperAdmin)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(templateId, businessId);
            if (existing == null)
                return ServiceResult.Fail("Template not found.");

            // Non-admin cannot delete managed templates
            if (existing.IsManaged && !isSuperAdmin)
                return ServiceResult.Fail("Managed templates cannot be deleted.");

            await _repository.DeleteAsync(templateId, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static string? ValidateColumnMappings(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "Column mappings are required.";

        List<ColumnMapping>? mappings;
        try
        {
            mappings = JsonSerializer.Deserialize<List<ColumnMapping>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return "Column mappings JSON is invalid.";
        }

        if (mappings == null || mappings.Count == 0)
            return "At least one column mapping is required.";

        var activeTargets = mappings
            .Where(m => !m.IsSkipped)
            .Select(m => m.TargetField)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Must have InvoiceDate
        if (!activeTargets.Contains(ImportTargetFields.InvoiceDate))
            return "A mapping for InvoiceDate is required.";

        // Must have AmountExcludingVat OR TotalAmount
        if (!activeTargets.Contains(ImportTargetFields.AmountExcludingVat) &&
            !activeTargets.Contains(ImportTargetFields.TotalAmount))
            return "A mapping for AmountExcludingVat or TotalAmount is required.";

        return null;
    }
}
