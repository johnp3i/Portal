using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Repositories;

namespace Portal.Web.Controllers;

/// <summary>
/// Public controller for viewing shared payment receipts via token-based links.
/// No authentication required — tokens validate access.
/// </summary>
[AllowAnonymous]
public class ReceiptViewController : Controller
{
    private readonly PaymentReceiptShareRepository _shareRepository;
    private readonly PaymentReceiptRepository _receiptRepository;

    public ReceiptViewController(
        PaymentReceiptShareRepository shareRepository,
        PaymentReceiptRepository receiptRepository)
    {
        _shareRepository = shareRepository;
        _receiptRepository = receiptRepository;
    }

    [HttpGet]
    [Route("receipt/view/{token}")]
    public async Task<IActionResult> Index(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return View("NotFound");

            var share = await _shareRepository.GetByTokenAsync(token);
            if (share == null)
                return View("NotFound");

            if (share.ExpiresAtUtc < DateTimeOffset.UtcNow)
            {
                ViewBag.Expired = true;
                return View("Expired");
            }

            // Check if receipt is voided
            var receipt = await _receiptRepository.GetByIdAsync(share.PaymentReceiptId, share.BusinessId);
            if (receipt != null && receipt.IsVoided)
            {
                ViewBag.IsVoided = true;
            }

            ViewBag.SnapshotHtml = share.SnapshotHtml;
            ViewBag.ReceiptNumber = receipt?.ReceiptNumber ?? "Unknown";
            return View();
        }
        catch (Exception ex)
        {
            return View("NotFound");
        }
    }
}
