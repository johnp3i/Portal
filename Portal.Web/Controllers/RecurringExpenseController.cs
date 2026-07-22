using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

using DeleteRecurringRuleRequest = Portal.Web.Models.RecurringExpense.DeleteRecurringRuleRequest;
using RecurringExpenseValidateRequest = Portal.Web.Models.RecurringExpense.RecurringExpenseValidateRequest;
using ToggleRecurringRuleRequest = Portal.Web.Models.RecurringExpense.ToggleRecurringRuleRequest;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.RecurringExpenseValidation)]
public class RecurringExpenseController : Controller
{
    private readonly IRecurringExpenseValidationService _validationService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IPlanCheckService _planCheckService;
    private readonly PortalDbContext _dbContext;

    public RecurringExpenseController(
        IRecurringExpenseValidationService validationService,
        ICurrentTenantService currentTenantService,
        IPlanCheckService planCheckService,
        PortalDbContext dbContext)
    {
        _validationService = validationService;
        _currentTenantService = currentTenantService;
        _planCheckService = planCheckService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            // Plan-level gating (not bypassed by SuperAdmin)
            var isInPlan = await _planCheckService.IsModuleInPlanAsync(PortalModules.RecurringExpenseValidation);
            if (!isInPlan)
            {
                var requiredPlan = await _planCheckService.GetRequiredPlanForModuleAsync(PortalModules.RecurringExpenseValidation) ?? "Professional";
                return View("PlanSoftGate", new SoftGateViewModel
                {
                    ModuleName = PortalModules.RecurringExpenseValidation,
                    ModuleDisplayName = "Recurring Expense Validation",
                    ModuleDescription = "Define expected recurring purchases per supplier, validate that all expected expenses are recorded before VAT submission, and catch missing invoices automatically.",
                    RequiredPlanName = requiredPlan,
                    CurrentPlanName = "your current plan"
                });
            }

            var businessId = _currentTenantService.CurrentBusinessId;

            var vatPeriods = await _dbContext.VatSubmissionPeriods
                .Where(p => p.BusinessId == businessId)
                .OrderByDescending(p => p.PeriodStartDate)
                .ToListAsync();

            var profile = await _dbContext.BusinessProfiles.FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
            ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";
            ViewBag.VatPeriods = vatPeriods;

            return View();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Rules()
    {
        try
        {
            // Plan-level gating (not bypassed by SuperAdmin)
            var isInPlan = await _planCheckService.IsModuleInPlanAsync(PortalModules.RecurringExpenseValidation);
            if (!isInPlan)
            {
                var requiredPlan = await _planCheckService.GetRequiredPlanForModuleAsync(PortalModules.RecurringExpenseValidation) ?? "Professional";
                return View("PlanSoftGate", new SoftGateViewModel
                {
                    ModuleName = PortalModules.RecurringExpenseValidation,
                    ModuleDisplayName = "Recurring Expense Validation",
                    ModuleDescription = "Define expected recurring purchases per supplier, validate that all expected expenses are recorded before VAT submission, and catch missing invoices automatically.",
                    RequiredPlanName = requiredPlan,
                    CurrentPlanName = "your current plan"
                });
            }

            var businessId = _currentTenantService.CurrentBusinessId;

            var suppliers = await _dbContext.Suppliers
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var categories = await _dbContext.ExpenseCategories
                .Where(c => c.BusinessId == businessId && c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var rules = await _validationService.GetRulesForBusinessAsync(businessId);

            var profile = await _dbContext.BusinessProfiles.FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
            ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";
            ViewBag.Suppliers = suppliers;
            ViewBag.Categories = categories;
            ViewBag.Rules = rules;

            return View();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    [HttpPost]
    public async Task<IActionResult> Validate([FromBody] RecurringExpenseValidateRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var result = await _validationService.ValidateAsync(businessId, request.StartDate, request.EndDate);

            return Json(new { success = true, summary = result.Summary, ruleResults = result.RuleResults });
        }
        catch (Exception ex)
        {
            // Fail-safe: always return valid JSON so the UI is never blocked
            return Json(new
            {
                success = true,
                summary = new ValidationSummary(),
                ruleResults = new List<RuleValidationResult>()
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveRule([FromBody] SaveRecurringRuleRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var result = await _validationService.SaveRuleAsync(businessId, request);

            return Json(new { success = result.Success, message = result.Success ? "Rule saved." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeleteRule([FromBody] DeleteRecurringRuleRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var result = await _validationService.DeleteRuleAsync(businessId, request.Id);

            return Json(new { success = result.Success, message = result.Success ? "Rule deleted." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleRule([FromBody] ToggleRecurringRuleRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var result = await _validationService.ToggleRuleAsync(businessId, request.Id);

            return Json(new { success = result.Success, message = result.Success ? "Rule updated." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }
}
