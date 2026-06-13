using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Quotation)]
public class QuotationController : Controller
{
    private readonly IQuotationService _quotationService;
    private readonly ICustomerService _customerService;
    private readonly IProposalService _proposalService;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;
    private readonly QuotationContactRepository _contactRepository;
    private readonly IProposalSectionService _sectionService;
    private readonly IInvoiceService _invoiceService;
    private readonly IBusinessService _businessService;
    private readonly IDocumentDuplicationService _duplicationService;
    private readonly IDocumentSoftDeleteService _softDeleteService;
    private readonly ProductRepository _productRepository;
    private readonly IProposalAcceptanceService _acceptanceService;

    public QuotationController(
        IQuotationService quotationService,
        ICustomerService customerService,
        IProposalService proposalService,
        ILogoService logoService,
        ICurrentTenantService tenantService,
        QuotationContactRepository contactRepository,
        IProposalSectionService sectionService,
        IInvoiceService invoiceService,
        IBusinessService businessService,
        IDocumentDuplicationService duplicationService,
        IDocumentSoftDeleteService softDeleteService,
        ProductRepository productRepository,
        IProposalAcceptanceService acceptanceService)
    {
        _quotationService = quotationService;
        _customerService = customerService;
        _proposalService = proposalService;
        _logoService = logoService;
        _tenantService = tenantService;
        _contactRepository = contactRepository;
        _sectionService = sectionService;
        _invoiceService = invoiceService;
        _businessService = businessService;
        _duplicationService = duplicationService;
        _softDeleteService = softDeleteService;
        _productRepository = productRepository;
        _acceptanceService = acceptanceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? status, int? customer, DateTime? dateFrom, DateTime? dateTo, string? search, int page = 1)
    {
        var pagedQuotations = await _quotationService.GetQuotationsPagedAsync(status, customer, dateFrom, dateTo, search, page);
        var customers = await _customerService.GetCustomersAsync(null, true);
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);

        // Load acceptance status for quotations that have active shares
        var quotationIds = pagedQuotations.Items.Select(q => q.Id).ToList();
        var activeShares = new Dictionary<int, int>(); // quotationId → shareId
        foreach (var quotationId in quotationIds)
        {
            var share = await _proposalService.GetActiveShareByQuotationIdAsync(quotationId);
            if (share != null)
                activeShares[quotationId] = share.Id;
        }

        if (activeShares.Count > 0)
        {
            var acceptedShareIds = await _acceptanceService.GetAcceptedShareIdsAsync(activeShares.Values);
            foreach (var item in pagedQuotations.Items)
            {
                if (activeShares.TryGetValue(item.Id, out var shareId))
                {
                    item.AcceptanceStatus = acceptedShareIds.Contains(shareId) ? "accepted" : "awaiting";
                }
            }
        }

