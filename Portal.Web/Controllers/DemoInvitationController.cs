using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("Admin/DemoInvitations")]
public class DemoInvitationController : Controller
{
    private readonly IDemoInvitationService _demoInvitationService;

    public DemoInvitationController(IDemoInvitationService demoInvitationService)
    {
        _demoInvitationService = demoInvitationService;
    }

    // GET /Admin/DemoInvitations
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1)
    {
        var pagedResult = await _demoInvitationService.GetAllPagedAsync(page, 10);
        return View(pagedResult);
    }

    // GET /Admin/DemoInvitations/Create
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var businesses = await _demoInvitationService.GetDemoBusinessesAsync();
        ViewBag.DemoBusinesses = businesses;
        return View();
    }

    // POST /Admin/DemoInvitations/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateDemoInvitationRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _demoInvitationService.CreateAsync(request, userId);

            if (result.IsEmailSent)
            {
                return Json(new { success = true, message = $"Invitation sent to {request.RecipientEmail}" });
            }
            else
            {
                return Json(new { success = true, warning = true, message = $"Invitation created but email delivery failed. Use Resend to retry." });
            }
        }
        catch (ValidationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create demo invitation for RecipientEmail={RecipientEmail}",
                request.RecipientEmail);
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // POST /Admin/DemoInvitations/Revoke
    [HttpPost("Revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke([FromBody] RevokeRequest request)
    {
        try
        {
            await _demoInvitationService.RevokeAsync(request.InvitationId);
            return Json(new { success = true, message = "Invitation revoked successfully." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to revoke demo invitation InvitationId={InvitationId}",
                request.InvitationId);
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // POST /Admin/DemoInvitations/Resend
    [HttpPost("Resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend([FromBody] ResendRequest request)
    {
        try
        {
            await _demoInvitationService.ResendEmailAsync(request.InvitationId);
            return Json(new { success = true, message = "Invitation email resent successfully." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to resend demo invitation email InvitationId={InvitationId}",
                request.InvitationId);
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // GET /Admin/DemoInvitations/Permissions/{id}
    [HttpGet("Permissions/{id}")]
    public async Task<IActionResult> GetPermissions(int id)
    {
        try
        {
            var permissions = await _demoInvitationService.GetPermissionsForInvitationAsync(id);
            return Json(new { success = true, permissions });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get permissions for InvitationId={InvitationId}", id);
            return Json(new { success = false, message = "Failed to load permissions." });
        }
    }

    // POST /Admin/DemoInvitations/Permissions
    [HttpPost("Permissions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePermissions([FromBody] UpdatePermissionsRequest request)
    {
        try
        {
            await _demoInvitationService.UpdatePermissionsAsync(request.InvitationId, request.Permissions);
            return Json(new { success = true, message = "Permissions updated successfully." });
        }
        catch (ValidationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update permissions for InvitationId={InvitationId}", request.InvitationId);
            return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // GET /Admin/DemoInvitations/SearchContacts
    [HttpGet("SearchContacts")]
    public async Task<IActionResult> AxGetSearchContacts(string? search)
    {
        try
        {
            var contacts = await _demoInvitationService.SearchSalesContactsAsync(search);
            return Json(new { success = true, data = contacts });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to search contacts");
            return Json(new { success = false, message = "Failed to load contacts." });
        }
    }
}
