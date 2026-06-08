using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private readonly ICheckoutService _checkoutService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICheckoutService checkoutService,
        ILogger<CheckoutController> logger)
    {
        _checkoutService = checkoutService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _checkoutService.CreateCheckoutSessionAsync(userId);

        if (result.Success)
        {
            return Redirect(result.CheckoutUrl!);
        }

        return result.FailureReason switch
        {
            CheckoutFailureReason.NoPendingRegistration => RedirectToAction("Register", "Account"),
            CheckoutFailureReason.AlreadyCompleted => RedirectToAction("Index", "Home"),
            _ => View("Error", result.ErrorMessage)
        };
    }

    [HttpGet]
    public IActionResult Success()
    {
        ViewData["Title"] = "Payment Successful";
        ViewData["Description"] = "Your payment was successful. We're setting up your account.";
        return View();
    }

    [HttpGet]
    public IActionResult Cancelled()
    {
        ViewData["Title"] = "Payment Cancelled";
        ViewData["Description"] = "Your payment was cancelled. You can try again when you're ready.";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { provisioned = false });
        }

        // Check if the user now has a business associated
        var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Portal.Infrastructure.Entities.Identity.ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);

        return Json(new { provisioned = user?.BusinessId != null });
    }
}
