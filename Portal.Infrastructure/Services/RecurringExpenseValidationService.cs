using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for recurring expense rule management and validation against purchase history.
/// </summary>
public class RecurringExpenseValidationService : IRecurringExpenseValidationService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly SupplierRecurringRuleRepository _ruleRepository;
    private readonly PurchaseRepository _purchaseRepository;
    private readonly PortalDbContext _portalDbContext;

    public RecurringExpenseValidationService(
        ICurrentTenantService currentTenantService,
        SupplierRecurringRuleRepository ruleRepository,
        PurchaseRepository purchaseRepository,
        PortalDbContext portalDbContext)
    {
        _currentTenantService = currentTenantService;
        _ruleRepository = ruleRepository;
        _purchaseRepository = purchaseRepository;
        _portalDbContext = portalDbContext;
    }

    /// <inheritdoc />
    public async Task<RecurringExpenseValidationResult> ValidateAsync(int businessId, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            return new RecurringExpenseValidationResult();
        }

        var activeRules = await _ruleRepository.GetActiveByBusinessIdAsync(businessId);

        // Pre-load supplier and category names for building results
        var supplierIds = activeRules.Select(r => r.SupplierId).Distinct().ToList();
        var categoryIds = activeRules.Where(r => r.ExpenseCategoryId.HasValue).Select(r => r.ExpenseCategoryId!.Value).Distinct().ToList();

        var supplierNames = await _portalDbContext.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        var categoryNames = await _portalDbContext.ExpenseCategories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var ruleResults = new List<RuleValidationResult>();

        foreach (var rule in activeRules)
        {
            try
            {
                // Calculate period months (inclusive month count)
                var periodMonths = (endDate.Year - startDate.Year) * 12 + (endDate.Month - startDate.Month) + 1;

                // Calculate expected count using integer division (floor)
                var expectedCount = (int)Math.Floor((double)periodMonths / rule.FrequencyMonths);

                // Skip this rule if the period is shorter than the frequency
                if (expectedCount == 0)
                    continue;

                // Calculate lookup window with grace period
                var lookupStart = startDate.AddDays(-rule.GracePeriodDays);
                var lookupEnd = endDate.AddDays(rule.GracePeriodDays);

                // Clamp lookupStart to minimum DateOnly value if it went negative
                if (lookupStart < DateOnly.MinValue)
                    lookupStart = DateOnly.MinValue;

                // Get actual qualifying purchase count
                var actualCount = await _purchaseRepository.CountQualifyingPurchasesAsync(
                    businessId, rule.SupplierId, rule.ExpenseCategoryId, lookupStart, lookupEnd);

                // Determine frequency status
                var frequencyStatus = DetermineStatus(actualCount, expectedCount);

                // Amount validation (if configured)
                int? amountMatchCount = null;
                bool? isAmountMatched = null;
                var overallStatus = frequencyStatus;

                if (rule.ExpectedAmount.HasValue && rule.AmountTolerancePercent.HasValue)
                {
                    amountMatchCount = await _purchaseRepository.CountAmountMatchingPurchasesAsync(
                        businessId, rule.SupplierId, rule.ExpenseCategoryId, lookupStart, lookupEnd,
                        rule.ExpectedAmount.Value, rule.AmountTolerancePercent.Value);

                    var amountStatus = DetermineStatus(amountMatchCount.Value, expectedCount);
                    isAmountMatched = amountMatchCount >= expectedCount;
                    overallStatus = WorstStatus(frequencyStatus, amountStatus);
                }

                // Build result
                var result = new RuleValidationResult
                {
                    RuleId = rule.Id,
                    SupplierName = supplierNames.GetValueOrDefault(rule.SupplierId, "Unknown"),
                    CategoryName = rule.ExpenseCategoryId.HasValue
                        ? categoryNames.GetValueOrDefault(rule.ExpenseCategoryId.Value, "Unknown")
                        : null,
                    Description = rule.Description,
                    FrequencyMonths = rule.FrequencyMonths,
                    ExpectedCount = expectedCount,
                    ActualCount = actualCount,
                    Status = overallStatus,
                    ExpectedAmount = rule.ExpectedAmount,
                    AmountTolerancePercent = rule.AmountTolerancePercent,
                    GracePeriodDays = rule.GracePeriodDays,
                    IsAmountMatched = isAmountMatched,
                    AmountMatchCount = amountMatchCount
                };

                ruleResults.Add(result);
            }
            catch (Exception ex)
            {
                // Partial failure tolerance: if one rule fails, continue evaluating others
                continue;
            }
        }

        // Sort results: FAIL first, WARNING second, PASS last
        ruleResults = ruleResults
            .OrderBy(r => r.Status == "fail" ? 0 : r.Status == "warning" ? 1 : 2)
            .ToList();

        // Build summary
        var summary = new ValidationSummary
        {
            TotalRules = ruleResults.Count,
            PassCount = ruleResults.Count(r => r.Status == "pass"),
            WarningCount = ruleResults.Count(r => r.Status == "warning"),
            FailCount = ruleResults.Count(r => r.Status == "fail")
        };

        return new RecurringExpenseValidationResult
        {
            Summary = summary,
            RuleResults = ruleResults
        };
    }

    /// <inheritdoc />
    public async Task<List<RecurringRuleViewModel>> GetRulesForBusinessAsync(int businessId)
    {
        try
        {
            var rules = await _ruleRepository.GetAllByBusinessIdAsync(businessId);

            // Get supplier names
            var supplierIds = rules.Select(r => r.SupplierId).Distinct().ToList();
            var supplierNames = await _portalDbContext.Suppliers
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name);

            // Get category names
            var categoryIds = rules.Where(r => r.ExpenseCategoryId.HasValue).Select(r => r.ExpenseCategoryId!.Value).Distinct().ToList();
            var categoryNames = await _portalDbContext.ExpenseCategories
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            // Map to view models
            var viewModels = rules.Select(rule => new RecurringRuleViewModel
            {
                Id = rule.Id,
                SupplierId = rule.SupplierId,
                SupplierName = supplierNames.GetValueOrDefault(rule.SupplierId, "Unknown"),
                ExpenseCategoryId = rule.ExpenseCategoryId,
                CategoryName = rule.ExpenseCategoryId.HasValue
                    ? categoryNames.GetValueOrDefault(rule.ExpenseCategoryId.Value, "Unknown")
                    : null,
                FrequencyMonths = rule.FrequencyMonths,
                FrequencyLabel = GetFrequencyLabel(rule.FrequencyMonths),
                ExpectedAmount = rule.ExpectedAmount,
                AmountTolerancePercent = rule.AmountTolerancePercent,
                GracePeriodDays = rule.GracePeriodDays,
                Description = rule.Description,
                IsActive = rule.IsActive
            }).ToList();

            return viewModels;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SaveRuleAsync(int businessId, SaveRecurringRuleRequest request)
    {
        try
        {
            // Validate FrequencyMonths
            if (request.FrequencyMonths < 1)
            {
                return ServiceResult.Fail("Frequency must be at least 1 month.");
            }

            // Validate Description
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return ServiceResult.Fail("Description is required.");
            }

            if (request.Description.Length > 200)
            {
                return ServiceResult.Fail("Description must not exceed 200 characters.");
            }

            // Validate GracePeriodDays
            if (request.GracePeriodDays < 0 || request.GracePeriodDays > 15)
            {
                return ServiceResult.Fail("Grace period must be between 0 and 15 days.");
            }

            // Validate ExpectedAmount and tolerance
            if (request.ExpectedAmount.HasValue)
            {
                if (request.ExpectedAmount.Value <= 0)
                {
                    return ServiceResult.Fail("Expected amount must be greater than zero.");
                }

                // Default tolerance to 5 if not set
                if (!request.AmountTolerancePercent.HasValue)
                {
                    request.AmountTolerancePercent = 5m;
                }
            }

            if (request.Id == null)
            {
                // INSERT new rule
                var entity = new SupplierRecurringRule
                {
                    BusinessId = businessId,
                    SupplierId = request.SupplierId,
                    ExpenseCategoryId = request.ExpenseCategoryId,
                    FrequencyMonths = request.FrequencyMonths,
                    ExpectedAmount = request.ExpectedAmount,
                    AmountTolerancePercent = request.AmountTolerancePercent,
                    GracePeriodDays = request.GracePeriodDays,
                    Description = request.Description,
                    IsActive = true,
                    IsDeleted = false
                };

                await _ruleRepository.InsertAsync(entity);
            }
            else
            {
                // UPDATE existing rule — verify ownership first
                var existing = await _ruleRepository.GetByIdAsync(request.Id.Value, businessId);
                if (existing == null)
                {
                    return ServiceResult.Fail("Rule not found or does not belong to this business.");
                }

                existing.SupplierId = request.SupplierId;
                existing.ExpenseCategoryId = request.ExpenseCategoryId;
                existing.FrequencyMonths = request.FrequencyMonths;
                existing.ExpectedAmount = request.ExpectedAmount;
                existing.AmountTolerancePercent = request.AmountTolerancePercent;
                existing.GracePeriodDays = request.GracePeriodDays;
                existing.Description = request.Description;

                await _ruleRepository.UpdateAsync(existing);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteRuleAsync(int businessId, int ruleId)
    {
        try
        {
            // Verify rule exists and belongs to business
            var existing = await _ruleRepository.GetByIdAsync(ruleId, businessId);
            if (existing == null)
            {
                return ServiceResult.Fail("Rule not found or does not belong to this business.");
            }

            await _ruleRepository.SoftDeleteAsync(ruleId, businessId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ToggleRuleAsync(int businessId, int ruleId)
    {
        try
        {
            // Get rule by ID and business
            var existing = await _ruleRepository.GetByIdAsync(ruleId, businessId);
            if (existing == null)
            {
                return ServiceResult.Fail("Rule not found or does not belong to this business.");
            }

            // Toggle: set to opposite of current state
            await _ruleRepository.ToggleIsActiveAsync(ruleId, businessId, !existing.IsActive);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static string DetermineStatus(int actualCount, int expectedCount)
    {
        if (actualCount >= expectedCount) return "pass";
        if (actualCount > 0) return "warning";
        return "fail";
    }

    private static string WorstStatus(string a, string b)
    {
        if (a == "fail" || b == "fail") return "fail";
        if (a == "warning" || b == "warning") return "warning";
        return "pass";
    }

    private static string GetFrequencyLabel(int frequencyMonths)
    {
        return frequencyMonths switch
        {
            1 => "Monthly",
            2 => "Bimonthly",
            3 => "Quarterly",
            6 => "Semi-annually",
            12 => "Annually",
            _ => $"{frequencyMonths} months"
        };
    }
}
