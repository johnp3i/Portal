using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Vat)]
public class VatController : Controller
{
    private readonly IVatPeriodGenerationService _vatPeriodGenerationService;
    private readonly IVatSubmissionService _vatSubmissionService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;

    public VatController(
        IVatPeriodGenerationService vatPeriodGenerationService,
        IVatSubmissionService vatSubmissionService,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext)
    {
        _vatPeriodGenerationService = vatPeriodGenerationService;
        _vatSubmissionService = vatSubmissionService;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Generate/extend periods to ensure they are up to date
        var periods = await _vatPeriodGenerationService.GeneratePeriodsAsync();

        // Fetch all submissions for the current tenant
        var businessId = _currentTenantService.CurrentBusinessId;
        var submissions = await _dbContext.VatSubmissions
            .Where(s => s.BusinessId == businessId)
            .ToListAsync();

        // Build view model with status logic
        var viewModel = new VatPeriodsListViewModel
        {
            NeedsFirstPeriod = periods.Count == 0,
            Periods = periods
                .OrderByDescending(p => p.PeriodStartDate)
                .Select(p =>
                {
                    var submission = submissions.FirstOrDefault(s => s.VatSubmissionPeriodId == p.Id);

                    string status;
                    DateTime? submittedAtUtc = null;

                    if (submission != null && submission.IsSubmitted)
                    {
                        status = "Submitted";
                        submittedAtUtc = submission.SubmittedAtUtc;
                    }
                    else if (submission != null)
                    {
                        status = "Pending";
                    }
                    else
                    {
                        status = "Not Started";
                    }

                    return new VatPeriodRowViewModel
                    {
                        PeriodId = p.Id,
                        PeriodLabel = p.PeriodLabel,
                        PeriodStartDate = p.PeriodStartDate,
                        PeriodEndDate = p.PeriodEndDate,
                        Status = status,
                        SubmittedAtUtc = submittedAtUtc
                    };
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult CreateFirstPeriod()
    {
        return View(new CreateFirstPeriodViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Vat, AccessLevels.Full)]
    public async Task<IActionResult> CreateFirstPeriod(CreateFirstPeriodViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _vatPeriodGenerationService.CreateFirstPeriodAsync(
            model.StartYear, model.StartMonth, model.EndYear, model.EndMonth);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int periodId)
    {
        // Create or recalculate the submission for this period
        var result = await _vatSubmissionService.CreateOrRecalculateAsync(periodId);

        if (!result.Success || result.Data == null)
        {
            return NotFound();
        }

        var submission = result.Data;

        // Get CurrencySymbol from BusinessProfile
        var businessId = _currentTenantService.CurrentBusinessId;
        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

        var currencySymbol = profile?.CurrencySymbol ?? "€";

        // Get the period for label and dates
        var period = await _dbContext.VatSubmissionPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

        if (period == null)
        {
            return NotFound();
        }

        var viewModel = new VatSubmissionDetailViewModel
        {
            SubmissionId = submission.Id,
            PeriodId = period.Id,
            PeriodLabel = period.PeriodLabel,
            PeriodStartDate = period.PeriodStartDate,
            PeriodEndDate = period.PeriodEndDate,
            TotalOutputVat = submission.TotalOutputVat,
            TotalInputVat = submission.TotalInputVat,
            NetVatPayable = submission.NetVatPayable,
            IsSubmitted = submission.IsSubmitted,
            SubmittedAtUtc = submission.SubmittedAtUtc,
            CurrencySymbol = currencySymbol
        };

        // Compute discrepancy: Input VAT by InvoiceDate vs by period assignment
        var inputVatByDate = await _dbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.InvoiceDate >= period.PeriodStartDate
                && p.InvoiceDate <= period.PeriodEndDate)
            .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;

        viewModel.InputVatByDate = inputVatByDate;

        // Count late purchases included in this period (InvoiceDate outside this period but assigned here)
        viewModel.LatePurchasesIncluded = await _dbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.VatSubmissionPeriodId == periodId
                && (p.InvoiceDate < period.PeriodStartDate || p.InvoiceDate > period.PeriodEndDate))
            .CountAsync();

        // Count purchases from this period's date range that were reported in a later period
        viewModel.PurchasesReportedLater = await _dbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.InvoiceDate >= period.PeriodStartDate
                && p.InvoiceDate <= period.PeriodEndDate
                && p.VatSubmissionPeriodId != null
                && p.VatSubmissionPeriodId != periodId)
            .CountAsync();

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Vat, AccessLevels.Full)]
    public async Task<IActionResult> MarkAsSubmitted(int submissionId)
    {
        var result = await _vatSubmissionService.MarkAsSubmittedAsync(submissionId);

        return Json(new { success = result.Success, message = result.Success ? "Submission marked as submitted successfully." : result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoiceBreakdown(int periodId, int page = 1, string search = "")
    {
        const int pageSize = 15;
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate the period belongs to the current business
        var period = await _dbContext.VatSubmissionPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

        if (period == null)
        {
            return Json(new { success = false, message = "Period not found." });
        }

        // Build the combined query: explicit assignments OR date-range fallback
        // Conditions are mutually exclusive (explicit has non-null VatSubmissionPeriodId, date-range has null)
        var query = _dbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && (i.VatSubmissionPeriodId == periodId
                    || (i.VatSubmissionPeriodId == null
                        && i.InvoiceDate >= period.PeriodStartDate
                        && i.InvoiceDate <= period.PeriodEndDate)));

        // Apply search filter (case-insensitive contains on invoice number or customer name)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTrimmed = search.Trim();
            query = query.Where(i =>
                i.InvoiceNumber.Contains(searchTrimmed)
                || i.Customer.Name.Contains(searchTrimmed));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var safePage = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var items = await query
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new VatInvoiceBreakdownDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                CustomerName = i.Customer.Name,
                InvoiceDate = i.InvoiceDate,
                IsExplicitAssignment = i.VatSubmissionPeriodId == periodId,
                Subtotal = i.Subtotal,
                TaxAmount = i.TaxAmount
            })
            .ToListAsync();

        return Json(new
        {
            success = true,
            items,
            totalCount,
            page = safePage,
            pageSize,
            totalPages
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetPurchaseBreakdown(int periodId, int page = 1, string search = "")
    {
        const int pageSize = 15;
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate the period belongs to the current business
        var period = await _dbContext.VatSubmissionPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

        if (period == null)
        {
            return Json(new { success = false, message = "Period not found." });
        }

        // Build the combined query: explicit assignments OR date-range fallback
        // All non-cancelled purchases are included regardless of origin type —
        // EU Reverse Charge (type 2) purchases are part of the VAT submission even when VatAmount = 0
        var query = _dbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && !p.IsCancelled
                && (p.VatSubmissionPeriodId == periodId
                    || (p.VatSubmissionPeriodId == null
                        && p.InvoiceDate >= period.PeriodStartDate
                        && p.InvoiceDate <= period.PeriodEndDate)));

        // Apply search filter (case-insensitive contains on invoice number, description, or supplier name)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTrimmed = search.Trim();
            query = query.Where(p =>
                (p.InvoiceNumber != null && p.InvoiceNumber.Contains(searchTrimmed))
                || (p.Description != null && p.Description.Contains(searchTrimmed))
                || p.Supplier.Name.Contains(searchTrimmed));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var safePage = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var items = await query
            .OrderByDescending(p => p.InvoiceDate)
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new VatPurchaseBreakdownDto
            {
                Id = p.Id,
                InvoiceNumber = p.InvoiceNumber,
                Description = p.Description,
                SupplierName = p.Supplier.Name,
                InvoiceDate = p.InvoiceDate,
                CategoryName = p.ExpenseCategory.Name,
                IsExplicitAssignment = p.VatSubmissionPeriodId == periodId,
                PurchaseOriginTypeId = p.PurchaseOriginTypeId,
                VatAmount = p.VatAmount
            })
            .ToListAsync();

        return Json(new
        {
            success = true,
            items,
            totalCount,
            page = safePage,
            pageSize,
            totalPages,
            originSummary = await _dbContext.Purchases
                .Where(p => p.BusinessId == businessId
                    && !p.IsCancelled
                    && (p.VatSubmissionPeriodId == periodId
                        || (p.VatSubmissionPeriodId == null
                            && p.InvoiceDate >= period.PeriodStartDate
                            && p.InvoiceDate <= period.PeriodEndDate)))
                .GroupBy(p => p.PurchaseOriginTypeId)
                .Select(g => new
                {
                    originTypeId = g.Key,
                    count = g.Count(),
                    totalVat = g.Sum(p => p.VatAmount)
                })
                .ToListAsync()
        });
    }
}
