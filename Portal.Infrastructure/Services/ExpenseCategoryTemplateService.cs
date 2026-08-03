using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Manages expense category templates and handles business import (copy operation).
/// </summary>
public class ExpenseCategoryTemplateService : IExpenseCategoryTemplateService
{
    private readonly ExpenseCategoryTemplateRepository _templateRepository;
    private readonly PortalDbContext _dbContext;

    public ExpenseCategoryTemplateService(
        ExpenseCategoryTemplateRepository templateRepository,
        PortalDbContext dbContext)
    {
        _templateRepository = templateRepository;
        _dbContext = dbContext;
    }

    public async Task<List<ExpenseCategoryTemplate>> GetActiveTemplatesAsync()
    {
        return await _templateRepository.GetAllActiveAsync();
    }

    public async Task<List<ExpenseCategoryTemplate>> GetAllTemplatesAsync()
    {
        return await _templateRepository.GetAllAsync();
    }

    public async Task<ServiceResult> CreateAsync(string name, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return ServiceResult.Fail("Template name is required.");

            await _templateRepository.InsertAsync(new ExpenseCategoryTemplate { Name = name.Trim(), Description = description?.Trim() });
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateAsync(int id, string name, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return ServiceResult.Fail("Template name is required.");

            await _templateRepository.UpdateAsync(id, name.Trim(), description?.Trim());
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateAsync(int id)
    {
        try
        {
            await _templateRepository.DeactivateAsync(id);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ReactivateAsync(int id)
    {
        try
        {
            await _templateRepository.ReactivateAsync(id);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Imports selected templates into the business's ExpenseCategory table.
    /// Skips duplicates (case-insensitive name match). Returns count of imported.
    /// </summary>
    public async Task<ServiceResult<int>> ImportTemplatesAsync(int businessId, int[] templateIds)
    {
        try
        {
            if (templateIds == null || templateIds.Length == 0)
                return ServiceResult<int>.Fail("No templates selected.");

            var templates = await _templateRepository.GetAllActiveAsync();
            var selectedTemplates = templates.Where(t => templateIds.Contains(t.Id)).ToList();

            if (selectedTemplates.Count == 0)
                return ServiceResult<int>.Fail("No valid templates found.");

            // Get existing category names for this business (case-insensitive)
            var existingNames = await _dbContext.ExpenseCategories
                .Where(c => c.BusinessId == businessId)
                .Select(c => c.Name.ToLower())
                .ToListAsync();

            var importedCount = 0;

            foreach (var template in selectedTemplates)
            {
                if (existingNames.Contains(template.Name.ToLower()))
                    continue; // Skip duplicate

                _dbContext.ExpenseCategories.Add(new ExpenseCategory
                {
                    BusinessId = businessId,
                    Name = template.Name,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
                importedCount++;
            }

            if (importedCount > 0)
                await _dbContext.SaveChangesAsync();

            var skipped = selectedTemplates.Count - importedCount;
            return ServiceResult<int>.Ok(importedCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
