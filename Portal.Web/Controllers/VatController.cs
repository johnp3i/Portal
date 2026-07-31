using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;
using Portal.Web.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Vat)]
public class VatController : Controller
{
    private readonly IVatPeriodGenerationService _vatPeriodGenerationService;
    private readonly IVatSubmissionService _vatSubmissionService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;
    private readonly IViewRenderService _viewRenderService;
    private readonly IPurchaseService _purchaseService;

    public VatController(
        IVatPeriodGenerationService vatPeriodGenerationService,
        IVatSubmissionService vatSubmissionService,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext,
        IViewRenderService viewRenderService,
        IPurchaseService purchaseService)
    {
        _vatPeriodGenerationService = vatPeriodGenerationService;
        _vatSubmissionService = vatSubmissionService;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
        _viewRenderService = viewRenderService;
        _purchaseService = purchaseService;
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

        // Compute unassigned purchase counts for unsubmitted periods (optimised: single query)
        var unsubmittedPeriods = viewModel.Periods.Where(p => p.Status != "Submitted").ToList();
        if (unsubmittedPeriods.Count > 0)
        {
            // Single query: get all unassigned purchases within the full date range, then partition in memory
            var periodLookup = periods.ToDictionary(p => p.Id);
            var earliestStart = unsubmittedPeriods
                .Select(p => periodLookup.TryGetValue(p.PeriodId, out var per) ? per.PeriodStartDate : DateOnly.MaxValue)
                .Min();
            var latestEnd = unsubmittedPeriods
                .Select(p => periodLookup.TryGetValue(p.PeriodId, out var per) ? per.PeriodEndDate : DateOnly.MinValue)
                .Max();

            // Fetch all unassigned purchase dates in the full range (single query, only dates needed)
            var unassignedDates = await _dbContext.Purchases
                .Where(p => p.BusinessId == businessId
                    && p.VatSubmissionPeriodId == null
                    && !p.IsCancelled
                    && p.InvoiceDate >= earliestStart
                    && p.InvoiceDate <= latestEnd)
                .Select(p => p.InvoiceDate)
                .ToListAsync();

            // Partition counts by period date ranges in memory
            foreach (var periodRow in unsubmittedPeriods)
            {
                if (periodLookup.TryGetValue(periodRow.PeriodId, out var period))
                {
                    periodRow.UnassignedPurchaseCount = unassignedDates
                        .Count(d => d >= period.PeriodStartDate && d <= period.PeriodEndDate);
                }
            }
        }

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
    public async Task<IActionResult> DiagnoseVat(int periodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var period = await _dbContext.VatSubmissionPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);
        if (period == null) return Json(new { error = "Period not found" });

        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

        var explicitOutputVat = await _dbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && i.VatSubmissionPeriodId == periodId)
            .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

