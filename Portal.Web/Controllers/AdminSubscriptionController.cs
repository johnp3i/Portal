using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Repositories;
using Portal.Web.Models;
using Portal.Web.Services.Billing;
using Portal.Web.Services.Stripe;
using Serilog;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("Admin/Subscriptions")]
public class AdminSubscriptionController : Controller
{
    private readonly PortalDbContext _portalDbContext;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly BillingInvoiceRepository _billingInvoiceRepository;
    private readonly BillingPaymentRepository _billingPaymentRepository;
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly IBillingService _billingService;

    private static readonly string[] ValidManualMethods = { "bank_transfer", "cheque", "cash", "other" };

    public AdminSubscriptionController(
        PortalDbContext portalDbContext,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        BillingInvoiceRepository billingInvoiceRepository,
        BillingPaymentRepository billingPaymentRepository,
        SubscriptionRepository subscriptionRepository,
        IBillingService billingService)
    {
        _portalDbContext = portalDbContext;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _billingInvoiceRepository = billingInvoiceRepository;
        _billingPaymentRepository = billingPaymentRepository;
        _subscriptionRepository = subscriptionRepository;
        _billingService = billingService;
    }

    // GET: /Admin/Subscriptions
    [HttpGet("")]
    public async Task<IActionResult> SubscriptionManagement()
    {
        try
        {
            var businesses = await _portalDbContext.Businesses
                .Select(b => new SubscriptionManagementItem
                {
                    BusinessId = b.Id,
                    BusinessName = b.Name,
                    IsActive = b.IsActive,
                    IsDemoAccount = b.IsDemoAccount
                })
                .ToListAsync();

            var businessPlans = await _portalDbContext.BusinessPlans
                .Include(bp => bp.Plan)
                .Where(bp => bp.IsActive)
                .ToListAsync();

            foreach (var business in businesses)
            {
                var bp = businessPlans.FirstOrDefault(x => x.BusinessId == business.BusinessId);
                if (bp != null)
                {
                    business.BusinessPlanId = bp.Id;
                    business.PlanName = bp.Plan.Name;
                    business.PlanAnnualPriceEur = bp.Plan.AnnualPriceEur;
                    business.Status = bp.Status;
                    business.StartedAtUtc = bp.StartDateUtc;
                    business.ExpiresAtUtc = bp.EndDateUtc;
                    business.TrialEndsAtUtc = bp.TrialEndsAtUtc;
                }
            }

            var availablePlans = await _portalDbContext.Plans
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new AvailablePlanItem
                {
                    PlanId = p.Id,
                    PlanName = p.Name
                })
                .ToListAsync();

            var viewModel = new SubscriptionManagementViewModel
            {
                Businesses = businesses,
                AvailablePlans = availablePlans
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading subscription management page");
            return View("Error");
        }
    }