        var viewModel = new QuotationListViewModel
        {
            PagedQuotations = pagedQuotations,
            Quotations = pagedQuotations.Items,
            StatusFilter = status,
            CustomerFilter = customer,
            DateFrom = dateFrom,
            DateTo = dateTo,
            SearchTerm = search,
            Customers = customers,
            Statuses = new List<QuotationStatusType>
            {
                new() { Id = 1, Name = "Draft" },
                new() { Id = 2, Name = "Sent" },
                new() { Id = 3, Name = "Accepted" },
                new() { Id = 4, Name = "Converted" },
                new() { Id = 5, Name = "Archived" }
            }
        };

        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var customers = await _customerService.GetCustomersAsync(null, true);
        var viewModel = new QuotationCreateViewModel
        {
            Customers = customers
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Create(QuotationCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Customers = await _customerService.GetCustomersAsync(null, true);
            return View(model);
        }

        try
        {
            var quotation = await _quotationService.CreateQuotationAsync(model.CustomerId, model.ValidUntil, model.Notes);
            return RedirectToAction(nameof(Edit), new { id = quotation.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Customers = await _customerService.GetCustomersAsync(null, true);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var quotation = await _quotationService.GetQuotationByIdAsync(id);
        if (quotation == null) return NotFound();

        if (quotation.QuotationStatusTypeId != 1 && quotation.QuotationStatusTypeId != 2)
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        var lines = await _quotationService.GetQuotationLinesAsync(id);
        var customers = await _customerService.GetCustomersAsync(null, true);
        var contacts = await _contactRepository.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        var sections = await _sectionService.GetByQuotationIdAsync(id);

        // Build DisplayLines with derived ProductTypeName from linked products
        var displayLines = await BuildDisplayLinesAsync(lines);

        var viewModel = new QuotationEditViewModel
        {
            Id = quotation.Id,
            Reference = quotation.Reference,
            CustomerId = quotation.CustomerId,
            ValidUntil = quotation.ValidUntil,
            Notes = quotation.Notes,
            QuotationContactId = quotation.QuotationContactId,
            IsGrandTotalShown = quotation.IsGrandTotalShown,
            Lines = lines,
            DisplayLines = displayLines,
            Sections = sections,
            Subtotal = quotation.Subtotal,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.TotalAmount,
            Customers = customers,
            Contacts = contacts
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Edit(int id, QuotationEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var quotation = await _quotationService.GetQuotationByIdAsync(id);
            if (quotation == null) return NotFound();

            model.Id = id;
            model.Reference = quotation.Reference;
            model.Lines = await _quotationService.GetQuotationLinesAsync(id);
            model.DisplayLines = await BuildDisplayLinesAsync(model.Lines);
            model.Sections = await _sectionService.GetByQuotationIdAsync(id);
            model.Subtotal = quotation.Subtotal;
            model.TaxAmount = quotation.TaxAmount;
            model.TotalAmount = quotation.TotalAmount;
            model.Customers = await _customerService.GetCustomersAsync(null, true);
            return View(model);
        }

        try
        {
            await _quotationService.UpdateQuotationAsync(id, model.CustomerId, model.ValidUntil, model.Notes, model.QuotationContactId, model.IsGrandTotalShown, model.Reference);
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var q = await _quotationService.GetQuotationByIdAsync(id);
            model.Id = id;
            model.Reference = q?.Reference ?? string.Empty;
            model.Lines = await _quotationService.GetQuotationLinesAsync(id);
            model.DisplayLines = await BuildDisplayLinesAsync(model.Lines);
            model.Sections = await _sectionService.GetByQuotationIdAsync(id);
            model.Subtotal = q?.Subtotal ?? 0;
            model.TaxAmount = q?.TaxAmount ?? 0;
            model.TotalAmount = q?.TotalAmount ?? 0;
            model.Customers = await _customerService.GetCustomersAsync(null, true);
            return View(model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var q = await _quotationService.GetQuotationByIdAsync(id);
            model.Id = id;
            model.Reference = q?.Reference ?? string.Empty;
            model.Lines = await _quotationService.GetQuotationLinesAsync(id);
            model.DisplayLines = await BuildDisplayLinesAsync(model.Lines);
            model.Sections = await _sectionService.GetByQuotationIdAsync(id);
            model.Subtotal = q?.Subtotal ?? 0;
            model.TaxAmount = q?.TaxAmount ?? 0;
            model.TotalAmount = q?.TotalAmount ?? 0;
            model.Customers = await _customerService.GetCustomersAsync(null, true);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var quotation = await _quotationService.GetQuotationByIdAsync(id);
        if (quotation == null) return NotFound();

        var lines = await _quotationService.GetQuotationLinesAsync(id);
        var customer = await _customerService.GetCustomerByIdAsync(quotation.CustomerId);

        var transitions = _quotationService.GetValidTransitions();
        var availableTransitions = transitions.TryGetValue(quotation.QuotationStatusTypeId, out var targets)
            ? targets
            : new List<int>();

        var statusNames = new Dictionary<int, string>
        {
            { 1, "Draft" }, { 2, "Sent" }, { 3, "Accepted" }, { 4, "Converted" }, { 5, "Archived" }
        };

        // Share dialog data
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        var sections = await _sectionService.GetByQuotationIdAsync(id);
        ViewBag.Logos = logos;
        ViewBag.Sections = sections;
        ViewBag.QuotationId = id;
        ViewBag.DefaultExpiration = DateTimeOffset.UtcNow.AddDays(3);
        ViewBag.CustomerEmail = customer?.Email ?? "";

        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        var viewModel = new QuotationDetailViewModel
        {
            Quotation = quotation,
            Lines = lines,
            CustomerName = customer?.Name ?? "Unknown",
            StatusName = statusNames.GetValueOrDefault(quotation.QuotationStatusTypeId, "Unknown"),
            IsExpired = _quotationService.IsExpired(quotation),
            AvailableTransitions = availableTransitions
        };

        // Acceptance status
        var activeShare = await _proposalService.GetActiveShareByQuotationIdAsync(id);
        if (activeShare != null)
        {
            var acceptance = await _acceptanceService.GetByProposalShareIdAsync(activeShare.Id);
            if (acceptance != null)
            {
                ViewBag.AcceptanceStatus = "accepted";
                ViewBag.AcceptedAtUtc = acceptance.AcceptedAtUtc;
            }
            else
            {
                ViewBag.AcceptanceStatus = "awaiting";
            }
        }
        else
        {
            ViewBag.AcceptanceStatus = null;
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> TransitionStatus(int id, int newStatusId)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            await _quotationService.TransitionStatusAsync(id, newStatusId, userId);
        }
        catch (InvalidOperationException)
        {
            // Redirect back to detail — error will be visible from status not changing
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> ConvertToInvoice(int quotationId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;
            var newInvoice = await _invoiceService.ConvertFromQuotationAsync(quotationId, userId);
            return Redirect($"/Invoice/Detail/{newInvoice.Id}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id = quotationId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> CreateContact(int quotationId, string contactName, string? contactEmail, string? contactPhone)
    {
        if (string.IsNullOrWhiteSpace(contactName))
        {
            TempData["LineError"] = "Contact name is required.";
            return RedirectToAction(nameof(Edit), new { id = quotationId });
        }

        var contact = new Portal.Infrastructure.Entities.QuotationContact
        {
            BusinessId = _tenantService.CurrentBusinessId,
            Name = contactName.Trim(),
            Email = contactEmail?.Trim(),
            TelephoneNumber = contactPhone?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _contactRepository.InsertAsync(contact);
        TempData["Success"] = $"Contact '{contactName}' added.";

        return RedirectToAction(nameof(Edit), new { id = quotationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> AddLine(int quotationId, QuotationLineFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return Json(new { success = false, message = "Validation failed" });

            return RedirectToAction(nameof(Edit), new { id = quotationId });
        }

        try
        {
            await _quotationService.AddLineAsync(quotationId, model.Description, model.Quantity, model.UnitPrice, model.VatRate, model.ReferenceUrl, model.Discount, model.DiscountType, model.Subtitle, costPrice: model.CostPrice, productCode: model.ProductCode, isReverseCharge: model.IsReverseCharge, proposalSectionId: model.ProposalSectionId);
        }
        catch (ArgumentException ex)
        {
            if (IsAjaxRequest())
                return Json(new { success = false, message = ex.Message });

            TempData["LineError"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = quotationId });
        }
        catch (InvalidOperationException ex)
        {
            if (IsAjaxRequest())
                return Json(new { success = false, message = ex.Message });

            TempData["LineError"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = quotationId });
        }

        if (IsAjaxRequest())
            return Json(new { success = true });

        return RedirectToAction(nameof(Edit), new { id = quotationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> UpdateLine(int quotationId, int lineId, QuotationLineFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return Json(new { success = false, message = "Validation failed" });

            return RedirectToAction(nameof(Edit), new { id = quotationId });
        }

        try
        {
            await _quotationService.UpdateLineAsync(lineId, model.Description, model.Quantity, model.UnitPrice, model.VatRate, model.ReferenceUrl, model.Discount, model.DiscountType, model.Subtitle, costPrice: model.CostPrice, isReverseCharge: model.IsReverseCharge);
        }
        catch (ArgumentException ex)
        {
            if (IsAjaxRequest())
                return Json(new { success = false, message = ex.Message });

            TempData["LineError"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = quotationId });
        }
        catch (InvalidOperationException ex)
        {
            if (IsAjaxRequest())
                return Json(new { success = false, message = ex.Message });

            TempData["LineError"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = quotationId });
        }

        if (IsAjaxRequest())
            return Json(new { success = true });

        return RedirectToAction(nameof(Edit), new { id = quotationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> RemoveLine(int quotationId, int lineId)
    {
        try
        {
            await _quotationService.RemoveLineAsync(lineId);
        }
        catch (InvalidOperationException ex)
        {
            TempData["LineError"] = ex.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = quotationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Preview(int id, List<int> heroLogoIds, int? metaLogoId)
    {
        try
        {
            // POST: use form-selected logos
            var html = await _proposalService.PreviewAsync(id, heroLogoIds ?? new List<int>(), metaLogoId);
            return Content(html, "text/html");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }
    }

    [HttpGet]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Preview(int id)
    {
        try
        {
            // GET: default to primary logo as hero and meta
            var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
            var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary);
            var heroLogoIds = primaryLogo != null ? new List<int> { primaryLogo.Id } : new List<int>();

            var html = await _proposalService.PreviewAsync(id, heroLogoIds, primaryLogo?.Id);
            return Content(html, "text/html");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }
    }

    [HttpGet]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> ShareDialog(int id)
    {
        var quotation = await _quotationService.GetQuotationByIdAsync(id);
        if (quotation == null) return NotFound();

        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        ViewBag.Logos = logos;
        ViewBag.QuotationId = id;
        ViewBag.DefaultExpiration = DateTimeOffset.UtcNow.AddDays(3);

        return PartialView("_ShareDialog");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Share(int id, ShareProposalViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid share configuration." });
        }

        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var share = await _proposalService.ShareAsync(id, model.ExpiresAtUtc, model.HeroLogoIds, model.MetaLogoId, userId, model.RecipientEmail, model.SendEmail);
            var shareUrl = $"/proposal/{share.ShareToken}";
            return Json(new { success = true, shareUrl, token = share.ShareToken, expiresAt = share.ExpiresAtUtc });
        }
        catch (ArgumentException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> CopyShareLink(int id)
    {
        var share = await _proposalService.GetActiveShareByQuotationIdAsync(id);
        if (share == null)
        {
            return Json(new { success = false, message = "No active share link found." });
        }

        var url = $"{Request.Scheme}://{Request.Host}/proposal/{share.ShareToken}";
        return Json(new { success = true, url });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Duplicate(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var duplicate = await _duplicationService.DuplicateQuotationAsync(id, userId);
            return Json(new { success = true, redirectUrl = Url.Action("Details", new { id = duplicate.Id }) });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> SoftDelete(int id)
    {
        try
        {
            var result = await _softDeleteService.SoftDeleteQuotationAsync(id);

            if (result.Success)
                return Json(new { success = true, message = "Quotation deleted successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while deleting the quotation." });
        }
    }

    private async Task<List<QuotationLineDisplayViewModel>> BuildDisplayLinesAsync(List<QuotationLine> lines)
    {
        var businessId = _tenantService.CurrentBusinessId;
        var displayLines = new List<QuotationLineDisplayViewModel>();

        var productCodes = lines
            .Where(l => !string.IsNullOrEmpty(l.ProductCode))
            .Select(l => l.ProductCode!)
            .Distinct()
            .ToList();

        var productLookup = new Dictionary<string, Product?>();
        foreach (var code in productCodes)
        {
            var product = await _productRepository.GetByProductCodeAndBusinessIdAsync(code, businessId);
            productLookup[code] = product;
        }

        foreach (var line in lines)
        {
            string? productTypeName = null;

            if (!string.IsNullOrEmpty(line.ProductCode) && productLookup.TryGetValue(line.ProductCode, out var product))
            {
                productTypeName = product?.ProductTypeId switch
                {
                    1 => "Services",
                    2 => "Goods",
                    _ => null
                };
            }

            displayLines.Add(new QuotationLineDisplayViewModel
            {
                Line = line,
                ProductTypeName = productTypeName
            });
        }

        return displayLines;
    }

    private bool IsAjaxRequest()
        => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
}
