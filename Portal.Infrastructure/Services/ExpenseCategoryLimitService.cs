using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for expense category spending limit management and evaluation.
/// </summary>
public class ExpenseCategoryLimitService : IExpenseCategoryLimitService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ExpenseCategoryLimitRepository _expenseCategoryLimitRepository;
    private readonly PurchaseRepository _purchaseRepository;
    private readonly PortalDbContext _portalDbContext;

    public ExpenseCategoryLimitService(
        ICurrentTenantService currentTenantService,
        ExpenseCategoryLimitRepository expenseCategoryLimitRepository,
        PurchaseRepository purchaseRepository,
        PortalDbContext portalDbContext)
    {
        _currentTenantService = currentTenantService;
        _expenseCategoryLimitRepository = expenseCategoryLimitRepository;
        _purchaseRepository = purchaseRepository;
        _portalDbContext = portalDbContext;
    }

    /// <inheritdoc />
    public async Task<List<ExpenseCategoryLimitViewModel>> GetLimitsForBusinessAsync()
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Get all active expense categories for the business
        var categories = await _portalDbContext.ExpenseCategories
            .Where(c => c.BusinessId == businessId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        // Get all configured limits for the business
        var limits = await _expenseCategoryLimitRepository.GetAllByBusinessIdAsync(businessId);

        // Left-join categories with limits
        var result = categories.Select(category =>
        {
            var limit = limits.FirstOrDefault(l => l.ExpenseCategoryId == category.Id);
            return new ExpenseCategoryLimitViewModel
            {
                ExpenseCategoryId = category.Id,
                CategoryName = category.Name,
                AnnualLimitEur = limit?.AnnualLimitEur,
                PeriodLimitEur = limit?.PeriodLimitEur
            };
        }).ToList();

        return result;
    }

    /// <inheritdoc />
    public async Task<LimitCheckResult> EvaluateLimitsAsync(CheckLimitsRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            // Get limit configuration for this business + category
            var limitConfig = await _expenseCategoryLimitRepository.GetByBusinessAndCategoryAsync(businessId, request.ExpenseCategoryId);

            // If no limit configured or both limits are null, return no warnings
            if (limitConfig == null || (limitConfig.AnnualLimitEur == null && limitConfig.PeriodLimitEur == null))
            {
                return new LimitCheckResult { HasWarning = false };
            }

            var warnings = new List<LimitWarning>();

            // Evaluate annual limit
            if (limitConfig.AnnualLimitEur.HasValue)
            {
                var year = request.InvoiceDate.Year;
                var annualSpending = await _purchaseRepository.GetAnnualSpendingAsync(
                    businessId, request.ExpenseCategoryId, year, request.PurchaseId);

                var projectedTotal = annualSpending + request.TotalAmount;

                if (projectedTotal > limitConfig.AnnualLimitEur.Value)
                {
                    warnings.Add(new LimitWarning
                    {
                        LimitType = "annual",
                        ConfiguredLimit = limitConfig.AnnualLimitEur.Value,
                        CumulativeTotal = annualSpending,
                        ProjectedTotal = projectedTotal,
                        ExceededBy = projectedTotal - limitConfig.AnnualLimitEur.Value
                    });
                }
            }

            // Evaluate period limit
            if (limitConfig.PeriodLimitEur.HasValue)
            {
                // Find the VAT period containing the InvoiceDate for this business
                var vatPeriod = await _portalDbContext.VatSubmissionPeriods
                    .Where(p => p.BusinessId == businessId
                        && p.PeriodStartDate <= request.InvoiceDate
                        && p.PeriodEndDate >= request.InvoiceDate)
                    .FirstOrDefaultAsync();

                // Skip period evaluation if no matching VAT period exists
                if (vatPeriod != null)
                {
                    var periodSpending = await _purchaseRepository.GetPeriodSpendingAsync(
                        businessId, request.ExpenseCategoryId, vatPeriod.PeriodStartDate, vatPeriod.PeriodEndDate, request.PurchaseId);

                    var projectedTotal = periodSpending + request.TotalAmount;

                    if (projectedTotal > limitConfig.PeriodLimitEur.Value)
                    {
                        warnings.Add(new LimitWarning
                        {
                            LimitType = "period",
                            ConfiguredLimit = limitConfig.PeriodLimitEur.Value,
                            CumulativeTotal = periodSpending,
                            ProjectedTotal = projectedTotal,
                            ExceededBy = projectedTotal - limitConfig.PeriodLimitEur.Value
                        });
                    }
                }
            }

            return new LimitCheckResult
            {
                HasWarning = warnings.Count > 0,
                Warnings = warnings
            };
        }
        catch (Exception)
        {
            // Fail-safe: return no warnings on any exception so the purchase form is never blocked
            return new LimitCheckResult { HasWarning = false };
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SaveLimitAsync(int expenseCategoryId, decimal? annualLimitEur, decimal? periodLimitEur)
    {
        // Validate that at least one limit is provided and all provided values are > 0
        if (annualLimitEur == null && periodLimitEur == null)
        {
            return ServiceResult.Fail("At least one limit value must be provided.");
        }

        if (annualLimitEur.HasValue && annualLimitEur.Value <= 0)
        {
            return ServiceResult.Fail("Annual limit must be greater than zero.");
        }

        if (periodLimitEur.HasValue && periodLimitEur.Value <= 0)
        {
            return ServiceResult.Fail("Period limit must be greater than zero.");
        }

        var businessId = _currentTenantService.CurrentBusinessId;

        // Upsert pattern: check if record exists
        var existing = await _expenseCategoryLimitRepository.GetByBusinessAndCategoryAsync(businessId, expenseCategoryId);

        if (existing != null)
        {
            // Update existing record
            existing.AnnualLimitEur = annualLimitEur;
            existing.PeriodLimitEur = periodLimitEur;
            await _expenseCategoryLimitRepository.UpdateAsync(existing);
        }
        else
        {
            // Insert new record
            var entity = new ExpenseCategoryLimit
            {
                BusinessId = businessId,
                ExpenseCategoryId = expenseCategoryId,
                AnnualLimitEur = annualLimitEur,
                PeriodLimitEur = periodLimitEur
            };
            await _expenseCategoryLimitRepository.InsertAsync(entity);
        }

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ClearLimitAsync(int expenseCategoryId, string limitType)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Map limitType to database field name
        var fieldName = limitType switch
        {
            "annual" => "AnnualLimitEur",
            "period" => "PeriodLimitEur",
            _ => null
        };

        if (fieldName == null)
        {
            return ServiceResult.Fail("Invalid limit type. Use 'annual' or 'period'.");
        }

        await _expenseCategoryLimitRepository.ClearLimitFieldAsync(businessId, expenseCategoryId, fieldName);

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<List<CategorySpendingProgress>> GetSpendingProgressAsync()
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        var limits = await _expenseCategoryLimitRepository.GetAllByBusinessIdAsync(businessId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;

        // Find the current VAT period for this business
        var currentPeriod = await _portalDbContext.VatSubmissionPeriods
            .Where(p => p.BusinessId == businessId
                && p.PeriodStartDate <= today
                && p.PeriodEndDate >= today)
            .FirstOrDefaultAsync();

        var result = new List<CategorySpendingProgress>();

        foreach (var limit in limits)
        {
            if (limit.AnnualLimitEur == null && limit.PeriodLimitEur == null)
                continue;

            var progress = new CategorySpendingProgress
            {
                ExpenseCategoryId = limit.ExpenseCategoryId,
                AnnualLimitEur = limit.AnnualLimitEur,
                PeriodLimitEur = limit.PeriodLimitEur
            };

            if (limit.AnnualLimitEur.HasValue)
            {
                progress.AnnualSpent = await _purchaseRepository.GetAnnualSpendingAsync(
                    businessId, limit.ExpenseCategoryId, currentYear, null);
                progress.AnnualYear = currentYear;
            }

            if (limit.PeriodLimitEur.HasValue && currentPeriod != null)
            {
                progress.PeriodSpent = await _purchaseRepository.GetPeriodSpendingAsync(
                    businessId, limit.ExpenseCategoryId, currentPeriod.PeriodStartDate, currentPeriod.PeriodEndDate, null);
                progress.PeriodLabel = $"{currentPeriod.PeriodStartDate:MMM}–{currentPeriod.PeriodEndDate:MMM yyyy}";
            }

            result.Add(progress);
        }

        return result;
    }
}
