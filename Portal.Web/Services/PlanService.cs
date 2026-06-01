using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Web.Models;

namespace Portal.Web.Services;

/// <summary>
/// Provides read-only access to subscription plans from the Portal database.
/// Used by the registration page to display available plans and pre-select by slug.
/// </summary>
public class PlanService : IPlanService
{
    private readonly PortalDbContext _dbContext;

    public PlanService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<List<PlanDisplayModel>> GetActivePlansOrderedAsync()
    {
        try
        {
            return await _dbContext.Plans
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new PlanDisplayModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    MonthlyPriceEur = p.MonthlyPriceEur,
                    Description = p.Description
                })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PlanDisplayModel?> GetPlanBySlugAsync(string slug)
    {
        try
        {
            return await _dbContext.Plans
                .Where(p => p.Slug == slug && p.IsActive)
                .Select(p => new PlanDisplayModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    MonthlyPriceEur = p.MonthlyPriceEur,
                    Description = p.Description
                })
                .FirstOrDefaultAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
