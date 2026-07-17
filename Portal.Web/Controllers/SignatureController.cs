using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// Manages digital signatures for receipts and documents.
/// Upload/edit/deactivate requires owner or SuperAdmin role (signature_manage permission).
/// Viewing and selecting signatures for receipts is available to all authenticated users (signature_use).
/// </summary>
[Authorize]
public class SignatureController : Controller
{
    private readonly ISignatureService _signatureService;
    private readonly ICurrentTenantService _tenantService;

    public SignatureController(
        ISignatureService signatureService,
        ICurrentTenantService tenantService)
    {
        _signatureService = signatureService;
        _tenantService = tenantService;
    }

    private bool CanManageSignatures()
    {
        return User.IsInRole("SuperAdmin") || User.HasClaim("IsOwner", "true");
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> AxGetList()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var signatures = await _signatureService.GetAllForBusinessAsync(businessId);
            return Json(new { success = true, data = signatures });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load signatures." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetListAll()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var signatures = await _signatureService.GetAllIncludingInactiveAsync(businessId);
            return Json(new { success = true, data = signatures });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load signatures." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpload(IFormFile file, string label, string? position)
    {
        try
        {
            if (!CanManageSignatures())
                return Json(new { success = false, message = "Only the business owner can manage signatures." });

            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            if (file.Length > 2 * 1024 * 1024)
                return Json(new { success = false, message = "File size must not exceed 2 MB." });

            using var stream = file.OpenReadStream();
            var result = await _signatureService.UploadAsync(
                businessId, userId, label, position, file.FileName, file.ContentType, stream);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Signature uploaded.", data = result.Data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to upload signature." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetDefault(int id)
    {
        try
        {
            if (!CanManageSignatures())
                return Json(new { success = false, message = "Only the business owner can manage signatures." });

            var businessId = _tenantService.CurrentBusinessId;
            var result = await _signatureService.SetDefaultAsync(id, businessId);
            return Json(new { success = result.Success, message = result.Success ? "Default signature updated." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to set default signature." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivate(int id)
    {
        try
        {
            if (!CanManageSignatures())
                return Json(new { success = false, message = "Only the business owner can manage signatures." });

            var businessId = _tenantService.CurrentBusinessId;
            var result = await _signatureService.DeactivateAsync(id, businessId);
            return Json(new { success = result.Success, message = result.Success ? "Signature deactivated." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to deactivate signature." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostReactivate(int id)
    {
        try
        {
            if (!CanManageSignatures())
                return Json(new { success = false, message = "Only the business owner can manage signatures." });

            var businessId = _tenantService.CurrentBusinessId;
            var result = await _signatureService.ReactivateAsync(id, businessId);
            return Json(new { success = result.Success, message = result.Success ? "Signature reactivated." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to reactivate signature." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateLabel(int id, string label, string? position)
    {
        try
        {
            if (!CanManageSignatures())
                return Json(new { success = false, message = "Only the business owner can manage signatures." });

            var businessId = _tenantService.CurrentBusinessId;
            var result = await _signatureService.UpdateLabelAsync(id, businessId, label, position);
            return Json(new { success = result.Success, message = result.Success ? "Signature updated." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update signature." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetImage(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var stream = await _signatureService.GetImageStreamAsync(id, businessId);
            if (stream == null) return NotFound();

            // Determine content type from the signature record
            var signatures = await _signatureService.GetAllForBusinessAsync(businessId);
            var sig = signatures.FirstOrDefault(s => s.Id == id);
            var contentType = sig?.ContentType ?? "image/png";

            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            return NotFound();
        }
    }
}
