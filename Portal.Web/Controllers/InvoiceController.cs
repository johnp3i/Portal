using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Invoice)]
public class InvoiceController : Controller
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceSectionService _sectionService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ICustomerService _customerService;
    private readonly ILogoService _logoService;
    private readonly IBusinessService _businessService;
    private readonly BusinessPaymentDetailRepository _paymentDetailRepository;

    public InvoiceController(
        IInvoiceService invoiceService,
        IInvoiceSectionService sectionService,
        ICurrentTenantService tenantService,
        ICustomerService customerService,
        ILogoService logoService,
        IBusinessService businessService,
        BusinessPaymentDetailRepository paymentDetailRepository)
    {
        _invoiceService = invoiceService;
        _sectionService = sectionService;
        _tenantService = tenantService;
        _customerService = customerService;
        _logoService = logoService;
        _businessService = businessService;
        _paymentDetailRepository = paymentDetailRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? status, int? financialStatus, int? customer)
    {
        var invoices = await _invoiceService.GetInvoicesAsync(status, financialStatus, customer);
        var customers = await _customerService.GetCustomersAsync(null, true);

        ViewBag.StatusFilter = status;
        ViewBag.FinancialStatusFilter = financialStatus;
        ViewBag.CustomerFilter = customer;
        ViewBag.Customers = customers;
        ViewBag.Statuses = new List<InvoiceStatusType>
        {
            new() { Id = 1, Name = "Draft" },
            new() { Id = 2, Name = "Issued" },
            new() { Id = 3, Name = "Cancelled" }
        };
        ViewBag.FinancialStatuses = new List<InvoiceFinancialStatusType>
        {
            new() { Id = 1, Name = "Unpaid" },
            new() { Id = 2, Name = "PartiallyPaid" },
            new() { Id = 3, Name = "Paid" },
            new() { Id = 4, Name = "Overdue" },
            new() { Id = 5, Name = "WrittenOff" }
        };

        return View(invoices);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null) return NotFound();

        var lines = await _invoiceService.GetInvoiceLinesAsync(id);
        var sections = await _sectionService.GetByInvoiceIdAsync(id);
        var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId);

        var statusNames = new Dictionary<int, string>
        {
            { 1, "Draft" }, { 2, "Issued" }, { 3, "Cancelled" }
        };

        var financialStatusNames = new Dictionary<int, string>
        {
            { 1, "Unpaid" }, { 2, "PartiallyPaid" }, { 3, "Paid" },
            { 4, "Overdue" }, { 5, "WrittenOff" }
        };

        ViewBag.Invoice = invoice;
        ViewBag.Lines = lines;
        ViewBag.Sections = sections;
        ViewBag.CustomerName = customer?.Name ?? "Unknown";
        ViewBag.StatusName = statusNames.GetValueOrDefault(invoice.InvoiceStatusTypeId, "Unknown");
        ViewBag.FinancialStatusName = financialStatusNames.GetValueOrDefault(invoice.InvoiceFinancialStatusTypeId, "Unknown");

        return View(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var customers = await _customerService.GetCustomersAsync(null, true);
        ViewBag.Customers = customers;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown, List<CreateInvoiceLineDto> lines,
        List<CreateInvoiceSectionDto>? sections)
    {
        try
        {
            var invoice = await _invoiceService.CreateInvoiceAsync(customerId, invoiceDate, dueDate,
                notes, isGrandTotalShown, lines, sections);
            return RedirectToAction(nameof(Detail), new { id = invoice.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var customers = await _customerService.GetCustomersAsync(null, true);
            ViewBag.Customers = customers;
            return View();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Preview(int id)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null) return NotFound();

        var lines = await _invoiceService.GetInvoiceLinesAsync(id);
        var sections = await _sectionService.GetByInvoiceIdAsync(id);
        var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId);
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();
        var business = await _businessService.GetBusinessByIdAsync(_tenantService.CurrentBusinessId);
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        var paymentDetails = await _paymentDetailRepository.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);

        ViewBag.Invoice = invoice;
        ViewBag.Lines = lines;
        ViewBag.Sections = sections;
        ViewBag.CustomerName = customer?.Name ?? "Unknown";
        ViewBag.LogoUrl = primaryLogo?.PublicUrl;
        ViewBag.BusinessName = business?.Name ?? "";
        ViewBag.Profile = profile;
        ViewBag.PaymentDetails = paymentDetails;

        return View(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null) return NotFound();

        if (invoice.InvoiceStatusTypeId != 1)
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        var lines = await _invoiceService.GetInvoiceLinesAsync(id);
        var sections = await _sectionService.GetByInvoiceIdAsync(id);
        var customers = await _customerService.GetCustomersAsync(null, true);

        ViewBag.Lines = lines;
        ViewBag.Sections = sections;
        ViewBag.Customers = customers;

        return View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown)
    {
        try
        {
            await _invoiceService.UpdateInvoiceAsync(id, customerId, invoiceDate, dueDate, notes, isGrandTotalShown);
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null) return NotFound();

            var lines = await _invoiceService.GetInvoiceLinesAsync(id);
            var sections = await _sectionService.GetByInvoiceIdAsync(id);
            var customers = await _customerService.GetCustomersAsync(null, true);

            ViewBag.Lines = lines;
            ViewBag.Sections = sections;
            ViewBag.Customers = customers;

            return View(invoice);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertFromQuotation(int quotationId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;
            var invoice = await _invoiceService.ConvertFromQuotationAsync(quotationId, userId);
            return RedirectToAction(nameof(Detail), new { id = invoice.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return Redirect($"/Quotation/Detail/{quotationId}");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransitionStatus(int invoiceId, int newStatusId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;
            await _invoiceService.TransitionStatusAsync(invoiceId, newStatusId, userId);
            return Json(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(int invoiceId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId)
    {
        try
        {
            var line = await _invoiceService.AddLineAsync(invoiceId, description, quantity,
                unitPrice, vatRate, discount, discountType, costPrice, referenceUrl, subtitle, invoiceSectionId);
            return Json(new { success = true, lineId = line.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateLine(int lineId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId)
    {
        try
        {
            await _invoiceService.UpdateLineAsync(lineId, description, quantity,
                unitPrice, vatRate, discount, discountType, costPrice, referenceUrl, subtitle, invoiceSectionId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLine(int lineId)
    {
        try
        {
            await _invoiceService.RemoveLineAsync(lineId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSection(int invoiceId, string name, string? description,
        string columnConfiguration, string sectionType, bool isEmphasized,
        string? accentColor, string? label, bool isTotalsTableShown)
    {
        try
        {
            await _sectionService.AddSectionAsync(invoiceId, name, description,
                columnConfiguration, sectionType, isEmphasized, accentColor, label, isTotalsTableShown);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSection(int sectionId, string name, string? description,
        string? notes, string? columnConfiguration, string? sectionType, bool? isEmphasized,
        string? accentColor, string? label, bool? isTotalsTableShown)
    {
        try
        {
            await _sectionService.UpdateSectionAsync(sectionId, name, description, notes,
                columnConfiguration, sectionType, isEmphasized, accentColor, label, isTotalsTableShown);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSection(int sectionId, int invoiceId)
    {
        try
        {
            await _sectionService.RemoveSectionAsync(sectionId, invoiceId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderSections(int invoiceId, List<int> orderedSectionIds)
    {
        await _sectionService.ReorderSectionsAsync(invoiceId, orderedSectionIds);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLineToSection(int lineId, int? targetSectionId)
    {
        await _sectionService.MoveLineToSectionAsync(lineId, targetSectionId);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderLines(List<int> orderedLineIds)
    {
        await _sectionService.ReorderLinesAsync(orderedLineIds);
        return Json(new { success = true });
    }
}