    // POST: /Admin/Subscriptions/ChangePlan
    [HttpPost("ChangePlan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostChangeBusinessPlan([FromBody] ChangeBusinessPlanRequest request)
    {
        try
        {
            var businessPlan = await _portalDbContext.BusinessPlans
                .FirstOrDefaultAsync(bp => bp.BusinessId == request.BusinessId && bp.IsActive);

            if (businessPlan == null)
                return Json(new { success = false, message = "No active subscription found for this business." });

            var newPlan = await _portalDbContext.Plans
                .FirstOrDefaultAsync(p => p.Id == request.PlanId);

            if (newPlan == null)
                return Json(new { success = false, message = "The selected plan does not exist." });

            var oldPlanId = businessPlan.PlanId;
            businessPlan.PlanId = request.PlanId;

            // Sync [billing].[Subscription] to keep both tables aligned
            var subscription = await _portalDbContext.Database
                .SqlQueryRaw<int>("SELECT [Id] AS [Value] FROM [billing].[Subscription] WHERE [BusinessId] = @p0", request.BusinessId)
                .FirstOrDefaultAsync();

            if (subscription > 0)
            {
                await _portalDbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE [billing].[Subscription] SET [PlanId] = @p0 WHERE [BusinessId] = @p1",
                    request.PlanId, request.BusinessId);
            }

            await _portalDbContext.SaveChangesAsync();

            Log.Information("SuperAdmin changed business {BusinessId} plan from PlanId={OldPlanId} to PlanId={NewPlanId}",
                request.BusinessId, oldPlanId, request.PlanId);

            return Json(new { success = true, message = $"Plan changed to '{newPlan.Name}' successfully." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error changing plan for BusinessId={BusinessId}, PlanId={PlanId}",
                request.BusinessId, request.PlanId);
            return Json(new { success = false, message = "The plan could not be changed. Please try again." });
        }
    }

    // POST: /Admin/Subscriptions/ChangeStatus
    [HttpPost("ChangeStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostChangeSubscriptionStatus([FromBody] ChangeSubscriptionStatusRequest request)
    {
        try
        {
            var validStatuses = new[] { "active", "trial", "cancelled", "expired" };
            if (!validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = $"Invalid status '{request.Status}'. Valid values: active, trial, cancelled, expired." });

            var businessPlan = await _portalDbContext.BusinessPlans
                .FirstOrDefaultAsync(bp => bp.Id == request.BusinessPlanId);

            if (businessPlan == null)
                return Json(new { success = false, message = "Business subscription record not found." });

            var oldStatus = businessPlan.Status;
            businessPlan.Status = request.Status.ToLowerInvariant();

            // Sync [billing].[Subscription] status to keep both tables aligned
            await _portalDbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [billing].[Subscription] SET [Status] = @p0 WHERE [BusinessId] = @p1",
                request.Status.ToLowerInvariant(), businessPlan.BusinessId);

            await _portalDbContext.SaveChangesAsync();

            Log.Information("SuperAdmin changed BusinessPlan {BusinessPlanId} status from '{OldStatus}' to '{NewStatus}'",
                request.BusinessPlanId, oldStatus, request.Status);

            return Json(new { success = true, message = $"Subscription status changed to '{request.Status}' successfully." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error changing subscription status for BusinessPlanId={BusinessPlanId}, Status={Status}",
                request.BusinessPlanId, request.Status);
            return Json(new { success = false, message = "The status could not be changed. Please try again." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MANUAL PAYMENT RECORDING
    // ═══════════════════════════════════════════════════════════

    // POST: /Admin/Subscriptions/RecordPayment
    [HttpPost("RecordPayment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRecordPayment([FromBody] RecordManualPaymentRequest request)
    {
        try
        {
            // Validation
            if (request.InvoiceAmount <= 0)
                return Json(new { success = false, message = "Invoice amount must be greater than zero." });

            if (request.PaymentAmount <= 0)
                return Json(new { success = false, message = "Payment amount must be greater than zero." });

            if (request.PaymentAmount > request.InvoiceAmount)
                return Json(new { success = false, message = "Payment amount cannot exceed the invoice total." });

            if (request.PeriodEnd <= request.PeriodStart)
                return Json(new { success = false, message = "Period end must be after period start." });

            if (!ValidManualMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = $"Invalid payment method. Valid values: {string.Join(", ", ValidManualMethods)}." });

            // Verify subscription exists
            var subscription = await _subscriptionRepository.GetByBusinessIdAsync(request.BusinessId);
            if (subscription == null)
                return Json(new { success = false, message = "No subscription found for this business." });

            var now = DateTime.UtcNow;
            var isFullyPaid = request.PaymentAmount == request.InvoiceAmount;
            var invoiceStatus = isFullyPaid ? "paid" : "partially_paid";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Start transaction BEFORE GenerateNextAsync (it requires an active transaction)
            await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

            try
            {
                // Generate sequential invoice number
                var invoiceNumber = await _invoiceNumberGenerator.GenerateNextAsync(now);

                // Create billing invoice
                var invoiceId = await _billingInvoiceRepository.InsertAsync(new BillingInvoice
                {
                    BusinessId = request.BusinessId,
                    StripeInvoiceId = null,
                    AmountEur = request.InvoiceAmount,
                    PeriodStart = request.PeriodStart,
                    PeriodEnd = request.PeriodEnd,
                    Status = invoiceStatus,
                    PaidAtUtc = isFullyPaid ? now : null,
                    InvoiceNumber = invoiceNumber,
                    IsEmailSent = false,
                    CreatedAtUtc = now
                });

                // Create billing payment
                await _billingPaymentRepository.InsertAsync(new BillingPayment
                {
                    InvoiceId = invoiceId,
                    AmountEur = request.PaymentAmount,
                    Method = request.Method.ToLowerInvariant(),
                    PaidAtUtc = now,
                    StripePaymentIntentId = null,
                    Reference = request.Reference?.Trim(),
                    Notes = request.Notes?.Trim(),
                    RecordedByUserId = userId,
                    CreatedAtUtc = now
                });

                // Update subscription period and status
                await _subscriptionRepository.UpdatePeriodAsync(
                    subscription.Id,
                    request.PeriodStart,
                    request.PeriodEnd,
                    "active",
                    subscription.PlanId);

                // Update BusinessPlan if it exists (null-safe)
                var businessPlan = await _portalDbContext.BusinessPlans
                    .FirstOrDefaultAsync(bp => bp.BusinessId == request.BusinessId && bp.IsActive);

                if (businessPlan != null)
                {
                    businessPlan.Status = "active";
                    businessPlan.StartDateUtc = request.PeriodStart;
                    businessPlan.EndDateUtc = request.PeriodEnd;
                    await _portalDbContext.SaveChangesAsync();
                }
                else
                {
                    Log.Warning("No BusinessPlan found for BusinessId={BusinessId} during manual payment recording — subscription updated but BusinessPlan skipped.",
                        request.BusinessId);
                }

                await transaction.CommitAsync();

                Log.Information("SuperAdmin recorded manual payment for BusinessId={BusinessId}. InvoiceNumber={InvoiceNumber}, Amount={Amount}, Method={Method}, Status={Status}",
                    request.BusinessId, invoiceNumber, request.PaymentAmount, request.Method, invoiceStatus);

                return Json(new { success = true, message = $"Payment recorded. Invoice {invoiceNumber}. Subscription activated until {request.PeriodEnd:dd MMM yyyy}.", invoiceNumber });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error recording manual payment for BusinessId={BusinessId}", request.BusinessId);
            return Json(new { success = false, message = "Payment could not be recorded. Please try again." });
        }
    }

    // POST: /Admin/Subscriptions/AddPayment
    [HttpPost("AddPayment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAddPayment([FromBody] AddInstalmentPaymentRequest request)
    {
        try
        {
            if (request.PaymentAmount <= 0)
                return Json(new { success = false, message = "Payment amount must be greater than zero." });

            if (!ValidManualMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = $"Invalid payment method. Valid values: {string.Join(", ", ValidManualMethods)}." });

            // Load invoice (admin — no business scoping)
            var invoice = await _billingInvoiceRepository.GetByInvoiceIdAsync(request.InvoiceId);
            if (invoice == null)
                return Json(new { success = false, message = "Invoice not found." });

            // Verify invoice belongs to the specified business
            if (invoice.BusinessId != request.BusinessId)
                return Json(new { success = false, message = "Invoice does not belong to the specified business." });

            if (invoice.Status != "partially_paid")
                return Json(new { success = false, message = "This invoice is already fully paid or is not in a payable state." });

            // Calculate remaining balance
            var totalPaid = await _billingPaymentRepository.GetTotalPaidByInvoiceIdAsync(request.InvoiceId);
            var remaining = invoice.AmountEur - totalPaid;

            if (request.PaymentAmount > remaining)
                return Json(new { success = false, message = $"Payment amount exceeds the remaining balance of €{remaining:F2}." });

            var now = DateTime.UtcNow;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var newTotalPaid = totalPaid + request.PaymentAmount;
            var isNowFullyPaid = newTotalPaid == invoice.AmountEur;

            await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

            try
            {
                // Insert payment
                await _billingPaymentRepository.InsertAsync(new BillingPayment
                {
                    InvoiceId = request.InvoiceId,
                    AmountEur = request.PaymentAmount,
                    Method = request.Method.ToLowerInvariant(),
                    PaidAtUtc = now,
                    StripePaymentIntentId = null,
                    Reference = request.Reference?.Trim(),
                    Notes = request.Notes?.Trim(),
                    RecordedByUserId = userId,
                    CreatedAtUtc = now
                });

                // Update invoice status if fully paid
                if (isNowFullyPaid)
                {
                    await _billingInvoiceRepository.UpdateStatusAsync(request.InvoiceId, "paid", now);
                }

                await transaction.CommitAsync();

                var statusMessage = isNowFullyPaid
                    ? $"Payment added. Invoice {invoice.InvoiceNumber ?? $"#{request.InvoiceId}"} is now fully paid."
                    : $"Payment added. Remaining balance: €{(remaining - request.PaymentAmount):F2}.";

                Log.Information("SuperAdmin added instalment payment to InvoiceId={InvoiceId}. Amount={Amount}, TotalPaid={TotalPaid}, FullyPaid={FullyPaid}",
                    request.InvoiceId, request.PaymentAmount, newTotalPaid, isNowFullyPaid);

                return Json(new { success = true, message = statusMessage, isFullyPaid = isNowFullyPaid });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding instalment payment to InvoiceId={InvoiceId}", request.InvoiceId);
            return Json(new { success = false, message = "Payment could not be added. Please try again." });
        }
    }

    // GET: /Admin/Subscriptions/PaymentHistory/{businessId}
    [HttpGet("PaymentHistory/{businessId}")]
    public async Task<IActionResult> AxGetPaymentHistory(int businessId)
    {
        try
        {
            // Get all invoices for this business
            var (invoices, _) = await _billingInvoiceRepository.GetByBusinessIdPagedAsync(businessId, 1, 100);

            // Get all payments for each invoice and build hierarchy
            var invoiceData = new List<object>();
            decimal totalRevenue = 0;
            decimal totalOutstanding = 0;

            foreach (var inv in invoices)
            {
                var payments = await _billingPaymentRepository.GetByInvoiceIdAsync(inv.Id);
                var amountPaid = payments.Sum(p => p.AmountEur);
                var outstanding = inv.AmountEur - amountPaid;

                totalRevenue += amountPaid;
                totalOutstanding += outstanding > 0 ? outstanding : 0;

                invoiceData.Add(new
                {
                    invoiceId = inv.Id,
                    invoiceNumber = inv.InvoiceNumber ?? $"INV-{inv.Id:D6}",
                    amountDue = inv.AmountEur,
                    amountPaid,
                    outstanding = outstanding > 0 ? outstanding : 0,
                    status = inv.Status,
                    periodStart = inv.PeriodStart,
                    periodEnd = inv.PeriodEnd,
                    createdAtUtc = inv.CreatedAtUtc,
                    payments = payments.Select(p => new
                    {
                        id = p.Id,
                        amount = p.AmountEur,
                        method = p.Method,
                        paidAtUtc = p.PaidAtUtc,
                        reference = p.Reference,
                        notes = p.Notes,
                        isStripe = !string.IsNullOrEmpty(p.StripePaymentIntentId)
                    })
                });
            }

            return Json(new
            {
                success = true,
                summary = new
                {
                    totalRevenue,
                    invoiceCount = invoices.Count,
                    outstanding = totalOutstanding
                },
                data = invoiceData
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading payment history for BusinessId={BusinessId}", businessId);
            return Json(new { success = false, message = "Failed to load payment history." });
        }
    }

    // GET: /Admin/Subscriptions/DownloadInvoice/{invoiceId}
    [HttpGet("DownloadInvoice/{invoiceId}")]
    public async Task<IActionResult> AxGetDownloadInvoice(int invoiceId, int businessId)
    {
        try
        {
            var pdfBytes = await _billingService.GenerateInvoicePdfAsync(invoiceId, businessId);
            return File(pdfBytes, "application/pdf", $"Invoice-{invoiceId}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "Invoice {InvoiceId} not found for BusinessId {BusinessId} (admin download)", invoiceId, businessId);
            return Json(new { success = false, message = "Invoice not found." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating PDF for InvoiceId={InvoiceId}, BusinessId={BusinessId} (admin download)", invoiceId, businessId);
            return Json(new { success = false, message = "Failed to generate invoice PDF." });
        }
    }
}

/// <summary>
/// View model for the subscription management page.
/// </summary>
public class SubscriptionManagementViewModel
{
    public List<SubscriptionManagementItem> Businesses { get; set; } = new();

    public List<AvailablePlanItem> AvailablePlans { get; set; } = new();
}

/// <summary>
/// A single business row in the subscription management table.
/// </summary>
public class SubscriptionManagementItem
{
    public int BusinessId { get; set; }

    public string BusinessName { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsDemoAccount { get; set; }

    public int? BusinessPlanId { get; set; }

    public string? PlanName { get; set; }

    /// <summary>
    /// Plan annual price for pre-populating the Record Payment modal.
    /// </summary>
    public decimal? PlanAnnualPriceEur { get; set; }

    public string? Status { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? TrialEndsAtUtc { get; set; }
}

/// <summary>
/// A plan option available for assignment.
/// </summary>
public class AvailablePlanItem
{
    public int PlanId { get; set; }

    public string PlanName { get; set; } = null!;
}

/// <summary>
/// Request model for recording a manual payment (first payment — creates new invoice).
/// </summary>
public class RecordManualPaymentRequest
{
    public int BusinessId { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public string Method { get; set; } = null!;
    public string? Reference { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for adding an instalment payment to an existing invoice.
/// </summary>
public class AddInstalmentPaymentRequest
{
    public int InvoiceId { get; set; }
    public int BusinessId { get; set; }
    public decimal PaymentAmount { get; set; }
    public string Method { get; set; } = null!;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
