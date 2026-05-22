using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IQuotationService _quotationService;
    private readonly ICustomerService _customerService;
    private readonly IBusinessService _businessService;
    private readonly ICurrentTenantService _tenantService;

    public HomeController(IQuotationService quotationService, ICustomerService customerService, IBusinessService businessService, ICurrentTenantService tenantService)
    {
        _quotationService = quotationService;
        _customerService = customerService;
        _businessService = businessService;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var allQuotations = await _quotationService.GetQuotationsAsync();
        var customers = await _customerService.GetCustomersAsync(null, true);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        var draftCount = allQuotations.Count(q => q.QuotationStatusTypeId == 1);
        var sentThisMonth = allQuotations.Where(q => q.QuotationStatusTypeId == 2).ToList();
        var acceptedCount = allQuotations.Count(q => q.QuotationStatusTypeId == 3);
        var totalSent = allQuotations.Count(q => q.QuotationStatusTypeId >= 2);
        var activeCustomerCount = customers.Count;

        var sentThisMonthValue = sentThisMonth.Sum(q => q.TotalAmount);
        var acceptanceRate = totalSent > 0 ? Math.Round((decimal)acceptedCount / totalSent * 100, 0) : 0;

        ViewBag.DraftCount = draftCount;
        ViewBag.SentThisMonthCount = sentThisMonth.Count;
        ViewBag.SentThisMonthValue = sentThisMonthValue;
        ViewBag.AcceptedCount = acceptedCount;
        ViewBag.AcceptanceRate = acceptanceRate;
        ViewBag.ActiveCustomerCount = activeCustomerCount;

        // Recent quotations for the table
        ViewBag.RecentQuotations = allQuotations.Take(5).ToList();

        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        return View();
    }

    [AllowAnonymous]
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        return View();
    }
}