        var dateRangeOutputVat = await _dbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && i.VatSubmissionPeriodId == null
                && i.InvoiceDate >= period.PeriodStartDate
                && i.InvoiceDate <= period.PeriodEndDate)
            .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

        var creditNoteTaxReduction = await _dbContext.CreditNotes
            .Where(cn => cn.BusinessId == businessId
                && cn.VatSubmissionPeriodId == periodId
                && (cn.CreditNoteStatusTypeId == 2 || cn.CreditNoteStatusTypeId == 3))
            .SumAsync(cn => (decimal?)cn.TaxAmount) ?? 0m;

        var zReportVat = await _dbContext.RevenueSummaries
            .Where(rs => rs.BusinessId == businessId
                && rs.IsActive
                && rs.VatSubmissionPeriodId == periodId)
            .SumAsync(rs => (decimal?)rs.TotalVat) ?? 0m;

        var zReportCount = await _dbContext.RevenueSummaries
            .Where(rs => rs.BusinessId == businessId
                && rs.IsActive
                && rs.VatSubmissionPeriodId == periodId)
            .CountAsync();

        return Json(new
        {
            businessId,
            periodId,
            periodLabel = period.PeriodLabel,
            isZReportEnabled = profile?.IsZReportEnabled,
            profileFound = profile != null,
            explicitOutputVat,
            dateRangeOutputVat,
            creditNoteTaxReduction,
            zReportVat,
            zReportCount,
            totalWithZReports = explicitOutputVat + dateRangeOutputVat + zReportVat - creditNoteTaxReduction,
            totalWithoutZReports = explicitOutputVat + dateRangeOutputVat - creditNoteTaxReduction
        });
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
            InvoiceOutputVat = submission.TotalOutputVat, // Will be adjusted below if Z-Reports are included
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

        // Z-Reports: load Revenue Summaries for this period (if feature enabled)
        viewModel.IsZReportEnabled = profile?.IsZReportEnabled ?? false;
        if (viewModel.IsZReportEnabled)
        {
            var revenueSummaries = await _dbContext.RevenueSummaries
                .Where(rs => rs.BusinessId == businessId
                    && rs.IsActive
                    && rs.VatSubmissionPeriodId == periodId)
                .Join(_dbContext.RevenueSources,
                    rs => rs.RevenueSourceId,
                    src => src.Id,
                    (rs, src) => new { rs, src.Name })
                .ToListAsync();

            viewModel.ZReportRows = revenueSummaries.Select(x => new ZReportDetailRow
            {
                SourceName = x.Name,
                ZReportNumber = x.rs.ZReportNumber,
                PeriodDisplay = x.rs.PeriodEndDate.HasValue && x.rs.PeriodEndDate.Value != x.rs.SummaryDate
                    ? $"{x.rs.SummaryDate:dd/MM/yyyy} – {x.rs.PeriodEndDate:dd/MM/yyyy}"
                    : x.rs.SummaryDate.ToString("dd/MM/yyyy"),
                TotalVat = x.rs.TotalVat,
                AssignmentStatus = "Explicit"
            }).ToList();

            viewModel.ZReportTotalVat = revenueSummaries.Sum(x => x.rs.TotalVat);

            // Invoice-only Output VAT = Total Output VAT minus Z-Report contribution
            viewModel.InvoiceOutputVat = viewModel.TotalOutputVat - viewModel.ZReportTotalVat;
        }
        else
        {
            // Feature is disabled — check if there are Z-Reports assigned to this period
            // that are being excluded from the calculation (safety warning)
            var excludedCount = await _dbContext.RevenueSummaries
                .Where(rs => rs.BusinessId == businessId
                    && rs.IsActive
                    && rs.VatSubmissionPeriodId == periodId)
                .CountAsync();

            if (excludedCount > 0)
            {
                var excludedVat = await _dbContext.RevenueSummaries
                    .Where(rs => rs.BusinessId == businessId
                        && rs.IsActive
                        && rs.VatSubmissionPeriodId == periodId)
                    .SumAsync(rs => (decimal?)rs.TotalVat) ?? 0m;

                viewModel.HasExcludedZReports = true;
                viewModel.ExcludedZReportCount = excludedCount;
                viewModel.ExcludedZReportVat = excludedVat;
            }
        }

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
    public async Task<IActionResult> GetUnassignedPurchases(int periodId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var purchases = await _purchaseService.GetUnassignedForPeriodAsync(businessId, periodId);

            var items = purchases.Select(p => new
            {
                id = p.Id,
                description = p.Description,
                invoiceNumber = p.InvoiceNumber,
                supplierId = p.SupplierId,
                supplierName = p.Supplier?.Name,
                expenseCategoryId = p.ExpenseCategoryId,
                categoryName = p.ExpenseCategory?.Name,
                invoiceDate = p.InvoiceDate.ToString("dd MMM yyyy"),
                amountExcludingVat = p.AmountExcludingVat,
                vatAmount = p.VatAmount,
                totalAmount = p.TotalAmount
            }).ToList();

            return Json(new { success = true, items, count = items.Count });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load unassigned purchases.", error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetUnassignedCount(int periodId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var count = await _purchaseService.CountUnassignedForPeriodAsync(businessId, periodId);

            return Json(new { success = true, count });
        }
        catch (Exception ex)
        {
            return Json(new { success = true, count = 0 });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAssignPurchasesToPeriod([FromBody] AssignPurchasesRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var result = await _purchaseService.AssignPurchasesToPeriodAsync(businessId, request.PeriodId, request.PurchaseIds);

            return Json(new { success = result.Success, message = result.Success ? $"{result.Id} purchase(s) assigned." : result.Message, count = result.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUnassignPurchasesFromPeriod([FromBody] UnassignPurchasesRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var result = await _purchaseService.UnassignPurchasesFromPeriodAsync(businessId, request.PurchaseIds);

            return Json(new { success = result.Success, message = result.Success ? $"{result.Id} purchase(s) unassigned." : result.Message, count = result.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
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

    [HttpGet]
    public async Task<IActionResult> ExportInvoicesCsv(int periodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var period = await _dbContext.VatSubmissionPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

        if (period == null) return NotFound();

        var invoices = await _dbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && (i.VatSubmissionPeriodId == periodId
                    || (i.VatSubmissionPeriodId == null
                        && i.InvoiceDate >= period.PeriodStartDate
                        && i.InvoiceDate <= period.PeriodEndDate)))
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new
            {
                i.InvoiceNumber,
                CustomerName = i.Customer.Name,
                i.InvoiceDate,
                i.Subtotal,
                i.TaxAmount,
                i.TotalAmount,
                Assignment = i.VatSubmissionPeriodId == periodId ? "Explicit" : "Date Range"
            })
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Invoice Number,Customer,Invoice Date,Subtotal,VAT Amount,Total,Assignment");

        foreach (var inv in invoices)
        {
            csv.AppendLine($"\"{inv.InvoiceNumber}\",\"{inv.CustomerName}\",{inv.InvoiceDate:yyyy-MM-dd},{inv.Subtotal:F2},{inv.TaxAmount:F2},{inv.TotalAmount:F2},\"{inv.Assignment}\"");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"vat-period-invoices-{period.PeriodLabel.Replace(" ", "-")}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPurchasesCsv(int periodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var period = await _dbContext.VatSubmissionPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

        if (period == null) return NotFound();

        var originLabels = new Dictionary<int, string>
        {
            { 1, "Domestic" },
            { 2, "EU Reverse Charge" },
            { 3, "Non-EU" }
        };

        var purchases = await _dbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && !p.IsCancelled
                && (p.VatSubmissionPeriodId == periodId
                    || (p.VatSubmissionPeriodId == null
                        && p.InvoiceDate >= period.PeriodStartDate
                        && p.InvoiceDate <= period.PeriodEndDate)))
            .OrderByDescending(p => p.InvoiceDate)
            .Select(p => new
            {
                p.InvoiceNumber,
                SupplierName = p.Supplier.Name,
                CategoryName = p.ExpenseCategory.Name,
                p.Description,
                p.InvoiceDate,
                p.AmountExcludingVat,
                p.VatAmount,
                p.TotalAmount,
                p.PurchaseOriginTypeId,
                Assignment = p.VatSubmissionPeriodId == periodId ? "Explicit" : "Date Range"
            })
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Invoice Number,Supplier,Category,Description,Date,Net Amount,VAT Amount,Total,Origin Type,Assignment");

        foreach (var p in purchases)
        {
            var origin = originLabels.GetValueOrDefault(p.PurchaseOriginTypeId, "Other");
            csv.AppendLine($"\"{p.InvoiceNumber}\",\"{p.SupplierName}\",\"{p.CategoryName}\",\"{p.Description}\",{p.InvoiceDate:yyyy-MM-dd},{p.AmountExcludingVat:F2},{p.VatAmount:F2},{p.TotalAmount:F2},\"{origin}\",\"{p.Assignment}\"");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"vat-period-purchases-{period.PeriodLabel.Replace(" ", "-")}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> PeriodReport(int periodId)
    {
        var model = await BuildPeriodReportModelAsync(periodId);
        if (model == null) return NotFound();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PeriodReportPdf(int periodId)
    {
        var model = await BuildPeriodReportModelAsync(periodId);
        if (model == null) return NotFound();

        var html = await _viewRenderService.RenderViewToStringAsync("~/Views/Vat/_PeriodReportPdf.cshtml", model);

        await new BrowserFetcher().DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
        });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "12mm",
                Bottom = "12mm",
                Left = "12mm",
                Right = "12mm"
            }
        });

        var fileName = $"vat-period-report-{model.PeriodStartDate:yyyyMMdd}-{model.PeriodEndDate:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task<VatPeriodReportViewModel?> BuildPeriodReportModelAsync(int periodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var period = await _dbContext.VatSubmissionPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.BusinessId == businessId);

        if (period == null) return null;

        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

        var currencySymbol = profile?.CurrencySymbol ?? "€";

        // Generate all months in the period
        var months = new List<(DateOnly Start, DateOnly End, string Name)>();
        var current = new DateOnly(period.PeriodStartDate.Year, period.PeriodStartDate.Month, 1);
        var periodEnd = period.PeriodEndDate;
        while (current <= periodEnd)
        {
            var monthEnd = current.AddMonths(1).AddDays(-1);
            if (monthEnd > periodEnd) monthEnd = periodEnd;
            months.Add((current, monthEnd, current.ToString("MMMM yyyy")));
            current = current.AddMonths(1);
        }

        // Load invoices for this period
        var invoices = await _dbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && (i.VatSubmissionPeriodId == periodId
                    || (i.VatSubmissionPeriodId == null
                        && i.InvoiceDate >= period.PeriodStartDate
                        && i.InvoiceDate <= period.PeriodEndDate)))
            .Select(i => new { i.InvoiceDate, i.Subtotal, i.TaxAmount, i.TotalAmount })
            .ToListAsync();

        // Load purchases for this period
        var purchases = await _dbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && !p.IsCancelled
                && (p.VatSubmissionPeriodId == periodId
                    || (p.VatSubmissionPeriodId == null
                        && p.InvoiceDate >= period.PeriodStartDate
                        && p.InvoiceDate <= period.PeriodEndDate)))
            .Select(p => new { p.InvoiceDate, p.AmountExcludingVat, p.VatAmount, p.TotalAmount, p.PurchaseOriginTypeId, p.ExpenseCategory.ExpenseTypeId })
            .ToListAsync();

        // Section 1: Sales by month
        var salesByMonth = months.Select(m =>
        {
            var monthInvoices = invoices.Where(i => i.InvoiceDate >= m.Start && i.InvoiceDate <= m.End).ToList();
            return new MonthlyAmountRow
            {
                MonthName = m.Name,
                Net = monthInvoices.Sum(i => i.Subtotal),
                Vat = monthInvoices.Sum(i => i.TaxAmount),
                Gross = monthInvoices.Sum(i => i.TotalAmount),
                Count = monthInvoices.Count
            };
        }).ToList();

        // Add "Prior Period (Late)" row for explicitly-assigned invoices with dates before the period
        var lateInvoices = invoices.Where(i => i.InvoiceDate < period.PeriodStartDate).ToList();
        if (lateInvoices.Count > 0)
        {
            salesByMonth.Insert(0, new MonthlyAmountRow
            {
                MonthName = "Prior Period (Late)",
                Net = lateInvoices.Sum(i => i.Subtotal),
                Vat = lateInvoices.Sum(i => i.TaxAmount),
                Gross = lateInvoices.Sum(i => i.TotalAmount),
                Count = lateInvoices.Count
            });
        }

        // Section 2: Purchases by month
        var purchasesByMonth = months.Select(m =>
        {
            var monthPurchases = purchases.Where(p => p.InvoiceDate >= m.Start && p.InvoiceDate <= m.End).ToList();
            return new MonthlyAmountRow
            {
                MonthName = m.Name,
                Net = monthPurchases.Sum(p => p.AmountExcludingVat),
                Vat = monthPurchases.Sum(p => p.VatAmount),
                Gross = monthPurchases.Sum(p => p.TotalAmount),
                Count = monthPurchases.Count
            };
        }).ToList();

        // Add "Prior Period (Late)" row for explicitly-assigned purchases with dates before the period
        var latePurchases = purchases.Where(p => p.InvoiceDate < period.PeriodStartDate).ToList();
        if (latePurchases.Count > 0)
        {
            purchasesByMonth.Insert(0, new MonthlyAmountRow
            {
                MonthName = "Prior Period (Late)",
                Net = latePurchases.Sum(p => p.AmountExcludingVat),
                Vat = latePurchases.Sum(p => p.VatAmount),
                Gross = latePurchases.Sum(p => p.TotalAmount),
                Count = latePurchases.Count
            });
        }

        // Load expense types from database for dynamic breakdown
        var expenseTypes = await _dbContext.ExpenseTypes
            .OrderBy(et => et.Id)
            .Select(et => new ExpenseTypeLookup { Id = et.Id, Name = et.Name })
            .ToListAsync();

        // Section 3: Purchases by origin per month (with dynamic expense type breakdown)
        var purchasesByOriginPerMonth = months.Select(m =>
        {
            var monthPurchases = purchases.Where(p => p.InvoiceDate >= m.Start && p.InvoiceDate <= m.End).ToList();
            return new MonthlyOriginRow
            {
                MonthName = m.Name,
                Domestic = monthPurchases.Where(p => p.PurchaseOriginTypeId == 1).Sum(p => p.AmountExcludingVat),
                EuReverseCharge = monthPurchases.Where(p => p.PurchaseOriginTypeId == 2).Sum(p => p.AmountExcludingVat),
                NonEu = monthPurchases.Where(p => p.PurchaseOriginTypeId == 3).Sum(p => p.AmountExcludingVat),
                Total = monthPurchases.Sum(p => p.AmountExcludingVat),
                ExpenseTypeRows = expenseTypes.Select(et => new OriginExpenseTypeRow
                {
                    ExpenseTypeId = et.Id,
                    ExpenseTypeName = et.Name,
                    Domestic = monthPurchases.Where(p => p.PurchaseOriginTypeId == 1 && p.ExpenseTypeId == et.Id).Sum(p => p.AmountExcludingVat),
                    EuReverseCharge = monthPurchases.Where(p => p.PurchaseOriginTypeId == 2 && p.ExpenseTypeId == et.Id).Sum(p => p.AmountExcludingVat),
                    NonEu = monthPurchases.Where(p => p.PurchaseOriginTypeId == 3 && p.ExpenseTypeId == et.Id).Sum(p => p.AmountExcludingVat)
                }).ToList()
            };
        }).ToList();

        // Section 4: Period totals by origin
        var originNames = new Dictionary<int, string>
        {
            { 1, "Domestic" },
            { 2, "EU Reverse Charge" },
            { 3, "Non-EU" }
        };

        var periodTotalsByOrigin = purchases
            .GroupBy(p => p.PurchaseOriginTypeId)
            .OrderBy(g => g.Key)
            .Select(g => new OriginTotalRow
            {
                OriginName = originNames.GetValueOrDefault(g.Key, "Other"),
                Net = g.Sum(p => p.AmountExcludingVat),
                Vat = g.Sum(p => p.VatAmount),
                Gross = g.Sum(p => p.TotalAmount),
                Count = g.Count()
            })
            .ToList();

        var totalOutputVat = invoices.Sum(i => i.TaxAmount);
        var totalInputVat = purchases.Where(p => p.PurchaseOriginTypeId != 2).Sum(p => p.VatAmount);

        // Z-Reports: load Revenue Summaries assigned to this period (if enabled)
        var isZReportEnabled = profile?.IsZReportEnabled ?? false;
        var zReportRows = new List<ZReportPeriodRow>();
        var zReportOutputVat = 0m;

        if (isZReportEnabled)
        {
            var revenueSummaries = await _dbContext.RevenueSummaries
                .Where(rs => rs.BusinessId == businessId
                    && rs.IsActive
                    && rs.VatSubmissionPeriodId == periodId)
                .Join(_dbContext.RevenueSources,
                    rs => rs.RevenueSourceId,
                    src => src.Id,
                    (rs, src) => new { rs, src.Name })
                .ToListAsync();

            zReportRows = revenueSummaries.Select(x => new ZReportPeriodRow
            {
                SourceName = x.Name,
                ZReportNumber = x.rs.ZReportNumber,
                PeriodDisplay = x.rs.PeriodEndDate.HasValue && x.rs.PeriodEndDate.Value != x.rs.SummaryDate
                    ? $"{x.rs.SummaryDate:dd/MM/yyyy} – {x.rs.PeriodEndDate:dd/MM/yyyy}"
                    : x.rs.SummaryDate.ToString("dd/MM/yyyy"),
                Net = x.rs.TotalNet,
                Vat = x.rs.TotalVat,
                Gross = x.rs.TotalGross,
                Discount = x.rs.TotalDiscount
            }).ToList();

            zReportOutputVat = revenueSummaries.Sum(x => x.rs.TotalVat);
            totalOutputVat += zReportOutputVat;
        }

        return new VatPeriodReportViewModel
        {
            PeriodId = period.Id,
            PeriodLabel = period.PeriodLabel,
            PeriodStartDate = period.PeriodStartDate,
            PeriodEndDate = period.PeriodEndDate,
            CurrencySymbol = currencySymbol,
            OutputVat = totalOutputVat,
            InputVat = totalInputVat,
            TaxOwed = totalOutputVat - totalInputVat,
            SalesByMonth = salesByMonth,
            ZReportRows = zReportRows,
            IsZReportEnabled = isZReportEnabled,
            PurchasesByMonth = purchasesByMonth,
            PurchasesByOriginPerMonth = purchasesByOriginPerMonth,
            ExpenseTypes = expenseTypes,
            PeriodTotalsByOrigin = periodTotalsByOrigin
        };
    }
}
