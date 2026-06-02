using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Web.Models.PromoCode;
using Portal.Web.Services;
using Serilog;
using System.Security.Claims;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("Admin/PromoCodes")]
public class PromoCodeController : Controller
{
    private readonly IPromoCodeService _promoCodeService;
    private readonly IPromoEmailService _promoEmailService;

    public PromoCodeController(
        IPromoCodeService promoCodeService,
        IPromoEmailService promoEmailService)
    {
        _promoCodeService = promoCodeService;
        _promoEmailService = promoEmailService;
    }

    // GET /Admin/PromoCodes
    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        var filter = new PromoCodeFilter
        {
            Status = status,
            Page = page,
            PageSize = 20
        };

        var pagedResult = await _promoCodeService.GetAllAsync(filter);

        ViewBag.StatusFilter = status;

        return View(pagedResult);
    }

    // POST /Admin/PromoCodes/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CreatePromoCodeRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _promoCodeService.CreateAsync(request, userId);

            if (result.Success)
            {
                return Json(new { success = true, code = result.Code, message = $"Promo code '{result.Code}' created successfully." });
            }

            return Json(new { success = false, message = result.ErrorMessage ?? "Failed to create promo code." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating promo code for UserId={UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // POST /Admin/PromoCodes/Revoke
    [HttpPost("Revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke([FromForm] int promoCodeId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _promoCodeService.RevokeAsync(promoCodeId, userId);

            if (result.Success)
            {
                return Json(new { success = true, message = "Promo code revoked successfully." });
            }

            return Json(new { success = false, message = result.Message ?? "Failed to revoke promo code." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error revoking promo code PromoCodeId={PromoCodeId}", promoCodeId);
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // POST /Admin/PromoCodes/SendCode
    [HttpPost("SendCode")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCode([FromForm] int promoCodeId, [FromForm] string? recipientEmail)
    {
        try
        {
            var promoCode = await _promoCodeService.GetByIdAsync(promoCodeId);
            if (promoCode == null)
            {
                return Json(new { success = false, message = "Promo code not found." });
            }

            if (promoCode.Status != "Active")
            {
                return Json(new { success = false, message = "Only active promo codes can be sent." });
            }

            // Determine recipient: BoundEmail for email-bound codes, or provided email for generic codes
            var emailTo = promoCode.BoundEmail ?? recipientEmail;

            if (string.IsNullOrWhiteSpace(emailTo))
            {
                return Json(new { success = false, message = "A recipient email address is required for generic codes." });
            }

            var sent = await _promoEmailService.SendPromoCodeEmailAsync(
                emailTo,
                promoCode.Code,
                promoCode.DurationMonths,
                promoCode.ExpiresAtUtc,
                promoCodeId);

            if (sent)
            {
                return Json(new { success = true, message = $"Promo code email sent to {emailTo}." });
            }

            return Json(new { success = false, message = "Email could not be sent. Please try again." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending promo code email for PromoCodeId={PromoCodeId}", promoCodeId);
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }
}
