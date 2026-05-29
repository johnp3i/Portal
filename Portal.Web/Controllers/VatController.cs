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

    public VatController(
        IVatPeriodGenerationService vatPeriodGenerationService,
        IVatSubmissionService vatSubmissionService,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext,
        IViewRenderService viewRenderService)
    {
        _vatPeriodGenerationService = vatPeriodGenerationService;
        _vatSubmissionService = vatSubmissionService;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
        _viewRenderService = viewRenderService;
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
            .Select(p => new { p.InvoiceDate, p.AmountExcludingVat, p.VatAmount, p.TotalAmount, p.PurchaseOriginTypeId })
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

        // Section 3: Purchases by origin per month
        var purchasesByOriginPerMonth = months.Select(m =>
        {
            var monthPurchases = purchases.Where(p => p.InvoiceDate >= m.Start && p.InvoiceDate <= m.End).ToList();
            return new MonthlyOriginRow
            {
                MonthName = m.Name,
                Domestic = monthPurchases.Where(p => p.PurchaseOriginTypeId == 1).Sum(p => p.AmountExcludingVat),
                EuReverseCharge = monthPurchases.Where(p => p.PurchaseOriginTypeId == 2).Sum(p => p.AmountExcludingVat),
                NonEu = monthPurchases.Where(p => p.PurchaseOriginTypeId == 3).Sum(p => p.AmountExcludingVat),
                Total = monthPurchases.Sum(p => p.AmountExcludingVat)
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
            PurchasesByMonth = purchasesByMonth,
            PurchasesByOriginPerMonth = purchasesByOriginPerMonth,
            PeriodTotalsByOrigin = periodTotalsByOrigin
        };
    }
}
