using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for purchase management including VAT calculation, origin type handling,
/// and bulk operations with transactional support.
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PurchaseRepository _purchaseRepository;
    private readonly SupplierRepository _supplierRepository;
    private readonly ExpenseCategoryRepository _expenseCategoryRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly PortalDbContext _portalDbContext;

    public PurchaseService(
        ICurrentTenantService currentTenantService,
        PurchaseRepository purchaseRepository,
        SupplierRepository supplierRepository,
        ExpenseCategoryRepository expenseCategoryRepository,
        AuditLogRepository auditLogRepository,
        PortalDbContext portalDbContext)
    {
        _currentTenantService = currentTenantService;
        _purchaseRepository = purchaseRepository;
        _supplierRepository = supplierRepository;
        _expenseCategoryRepository = expenseCategoryRepository;
        _auditLogRepository = auditLogRepository;
        _portalDbContext = portalDbContext;
    }

    public async Task<List<Purchase>> GetPurchasesAsync()
    {
        return await _purchaseRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
    }

    public async Task<List<Purchase>> GetFilteredPurchasesAsync(
        int? supplierId,
        int? expenseCategoryId,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        return await _purchaseRepository.GetFilteredAsync(
            _currentTenantService.CurrentBusinessId,
            supplierId,
            expenseCategoryId,
            dateFrom,
            dateTo);
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
        return await _purchaseRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<ServiceResult> CreatePurchaseAsync(Purchase purchase)
    {
        var validationResult = await ValidatePurchaseAsync(purchase);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        ApplyOriginTypeLogic(purchase);

        purchase.BusinessId = _currentTenantService.CurrentBusinessId;
        purchase.CreatedAtUtc = DateTime.UtcNow;
        purchase.UpdatedAtUtc = DateTime.UtcNow;

        // Auto-assign VAT submission period
        await AssignVatPeriodAsync(purchase);

        await _purchaseRepository.InsertAsync(purchase);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Create",
            TableName = "purchase.Purchase",
            RecordId = purchase.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdatePurchaseAsync(Purchase purchase)
    {
        var existing = await _purchaseRepository.GetByIdAndBusinessIdAsync(purchase.Id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Purchase not found.");
        }

        var validationResult = await ValidatePurchaseAsync(purchase);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        ApplyOriginTypeLogic(purchase);

        purchase.BusinessId = _currentTenantService.CurrentBusinessId;
        purchase.UpdatedAtUtc = DateTime.UtcNow;

        await _purchaseRepository.UpdateAsync(purchase);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Update",
            TableName = "purchase.Purchase",
            RecordId = purchase.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelPurchaseAsync(int id)
    {
        var existing = await _purchaseRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Purchase not found.");
        }

        if (existing.IsCancelled)
        {
            return ServiceResult.Fail("This purchase has already been cancelled.");
        }

        await _purchaseRepository.CancelAsync(id, _currentTenantService.CurrentBusinessId);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Cancel",
            TableName = "purchase.Purchase",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> BulkCreatePurchasesAsync(List<Purchase> purchases)
    {
        var errors = new List<string>();

        for (int i = 0; i < purchases.Count; i++)
        {
            var rowNumber = i + 1;
            var purchase = purchases[i];

            var validationResult = await ValidatePurchaseAsync(purchase);
            if (!validationResult.Success)
            {
                errors.Add($"Row {rowNumber}: {validationResult.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail($"Batch contains validation errors. No records were saved. {string.Join(" ", errors)}");
        }

        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            foreach (var purchase in purchases)
            {
                ApplyOriginTypeLogic(purchase);

                purchase.BusinessId = _currentTenantService.CurrentBusinessId;
                purchase.CreatedAtUtc = DateTime.UtcNow;
                purchase.UpdatedAtUtc = DateTime.UtcNow;

                await _purchaseRepository.InsertAsync(purchase);

                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = _currentTenantService.CurrentBusinessId,
                    Action = "Create",
                    TableName = "purchase.Purchase",
                    RecordId = purchase.Id.ToString(),
                    Timestamp = DateTime.UtcNow
                });
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> ValidatePurchaseAsync(Purchase purchase)
    {
        if (purchase.AmountExcludingVat <= 0)
        {
            return ServiceResult.Fail("Amount excluding VAT must be greater than zero.");
        }

        if (purchase.VatAmount < 0)
        {
            return ServiceResult.Fail("VAT amount cannot be negative.");
        }

        if (purchase.PurchaseOriginTypeId < 1 || purchase.PurchaseOriginTypeId > 4)
        {
            return ServiceResult.Fail("Invalid purchase origin type.");
        }

        if (purchase.PurchaseTypeId < 1 || purchase.PurchaseTypeId > 3)
        {
            return ServiceResult.Fail("Purchase type is required. Select Asset, Stock, or Expense.");
        }

        // Validate Country requirement for EU RC, Non-EU, and EU Paid
        if (purchase.PurchaseOriginTypeId == 2 && string.IsNullOrWhiteSpace(purchase.Country))
        {
            return ServiceResult.Fail("Country is required for EU Reverse Charge transactions.");
        }

        if (purchase.PurchaseOriginTypeId == 3 && string.IsNullOrWhiteSpace(purchase.Country))
        {
            return ServiceResult.Fail("Country is required for Non-EU purchases.");
        }

        if (purchase.PurchaseOriginTypeId == 4 && string.IsNullOrWhiteSpace(purchase.Country))
        {
            return ServiceResult.Fail("Country is required for EU Paid purchases.");
        }

        // Validate SupplierId references an active supplier belonging to the current tenant
        var supplier = await _supplierRepository.GetByIdAndBusinessIdAsync(purchase.SupplierId, _currentTenantService.CurrentBusinessId);
        if (supplier == null || !supplier.IsActive)
        {
            return ServiceResult.Fail("Selected supplier is not active or does not exist.");
        }

        // Validate ExpenseCategoryId references an active expense category belonging to the current tenant
        var expenseCategory = await _expenseCategoryRepository.GetByIdAndBusinessIdAsync(purchase.ExpenseCategoryId, _currentTenantService.CurrentBusinessId);
        if (expenseCategory == null || !expenseCategory.IsActive)
        {
            return ServiceResult.Fail("Selected expense category is not active or does not exist.");
        }

        return ServiceResult.Ok();
    }

    private static void ApplyOriginTypeLogic(Purchase purchase)
    {
        switch (purchase.PurchaseOriginTypeId)
        {
            case 2: // EU Reverse Charge — force VatAmount to 0, TotalAmount = AmountExcludingVat
                purchase.VatAmount = 0;
                purchase.TotalAmount = purchase.AmountExcludingVat;
                break;

            case 1: // Domestic — preserve VatAmount, compute TotalAmount
            case 3: // Non-EU — preserve VatAmount, compute TotalAmount
            case 4: // EU Paid — preserve VatAmount, compute TotalAmount (same as Domestic/Non-EU)
            default:
                purchase.TotalAmount = purchase.AmountExcludingVat + purchase.VatAmount;
                break;
        }
    }

    /// <summary>
    /// Auto-assigns a purchase to the appropriate VAT submission period.
    /// 1. Find the period whose date range contains the InvoiceDate
    /// 2. If that period's submission is already submitted → assign to the next unsubmitted period
    /// 3. If no matching period exists → leave VatSubmissionPeriodId as null
    /// </summary>
    private async Task AssignVatPeriodAsync(Purchase purchase)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Find the period whose date range contains the InvoiceDate
        var matchingPeriod = await _portalDbContext.VatSubmissionPeriods
            .Where(p => p.BusinessId == businessId
                && p.PeriodStartDate <= purchase.InvoiceDate
                && p.PeriodEndDate >= purchase.InvoiceDate)
            .FirstOrDefaultAsync();

        if (matchingPeriod == null)
        {
            // No period covers this date — leave unassigned
            purchase.VatSubmissionPeriodId = null;
            return;
        }

        // Check if that period's submission is already submitted
        var submission = await _portalDbContext.VatSubmissions
            .Where(s => s.BusinessId == businessId
                && s.VatSubmissionPeriodId == matchingPeriod.Id
                && s.IsSubmitted)
            .FirstOrDefaultAsync();

        if (submission == null)
        {
            // Period is not yet submitted — assign to it
            purchase.VatSubmissionPeriodId = matchingPeriod.Id;
            return;
        }

        // Period is already submitted — find the next unsubmitted period
        var nextPeriod = await _portalDbContext.VatSubmissionPeriods
            .Where(p => p.BusinessId == businessId
                && p.PeriodStartDate > matchingPeriod.PeriodEndDate)
            .OrderBy(p => p.PeriodStartDate)
            .FirstOrDefaultAsync();

        if (nextPeriod != null)
        {
            // Check if the next period is also submitted — keep looking
            var nextSubmission = await _portalDbContext.VatSubmissions
                .Where(s => s.BusinessId == businessId
                    && s.VatSubmissionPeriodId == nextPeriod.Id
                    && s.IsSubmitted)
                .FirstOrDefaultAsync();

            if (nextSubmission == null)
            {
                purchase.VatSubmissionPeriodId = nextPeriod.Id;
                return;
            }

            // Find the first unsubmitted period after the matching one
            var unsubmittedPeriod = await _portalDbContext.VatSubmissionPeriods
                .Where(p => p.BusinessId == businessId
                    && p.PeriodStartDate > matchingPeriod.PeriodEndDate
                    && !_portalDbContext.VatSubmissions.Any(s =>
                        s.BusinessId == businessId
                        && s.VatSubmissionPeriodId == p.Id
                        && s.IsSubmitted))
                .OrderBy(p => p.PeriodStartDate)
                .FirstOrDefaultAsync();

            purchase.VatSubmissionPeriodId = unsubmittedPeriod?.Id;
        }
        else
        {
            // No next period exists — leave unassigned for now
            purchase.VatSubmissionPeriodId = null;
        }
    }
}
